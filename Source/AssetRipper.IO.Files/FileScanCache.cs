using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetRipper.IO.Files;

/// <summary>
/// 文件类型扫描结果的磁盘缓存：把「某目录下哪些文件是序列化文件 / 资源包」这一昂贵结论持久化，供后续导入直接复用。
/// </summary>
/// <remarks>
/// 缓存必然可能与磁盘现状不一致（文件被增删、被替换），因此每个目录都记录一份指纹（条目数与最新写入时间）。
/// 加载时逐目录比对指纹，只有完全吻合的目录才采用缓存，任何差异都回退到真实扫描。
/// 这种「不匹配即重扫」的策略让缓存不可能导致漏文件，代价只是失效目录多花一次扫描。
/// </remarks>
public static class FileScanCache
{
	/// <summary>当前缓存文件格式版本，结构变更时用于丢弃旧缓存。</summary>
	public const int CurrentVersion = 1;

	/// <summary>缓存文件默认文件名。</summary>
	public const string DefaultFileName = "AssetRipper.FileScanCache.json";

	/// <summary>
	/// 尝试加载缓存，返回「(目录, 扫描类型) → 条目」的字典；不可用或校验失败时返回 <see langword="null"/>。
	/// </summary>
	/// <param name="cachePath">缓存文件路径。</param>
	/// <param name="rootPath">扫描根目录，用于校验缓存是否属于同一份游戏资源。</param>
	/// <param name="fileSystem">用于访问文件。</param>
	/// <param name="kindLookup">全部可能出现的扫描类型：缓存中的类型名 → 调用方枚举值。</param>
	/// <param name="logWarning">可选的诊断回调，用于把缓存不可用的原因暴露给上层日志。</param>
	public static Dictionary<TKey, List<KeyValuePair<string, string>>>? TryLoad<TKey>(
		string cachePath,
		string? rootPath,
		FileSystem fileSystem,
		IReadOnlyDictionary<string, TKey> kindLookup,
		Action<string>? logWarning = null)
		where TKey : struct
	{
		try
		{
			if (!fileSystem.File.Exists(cachePath))
			{
				return null;
			}

			FileScanCacheModel? model = JsonSerializer.Deserialize(fileSystem.File.ReadAllText(cachePath), FileScanCacheContext.Default.FileScanCacheModel);
			if (model is null)
			{
				logWarning?.Invoke($"文件扫描缓存内容为空：'{cachePath}'");
				return null;
			}

			if (model.Version != CurrentVersion)
			{
				logWarning?.Invoke($"文件扫描缓存版本不匹配（文件为 {model.Version}，当前为 {CurrentVersion}），将重新扫描：'{cachePath}'");
				return null;
			}

			// 根目录变化意味着缓存对应的是另一份资源，直接作废
			if (rootPath is not null && !string.Equals(model.RootPath, rootPath, StringComparison.OrdinalIgnoreCase))
			{
				logWarning?.Invoke($"文件扫描缓存对应的根目录已变化，将重新扫描：'{cachePath}'");
				return null;
			}

			Dictionary<TKey, List<KeyValuePair<string, string>>> result = [];

			foreach (FileScanCacheEntry entry in model.Entries)
			{
				if (!kindLookup.TryGetValue(entry.Kind, out TKey kind))
				{
					continue;
				}

				if (!FingerprintMatches(entry, fileSystem, logWarning))
				{
					// 目录已变动：跳过该目录，让其走真实扫描，从而保证不会漏掉新增文件
					continue;
				}

				List<KeyValuePair<string, string>> files = new(entry.Files.Count);
				foreach (FileScanCacheItem item in entry.Files)
				{
					files.Add(new(item.Name, item.Path));
				}

				result[kind] = files;
			}

			return result.Count > 0 ? result : null;
		}
		catch (Exception ex) // 缓存损坏、磁盘 IO 失败等都不应中断导入流程
		{
			logWarning?.Invoke($"文件扫描缓存加载失败：'{cachePath}'：{ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// 把本次扫描结果写入缓存文件。写入失败只通过回调报告，不影响导入流程。
	/// </summary>
	/// <param name="logWarning">可选的诊断回调。</param>
	public static void Save<TKey>(
		string cachePath,
		string? rootPath,
		FileSystem fileSystem,
		Dictionary<TKey, List<KeyValuePair<string, string>>> additions,
		Func<TKey, string> keySelector,
		Action<string>? logWarning = null)
		where TKey : struct
	{
		try
		{
			FileScanCacheModel model = new()
			{
				Version = CurrentVersion,
				RootPath = rootPath,
			};

			foreach (KeyValuePair<TKey, List<KeyValuePair<string, string>>> pair in additions)
			{
				if (!TryGetDirectory(pair.Key, out string? directory))
				{
					continue;
				}

				FileScanCacheEntry entry = new()
				{
					Directory = directory,
					Kind = keySelector(pair.Key),
				};

				foreach (KeyValuePair<string, string> file in pair.Value)
				{
					entry.Files.Add(new(file.Key, file.Value));
				}

				ApplyFingerprint(entry, fileSystem);
				model.Entries.Add(entry);
			}

			using Stream stream = fileSystem.File.Create(cachePath);
			JsonSerializer.Serialize(stream, model, FileScanCacheContext.Default.FileScanCacheModel);
		}
		catch (Exception ex)
		{
			logWarning?.Invoke($"文件扫描缓存保存失败：'{cachePath}'：{ex.Message}");
		}
	}

	/// <summary>
	/// 从缓存键中取出目录部分。约定键的第一个字段为目录路径。
	/// </summary>
	private static bool TryGetDirectory<TKey>(TKey key, [NotNullWhen(true)] out string? directory)
		where TKey : struct
	{
		if (key is ITuple tuple && tuple.Length > 0 && tuple[0] is string path)
		{
			directory = path;
			return true;
		}

		directory = null;
		return false;
	}

	/// <summary>
	/// 记录目录指纹：条目数与最新写入时间。这两项足以发现新增、删除与替换。
	/// </summary>
	private static void ApplyFingerprint(FileScanCacheEntry entry, FileSystem fileSystem)
	{
		try
		{
			if (fileSystem.File.GetLocalPath(entry.Directory) is not { } localPath)
			{
				return;
			}

			System.IO.DirectoryInfo info = new(localPath);
			entry.FileCount = info.EnumerateFiles().Count();
			entry.LastWriteTicks = info.LastWriteTimeUtc.Ticks;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			// 拿不到指纹时留空，加载端会因指纹不匹配而退回真实扫描，等价于本次不缓存
		}
	}

	/// <summary>
	/// 校验目录指纹是否仍然吻合。任一字段缺失或不同即视为失效。
	/// </summary>
	private static bool FingerprintMatches(FileScanCacheEntry entry, FileSystem fileSystem, Action<string>? logWarning)
	{
		try
		{
			if (fileSystem.File.GetLocalPath(entry.Directory) is not { } localPath)
			{
				return false;
			}

			System.IO.DirectoryInfo info = new(localPath);
			bool matches = info.EnumerateFiles().Count() == entry.FileCount
				&& info.LastWriteTimeUtc.Ticks == entry.LastWriteTicks;

			if (!matches)
			{
				logWarning?.Invoke($"文件扫描缓存已失效（目录内容变动），将重新扫描：'{entry.Directory}'");
			}

			return matches;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			return false;
		}
	}
}

/// <summary>缓存文件模型。</summary>
public sealed class FileScanCacheModel
{
	/// <summary>格式版本。</summary>
	public int Version { get; set; } = FileScanCache.CurrentVersion;

	/// <summary>缓存对应的扫描根目录。</summary>
	public string? RootPath { get; set; }

	/// <summary>各目录的扫描结果。</summary>
	public List<FileScanCacheEntry> Entries { get; set; } = [];
}

/// <summary>单个目录的扫描结果与指纹。</summary>
public sealed class FileScanCacheEntry
{
	/// <summary>被扫描的目录。</summary>
	public string Directory { get; set; } = string.Empty;

	/// <summary>扫描类型，与调用方的枚举名对应。</summary>
	public string Kind { get; set; } = string.Empty;

	/// <summary>扫描时的目录文件条目数。</summary>
	public int FileCount { get; set; }

	/// <summary>扫描时的目录写入时间（UTC ticks）。</summary>
	public long LastWriteTicks { get; set; }

	/// <summary>扫描结果。</summary>
	public List<FileScanCacheItem> Files { get; set; } = [];
}

/// <summary>缓存中的一条文件记录。</summary>
public sealed class FileScanCacheItem
{
	/// <summary>文件在收集结果中使用的键。</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>文件路径。</summary>
	public string Path { get; set; } = string.Empty;

	public FileScanCacheItem() { }

	public FileScanCacheItem(string name, string path)
	{
		Name = name;
		Path = path;
	}
}

/// <summary>
/// 为缓存模型提供 AOT 友好的源生成序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(FileScanCacheModel))]
internal sealed partial class FileScanCacheContext : JsonSerializerContext
{
}
