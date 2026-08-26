using AssetRipper.Assets;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// 按资产稳定标识计算确定性 GUID，供"分批导出时保持 GUID 不变"使用。
/// </summary>
/// <remarks>
/// 计算键取 {ClassName}|{GetBestDirectory()}|{GetBestName()}|{Collection.Name}|{PathID}：
/// 这些信息全部来自已解析的游戏数据，与导出批次、导出顺序、输出目录已有文件均无关，
/// 只要加载的是同一份游戏文件，任意多次导出都得到完全相同的结果；
/// 同时 PathID 保证同一批内不同资源（即使目录+名称相同）不会产生重复 GUID，规避 Unity 重复 GUID 错误。
/// </remarks>
public static class DeterministicGuidCalculator
{
	/// <summary>
	/// 计算指定资产的确定性 GUID。
	/// </summary>
	/// <param name="asset">目标资产。</param>
	/// <returns>基于稳定标识的 <see cref="UnityGuid"/>，与 `.meta` 的 32 位小写十六进制格式一致。</returns>
	public static UnityGuid Calculate(IUnityObjectBase asset)
	{
		string key = $"{asset.ClassName}|{asset.GetBestDirectory()}|{asset.GetBestName()}|{asset.Collection.Name}|{asset.PathID}";
		return UnityGuid.Md5Hash(key);
	}
}