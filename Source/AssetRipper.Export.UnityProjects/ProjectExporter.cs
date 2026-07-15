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

	private readonly ObjectHandlerStack<IAssetExporter> assetExporterStack = new();

	/// <summary>Adds an exporter to the stack of exporters for this asset type.</summary>
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
		// ProcessingSettings is stored in SingletonData by FullConfiguration. Access it directly
		// via CoreConfiguration since this project does not reference AssetRipper.Export.
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
				Logger.Info(LogCategory.ExportProgress, $"({currentExportable}/{exportableCount}) Exporting '{collection.Name}'");
				bool exportedSuccessfully = collection.Export(container, options.ProjectRootPath, fileSystem);
				if (!exportedSuccessfully)
				{
					Logger.Warning(LogCategory.ExportProgress, $"Failed to export '{collection.Name}' ({collection.GetType().Name})");
				}
			}
			EventExportProgressUpdated?.Invoke(i, collections.Count);
		}
		EventExportFinished?.Invoke();
	}

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
	/// Groups collections by their primary asset's (Type, content hash) and treats
	/// assets with identical hashes as duplicates.
	/// </summary>
	/// <remarks>
	/// The content hash is computed by <see cref="ContentHashWalker"/>, which walks the
	/// asset's serialized fields without dereferencing PPtr targets. This avoids the
	/// unbounded reference-graph traversal (and resulting <see cref="OutOfMemoryException"/>)
	/// that <see cref="AssetRipper.Assets.Cloning.AssetEqualityComparer"/> performs.
	/// A 64-bit XxHash combined with the Type in the bucket key makes false positives
	/// negligibly rare.
	/// </remarks>
	private void ApplyDeduplication(List<IExportCollection> collections,
		out HashSet<IExportCollection> skippedCollections, out Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		skippedCollections = new();
		redirectMap = new();

		// Cache primary asset per collection and bucket by (Type, content hash).
		// Assets whose hash is ContentHashWalker.Unhashable are kept entirely.
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
			$"Asset deduplication: compared {comparedCount} assets, kept {keptCount}, skipped {skippedCount}.");

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
