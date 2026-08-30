using AssetRipper.Import.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetRipper.Import.Structure;

/// <summary>
/// 依赖关系文件：记录依赖名到文件绝对路径的映射，用于加载时解析不在打开文件夹内的依赖文件。
/// </summary>
public sealed class DependencyMap
{
	/// <summary>当前文件格式版本，用于未来结构变更时做兼容判断。</summary>
	public const int CurrentVersion = 1;

	/// <summary>文件格式版本，加载时与 <see cref="CurrentVersion"/> 比对。</summary>
	public int Version { get; set; } = CurrentVersion;

	/// <summary>依赖名（小写规范化）→ 文件绝对路径</summary>
	public Dictionary<string, string> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// 添加映射。name 统一小写化，与 FileIdentifier.PathName（经 SpecialFileNames.FixFileIdentifier 规范化）格式匹配。
	/// </summary>
	public void Add(string name, string path)
	{
		// 依赖查找键统一小写：PathName 经 FixFileIdentifier 规范化后即为小写，
		// 这里提前归一，避免大小写差异导致查找失败
		Entries[name.ToLowerInvariant()] = path;
	}

	/// <summary>按依赖名查询绝对路径，name 小写化后匹配。</summary>
	public bool TryResolve(string name, out string path)
	{
		if (Entries.TryGetValue(name.ToLowerInvariant(), out string? resolved))
		{
			path = resolved;
			return true;
		}

		// 未命中时给空串占位，调用方以返回值为准
		path = string.Empty;
		return false;
	}

	/// <summary>
	/// 从 JSON 文件加载。文件不存在或格式非法时返回 null 并用 Logger 记录警告（LogCategory.Import），不抛异常。
	/// </summary>
	public static DependencyMap? Load(string path)
	{
		if (!File.Exists(path))
		{
			Logger.Warning(LogCategory.Import, $"依赖关系文件不存在：'{path}'");
			return null;
		}

		try
		{
			DependencyMap? map = JsonSerializer.Deserialize(File.ReadAllText(path), DependencyMapContext.Default.DependencyMap);
			if (map is null)
			{
				Logger.Warning(LogCategory.Import, $"依赖关系文件内容为空：'{path}'");
				return null;
			}

			// 反序列化得到的字典使用默认比较器，重建以恢复大小写不敏感查找；
			// JSON 中显式写 null 时回退为空字典，避免后续空引用
			map.Entries = new Dictionary<string, string>(map.Entries ?? [], StringComparer.OrdinalIgnoreCase);

			if (map.Version != CurrentVersion)
			{
				// 版本不一致时仍按当前结构使用；未来若格式不兼容，应在此处迁移或拒绝加载
				Logger.Warning(LogCategory.Import, $"依赖关系文件版本不匹配：文件为 {map.Version}，当前为 {CurrentVersion}：'{path}'");
			}

			return map;
		}
		catch (Exception ex) // JSON 损坏、磁盘 IO 失败等不应中断整体加载流程
		{
			Logger.Warning(LogCategory.Import, $"依赖关系文件加载失败：'{path}'：{ex.Message}");
			return null;
		}
	}

	/// <summary>保存为缩进 JSON。</summary>
	public void Save(string path)
	{
		// 用 File.Create 覆盖写入，保证每次扫描都生成最新映射
		using FileStream fileStream = File.Create(path);
		JsonSerializer.Serialize(fileStream, this, DependencyMapContext.Default.DependencyMap);
	}
}

/// <summary>
/// 为 <see cref="DependencyMap"/> 提供 AOT 友好的源生成序列化上下文。
/// 项目启用了 IsAotCompatible，必须使用源生成而非运行时反射。
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DependencyMap))]
internal sealed partial class DependencyMapContext : JsonSerializerContext
{
}
