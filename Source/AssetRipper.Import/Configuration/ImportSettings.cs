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
	/// The maximum size in bytes of a single decompressed bundle block or
	/// decompressed file that is kept entirely in memory. When a decompressed
	/// payload exceeds this threshold it is spilled to a temporary file via
	/// <c>SmartStream.CreateTemp()</c> to avoid unbounded memory growth.
	/// Default value is <c>50 * 1024 * 1024</c> (50 MB).
	/// </summary>
	public int MaxInMemoryBundleBlockSize { get; set; } = 50 * 1024 * 1024;

	/// <summary>
	/// The maximum depth used when recursively scanning directories during
	/// import. Used to guard against symlink loops and abnormally deep paths.
	/// Default value is <c>32</c>.
	/// </summary>
	public int MaxRecursiveDirectoryDepth { get; set; } = 32;

	/// <summary>
	/// The maximum number of files that a single import is allowed to collect.
	/// Once this many files have been discovered, further recursion stops and a
	/// warning is emitted. Default value is <c>100_000</c>.
	/// </summary>
	public int MaxCollectedFiles { get; set; } = 100_000;

	/// <summary>
	/// The number of files to load per batch when loading a
	/// <see cref="AssetRipper.Assets.Bundles.GameBundle"/>. Smaller batches reduce
	/// peak memory usage at the cost of more iterations. Default value is <c>50</c>.
	/// </summary>
	public int FileBatchSize { get; set; } = 50;

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(ScriptContentLevel)}: {ScriptContentLevel}");
		Logger.Info(LogCategory.General, $"{nameof(StreamingAssetsMode)}: {StreamingAssetsMode}");
		Logger.Info(LogCategory.General, $"{nameof(DefaultVersion)}: {DefaultVersion}");
		Logger.Info(LogCategory.General, $"{nameof(TargetVersion)}: {TargetVersion}");
		Logger.Info(LogCategory.General, $"{nameof(MaxInMemoryBundleBlockSize)}: {MaxInMemoryBundleBlockSize}");
		Logger.Info(LogCategory.General, $"{nameof(MaxRecursiveDirectoryDepth)}: {MaxRecursiveDirectoryDepth}");
		Logger.Info(LogCategory.General, $"{nameof(MaxCollectedFiles)}: {MaxCollectedFiles}");
		Logger.Info(LogCategory.General, $"{nameof(FileBatchSize)}: {FileBatchSize}");
	}
}
