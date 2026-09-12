using AssetRipper.Import.Configuration;

namespace AssetRipper.GUI.Web.Pages.Settings.DropDown;

/// <summary>
/// 内存拆解口径下拉设置项，用于选择内存诊断输出占用拆解日志时的统计方式。
/// </summary>
public sealed class MemoryBreakdownModeDropDownSetting : DropDownSetting<MemoryBreakdownMode>
{
	public static MemoryBreakdownModeDropDownSetting Instance { get; } = new();

	public override string Title => Localization.MemoryBreakdownModeTitle;

	protected override string GetDisplayName(MemoryBreakdownMode value) => value switch
	{
		MemoryBreakdownMode.Serialized => Localization.MemoryBreakdownModeSerialized,
		MemoryBreakdownMode.Live => Localization.MemoryBreakdownModeLive,
		MemoryBreakdownMode.ClrMd => Localization.MemoryBreakdownModeClrMd,
		_ => base.GetDisplayName(value),
	};

	protected override string? GetDescription(MemoryBreakdownMode value) => value switch
	{
		MemoryBreakdownMode.Serialized => Localization.MemoryBreakdownModeSerializedDescription,
		MemoryBreakdownMode.Live => Localization.MemoryBreakdownModeLiveDescription,
		MemoryBreakdownMode.ClrMd => Localization.MemoryBreakdownModeClrMdDescription,
		_ => base.GetDescription(value),
	};
}
