using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.CompressedFiles;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.Streams;
using System.Diagnostics;

namespace AssetRipper.Import.Structure;

/// <summary>
/// 依赖关系映射扫描器：遍历完整游戏文件夹，建立「依赖名 → 磁盘绝对路径」映射，
/// 供加载子文件夹时解析不在打开范围内的依赖文件（如 sharedassets0.assets）。
/// </summary>
public static class DependencyMapScanner
{
	public const string DefaultFileName = "AssetRipper.DependencyMap.json";

	/// <summary>扫描文件夹，生成依赖关系映射（不写盘）。</summary>
	public static DependencyMap Scan(string rootPath, FileSystem fileSystem)
	{
		// 默认跳过依赖关系文件自身，避免把上次扫描结果当作游戏文件解析
		return ScanCore(rootPath, fileSystem, [DefaultFileName]);
	}

	/// <summary>
	/// 扫描并写入依赖关系文件。outputPath 为空时默认 rootPath 下 AssetRipper.DependencyMap.json。返回实际输出路径。
	/// </summary>
	public static string ScanToFile(string rootPath, string? outputPath, FileSystem fileSystem)
	{
		string actualOutputPath = string.IsNullOrEmpty(outputPath)
			? fileSystem.Path.Join(rootPath, DefaultFileName)
			: outputPath;

		// 自定义输出文件可能已存在（上次扫描结果），同样需要跳过
		DependencyMap map = ScanCore(rootPath, fileSystem, [DefaultFileName, fileSystem.Path.GetFileName(actualOutputPath)]);
		map.Save(actualOutputPath);
		return actualOutputPath;
	}

	/// <summary>扫描核心实现。skippedFileNames 为需要跳过的文件名集合（忽略大小写）。</summary>
	private static DependencyMap ScanCore(string rootPath, FileSystem fileSystem, string[] skippedFileNames)
	{
		// Windows 文件名大小写不敏感，比较键也忽略大小写
		HashSet<string> skipped = new(skippedFileNames, StringComparer.OrdinalIgnoreCase);
		HashSet<string> processedPaths = new(StringComparer.OrdinalIgnoreCase);
		DependencyMap map = new();
		int totalCount = 0;
		int successCount = 0;
		int failedCount = 0;

		Logger.Info(LogCategory.Import, $"依赖关系扫描开始：'{rootPath}'");
		Stopwatch stopwatch = Stopwatch.StartNew();

		foreach (string enumeratedPath in EnumerateAllFiles(rootPath, fileSystem))
		{
			// 跳过依赖关系文件自身（含历史输出），避免把映射文件当作游戏文件解析
			if (skipped.Contains(fileSystem.Path.GetFileName(enumeratedPath)))
			{
				continue;
			}

			// split 文件（xx.split0/1/2…）归一到基础路径：加载时 MultiFileStream 会一次打开全部分片，
			// 因此同一基础路径只处理一次，避免重复加载同一份内容
			string filePath = MultiFileStream.GetFilePath(enumeratedPath);
			if (!processedPaths.Add(filePath))
			{
				continue;
			}

			totalCount++;

			// 逐文件打印进度：大文件夹扫描耗时较长，让用户确认扫描仍在进行以及当前处理位置
			Logger.Info(LogCategory.Import, $"依赖关系扫描 [{totalCount}]：'{filePath}'");

			FileBase file;
			try
			{
				file = SchemeReader.LoadFile(enumeratedPath, fileSystem);
				file.ReadContentsRecursively();
			}
			catch (Exception ex) // 单个文件解析失败不应中断整体扫描
			{
				Logger.Warning(LogCategory.Import, $"依赖关系扫描：加载文件失败 '{enumeratedPath}'：{ex.Message}");
				failedCount++;
				continue;
			}

			// 及时释放加载的文件及其底层流，扫描大量文件时控制内存峰值
			using FileBase loadedFile = file;

			// 展开压缩文件（gzip/brotli），取解压后的实际内容，与 GameBundle.FromPaths 的处理方式一致。
			// UncompressedFile 可能为 null（解压失败），后续模式匹配对 null 均不命中，安全跳过
			FileBase? content = file;
			while (content is CompressedFile compressedFile)
			{
				content = compressedFile.UncompressedFile;
			}

			// SchemeReader 无法识别的内容（损坏/加密等）会以 FailedFile 呈现，按失败统计跳过
			if (content is FailedFile)
			{
				failedCount++;
				continue;
			}

			successCount++;

			// a/b/d 键对任何成功加载的文件都有记录价值：依赖可能按文件名（含/不含扩展名）或相对路径引用
			string fileName = MultiFileStream.GetFileName(filePath);
			map.Add(fileName.ToLowerInvariant(), filePath);
			map.Add(fileSystem.Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant(), filePath);
			map.Add(GetRelativePathKey(rootPath, filePath, fileSystem), filePath);

			// c 键：SerializedFile 的 NameFixed 与依赖名（FileIdentifier.PathName 规范化后）格式一致，
			// 是依赖解析最精确的键，因此仅对含 SerializedFile 内容的文件额外记录
			if (content is SerializedFile serializedFile)
			{
				map.Add(serializedFile.NameFixed, filePath);
			}
			else if (content is FileContainer container)
			{
				foreach (SerializedFile nestedFile in container.FetchSerializedFiles())
				{
					map.Add(nestedFile.NameFixed, filePath);
				}
			}
		}

		stopwatch.Stop();
		// 完成日志带上耗时与统计，与逐文件进度日志首尾呼应，方便判断扫描是否正常结束
		Logger.Info(LogCategory.Import, $"依赖关系扫描完成：共 {totalCount} 个文件，成功 {successCount} 个，失败 {failedCount} 个，映射条目 {map.Entries.Count} 个，耗时 {stopwatch.Elapsed.TotalSeconds:F1} 秒。");
		return map;
	}

	/// <summary>递归枚举 rootPath 下所有文件（含子目录）。</summary>
	private static IEnumerable<string> EnumerateAllFiles(string rootPath, FileSystem fileSystem)
	{
		foreach (string file in fileSystem.Directory.EnumerateFiles(rootPath))
		{
			yield return file;
		}

		foreach (string directory in fileSystem.Directory.EnumerateDirectories(rootPath))
		{
			foreach (string file in EnumerateAllFiles(directory, fileSystem))
			{
				yield return file;
			}
		}
	}

	/// <summary>计算相对 rootPath 的相对路径键：统一正斜杠与小写，与依赖名格式保持一致。</summary>
	private static string GetRelativePathKey(string rootPath, string filePath, FileSystem fileSystem)
	{
		// 依赖路径统一使用正斜杠（Unity 内部格式），并小写化以匹配查找键规范
		return fileSystem.Path.GetRelativePath(rootPath, filePath).Replace('\\', '/').ToLowerInvariant();
	}
}
