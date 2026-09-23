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
		// 方法结束立即再次清除（用完即弃）
		using AssetDataRestoreHandle? restore = PlaceholderMode && asset is ITexture2D texture ? StrippedAssetData.TryAcquire(texture) : null;

		if (PlaceholderMode && asset is ITexture2D texture2D && !TextureConverter.IsSupportedFormat(texture2D.Format_C28E))
		{
			// 解码层不支持的格式（如 ASTC_HDR）：每格式提示一次后直接降级占位图，避免误导性的 Error 日志
			TextureConverter.LogUnsupportedFormatOnce(texture2D.Format_C28E);
			return ExportPlaceholder(texture2D, path, fileSystem);
		}

		if (TextureConverter.TryConvertToBitmap((IImageTexture)asset, out DirectBitmap bitmap))
		{
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, ImageFormat, path);
			return true;
		}
		else if (PlaceholderMode && asset is ITexture2D placeholderTexture)
		{
			return ExportPlaceholder(placeholderTexture, path, fileSystem);
		}
		else
		{
			return false;
		}
	}

	/// <summary>
	/// 回读失败（数据不可用 / 格式无法解码）时生成白色占位图，保持文件名与引用不丢失。
	/// </summary>
	private bool ExportPlaceholder(ITexture2D texture, string path, FileSystem fileSystem)
	{
		if (TextureConverter.TryCreatePlaceholderBitmap(texture, out DirectBitmap placeholder))
		{
			using Stream stream = fileSystem.File.Create(path);
			placeholder.Save(stream, ImageFormat, path);
			return true;
		}
		return false;
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
