using AssetRipper.Import.Logging;

namespace AssetRipper.Processing.Configuration;

public sealed record class ProcessingSettings
{
	public bool EnablePrefabOutlining { get; set; } = false;
	public bool EnableStaticMeshSeparation { get; set; } = true;
	public bool EnableAssetDeduplication { get; set; } = false;
	public bool RemoveNullableAttributes { get; set; } = false;
	public bool PublicizeAssemblies { get; set; } = false;
	public BundledAssetsExportMode BundledAssetsExportMode { get; set; } = BundledAssetsExportMode.DirectExport;

	/// <summary>
	/// The maximum degree of parallelism used by <c>Parallel.ForEach</c> in the
	/// processing stage (for example <see cref="AssetRipper.Processing.Editor.EditorFormatProcessor"/>).
	/// Limiting this caps the peak memory and CPU usage during processing.
	/// Default value is <see cref="System.Environment.ProcessorCount"/>.
	/// </summary>
	public int MaxImportParallelism { get; set; } = System.Environment.ProcessorCount;

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(EnablePrefabOutlining)}: {EnablePrefabOutlining}");
		Logger.Info(LogCategory.General, $"{nameof(EnableStaticMeshSeparation)}: {EnableStaticMeshSeparation}");
		Logger.Info(LogCategory.General, $"{nameof(EnableAssetDeduplication)}: {EnableAssetDeduplication}");
		Logger.Info(LogCategory.General, $"{nameof(BundledAssetsExportMode)}: {BundledAssetsExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(MaxImportParallelism)}: {MaxImportParallelism}");
	}
}
