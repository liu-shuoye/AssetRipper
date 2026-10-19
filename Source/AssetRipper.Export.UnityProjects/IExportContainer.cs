using AssetRipper.Assets;
using AssetRipper.Assets.Collections;

namespace AssetRipper.Export.UnityProjects;

public interface IExportContainer
{
	long GetExportID(IUnityObjectBase asset);
	AssetType ToExportType(Type type);
	MetaPtr CreateExportPointer(IUnityObjectBase asset);

	UnityGuid ScenePathToGUID(string name);
	bool IsSceneDuplicate(int sceneID);

	AssetCollection File { get; }

	UnityVersion ExportVersion { get; }

	/// <summary>
	/// 是否启用确定性 GUID 导出。
	/// </summary>
	/// <remarks>
	/// 默认关闭（保持不变）；开启后，导出时生成的 SpriteID 等值也改用确定性算法，
	/// 以免分批导出时随机值变化导致引用不一致。
	/// </remarks>
	bool UseDeterministicGuids => false;

	/// <summary>
	/// 是否在导出 .meta 文件时省略 AssetBundle 名称与变体（assetBundleName / assetBundleVariant）。
	/// </summary>
	/// <remarks>
	/// 默认关闭（meta 保留原始包体名）；开启后 meta 不再写出这两个字段，重建工程不会把资产归入不存在的 AssetBundle。
	/// </remarks>
	bool SkipAssetBundleNameInMeta => true;
}
