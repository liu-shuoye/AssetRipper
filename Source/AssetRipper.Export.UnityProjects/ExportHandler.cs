using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Diagnostics.Memory;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.PathIdMapping;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Structure;
using AssetRipper.Logging;
using AssetRipper.Processing;
using AssetRipper.Processing.AnimatorControllers;
using AssetRipper.Processing.Assemblies;
using AssetRipper.Processing.AudioMixers;
using AssetRipper.Processing.Configuration;
using AssetRipper.Processing.Editor;
using AssetRipper.Processing.Prefabs;
using AssetRipper.Processing.Scenes;
using AssetRipper.Processing.ScriptableObject;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Extensions.SourceGenerator;
using System;
using System.Linq;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// 导出处理器
/// </summary>
/// <param name="settings"></param>
public class ExportHandler(FullConfiguration settings)
{
	protected FullConfiguration Settings { get; } = settings;

	/// <summary>
	/// 内存诊断入口：把当前 GameData 的资产集合（惰性求值，拆解时才真正枚举）交给 <see cref="Logger"/>，
	/// 口径取自设置 <see cref="ImportSettings.MemoryBreakdownMode"/>。
	/// 只有默认／live 口径需要资产集合，<c>clrmd</c> 口径用不到但传了也无害。
	/// </summary>
	private void LogMemoryDiagnostics(GameData gameData, string stage)
	{
		MemoryDiagnostics.LogMemoryDiagnostics(stage, gameData.GameBundle.FetchAssetCollections(), GetBreakdownModeString(Settings.ImportSettings.MemoryBreakdownMode));
	}

	/// <summary>把设置里的口径枚举翻译成内存诊断内部使用的模式串（live/clrmd，空串表示默认口径）。</summary>
	private static string GetBreakdownModeString(MemoryBreakdownMode mode) => mode switch
	{
		MemoryBreakdownMode.Live => "live",
		MemoryBreakdownMode.ClrMd => "clrmd",
		_ => string.Empty,
	};

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
		LogMemoryDiagnostics(gameData, "Process开始");
		foreach (IAssetProcessor processor in GetProcessors())
		{
			string processorName = processor.GetType().Name;
			LogMemoryDiagnostics(gameData, $"Process前 - {processorName}");
			processor.Process(gameData);
			LogMemoryDiagnostics(gameData, $"Process后 - {processorName}");
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
		yield return new LightingDataProcessor(); //需要在静态网格分离之后进行
		yield return new PrefabProcessor();
		yield return new SpriteProcessor();
		yield return new ScriptableObjectProcessor();
		if (Settings.ProcessingSettings.BundledAssetsExportMode == BundledAssetsExportMode.MainAssetFolder)
		{
			// 必须排在所有会设置 MainAsset 的处理器（PrefabProcessor / SpriteProcessor / ScriptableObjectProcessor）之后，
			// 否则读到的主资产还是 null，无法据此分配导出目录。
			yield return new MainAssetFolderProcessor();
		}
	}

	/// <summary> 导出 </summary>
	public void Export(GameData gameData, string outputPath, FileSystem fileSystem)
	{
		Logger.Info(LogCategory.Export, "开始导出");
		Logger.Info(LogCategory.Export, $"尝试将资产导出到 {outputPath}...");
		Logger.Info(LogCategory.Export, $"游戏文件包含以下 Unity 版本：{GetListOfVersions(gameData.GameBundle)}");
		Logger.Info(LogCategory.Export, $"导出到 Unity 版本 {gameData.ProjectVersion}");

		// P1-1：进入导出前主动做一次 LOH 压缩。Process 阶段（尤其修复前的全量物化）会留下大量
		// 大对象碎片（整堆快照实测 Free 碎片 8.5 GB / 16.5%），此刻压缩既能回收碎片，
		// 又能让 Server GC 把不再需要的堆段归还给 OS，为导出期 FastPng 等原生 2N 缓冲分配腾出可提交内存。
		// 这是 export 前唯一一次主动压缩；后续 OOM 分支的降压由 ProjectExporter.Export 内的熔断逻辑负责。
		Logger.Info(LogCategory.Export, "导出前内存整理（LOH 压缩）...");
		long heapBeforeMb = GC.GetTotalMemory(false) / 1024 / 1024;
		long workingSetBeforeMb = Environment.WorkingSet / 1024 / 1024;
		ProjectExporter.RelieveMemoryPressure();
		long heapAfterMb = GC.GetTotalMemory(false) / 1024 / 1024;
		long workingSetAfterMb = Environment.WorkingSet / 1024 / 1024;
		Logger.Info(LogCategory.Export,
			$"内存整理完成：托管堆 {heapBeforeMb:N0} MB → {heapAfterMb:N0} MB，工作集 {workingSetBeforeMb:N0} MB → {workingSetAfterMb:N0} MB。");

		Settings.ExportRootPath = outputPath;
		Settings.SetProjectSettings(gameData.ProjectVersion);

		LogMemoryDiagnostics(gameData, "Export前 - 创建ProjectExporter");
		ProjectExporter projectExporter = new(Settings, gameData.AssemblyManager);
		BeforeExport(projectExporter);
		projectExporter.DoFinalOverrides(Settings);
		LogMemoryDiagnostics(gameData, "Export前 - DoFinalOverrides完成");
		projectExporter.Export(gameData.GameBundle, Settings, fileSystem);
		LogMemoryDiagnostics(gameData, "Export后 - 主导出完成");

		Logger.Info(LogCategory.Export, "资产导出完成");

		foreach (IPostExporter postExporter in GetPostExporters())
		{
			string postExporterName = postExporter.GetType().Name;
			LogMemoryDiagnostics(gameData, $"PostExport前 - {postExporterName}");
			postExporter.DoPostExport(gameData, Settings, fileSystem);
			LogMemoryDiagnostics(gameData, $"PostExport后 - {postExporterName}");
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
		// 分两步加载：先 Load（不触发反序列化），再 Process（触发反序列化）
		// 中间输出内存诊断，用于验证懒加载效果
		GameData gameData = Load(paths, fileSystem);
		LogMemoryDiagnostics(gameData, "Load完成（懒加载，资产未反序列化）");
		MemoryDiagnostics.LogResourceBreakdown(gameData.GameBundle.FetchAssetCollections(), "Load完成（未反序列化）", GetBreakdownModeString(Settings.ImportSettings.MemoryBreakdownMode));
		if (gameData.GameBundle.HasAnyAssetCollections())
		{
			Process(gameData);
		}

		LogMemoryDiagnostics(gameData, "Process完成（资产已反序列化）");
		MemoryDiagnostics.LogResourceBreakdown(gameData.GameBundle.FetchAssetCollections(), "Process完成（已反序列化）", GetBreakdownModeString(Settings.ImportSettings.MemoryBreakdownMode));

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
