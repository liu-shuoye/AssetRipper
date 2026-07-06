using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_189;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.PrimaryContent.Textures;

public sealed class TextureExporter(ImageExportFormat imageFormat) : IContentExtractor
{
	private ImageExportFormat ImageFormat { get; } = imageFormat;

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
	{
		if (asset is IImageTexture texture && texture.CheckAssetIntegrity())
		{
			exportCollection = new ImageExportCollection(this, texture);
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
		if (TextureConverter.TryConvertToBitmap((IImageTexture)asset, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, ImageFormat, path);
			return true;
		}
		else
		{
			return false;
		}
	}

	private sealed class ImageExportCollection(IContentExtractor contentExtractor, IImageTexture asset) : SingleExportCollection<IImageTexture>(contentExtractor, asset)
	{
		private ImageExportFormat ExportFormat => ((TextureExporter)ContentExtractor).ImageFormat;

		protected override string ExportExtension => ExportFormat.GetFileExtension();

		protected override string GetExportExtension(IUnityObjectBase asset)
		{
			if (ExportFormat == ImageExportFormat.Original)
			{
				return asset.GetBestExtension() ?? base.GetExportExtension(asset);
			}

			return base.GetExportExtension(asset);
		}
	}
}
