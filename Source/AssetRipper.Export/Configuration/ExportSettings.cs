using AssetRipper.Logging;

namespace AssetRipper.Export.Configuration;

public sealed record class ExportSettings
{
	/// <summary>
	/// 音频剪辑导出为何种格式？推荐：Ogg
	/// </summary>
	public AudioExportFormat AudioExportFormat { get; set; } = AudioExportFormat.Default;

	/// <summary>
	/// 图像（如纹理）导出为何种格式？
	/// </summary>
	public ImageExportFormat ImageExportFormat { get; set; } = ImageExportFormat.Original;

	/// <summary>
	/// 环境光贴图导出为何种格式？
	/// </summary>
	public LightmapTextureExportFormat LightmapTextureExportFormat { get; set; } = LightmapTextureExportFormat.Exr;

	/// <summary>
	/// 脚本如何导出？推荐：Hybrid
	/// </summary>
	public ScriptExportMode ScriptExportMode { get; set; } = ScriptExportMode.Hybrid;

	/// <summary>
	/// 解密脚本时使用的 C# 语言版本。
	/// </summary>
	public ScriptLanguageVersion ScriptLanguageVersion { get; set; } = ScriptLanguageVersion.AutoSafe;

	/// <summary>
	/// 是否对脚本中的类型引用进行完全限定？
	/// </summary>
	public bool ScriptTypesFullyQualified { get; set; } = false;

	/// <summary>
	/// 如何导出着色器？
	/// </summary>
	public ShaderExportMode ShaderExportMode { get; set; } = ShaderExportMode.RuriDecompile;

	/// <summary>
	/// 如何导出 ComputeShader（计算着色器）？
	/// </summary>
	public ComputeShaderExportMode ComputeShaderExportMode { get; set; } = ComputeShaderExportMode.Yaml;

	/// <summary>
	/// 是否将精灵导出为纹理？推荐：原生
	/// </summary>
	public SpriteExportMode SpriteExportMode { get; set; } = SpriteExportMode.Native;

	/// <summary>
	/// 文本资产如何导出？
	/// </summary>
	public TextExportMode TextExportMode { get; set; } = TextExportMode.Parse;

	public bool ExportUnreadableAssets { get; set; } = false;

	public bool SaveSettingsToDisk { get; set; }

	public string? LanguageCode { get; set; }

	/// <summary>
	/// 用户 Unity 项目根目录或资产文件夹路径。
	/// 导出时，同类型同名的资产将直接复制该目录中的源文件与 .meta（保留其 GUID），
	/// 而不是导出反编译/转换版本。留空则禁用该功能。
	/// </summary>
	public string? CustomProjectPath { get; set; }

	public void Log()
	{
		Logger.Info(LogCategory.General, $"{nameof(AudioExportFormat)}: {AudioExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ImageExportFormat)}: {ImageExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(LightmapTextureExportFormat)}: {LightmapTextureExportFormat}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptExportMode)}: {ScriptExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ScriptLanguageVersion)}: {ScriptLanguageVersion}");
		Logger.Info(LogCategory.General, $"{nameof(ShaderExportMode)}: {ShaderExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ComputeShaderExportMode)}: {ComputeShaderExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(SpriteExportMode)}: {SpriteExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(TextExportMode)}: {TextExportMode}");
		Logger.Info(LogCategory.General, $"{nameof(ExportUnreadableAssets)}: {ExportUnreadableAssets}");
		Logger.Info(LogCategory.General, $"{nameof(CustomProjectPath)}: {CustomProjectPath}");
	}
}
