using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_189;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.PrimaryContent.Textures;

public sealed class TextureExporter(ImageExportFormat imageFormat, bool placeholderMode = false) : IContentExtractor
{
	private ImageExportFormat ImageFormat { get; } = imageFormat;

	/// <summary>
	/// 占位模式：Texture2D 数据已在加载阶段剥离，导出时生成白色同尺寸占位图以保持引用。
	/// </summary>
	private bool PlaceholderMode { get; } = placeholderMode;

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
	{
		// 占位模式下数据已剥离、完整性检查必然失败，需放行以生成占位文件
		if (asset is IImageTexture texture && (PlaceholderMode || texture.CheckAssetIntegrity()))
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
		if (PlaceholderMode && asset is ITexture2D placeholderTexture
			&& TextureConverter.TryCreatePlaceholderBitmap(placeholderTexture, out DirectBitmap placeholder))
		{
			// 数据已在加载阶段剥离，跳过解码直接生成占位图；
			// 格式沿用 ImageFormat，保证内容与文件扩展名一致
			using Stream stream = fileSystem.File.Create(path);
			placeholder.Save(stream, ImageFormat, path);
			return true;
		}

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
