namespace AssetRipper.Assets.Bundles;

/// <summary>
/// Shared default values used by <see cref="GameBundle"/> batch loading.
/// Centralized so that <see cref="GameBundle.FromPaths"/> defaults stay aligned
/// with <see cref="Import.Configuration.ImportSettings"/> without taking a
/// direct dependency on the import configuration assembly.
/// </summary>
internal static class GameBundleDefaults
{
	/// <summary>
	/// Default number of files processed per batch during
	/// <see cref="GameBundle.FromPaths"/>. Matches the default
	/// <see cref="Import.Configuration.ImportSettings.FileBatchSize"/>.
	/// </summary>
	public const int DefaultFileBatchSize = 50;

	/// <summary>
	/// Default maximum size in bytes of an in-memory
	/// <see cref="IO.Files.ResourceFiles.ResourceFile"/> payload before it is spilled
	/// to a temporary file via <see cref="IO.Files.Streams.Smart.SmartStream.CreateTemp"/>.
	/// Matches the default <see cref="Import.Configuration.ImportSettings.MaxInMemoryBundleBlockSize"/>.
	/// </summary>
	public const int DefaultMaxInMemoryBundleBlockSize = 50 * 1024 * 1024;
}
