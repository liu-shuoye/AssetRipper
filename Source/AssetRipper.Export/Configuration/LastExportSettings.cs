using AssetRipper.IO.Files;
using System.Text.Json;

namespace AssetRipper.Export.Configuration;

/// <summary>
/// 记录上次导入的源目录、上次导出的路径与相关选项，始终写入磁盘，
/// 与现有"保存到磁盘"开关（SaveSettingsToDisk）无关，从而每次打开导出页都自动预填。
/// </summary>
public sealed class LastExportSettings
{
	/// <summary>
	/// 用户上次导入时选择的源目录。用于"加载文件夹后自动导出"时推断默认导出位置，
	/// 目标是让用户只需选一次导入目录，之后加载与导出都不必再手填路径。
	/// </summary>
	public string? ImportPath { get; set; }

	/// <summary>
	/// 用户上次导出时填写的基础导出目录（不含追加的时间戳子文件夹）。
	/// </summary>
	public string? ExportPath { get; set; }

	/// <summary>
	/// 上次导出时是否勾选了"创建时间戳子文件夹"。
	/// </summary>
	public bool CreateSubfolder { get; set; }

	public const string DefaultFileName = "AssetRipper.LastExport.json";

	public static string DefaultFilePath => Path.Join(LocalFileSystem.ExecutingDirectory, DefaultFileName);

	public static bool TryLoadFromDefaultPath(out LastExportSettings settings)
	{
		if (File.Exists(DefaultFilePath))
		{
			settings = JsonSerializer.Deserialize(
				File.ReadAllText(DefaultFilePath),
				LastExportSettingsContext.Default.LastExportSettings)!;
			return true;
		}

		settings = new LastExportSettings();
		return false;
	}

	public void SaveToDefaultPath()
	{
		// 用 File.Create 覆盖写入，保证最新一次导出记忆始终落盘
		using FileStream fileStream = File.Create(DefaultFilePath);
		JsonSerializer.Serialize(fileStream, this, LastExportSettingsContext.Default.LastExportSettings);
	}
}
