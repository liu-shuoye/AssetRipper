using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetRipper.IO.Files;

public partial class FileSystem
{
	/// <summary>
	/// <see href="https://en.wikipedia.org/wiki/Comparison_of_file_systems#Limits"/>
	/// </summary>
	private const int ActualMaxFileNameLength = 255;
	/// <summary>
	/// We reserve 10 characters for handling file name conflicts, an underscore and up to 9 digits.
	/// This allows us to handle up to 1 billion duplicates, far more than we'll ever need.
	/// </summary>
	private const int ReservedCharacterCount = 10;
	public const int MaxFileNameLength = ActualMaxFileNameLength - ReservedCharacterCount;

	public abstract string TemporaryDirectory { get; set; }

	/// <summary>
	/// 并发批量读取各文件的开头若干字节，用于在不完整解析文件的前提下识别文件类型。
	/// </summary>
	/// <param name="paths">待读取的文件路径，下标与 <paramref name="buffers"/> 一一对应。</param>
	/// <param name="maxBytes">每个文件最多读取的字节数，即缓冲区的长度。</param>
	/// <param name="buffers">与 <paramref name="paths"/> 等长的缓冲区数组，每个缓冲区长度不小于 <paramref name="maxBytes"/>。</param>
	/// <param name="concurrency">并发度，必须为正数。</param>
	/// <returns>实际读取的字节数；读取失败的文件返回 -1。</returns>
	public int[] BatchReadHeaderPrefix(IReadOnlyList<string> paths, int maxBytes, byte[][] buffers, int concurrency)
	{
		return File.BatchReadHeaderPrefix(paths, maxBytes, buffers, concurrency);
	}

	public partial class FileImplementation
	{
		public string CreateTemporary()
		{
			Directory.Create(Parent.TemporaryDirectory);
			string path = Path.Join(Parent.TemporaryDirectory, GetRandomString());
			File.Create(path).Dispose();
			return path;
		}

		/// <summary>
		/// 返回该路径在本机真实文件系统中的位置；若此文件系统并非本机磁盘（如虚拟文件系统）则返回 <see langword="null"/>。
		/// </summary>
		/// <remarks>
		/// 用于把本机特有的快速路径（批量 stat、并发读取）限制在 <see cref="LocalFileSystem"/> 上，
		/// 其他实现保持与原有逐次调用等价的语义。
		/// </remarks>
		public virtual string? GetLocalPath(string path) => null;
	}

	public partial class DirectoryImplementation
	{
		public virtual void Create(string path) => throw new NotSupportedException();

		public virtual void Delete(string path) => throw new NotSupportedException();

		public string CreateTemporary()
		{
			string path = Path.Join(Parent.TemporaryDirectory, GetRandomString()[0..8]);
			Directory.Create(path);
			return path;
		}
	}

	public void DeleteTemporaryDirectory()
	{
		if (Directory.Exists(TemporaryDirectory))
		{
			Directory.Delete(TemporaryDirectory);
		}
	}

	public string GetUniqueName(string dirPath, string fileName, int maxNameLength)
	{
		string? ext = null;
		string? name = null;
		string validFileName = fileName;
		if (Encoding.UTF8.GetByteCount(fileName) > maxNameLength)
		{
			ext = Path.GetExtension(validFileName);
			name = Utf8Truncation.TruncateToUTF8ByteLength(fileName, maxNameLength - Encoding.UTF8.GetByteCount(ext));
			validFileName = name + ext;
		}

		if (!Directory.Exists(dirPath))
		{
			return validFileName;
		}

		name ??= Path.GetFileNameWithoutExtension(validFileName);
		if (!IsReservedName(name))
		{
			if (!File.Exists(Path.Join(dirPath, validFileName)))
			{
				return validFileName;
			}
		}

		ext ??= Path.GetExtension(validFileName);

		string key = Path.Join(dirPath, $"{name}{ext}");
		UniqueNamesByInitialPath.TryGetValue(key, out int initial);

		for (int counter = initial; counter < int.MaxValue; counter++)
		{
			string proposedName = $"{name}_{counter}{ext}";
			if (!File.Exists(Path.Join(dirPath, proposedName)))
			{
				UniqueNamesByInitialPath[key] = counter;
				return proposedName;
			}
		}
		throw new Exception($"Can't generate unique name for file {fileName} in directory {dirPath}");
	}

	private Dictionary<string, int> UniqueNamesByInitialPath { get; } = [];

	public static string RemoveCloneSuffixes(string path)
	{
		return path.Replace("(Clone)", null);
	}

	public static string RemoveInstanceSuffixes(string path)
	{
		return path.Replace("(Instance)", null);
	}

	public static string FixInvalidFileNameCharacters(string path)
	{
		return FileNameRegex.Replace(path, "_");
	}

	private static Regex CreateFileNameRegex()
	{
		string invalidChars = GetInvalidFileNameChars();
		string escapedChars = Regex.Escape(invalidChars);
		// Updated regex to include commas, square brackets, and ASCII control characters
		return new Regex($@"[{escapedChars},\[\]\x00-\x1F]", RegexOptions.Compiled);
	}

	/// <summary>
	/// Gets all the invalid characters including the colon on Linux
	/// </summary>
	/// <returns></returns>
	private static string GetInvalidFileNameChars()
	{
		char[] defaultBadCharacters = System.IO.Path.GetInvalidFileNameChars();
		string result = new string(defaultBadCharacters);
		if (defaultBadCharacters.Contains(':'))
		{
			return result;
		}
		else
		{
			return result + ':';
		}
	}

	private static Regex FileNameRegex { get; } = CreateFileNameRegex();

	public static string FixInvalidPathCharacters(string path)
	{
		return TrimEntries(PathRegex.Replace(path, "_"));

		static string TrimEntries(string path)
		{
			if (path.Contains(" /", StringComparison.Ordinal) || path.Contains("/ ", StringComparison.Ordinal))
			{
				string[] entries = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				return string.Join('/', entries);
			}
			else
			{
				return path.Trim();
			}
		}
	}

	private static Regex CreatePathRegex()
	{
		string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars().Except(['\\', '/']).ToArray());
		string escapedChars = Regex.Escape(invalidChars);
		// Updated regex to include commas, square brackets, and ASCII control characters
		return new Regex($@"[{escapedChars},\[\]\x00-\x1F]", RegexOptions.Compiled);
	}

	private static Regex PathRegex { get; } = CreatePathRegex();

	public static bool IsReservedName(string name)
	{
		return OperatingSystem.IsWindows() && name.Length is 3 or 4 && ReservedNames.Contains(name);
	}

	private static HashSet<string> ReservedNames { get; } = new(StringComparer.OrdinalIgnoreCase)
	{
		"aux", "con", "nul", "prn",
		"com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
		"lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
	};

	private protected static string GetRandomString() => Guid.NewGuid().ToString();

	/// <summary>
	/// 一个目录条目的路径与大小。大小为 <see cref="UnknownSize"/> 表示该文件系统无法廉价地提供大小。
	/// </summary>
	/// <param name="Path">文件完整路径。</param>
	/// <param name="Length">文件字节数，未知时为 <see cref="UnknownSize"/>。</param>
	public readonly record struct FileEntryInfo(string Path, long Length);

	/// <summary>
	/// <see cref="FileEntryInfo.Length"/> 的哨兵值：大小未知。0 是合法的文件大小，所以不能复用。
	/// </summary>
	public const long UnknownSize = -1;

	/// <summary>
	/// 尝试获取文件大小，失败或不被支持时返回 <see cref="UnknownSize"/>。
	/// </summary>
	private protected static long TryGetFileLength(FileSystem fileSystem, string path)
	{
		try
		{
			string? localPath = fileSystem.File.GetLocalPath(path);
			if (localPath is not null)
			{
				return new System.IO.FileInfo(localPath).Length;
			}
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			// 大小只是用于筛选的提示信息，拿不到时退化为“未知”，交由调用方决定是否仍需读取文件头
		}

		return UnknownSize;
	}

	/// <summary>
	/// 并发批量读取各文件的开头若干字节，用于识别文件类型而不必完整打开或解析文件。
	/// </summary>
	/// <param name="fileSystem">用于打开文件。</param>
	/// <param name="paths">待读取的文件路径，下标与 <paramref name="buffers"/> 一一对应。</param>
	/// <param name="buffers">每个文件对应的缓冲区，长度即该文件最多读取的字节数。</param>
	/// <param name="concurrency">并发度，必须为正数。</param>
	/// <returns>实际读取的字节数；失败的文件返回 -1。</returns>
	public static int[] BatchReadHeaderPrefixImpl(FileSystem fileSystem, IReadOnlyList<string> paths, byte[][] buffers, int concurrency)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(concurrency, 1);
		int[] results = new int[paths.Count];
		// 每个线程各持有自己的小缓冲区，避免共享状态；顺序无关，因此不需要任何同步
		int workerCount = Math.Min(concurrency, paths.Count);
		if (workerCount <= 1)
		{
			for (int i = 0; i < paths.Count; i++)
			{
				results[i] = ReadHeaderPrefix(fileSystem, paths[i], buffers[i]);
			}
			return results;
		}

		int next = -1;
		Thread[] workers = new Thread[workerCount];
		for (int w = 0; w < workerCount; w++)
		{
			workers[w] = new Thread(() =>
			{
				int i;
				while ((i = Interlocked.Increment(ref next)) < paths.Count)
				{
					results[i] = ReadHeaderPrefix(fileSystem, paths[i], buffers[i]);
				}
			})
			{
				IsBackground = true,
				// 大项目下等待全部读完可显著超过默认栈大小所暗示的用途，给足栈空间更从容
				Name = "AssetRipper.HeaderReader",
			};
			workers[w].Start();
		}

		foreach (Thread worker in workers)
		{
			worker.Join();
		}

		return results;
	}

	/// <summary>
	/// 串行版本的批量读取，供不支持并发的文件系统使用。
	/// </summary>
	private protected static int[] ReadHeaderPrefixSequentially(FileSystem fileSystem, IReadOnlyList<string> paths, byte[][] buffers)
	{
		int[] results = new int[paths.Count];
		for (int i = 0; i < paths.Count; i++)
		{
			results[i] = ReadHeaderPrefix(fileSystem, paths[i], buffers[i]);
		}
		return results;
	}

	private static int ReadHeaderPrefix(FileSystem fileSystem, string path, byte[] buffer)
	{
		try
		{
			using Stream stream = fileSystem.File.OpenRead(path);
			return stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			return -1;
		}
	}
}
