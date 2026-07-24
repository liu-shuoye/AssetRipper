using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Cloning;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Configuration;
using AssetRipper.Processing.Editor;
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

	/// <param name="editorFormatProcessor">
	/// 可选的 EditorFormat 处理器。传入时其 <see cref="EditorFormatProcessor.ProcessForExport"/>
	/// 会作为按需转换回调注入 <see cref="ProjectAssetContainer"/>，由
	/// <see cref="ProjectYamlWalker.ExportYamlDocument"/> 在 <c>WalkEditor</c> 之前调用。
	/// 调用方需在传入前先执行 <see cref="EditorFormatProcessor.PrepareForExport"/>。
	/// 传 <c>null</c> 时禁用延迟转换（兼容旧调用路径）。
	/// </param>
	public void Export(GameBundle fileCollection, CoreConfiguration options, FileSystem fileSystem,
		EditorFormatProcessor? editorFormatProcessor = null)
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

		// 注入按需转换回调：ProjectYamlWalker.ExportYamlDocument 会在 WalkEditor 之前调用它，
		// 把 Process 阶段保留的非破坏性 EditorFormat 转换延迟到导出阶段逐资产执行。
		if (editorFormatProcessor != null)
		{
			container.EditorFormatConverter = editorFormatProcessor.ProcessForExport;
		}

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
				skippedCollections.Add(collection);
				redirectMap[primaryAsset] = keptCollection.Assets.FirstOrDefault()!;
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
}
