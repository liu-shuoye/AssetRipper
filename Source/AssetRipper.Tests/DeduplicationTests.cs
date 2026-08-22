using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing.Configuration;
using AssetRipper.Processing.Prefabs;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Extensions;
using System.Reflection;

namespace AssetRipper.Tests;

internal class DeduplicationTests
{
	/// <summary>
	/// SubTask 7.1: Two content-equal assets are deduplicated when the switch is enabled.
	/// Only one exported file should remain.
	/// </summary>
	[Test]
	public void Deduplication_WhenEnabled_KeepsSingleCopyOfDuplicateAsset()
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
			Assert.That(assetFiles, Has.Length.EqualTo(1));
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

		// Also include a pair of duplicate non-scene collections to ensure the deduplication logic runs.
		IMonoBehaviour mono1 = collection.CreateMonoBehaviour();
		mono1.Name = "DuplicateMono";
		IMonoBehaviour mono2 = collection.CreateMonoBehaviour();
		mono2.Name = "DuplicateMono";
		ScriptableObjectExporter soExporter = new();
		Assert.That(soExporter.TryCreateCollection(mono1, out IExportCollection? monoCollection1), Is.True);
		Assert.That(soExporter.TryCreateCollection(mono2, out IExportCollection? monoCollection2), Is.True);

		List<IExportCollection> collections = new()
		{
			sceneCollection1,
			sceneCollection2,
			monoCollection1!,
			monoCollection2!,
		};

		FullConfiguration settings = new();
		settings.SetProjectSettings(version);
		BaseManager assemblyManager = new(_ => { });
		ProjectExporter projectExporter = new(settings, assemblyManager);

		MethodInfo? method = typeof(ProjectExporter).GetMethod(
			"ApplyDeduplication",
			BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.That(method, Is.Not.Null);

		object[] parameters = { collections, null!, null! };
		method!.Invoke(projectExporter, parameters);
		HashSet<IExportCollection> skippedCollections = (HashSet<IExportCollection>)parameters[1]!;

		Assert.Multiple(() =>
		{
			// The duplicate MonoBehaviour collection should be skipped.
			Assert.That(skippedCollections, Has.Count.EqualTo(1));
			Assert.That(skippedCollections, Contains.Item(monoCollection2!));
			// Neither scene collection should be skipped.
			Assert.That(skippedCollections, Does.Not.Contain(sceneCollection1));
			Assert.That(skippedCollections, Does.Not.Contain(sceneCollection2));
		});
	}

	/// <summary>
	/// T1：两个"主资源与子资源内容完全相同"的多子资源集合 → 仅保留一个，且被跳过集合的
	/// 主资源与子资源都被一对一重定向到保留集合的对应资源。
	/// 用轻量化并可哈希的 MonoBehaviour 充当"主资源+子资源"，因为去重逻辑与资源类型无关，
	/// 不依赖贴图/精灵的构造管线。这验证了修复：对被去重集合的子资源引用不再丢失。
	/// </summary>
	[Test]
	public void Deduplication_FullyIdenticalMultiAssetCollections_RedirectsAllAssets()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("DupFullTest", UnityVersion.V_2022);

		IMonoBehaviour keepPrimary = collection.CreateMonoBehaviour();
		keepPrimary.Name = "Primary";
		IMonoBehaviour keepSub = collection.CreateMonoBehaviour();
		keepSub.Name = "Sub";

		IMonoBehaviour skipPrimary = collection.CreateMonoBehaviour();
		skipPrimary.Name = "Primary";
		IMonoBehaviour skipSub = collection.CreateMonoBehaviour();
		skipSub.Name = "Sub";

		ScriptableObjectExporter exporter = new();
		StubAssetsCollection keep = new(exporter, keepPrimary);
		keep.AddSubAsset(keepSub);
		StubAssetsCollection skip = new(exporter, skipPrimary);
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
	/// T2：两个集合主资源内容相同但子资源不同 → 不得合并删除，两个集合都应保留。
	/// 这是为了避免"字节相同但子资源不同"的贴图被误判为重复而丢失不同子资源。
	/// </summary>
	[Test]
	public void Deduplication_SamePrimaryButDifferentSubAsset_DoesNotDeduplicate()
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("DupDiffTest", UnityVersion.V_2022);

		IMonoBehaviour keepPrimary = collection.CreateMonoBehaviour();
		keepPrimary.Name = "Primary";
		IMonoBehaviour keepSub = collection.CreateMonoBehaviour();
		keepSub.Name = "Sub";

		IMonoBehaviour skipPrimary = collection.CreateMonoBehaviour();
		skipPrimary.Name = "Primary";
		IMonoBehaviour skipSub = collection.CreateMonoBehaviour();
		skipSub.Name = "DifferentSub";

		ScriptableObjectExporter exporter = new();
		StubAssetsCollection first = new(exporter, keepPrimary);
		first.AddSubAsset(keepSub);
		StubAssetsCollection second = new(exporter, skipPrimary);
		second.AddSubAsset(skipSub);

		(_, HashSet<IExportCollection> skipped, _) = RunApplyDeduplication(first, second);

		Assert.That(skipped, Is.Empty, "主资源相同但子资源不同的集合不得被去重");
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
	/// 测试用多子资源集合桩：继承真实导出集合，通过 AddAsset 挂一个额外的子资源。
	/// 用于在无需纹理/精灵物理管线的前提下验证去重对"子资源"的处理。
	/// </summary>
	private sealed class StubAssetsCollection : AssetsExportCollection<IMonoBehaviour>
	{
		public StubAssetsCollection(ScriptableObjectExporter exporter, IMonoBehaviour primary)
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
