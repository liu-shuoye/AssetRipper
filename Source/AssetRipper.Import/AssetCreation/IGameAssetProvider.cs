using AssetRipper.Assets;
using AssetRipper.Assets.Metadata;

namespace AssetRipper.Import.AssetCreation;

/// <summary>
/// 游戏专属资产提供者接口，用于按游戏类型创建定制解析的资产对象。
/// </summary>
public interface IGameAssetProvider
{
	/// <summary>
	/// 尝试创建游戏专属的资产对象。
	/// </summary>
	/// <param name="assetInfo">资产信息。</param>
	/// <param name="version">Unity 版本。</param>
	/// <returns>创建的资产对象；返回 null 表示回退到默认解析。</returns>
	IUnityObjectBase? TryCreateAsset(AssetInfo assetInfo, UnityVersion version);
}
