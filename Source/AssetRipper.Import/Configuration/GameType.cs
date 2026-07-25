namespace AssetRipper.Import.Configuration;

/// <summary>
/// 游戏类型，决定资产解析时使用的解析逻辑。
/// </summary>
public enum GameType
{
	/// <summary>
	/// 默认值，使用通用的 Unity 资产解析。
	/// </summary>
	Generic = 0,
	/// <summary>
	/// 闪耀暖暖（Infinity Nikki）专属资产解析。
	/// </summary>
	Nikki4,
}
