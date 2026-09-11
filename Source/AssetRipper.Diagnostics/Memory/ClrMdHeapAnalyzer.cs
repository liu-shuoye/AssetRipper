using System.Diagnostics;
using Microsoft.Diagnostics.Runtime;

namespace AssetRipper.Diagnostics.Memory;

/// <summary>
/// 一次整堆快照的按类型聚合结果。
/// </summary>
public sealed class HeapSnapshotResult
{
	/// <summary>按占用字节降序排列的托管类型聚合。</summary>
	public required IReadOnlyList<TypeAggregate> Types { get; init; }

	/// <summary>堆中对象总数（含尚未被复用的 Free 空洞）。</summary>
	public long TotalCount { get; init; }

	/// <summary>堆中对象占用的总字节数。</summary>
	public long TotalSize { get; init; }

	/// <summary>堆段（segment）数量。</summary>
	public int SegmentCount { get; init; }

	/// <summary>是否使用服务器 GC。</summary>
	public bool IsServerGC { get; init; }

	/// <summary>数据来源的可读描述（当前进程快照 / 附加 / 转储文件）。</summary>
	public required string Source { get; init; }

	/// <summary>是否因达到对象数上限而提前结束了遍历，此时各项统计只是抽样结果。</summary>
	public bool Truncated { get; init; }

	/// <summary>取快照与遍历堆的总耗时。</summary>
	public TimeSpan Elapsed { get; init; }
}

/// <summary>
/// 用 ClrMD 抓取整堆快照，并按托管类型聚合对象数量与占用字节数。
/// </summary>
/// <remarks>
/// 与 <see cref="ManagedSizeCalculator"/> 的反射估算不同，这里读的是 GC 堆本身：结果包含 BCL、第三方库
/// 以及快照瞬间全部存活的对象，因此累加值可与 <c>GC.GetTotalMemory</c>（或转储文件的托管堆大小）直接对齐，
/// 用来回答"内存究竟在谁手里"。
/// 注意：取快照会短暂挂起目标进程再读取（读到的是一致的内存镜像），不要在持有锁或处于不可中断的关键区时调用。
/// 由于 <see cref="ClrInfo.CreateRuntime"/> 先于堆遍历执行，快照里也会带上 ClrMD 自身为读堆而建立的少量对象
/// （数百 KB 量级，通常出现在类型榜末尾），量级上可忽略，但看到时不必惊讶。
/// 所有失败都以返回值表达，不向上抛异常。
/// </remarks>
public static class ClrMdHeapAnalyzer
{
	/// <summary>无法取到类型名时的归并桶（例如元数据缺失的对象）。</summary>
	private const string UnnamedType = "(无法识别类型)";

	/// <summary>
	/// 抓取整堆快照并按类型聚合。
	/// </summary>
	/// <param name="dumpPath">转储文件路径；为 null 或空白时对当前进程取快照。</param>
	/// <param name="maxObjects">遍历对象数上限，&lt;= 0 表示不限制。</param>
	/// <param name="result">成功时的聚合结果。</param>
	/// <param name="error">失败原因，仅在返回 false 时非空。</param>
	/// <returns>成功返回 true；平台不支持、DAC 缺失、堆不可遍历等失败情况返回 false。</returns>
	public static bool TryAnalyze(
		string? dumpPath,
		int maxObjects,
		[NotNullWhen(true)] out HeapSnapshotResult? result,
		[NotNullWhen(false)] out string? error)
	{
		result = null;
		error = null;

		Stopwatch stopwatch = Stopwatch.StartNew();
		DataTarget? target = null;
		ClrRuntime? runtime = null;
		try
		{
			target = OpenTarget(dumpPath, out string source);

			ClrInfo? clrInfo = target.ClrVersions.FirstOrDefault();
			if (clrInfo is null)
			{
				error = "未在目标中发现 CLR 运行时";
				return false;
			}

			// 创建运行时需要匹配版本的 DAC，缺失时会在这里抛出，由外层统一转成失败原因
			runtime = clrInfo.CreateRuntime();
			ClrHeap heap = runtime.Heap;
			if (!heap.CanWalkHeap)
			{
				error = "堆不可遍历（快照不完整，或快照时 GC 正在搬移对象）";
				return false;
			}

			Dictionary<string, TypeStat> byType = new();
			long totalCount = 0;
			long totalSize = 0;
			bool truncated = false;
			foreach (ClrObject obj in heap.EnumerateObjects())
			{
				if (!obj.IsValid)
				{
					continue;
				}

				// Free 是段内尚未分配的空洞而非真实对象，但确实占着段空间；单独成桶才解释得清与段大小的差值
				string name = obj.Type?.Name ?? UnnamedType;
				long size = (long)obj.Size;

				byType.TryGetValue(name, out TypeStat stat);
				stat.Count++;
				stat.Size += size;
				byType[name] = stat;

				totalCount++;
				totalSize += size;

				if (maxObjects > 0 && totalCount >= maxObjects)
				{
					truncated = true;
					break;
				}
			}

			TypeAggregate[] types = byType
				.Select(kvp => new TypeAggregate(kvp.Key, kvp.Value.Count, kvp.Value.Size))
				.OrderByDescending(t => t.Size)
				.ToArray();

			result = new HeapSnapshotResult
			{
				Types = types,
				TotalCount = totalCount,
				TotalSize = totalSize,
				SegmentCount = heap.Segments.Count(),
				IsServerGC = heap.IsServer,
				Source = source,
				Truncated = truncated,
				Elapsed = stopwatch.Elapsed,
			};
			return true;
		}
		catch (Exception ex)
		{
			// 诊断本身失败不应该影响导出流程，因此一律吞掉异常并如实回报原因
			error = $"{ex.GetType().Name}: {ex.Message}";
			return false;
		}
		finally
		{
			runtime?.Dispose();
			target?.Dispose();
		}
	}

	/// <summary>
	/// 打开数据源：优先转储文件，否则对当前进程取快照。
	/// </summary>
	/// <param name="dumpPath">转储文件路径，可为空。</param>
	/// <param name="source">实际采用的数据源描述，写进日志便于判断数据可信度。</param>
	private static DataTarget OpenTarget(string? dumpPath, out string source)
	{
		if (!string.IsNullOrWhiteSpace(dumpPath))
		{
			source = $"转储文件 {dumpPath}";
			return DataTarget.LoadDump(dumpPath);
		}

		int processId = Environment.ProcessId;
		if (OperatingSystem.IsWindows())
		{
			try
			{
				// 快照方式：只在创建快照的瞬间挂起进程，之后读的是冻结镜像，堆数据自洽。
				// 直接附加到仍在运行的进程属于 ClrMD 明确不支持的用法，故只作为兜底。
				source = $"当前进程快照 (PID {processId})";
				return DataTarget.CreateSnapshotAndAttach(processId);
			}
			catch (Exception ex)
			{
				source = $"当前进程附加 (PID {processId}；快照不可用：{ex.GetType().Name})";
				return DataTarget.AttachToProcess(processId, suspend: false);
			}
		}

		// 非 Windows 平台不提供快照，只能退化为非挂起附加，一致性依赖调用时机（此处紧随一次强制 GC 之后）
		source = $"当前进程附加 (PID {processId}；非 Windows 平台无快照支持)";
		return DataTarget.AttachToProcess(processId, suspend: false);
	}

	/// <summary>聚合累加器：对象数量与字节数。</summary>
	private struct TypeStat
	{
		public long Count;
		public long Size;
	}
}
