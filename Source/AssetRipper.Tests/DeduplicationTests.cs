using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Export.UnityProjects.Shaders;
using AssetRipper.Export.UnityProjects.Textures;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing.Configuration;
using AssetRipper.Processing.Prefabs;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using System.Reflection;

namespace AssetRipper.Tests;

internal class DeduplicationTests
{
	/// <summary>
	/// 去重仅对 shader 与图片资源生效：内容相同的两个图片（Texture2D）集合只保留一个。
	/// </summary>
	[Test]
	public void Deduplication_IdenticalImageCollections_KeepsSingleCopy()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);

		ITexture2D first = CreateTexture(collection, "DuplicateTexture");
		ITexture2D second = CreateTexture(collection, "DuplicateTexture");

		(_, HashSet<IExportCollection> skipped, Dictionary<IUnityObjectBase, IUnityObjectBase> redirect) =
			RunApplyDeduplication(CreateTextureCollection(first), CreateTextureCollection(second));

		Assert.Multiple(() =>
		{
			Assert.That(skipped, Has.Count.EqualTo(1), "内容相同的两个图片集合应只保留一个");
			Assert.That(redirect, Has.Count.EqualTo(1), "被跳过的贴图必须重定向到保留的贴图");
		});
	}

	/// <summary>
	/// 主资源（贴图）内容相同但子资源不同 → 不得合并删除，两个集合都应保留：
	/// 子资源会随被跳过集合一起消失。
	/// </summary>
	[Test]
	public void Deduplication_SameTextureButDifferentSubAsset_DoesNotDeduplicate()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);

		IMonoBehaviour firstSub = collection.CreateMonoBehaviour();
		firstSub.Name = "SubAsset";
		IMonoBehaviour secondSub = collection.CreateMonoBehaviour();
		secondSub.Name = "OtherSubAsset";

		StubAssetsCollection<ITexture2D> first = CreateTextureCollection(CreateTexture(collection, "DuplicateTexture"));
		first.AddSubAsset(firstSub);
		StubAssetsCollection<ITexture2D> second = CreateTextureCollection(CreateTexture(collection, "DuplicateTexture"));
		second.AddSubAsset(secondSub);

		(_, HashSet<IExportCollection> skipped, _) = RunApplyDeduplication(first, second);

		Assert.That(skipped, Is.Empty, "主资源相同但子资源不同的集合不得被去重");
	}

	/// <summary>
	/// 非 shader/图片资源（如 MonoBehaviour）不参与去重：即使内容完全相同也各自导出。
	/// </summary>
	[Test]
	public void Deduplication_NonShaderOrImageAssets_AreNotDeduplicated()
	{
		ProcessedAssetCollection collection = AssetCreator.CreateCollection(UnityVersion.V_2022);

		IMonoBehaviour first = collection.CreateMonoBehaviour();
		first.Name = "DuplicateAsset";
		IMonoBehaviour second = collection.CreateMonoBehaviour();
		second.Name = "DuplicateAsset";

		ScriptableObjectExporter exporter = new();
		(_, HashSet<IExportCollection> skipped, _) = RunApplyDeduplication(
			new StubAssetsCollection<IMonoBehaviour>(exporter, first),
			new StubAssetsCollection<IMonoBehaviour>(exporter, second));

		Assert.That(skipped, Is.Empty, "去重只对 shader 与图片资源生效");
	}

	/// <summary>
	/// 端到端：启用去重时，非 shader/图片资源（MonoBehaviour）的重复副本仍全部导出。
	/// </summary>
	[Test]
	public void Deduplication_WhenEnabled_DoesNotDeduplicateNonShaderOrImageAssets()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("TestCollection", UnityVersion.V_2022);

		IMonoBehaviour asset1 = collection.CreateMonoBehaviour();
		asset1.Name = "DuplicateAsset";
		IMonoBehaviour asset2 = collection.CreateMonoBehaviour();
		asset2.Name = "DuplicateAsset";

		VirtualFileSystem fileSystem = ExportWithDeduplication(gameBundle, UnityVersion.V_2022, enableDeduplication: true);

		string assetDir = "/output/ExportedProject/Assets/MonoBehaviour";
		string[] assetFiles = fileSystem.Directory.GetFiles(assetDir, "*.asset")
			.Where(f => !f.EndsWith(".meta"))
			.ToArray();

		Assert.Multiple(() =>
		{
			Assert.That(fileSystem.File.Exists($"{assetDir}/DuplicateAsset.asset"), Is.True);
			Assert.That(assetFiles, Has.Length.EqualTo(2), "非 shader/图片资源的重复副本不应被去重");
		});
	}

	/// <summary>
	/// SubTask 7.3: When deduplication is disabled, both copies are exported.
	/// </summary>
	[Test]
	public void Deduplication_WhenDisabled_ExportsAllCopies()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("TestCollection", UnityVersion.V_2022);

		IMonoBehaviour asset1 = collection.CreateMonoBehaviour();
		asset1.Name = "DuplicateAsset";
		IMonoBehaviour asset2 = collection.CreateMonoBehaviour();
		asset2.Name = "DuplicateAsset";

		VirtualFileSystem fileSystem = ExportWithDeduplication(gameBundle, UnityVersion.V_2022, enableDeduplication: false);

		string assetDir = "/output/ExportedProject/Assets/MonoBehaviour";
		string[] assetFiles = fileSystem.Directory.GetFiles(assetDir, "*.asset")
			.Where(f => !f.EndsWith(".meta"))
			.ToArray();

		Assert.Multiple(() =>
		{
			Assert.That(fileSystem.File.Exists($"{assetDir}/DuplicateAsset.asset"), Is.True);
			Assert.That(assetFiles, Has.Length.EqualTo(2));
		});
	}

	/// <summary>
	/// SubTask 7.2: When a skipped asset is queried through ProjectAssetContainer, its export id and
	/// pointer are redirected to the kept asset via redirectMap.
	/// </summary>
	[Test]
	public void ProjectAssetContainer_RedirectsSkippedAssetToKeptAsset()
	{
		UnityVersion version = UnityVersion.V_2022;
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("ContainerTest", version);

		IMonoBehaviour keptAsset = collection.CreateMonoBehaviour();
		keptAsset.Name = "RedirectTarget";
		IMonoBehaviour skippedAsset = collection.CreateMonoBehaviour();
		skippedAsset.Name = "RedirectTarget";

		ScriptableObjectExporter exporter = new();
		Assert.That(exporter.TryCreateCollection(keptAsset, out IExportCollection? keptCollection), Is.True);
		Assert.That(exporter.TryCreateCollection(skippedAsset, out IExportCollection? skippedCollection), Is.True);

		HashSet<IExportCollection> skippedCollections = new() { skippedCollection! };
		Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap = new() { [skippedAsset] = keptAsset };

		FullConfiguration settings = new();
		settings.SetProjectSettings(version);
		BaseManager assemblyManager = new(_ => { });
		ProjectExporter projectExporter = new(settings, assemblyManager);

		ProjectAssetContainer container = new(projectExporter, settings, gameBundle.FetchAssets(),
			new List<IExportCollection> { keptCollection!, skippedCollection! }, skippedCollections, redirectMap);
		container.CurrentCollection = keptCollection!;

		long keptExportId = container.GetExportID(keptAsset);
		long skippedExportId = container.GetExportID(skippedAsset);

		MetaPtr keptPointer = container.CreateExportPointer(keptAsset);
		MetaPtr skippedPointer = container.CreateExportPointer(skippedAsset);

		Assert.Multiple(() =>
		{
			Assert.That(skippedExportId, Is.EqualTo(keptExportId));
			Assert.That(skippedPointer, Is.EqualTo(keptPointer));
		});
	}

	/// <summary>
	/// SubTask 7.4: SceneExportCollection is exempt from deduplication even when content is equal.
	/// </summary>
	[Test]
	public void Deduplication_DoesNotSkipSceneExportCollections()
	{
		UnityVersion version = UnityVersion.V_2022;
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("SceneTest", version);

		SceneDefinition scene1 = SceneDefinition.FromName("SameScene");
		SceneHierarchyObject hierarchy1 = collection.CreateAsset(-1, ai => new SceneHierarchyObject(ai, scene1));
		hierarchy1.SetMainAsset();

		SceneDefinition scene2 = SceneDefinition.FromName("SameScene");
		SceneHierarchyObject hierarchy2 = collection.CreateAsset(-1, ai => new SceneHierarchyObject(ai, scene2));
		hierarchy2.SetMainAsset();

		SceneYamlExporter sceneExporter = new();
		SceneExportCollection sceneCollection1 = new(sceneExporter, hierarchy1);
		SceneExportCollection sceneCollection2 = new(sceneExporter, hierarchy2);

		// 同时放一对内容相同的图片集合，保证去重逻辑确实运行过（否则"场景未被跳过"可能只是没跑）。
		StubAssetsCollection<ITexture2D> textureCollection1 =
			CreateTextureCollection(CreateTexture(collection, "DuplicateTexture"));
		StubAssetsCollection<ITexture2D> textureCollection2 =
			CreateTextureCollection(CreateTexture(collection, "DuplicateTexture"));

		(_, HashSet<IExportCollection> skippedCollections, _) =
			RunApplyDeduplication(sceneCollection1, sceneCollection2, textureCollection1, textureCollection2);

		Assert.Multiple(() =>
		{
			// 内容相同的图片集合应被跳过其中一个。
			Assert.That(skippedCollections, Has.Count.EqualTo(1));
			Assert.That(skippedCollections.Contains(textureCollection1) || skippedCollections.Contains(textureCollection2), Is.True);
			// Neither scene collection should be skipped.
			Assert.That(skippedCollections, Does.Not.Contain(sceneCollection1));
			Assert.That(skippedCollections, Does.Not.Contain(sceneCollection2));
		});
	}

	/// <summary>
	/// T1：两个"主资源与子资源内容完全相同"的多子资源集合 → 仅保留一个，且被跳过集合的
	/// 主资源与子资源都被一对一重定向到保留集合的对应资源。
	/// 去重只对 shader 与图片资源生效，因此主资源用 Texture2D 走图片分支，
	/// 子资源用轻量的 MonoBehaviour（无需贴图/精灵的构造管线）。这验证了：
	/// 对被去重集合的子资源引用不再丢失。
	/// </summary>
	[Test]
	public void Deduplication_FullyIdenticalImageCollections_RedirectsAllAssets()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("DupFullTest", UnityVersion.V_2022);

		ITexture2D keepPrimary = CreateTexture(collection, "Primary");
		IMonoBehaviour keepSub = collection.CreateMonoBehaviour();
		keepSub.Name = "Sub";

		ITexture2D skipPrimary = CreateTexture(collection, "Primary");
		IMonoBehaviour skipSub = collection.CreateMonoBehaviour();
		skipSub.Name = "Sub";

		StubAssetsCollection<ITexture2D> keep = CreateTextureCollection(keepPrimary);
		keep.AddSubAsset(keepSub);
		StubAssetsCollection<ITexture2D> skip = CreateTextureCollection(skipPrimary);
		skip.AddSubAsset(skipSub);

		(List<IExportCollection> collections,
		 HashSet<IExportCollection> skipped,
		 Dictionary<IUnityObjectBase, IUnityObjectBase> redirect) = RunApplyDeduplication(keep, skip);

		IExportCollection keepCol = skipped.Contains(keep) ? skip : keep;
		IExportCollection skipCol = keepCol == keep ? skip : keep;
		IUnityObjectBase keepPrimaryAsset = keepCol.Assets.ElementAt(0);
		IUnityObjectBase keepSubAsset = keepCol.Assets.ElementAt(1);
		IUnityObjectBase skipPrimaryAsset = skipCol.Assets.ElementAt(0);
		IUnityObjectBase skipSubAsset = skipCol.Assets.ElementAt(1);

		Assert.Multiple(() =>
		{
			// 只保留一个集合（内容完全一致的重复集）。
			Assert.That(skipped, Has.Count.EqualTo(1));
			// 主资源与子资源都必须重定向到保留集合的对应资源（修复点）。
			Assert.That(redirect[skipPrimaryAsset], Is.EqualTo(keepPrimaryAsset));
			Assert.That(redirect[skipSubAsset], Is.EqualTo(keepSubAsset));
		});

		// 通过容器验证：对被跳过集合"子资源"的引用解析结果与直接引用保留集合子资源一致。
		FullConfiguration settings = new();
		settings.SetProjectSettings(UnityVersion.V_2022);
		BaseManager assemblyManager = new(_ => { });
		ProjectExporter projectExporter = new(settings, assemblyManager);
		ProjectAssetContainer container = new(projectExporter, settings, gameBundle.FetchAssets(),
			collections, skipped, redirect);
		// 把当前集合设为第三方集合，使指针包含(guid, type)，从而同时校验 GUID 与 fileID 的一致性。
		container.CurrentCollection = keepCol == keep ? skip : keep;

		MetaPtr keptSubPointer = container.CreateExportPointer(keepSubAsset);
		MetaPtr skippedSubPointer = container.CreateExportPointer(skipSubAsset);
		MetaPtr keptPrimaryPointer = container.CreateExportPointer(keepPrimaryAsset);
		MetaPtr skippedPrimaryPointer = container.CreateExportPointer(skipPrimaryAsset);

		Assert.Multiple(() =>
		{
			Assert.That(skippedSubPointer, Is.EqualTo(keptSubPointer));
			Assert.That(skippedPrimaryPointer, Is.EqualTo(keptPrimaryPointer));
		});
	}

	/// <summary>
	/// 同名 shader 按名称去重：即使两个 shader 的序列化内容不一致，只要名称相同，
	/// 也会被识别为重复并只保留一个（避免由编译二进制差异引起的内容哈希不同而无法判重）。
	/// </summary>
	[Test]
	public void Deduplication_ShadersWithSameName_DeduplicatesByName()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("ShaderDupNameTest", UnityVersion.V_2022);

		IShader shader1 = CreateShader(collection, "Amplify/build_shader");
		IShader shader2 = CreateShader(collection, "Amplify/build_shader");

		SimpleShaderExporter exporter = new();
		ShaderExportCollection col1 = new(exporter, shader1);
		ShaderExportCollection col2 = new(exporter, shader2);

		(_, HashSet<IExportCollection> skipped, Dictionary<IUnityObjectBase, IUnityObjectBase> redirect) =
			RunApplyDeduplication(col1, col2);

		Assert.Multiple(() =>
		{
			// 两个同名 shader 应只保留一个。
			Assert.That(skipped, Has.Count.EqualTo(1));
			// 被跳过的 shader 必须重定向到保留的那个同名 shader，避免引用丢失。
			Assert.That(redirect, Has.Count.EqualTo(1));
		});
	}

	/// <summary>
	/// 同名 shader 按名称去重时，不同名的 shader 不应被误合并。
	/// </summary>
	[Test]
	public void Deduplication_ShadersWithDifferentNames_KeepsBoth()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("ShaderDupDiffNameTest", UnityVersion.V_2022);

		IShader shader1 = CreateShader(collection, "Amplify/A");
		IShader shader2 = CreateShader(collection, "Amplify/B");

		SimpleShaderExporter exporter = new();
		ShaderExportCollection col1 = new(exporter, shader1);
		ShaderExportCollection col2 = new(exporter, shader2);

		(_, HashSet<IExportCollection> skipped, _) = RunApplyDeduplication(col1, col2);

		Assert.That(skipped, Is.Empty, "不同名的 shader 不得被去重合并");
	}

	/// <summary>
	/// 构造一个指定名称的 shader 实例。名称通过 <see cref="IUnityObjectBase.OverrideName"/> 控制，
	/// 使去重测试不依赖特定的 shader 生成类型字段。
	/// </summary>
	private static IShader CreateShader(ProcessedAssetCollection collection, string name)
	{
		Type? shaderType = typeof(IShader).Assembly.GetTypes()
			.FirstOrDefault(t => typeof(IShader).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
		Assert.That(shaderType, Is.Not.Null);

		IShader shader = collection.CreateAsset(-1, ai => (IShader)Activator.CreateInstance(shaderType!, ai)!);
		shader.OverrideName = name;
		return shader;
	}

	/// <summary>
	/// 通过反射调用内部 ApplyDeduplication，返回去重决策结果。
	/// </summary>
	private static (List<IExportCollection> collections, HashSet<IExportCollection> skipped,
		Dictionary<IUnityObjectBase, IUnityObjectBase> redirect) RunApplyDeduplication(params IExportCollection[] collectionsArray)
	{
		List<IExportCollection> collections = collectionsArray.ToList();

		FullConfiguration settings = new();
		settings.SetProjectSettings(UnityVersion.V_2022);
		BaseManager assemblyManager = new(_ => { });
		ProjectExporter projectExporter = new(settings, assemblyManager);

		MethodInfo? method = typeof(ProjectExporter).GetMethod(
			"ApplyDeduplication",
			BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.That(method, Is.Not.Null);

		object[] parameters = { collections, null!, null! };
		method!.Invoke(projectExporter, parameters);
		return (collections, (HashSet<IExportCollection>)parameters[1]!, (Dictionary<IUnityObjectBase, IUnityObjectBase>)parameters[2]!);
	}

	/// <summary>
	/// 构造一个指定名称的 Texture2D 实例，代表参与去重的图片资源。
	/// </summary>
	private static ITexture2D CreateTexture(ProcessedAssetCollection collection, string name)
	{
		ITexture2D texture = collection.CreateTexture2D();
		texture.Name = name;
		return texture;
	}

	/// <summary>
	/// 以 Texture2D 为主资源构造图片集合桩。去重按主资源类型判定资格，桩集合即可走图片分支，
	/// 无需构造贴图/精灵的完整导出管线。
	/// </summary>
	private static StubAssetsCollection<ITexture2D> CreateTextureCollection(ITexture2D texture)
	{
		return new StubAssetsCollection<ITexture2D>(new TextureAssetExporter(new FullConfiguration()), texture);
	}

	/// <summary>
	/// 测试用多子资源集合桩：继承真实导出集合，通过 AddSubAsset 挂一个额外的子资源。
	/// 用于在无需纹理/精灵构造管线的前提下验证去重对"子资源"的处理。
	/// </summary>
	private sealed class StubAssetsCollection<T> : AssetsExportCollection<T> where T : IUnityObjectBase
	{
		public StubAssetsCollection(IAssetExporter exporter, T primary)
			: base(exporter, primary)
		{
		}

		public void AddSubAsset(IUnityObjectBase sub)
		{
			AddAsset(sub);
		}
	}

	private static VirtualFileSystem ExportWithDeduplication(GameBundle gameBundle, UnityVersion version, bool enableDeduplication)
	{
		FullConfiguration settings = new();
		settings.ProcessingSettings = new ProcessingSettings { EnableAssetDeduplication = enableDeduplication };
		settings.ExportRootPath = "output";
		settings.SetProjectSettings(version);

		BaseManager assemblyManager = new(_ => { });
		ProjectExporter projectExporter = new(settings, assemblyManager);
		projectExporter.DoFinalOverrides(settings);

		VirtualFileSystem fileSystem = new();
		projectExporter.Export(gameBundle, settings, fileSystem);
		return fileSystem;
	}
}
