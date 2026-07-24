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

/// <summary>
/// 导出处理器
/// </summary>
/// <param name="settings"></param>
public sealed class ExportHandler(FullConfiguration settings)
{
	private FullConfiguration Settings { get; } = settings;
	private EditorFormatProcessor EditorFormatProcessor { get; } = new(settings.ProcessingSettings.BundledAssetsExportMode);

	public GameData Load(IReadOnlyList<string> paths, FileSystem fileSystem)
	{
		Logger.Info(LogCategory.Import, paths.Count == 1 ? $"尝试从 {paths[0]} 读取文件" : $"尝试从 {paths.Count} 个路径读取文件……");

		GameStructure gameStructure = GameStructure.Load(paths, fileSystem, Settings);
		GameData gameData = GameData.FromGameStructure(gameStructure);
		Logger.Info(LogCategory.Import, "已读完文件");
		return gameData;
	}

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "正在处理加载的资产...");
		Logger.LogMemoryDiagnostics("Process开始");
		foreach (IAssetProcessor processor in GetProcessors())
		{
			string processorName = processor.GetType().Name;
			Logger.LogMemoryDiagnostics($"Process前 - {processorName}");
			processor.Process(gameData);
			Logger.LogMemoryDiagnostics($"Process后 - {processorName}");
		}

		Logger.Info(LogCategory.Processing, "已处理完资产");
	}


	/// <summary> 获取处理器 </summary>
	private IEnumerable<IAssetProcessor> GetProcessors()
	{
		Logger.Info(LogCategory.Processing, "汇编处理器");
		// 汇编处理器
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

		Logger.Info(LogCategory.Processing, "资产处理器");
		// 资产处理器
		yield return new SceneDefinitionProcessor();
		yield return new OriginalPathProcessor(Settings.ProcessingSettings.BundledAssetsExportMode);
		yield return new MainAssetProcessor();
		yield return new AnimatorControllerProcessor();
		yield return new AudioMixerProcessor();
		yield return EditorFormatProcessor;
		// 静态网格分离 在这里
		yield return new LightingDataProcessor(); //需要在静态网格分离之后进行
		yield return new PrefabProcessor();
		yield return new SpriteProcessor();
		yield return new ScriptableObjectProcessor();
	}

	/// <summary> 导出 </summary>
	public void Export(GameData gameData, string outputPath, FileSystem fileSystem)
	{
		Logger.Info(LogCategory.Export, "开始导出");
		Logger.Info(LogCategory.Export, $"尝试将资产导出到 {outputPath}...");
		Logger.Info(LogCategory.Export, $"游戏文件包含以下 Unity 版本：{GetListOfVersions(gameData.GameBundle)}");
		Logger.Info(LogCategory.Export, $"导出到 Unity 版本 {gameData.ProjectVersion}");

		Settings.ExportRootPath = outputPath;
		Settings.SetProjectSettings(gameData.ProjectVersion);

		Logger.LogMemoryDiagnostics("Export前 - 创建ProjectExporter");
		ProjectExporter projectExporter = new(Settings, gameData.AssemblyManager);
		BeforeExport(projectExporter);
		projectExporter.DoFinalOverrides(Settings);
		Logger.LogMemoryDiagnostics("Export前 - DoFinalOverrides完成");

		// 为导出阶段准备 EditorFormatProcessor：重建 tagManager 与 assemblyManager 依赖。
		// Process 阶段已处理破坏性内存优化（AnimationClip、AssetBundle），此处仅准备导出阶段所需的依赖，
		// 让 ProjectYamlWalker 在 WalkEditor 之前能按需对单个资产调用 ProcessForExport。
		EditorFormatProcessor.PrepareForExport(gameData);
		Logger.LogMemoryDiagnostics("Export前 - EditorFormatProcessor.PrepareForExport完成");

		projectExporter.Export(gameData.GameBundle, Settings, fileSystem, EditorFormatProcessor);
		Logger.LogMemoryDiagnostics("Export后 - 主导出完成");

		Logger.Info(LogCategory.Export, "资产导出完成");

		foreach (IPostExporter postExporter in GetPostExporters())
		{
			string postExporterName = postExporter.GetType().Name;
			Logger.LogMemoryDiagnostics($"PostExport前 - {postExporterName}");
			postExporter.DoPostExport(gameData, Settings, fileSystem);
			Logger.LogMemoryDiagnostics($"PostExport后 - {postExporterName}");
		}

		Logger.Info(LogCategory.Export, "导出完成之后");

		static string GetListOfVersions(GameBundle gameBundle)
		{
			return string.Join(' ', gameBundle
				.FetchAssetCollections()
				.Select(c => c.Version)
				.Distinct()
				.Select(v => v.ToString()));
		}
	}

	/// <summary> 导出之前 </summary>
	private void BeforeExport(ProjectExporter projectExporter)
	{
		// 高级版所需
	}

	/// <summary> 获取导出之后的处理器 </summary>
	private IEnumerable<IPostExporter> GetPostExporters()
	{
		yield return new ProjectVersionPostExporter();
		yield return new PackageManifestPostExporter();
		yield return new StreamingAssetsPostExporter();
		yield return new DllPostExporter();
		yield return new PathIdMapExporter();
	}

	/// <summary> 加载并处理 </summary>
	public GameData LoadAndProcess(IReadOnlyList<string> paths, FileSystem fileSystem)
	{
		// 分两步加载：先 Load（不触发反序列化），再 Process（触发反序列化）
		// 中间输出内存诊断，用于验证懒加载效果
		GameData gameData = Load(paths, fileSystem);
		Logger.LogMemoryDiagnostics("Load完成（懒加载，资产未反序列化）");
		if (gameData.GameBundle.HasAnyAssetCollections())
		{
			Process(gameData);
		}

		Logger.LogMemoryDiagnostics("Process完成（资产已反序列化）");

		return gameData;
	}

	/// <summary> 加载处理并导出 </summary>
	public void LoadProcessAndExport(IReadOnlyList<string> inputPaths, string outputPath, FileSystem fileSystem)
	{
		GameData gameData = LoadAndProcess(inputPaths, fileSystem);
		Export(gameData, outputPath, fileSystem);
	}

	/// <summary> 检查设置是否匹配 </summary>
	public void ThrowIfSettingsDontMatch(FullConfiguration settings)
	{
		if (Settings != settings)
		{
			throw new ArgumentException("Settings don't match");
		}
	}
}
