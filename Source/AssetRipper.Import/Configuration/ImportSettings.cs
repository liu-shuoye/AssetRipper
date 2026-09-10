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
	public GameType GameType { get; set; } = GameType.Nikki4;

	/// <summary>
	/// 是否在加载文件时使用依赖关系文件解析不在打开文件夹内的依赖文件。
	/// </summary>
	public bool LoadDependencyMap { get; set; }

	/// <summary>
	/// 依赖关系文件路径。由"扫描依赖关系"命令生成，需配合 <see cref="LoadDependencyMap"/> 使用。
	/// </summary>
	public string? DependencyMapPath { get; set; }

	/// <summary>
	/// IL2Cpp dump 目录路径。指向 Il2CppDumper 的输出目录（自动识别其中的 DummyDll 子目录），
	/// 或直接指向包含 dump 程序集的目录。用于 Cpp2IL 无法解析的游戏（如元数据加密），
	/// 由用户在外部工具完成解析后直接加载导出的托管程序集。
	/// </summary>
	public string? Il2CppDumpPath { get; set; }

	/// <summary>
	/// 加载时剥离 Texture2D 的图像数据（内嵌 m_ImageData 与 m_StreamData 流引用），
	/// 大幅降低批量加载全部资源时的内存占用；导出时为每个 Texture2D 生成白色同尺寸占位图，
	/// 保持文件名与 .meta 引用不丢失。更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripTexture2DData { get; set; } = false;

	/// <summary>
	/// 加载时剥离 Mesh 的顶点数据、索引缓冲与外部流引用以降低内存占用；
	/// 导出时生成不含网格数据的占位文件（工程模式为空网格 YAML，主内容模式为空 GLB），
	/// 保持文件名与引用不丢失。更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripMeshData { get; set; } = false;

	/// <summary>
	/// 加载时剥离 AudioClip 的音频数据引用；
	/// 导出时生成空占位音频文件，保持文件名与引用不丢失。
	/// AudioClip 数据本身是懒加载流，此项不降低加载内存，仅用于统一生成占位文件。
	/// 更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripAudioClipData { get; set; } = false;

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(ScriptContentLevel)}: {ScriptContentLevel}");
		Logger.Info(LogCategory.General, $"{nameof(StreamingAssetsMode)}: {StreamingAssetsMode}");
		Logger.Info(LogCategory.General, $"{nameof(DefaultVersion)}: {DefaultVersion}");
		Logger.Info(LogCategory.General, $"{nameof(TargetVersion)}: {TargetVersion}");
		Logger.Info(LogCategory.General, $"{nameof(GameType)}: {GameType}");
		Logger.Info(LogCategory.General, $"{nameof(LoadDependencyMap)}: {LoadDependencyMap}");
		Logger.Info(LogCategory.General, $"{nameof(DependencyMapPath)}: {DependencyMapPath}");
		Logger.Info(LogCategory.General, $"{nameof(Il2CppDumpPath)}: {Il2CppDumpPath}");
		Logger.Info(LogCategory.General, $"{nameof(StripTexture2DData)}: {StripTexture2DData}");
		Logger.Info(LogCategory.General, $"{nameof(StripMeshData)}: {StripMeshData}");
		Logger.Info(LogCategory.General, $"{nameof(StripAudioClipData)}: {StripAudioClipData}");
	}
}
