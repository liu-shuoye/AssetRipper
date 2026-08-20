using AssetRipper.Import.Logging;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 用户项目资产索引：扫描用户提供的 Unity 项目根目录或资产文件夹，
/// 建立 (资产类别, 名称, 着色器名) 到 (源文件, GUID, 目标相对路径) 的映射，供导出时替换同名资产。
/// </summary>
/// <remarks>
/// 由于构建后的游戏中资产原始 GUID 已丢失，匹配只能按"类型 + 名称"进行。
/// 但材质不能仅以名称判断是否同一资源：同名材质可能引用完全不同的着色器（从而属性、外观都不同）。
/// 因此材质以 (名称, 着色器名) 复合键区分，与引擎 <c>PredefinedAssetCache.MaterialKey</c> 的判定口径一致；
/// 着色器名无法解析时（如内置着色器）退化为仅按名称匹配，此时同名仍可能误判，属已知限制。
/// Shader 取 .shader 文件内 <c>Shader "名称"</c> 声明，其余类别取文件名（不含扩展名）；
/// 材质另取 .mat 内 <c>m_Name</c> 作为名称（与导出侧 <c>named.Name</c> 一致），而非文件名。
/// </remarks>
public sealed class UserAssetIndex
{
	// 资产类别不同，键的含义不同：Shader 的 Shader 分量为 null，材质用 Shader 分量着色器名区分。
	private readonly Dictionary<AssetKey, UserAssetEntry> entries = new();
	private readonly Dictionary<UnityGuid, string> shaderNameByGuid = new();
	private readonly List<PendingMaterial> pendingMaterials = new();

	/// <summary>索引中的条目总数。</summary>
	public int EntryCount => entries.Count;

	private UserAssetIndex() { }

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

		// 扫描阶段把材质先收集为待定项（其着色器名需依赖已索引的着色器 guid 映射才能解析），
		// 故在所有目录扫描完毕后再统一建键，确保跨 Assets/Packages 的着色器都能被解析到。
		result.ResolvePendingMaterials();

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
	/// 按类别、名称与（材质专用的）着色器名查找用户资产条目。
	/// </summary>
	/// <param name="shaderName">材质的着色器名；非材质类别传 null。名称与着色器名比较均不区分大小写。</param>
	public bool TryGet(UserAssetKind kind, string name, string? shaderName, [NotNullWhen(true)] out UserAssetEntry? entry)
	{
		return entries.TryGetValue(new AssetKey(kind, name, shaderName), out entry);
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

			if (kind.Value is UserAssetKind.Shader)
			{
				// 同步建立 guid -> 着色器名 映射，供后续材质解析其引用的着色器名。
				shaderNameByGuid[guid] = name;
				AddEntry(UserAssetKind.Shader, name, null, entry);
			}
			else if (kind.Value is UserAssetKind.Material)
			{
				// 材质不能仅用名称判等：先读出 m_Name 与 m_Shader 的 guid，待全量扫描后再按 (名称, 着色器名) 建键。
				if (!TryReadMaterialInfo(filePath, out string? materialName, out UnityGuid shaderGuid))
				{
					statistics.SkippedNoName++;
					continue;
				}

				if (string.IsNullOrWhiteSpace(materialName))
				{
					materialName = name; // 兜底用文件名
				}

				materialName = materialName.Trim();
				pendingMaterials.Add(new PendingMaterial(filePath, metaPath, guid, $"{relativeTargetPrefix}/{relativePath}", materialName, shaderGuid));
			}
			else
			{
				AddEntry(kind.Value, name, null, entry);
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
			// ".cs" => UserAssetKind.Script,
			// ".png" or ".tga" or ".jpg" or ".jpeg" or ".psd" or ".tif" or ".tiff" or ".exr" or ".bmp" => UserAssetKind.Texture,
			// ".wav" or ".mp3" or ".ogg" or ".aif" or ".aiff" or ".flac" => UserAssetKind.Audio,
			// ".txt" or ".json" or ".xml" or ".csv" or ".bytes" => UserAssetKind.Text,
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

	// 材质文件解析：m_Name 可能在文件任意位置；m_Shader 为单行流程映射（含 32 位 guid）。
	private static Regex MaterialNameRegex { get; } = new(@"^\s*m_Name:\s*(.*)$", RegexOptions.Compiled);
	private static Regex MaterialShaderRegex { get; } = new(@"m_Shader:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

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
	/// 索引键：资产类别 + 名称 +（材质专用的）着色器名。
	/// 非材质类别的 Shader 分量为 null；材质以 (名称, 着色器名) 区分"同一资源"。
	/// 名称与着色器名比较均不区分大小写，与历史行为及跨侧来源的大小写差异兼容。
	/// </summary>
	private struct AssetKey : IEquatable<AssetKey>
	{
		public UserAssetKind Kind { get; set; }
		public string Name { get; set; }
		public string? Shader { get; set; }

		public AssetKey(UserAssetKind kind, string name, string? shader)
		{
			Kind = kind;
			Name = name;
			Shader = shader;
		}

		public bool Equals(AssetKey other) =>
			Kind == other.Kind
			&& string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(Shader, other.Shader, StringComparison.OrdinalIgnoreCase);

		public override bool Equals(object? obj) => obj is AssetKey other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(Kind, StringComparer.OrdinalIgnoreCase.GetHashCode(Name), StringComparer.OrdinalIgnoreCase.GetHashCode(Shader ?? string.Empty));
	}

	/// <summary>
	/// 扫描阶段收集、待全量扫描后再建键的材质：其着色器名依赖 shaderNameByGuid 映射。
	/// </summary>
	/// <param name="SourceFilePath">材质源文件绝对路径。</param>
	/// <param name="SourceMetaPath">材质 .meta 文件绝对路径。</param>
	/// <param name="Guid">材质 GUID。</param>
	/// <param name="RelativeTargetPath">导出项目内相对目标路径。</param>
	/// <param name="Name">材质 m_Name（已裁剪空白）。</param>
	/// <param name="ShaderGuid">材质 m_Shader 引用的着色器 guid（用于解析着色器名）。</param>
	private sealed record PendingMaterial(string SourceFilePath, string SourceMetaPath, UnityGuid Guid, string RelativeTargetPath, string Name, UnityGuid ShaderGuid);

	/// <summary>
	/// 将一条资产加入索引；键冲突时仅保留首个并告警（同名不同文件视为同一资源，仅首个可用于替换）。
	/// </summary>
	private void AddEntry(UserAssetKind kind, string name, string? shader, UserAssetEntry entry)
	{
		AssetKey key = new(kind, name, shader);
		if (!entries.TryAdd(key, entry))
		{
			UserAssetEntry existing = entries[key];
			Logger.Warning(LogCategory.Export, $"用户项目中存在重名资产 '{FormatLabel(kind, name, shader)}'：'{entry.SourceFilePath}' 与 '{existing.SourceFilePath}'，仅首个生效。");
		}
	}

	/// <summary>
	/// 扫描结束后，将待定材质按 (名称, 着色器名) 建键加入索引。
	/// 着色器名由材质 m_Shader 的 guid 经已建立的映射解析；解析不到（如内置着色器）则着色器名为 null，退化为仅按名称匹配。
	/// </summary>
	private void ResolvePendingMaterials()
	{
		foreach (PendingMaterial pending in pendingMaterials)
		{
			shaderNameByGuid.TryGetValue(pending.ShaderGuid, out string? shaderName);
			AssetKey key = new(UserAssetKind.Material, pending.Name, shaderName);
			switch (pending.Name)
			{
				case "UnlitBlendModeNormalOver":
					key.Name = "Unlit";
					key.Shader = "Live2D Cubism/Unlit";
					break;
				case "UnlitBlendModeMaskedNormalOver":
					key.Name = "UnlitMasked";
					key.Shader = "Live2D Cubism/Unlit";
					break;
				case "UnlitBlendModeInvertMaskedNormalOver":
					key.Name = "UnlitMaskedInverted";
					key.Shader = "Live2D Cubism/Unlit";
					break;
				case "UnlitBlendModeMultiply":
					key.Name = "UnlitMultiply";
					key.Shader = "Live2D Cubism/Unlit";
					break;
			}

			UserAssetEntry entry = new(pending.SourceFilePath, pending.SourceMetaPath, pending.Guid, pending.RelativeTargetPath);
			if (!entries.TryAdd(key, entry))
			{
				UserAssetEntry existing = entries[key];
				Logger.Warning(LogCategory.Export, $"用户项目中存在重名材质 '{FormatLabel(UserAssetKind.Material, pending.Name, shaderName)}'：'{pending.SourceFilePath}' 与 '{existing.SourceFilePath}'，仅首个生效。");
			}
		}
	}

	/// <summary>
	/// 为人类可读的告警文本生成资产标签；材质附带着色器名，便于区分同名不同着色器的两个材质。
	/// </summary>
	private static string FormatLabel(UserAssetKind kind, string name, string? shader)
	{
		return kind is UserAssetKind.Material && shader is not null
			? $"{name}（着色器：{shader}）"
			: name;
	}

	/// <summary>
	/// 从 .mat 文件解析材质 m_Name 与其 m_Shader 引用的着色器 guid。
	/// m_Name 可能在文件任意位置，m_Shader 为单行流程映射（含 guid）。
	/// </summary>
	private static bool TryReadMaterialInfo(string filePath, out string? materialName, out UnityGuid shaderGuid)
	{
		materialName = null;
		shaderGuid = default;
		bool foundShader = false;
		try
		{
			foreach (string line in File.ReadLines(filePath))
			{
				if (materialName is null)
				{
					Match nameMatch = MaterialNameRegex.Match(line);
					if (nameMatch.Success)
					{
						string value = nameMatch.Groups[1].Value.Trim();
						// YAML 中 m_Name 可能带引号，去掉首尾引号以得到纯名称
						if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
						{
							value = value.Substring(1, value.Length - 2);
						}

						materialName = value;
					}
				}

				if (!foundShader)
				{
					Match shaderMatch = MaterialShaderRegex.Match(line);
					if (shaderMatch.Success && TryParseGuid(shaderMatch.Groups[1].Value, out UnityGuid guid))
					{
						shaderGuid = guid;
						foundShader = true;
					}
				}

				if (materialName is not null && foundShader)
				{
					break;
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Logger.Warning(LogCategory.Export, $"无法读取材质文件 '{filePath}'：{ex.Message}");
			return false;
		}

		// 有名称即可建键；着色器 guid 解析不到时退化为仅按名称匹配。
		return materialName is not null;
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
