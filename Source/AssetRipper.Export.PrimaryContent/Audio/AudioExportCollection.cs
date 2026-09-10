using AssetRipper.Export.Modules.Audio;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_83;

namespace AssetRipper.Export.PrimaryContent.Audio;

public sealed class AudioExportCollection : SingleExportCollection<IAudioClip>
{
	private byte[] data;
	private readonly string extension;

	private AudioExportCollection(AudioContentExtractor contentExtractor, IAudioClip asset, byte[] data, string extension) : base(contentExtractor, asset)
	{
		this.data = data;
		this.extension = extension;
	}

	public static bool TryCreate(AudioContentExtractor contentExtractor, IAudioClip asset, [NotNullWhen(true)] out AudioExportCollection? exportCollection)
	{
		if (AudioClipDecoder.TryDecode(asset, out byte[]? data, out string? extension, out string? message))
		{
			exportCollection = new AudioExportCollection(contentExtractor, asset, data, extension);
			return true;
		}
		else
		{
			Logger.Log(LogType.Warning, LogCategory.Export, message);
			exportCollection = null;
			return false;
		}
	}

	/// <summary>
	/// 占位模式专用：数据已在加载阶段剥离、无法解码，直接创建空占位音频文件集合。
	/// 扩展名固定为 ogg（主内容模式下占位文件仅用于保持文件名与引用）。
	/// </summary>
	public static bool TryCreatePlaceholder(AudioContentExtractor contentExtractor, IAudioClip asset, [NotNullWhen(true)] out AudioExportCollection? exportCollection)
	{
		exportCollection = new AudioExportCollection(contentExtractor, asset, [], "ogg");
		return true;
	}

	protected override bool ExportInner(string filePath, string dirPath, FileSystem fileSystem)
	{
		// 占位集合的 data 为空数组，需照常写出空文件；正常导出的 data 始终非空
		fileSystem.File.WriteAllBytes(filePath, data);
		data = []; // Export is only called once, so we can clear the data.
		return true;
	}

	protected override string ExportExtension => extension;
}
