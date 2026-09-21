using AssetRipper.Import.Configuration;

namespace AssetRipper.GUI.Web.Pages.Settings.DropDown;

/// <summary>
/// 导入完成后自动导出的方式下拉设置项。
/// </summary>
/// <remarks>
/// 与导出页的两个手动按钮一一对应：选择某一项后，导入结束会立刻按该方式导出，
/// 用户无需再切到导出页点击。选择 "None" 即回到原有的纯手动流程。
/// </remarks>
public sealed class AutoExportKindDropDownSetting : DropDownSetting<AutoExportKind>
{
	public static AutoExportKindDropDownSetting Instance { get; } = new();

	public override string Title => Localization.AutoExportKindTitle;

	protected override string GetDisplayName(AutoExportKind value) => value switch
	{
		AutoExportKind.None => Localization.AutoExportKindNone,
		AutoExportKind.UnityProject => Localization.AutoExportKindUnityProject,
		AutoExportKind.PrimaryContent => Localization.AutoExportKindPrimaryContent,
		_ => base.GetDisplayName(value),
	};

	protected override string? GetDescription(AutoExportKind value) => value switch
	{
		AutoExportKind.None => Localization.AutoExportKindNoneDescription,
		AutoExportKind.UnityProject => Localization.AutoExportKindUnityProjectDescription,
		AutoExportKind.PrimaryContent => Localization.AutoExportKindPrimaryContentDescription,
		_ => base.GetDescription(value),
	};
}
