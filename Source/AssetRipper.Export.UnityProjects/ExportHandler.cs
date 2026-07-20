using AssetRipper.Assets.Bundles;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.PathIdMapping;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure;
using AssetRipper.Processing;
using AssetRipper.Processing.AnimatorControllers;
using AssetRipper.Processing.Assemblies;
using AssetRipper.Processing.AudioMixers;
using AssetRipper.Processing.Editor;
using AssetRipper.Processing.Prefabs;
using AssetRipper.Processing.Scenes;
using AssetRipper.Processing.ScriptableObject;
using AssetRipper.Processing.Textures;

namespace AssetRipper.Export.UnityProjects;

public class ExportHandler
{
	protected FullConfiguration Settings { get; }

	public ExportHandler(FullConfiguration settings)
	{
		Settings = settings;
	}

	public GameData Load(IReadOnlyList<string> paths, FileSystem fileSystem)
	{
		if (paths.Count == 1)
		{
			Logger.Info(LogCategory.Import, $"Attempting to read files from {paths[0]}");
		}
		else
		{
			Logger.Info(LogCategory.Import, $"Attempting to read files from {paths.Count} paths...");
		}

		GameStructure gameStructure = GameStructure.Load(paths, fileSystem, Settings);
		GameData gameData = GameData.FromGameStructure(gameStructure);
		Logger.Info(LogCategory.Import, "Finished reading files");
		return gameData;
	}

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "Processing loaded assets...");
		LogMemoryDiagnostics("Process开始");
		foreach (IAssetProcessor processor in GetProcessors())
		{
			string processorName = processor.GetType().Name;
			LogMemoryDiagnostics($"Process前 - {processorName}");
			processor.Process(gameData);
			LogMemoryDiagnostics($"Process后 - {processorName}");
		}
		Logger.Info(LogCategory.Processing, "Finished processing assets");
	}

	/// <summary>
	/// 输出当前内存状态，用于定位哪个阶段内存上涨最多。
	/// </summary>
	private static void LogMemoryDiagnostics(string stage)
	{
		// 强制 GC 后再统计，排除已可回收但未回收的对象干扰
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long managedMemory = GC.GetTotalMemory(false);
		long workingSet = Environment.WorkingSet;
		Logger.Info(LogCategory.Processing, $"[内存诊断] {stage}: 托管: {managedMemory / 1024.0 / 1024.0:F1} MB | 工作集: {workingSet / 1024.0 / 1024.0:F1} MB");
	}

	protected virtual IEnumerable<IAssetProcessor> GetProcessors()
	{
		// Assembly processors
		yield return new AttributePolyfillGenerator();
		yield return new MonoExplicitPropertyRepairProcessor();
		yield return new ObfuscationRepairProcessor();
		yield return new ForwardingAssemblyGenerator();
		if (Settings.ImportSettings.ScriptContentLevel == ScriptContentLevel.Level1)
		{
			yield return new MethodStubbingProcessor();
		}
		yield return new NullRefReturnProcessor(Settings.ImportSettings.ScriptContentLevel);
		yield return new UnmanagedConstraintRecoveryProcessor();
		if (Settings.ProcessingSettings.RemoveNullableAttributes)
		{
			yield return new NullableRemovalProcessor();
		}
		if (Settings.ProcessingSettings.PublicizeAssemblies)
		{
			yield return new SafeAssemblyPublicizingProcessor();
		}
		yield return new RemoveAssemblyKeyFileAttributeProcessor();
		yield return new InternalsVisibileToPublicKeyRemover();

		// Asset processors
		yield return new SceneDefinitionProcessor();
		yield return new OriginalPathProcessor(Settings.ProcessingSettings.BundledAssetsExportMode);
		yield return new MainAssetProcessor();
		yield return new AnimatorControllerProcessor();
		yield return new AudioMixerProcessor();
		yield return new EditorFormatProcessor(Settings.ProcessingSettings.BundledAssetsExportMode);
		//Static mesh separation goes here
		yield return new LightingDataProcessor();//Needs to be after static mesh separation
		yield return new PrefabProcessor();
		yield return new SpriteProcessor();
		yield return new ScriptableObjectProcessor();
	}

	public void Export(GameData gameData, string outputPath, FileSystem fileSystem)
	{
		Logger.Info(LogCategory.Export, "Starting export");
		Logger.Info(LogCategory.Export, $"Attempting to export assets to {outputPath}...");
		Logger.Info(LogCategory.Export, $"Game files have these Unity versions: {GetListOfVersions(gameData.GameBundle)}");
		Logger.Info(LogCategory.Export, $"Exporting to Unity version {gameData.ProjectVersion}");

		Settings.ExportRootPath = outputPath;
		Settings.SetProjectSettings(gameData.ProjectVersion);

		LogMemoryDiagnostics("Export前 - 创建ProjectExporter");
		ProjectExporter projectExporter = new(Settings, gameData.AssemblyManager);
		BeforeExport(projectExporter);
		projectExporter.DoFinalOverrides(Settings);
		LogMemoryDiagnostics("Export前 - DoFinalOverrides完成");
		projectExporter.Export(gameData.GameBundle, Settings, fileSystem);
		LogMemoryDiagnostics("Export后 - 主导出完成");

		Logger.Info(LogCategory.Export, "Finished exporting assets");

		foreach (IPostExporter postExporter in GetPostExporters())
		{
			string postExporterName = postExporter.GetType().Name;
			LogMemoryDiagnostics($"PostExport前 - {postExporterName}");
			postExporter.DoPostExport(gameData, Settings, fileSystem);
			LogMemoryDiagnostics($"PostExport后 - {postExporterName}");
		}
		Logger.Info(LogCategory.Export, "Finished post-export");

		static string GetListOfVersions(GameBundle gameBundle)
		{
			return string.Join(' ', gameBundle
				.FetchAssetCollections()
				.Select(c => c.Version)
				.Distinct()
				.Select(v => v.ToString()));
		}
	}

	protected virtual void BeforeExport(ProjectExporter projectExporter)
	{
		// Needed for the premium edition
	}

	protected virtual IEnumerable<IPostExporter> GetPostExporters()
	{
		yield return new ProjectVersionPostExporter();
		yield return new PackageManifestPostExporter();
		yield return new StreamingAssetsPostExporter();
		yield return new DllPostExporter();
		yield return new PathIdMapExporter();
	}

	public GameData LoadAndProcess(IReadOnlyList<string> paths, FileSystem fileSystem)
	{
		GameData gameData = Load(paths, fileSystem);
		if (gameData.GameBundle.HasAnyAssetCollections())
		{
			Process(gameData);
		}
		return gameData;
	}

	public void LoadProcessAndExport(IReadOnlyList<string> inputPaths, string outputPath, FileSystem fileSystem)
	{
		GameData gameData = LoadAndProcess(inputPaths, fileSystem);
		Export(gameData, outputPath, fileSystem);
	}

	public void ThrowIfSettingsDontMatch(FullConfiguration settings)
	{
		if (Settings != settings)
		{
			throw new ArgumentException("Settings don't match");
		}
	}
}
