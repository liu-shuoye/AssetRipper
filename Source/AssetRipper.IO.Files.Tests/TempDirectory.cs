namespace AssetRipper.IO.Files.Tests;

/// <summary>
/// 一个用完即删的临时目录，供需要真实文件系统的测试使用。
/// </summary>
/// <remarks>
/// 扫描逻辑刻意依赖真实文件的大小与内容，用虚拟文件系统无法覆盖本机读取路径，
/// 因此这里落到磁盘上；目录名带随机后缀，避免并行执行的测试互相干扰。
/// </remarks>
internal sealed class TempDirectory : IDisposable
{
	public string Path { get; }

	public FileSystem FileSystem { get; } = LocalFileSystem.Instance;

	public TempDirectory()
	{
		Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AssetRipperTests", Guid.NewGuid().ToString("N")[..12]);
		Directory.CreateDirectory(Path);
	}

	/// <summary>写入一个文件并返回其完整路径。</summary>
	public string WriteFile(string name, byte[] contents)
	{
		string filePath = System.IO.Path.Combine(Path, name);
		System.IO.File.WriteAllBytes(filePath, contents);
		return filePath;
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(Path, recursive: true);
		}
		catch (IOException)
		{
			// 临时目录清理失败不应让测试失败
		}
	}
}
