using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Structure;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Platforms;

namespace AssetRipper.Processing;

public record GameData(
	GameBundle GameBundle,
	UnityVersion ProjectVersion,
	IAssemblyManager AssemblyManager,
	PlatformGameStructure? PlatformStructure)
{
	public ProcessedAssetCollection AddNewProcessedCollection(string name)
	{
		return GameBundle.AddNewProcessedCollection(name, ProjectVersion);
	}

	public static GameData FromGameStructure(GameStructure gameStructure)
	{
		return new(gameStructure.FileCollection, gameStructure.FileCollection.GetMaxUnityVersion(), gameStructure.AssemblyManager, gameStructure.PlatformStructure);
	}

	/// <summary>
	/// 用元数据枚举遍历 bundle 中所有 AssetCollection，仅对指定 ClassID 的对象做单对象反序列化。
	/// 用于替代 FetchAssets().OfType&lt;T&gt;() 模式，避免触发全量反序列化。
	/// </summary>
	public IEnumerable<T> EnumerateAssetsByClassID<T>(int classID) where T : IUnityObjectBase
	{
		foreach (AssetCollection collection in GameBundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID != classID)
				{
					continue;
				}

				T? asset = collection.TryGetAssetOnly<T>(meta.PathID);
				if (asset is not null)
				{
					yield return asset;
				}
			}
		}
	}
}
