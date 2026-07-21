using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.IO.Files.SerializedFiles;

namespace AssetRipper.Export.UnityProjects;

public interface IExportCollection
{
	/// <summary>
	/// 导出此集合中的资源。
	/// </summary>
	/// <param name="container"></param>
	/// <param name="projectDirectory">The directory containing the whole project including Assets and ProjectSettings.</param>
	/// <returns>True if export was successful.</returns>
	bool Export(IExportContainer container, string projectDirectory, FileSystem fileSystem);
	/// <summary>
	/// 这个收藏中的资产部分是吗？
	/// </summary>
	bool Contains(IUnityObjectBase asset);
	/// <summary>
	/// 获取资产的导出ID。
	/// </summary>
	long GetExportID(IExportContainer container, IUnityObjectBase asset);
	MetaPtr CreateExportPointer(IExportContainer container, IUnityObjectBase asset, bool isLocal);

	AssetCollection File { get; }
	TransferInstructionFlags Flags { get; }
	IEnumerable<IUnityObjectBase> Assets { get; }
	IEnumerable<IUnityObjectBase> ExportableAssets => Assets;
	/// <summary>
	/// 这个集合保存任何文件吗？
	/// </summary>
	bool Exportable => true;
	string Name { get; }
}
