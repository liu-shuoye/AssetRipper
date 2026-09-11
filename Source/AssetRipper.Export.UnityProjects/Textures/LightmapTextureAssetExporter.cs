using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_1120;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Textures;

public class LightmapTextureAssetExporter : BinaryAssetExporter
{
	public ImageExportFormat ImageExportFormat { get; private set; }

	/// <summary>
	/// 占位模式：Texture2D 数据已在加载阶段剥离，导出时生成白色同尺寸占位图以保持引用。
	/// </summary>
	private bool PlaceholderMode { get; }

	public LightmapTextureAssetExporter(ImageExportFormat imageExportFormat, bool placeholderMode = false)
	{
		ImageExportFormat = imageExportFormat;
		PlaceholderMode = placeholderMode;
	}

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset.MainAsset is ILightingDataAsset)
		{
			exportCollection = new LightmapExportCollection(this, (ITexture2D)asset);
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		ITexture2D texture = (ITexture2D)asset;
		if (PlaceholderMode)
		{
			// 数据已在加载阶段剥离，跳过完整性与解码检查直接生成占位图
			if (TextureConverter.TryCreatePlaceholderBitmap(texture, out DirectBitmap placeholder))
			{
				using Stream stream = fileSystem.File.Create(path);
				placeholder.Save(stream, ImageExportFormat, path);
				return true;
			}
			return false;
		}

		if (!texture.CheckAssetIntegrity())
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Can't export '{texture.Name}' because resources file '{texture.StreamData_C28?.Path}' hasn't been found");
			return false;
		}

		if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, ImageExportFormat, path);
			return true;
		}
		else
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Unable to convert '{texture.Name}' to bitmap");
			return false;
		}
	}

	private sealed class LightmapExportCollection(LightmapTextureAssetExporter exporter, ITexture2D lightmap) : AssetExportCollection<ITexture2D>(exporter, lightmap)
	{
		protected override string GetExportExtension(IUnityObjectBase asset)
		{
			return ((LightmapTextureAssetExporter)AssetExporter).ImageExportFormat.GetFileExtension();
		}

		protected override IUnityObjectBase CreateImporter(IExportContainer container)
		{
			return ImporterFactory.GenerateTextureImporter(container, Asset);
		}
	}
}
