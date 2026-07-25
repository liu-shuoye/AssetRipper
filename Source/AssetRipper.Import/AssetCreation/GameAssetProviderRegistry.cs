using AssetRipper.Import.AssetCreation.Nikki4;
using AssetRipper.Import.Configuration;

namespace AssetRipper.Import.AssetCreation;

/// <summary>
/// 游戏专属资产提供者注册表，按 <see cref="GameType"/> 维护各游戏的提供者实例。
/// </summary>
/// <remarks>
/// 新增游戏支持的扩展方式：
/// 1. 在 <see cref="GameType"/> 枚举中添加新的游戏值；
/// 2. 实现该游戏的 <see cref="IGameAssetProvider"/>；
/// 3. 在下方字典中注册一行映射即可。
/// </remarks>
public static class GameAssetProviderRegistry
{
	/// <summary>
	/// 游戏类型到专属资产提供者的映射表。
	/// </summary>
	private static readonly Dictionary<GameType, IGameAssetProvider> Providers = new()
	{
		// 闪耀暖暖专属资产解析
		[GameType.Nikki4] = new Nikki4GameAssetProvider(),
	};

	/// <summary>
	/// 根据游戏类型获取对应的资产提供者。
	/// </summary>
	/// <param name="gameType">游戏类型。</param>
	/// <returns>对应的资产提供者；该游戏未注册专属解析时返回 null。</returns>
	public static IGameAssetProvider? GetProvider(GameType gameType)
	{
		return Providers.TryGetValue(gameType, out IGameAssetProvider? provider) ? provider : null;
	}
}
