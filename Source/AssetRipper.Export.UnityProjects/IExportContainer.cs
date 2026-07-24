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
	/// 导出阶段的 EditorFormat 转换回调。
	/// </summary>
	/// <remarks>
	/// 由 <see cref="Project.ProjectYamlWalker.ExportYamlDocument"/> 在 <c>WalkEditor</c> 之前对单个资产调用，
	/// 把非破坏性的 EditorFormat 转换从 Process 阶段延迟到导出阶段按需执行，减少 Process 阶段内存峰值。
	/// 返回 <c>null</c> 时表示无需转换（例如未启用延迟转换的调用路径）。
	/// </remarks>
	Action<IUnityObjectBase>? EditorFormatConverter { get; }
}
