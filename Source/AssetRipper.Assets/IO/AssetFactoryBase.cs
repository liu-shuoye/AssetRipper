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

	/// <summary>
	/// 读取资产但跳过"剥离数据"类选项，返回数据完整的对象。
	/// 占位模式下，导出阶段据此重建单个对象以回读被剥离的真实数据（配合 <see cref="AssetCollection.MaterializeAssetOnly"/>）。
	/// 默认实现与 <see cref="ReadAsset"/> 相同；带剥离逻辑的工厂子类应重写此方法区分两种语义。
	/// </summary>
	public virtual IUnityObjectBase? ReadAssetWithData(AssetInfo assetInfo, ReadOnlyArraySegment<byte> assetData, SerializedType? assetType)
	{
		return ReadAsset(assetInfo, assetData, assetType);
	}
}
