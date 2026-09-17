using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_189;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Project;

public sealed class YamlStreamedAssetExportCollection : AssetExportCollection<IUnityObjectBase>
{
	public YamlStreamedAssetExportCollection(IAssetExporter assetExporter, IUnityObjectBase asset) : base(assetExporter, asset)
	{
	}

	protected override bool ExportInner(IExportContainer container, string filePath, string dirPath, FileSystem fileSystem)
	{
		return Asset switch
		{
			IMesh mesh => ExportMesh(container, filePath, dirPath, mesh, fileSystem),
			IImageTexture texture => ExportTexture(container, filePath, dirPath, texture, fileSystem),
			_ => false,
		};
	}

	private bool ExportMesh(IExportContainer container, string filePath, string dirPath, IMesh mesh, FileSystem fileSystem)
	{
		// 占位模式：索引缓冲（及完全内嵌网格的顶点数据）在加载期被剥离，
		// 此处从原始序列化文件重建对象补回再导出，方法结束立即再次清除（用完即弃）。
		// 流式网格的顶点数据仍由下方 GetContent 懒读 .resS；非剥离网格索引非空，TryAcquire 免恢复。
		using AssetDataRestoreHandle restore = StrippedAssetData.TryAcquire(mesh);
		if (!mesh.Has_StreamData())
		{
			return base.ExportInner(container, filePath, dirPath, fileSystem);
		}

		bool result;
		mesh.StreamData.GetValues(out Utf8String path, out ulong offset, out uint size);
		if (mesh.VertexData.Data.Length != 0)
		{
			mesh.StreamData.ClearValues();
			result = base.ExportInner(container, filePath, dirPath, fileSystem);
		}
		else
		{
			mesh.VertexData.Data = mesh.StreamData.GetContent(mesh.Collection);
			mesh.StreamData.ClearValues();
			result = base.ExportInner(container, filePath, dirPath, fileSystem);
			mesh.VertexData.Data = [];
		}
		mesh.StreamData.SetValues(path, offset, size);

		return result;
	}

	private bool ExportTexture(IExportContainer container, string filePath, string dirPath, IImageTexture texture, FileSystem fileSystem)
	{
		if (!texture.Has_StreamData_C189())
		{
			return base.ExportInner(container, filePath, dirPath, fileSystem);
		}

		bool result;
		texture.StreamData_C189.GetValues(out Utf8String path, out ulong offset, out uint size);
		if (texture.ImageData_C189.Length != 0)
		{
			texture.StreamData_C189.ClearValues();
			result = base.ExportInner(container, filePath, dirPath, fileSystem);
		}
		else
		{
			byte[]? data = texture.StreamData_C189.GetContent(texture.Collection);
			if (data.IsNullOrEmpty())
			{
				return false;
			}
			texture.ImageData_C189 = data;
			texture.StreamData_C189.ClearValues();
			result = base.ExportInner(container, filePath, dirPath, fileSystem);
			texture.ImageData_C189 = [];
		}
		texture.StreamData_C189.SetValues(path, offset, size);

		return result;
	}
}
