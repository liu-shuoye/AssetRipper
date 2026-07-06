namespace AssetRipper.Export.Configuration;

public enum ImageExportFormat
{
	/// <summary>
	/// 无损压缩. Bitmap<br/>
	/// <see href="https://en.wikipedia.org/wiki/BMP_file_format"/>
	/// </summary>
	Bmp,

	/// <summary>
	/// 无损压缩. OpenEXR<br/>
	/// <see href="https://en.wikipedia.org/wiki/OpenEXR"/>
	/// </summary>
	Exr,

	/// <summary>
	/// 无损压缩. Radiance HDR<br/>
	/// <see href="https://en.wikipedia.org/wiki/RGBE_image_format"/>
	/// </summary>
	Hdr,

	/// <summary>
	/// 有损压缩. Joint Photographic Experts Group<br/>
	/// <see href="https://en.wikipedia.org/wiki/JPEG"/>
	/// </summary>
	Jpeg,

	/// <summary>
	/// 无损压缩. Portable Network Graphics<br/>
	/// <see href="https://en.wikipedia.org/wiki/Portable_Network_Graphics"/>
	/// </summary>
	Png,

	/// <summary>
	/// 无损压缩. Truevision TGA<br/>
	/// <see href="https://en.wikipedia.org/wiki/Truevision_TGA"/>
	/// </summary>
	Tga,

	/// <summary>
	/// 资产的原始格式。
	/// </summary>
	Original,
}

public static class ImageExportFormatExtensions
{
	public static string GetFileExtension(this ImageExportFormat _this)
	{
		return _this switch
		{
			ImageExportFormat.Bmp => "bmp",
			ImageExportFormat.Exr => "exr",
			ImageExportFormat.Hdr => "hdr",
			ImageExportFormat.Jpeg => "jpeg",
			ImageExportFormat.Png => "png",
			ImageExportFormat.Tga => "tga",
			ImageExportFormat.Original => string.Empty,
			_ => throw new ArgumentOutOfRangeException(nameof(_this)),
		};
	}

	//When extension types come in C# 13, this will be more convenient to use.
	public static bool TryGetFromExtension(string extension, out ImageExportFormat format)
	{
		format = extension switch
		{
			"bmp" => ImageExportFormat.Bmp,
			"exr" => ImageExportFormat.Exr,
			"hdr" => ImageExportFormat.Hdr,
			"jpeg" => ImageExportFormat.Jpeg,
			"jpg" => ImageExportFormat.Jpeg,
			"png" => ImageExportFormat.Png,
			"tga" => ImageExportFormat.Tga,
			_ => (ImageExportFormat)(-1),
		};
		return format >= 0;
	}

	public static ImageExportFormat GetFromExtension(string path)
	{
		string extension = Path.GetExtension(path).ToLowerInvariant();
		ImageExportFormat format = extension switch
		{
			".bmp" => ImageExportFormat.Bmp,
			".exr" => ImageExportFormat.Exr,
			".hdr" => ImageExportFormat.Hdr,
			".jpeg" => ImageExportFormat.Jpeg,
			".jpg" => ImageExportFormat.Jpeg,
			".png" => ImageExportFormat.Png,
			".tga" => ImageExportFormat.Tga,
			_ => (ImageExportFormat)(-1),
		};
		return format;
	}
}
