using AssetRipper.Logging;
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
	/// 加载时剥离 Texture2D 的内嵌像素数据（保留 m_StreamData 流引用），
	/// 大幅降低批量加载全部资源时的内存占用；导出时按需回读真实数据（流式懒读 .resS /
	/// 完全内嵌者从原始序列化文件重建对象补回），用完即弃，回读失败才降级白色占位图。
	/// 更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripTexture2DData { get; set; } = false;

	/// <summary>
	/// 加载时剥离 Mesh 的内嵌顶点数据与索引缓冲（保留 m_StreamData 流引用）以降低内存占用；
	/// 导出时按需回读真实数据（索引缓冲等从原始序列化文件重建对象补回，流式顶点懒读 .resS），
	/// 用完即弃，回读失败才生成占位文件（工程模式空网格 YAML / 主内容模式空 GLB）。
	/// 更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripMeshData { get; set; } = false;

	/// <summary>
	/// 加载时剥离 AudioClip 的内嵌音频数据（保留 m_Resource 流引用）；
	/// 导出时按需回读真实数据解码（流式懒读 .resource / 内嵌者重建对象补回），用完即弃，
	/// 解码失败才生成空占位音频文件，保持文件名与引用不丢失。
	/// AudioClip 数据本身是懒加载流，此项主要削减对象驻留体积，而非加载内存。
	/// 更改此选项后需重新加载资源文件才能生效。
	/// </summary>
	public bool StripAudioClipData { get; set; } = false;

	/// <summary>
	/// 内存拆解口径：控制加载/处理阶段输出资源占用拆解日志时的统计方式。
	/// 默认按已反序列化对象的序列化字节数统计（廉价）；live 反射估算真实托管堆；clrmd 抓取整堆快照。
	/// </summary>
	public MemoryBreakdownMode MemoryBreakdownMode { get; set; } = MemoryBreakdownMode.Serialized;

	/// <summary>
	/// 导入类型白名单：只解析并导出这里列出的资产大类，未列出的类型在
	/// <c>GameAssetFactory.ReadAsset</c> 阶段直接跳过，完全不反序列化。
	/// </summary>
	/// <remarks>
	/// 本集合仅在 <see cref="EnableImportAssetTypeFilter"/> 为 true 时生效，
	/// 否则一律全量导入——默认关闭是为了避免用户误清空后导出出空项目。
	/// 使用集合而非单个枚举是因为需求要求多选；<see cref="ImportAssetTypeExtensions.IsClassIdAllowed"/>
	/// 是判定入口，导入与导出两侧共用同一份规则。
	/// </remarks>
	public HashSet<ImportAssetType> ImportAssetTypes { get; set; } = [];

	/// <summary>
	/// 是否启用 <see cref="ImportAssetTypes"/> 白名单过滤。
	/// </summary>
	/// <remarks>
	/// 与集合内容分离是刻意的：设置页用一组复选框表达「选中哪些类型」，
	/// 但复选框全不勾选在语义上更接近「什么都不要」而非「不过滤」，
	/// 于是再加一个总开关让用户能明确表达「取消过滤、恢复全量导入」。
	/// </remarks>
	public bool EnableImportAssetTypeFilter { get; set; } = false;

	/// <summary>
	/// 是否启用文件扫描结果缓存。启用后同目录的扫描结果会落盘，
	/// 下次导入且目录未变化时可直接复用，跳过逐文件读取文件头。
	/// </summary>
	/// <remarks>
	/// 默认关闭是刻意的：缓存会在磁盘上写出一个额外文件，不应在用户未要求时悄悄产生。
	/// 缓存本身按目录指纹（文件数 + 最后写入时间）校验，指纹不符即重新扫描，不会漏文件。
	/// </remarks>
	public bool EnableFileScanCache { get; set; } = false;

	/// <summary>
	/// 文件扫描结果缓存文件路径。仅在 <see cref="EnableFileScanCache"/> 为 true 时生效。
	/// </summary>
	public string? FileScanCachePath { get; set; }

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
		Logger.Info(LogCategory.General, $"{nameof(MemoryBreakdownMode)}: {MemoryBreakdownMode}");
		Logger.Info(LogCategory.General, $"{nameof(EnableImportAssetTypeFilter)}: {EnableImportAssetTypeFilter}");
		Logger.Info(LogCategory.General, $"{nameof(ImportAssetTypes)}: {(ImportAssetTypes.Count == 0 ? "(all)" : string.Join(", ", ImportAssetTypes.OrderBy(t => t)))}");
		Logger.Info(LogCategory.General, $"{nameof(EnableFileScanCache)}: {EnableFileScanCache}");
		Logger.Info(LogCategory.General, $"{nameof(FileScanCachePath)}: {FileScanCachePath}");
	}

	/// <summary>
	/// 当前生效的白名单类型集合；未启用过滤时返回 null，下游据此跳过判定。
	/// </summary>
	/// <remarks>
	/// 把「开关折叠进取值」这一步收敛到这里，下游（导入工厂、导出集合创建）就无需
	/// 分别判断开关与集合内容，减少两处状态不一致的可能。
	/// 注意：启用过滤但集合为空时返回空集合，语义是「不导入任何已知类型」，
	/// 这比「参数为 null 即不过滤」更符合用户勾选后的直觉。
	/// </remarks>
	[JsonIgnore]
	public IReadOnlyCollection<ImportAssetType>? EffectiveImportAssetTypes => EnableImportAssetTypeFilter ? ImportAssetTypes : null;
}
