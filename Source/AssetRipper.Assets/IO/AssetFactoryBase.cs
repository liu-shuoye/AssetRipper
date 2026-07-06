using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Files.SerializedFiles.Parser;

namespace AssetRipper.Assets.IO;
/// <summary>
/// 资源工厂
/// </summary>
public abstract class AssetFactoryBase
{
	public abstract IUnityObjectBase? ReadAsset(AssetInfo assetInfo, ReadOnlyArraySegment<byte> assetData, SerializedType? assetType);
}
