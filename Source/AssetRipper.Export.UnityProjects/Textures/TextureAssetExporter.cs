using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Logging;
using AssetRipper.Processing.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_213;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Textures;

public class TextureAssetExporter : BinaryAssetExporter
{
	public ImageExportFormat ImageExportFormat { get; private set; }
	private SpriteExportMode SpriteExportMode { get; set; }
	private bool ExportSprites => SpriteExportMode is not SpriteExportMode.Yaml;

	/// <summary>
	/// 占位模式：Texture2D 数据在加载阶段剥离，导出时按需回读真实数据（流式懒读 .resS / 内嵌重建补回），
	/// 用完即弃；回读失败才降级为白色占位图以保持引用。
	/// </summary>
	private bool PlaceholderMode { get; }

	public TextureAssetExporter(FullConfiguration configuration)
	{
		ImageExportFormat = configuration.ExportSettings.ImageExportFormat;
		SpriteExportMode = configuration.ExportSettings.SpriteExportMode;
		PlaceholderMode = configuration.ImportSettings.StripTexture2DData;
	}

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset.MainAsset is SpriteInformationObject spriteInformationObject && (ExportSprites || asset is not ISprite))
		{
			exportCollection = new TextureExportCollection(this, spriteInformationObject, ExportSprites);
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
			// 导出时回读真实数据：句柄在进入 using 块时补回被剥离的内嵌像素数据，
			// 离开时立即再次清除（用完即弃）；流式纹理由 GetImageData 懒读 .resS。
			// 回读失败（如 .resS 缺失、资产不可重建）才降级为白色占位图。
			using AssetDataRestoreHandle restore = StrippedAssetData.TryAcquire(texture);
			if (ExportReal(texture, path, fileSystem))
			{
				return true;
			}
			return ExportPlaceholder(texture, path, fileSystem);
		}
		return ExportReal(texture, path, fileSystem);
	}

	private bool ExportReal(ITexture2D texture, string path, FileSystem fileSystem)
	{
		if (!texture.CheckAssetIntegrity())
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Can't export '{texture.Name}' because resources file '{texture.StreamData_C28?.Path}' hasn't been found");
			return false;
		}

		if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
		{
			ImageExportFormat imageExportFormat = ImageExportFormat;
			if (ImageExportFormat.Original == ImageExportFormat)
			{
				imageExportFormat = ImageExportFormatExtensions.GetFromExtension(path);
			}
			using Stream stream = fileSystem.File.Create(path);
			bitmap.Save(stream, imageExportFormat, path);
			return true;
		}
		else
		{
			Logger.Log(LogType.Warning, LogCategory.Export, $"Unable to convert '{texture.Name}' to bitmap");
			return false;
		}
	}

	/// <summary>
	/// 生成同尺寸纯白占位图：回读真实数据失败的兜底，保持文件名与引用不丢失。
	/// </summary>
	private bool ExportPlaceholder(ITexture2D texture, string path, FileSystem fileSystem)
	{
		if (TextureConverter.TryCreatePlaceholderBitmap(texture, out DirectBitmap placeholder))
		{
			using Stream stream = fileSystem.File.Create(path);
			placeholder.Save(stream, ImageExportFormat, path);
			return true;
		}
		return false;
	}
}
