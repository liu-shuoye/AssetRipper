using AssetRipper.Import.Logging;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 用户项目资产索引：扫描用户提供的 Unity 项目根目录或资产文件夹，
/// 建立 (资产类别, 名称) 到 (源文件, GUID, 目标相对路径) 的映射，供导出时替换同名资产。
/// </summary>
/// <remarks>
/// 由于构建后的游戏中资产原始 GUID 已丢失，匹配只能按"类型 + 名称"进行：
/// Shader 取 .shader 文件内 <c>Shader "名称"</c> 声明，其余类别取文件名（不含扩展名）。
/// </remarks>
public sealed class UserAssetIndex
{
	private readonly Dictionary<(UserAssetKind Kind, string Name), UserAssetEntry> entries = new(KeyComparer.Instance);

	/// <summary>索引中的条目总数。</summary>
	public int EntryCount => entries.Count;

	private UserAssetIndex()
	{
	}

	/// <summary>
	/// 尝试从给定路径构建索引。
	/// </summary>
	/// <param name="rootPath">用户提供的 Unity 项目根目录或资产文件夹路径。</param>
	/// <param name="index">构建成功时的索引。</param>
	/// <returns>路径无效或未索引到任何条目时返回 false（调用方应视为功能未启用，正常导出）。</returns>
	public static bool TryCreate(string rootPath, [NotNullWhen(true)] out UserAssetIndex? index)
	{
		index = null;
		if (string.IsNullOrWhiteSpace(rootPath))
		{
			return false;
		}

		string fullPath;
		try
		{
			fullPath = Path.GetFullPath(rootPath);
		}
		catch (Exception)
		{
			Logger.Warning(LogCategory.Export, $"用户项目路径 '{rootPath}' 不是有效路径，用户资产替换已禁用。");
			return false;
		}

		if (!Directory.Exists(fullPath))
		{
			Logger.Warning(LogCategory.Export, $"用户项目路径 '{fullPath}' 不存在，用户资产替换已禁用。");
			return false;
		}

		UserAssetIndex result = new();
		ScanStatistics statistics = new();

		if (Directory.Exists(Path.Combine(fullPath, "Assets")) || Directory.Exists(Path.Combine(fullPath, "ProjectSettings")))
		{
			// 形态一：Unity 项目根目录。扫描 Assets/ 与 Packages/ 下的一级子目录（嵌式包）。
			string assetsDirectory = Path.Combine(fullPath, "Assets");
			if (Directory.Exists(assetsDirectory))
			{
				result.ScanDirectory(assetsDirectory, "Assets", statistics);
			}

			string packagesDirectory = Path.Combine(fullPath, "Library/PackageCache");
			if (Directory.Exists(packagesDirectory))
			{
				foreach (string packageDirectory in EnumerateDirectoriesSafe(packagesDirectory))
				{
					// 嵌式包原样保留 Packages/<包名>/ 结构：Unity 自动识别含 package.json 的目录，
					// 无需修改导出项目的 manifest.json，且 asmdef 作用域与用户项目一致。
					string packageName = Path.GetFileName(packageDirectory);
					result.ScanDirectory(packageDirectory, $"Packages/{packageName}", statistics);
				}
			}
		}
		else
		{
			// 形态二：资产文件夹（如 spine-unity 包目录）。统一放到 Assets/<目录名>/ 下，路径可预测。
			string directoryName = new DirectoryInfo(fullPath).Name;
			result.ScanDirectory(fullPath, $"Assets/{directoryName}", statistics);
		}

		if (result.EntryCount == 0)
		{
			Logger.Warning(LogCategory.Export, $"用户项目路径 '{fullPath}' 中未找到任何可替换资产（需要带 .meta 的受支持类型文件），用户资产替换已禁用。");
			return false;
		}

		if (statistics.SkippedNoMeta > 0)
		{
			Logger.Warning(LogCategory.Export, $"用户项目中有 {statistics.SkippedNoMeta} 个文件因缺少 .meta 而未纳入替换索引。");
		}

		Logger.Info(LogCategory.Export, $"已建立用户项目资产索引：{result.EntryCount} 个条目（来源：{fullPath}）。");
		index = result;
		return true;
	}

	/// <summary>
	/// 按类别与名称查找用户资产条目（名称比较不区分大小写）。
	/// </summary>
	public bool TryGet(UserAssetKind kind, string name, [NotNullWhen(true)] out UserAssetEntry? entry)
	{
		return entries.TryGetValue((kind, name), out entry);
	}

	/// <summary>
	/// 递归扫描目录，把受支持类型且带 .meta 的文件加入索引。
	/// </summary>
	/// <param name="directory">要扫描的目录绝对路径。</param>
	/// <param name="relativeTargetPrefix">导出项目内的目标路径前缀（如 "Assets"、"Packages/com.example.x"）。</param>
	/// <param name="statistics">扫描统计（用于汇总警告，避免逐文件刷日志）。</param>
	private void ScanDirectory(string directory, string relativeTargetPrefix, ScanStatistics statistics)
	{
		IEnumerable<string> files;
		try
		{
			files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Warning(LogCategory.Export, $"无法访问用户项目目录 '{directory}'：{ex.Message}");
			return;
		}

		foreach (string filePath in files)
		{
			if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// package.json 是包清单而非游戏资产，按文件名匹配可能误伤同名 TextAsset。
			if (string.Equals(Path.GetFileName(filePath), "package.json", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			UserAssetKind? kind = GetKindByExtension(Path.GetExtension(filePath));
			if (kind is null)
			{
				continue;
			}

			string metaPath = filePath + ".meta";
			if (!File.Exists(metaPath))
			{
				statistics.SkippedNoMeta++;
				continue;
			}

			if (!TryReadGuidFromMeta(metaPath, out UnityGuid guid))
			{
				statistics.SkippedInvalidMeta++;
				continue;
			}

			// 名称来源：shader 解析文件内的 Shader "名称" 声明，其余类别用文件名。
			string? name = kind.Value is UserAssetKind.Shader
				? TryGetShaderName(filePath)
				: Path.GetFileNameWithoutExtension(filePath);
			if (string.IsNullOrWhiteSpace(name))
			{
				statistics.SkippedNoName++;
				continue;
			}

			name = name.Trim();
			string relativePath = Path.GetRelativePath(directory, filePath).Replace('\\', '/');
			UserAssetEntry entry = new(filePath, metaPath, guid, $"{relativeTargetPrefix}/{relativePath}");

			if (!entries.TryAdd((kind.Value, name), entry))
			{
				UserAssetEntry existing = entries[(kind.Value, name)];
				Logger.Warning(LogCategory.Export, $"用户项目中存在重名资产 '{name}'（{kind.Value}）：'{filePath}' 与 '{existing.SourceFilePath}'，仅首个生效。");
			}
		}
	}

	/// <summary>
	/// 文件扩展名到资产类别的映射（v1 仅支持单文件、主资产 fileID 确定的类型）。
	/// </summary>
	private static UserAssetKind? GetKindByExtension(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".shader" => UserAssetKind.Shader,
			".mat" => UserAssetKind.Material,
			".cs" => UserAssetKind.Script,
			".png" or ".tga" or ".jpg" or ".jpeg" or ".psd" or ".tif" or ".tiff" or ".exr" or ".bmp" => UserAssetKind.Texture,
			".wav" or ".mp3" or ".ogg" or ".aif" or ".aiff" or ".flac" => UserAssetKind.Audio,
			".txt" or ".json" or ".xml" or ".csv" or ".bytes" => UserAssetKind.Text,
			_ => null,
		};
	}

	/// <summary>
	/// 从 .shader 文件中解析 <c>Shader "名称"</c> 声明（首个匹配生效）。
	/// </summary>
	private static string? TryGetShaderName(string filePath)
	{
		try
		{
			foreach (string line in File.ReadLines(filePath))
			{
				Match match = ShaderNameRegex.Match(line);
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Warning(LogCategory.Export, $"无法读取 shader 文件 '{filePath}'：{ex.Message}");
		}
		return null;
	}

	private static Regex ShaderNameRegex { get; } = new(@"^\s*Shader\s+""([^""]+)""", RegexOptions.Compiled);

	/// <summary>
	/// 从 .meta 文件解析 guid 行。
	/// </summary>
	private static bool TryReadGuidFromMeta(string metaPath, out UnityGuid guid)
	{
		guid = default;
		try
		{
			foreach (string line in File.ReadLines(metaPath))
			{
				// guid 行固定出现在文件开头附近，找到即止
				ReadOnlySpan<char> span = line.AsSpan().TrimStart();
				if (span.StartsWith("guid:", StringComparison.Ordinal))
				{
					ReadOnlySpan<char> value = span[5..].Trim();
					return TryParseGuid(value, out guid);
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Warning(LogCategory.Export, $"无法读取 meta 文件 '{metaPath}'：{ex.Message}");
		}
		return false;
	}

	/// <summary>
	/// 解析 .meta 中的 32 位十六进制 GUID 文本。
	/// </summary>
	/// <remarks>
	/// <see cref="UnityGuid.Parse(string)"/> 走的是 System.Guid 标准格式，不适用于 .meta 的 32 位 hex 形式。
/// 这里实现与 <see cref="UnityGuid.ToString()"/> 互逆的解析：ToString 对每个 uint 自低位起输出 8 个 nibble 字符。
	/// </remarks>
	private static bool TryParseGuid(ReadOnlySpan<char> text, out UnityGuid guid)
	{
		guid = default;
		if (text.Length != 32)
		{
			return false;
		}

		Span<uint> data = stackalloc uint[4];
		for (int i = 0; i < 4; i++)
		{
			uint value = 0;
			for (int j = 0; j < 8; j++)
			{
				int nibble = FromHex(text[i * 8 + j]);
				if (nibble < 0)
				{
					return false;
				}
				value |= (uint)nibble << (j * 4);
			}
			data[i] = value;
		}

		guid = new UnityGuid(data[0], data[1], data[2], data[3]);
		return true;
	}

	private static int FromHex(char c)
	{
		if (c is >= '0' and <= '9')
		{
			return c - '0';
		}
		if (c is >= 'a' and <= 'f')
		{
			return c - 'a' + 10;
		}
		if (c is >= 'A' and <= 'F')
		{
			return c - 'A' + 10;
		}
		return -1;
	}

	private static IEnumerable<string> EnumerateDirectoriesSafe(string path)
	{
		try
		{
			return Directory.EnumerateDirectories(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Warning(LogCategory.Export, $"无法访问用户项目目录 '{path}'：{ex.Message}");
			return [];
		}
	}

	/// <summary>
	/// 索引键比较器：类别一致且名称不区分大小写相等。
	/// </summary>
	private sealed class KeyComparer : IEqualityComparer<(UserAssetKind Kind, string Name)>
	{
		public static KeyComparer Instance { get; } = new();

		public bool Equals((UserAssetKind Kind, string Name) x, (UserAssetKind Kind, string Name) y)
		{
			return x.Kind == y.Kind && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
		}

		public int GetHashCode((UserAssetKind Kind, string Name) obj)
		{
			return HashCode.Combine(obj.Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
		}
	}

	/// <summary>
	/// 扫描统计，用于构建结束时汇总警告。
	/// </summary>
	private sealed class ScanStatistics
	{
		public int SkippedNoMeta { get; set; }
		public int SkippedInvalidMeta { get; set; }
		public int SkippedNoName { get; set; }
	}
}
