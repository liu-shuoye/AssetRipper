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
		// 占位模式：导出前补回被剥离的内嵌像素数据（流式纹理凭保留的 StreamData 懒读），
		// 方法结束立即再次清除（用完即弃）；回读失败才降级为白色占位图
		using AssetDataRestoreHandle? restore = PlaceholderMode && asset is ITexture2D texture ? StrippedAssetData.TryAcquire(texture) : null;

		if (TextureConverter.TryConvertToBitmap((IImageTexture)asset, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, ImageFormat, path);
			return true;
		}
		else if (PlaceholderMode && asset is ITexture2D placeholderTexture
			&& TextureConverter.TryCreatePlaceholderBitmap(placeholderTexture, out DirectBitmap placeholder))
		{
			using Stream stream = fileSystem.File.Create(path);
			placeholder.Save(stream, ImageFormat, path);
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
