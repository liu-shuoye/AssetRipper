using AssetRipper.Import.Logging;

namespace AssetRipper.Processing.Configuration;

public sealed record class ProcessingSettings
{
	public bool EnablePrefabOutlining { get; set; } = false;
	public bool EnableStaticMeshSeparation { get; set; } = true;
	public bool EnableAssetDeduplication { get; set; } = false;
	/// <summary>
	/// 开启后导出时不再随机生成 GUID，而是基于资产稳定标识计算确定性 GUID。
	/// 这样同一资源在分批导出时始终保持同一 GUID，跨批次引用不会丢失。
	/// </summary>
	public bool EnableDeterministicGuids { get; set; } = false;
	public bool RemoveNullableAttributes { get; set; } = false;
	public bool PublicizeAssemblies { get; set; } = false;
	public BundledAssetsExportMode BundledAssetsExportMode { get; set; } = BundledAssetsExportMode.DirectExport;

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(EnablePrefabOutlining)}: {EnablePrefabOutlining}");
		Logger.Info(LogCategory.General, $"{nameof(EnableStaticMeshSeparation)}: {EnableStaticMeshSeparation}");
		Logger.Info(LogCategory.General, $"{nameof(EnableAssetDeduplication)}: {EnableAssetDeduplication}");
		Logger.Info(LogCategory.General, $"{nameof(EnableDeterministicGuids)}: {EnableDeterministicGuids}");
		Logger.Info(LogCategory.General, $"{nameof(BundledAssetsExportMode)}: {BundledAssetsExportMode}");
	}
}
