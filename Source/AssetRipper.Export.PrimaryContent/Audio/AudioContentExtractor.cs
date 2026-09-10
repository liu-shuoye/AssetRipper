using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_83;

namespace AssetRipper.Export.PrimaryContent.Audio;

public sealed class AudioContentExtractor : IContentExtractor
{
	/// <summary>
	/// 占位模式：AudioClip 数据已在加载阶段剥离，导出时生成空占位音频文件以保持引用。
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
			// 占位模式下数据已剥离、解码必然失败，直接走占位文件路径
			if (PlaceholderMode)
			{
				if (AudioExportCollection.TryCreatePlaceholder(this, audioClip, out AudioExportCollection? placeholderCollection))
				{
					exportCollection = placeholderCollection;
					return true;
				}
			}
			else if (AudioExportCollection.TryCreate(this, audioClip, out AudioExportCollection? audioExportCollection))
			{
				exportCollection = audioExportCollection;
				return true;
			}
		}

		exportCollection = null;
		return false;
	}
}
