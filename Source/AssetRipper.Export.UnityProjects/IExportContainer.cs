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
}
