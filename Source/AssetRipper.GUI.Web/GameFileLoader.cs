using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.NativeDialogs;
using AssetRipper.Processing;

namespace AssetRipper.GUI.Web;

public static class GameFileLoader
{
	private static GameData? GameData { get; set; }
	[MemberNotNullWhen(true, nameof(GameData))]
	public static bool IsLoaded => GameData is not null;
	public static GameBundle GameBundle => GameData!.GameBundle;
	public static IAssemblyManager AssemblyManager => GameData!.AssemblyManager;
	public static FullConfiguration Settings { get; } = LoadSettings();
	public static bool Headless { get; set; }

	public static ExportHandler ExportHandler
	{
		private get;
		set
		{
			ArgumentNullException.ThrowIfNull(value);
			value.ThrowIfSettingsDontMatch(Settings);
			field = value;
		}
	} = new(Settings);

	/// <summary>
	/// Is this the premium edition?
	/// </summary>
	/// <remarks>
	/// This is purely for UI functionality and has no direct effect on the presense of features.
	/// </remarks>
	public static bool Premium => ExportHandler.GetType() != typeof(ExportHandler);

	public static void Reset()
	{
		if (GameData is not null)
		{
			try
			{
				// 显式释放 Bundle 持有的所有非托管资源（FileStream、AssetCollection 字典等）
				GameData.GameBundle.Dispose();
			}
			catch (Exception ex)
			{
				// 即使 Dispose 抛异常也不能阻塞重置流程，否则用户无法重新加载
				Logger.Error(LogCategory.General, $"Error during GameBundle disposal: {ex}");
			}
			GameData = null;
			// 双轮 GC：第一轮触发 finalizer，第二轮回收 finalizer 释放的对象
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			Logger.Info(LogCategory.General, "Data was reset.");
		}
	}

	public static void LoadAndProcess(IReadOnlyList<string> paths)
	{
		Reset();
		Settings.LogConfigurationValues();
		// 分两步加载：先 Load（不触发反序列化），再 Process（触发反序列化）
		// 中间输出内存诊断，用于验证懒加载效果
		GameData = ExportHandler.Load(paths, LocalFileSystem.Instance);
		LogMemoryDiagnostics("Load完成（懒加载，资产未反序列化）");
		if (GameData.GameBundle.HasAnyAssetCollections())
		{
			ExportHandler.Process(GameData);
		}
		LogMemoryDiagnostics("Process完成（资产已反序列化）");
	}

	/// <summary>
	/// 输出当前内存状态，用于在不同阶段对比内存占用。
	/// </summary>
	private static void LogMemoryDiagnostics(string stage)
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long managedMemory = GC.GetTotalMemory(false);
		long workingSet = Environment.WorkingSet;
		Logger.Info(LogCategory.General, $"[内存诊断] {stage}: 托管内存: {managedMemory / 1024.0 / 1024.0:F1} MB | 进程工作集: {workingSet / 1024.0 / 1024.0:F1} MB");
	}

	/// <summary>
	/// 把常见的 Unity ClassID 翻译为类型名，便于阅读诊断日志。
	/// </summary>
	private static string GetClassName(int classID) => classID switch
	{
		1 => "GameObject",
		4 => "Transform",
		21 => "Material",
		28 => "Texture2D",
		33 => "MeshFilter",
		43 => "Mesh",
		48 => "Shader",
		49 => "TextAsset",
		74 => "AnimationClip",
		83 => "AudioClip",
		114 => "MonoBehaviour",
		115 => "MonoScript",
		128 => "Font",
		142 => "AssetBundle",
		150 => "PreloadData",
		156 => "TerrainData",
		184 => "Sprite",
		194 => "ParticleEmitter",
		198 => "ParticleSystem",
		213 => "Sprite",
		222 => "CanvasRenderer",
		224 => "RectTransform",
		225 => "Canvas",
		238 => "EditorExtension",
		271 => "SpriteAtlas",
		329 => "AssemblyDefinitionReference",
		1024 => "AnimatorController",
		1025 => "AnimatorState",
		1101 => "AnimatorStateTransition",
		1102 => "AnimatorTransition",
		1105 => "AnimatorControllerLayer",
		1108 => "AnimatorControllerParameter",
		1110 => "AnimatorState",
		_ => $"Unknown({classID})"
	};

	public static async Task ExportUnityProject(string path)
	{
		if (IsLoaded && IsValidExportDirectory(path))
		{
			if (IsNonEmptyDirectory(path))
			{
				if (!await UserConsentsToDeletion())
				{
					Logger.Info(LogCategory.Export, "User declined to delete existing export directory. Aborting export.");
					return;
				}
				Directory.Delete(path, true);
			}

			Directory.CreateDirectory(path);
			ExportHandler.Export(GameData, path, LocalFileSystem.Instance);
		}
	}

	public static async Task ExportPrimaryContent(string path)
	{
		if (IsLoaded && IsValidExportDirectory(path))
		{
			if (IsNonEmptyDirectory(path))
			{
				if (!await UserConsentsToDeletion())
				{
					Logger.Info(LogCategory.Export, "User declined to delete existing export directory. Aborting export.");
					return;
				}
				Directory.Delete(path, true);
			}

			Directory.CreateDirectory(path);
			Logger.Info(LogCategory.Export, "Starting primary content export");
			Logger.Info(LogCategory.Export, $"Attempting to export assets to {path}...");
			Settings.ExportRootPath = path;
			PrimaryContentExporter.CreateDefault(GameData, Settings).Export(GameBundle, Settings, LocalFileSystem.Instance);
			Logger.Info(LogCategory.Export, "Finished exporting primary content.");
		}
	}

	private static FullConfiguration LoadSettings()
	{
		FullConfiguration settings = new();
		settings.LoadFromDefaultPath();
		return settings;
	}

	private static bool IsValidExportDirectory(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			Logger.Error(LogCategory.Export, "Export path is empty");
			return false;
		}
		string directoryName = Path.GetFileName(path);
		if (directoryName is "Desktop" or "Documents" or "Downloads")
		{
			Logger.Error(LogCategory.Export, $"Export path '{path}' is a system directory");
			return false;
		}
		return true;
	}

	private static bool IsNonEmptyDirectory(string path)
	{
		return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
	}

	private static async Task<bool> UserConsentsToDeletion()
	{
		if (Headless)
		{
			return true;
		}
		ConfirmationDialog.Options options = new()
		{
			Message = Localization.ExportDirectoryDeleteUserConfirmation,
			Type = ConfirmationDialog.Type.YesNo,
		};
		bool? result = await ConfirmationDialog.Confirm(options);
		return result ?? false;
	}
}
