using AssetRipper.Import.Configuration;

namespace AssetRipper.GUI.Web.Pages.Settings.DropDown;

/// <summary>
/// 游戏类型下拉设置项，用于选择针对特定游戏的专属解析方式。
/// </summary>
public sealed class GameTypeDropDownSetting : DropDownSetting<GameType>
{
	public static GameTypeDropDownSetting Instance { get; } = new();

	public override string Title => Localization.GameTypeTitle;

	protected override string GetDisplayName(GameType value) => value switch
	{
		GameType.Generic => Localization.GameTypeGeneric,
		GameType.Nikki4 => Localization.GameTypeNikki4,
		_ => base.GetDisplayName(value),
	};

	protected override string? GetDescription(GameType value) => value switch
	{
		GameType.Generic => Localization.GameTypeGenericDescription,
		GameType.Nikki4 => Localization.GameTypeNikki4Description,
		_ => base.GetDescription(value),
	};
}
