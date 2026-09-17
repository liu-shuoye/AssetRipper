using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Logging;
using AssetRipper.SourceGenerated;

namespace AssetRipper.Diagnostics.Memory;

/// <summary>
/// 内存诊断：阶段内存快照（<see cref="LogMemoryDiagnostics"/>）与按资源类型的占用拆解（<see cref="LogResourceBreakdown"/>）。
/// </summary>
/// <remarks>
/// 拆解口径由调用方（通常来自设置）通过参数传入：
/// <list type="bullet">
///   <item><description>默认（空串）：已反序列化对象的<b>序列化字节数</b>（廉价、不反序列化）；</description></item>
///   <item><description><c>live</c>：用 <see cref="ManagedSizeCalculator"/> 反射估算<b>真实托管堆字节数</b>；</description></item>
///   <item><description><c>clrmd</c>：用 ClrMD 抓取<b>整个进程 GC 堆</b>的快照并按托管类型聚合（不需要资产集合）。</description></item>
/// </list>
/// 只有 <c>clrmd</c> 口径能在拿不到资产集合时工作；另外两个口径的数据就在资产集合里，因此两个入口都接受一个可选的资产集合参数。
/// 拆解需要资产对象模型，所以放在这里而不是零依赖的 <see cref="Logger"/> 里。
/// </remarks>
public static class MemoryDiagnostics
{
	/// <summary>类型占用榜的展示条数。</summary>
	private const int TypeTopN = 25;
	/// <summary>类型数量榜的展示条数。</summary>
	private const int CountTopN = 10;
	/// <summary>类型平均占用榜的展示条数。</summary>
	private const int AverageTopN = 25;
	/// <summary>命名空间分组与 Unity 资产类型共用的展示条数。</summary>
	private const int GroupTopN = 20;

	/// <summary>
	/// 平均占用榜的对象数下限。平均值样本太少就没有代表性（一个 800MB 的单例会直接把平均拉爆），
	/// 因此低于该数量的类型不参与平均排名。
	/// </summary>
	private const int AverageMinCount = 1000;

	/// <summary>自动拆解的默认触发阈值（MB），可用 <c>RURI_MEM_BREAKDOWN_GROWTH_MB</c> 覆盖。</summary>
	private const int DefaultBreakdownGrowthMegabytes = 1024;

	/// <summary>上次输出拆解时的托管堆字节数，用于判断"是否发生了大幅度增长"。</summary>
	private static long lastBreakdownManagedBytes;
	/// <summary>基线是否已建立。首个诊断点只建立基线而不拆解，避免把加载前的存量算成"增长"。</summary>
	private static bool hasBreakdownBaseline;

	/// <summary>
	/// 输出当前内存状态，用于定位哪个阶段内存上涨最多。
	/// 当托管堆较上次拆解增长超过阈值时，额外输出一次按资源类型的占用拆解。
	/// </summary>
	/// <param name="stage">当前阶段标识，用于日志区分。</param>
	/// <param name="collections">
	/// 供拆解枚举的资产集合（通常传 <c>gameData.GameBundle.FetchAssetCollections()</c>，惰性求值）。
	/// 为 null 时只有 <c>clrmd</c> 口径可用。
	/// </param>
	/// <param name="mode">拆解口径：<c>live</c> 反射估算托管堆、<c>clrmd</c> 抓取整堆快照，空串/其它值走默认口径（序列化字节数）。</param>
	public static void LogMemoryDiagnostics(string stage, IEnumerable<AssetCollection>? collections = null, string? mode = null)
	{
		long managedMemory = Logger.LogMemoryDiagnostics(stage);

		long threshold = GetBreakdownGrowthThresholdBytes();
		if (threshold <= 0)
		{
			return; // 阈值设为 0 表示关闭自动拆解
		}

		if (!hasBreakdownBaseline)
		{
			hasBreakdownBaseline = true;
			lastBreakdownManagedBytes = managedMemory;
			return;
		}

		long growth = managedMemory - lastBreakdownManagedBytes;
		if (growth < threshold)
		{
			return;
		}

		// 详细拆解代价高（live 要反射遍历对象图，clrmd 要取一次整堆快照），因此只在托管堆明显上涨时做；
		// 拆解结束后会把基线前移，保证同一段增长只报一次
		Logger.Info(LogCategory.Processing,
			$"[内存诊断] ↑ {stage}: 托管堆较上次拆解增长 {ToMegabytes(growth):F1} MB（阈值 {ToMegabytes(threshold):F1} MB），输出详细拆解");
		LogResourceBreakdown(collections, stage, mode);
	}

	/// <summary>
	/// 按资源类型（ClassIDType）统计资产集合中已反序列化对象的占用并输出日志。
	/// <c>live</c> 与默认口径都只统计已驻留内存的对象，不触发额外反序列化；
	/// <c>clrmd</c> 口径统计整个进程 GC 堆，其累加值可与 <see cref="Logger.LogMemoryDiagnostics"/> 打印的托管总量对齐，
	/// 未解释的差额就是资产对象之外（框架/引擎/生成类之外）的开销。
	/// </summary>
	/// <param name="collections">要枚举的资产集合；默认与 <c>live</c> 口径必需，<c>clrmd</c> 口径可传 null。</param>
	/// <param name="stage">当前阶段标识，用于日志区分。</param>
	/// <param name="mode">拆解口径：<c>live</c> 反射估算托管堆、<c>clrmd</c> 抓取整堆快照，空串/其它值走默认口径（序列化字节数）。</param>
	public static void LogResourceBreakdown(IEnumerable<AssetCollection>? collections, string stage, string? mode = null)
	{
		try
		{
			if (string.Equals(mode, "clrmd", StringComparison.OrdinalIgnoreCase))
			{
				// 整堆视图与资产集合无关（统计的是进程 GC 堆），因此走独立分支
				LogHeapSnapshotBreakdown(stage);
				return;
			}

			if (collections is null)
			{
				Logger.Info(LogCategory.Processing,
					$"[内存诊断][资产类型] {stage}: 当前口径（{DescribeMode(mode)}）需要资产集合，但调用方未提供，已跳过");
				return;
			}

			LogCollectionBreakdown(collections, stage, string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase));
		}
		catch (Exception ex)
		{
			// 拆解可能被自动触发在导出流程中途，因此诊断自身失败一律降级为日志，绝不打断管线
			Logger.Warning(LogCategory.Processing, $"[内存诊断][资产类型] {stage}: 拆解失败，已跳过（{ex.GetType().Name}: {ex.Message}）");
		}
		finally
		{
			// 无论本次是否真的输出了内容，都把基线前移：刚做过拆解就不该马上因为同一段增长再报一次
			MoveBreakdownBaseline();
		}
	}

	/// <summary>
	/// 默认口径（序列化字节数）与 <c>live</c> 口径（反射估算托管堆）的共用实现。
	/// </summary>
	/// <param name="collections">要枚举的资产集合。</param>
	/// <param name="stage">当前阶段标识。</param>
	/// <param name="live">true 表示用 <see cref="ManagedSizeCalculator"/> 估算真实托管堆字节数。</param>
	private static void LogCollectionBreakdown(IEnumerable<AssetCollection> collections, string stage, bool live)
	{
		// 类型名 -> (对象数量, 字节数)
		Dictionary<string, TypeStat> byType = new();
		long totalCount = 0;
		long totalSize = 0;

		// 序列化文件路径 -> (文件数量, 字节数)，用于统计 SerializedFile 的托管堆占用
		Dictionary<string, TypeStat> byFile = new();
		long totalFileCount = 0;
		long totalFileSize = 0;

		// live 模式下，跨所有集合共享 visited，保证被多个资产共享的引用对象只计一次（符合堆中仅一份的实情）
		HashSet<object>? liveVisited = live
			? new HashSet<object>(ReferenceEqualityComparer.Instance)
			: null;

		foreach (AssetCollection collection in collections)
		{
			if (live)
			{
				// 真实托管堆估算：遍历 Assets 中已反序列化的每个对象，反射测算其对象图占用
				foreach (IUnityObjectBase asset in collection.Assets.Values)
				{
					string name = GetClassIDName(asset.ClassID);
					long assetSize = ManagedSizeCalculator.ComputeSize(asset, liveVisited!);
					if (assetSize > 1024 * 1024 * 10)
					{
						Logger.Info(LogCategory.Processing, $"[内存诊断][资产名]   {asset.GetBestName(),-22} +{assetSize / 1024.0 / 1024.0,8:F1} MB");
					}

					byType.TryGetValue(name, out TypeStat stat);
					stat.Count++;
					stat.Size += assetSize;
					byType[name] = stat;
					totalCount++;
					totalSize += assetSize;
				}

				// 同时统计该集合底层 SerializedFile 的托管堆占用（解析元数据/ObjectInfo 数组/类型树/字符串名表等），
				// 这部分是 assets 之外的主要托管内存来源。
				if (collection is SerializedAssetCollection sacDiag && sacDiag.SerializedFileForDiagnostics is { } sourceFile)
				{
					string filePath = sourceFile.FilePath;
					long fileSize = ManagedSizeCalculator.ComputeSize(sourceFile, liveVisited!);
					byFile.TryGetValue(filePath, out TypeStat fstat);
					fstat.Count++;
					fstat.Size += fileSize;
					byFile[filePath] = fstat;
					totalFileCount++;
					totalFileSize += fileSize;
				}
			}
			else if (collection is SerializedAssetCollection sac)
			{
				// 快速模式：仅统计已反序列化、驻留内存的对象，直接读其 ObjectInfo 的序列化字节长度
				foreach ((int classID, int size) in sac.EnumerateDeserializedObjectSizes())
				{
					string name = GetClassIDName(classID);
					byType.TryGetValue(name, out TypeStat stat);
					stat.Count++;
					stat.Size += size;
					byType[name] = stat;
					totalCount++;
					totalSize += size;
				}
			}
			else
			{
				// 非序列化集合（运行时生成/虚拟）：Assets 中的对象本就是已加载的，直接遍历计数；体积无序列化来源记为 0
				foreach (IUnityObjectBase asset in collection.Assets.Values)
				{
					string name = GetClassIDName(asset.ClassID);
					byType.TryGetValue(name, out TypeStat stat);
					stat.Count++;
					byType[name] = stat;
					totalCount++;
				}
			}
		}

		string metricLabel = live ? "托管堆(反射估算)" : "序列化数据";
		string prefix = "[内存诊断][资产类型]";
		Logger.Info(LogCategory.Processing,
			$"{prefix} {stage}: 共 {totalCount} 个对象 | {metricLabel}总计 {ToMegabytes(totalSize):F1} MB");

		// 按体积降序，仅输出占比最高的前若干类型，避免日志过长
		List<TypeAggregate> aggregates = new(byType.Count);
		int rank = 0;
		foreach (var kvp in byType.OrderByDescending(k => k.Value.Size))
		{
			aggregates.Add(new TypeAggregate(kvp.Key, kvp.Value.Count, kvp.Value.Size));
			if (++rank > TypeTopN)
			{
				continue;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {kvp.Key,-22} 数量 {kvp.Value.Count,8} | {metricLabel} {ToMegabytes(kvp.Value.Size),8:F1} MB");
		}

		if (byType.Count > TypeTopN)
		{
			Logger.Info(LogCategory.Processing, $"{prefix}   ... 其余 {byType.Count - TypeTopN} 种类型未列出");
		}

		LogRankingByAverage(prefix, stage, aggregates, metricLabel);

		// 序列化文件（SerializedFile）托管堆占用：仅在 live 模式可估算
		if (live && byFile.Count > 0)
		{
			Logger.Info(LogCategory.Processing,
				$"[内存诊断][序列化文件] {stage}: 共 {totalFileCount} 个文件 | {metricLabel}总计 {ToMegabytes(totalFileSize):F1} MB");
			int fRank = 0;
			foreach (var kvp in byFile.OrderByDescending(k => k.Value.Size))
			{
				if (++fRank > TypeTopN)
				{
					break;
				}
				Logger.Info(LogCategory.Processing,
					$"[内存诊断][序列化文件]   {fRank,2}. {kvp.Key,-50} {metricLabel} {ToMegabytes(kvp.Value.Size),8:F1} MB");
			}
			if (byFile.Count > TypeTopN)
			{
				Logger.Info(LogCategory.Processing,
					$"[内存诊断][序列化文件]   ... 其余 {byFile.Count - TypeTopN} 个文件未列出");
			}
			// 与资产占用合并，便于和 GC 报告的托管堆总量对比，看还剩多少未解释
			long grandTotal = totalSize + totalFileSize;
			Logger.Info(LogCategory.Processing,
				$"[内存诊断][合计] {stage}: 资产 {ToMegabytes(totalSize):F1} MB + 序列化文件 {ToMegabytes(totalFileSize):F1} MB = {ToMegabytes(grandTotal):F1} MB");
		}
	}

	/// <summary>
	/// 用 ClrMD 抓取整堆快照并按托管类型聚合，输出进程中"真实存在"的托管内存分布。
	/// 相关环境变量：
	/// <list type="bullet">
	///   <item><description><c>RURI_MEM_BREAKDOWN_DUMP</c>：改为分析指定转储文件（<c>dotnet-dump collect</c> 的产物），便于事后分析或分析已退出的进程；</description></item>
	///   <item><description><c>RURI_MEM_BREAKDOWN_MAX_OBJECTS</c>：遍历对象数上限，用于超大堆的抽样，默认不限。</description></item>
	/// </list>
	/// </summary>
	/// <param name="stage">当前阶段标识，用于日志区分。</param>
	private static void LogHeapSnapshotBreakdown(string stage)
	{
		Logger.Info(LogCategory.Processing, "======================================================== [内存诊断][整堆] ========================================================");
		string? dumpPath = Environment.GetEnvironmentVariable("RURI_MEM_BREAKDOWN_DUMP");
		int maxObjects = ParseObjectLimit(Environment.GetEnvironmentVariable("RURI_MEM_BREAKDOWN_MAX_OBJECTS"));

		if (!ClrMdHeapAnalyzer.TryAnalyze(dumpPath, maxObjects, out HeapSnapshotResult? snapshot, out string? error))
		{
			// 诊断失败不能中断导出，如实记录原因后按"无此数据"处理
			Logger.Warning(LogCategory.Processing, $"[内存诊断][整堆] {stage}: 整堆快照失败，已跳过（{error}）");
			return;
		}

		const string Prefix = "[内存诊断][整堆]";
		Logger.Info(LogCategory.Processing,
			$"{Prefix} {stage}: 数据来源 {snapshot.Source} | 堆段 {snapshot.SegmentCount} 个 | 服务器GC {(snapshot.IsServerGC ? "是" : "否")} | 遍历耗时 {snapshot.Elapsed.TotalSeconds:F1}s");
		Logger.Info(LogCategory.Processing,
			$"{Prefix} {stage}: 共 {snapshot.TotalCount} 个对象 | 堆占用 {ToMegabytes(snapshot.TotalSize):F1} MB{(snapshot.Truncated ? "（已达到对象数上限，以下为抽样结果）" : string.Empty)}");

		Logger.Info(LogCategory.Processing, "======================================================== 内存占用 ==================================================================");
		LogRankingBySize(Prefix, stage, snapshot.Types, snapshot.TotalSize);
		Logger.Info(LogCategory.Processing, "======================================================== 数量 ==================================================================");
		LogRankingByCount(Prefix, stage, snapshot.Types, snapshot.TotalCount);
		Logger.Info(LogCategory.Processing, "======================================================== 平均占用 ==================================================================");
		LogRankingByAverage(Prefix, stage, snapshot.Types, "堆占用");
		Logger.Info(LogCategory.Processing, "======================================================== Unity类型 ==================================================================");
		LogUnityTypeView(Prefix, stage, snapshot.Types);
		Logger.Info(LogCategory.Processing, "======================================================== 命名空间分组 ==================================================================");
		LogNamespaceGroups(Prefix, stage, snapshot.Types, snapshot.TotalSize);
		Logger.Info(LogCategory.Processing, "======================================================== END ==================================================================");
	}

	/// <summary>按占用字节降序输出类型排名，占比相对总量。</summary>
	private static void LogRankingBySize(string prefix, string stage, IReadOnlyList<TypeAggregate> types, long totalSize)
	{
		int shown = Math.Min(TypeTopN, types.Count);
		Logger.Info(LogCategory.Processing, $"{prefix} {stage}: 占用最高的 {shown} 种类型（按字节）");
		int rank = 0;
		foreach (TypeAggregate type in types)
		{
			if (++rank > TypeTopN)
			{
				break;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {type.TypeName,-70} 数量 {type.Count,10} | 占用 {ToMegabytes(type.Size),8:F1} MB (平均 {FormatAverage(type.Size, type.Count),10}) | {GetPercent(type.Size, totalSize),5:F1}%");
		}

		if (types.Count > TypeTopN)
		{
			Logger.Info(LogCategory.Processing, $"{prefix}   ... 其余 {types.Count - TypeTopN} 种类型未列出");
		}
	}

	/// <summary>
	/// 按对象数量降序输出类型排名。数量榜与总量榜回答的问题不同：
	/// 前者暴露大量小对象带来的装箱/句柄开销，后者暴露大块缓冲。
	/// </summary>
	private static void LogRankingByCount(string prefix, string stage, IReadOnlyList<TypeAggregate> types, long totalCount)
	{
		int shown = Math.Min(CountTopN, types.Count);
		Logger.Info(LogCategory.Processing, $"{prefix} {stage}: 数量最多的 {shown} 种类型（按个数）");
		int rank = 0;
		foreach (TypeAggregate type in types.OrderByDescending(t => t.Count))
		{
			if (++rank > CountTopN)
			{
				break;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {type.TypeName,-70} 数量 {type.Count,10} | 占用 {ToMegabytes(type.Size),8:F1} MB | {GetPercent(type.Count, totalCount),5:F1}%");
		}
	}

	/// <summary>
	/// 按"平均每个对象的占用"降序输出类型排名。
	/// 总量榜回答"谁占得最多"，平均榜回答"单个对象有多重"（例如 Texture2D 平均 2 MB 与 Material 平均 4 KB 的差别），
	/// 定位"单实例过重"的类型时更直接。为保证平均值有意义，只统计对象数 ≥ <see cref="AverageMinCount"/> 的类型。
	/// </summary>
	/// <param name="prefix">日志前缀（区分资产口径与整堆口径）。</param>
	/// <param name="stage"></param>
	/// <param name="types">类型聚合（数量与总量）。</param>
	/// <param name="metricLabel">口径标签，仅用于日志。</param>
	private static void LogRankingByAverage(string prefix, string stage, IReadOnlyList<TypeAggregate> types, string metricLabel)
	{
		TypeAggregate[] ranked = types
			.Where(t => t.Count >= AverageMinCount)
			.OrderByDescending(t => t.Size / (double)t.Count)
			.ToArray();

		if (ranked.Length == 0)
		{
			return;
		}

		int shown = Math.Min(AverageTopN, ranked.Length);
		Logger.Info(LogCategory.Processing,
			$"{prefix} {stage}: 平均占用最高的 {shown} 种类型（仅统计数量 ≥ {AverageMinCount} 的类型）");
		int rank = 0;
		foreach (TypeAggregate type in ranked)
		{
			if (++rank > AverageTopN)
			{
				break;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {type.TypeName,-40} 数量 {type.Count,10} | 平均 {FormatAverage(type.Size, type.Count),10} | {metricLabel}合计 {ToMegabytes(type.Size),8:F1} MB");
		}

		if (ranked.Length > AverageTopN)
		{
			Logger.Info(LogCategory.Processing, $"{prefix}   ... 其余 {ranked.Length - AverageTopN} 种类型未列出");
		}
	}

	/// <summary>
	/// 把托管类型名归并回 Unity 的 ClassID 名称，得到"整堆中的 Unity 资产占用"视图，
	/// 便于与默认／<c>live</c> 口径的资产类型榜对照：两者的差额是生成类之外的框架/引擎侧开销。
	/// </summary>
	private static void LogUnityTypeView(string prefix, string stage, IReadOnlyList<TypeAggregate> types)
	{
		Dictionary<string, TypeStat> byClassID = new();
		long totalCount = 0;
		long totalSize = 0;
		foreach (TypeAggregate type in types)
		{
			if (!TryMapToClassIdName(type.TypeName, out string? className))
			{
				continue;
			}

			byClassID.TryGetValue(className, out TypeStat stat);
			stat.Count += type.Count;
			stat.Size += type.Size;
			byClassID[className] = stat;
			totalCount += type.Count;
			totalSize += type.Size;
		}

		if (byClassID.Count == 0)
		{
			return;
		}

		Logger.Info(LogCategory.Processing,
			$"{prefix} {stage}: 其中 Unity 资产类共 {totalCount} 个对象 | 占用 {ToMegabytes(totalSize):F1} MB");
		int rank = 0;
		foreach (var kvp in byClassID.OrderByDescending(k => k.Value.Size))
		{
			if (++rank > GroupTopN)
			{
				break;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {kvp.Key,-22} 数量 {kvp.Value.Count,8} | 占用 {ToMegabytes(kvp.Value.Size),8:F1} MB");
		}

		if (byClassID.Count > GroupTopN)
		{
			Logger.Info(LogCategory.Processing, $"{prefix}   ... 其余 {byClassID.Count - GroupTopN} 种资产类型未列出");
		}
	}

	/// <summary>
	/// 按命名空间首段聚合，一眼看出内存被框架（System）、项目自身（AssetRipper）还是第三方库占住。
	/// </summary>
	private static void LogNamespaceGroups(string prefix, string stage, IReadOnlyList<TypeAggregate> types, long totalSize)
	{
		Dictionary<string, TypeStat> byGroup = new();
		foreach (TypeAggregate type in types)
		{
			string group = GetTopNamespace(type.TypeName);
			byGroup.TryGetValue(group, out TypeStat stat);
			stat.Count += type.Count;
			stat.Size += type.Size;
			byGroup[group] = stat;
		}

		Logger.Info(LogCategory.Processing, $"{prefix} {stage}: 按命名空间首段分组");
		int rank = 0;
		foreach (var kvp in byGroup.OrderByDescending(k => k.Value.Size))
		{
			if (++rank > GroupTopN)
			{
				break;
			}

			Logger.Info(LogCategory.Processing,
				$"{prefix}   {rank,2}. {kvp.Key,-22} 数量 {kvp.Value.Count,10} | 占用 {ToMegabytes(kvp.Value.Size),8:F1} MB | {GetPercent(kvp.Value.Size, totalSize),5:F1}%");
		}
	}

	/// <summary>
	/// 取出类型名的命名空间首段；数组、Free 空洞以及编译器生成的名字（匿名类型/闭包类，以 '&lt;' 开头）
	/// 都没有可用的命名空间，统一归入"其它"。
	/// </summary>
	private static string GetTopNamespace(string typeName)
	{
		if (typeName.Length == 0 || typeName[0] == '<')
		{
			return "其它";
		}

		int separatorIndex = typeName.IndexOf('.');
		return separatorIndex > 0 ? typeName[..separatorIndex] : "其它";
	}

	/// <summary>
	/// 尝试把 ClrMD 给出的托管类型名映射回 Unity 的 ClassID 名称。
	/// 生成类的全名形如 <c>AssetRipper.SourceGenerated.Classes.ClassID_128.Texture2D</c>，
	/// 其中 <c>ClassID_</c> 后面的数字就是 Unity 的 ClassID，据此复用 <see cref="GetClassIDName"/>
	/// 即可得到与其它统计口径一致的可读名，无需反射生成程序集。
	/// </summary>
	/// <param name="clrTypeName">ClrMD 报告的托管类型全名。</param>
	/// <param name="className">映射成功时的 Unity 类型名。</param>
	private static bool TryMapToClassIdName(string clrTypeName, [NotNullWhen(true)] out string? className)
	{
		className = null;

		const string marker = ".Classes.ClassID_";
		int markerIndex = clrTypeName.IndexOf(marker, StringComparison.Ordinal);
		if (markerIndex < 0)
		{
			return false;
		}

		int digitsStart = markerIndex + marker.Length;
		int digitsEnd = digitsStart;
		while (digitsEnd < clrTypeName.Length && char.IsAsciiDigit(clrTypeName[digitsEnd]))
		{
			digitsEnd++;
		}

		// 数字后必须紧跟类名分隔符或字符串结束，否则可能是 ClassID_1280 这类更长编号被截断匹配
		if (digitsEnd == digitsStart || (digitsEnd < clrTypeName.Length && clrTypeName[digitsEnd] != '.'))
		{
			return false;
		}

		if (!int.TryParse(clrTypeName.AsSpan(digitsStart, digitsEnd - digitsStart), out int classID))
		{
			return false;
		}

		className = GetClassIDName(classID);
		return true;
	}

	/// <summary>
	/// 把 ClassID 转为可读的类型名。已知 Unity 内置类型走 <see cref="ClassIDType"/> 枚举，
	/// 不在枚举中的（通常是自定义/第三方类型）回退为 "Class{id}" 形式。
	/// </summary>
	private static string GetClassIDName(int classID)
	{
		if (Enum.IsDefined(typeof(ClassIDType), classID))
		{
			return ((ClassIDType)classID).ToString();
		}

		return $"Class{classID}";
	}

	/// <summary>
	/// 自动拆解的触发阈值（字节）。由 <c>RURI_MEM_BREAKDOWN_GROWTH_MB</c> 覆盖，
	/// 显式设为非正数即视为关闭自动拆解。
	/// </summary>
	private static long GetBreakdownGrowthThresholdBytes()
	{
		string? raw = Environment.GetEnvironmentVariable("RURI_MEM_BREAKDOWN_GROWTH_MB");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return DefaultBreakdownGrowthMegabytes * 1024L * 1024L;
		}

		return long.TryParse(raw, out long megabytes) && megabytes > 0 ? megabytes * 1024L * 1024L : 0;
	}

	/// <summary>把"上次拆解"的基线前移到当前托管堆占用。</summary>
	private static void MoveBreakdownBaseline()
	{
		lastBreakdownManagedBytes = GC.GetTotalMemory(forceFullCollection: false);
		hasBreakdownBaseline = true;
	}

	/// <summary>把口径的内部值翻译成日志里的可读说法；null 视为默认口径。</summary>
	private static string DescribeMode(string? mode)
	{
		if (string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase))
		{
			return "live(托管堆估算)";
		}

		return string.IsNullOrEmpty(mode) ? "默认(序列化数据)" : mode;
	}

	/// <summary>解析对象数上限环境变量，缺失或非法时按"不限制"处理。</summary>
	private static int ParseObjectLimit(string? value)
	{
		return int.TryParse(value, out int limit) && limit > 0 ? limit : 0;
	}

	/// <summary>字节转 MB，仅用于日志展示。</summary>
	private static double ToMegabytes(long bytes) => bytes / 1024.0 / 1024.0;

	/// <summary>
	/// 把"平均每个对象的字节数"格式化成可读文本：小值用 B/KB，
	/// 避免一律按 MB 输出时小对象类型满屏 0.0。
	/// </summary>
	private static string FormatAverage(long totalSize, long count)
	{
		long average = count > 0 ? totalSize / count : 0;
		if (average < 1024)
		{
			return $"{average} B";
		}
		if (average < 1024 * 1024)
		{
			return $"{average / 1024.0:F1} KB";
		}
		return $"{ToMegabytes(average):F1} MB";
	}

	/// <summary>占比计算，分母为 0 时返回 0，避免统计口径为空时的除零。</summary>
	private static double GetPercent(long part, long total) => total > 0 ? part * 100.0 / total : 0;

	/// <summary>聚合累加器：对象数量与字节数。</summary>
	private struct TypeStat
	{
		public long Count;
		public long Size;
	}
}
