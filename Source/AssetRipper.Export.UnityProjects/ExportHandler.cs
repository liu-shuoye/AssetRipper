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
public class ExportHandler(FullConfiguration settings)
{
	protected FullConfiguration Settings { get; } = settings;

	public GameData Load(IReadOnlyList<string> paths, FileSystem fileSystem)
	{
		if (paths.Count == 1)
		{
			Logger.Info(LogCategory.Import, $"尝试从 {paths[0]} 读取文件");
		}
		else
		{
			Logger.Info(LogCategory.Import, $"尝试从 {paths.Count} 个路径读取文件……");
		}

		GameStructure gameStructure = GameStructure.Load(paths, fileSystem, Settings);
		GameData gameData = GameData.FromGameStructure(gameStructure);
		Logger.Info(LogCategory.Import, "已读完文件");
		return gameData;
	}

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "正在处理加载的资产...");
		foreach (IAssetProcessor processor in GetProcessors())
		{
			processor.Process(gameData);
		}
		Logger.Info(LogCategory.Processing, "已处理完资产");
	}

	/// <summary> 获取处理器 </summary>
	protected virtual IEnumerable<IAssetProcessor> GetProcessors()
	{
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

		// 资产处理器
		yield return new SceneDefinitionProcessor();
		yield return new OriginalPathProcessor(Settings.ProcessingSettings.BundledAssetsExportMode);
		yield return new MainAssetProcessor();
		yield return new AnimatorControllerProcessor();
		yield return new AudioMixerProcessor();
		yield return new EditorFormatProcessor(Settings.ProcessingSettings.BundledAssetsExportMode);
		// 静态网格分离 在这里
		yield return new LightingDataProcessor();//需要在静态网格分离之后进行
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

		ProjectExporter projectExporter = new(Settings, gameData.AssemblyManager);
		BeforeExport(projectExporter);
		projectExporter.DoFinalOverrides(Settings);
		projectExporter.Export(gameData.GameBundle, Settings, fileSystem);

		Logger.Info(LogCategory.Export, "资产导出完成");

		foreach (IPostExporter postExporter in GetPostExporters())
		{
			postExporter.DoPostExport(gameData, Settings, fileSystem);
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
	protected virtual void BeforeExport(ProjectExporter projectExporter)
	{
		// 高级版所需
	}

	/// <summary> 获取导出之后的处理器 </summary>
	protected virtual IEnumerable<IPostExporter> GetPostExporters()
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
		GameData gameData = Load(paths, fileSystem);
		if (gameData.GameBundle.HasAnyAssetCollections())
		{
			Process(gameData);
		}
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
