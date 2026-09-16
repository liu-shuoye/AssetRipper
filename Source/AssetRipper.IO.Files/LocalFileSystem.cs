using System.Diagnostics;

namespace AssetRipper.IO.Files;

public partial class LocalFileSystem : FileSystem
{
	public static LocalFileSystem Instance { get; } = new();

	/// <summary>
	/// 默认并发度：受 CPU 并发能力与磁盘队列深度共同限制后再封顶，避免在机械盘上因寻道抖动反而变慢。
	/// </summary>
	public static int DefaultHeaderReadConcurrency => Math.Clamp(Environment.ProcessorCount, 1, 16);

	public partial class LocalFileImplementation
	{
		public override string? GetLocalPath(string path) => path;

		/// <summary>
		/// 用 <see cref="FileStream"/> 并发读取各文件开头，避开 <see cref="System.IO.File"/> 的静态流池。
		/// </summary>
		/// <remarks>
		/// 这里刻意不使用 <see cref="System.IO.File.OpenRead"/>：它背后的 <c>FileStream</c> 池是按进程串行复用的，
		/// 多个线程并发调用时彼此等待，反而可能比串行更慢；直接构造 <see cref="FileStream"/> 才能拿到真正的并行 IO。
		/// </remarks>
		public override int[] BatchReadHeaderPrefix(IReadOnlyList<string> paths, int maxBytes, byte[][] buffers, int concurrency)
		{
			return BatchReadHeaderPrefixImpl(this.Parent, paths, buffers, concurrency);
		}
	}

	public partial class LocalDirectoryImplementation
	{
		public override void Create(string path) => System.IO.Directory.CreateDirectory(path);

		public override void Delete(string path) => System.IO.Directory.Delete(path, true);

		/// <summary>
		/// 一次遍历同时取得名称与大小。相较于先枚举路径再逐个 stat，这把本机上每次都要走系统调用的开销减半。
		/// </summary>
		public override IEnumerable<FileEntryInfo> EnumerateFileInfos(string path)
		{
			foreach (System.IO.FileInfo info in new System.IO.DirectoryInfo(path).EnumerateFiles())
			{
				yield return new FileEntryInfo(info.FullName, info.Length);
			}
		}
	}

	public static string ExecutingDirectory => AppContext.BaseDirectory;

	private string LocalTemporaryDirectory => Path.Join(ExecutingDirectory, "temp", GetRandomString()[0..4]);

	private string SystemTemporaryDirectory => Path.Join(System.IO.Path.GetTempPath(), "AssetRipper", GetRandomString()[0..4]);

	public override string TemporaryDirectory
	{
		get
		{
			if (string.IsNullOrEmpty(field))
			{
				field = LocalTemporaryDirectory;
				Debug.Assert(!Directory.Exists(field));
				try
				{
					Directory.Create(field);
					File.WriteAllText(Path.Join(field, ".WriteTest"), "test");
					Directory.Delete(field);
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
					field = SystemTemporaryDirectory;
				}
			}
			return field;
		}
		set
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				field = Path.GetFullPath(value);
			}
		}
	}
}
