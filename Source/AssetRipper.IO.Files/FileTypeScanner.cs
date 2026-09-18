using AssetRipper.IO.Files.Streams;

namespace AssetRipper.IO.Files;

/// <summary>
/// 按目录批量判定文件类型。核心目的只有一个：把“逐文件开流读文件头”降级为“先按廉价信息筛选，再对少数候选批量读头”。
/// </summary>
/// <remarks>
/// 大型项目（30 万以上文件）下逐文件调用 <see cref="SerializedFile.IsSerializedFile(string, FileSystem)"/>
/// 与 <see cref="BundleHeader.IsBundleHeader(string, FileSystem)"/> 是主要瓶颈：每个文件都要一次打开/关闭/寻址。
/// 本类通过两级筛选把绝大多数文件在内存中排除：
/// <list type="number">
/// <item>扩展名：可以确定不是目标类型的文件直接跳过，不产生任何系统调用。</item>
/// <item>文件大小：目标类型的文件头有最小体积要求，过小的文件不可能命中。</item>
/// </list>
/// 只有通过两级筛选的候选才会被真正读头，且读头是批量并发的。
/// </remarks>
public static class FileTypeScanner
{
	/// <summary>
	/// 识别文件类型所需读取的最大字节数，取各类文件头的最大值。
	/// </summary>
	public const int HeaderProbeLength = 32;

	/// <summary>
	/// 扩展名与目标类型明显不符的文件直接跳过，避免为它们做任何 IO。
	/// </summary>
	/// <remarks>
	/// 这些扩展名对应的文件不可能是 Unity 资源或资源包，是其“确定不可能命中”的判据。
	/// 未列入的扩展名一律仍然会读头，因此这里只需覆盖大项目里数量最多的媒体与缓存类文件。
	/// </remarks>
	private static readonly HashSet<string> DefinitelyNotUnityContent = new(StringComparer.OrdinalIgnoreCase)
	{
		// // 图片
		// ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tif", ".tiff", ".psd", ".gif", ".webp", ".exr", ".hdr",
		// // 音频
		// ".wav", ".mp3", ".ogg", ".m4a", ".aac", ".flac", ".wem", ".bnk",
		// // 视频
		// ".mp4", ".webm", ".mov", ".avi", ".bk2", ".usm",
		// // 文本与配置
		// ".txt", ".json", ".xml", ".yaml", ".yml", ".csv", ".ini", ".md", ".html", ".htm", ".css", ".js",
		// // 字体
		// ".otf", ".ttf", ".ttc", ".woff", ".woff2",
		// // 数据库与缓存
		// ".db", ".sqlite", ".cache", ".log", ".tmp", ".bak", ".pid", ".lock",
		// // 元数据侧车文件
		// ".meta", ".manifest",
	};

	/// <summary>
	/// 序列化文件的最小可能体积：头部与最小元数据之和。
	/// </summary>
	/// <remarks>
	/// 头部会声明文件总大小，因此真实文件不可能小于头部加最小元数据。
	/// </remarks>
	private const int SerializedFileMinimumSize = 0x10 + 13;

	/// <summary>
	/// 资源包头的最小可能体积：magic 及其后的版本号，加上两个以零结尾的版本串与终止符。
	/// </summary>
	private const int BundleMinimumSize = 0x14;

	/// <summary>
	/// 判断该大小的文件是否可能包含序列化文件头。
	/// </summary>
	/// <remarks>
	/// 序列化文件的头部声明了文件总大小，因此文件本身至少要能容纳头部与最小元数据。
	/// <paramref name="size"/> 为 <see cref="FileSystem.UnknownSize"/> 时不做判断，保守地认为可能命中。
	/// </remarks>
	public static bool CouldBeSerializedFile(long size)
	{
		return size == FileSystem.UnknownSize
			|| size >= SerializedFileMinimumSize;
	}

	/// <summary>
	/// 判断该大小的文件是否可能包含资源包头。
	/// </summary>
	/// <remarks>
	/// 资源包头至少需要容纳 magic 字符串及其后的版本号与两个版本串。大小未知时保守返回 <see langword="true"/>。
	/// </remarks>
	public static bool CouldBeBundle(long size)
	{
		return size == FileSystem.UnknownSize
			|| size >= BundleMinimumSize;
	}

	/// <summary>
	/// 判断该扩展名是否已足以确定文件不可能是 Unity 资源或资源包。
	/// </summary>
	public static bool IsDefinitelyNotUnityContent(string path)
	{
		string extension = Path.GetExtension(path);
		return extension.Length > 0 && DefinitelyNotUnityContent.Contains(extension);
	}

	/// <summary>
	/// 扫描一个目录，返回其中所有序列化文件的 (文件名, 路径)。
	/// </summary>
	/// <param name="fileSystem">用于访问文件。</param>
	/// <param name="directory">待扫描目录，仅扫描该层，不深入子目录。</param>
	public static List<KeyValuePair<string, string>> ScanSerializedFiles(FileSystem fileSystem, string directory)
	{
		List<KeyValuePair<string, string>> files = [];
		foreach (string path in CollectCandidates(
			fileSystem,
			directory,
			static size => CouldBeSerializedFile(size),
			static (buffer, length, fileSize) => HeaderProbe.MatchesSerializedFile(buffer, length, fileSize)))
		{
			string name = MultiFileStream.GetFileName(path);
			files.Add(new(name, path));
		}
		return files;
	}

	/// <summary>
	/// 扫描一个目录，返回其中所有资源包的 (名称, 路径)，名称为不含扩展名的小写形式。
	/// </summary>
	public static List<KeyValuePair<string, string>> ScanBundles(FileSystem fileSystem, string directory)
	{
		List<KeyValuePair<string, string>> files = [];
		foreach (string path in CollectCandidates(
			fileSystem,
			directory,
			static size => CouldBeBundle(size),
			static (buffer, length, fileSize) => HeaderProbe.MatchesBundle(buffer, length)))
		{
			string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
			files.Add(new(name, path));
		}
		return files;
	}

	/// <summary>
	/// 枚举目录，先用扩展名与大小筛掉不可能命中的文件，再对候选并发读取文件头并逐个复验。
	/// </summary>
	/// <param name="fileSystem">用于访问文件。</param>
	/// <param name="directory">待扫描目录。</param>
	/// <param name="sizePredicate">基于文件大小的可能性判断，返回 <see langword="false"/> 的文件不会被读取。</param>
	/// <param name="headerPredicate">
	/// 基于文件头的判定，决定该文件是否属于本次要收集的种类。
	/// 参数为读到的字节、读到的字节数以及文件实际大小——序列化文件的判定需要拿头部声明的大小
	/// 与实际大小比对，而批量读头只读取前 <see cref="HeaderProbeLength"/> 个字节，拿不到真实大小。
	/// </param>
	/// <returns>通过类型判定的文件路径。</returns>
	private static IEnumerable<string> CollectCandidates(
		FileSystem fileSystem,
		string directory,
		Func<long, bool> sizePredicate,
		Func<byte[], int, long, bool> headerPredicate)
	{
		List<string> candidates = [];
		List<long> sizes = [];
		foreach (FileSystem.FileEntryInfo entry in fileSystem.Directory.EnumerateFileInfos(directory))
		{
			if (IsDefinitelyNotUnityContent(entry.Path) || !sizePredicate(entry.Length))
			{
				continue;
			}

			candidates.Add(entry.Path);
			sizes.Add(entry.Length);
		}

		if (candidates.Count == 0)
		{
			return [];
		}

		return FilterByHeader(fileSystem, candidates, sizes, headerPredicate);
	}

	/// <summary>
	/// 并发读取候选文件的头部并按指定种类判定，因此每个文件只读一次。
	/// </summary>
	/// <remarks>
	/// 判定必须**只针对本次收集的种类**：同一份读头结果会同时用于序列化文件与资源包两种扫描，
	/// 若在这里取两者的并集，同一个资源包会被两次收集、进而被加载两遍，内存与耗时直接翻倍。
	/// </remarks>
	private static List<string> FilterByHeader(
		FileSystem fileSystem,
		List<string> candidates,
		List<long> sizes,
		Func<byte[], int, long, bool> headerPredicate)
	{
		byte[][] buffers = new byte[candidates.Count][];
		for (int i = 0; i < buffers.Length; i++)
		{
			buffers[i] = new byte[HeaderProbeLength];
		}

		int concurrency = LocalFileSystem.DefaultHeaderReadConcurrency;
		int[] lengths = fileSystem.BatchReadHeaderPrefix(candidates, HeaderProbeLength, buffers, concurrency);

		List<string> results = [];
		for (int i = 0; i < candidates.Count; i++)
		{
			int length = lengths[i];
			if (length <= 0)
			{
				// 读取失败的文件保持原实现的语义：它会被当作“不是目标类型”而跳过
				continue;
			}

			if (headerPredicate(buffers[i], length, sizes[i]))
			{
				results.Add(candidates[i]);
			}
		}

		return results;
	}
}
