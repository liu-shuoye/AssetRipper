using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.Modules.Audio;
using AssetRipper.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.Audio;

public sealed class AudioClipExporter : BinaryAssetExporter
{
	public AudioExportFormat AudioFormat { get; }

	/// <summary>
	/// 占位模式：AudioClip 数据在加载阶段剥离，导出时按需回读真实数据解码，
	/// 用完即弃；解码失败才生成空占位音频文件以保持引用。
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
				// 先按"回读真实数据"处理：句柄补回被剥离的内嵌音频（流式音频凭保留的 Resource 懒读），
				// 解码并取出字节后句柄立即释放、内嵌数据再次清空（用完即弃）；
				// 解码出的字节仍随集合驻留至写出，与普通模式行为一致。
				using AssetDataRestoreHandle restore = StrippedAssetData.TryAcquire(audio);
				if (TryCreateReal(audio, out exportCollection))
				{
					return true;
				}

				// 解码失败（如原始文件不可达）才创建空占位文件保持引用与文件名
				Logger.Error(LogCategory.Export, $"占位模式下调回真实音频数据失败，生成空占位文件：{audio.GetBestName()}");
				string extension = AudioFormat == AudioExportFormat.PreferWav ? "wav" : "ogg";
				exportCollection = new AudioClipExportCollection(this, audio, [], extension);
				return true;
			}

			if (TryCreateReal(audio, out exportCollection))
			{
				return true;
			}
		}

		exportCollection = null;
		return false;
	}

	private bool TryCreateReal(IAudioClip audio, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
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

		exportCollection = null;
		return false;
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		throw new NotSupportedException();
	}
}
