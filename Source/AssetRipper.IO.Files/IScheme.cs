using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files;

public interface IScheme
{
	/// <summary>
	/// 检查该方案是否可以读取此文件。
	/// </summary>
	/// <remarks>
	/// 实现应将 <paramref name="stream"/> 重置为其初始位置。
	/// </remarks>
	/// <param name="stream">文件的流。</param>
	/// <returns>如果文件可读，则为真。</returns>
	bool CanRead(SmartStream stream);
	FileBase Read(SmartStream stream, string filePath, string fileName);
}
