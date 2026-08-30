using AssetRipper.Import.Logging;
using System.Text.Json.Serialization;

namespace AssetRipper.Import.Configuration;

public sealed record class ImportSettings
{
	/// <summary>
	/// The level of scripts to export
	/// </summary>
	public ScriptContentLevel ScriptContentLevel { get; set; } = ScriptContentLevel.Level2;

	/// <summary>
	/// Including the streaming assets directory can cause some games to fail while exporting.
	/// </summary>
	[JsonIgnore]
	public bool IgnoreStreamingAssets
	{
		get => StreamingAssetsMode == StreamingAssetsMode.Ignore;
		set
		{
			StreamingAssetsMode = value ? StreamingAssetsMode.Ignore : StreamingAssetsMode.Extract;
		}
	}

	/// <summary>
	/// How the StreamingAssets folder is handled
	/// </summary>
	public StreamingAssetsMode StreamingAssetsMode { get; set; } = StreamingAssetsMode.Extract;

	/// <summary>
	/// The default version used when no version is specified, ie when the version has been stripped.
	/// </summary>
	[JsonConverter(typeof(UnityVersionJsonConverter))]
	public UnityVersion DefaultVersion { get; set; }

	/// <summary>
	/// The target version to convert all assets to. Experimental
	/// </summary>
	[JsonConverter(typeof(UnityVersionJsonConverter))]
	public UnityVersion TargetVersion { get; set; }

	/// <summary>
	/// 游戏类型，决定是否启用特定游戏的专属资产解析逻辑。默认使用通用 Unity 解析。
	/// </summary>
	public GameType GameType { get; set; } = GameType.Generic;

	/// <summary>
	/// 是否在加载文件时使用依赖关系文件解析不在打开文件夹内的依赖文件。
	/// </summary>
	public bool LoadDependencyMap { get; set; }

	/// <summary>
	/// 依赖关系文件路径。由"扫描依赖关系"命令生成，需配合 <see cref="LoadDependencyMap"/> 使用。
	/// </summary>
	public string? DependencyMapPath { get; set; }

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(ScriptContentLevel)}: {ScriptContentLevel}");
		Logger.Info(LogCategory.General, $"{nameof(StreamingAssetsMode)}: {StreamingAssetsMode}");
		Logger.Info(LogCategory.General, $"{nameof(DefaultVersion)}: {DefaultVersion}");
		Logger.Info(LogCategory.General, $"{nameof(TargetVersion)}: {TargetVersion}");
		Logger.Info(LogCategory.General, $"{nameof(GameType)}: {GameType}");
		Logger.Info(LogCategory.General, $"{nameof(LoadDependencyMap)}: {LoadDependencyMap}");
		Logger.Info(LogCategory.General, $"{nameof(DependencyMapPath)}: {DependencyMapPath}");
	}
}
