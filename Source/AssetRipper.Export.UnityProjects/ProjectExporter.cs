using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Cloning;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using System.Text;

namespace AssetRipper.Export.UnityProjects;

public sealed partial class ProjectExporter
{
	public event Action? EventExportPreparationStarted;
	public event Action? EventExportPreparationFinished;
	public event Action? EventExportStarted;
	public event Action<int, int>? EventExportProgressUpdated;
	public event Action? EventExportFinished;

	/// <summary> 资产导出器的堆栈。 </summary>
	private readonly ObjectHandlerStack<IAssetExporter> assetExporterStack = new();

	/// <summary>向该资产类型的导出器堆栈中添加一个导出器。</summary>
	/// <typeparam name="T">The c sharp type of this asset type. Any inherited types also get this exporter.</typeparam>
	/// <param name="exporter">The new exporter. If it doesn't work, the next one in the stack is used.</param>
	/// <param name="allowInheritance">Should types that inherit from this type also use the exporter?</param>
	public void OverrideExporter<T>(IAssetExporter exporter, bool allowInheritance = true)
	{
		assetExporterStack.OverrideHandler(typeof(T), exporter, allowInheritance);
	}

	/// <summary>Adds an exporter to the stack of exporters for this asset type.</summary>
	/// <param name="type">The c sharp type of this asset type. Any inherited types also get this exporter.</param>
	/// <param name="exporter">The new exporter. If it doesn't work, the next one in the stack is used.</param>
	/// <param name="allowInheritance">Should types that inherit from this type also use the exporter?</param>
	public void OverrideExporter(Type type, IAssetExporter exporter, bool allowInheritance)
	{
		assetExporterStack.OverrideHandler(type, exporter, allowInheritance);
	}

	/// <summary>
	/// Use the <see cref="DummyExporter"/> for the specified class type.
	/// </summary>
	/// <typeparam name="T">The base type for assets of that <paramref name="classType"/>.</typeparam>
	/// <param name="isEmptyCollection">
	/// True: an exception will be thrown if the asset is referenced by another asset.<br/>
	/// False: any references to this asset will be replaced with a missing reference.
	/// </param>
	/// <param name="isMetaType"><see cref="AssetType.Meta"/> or <see cref="AssetType.Serialized"/>?</param>
	private void OverrideDummyExporter<T>(bool isEmptyCollection, bool isMetaType)
	{
		OverrideExporter<T>(DummyAssetExporter.Get(isEmptyCollection, isMetaType), true);
	}

	public AssetType ToExportType(Type type)
	{
		foreach (IAssetExporter exporter in assetExporterStack.GetHandlerStack(type))
		{
			if (exporter.ToUnknownExportType(type, out AssetType assetType))
			{
				return assetType;
			}
		}

		throw new NotSupportedException($"There is no exporter that know {nameof(AssetType)} for unknown asset '{type}'");
	}

	/// <summary> 创建一个导出集合。 </summary>
	/// <param name="asset">主资源。</param>
	/// <param name="useDeterministicGuid">是否用确定性 GUID 替换集合的随机 GUID。</param>
	private IExportCollection CreateCollection(IUnityObjectBase asset, bool useDeterministicGuid)
	{
		foreach (IAssetExporter exporter in assetExporterStack.GetHandlerStack(asset.GetType()))
		{
			if (exporter.TryCreateCollection(asset, out IExportCollection? collection))
			{
				// 开启开关时，把普通资产集合的随机 GUID 替换为按稳定标识计算的值；
				// 场景/脚本/用户资产等集合的 UseDeterministicGuid 为空操作，GUID 保持不变。
				if (useDeterministicGuid && collection is ExportCollection exportCollection)
				{
					exportCollection.UseDeterministicGuid();
				}

				return collection;
			}
		}

		throw new Exception($"There is no exporter that can handle '{asset}'");
	}

	public void Export(GameBundle fileCollection, CoreConfiguration options, FileSystem fileSystem)
	{
		// ProcessingSettings 由 FullConfiguration 存储在 SingletonData 中。
		// 由于本项目未引用 AssetRipper.Export，因此需通过 CoreConfiguration 直接访问。
		bool enableDeduplication = options.SingletonData.TryGetStoredValue<ProcessingSettings>(
			                           nameof(ProcessingSettings), out ProcessingSettings? ps)
		                           && ps.EnableAssetDeduplication;

		// 与去重开关同理：从 SingletonData 读取"确定性 GUID"开关（默认关闭，保持随机 GUID 的旧行为）。
		bool enableDeterministicGuids = ps is not null && ps.EnableDeterministicGuids;
		if (enableDeterministicGuids)
		{
			Logger.Info(LogCategory.Export, "已启用确定性 GUID：导出时基于资产稳定标识计算 GUID，跨批次导出保持稳定。");
		}

		EventExportPreparationStarted?.Invoke();
		List<IExportCollection> collections = CreateCollections(fileCollection, enableDeduplication,
			enableDeterministicGuids,
			out HashSet<IExportCollection> skippedCollections,
			out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap);
		EventExportPreparationFinished?.Invoke();

		EventExportStarted?.Invoke();
		ProjectAssetContainer container = new ProjectAssetContainer(this, options, fileCollection.FetchAssets(),
			collections, skippedCollections, redirectMap);
		int exportableCount = collections.Count(c => c.Exportable && !skippedCollections.Contains(c));
		int currentExportable = 0;

		for (int i = 0; i < collections.Count; i++)
		{
			IExportCollection collection = collections[i];
			container.CurrentCollection = collection;
			if (collection.Exportable && !skippedCollections.Contains(collection))
			{
				currentExportable++;
				Logger.Info(LogCategory.ExportProgress, $"({currentExportable}/{exportableCount}) 正在导出 '{collection.Name}'");
				bool exportedSuccessfully = collection.Export(container, options.ProjectRootPath, fileSystem);
				if (!exportedSuccessfully)
				{
					Logger.Warning(LogCategory.ExportProgress, $"无法导出 '{collection.Name}' ({collection.GetType().Name})");
				}
			}

			EventExportProgressUpdated?.Invoke(i, collections.Count);
		}

		EventExportFinished?.Invoke();
	}

	/// <summary>
	/// 为给定的文件集合创建导出集合列表。
	/// </summary>
	/// <param name="fileCollection">文件集合</param>
	/// <param name="enableDeduplication"> 是否启用去重 </param>
	/// <param name="enableDeterministicGuids"> 是否为每个集合应用确定性 GUID </param>
	/// <param name="skippedCollections"> 跳过集合 </param>
	/// <param name="redirectMap"> 重定向映射 </param>
	/// <returns></returns>
	private List<IExportCollection> CreateCollections(GameBundle fileCollection, bool enableDeduplication,
		bool enableDeterministicGuids,
		out HashSet<IExportCollection> skippedCollections, out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		List<IExportCollection> collections = new();
		HashSet<IUnityObjectBase> queued = new();

		foreach (IUnityObjectBase asset in fileCollection.FetchAssets())
		{
			if (!queued.Contains(asset))
			{
				IExportCollection collection = CreateCollection(asset, enableDeterministicGuids);
				foreach (IUnityObjectBase element in collection.Assets)
				{
					queued.Add(element);
				}

				collections.Add(collection);
			}
		}

		if (enableDeduplication)
		{
			ApplyDeduplication(collections, out skippedCollections, out redirectMap);
		}
		else
		{
			skippedCollections = new();
			redirectMap = new();
		}

		return collections;
	}

	/// <summary>
	///  按（类型、内容哈希）对集合进行分组，并将具有相同哈希值的资产视为重复项。
	/// </summary>
	/// <remarks>
	/// 内容哈希由 <see cref="ContentHashWalker"/> 计算，该方法在遍历资源的序列化字段时不会解引用 PPtr 目标。
	/// 这避免了 <see cref="AssetRipper.Assets.Cloning.AssetEqualityComparer"/> 所执行的无限制引用图遍历
	/// （以及由此引发的 <see cref="OutOfMemoryException"/>）。
	/// 通过将 64 位 XxHash 与桶键中的类型结合使用，假阳性几乎可以忽略不计。
	///
	/// 仅当两个集合的"完整集合内容指纹"完全一致时才判为重复，即集合内每个资源（含主资源与子资源）
	/// 的 (ClassID, 内容哈希) 多重集合都相同。这样不会因主资源内容相同但子资源不同而误删集合。
	/// 被跳过集合的每个资源都会一对一重定向到 keeper 中指纹相同的对应资源，从而让场景/对象对任意
	/// 被跳过资源的引用 (guid, fileID) 都指向 keeper 导出的真实资源，避免引用丢失。
	/// </remarks>
	private void ApplyDeduplication(List<IExportCollection> collections,
		out HashSet<IExportCollection> skippedCollections, out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		skippedCollections = new();
		redirectMap = new();

		// 为每个集合算一次"完整集合内容指纹"，并对每个资源缓存哈希值，供分组与配对重用。
		Dictionary<IUnityObjectBase, ulong> hashCache = new();
		HashSet<IUnityObjectBase> visiting = new();
		Dictionary<IExportCollection, List<(int, ulong)>> fingerprintByCollection = new();
		// Shader 特殊处理：按名称去重，而不是按序列化内容。见 AddShaderGroupKey 说明。
		Dictionary<IExportCollection, string> shaderKeyByCollection = new();

		int comparedCount = 0;
		foreach (IExportCollection collection in collections)
		{
			if (collection is SceneExportCollection)
			{
				continue;
			}

			if (!collection.Exportable)
			{
				continue;
			}

			List<IUnityObjectBase> assets = collection.Assets.ToList();
			if (assets.Count == 0)
			{
				continue;
			}

			// Shader：以名称作为去重依据。同一条资源集中 shader 名称一般唯一，
			// 而编译二进制/ParsedForm 内细微差异会导致序列化内容哈希不同，无法识别重复，
			// 因此这里不走内容指纹，改为直接按 shader 名称分组。
			if (assets[0] is IShader)
			{
				string? shaderKey = GetShaderKey(assets);
				if (shaderKey is null)
				{
					// 取不到稳定名称（如空名），保守保留，不去重。
					continue;
				}

				comparedCount++;
				shaderKeyByCollection[collection] = shaderKey;
				continue;
			}

			if (!TryComputeCollectionFingerprint(assets, hashCache, visiting, out List<(int, ulong)> fingerprint))
			{
				// 集合内存在无法哈希的资源（如未加载的 MonoBehaviour 脚本数据），保守保留，不去重。
				continue;
			}

			comparedCount++;
			fingerprintByCollection[collection] = fingerprint;
		}

		// 两遍式：先按指纹分组，再在组内决出 keeper，避免 keeper 变更后旧重定向指向新跳过集合的悬空问题。
		Dictionary<string, List<IExportCollection>> groups = new();
		foreach (KeyValuePair<IExportCollection, List<(int, ulong)>> pair in fingerprintByCollection)
		{
			string groupKey = BuildFingerprintKey(pair.Value);
			if (!groups.TryGetValue(groupKey, out List<IExportCollection>? group))
			{
				group = new();
				groups[groupKey] = group;
			}

			group.Add(pair.Key);
		}

		// Shader 分组：以"Shader|<名称>"作为分组键，与内容指纹键（形如"1:ABC;2:DEF;"）互不冲突。
		foreach (KeyValuePair<IExportCollection, string> pair in shaderKeyByCollection)
		{
			string groupKey = AddShaderGroupKey(pair.Value);
			if (!groups.TryGetValue(groupKey, out List<IExportCollection>? group))
			{
				group = new();
				groups[groupKey] = group;
			}

			group.Add(pair.Key);
		}

		Dictionary<Type, int> skippedByType = new();
		int skippedCount = 0;
		foreach (List<IExportCollection> group in groups.Values)
		{
			if (group.Count == 1)
			{
				continue;
			}

			IExportCollection keep = SelectKeeper(group);
			foreach (IExportCollection skip in group)
			{
				if (ReferenceEquals(skip, keep))
				{
					continue;
				}

				skippedCollections.Add(skip);
				if (shaderKeyByCollection.TryGetValue(skip, out string? _))
				{
					// Shader 按名称去重：把 skipped 集合的 shader 引用重定向到 keeper 中同名的 shader。
					RedirectShaderCollection(skip, keep, redirectMap);
				}
				else
				{
					BuildRedirectForSkipped(skip, keep, hashCache, visiting, redirectMap);
				}

				Type t = skip.Assets.First().GetType();
				skippedByType[t] = skippedByType.TryGetValue(t, out int v) ? v + 1 : 1;
				skippedCount++;
			}
		}

		int keptCount = comparedCount - skippedCount;
		Logger.Info(LogCategory.ExportProgress,
			$"资产去重：比较了 {comparedCount} 个资产，保留了 {keptCount} 个，跳过了 {skippedCount} 个。");

		if (skippedCount > 0)
		{
			StringBuilder sb = new("Deduplicated: ");
			bool first = true;
			foreach (KeyValuePair<Type, int> pair in skippedByType)
			{
				if (!first)
				{
					sb.Append(", ");
				}

				sb.Append($"{pair.Key.Name}: {pair.Value}");
				first = false;
			}

			Logger.Info(LogCategory.ExportProgress, sb.ToString());
		}
	}

	/// <summary>
	/// 取 shader 服务集合的“去重键”，即 shader 名称。
	/// </summary>
	/// <param name="assets">shader 集合内的资源（首个为主 shader）。</param>
	/// <returns>名称去除首尾空白后的非空字符串；取不到稳定名称时返回 null。</returns>
	private static string? GetShaderKey(List<IUnityObjectBase> assets)
	{
		IUnityObjectBase? primary = assets.FirstOrDefault();
		string name = primary?.GetBestName() ?? string.Empty;
		return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
	}

	/// <summary>
	/// 给 shader 名称加上唯一前缀，作为去重分组键，避免与形如“1:ABC;2:DEF;”的内容指纹键冲突。
	/// </summary>
	private static string AddShaderGroupKey(string shaderName) => "Shader|" + shaderName;

	/// <summary>
	/// 为按名称去重的 shader 集合构建重定向：把 skip 中的每个 shader 映射到 keep 中名称相同的 shader。
	/// shader 集合通常只有主资源自身，因此逐资源按名称配对即可满足所有引用解析。
	/// </summary>
	private static void RedirectShaderCollection(IExportCollection skip, IExportCollection keep,
		Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		Dictionary<string, IUnityObjectBase> keepByName = new();
		foreach (IUnityObjectBase asset in keep.Assets)
		{
			if (asset is IShader shader)
			{
				// 同一集合内理论上不会出现重名的 shader，这里取首个即可代表该名称的 keeper。
				keepByName.TryAdd(shader.GetBestName(), asset);
			}
		}

		foreach (IUnityObjectBase asset in skip.Assets)
		{
			if (asset is IShader shader
				&& keepByName.TryGetValue(shader.GetBestName(), out IUnityObjectBase? target))
			{
				redirectMap[asset] = target;
			}
		}
	}

	/// <summary>
	/// 计算集合的"完整集合内容指纹"：对集合内每个资源求 (ClassID, 内容哈希)，随后整体排序。
	/// 排序保证指纹与资源的枚举顺序无关，只要两个集合包含完全相同的内容，指纹就相同。
	/// </summary>
	/// <param name="assets">集合内的全部资源（含主资源与子资源）。</param>
	/// <param name="hashCache">跨资源复用的哈希缓存，使共享的引用目标只哈希一次。</param>
	/// <param name="visiting">遍历栈，用于检测循环引用。</param>
	/// <param name="fingerprint">输出的指纹（已排序）。</param>
	/// <returns>集合内任一资源无法哈希时返回 false，表示该集合不参与去重。</returns>
	private static bool TryComputeCollectionFingerprint(List<IUnityObjectBase> assets,
		Dictionary<IUnityObjectBase, ulong> hashCache, HashSet<IUnityObjectBase> visiting,
		out List<(int, ulong)> fingerprint)
	{
		fingerprint = new();
		foreach (IUnityObjectBase asset in assets)
		{
			ulong hash = ContentHashWalker.ComputeHash(asset, hashCache, visiting);
			if (hash == ContentHashWalker.Unhashable)
			{
				return false;
			}

			fingerprint.Add((asset.ClassID, hash));
		}

		fingerprint.Sort();
		return true;
	}

	/// <summary>
	/// 把排序后的指纹序列化成唯一 key，用于按"完整集合内容"分组。
	/// </summary>
	private static string BuildFingerprintKey(List<(int ClassID, ulong hash)> fingerprint)
	{
		StringBuilder sb = new();
		foreach ((int classID, ulong hash) in fingerprint)
		{
			sb.Append(classID).Append(':').Append(hash.ToString("X16")).Append(';');
		}

		return sb.ToString();
	}

	/// <summary>
	/// 在同一重复组内按 <see cref="IsCollectionPreferred"/> 决出唯一 keeper。
	/// 逐步把组内相对当前 keeper 更优的集合提升为 keeper，其余保持不变。
	/// </summary>
	private static IExportCollection SelectKeeper(List<IExportCollection> group)
	{
		IExportCollection keep = group[0];
		for (int i = 1; i < group.Count; i++)
		{
			if (IsCollectionPreferred(group[i], keep))
			{
				keep = group[i];
			}
		}

		return keep;
	}

	/// <summary>
	/// 为被跳过的集合构建全资源重定向：把 skip 内每个资源映射到 keeper 中 (ClassID, 内容哈希) 相同的对应资源。
	/// 这样场景/对象对 skip 内任意资源（含子资源）的引用都会解析为 keeper 的 GUID + 真实存在的 fileID。
	/// </summary>
	/// <param name="skip">被跳过的集合（不导出）。</param>
	/// <param name="keep">保留导出的集合。</param>
	/// <param name="hashCache">复用此前算过的哈希缓存，避免重复计算。</param>
	/// <param name="visiting">遍历栈，用于检测循环引用。</param>
	/// <param name="redirectMap">收集重定向结果。</param>
	private static void BuildRedirectForSkipped(IExportCollection skip, IExportCollection keep,
		Dictionary<IUnityObjectBase, ulong> hashCache, HashSet<IUnityObjectBase> visiting,
		Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		// keeper 资源按指纹分组，仅消费与当前 skip 资源指纹相同的候选，保证同一指纹多资源时一对一配对。
		// 用 Queue 保持所选集合的枚举顺序，使 skip 的（主资源优先的）顺序与 keeper 对齐。
		Dictionary<(int, ulong), Queue<IUnityObjectBase>> keepByFingerprint = new();
		foreach (IUnityObjectBase keepAsset in keep.Assets)
		{
			ulong hash = ContentHashWalker.ComputeHash(keepAsset, hashCache, visiting);
			if (hash != ContentHashWalker.Unhashable)
			{
				(int, ulong) key = (keepAsset.ClassID, hash);
				if (!keepByFingerprint.TryGetValue(key, out Queue<IUnityObjectBase>? queue))
				{
					queue = new();
					keepByFingerprint[key] = queue;
				}

				queue.Enqueue(keepAsset);
			}
		}

		foreach (IUnityObjectBase skipAsset in skip.Assets)
		{
			ulong hash = ContentHashWalker.ComputeHash(skipAsset, hashCache, visiting);
			if (hash == ContentHashWalker.Unhashable
				|| !keepByFingerprint.TryGetValue((skipAsset.ClassID, hash), out Queue<IUnityObjectBase>? queue)
				|| queue.Count == 0)
			{
				// 无法找到对应资源，防御性跳过，交由容器既有 fallback 逻辑处理。
				continue;
			}

			redirectMap[skipAsset] = queue.Dequeue();
		}
	}

	/// <summary>
	/// 在两个内容相同的导出集合之间决定保留哪个。
	/// 优先保留“目录名与文件主名相似度”更高的资源；相似度相同时回退到原始路径字典序更小者，保证结果确定性。
	/// </summary>
	/// <remarks>
	/// 例如 cg15120101/CG15120101.png 中文件所在目录段 cg15120101 与主名 CG15120101 的最长公共子串
	/// 归一化后为高分，因此它会优先于 cg16120101/CG15120101.png 被保留。
	/// </remarks>
	private static bool IsCollectionPreferred(IExportCollection candidate, IExportCollection current)
	{
		IUnityObjectBase? candidateAsset = candidate.Assets.FirstOrDefault();
		double candidateScore = NameMatchScore(candidateAsset);
		IUnityObjectBase? currentAsset = current.Assets.FirstOrDefault();
		double currentScore = NameMatchScore(currentAsset);
		// 两个得分均由同一确定性算法算出，可直接比较相等性。
		Logger.Warning(LogCategory.ExportProgress, $"比较 {candidateAsset?.OriginalPath}/{candidateAsset?.GetBestName()} 得分 {candidateScore} , {currentAsset?.OriginalPath}/{currentAsset?.GetBestName()} 得分 {currentScore}");
		if (candidateScore != currentScore)
		{
			return candidateScore > currentScore;
		}

		// 得分相同时，原始路径字典序更小者优先，保证同一批资源去重结果可复现。
		string? candidatePath = candidateAsset?.OriginalPath;
		string? currentPath = currentAsset?.OriginalPath;
		return string.Compare(candidatePath, currentPath, StringComparison.OrdinalIgnoreCase) < 0;
	}

	/// <summary>
	/// 计算资源“目录名与文件主名匹配”的相似度，取值 [0, 1]。
	/// 目录段取 <see cref="IUnityObjectBase.OriginalPath"/>（目录路径）的最后一段，
	/// 文件主名取 <see cref="IUnityObjectBase.GetBestName"/>，两者忽略大小写的最长公共连续子串长度
	/// 除以较大长度。无法解析出目录段或名称时返回 0。
	/// </summary>
	private static double NameMatchScore(IUnityObjectBase? asset)
	{
		// OriginalPath 是不含文件名的目录路径，取其最后一段作为目录段。
		string? originalPath = asset?.OriginalPath;
		string? fileName = asset?.GetBestName();
		if (originalPath is not { Length: > 0 } || fileName is not { Length: > 0 })
		{
			return 0;
		}

		// 统一分隔符以便用同一逻辑解析路径段。
		string normalized = originalPath.Replace('\\', '/');
		int slashIndex = normalized.LastIndexOf('/');
		string dirSegment = slashIndex >= 0 ? normalized[(slashIndex + 1)..] : normalized;
		int maxLength = Math.Max(dirSegment.Length, fileName.Length);
		if (maxLength == 0)
		{
			return 0;
		}

		int commonLength = LongestCommonSubstringLength(dirSegment, fileName);
		return (double)commonLength / maxLength;
	}

	/// <summary>
	/// 计算两个字符串忽略大小写的最长公共连续子串长度（二维 DP）。
	/// </summary>
	private static int LongestCommonSubstringLength(string a, string b)
	{
		int n = a.Length;
		int m = b.Length;
		// dp[i, j] 表示以 a[..i-1] 与 b[..j-1] 结尾的公共连续子串长度。
		int[,] dp = new int[n + 1, m + 1];
		int best = 0;
		for (int i = 1; i <= n; i++)
		{
			for (int j = 1; j <= m; j++)
			{
				if (char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]))
				{
					dp[i, j] = dp[i - 1, j - 1] + 1;
					if (dp[i, j] > best)
					{
						best = dp[i, j];
					}
				}
			}
		}

		return best;
	}
}
