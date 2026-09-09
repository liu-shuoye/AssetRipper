using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Import.Logging;
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
	/// 占位模式：Texture2D 数据已在加载阶段剥离，导出时生成白色同尺寸占位图以保持引用。
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
			// 数据已在加载阶段剥离，跳过完整性与解码检查直接生成占位图；
			// 格式沿用 ImageExportFormat，保证内容与文件扩展名一致
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
}
