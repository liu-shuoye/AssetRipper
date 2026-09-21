using AssetRipper.Assets.Bundles;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.PrimaryContent;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Logging;
using AssetRipper.Import.Configuration;
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

	// 上次导出的记忆（路径 + 子文件夹选项），始终从磁盘加载，与"保存到磁盘"开关无关
	public static LastExportSettings LastExport { get; } = LoadLastExport();

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
		GameData = ExportHandler.LoadAndProcess(paths, LocalFileSystem.Instance);
	}

	/// <summary>
	/// 记录本次导入的源目录，供"导入完成后自动导出"推断默认导出位置。
	/// </summary>
	/// <param name="paths">用户本次选择的路径（文件或目录）。</param>
	/// <remarks>
	/// 只在路径确实是一个目录时才记住：多选文件或单个文件无法唯一确定一个"工作目录"，
	/// 此时保留上一次的目录记忆比写入一个文件路径更有用。
	/// </remarks>
	public static void RememberImportPath(IReadOnlyList<string> paths)
	{
		if (paths.Count == 0)
		{
			return;
		}

		string candidate = paths[0];
		if (!Directory.Exists(candidate))
		{
			return;
		}

		LastExport.ImportPath = candidate;
		LastExport.SaveToDefaultPath();
	}

	/// <summary>
	/// 推断自动导出使用的基础目录：优先用上次设置的导出目录，未设置时取 <paramref name="importPath"/> 的父目录。
	/// 写成同级目录（而非写入导入目录内部）是为了避免导出结果与源资源混在一起。
	/// </summary>
	/// <param name="importPath">本次导入的源目录，可为 null。</param>
	/// <returns>可直接用于导出的基础目录；两者都不可用时返回 null，调用方应放弃自动导出。</returns>
	public static string? ResolveAutoExportBasePath(string? importPath)
	{
		string? remembered = LastExport.ExportPath;
		if (!string.IsNullOrWhiteSpace(remembered))
		{
			return remembered;
		}

		if (string.IsNullOrWhiteSpace(importPath))
		{
			return null;
		}

		// 规范化掉结尾分隔符，否则 GetDirectoryName 会返回它自身或空串
		string normalized = importPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return Path.GetDirectoryName(normalized);
	}

	/// <summary>
	/// 按用户选定的方式自动导出 <see cref="AutoExportKind"/>；<see cref="AutoExportKind.None"/> 时直接返回。
	/// </summary>
	/// <param name="kind">用户在设置页选择的导出方式。</param>
	/// <param name="importPath">本次导入的源目录，用于推断导出位置。</param>
	/// <remarks>
	/// 复用导出页既有的路径拼接与"创建子文件夹"语义，因此自动导出与手动导出落点完全一致。
	/// 自动导出失败不应影响已完成的导入结果，所以这里吞掉异常只记日志。
	/// </remarks>
	public static async Task ExportAfterLoadAuto(AutoExportKind kind, string? importPath)
	{
		if (kind is AutoExportKind.None)
		{
			return;
		}

		string? basePath = ResolveAutoExportBasePath(importPath);
		if (string.IsNullOrEmpty(basePath))
		{
			Logger.Error(LogCategory.Export, "Auto export skipped: could not determine an export directory.");
			return;
		}

		bool createSubfolder = LastExport.CreateSubfolder;
		// 自动导出无法让用户确认"删除已有目录"，因此强制走时间戳子文件夹，
		// 既避免覆盖用户既有文件，也让多次导入的结果不会互相覆盖。
		if (!createSubfolder)
		{
			createSubfolder = true;
			Logger.Info(LogCategory.Export, "Auto export enabled timestamped subfolder to avoid overwriting existing files.");
		}

		string finalPath = AppendTimestampedSubfolder(basePath, createSubfolder);
		Logger.Info(LogCategory.Export, string.Format(Localization.AutoExportStarted, finalPath));
		try
		{
			if (kind is AutoExportKind.UnityProject)
			{
				await ExportUnityProject(finalPath);
			}
			else if (kind is AutoExportKind.PrimaryContent)
			{
				await ExportPrimaryContent(finalPath);
			}
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Export, $"Auto export failed: {ex}");
		}
	}

	/// <summary>
	/// 与导出命令共用同一套子文件夹命名规则，保证自动导出与手动导出生成相同的目录名。
	/// </summary>
	/// <param name="path">用户填写的基础导出目录。</param>
	/// <param name="append">是否追加时间戳子文件夹。</param>
	public static string AppendTimestampedSubfolder(string path, bool append)
	{
		if (!append)
		{
			return path;
		}

		string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
		return Path.Combine(path, $"AssetRipper_export_{timestamp}");
	}

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

	private static LastExportSettings LoadLastExport()
	{
		LastExportSettings.TryLoadFromDefaultPath(out LastExportSettings settings);
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
