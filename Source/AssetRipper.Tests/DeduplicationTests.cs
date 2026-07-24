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

		ProjectAssetContainer container = new(projectExporter, settings, gameBundle.FetchAssetCollections(),
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
