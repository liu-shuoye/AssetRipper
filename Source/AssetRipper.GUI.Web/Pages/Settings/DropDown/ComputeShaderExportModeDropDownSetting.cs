using AssetRipper.Export.Configuration;

namespace AssetRipper.GUI.Web.Pages.Settings.DropDown;

public sealed class ComputeShaderExportModeDropDownSetting : DropDownSetting<ComputeShaderExportMode>
{
	public static ComputeShaderExportModeDropDownSetting Instance { get; } = new();

	public override string Title => Localization.ComputeShaderAssetExportTitle;

	protected override string GetDisplayName(ComputeShaderExportMode value) => value switch
	{
		ComputeShaderExportMode.Yaml => Localization.ComputeShaderAssetFormatYaml,
		ComputeShaderExportMode.Source => Localization.ComputeShaderAssetFormatSource,
		_ => base.GetDisplayName(value),
	};

	protected override string? GetDescription(ComputeShaderExportMode value) => value switch
	{
		ComputeShaderExportMode.Yaml => Localization.ComputeShaderAssetFormatYamlDescription,
		ComputeShaderExportMode.Source => Localization.ComputeShaderAssetFormatSourceDescription,
		_ => base.GetDescription(value),
	};
}
