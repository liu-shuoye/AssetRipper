using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Cloning;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated;
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
	private IExportCollection CreateCollection(IUnityObjectBase asset)
	{
		foreach (IAssetExporter exporter in assetExporterStack.GetHandlerStack(asset.GetType()))
		{
			if (exporter.TryCreateCollection(asset, out IExportCollection? collection))
			{
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

		EventExportPreparationStarted?.Invoke();
		List<IExportCollection> collections = CreateCollections(fileCollection, enableDeduplication,
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
	/// <param name="skippedCollections"> 跳过集合 </param>
	/// <param name="redirectMap"> 重定向映射 </param>
	/// <returns></returns>
	private List<IExportCollection> CreateCollections(GameBundle fileCollection, bool enableDeduplication,
		out HashSet<IExportCollection> skippedCollections, out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		List<IExportCollection> collections = new();
		HashSet<IUnityObjectBase> queued = new();

		foreach (IUnityObjectBase asset in fileCollection.FetchAssets())
		{
			if (!queued.Contains(asset))
			{
				IExportCollection collection = CreateCollection(asset);
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
	///  按主要资产（类型、内容哈希）对集合进行分组，并将具有相同哈希值的资产视为重复项。
	/// </summary>
	/// <remarks>
	/// 内容哈希由 <see cref="ContentHashWalker"/> 计算，该方法在遍历资源的序列化字段时不会解引用 PPtr 目标。
	/// 这避免了 <see cref="AssetRipper.Assets.Cloning.AssetEqualityComparer"/> 所执行的无限制引用图遍历
	/// （以及由此引发的 <see cref="OutOfMemoryException"/>）。
	/// 通过将 64 位 XxHash 与桶键中的类型结合使用，假阳性几乎可以忽略不计。
	/// </remarks>
	private void ApplyDeduplication(List<IExportCollection> collections,
		out HashSet<IExportCollection> skippedCollections, out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		skippedCollections = new();
		redirectMap = new();

		// 按（类型、内容哈希）对每个集合和存储桶缓存主要资源。
		// 内容哈希为 ContentHashWalker.Unhashable 的资源将被完全保留。
		Dictionary<(Type, ulong), IExportCollection> keptByHash = new();
		Dictionary<IUnityObjectBase, ulong> hashCache = new();
		HashSet<IUnityObjectBase> visiting = new();
		Dictionary<Type, int> skippedByType = new();
		int comparedCount = 0;
		int skippedCount = 0;

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

			IUnityObjectBase? primaryAsset = collection.Assets.FirstOrDefault();
			if (primaryAsset is null)
			{
				continue;
			}

			comparedCount++;

			ulong hash = ContentHashWalker.ComputeHash(primaryAsset, hashCache, visiting);
			if (hash == ContentHashWalker.Unhashable)
			{
				// Asset cannot be hashed (e.g. unloaded MonoBehaviour script data). Keep it.
				continue;
			}

			(Type, ulong) key = (primaryAsset.GetType(), hash);
			if (keptByHash.TryGetValue(key, out IExportCollection? keptCollection))
			{
				// 决定保留哪个：优先保留目录名与文件主名匹配的资源，得分相同时回退到字典序更小者。
				IExportCollection keep = IsCollectionPreferred(collection, keptCollection)
					? collection
					: keptCollection;
				IExportCollection skip = object.ReferenceEquals(keep, collection) ? keptCollection : collection;
				if (!object.ReferenceEquals(keep, keptCollection))
				{
					keptByHash[key] = keep;
				}

				skippedCollections.Add(skip);
				redirectMap[skip.Assets.FirstOrDefault()!] = keep.Assets.FirstOrDefault()!;
				Type t = primaryAsset.GetType();
				skippedByType[t] = skippedByType.TryGetValue(t, out int v) ? v + 1 : 1;
				skippedCount++;
			}
			else
			{
				keptByHash[key] = collection;
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
