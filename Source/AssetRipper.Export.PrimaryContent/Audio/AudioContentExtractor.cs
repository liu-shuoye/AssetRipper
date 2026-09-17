using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.PrimaryContent.Audio;

public sealed class AudioContentExtractor : IContentExtractor
{
	/// <summary>
	/// 占位模式：AudioClip 数据在加载阶段剥离，导出时按需回读真实数据解码（用完即弃），
	/// 解码失败才生成空占位音频文件以保持引用。
	/// </summary>
	private bool PlaceholderMode { get; }

	public AudioContentExtractor(bool placeholderMode = false)
	{
		PlaceholderMode = placeholderMode;
	}

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
	{
		if (asset is IAudioClip audioClip)
		{
			// 占位模式升级：先在回读句柄内解码真实数据（句柄补回被剥离的内嵌音频，用完即弃），
			// 解码失败才创建空占位文件保持引用与文件名
			using AssetDataRestoreHandle? restore = PlaceholderMode ? StrippedAssetData.TryAcquire(audioClip) : null;
			if (AudioExportCollection.TryCreate(this, audioClip, out AudioExportCollection? audioExportCollection))
			{
				exportCollection = audioExportCollection;
				return true;
			}
			if (PlaceholderMode && AudioExportCollection.TryCreatePlaceholder(this, audioClip, out AudioExportCollection? placeholderCollection))
			{
				exportCollection = placeholderCollection;
				return true;
			}
		}

		exportCollection = null;
		return false;
	}
}
