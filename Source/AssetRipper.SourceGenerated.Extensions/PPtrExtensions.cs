using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;

namespace AssetRipper.SourceGenerated.Extensions;

public static class PPtrExtensions
{
	public static T? TryGetAsset<T>(this IPPtr<T> pptr, AssetCollection file) where T : IUnityObjectBase
	{
		pptr.TryGetAsset(file, out T? asset);
		return asset;
	}

	/// <summary>
	/// 跨 collection 单对象解引用 PPtr，避免触发目标 collection 全量反序列化。
	/// </summary>
	/// <remarks>
	/// 与 <see cref="TryGetAsset{T}(IPPtr{T}, AssetCollection)"/> 的区别：底层走
	/// <see cref="AssetCollection.TryGetAssetOnly{T}(int, long, out T?)"/> 而非 TryGetAsset，
	/// 解决 OriginalPathProcessor.SetOriginalPaths 跨 collection 解引用时引发的 3.6GB 内存峰值问题。
	/// </remarks>
	public static T? TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file) where T : IUnityObjectBase
	{
		file.TryGetAssetOnly(pptr.FileID, pptr.PathID, out T? asset);
		return asset;
	}

	public static bool IsAsset<T>(this IPPtr<T> pptr, AssetCollection file, IUnityObjectBase asset) where T : IUnityObjectBase
	{
		if (asset.PathID != pptr.PathID)
		{
			return false;
		}
		else if (pptr.FileID == 0)
		{
			return file == asset.Collection;
		}
		else
		{
			return file.Dependencies[pptr.FileID - 1] == asset.Collection;
		}
	}

	/// <summary>
	/// PathID == 0
	/// </summary>
	public static bool IsNull(this IPPtr pptr) => pptr.PathID == 0;
}
