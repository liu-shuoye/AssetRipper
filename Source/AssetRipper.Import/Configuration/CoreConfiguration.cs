using AssetRipper.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using System.Diagnostics;

namespace AssetRipper.Import.Configuration;

public class CoreConfiguration
{
	#region Import Settings
	/// <summary>
	/// 禁用脚本后，某些游戏可能会被导出，而之前无法导出。
	/// </summary>
	public bool DisableScriptImport => ImportSettings.ScriptContentLevel == ScriptContentLevel.Level0;

	public ImportSettings ImportSettings
	{
		get => SingletonData.GetStoredValue<ImportSettings>(nameof(ImportSettings));
		set => SingletonData.SetStoredValue(nameof(ImportSettings), value);
	}

	#endregion

	#region Export Settings
	/// <summary>
	/// 导出的根路径
	/// </summary>
	public string ExportRootPath { get; set; } = "";
	/// <summary>
	/// 创建新unity项目的路径
	/// </summary>
	public string ProjectRootPath => Path.Join(ExportRootPath, "ExportedProject");
	public string AssetsPath => Path.Join(ProjectRootPath, "Assets"); // 资源路径
	public string ProjectSettingsPath => Path.Join(ProjectRootPath, "ProjectSettings"); // 项目设置路径
	public string AuxiliaryFilesPath => Path.Join(ExportRootPath, "AuxiliaryFiles"); // 辅助文件路径
	#endregion

	#region Project Settings
	public UnityVersion Version { get; private set; }
	#endregion

	public SingletonDataStorage SingletonData { get; } = new();
	public ListDataStorage ListData { get; } = new();

	public CoreConfiguration()
	{
		ResetToDefaultValues();
		AddDebugData();
		SingletonData.Add(nameof(ImportSettings), new JsonDataInstance<ImportSettings>(ImportSettingsContext.Default.ImportSettings));
	}

	public void SetProjectSettings(UnityVersion version)
	{
		Version = version;
	}

	public virtual void ResetToDefaultValues()
	{
		ExportRootPath = Path.Join(LocalFileSystem.ExecutingDirectory, "Ripped");
		SingletonData.Clear();
		ListData.Clear();
	}

	public virtual void LogConfigurationValues()
	{
		Logger.Info(LogCategory.General, $"Configuration Settings:");
		Logger.Info(LogCategory.General, $"{nameof(ExportRootPath)}: {ExportRootPath}");
		ImportSettings.Log();
	}

	[Conditional("DEBUG")]
	private void AddDebugData()
	{
		SingletonData.Add("README", "This is a singleton entry. It is used to store information that can be contained in a single file.");
		ListData.Add("README", ["This is a list entry. It is used to store information that might be contained in multiple files."]);
		ListData.Add("Fibonacci", [1, 1, 2, 3, 5, 8, 13, 21, 34, 55]);
		ListData.Add("Unused Key", []);
	}
}
