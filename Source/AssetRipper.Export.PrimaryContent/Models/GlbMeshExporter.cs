using AssetRipper.Assets;
using AssetRipper.Export.Modules.Models;
using AssetRipper.Import.Logging;
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
			// 数据已剥离，写一个不含网格数据的空场景 GLB：文件合法可打开，引用与文件名保持
			GlbWriter.TryWrite(new SceneBuilder(), fileStream, out _);
			return true;
		}

		SceneBuilder sceneBuilder = GlbMeshBuilder.Build((IMesh)asset);
		if (GlbWriter.TryWrite(sceneBuilder, fileStream, out string? errorMessage))
		{
			return true;
		}
		else
		{
			Logger.Error(LogCategory.Export, errorMessage);
			return false;
		}
	}
}
