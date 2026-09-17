using AssetRipper.Assets;
using AssetRipper.Export.Modules.Models;
using AssetRipper.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Extensions;
using SharpGLTF.Scenes;

namespace AssetRipper.Export.PrimaryContent.Models;

public sealed class GlbMeshExporter : IContentExtractor
{
	/// <summary>
	/// 占位模式：Mesh 数据已在加载阶段剥离，导出时生成不含网格数据的最小合法 GLB 占位文件。
	/// </summary>
	private bool PlaceholderMode { get; }

	public GlbMeshExporter(bool placeholderMode = false)
	{
		PlaceholderMode = placeholderMode;
	}

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
	{
		// 占位模式下数据已剥离，IsSet() 必为 false，需放行以生成占位文件
		if (asset is IMesh mesh && (PlaceholderMode || mesh.IsSet()))
		{
			exportCollection = new GlbExportCollection(this, asset);
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public bool Export(IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		using Stream fileStream = fileSystem.File.Create(path);
		if (PlaceholderMode)
		{
			// 占位模式升级：导出时回读真实数据（索引缓冲及内嵌顶点经重建对象补回，
			// 流式顶点凭保留的 StreamData 懒读），完整建立 GLB 后句柄立即释放（用完即弃）；
			// 回读失败（如数据不可重建）才写一个不含网格的空场景占位，保证文件名与引用
			using AssetDataRestoreHandle restore = StrippedAssetData.TryAcquire((IMesh)asset);
			if (TryWriteRealGlb((IMesh)asset, fileStream))
			{
				return true;
			}
			// 失败路径可能已写入半截 GLB，先重置流再写空场景占位
			fileStream.SetLength(0);
			fileStream.Position = 0;
			return GlbWriter.TryWrite(new SceneBuilder(), fileStream, out _);
		}
		return TryWriteRealGlb((IMesh)asset, fileStream);
	}

	private bool TryWriteRealGlb(IMesh mesh, Stream fileStream)
	{
		try
		{
			SceneBuilder sceneBuilder = GlbMeshBuilder.Build(mesh);
			if (GlbWriter.TryWrite(sceneBuilder, fileStream, out string? errorMessage))
			{
				return true;
			}
			else
			{
				Logger.Error(LogCategory.Export, errorMessage);
			}
		}
		catch (Exception ex)
		{
			// 数据残缺/为空时 Build 可能抛异常：占位模式降级为空场景，普通模式记日志后按失败返回
			Logger.Error(LogCategory.Export, $"无法生成 GLB：{ex.Message}");
		}
		return false;
	}
}
