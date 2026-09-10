using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Audio;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_83;

namespace AssetRipper.Export.UnityProjects.Audio;

public sealed class AudioClipExporter : BinaryAssetExporter
{
	public AudioExportFormat AudioFormat { get; }

	/// <summary>
	/// 占位模式：AudioClip 数据已在加载阶段剥离，导出时生成空占位音频文件以保持引用。
	/// </summary>
	private bool PlaceholderMode { get; }

	public AudioClipExporter(FullConfiguration configuration)
	{
		AudioFormat = configuration.ExportSettings.AudioExportFormat;
		PlaceholderMode = configuration.ImportSettings.StripAudioClipData;
	}

	public static bool IsSupportedExportFormat(AudioExportFormat format) => format switch
	{
		AudioExportFormat.Default or AudioExportFormat.PreferWav => true,
		_ => false,
	};

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is IAudioClip audio)
		{
			if (PlaceholderMode)
			{
				// 数据已在加载阶段剥离，无法解码，这里直接生成空占位文件；
				// 扩展名按导出格式设置决定，与后续真实导出的文件名保持一致以便覆盖
				string extension = AudioFormat == AudioExportFormat.PreferWav ? "wav" : "ogg";
				exportCollection = new AudioClipExportCollection(this, audio, [], extension);
				return true;
			}

			if (AudioClipDecoder.TryDecode(audio, out byte[]? decodedData, out string? fileExtension, out string? message))
			{
				if (AudioFormat == AudioExportFormat.PreferWav && fileExtension == "ogg")
				{
					exportCollection = new AudioClipExportCollection(this, audio, AudioConverter.OggToWav(decodedData), "wav");
				}
				else
				{
					exportCollection = new AudioClipExportCollection(this, audio, decodedData, fileExtension);
				}

				return true;
			}
			else
			{
				Logger.Error(LogCategory.Export, message);
			}
		}

		exportCollection = null;
		return false;
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		throw new NotSupportedException();
	}
}
