using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_83;

namespace AssetRipper.Export.UnityProjects.Audio;

public sealed class AudioClipExportCollection : AudioExportCollection
{
	private byte[]? data;
	private readonly string fileExtension;
	public AudioClipExportCollection(AudioClipExporter assetExporter, IAudioClip asset, byte[] data, string fileExtension) : base(assetExporter, asset)
	{
		this.data = data;
		this.fileExtension = fileExtension;
	}

	protected override bool ExportInner(IExportContainer container, string filePath, string dirPath, FileSystem fileSystem)
	{
		// data 为 null 仍视为失败；空数组是占位模式生成的空占位文件，需照常写出以保持引用
		if (data is null)
		{
			return false;
		}
		else
		{
			fileSystem.File.WriteAllBytes(filePath, data);
			data = null;
			return true;
		}
	}

	protected override string GetExportExtension(IUnityObjectBase asset)
	{
		return fileExtension;
	}
}
