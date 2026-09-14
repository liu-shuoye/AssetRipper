using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.Logging;

namespace AssetRipper.Assets.Bundles;

/// <summary>
/// A container for <see cref="AssetCollection"/>s, <see cref="ResourceFile"/>s, and other <see cref="Bundle"/>s.
/// </summary>
public abstract class Bundle : IDisposable
{
	/// <summary>
	/// The parent <see cref="Bundle"/> of this Bundle.
	/// </summary>
	public Bundle? Parent { get; private set; }

	/// <summary>
	/// The list of <see cref="ResourceFile"/>s in this Bundle.
	/// </summary>
	public IReadOnlyList<ResourceFile> Resources => resources;
	private readonly List<ResourceFile> resources = [];

	/// <summary>
	/// The list of <see cref="AssetCollection"/>s in this Bundle.
	/// </summary>
	public IReadOnlyList<AssetCollection> Collections => collections;
	private readonly List<AssetCollection> collections = [];

	/// <summary>
	/// The list of child <see cref="Bundle"/>s in this Bundle.
	/// </summary>
	public IReadOnlyList<Bundle> Bundles => bundles;
	private readonly List<Bundle> bundles = [];

	/// <summary>
	/// The list of <see cref="FailedFile"/>s in this Bundle.
	/// </summary>
	public IReadOnlyList<FailedFile> FailedFiles => failedFiles;
	private readonly List<FailedFile> failedFiles = [];

	public bool AnyFailed => failedFiles.Count > 0 || bundles.Any(bundle => bundle.AnyFailed);

	private bool disposedValue;

	/// <summary>
	/// 按名称索引“本 Bundle 的集合 + 直接子 Bundle 的集合”，用于把依赖解析从“逐个子 Bundle 扫描”降为一次哈希查询。
	/// </summary>
	/// <remarks>
	/// 覆盖范围恰好等于一次解析中属于“同一层”的两步：先查自身集合，再按添加顺序查各直接子 Bundle 的集合。
	/// 每个名称只登记首个匹配（自身集合优先），与原先线性扫描返回首个匹配的语义一致——资产 Bundle 变体可能导致同名集合。
	/// 索引懒构建：首次查询时按需建立；集合或子 Bundle 变动时失效（见 <see cref="InvalidateLevelIndexes"/>），下次查询重建。
	/// 全树索引条目总数不超过集合总数的两倍（每个集合只出现在自己与父 Bundle 的索引里），
	/// 因此常驻内存有界，且比“每个 Bundle 各建一个只含自身集合的字典”更省。
	/// </remarks>
	private Dictionary<string, AssetCollection>? _levelCollectionsByName;

	/// <summary>与 <see cref="_levelCollectionsByName"/> 对应的“一层”集合条目数；<see cref="NotCounted"/> 表示未统计。</summary>
	private int _levelCollectionEntryCount = NotCounted;

	/// <summary>
	/// 按固定名称索引“本 Bundle 的资源 + 直接子 Bundle 的资源”，语义与 <see cref="_levelCollectionsByName"/> 一致，
	/// 供 <see cref="ResolveResource(string)"/> 使用。
	/// </summary>
	private Dictionary<string, ResourceFile>? _levelResourcesByName;

	/// <summary>与 <see cref="_levelResourcesByName"/> 对应的“一层”资源条目数；<see cref="NotCounted"/> 表示未统计。</summary>
	private int _levelResourceEntryCount = NotCounted;

	/// <summary>
	/// 名称索引的“一层”条目数阈值：不超过该值时解析直接线性扫描，不建字典。
	/// 30 万文件的项目里绝大多数 Bundle 只含一两个集合，这样能省掉几十万个微型字典的分配与常驻内存。
	/// </summary>
	private const int MaxLinearLevelEntryCount = 8;

	/// <summary>
	/// 条目数尚未统计（或统计已失效）的哨兵值。
	/// 必须与 0 区分：条目数为 0 是合法状态，表示这一层确实没有任何集合/资源。
	/// </summary>
	private const int NotCounted = -1;

	/// <summary>
	/// 使本 Bundle 的两个名称索引失效。集合、资源或子 Bundle 发生变动后必须调用，
	/// 否则索引会与 <see cref="Collections"/> / <see cref="Resources"/> / <see cref="Bundles"/> 不一致。
	/// </summary>
	private void InvalidateLevelIndexes()
	{
		_levelCollectionsByName = null;
		_levelCollectionEntryCount = NotCounted;
		_levelResourcesByName = null;
		_levelResourceEntryCount = NotCounted;
	}

	/// <summary>
	/// The name of this Bundle.
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// All the <see cref="SceneDefinition"/>s in this bundle.
	/// </summary>
	public IEnumerable<SceneDefinition> Scenes
	{
		get
		{
			HashSet<SceneDefinition> scenes = new();
			foreach (AssetCollection collection in FetchAssetCollections())
			{
				SceneDefinition? scene = collection.Scene;
				if (scene is not null && scenes.Add(scene))
				{
					yield return scene;
				}
			}
		}
	}

	/// <summary>
	/// 初始化此 Bundle 及其子 Bundle 中每个 SerializedAssetCollection 的依赖列表。
	/// </summary>
	/// <remarks>
	/// 大项目里这是“集合数 × 依赖数”量级的循环（30 万文件可达数百万次解析），因此除了依赖
	/// <see cref="ResolveCollection(string)"/> 的按名索引之外，本方法还刻意控制自身的开销：
	/// 重复出现的“未找到依赖项”只打印一次，并周期性输出进度（见 <see cref="DependencyInitializationContext"/>）。
	/// </remarks>
	internal void InitializeAllDependencyLists(IDependencyProvider? dependencyProvider)
	{
		DependencyInitializationContext context = new(dependencyProvider);
		InitializeAllDependencyLists(context);
		context.LogSummary();
	}

	/// <summary>
	/// <see cref="InitializeAllDependencyLists(IDependencyProvider?)"/> 的递归主体，上下文由整棵树共享。
	/// </summary>
	private void InitializeAllDependencyLists(DependencyInitializationContext context)
	{
		foreach (AssetCollection collection in Collections)
		{
			if (collection is SerializedAssetCollection serializedAssetCollection)
			{
				serializedAssetCollection.InitializeDependencyList(context.DependencyProvider);
				context.OnCollectionProcessed(collection.Name);
			}
		}
		foreach (Bundle bundle in Bundles)
		{
			bundle.InitializeAllDependencyLists(context);
		}
	}

	/// <summary>
	/// 依赖初始化过程中跨整棵 Bundle 树共享的上下文：给依赖提供者套一层日志去重，并统计进度与缺失依赖总量。
	/// </summary>
	private sealed class DependencyInitializationContext
	{
		/// <summary>最多逐条打印多少个不同的缺失依赖名，其余只计入汇总，避免日志本身拖慢初始化。</summary>
		private const int MaxReportedMissingNames = 1000;

		/// <summary>最多在内存中记录多少个不同的缺失依赖名，达到上限后只计数不记录，防止 HashSet 随缺失量无界增长。</summary>
		private const int MaxDistinctMissingNames = 10000;

		/// <summary>每处理多少个集合打印一次进度。</summary>
		private const int ProgressLogInterval = 50_000;

		private readonly IDependencyProvider? rawProvider;
		private readonly HashSet<string> reportedMissingNames = new(StringComparer.Ordinal);
		private long collectionCount;
		private long missingCount;

		public DependencyInitializationContext(IDependencyProvider? dependencyProvider)
		{
			rawProvider = dependencyProvider;
			DependencyProvider = dependencyProvider is null ? null : new DeduplicatingDependencyProvider(dependencyProvider, this);
		}

		/// <summary>供 <see cref="SerializedAssetCollection.InitializeDependencyList"/> 使用的依赖提供者。</summary>
		public IDependencyProvider? DependencyProvider { get; }

		public void OnCollectionProcessed(string name)
		{
			collectionCount++;
			if (collectionCount % ProgressLogInterval == 0)
			{
				Logger.Info(LogCategory.Import, $"依赖初始化进度：已处理 {collectionCount} 个集合，当前 '{name}'");
			}
		}

		/// <summary>
		/// 记录一次未找到的依赖。首次出现的名称会转交底层提供者打印（受上限约束），
		/// 后续同名重复只累加计数——大项目里同一个缺失依赖可能出现几十万次。
		/// </summary>
		public void OnMissingDependency(in FileIdentifier identifier)
		{
			missingCount++;
			if (reportedMissingNames.Count >= MaxDistinctMissingNames)
			{
				return;
			}

			if (reportedMissingNames.Add(identifier.GetFilePath()) && reportedMissingNames.Count <= MaxReportedMissingNames)
			{
				rawProvider?.ReportMissingDependency(identifier);
			}
		}

		public void LogSummary()
		{
			if (missingCount == 0)
			{
				return;
			}

			string distinctDescription = reportedMissingNames.Count >= MaxDistinctMissingNames
				? $"至少 {reportedMissingNames.Count} 个不同名称，已达统计上限 {MaxDistinctMissingNames}"
				: $"{reportedMissingNames.Count} 个不同名称，其中前 {Math.Min(reportedMissingNames.Count, MaxReportedMissingNames)} 个已逐条打印";
			Logger.Warning(LogCategory.Import, $"依赖初始化：共 {missingCount} 处依赖未找到（{distinctDescription}）");
		}

		/// <summary>
		/// 把缺失依赖的日志去重后转发给底层提供者的装饰器；其余方法原样转发。
		/// </summary>
		private sealed class DeduplicatingDependencyProvider(IDependencyProvider inner, DependencyInitializationContext context) : IDependencyProvider
		{
			public FileBase? FindDependency(FileIdentifier identifier) => inner.FindDependency(identifier);

			public void ReportMissingDependency(FileIdentifier identifier) => context.OnMissingDependency(identifier);
		}
	}

	/// <summary>
	/// Resolves an <see cref="AssetCollection"/> with the specified name in this Bundle and its ascendants.
	/// </summary>
	/// <param name="identifier">The identifier of the file of the <see cref="AssetCollection"/>.</param>
	/// <returns>The resolved <see cref="AssetCollection"/> if it exists, else null.</returns>
	public AssetCollection? ResolveCollection(FileIdentifier identifier)
	{
		return ResolveCollection(identifier.GetFilePath());
	}

	/// <summary>
	/// Resolves an <see cref="AssetCollection"/> with the specified name in this Bundle and its ascendants.
	/// </summary>
	/// <param name="name">The name of the <see cref="AssetCollection"/>.</param>
	/// <returns>The resolved <see cref="AssetCollection"/> if it exists, else null.</returns>
	public AssetCollection? ResolveCollection(string name)
	{
		AssetCollection? result = ResolveInternal(name);
		if (result is not null)
		{
			return result;
		}

		string fixedName = SpecialFileNames.FixFileIdentifier(name);
		// 调用方传入的名字通常已经规范化（FileIdentifier.PathName 在读取时就 Fix 过，集合名也来自 NameFixed），
		// 此时再解析一次纯属重复查询；树越大重复成本越高，故仅在名字确实被改动时才重试。
		if (!string.Equals(fixedName, name, StringComparison.Ordinal))
		{
			result = ResolveInternal(fixedName);
			if (result is not null)
			{
				return result;
			}
		}

		return fixedName switch
		{
			SpecialFileNames.DefaultResourceName1 => ResolveInternal(SpecialFileNames.DefaultResourceName2),
			SpecialFileNames.DefaultResourceName2 => ResolveInternal(SpecialFileNames.DefaultResourceName1),
			SpecialFileNames.BuiltinExtraName1 => ResolveInternal(SpecialFileNames.BuiltinExtraName2),
			SpecialFileNames.BuiltinExtraName2 => ResolveInternal(SpecialFileNames.BuiltinExtraName1),
			_ => null,
		};

	}

	/// <summary>
	/// 自本 Bundle 逐层向上解析集合：每层先查自身集合，再按添加顺序查各直接子 Bundle 的集合。
	/// </summary>
	/// <remarks>
	/// 每层都是对 <see cref="_levelCollectionsByName"/> 的一次哈希查询，整体代价为树深，
	/// 与子 Bundle 数量无关——这是依赖初始化能在几十万文件规模下跑完的关键。
	/// 原实现对每一层都要遍历全部子 Bundle，30 万个子 Bundle 时单次解析就是几十万次比较。
	/// </remarks>
	private AssetCollection? ResolveInternal(string name)
	{
		Bundle? currentBundle = this;
		while (currentBundle is not null)
		{
			AssetCollection? result = currentBundle.ResolveFromLevel(name);
			if (result is not null)
			{
				return result;
			}

			currentBundle = currentBundle.Parent;
		}

		return null;
	}

	/// <summary>
	/// 在“本 Bundle 的集合 + 直接子 Bundle 的集合”范围内按名称解析，即 <see cref="ResolveInternal"/> 中的一层。
	/// </summary>
	/// <remarks>
	/// 条目多时用 <see cref="_levelCollectionsByName"/> 缓存；条目少时直接线性扫描，
	/// 以免为几十万个只含一两个集合的 Bundle 各分配一个字典（外加一份计数缓存）。
	/// 每个名称只取首个匹配，与原先“自身集合优先，再按子 Bundle 顺序”的语义一致。
	/// </remarks>
	private AssetCollection? ResolveFromLevel(string name)
	{
		if (_levelCollectionsByName is { } index)
		{
			return index.GetValueOrDefault(name);
		}

		if (CountLevelEntries(ref _levelCollectionEntryCount, static bundle => bundle.collections.Count) <= MaxLinearLevelEntryCount)
		{
			foreach (AssetCollection collection in collections)
			{
				if (collection.Name == name)
				{
					return collection;
				}
			}
			foreach (Bundle bundle in bundles)
			{
				foreach (AssetCollection collection in bundle.collections)
				{
					if (collection.Name == name)
					{
						return collection;
					}
				}
			}
			return null;
		}

		Dictionary<string, AssetCollection> built = new(_levelCollectionEntryCount);
		foreach (AssetCollection collection in collections)
		{
			built.TryAdd(collection.Name, collection);
		}
		foreach (Bundle bundle in bundles)
		{
			foreach (AssetCollection collection in bundle.collections)
			{
				built.TryAdd(collection.Name, collection);
			}
		}
		_levelCollectionsByName = built;
		return built.GetValueOrDefault(name);
	}

	/// <summary>
	/// 统计“一层”条目数（自身条目 + 直接子 Bundle 的条目），结果缓存在 <paramref name="cachedCount"/> 中。
	/// </summary>
	/// <param name="cachedCount">缓存字段；等于 <see cref="NotCounted"/> 时重新统计。</param>
	/// <param name="countInBundle">给定子 Bundle，返回它贡献的条目数。</param>
	private int CountLevelEntries(ref int cachedCount, Func<Bundle, int> countInBundle)
	{
		if (cachedCount != NotCounted)
		{
			return cachedCount;
		}

		int count = countInBundle(this);
		foreach (Bundle bundle in bundles)
		{
			count += countInBundle(bundle);
		}
		cachedCount = count;
		return count;
	}

	/// <summary>
	/// Resolves a ResourceFile with the specified name in this Bundle and its ascendants.
	/// </summary>
	/// <param name="name">The name of the ResourceFile.</param>
	/// <returns>The resolved ResourceFile if it exists, else null.</returns>
	public ResourceFile? ResolveResource([NotNullWhen(true)] string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}

		string originalName = name;
		string fixedName = SpecialFileNames.FixFileIdentifier(name);

		// 逐层向上：每层先查资源（自身 + 直接子 Bundle），再给该层一次向外部提供者求助的机会。
		// 与集合解析同理，每层靠 _levelResourcesByName 做到与子 Bundle 数量无关。
		Bundle? currentBundle = this;
		while (currentBundle is not null)
		{
			ResourceFile? result = currentBundle.ResolveResourceFromLevel(fixedName)
				?? currentBundle.ResolveExternalResource(originalName);
			if (result is not null)
			{
				return result;
			}

			currentBundle = currentBundle.Parent;
		}

		return null;

	}

	/// <summary>
	/// 在“本 Bundle 的资源 + 直接子 Bundle 的资源”范围内按固定名称解析，即 <see cref="ResolveResource(string)"/> 中的一层。
	/// </summary>
	/// <param name="fixedName">经 <see cref="SpecialFileNames.FixFileIdentifier"/> 规范化的资源名。</param>
	/// <remarks>取舍与 <see cref="ResolveFromLevel"/> 相同：条目少时线性扫描，条目多时才建字典缓存。</remarks>
	private ResourceFile? ResolveResourceFromLevel(string fixedName)
	{
		if (_levelResourcesByName is { } index)
		{
			return index.GetValueOrDefault(fixedName);
		}

		if (CountLevelEntries(ref _levelResourceEntryCount, static bundle => bundle.resources.Count) <= MaxLinearLevelEntryCount)
		{
			foreach (ResourceFile resource in resources)
			{
				if (resource.NameFixed == fixedName)
				{
					return resource;
				}
			}
			foreach (Bundle bundle in bundles)
			{
				foreach (ResourceFile resource in bundle.resources)
				{
					if (resource.NameFixed == fixedName)
					{
						return resource;
					}
				}
			}
			return null;
		}

		Dictionary<string, ResourceFile> built = new(_levelResourceEntryCount);
		foreach (ResourceFile resource in resources)
		{
			built.TryAdd(resource.NameFixed, resource);
		}
		foreach (Bundle bundle in bundles)
		{
			foreach (ResourceFile resource in bundle.resources)
			{
				built.TryAdd(resource.NameFixed, resource);
			}
		}
		_levelResourcesByName = built;
		return built.GetValueOrDefault(fixedName);
	}

	protected virtual ResourceFile? ResolveExternalResource(string originalName) => null;

	/// <summary>
	/// Adds a ResourceFile to this Bundle.
	/// </summary>
	/// <param name="resource">The ResourceFile to add.</param>
	public void AddResource(ResourceFile resource)
	{
		resources.Add(resource);
		// 自身资源变化 → 本 Bundle 的一层索引失效；父 Bundle 的一层索引也包含本 Bundle 的资源，同样失效
		InvalidateLevelIndexes();
		Parent?.InvalidateLevelIndexes();
	}

	/// <summary>
	/// Adds an <see cref="AssetCollection"/> to this Bundle.
	/// </summary>
	/// <param name="collection">The <see cref="AssetCollection"/> to add.</param>
	public void AddCollection(AssetCollection collection)
	{
		if (collection.Bundle != this)
		{
			throw new ArgumentException($"Collection's {nameof(AssetCollection.Bundle)} property did not match this.", nameof(collection));
		}
		else if (IsCompatibleCollection(collection))
		{
			collections.Add(collection);
			// 本 Bundle 的一层索引含自身集合，父 Bundle 的一层索引含本 Bundle 的集合，两者都必须失效
			InvalidateLevelIndexes();
			Parent?.InvalidateLevelIndexes();
		}
		else
		{
			throw new ArgumentException($"The collection is not compatible with this {nameof(Bundle)}.", nameof(collection));
		}
	}

	/// <summary>
	/// 向此 Bundle 添加一个子 Bundle 。
	/// </summary>
	/// <param name="bundle">要添加的 Bundle </param>
	public void AddBundle(Bundle bundle)
	{
		if (bundle.Parent is null)
		{
			if (IsCompatibleBundle(bundle))
			{
				bundles.Add(bundle);
				bundle.Parent = this;
				// 新子 Bundle 的集合进入了本 Bundle 的一层索引，索引失效待重建
				InvalidateLevelIndexes();
			}
			else
			{
				throw new ArgumentException($"Child {nameof(Bundle)} is not compatible with this parent {nameof(Bundle)}.", nameof(bundle));
			}
		}
		else if (bundle.Parent == this)
		{
		}
		else
		{
			throw new ArgumentException($"{nameof(bundle)} already has a parent.", nameof(bundle));
		}
	}

	public void AddFailed(FailedFile file)
	{
		failedFiles.Add(file);
	}

	/// <summary>
	/// Indicates if the specified <see cref="AssetCollection"/> is compatible with this Bundle.
	/// </summary>
	/// <param name="collection">The <see cref="AssetCollection"/> to check.</param>
	/// <returns>True if the <see cref="AssetCollection"/> is compatible, else false.</returns>
	protected virtual bool IsCompatibleCollection(AssetCollection collection) => true;

	/// <summary>
	/// 表示指定的 Bundle 是否与当前 Bundle 兼容。
	/// </summary>
	/// <param name="bundle">The Bundle to check.</param>
	/// <returns>True if the Bundle is compatible, else false.</returns>
	protected virtual bool IsCompatibleBundle(Bundle bundle) => bundle is not GameBundle;

	/// <summary>
	/// 获取此 Bundle 的根 Bundle 。
	/// </summary>
	/// <returns>The root Bundle of this Bundle.</returns>
	public Bundle GetRoot()
	{
		Bundle root = this;
		while (root.Parent is not null)
		{
			root = root.Parent;
		}
		return root;
	}

	/// <summary>
	/// 获取此 Bundle 层次结构中所有 IUnityObjectBase 的 IEnumerable。
	/// </summary>
	/// <returns>此 Bundle 层次结构中所有 IUnityObjectBase 的 IEnumerable。</returns>
	public IEnumerable<IUnityObjectBase> FetchAssetsInHierarchy()
	{
		return GetRoot().FetchAssets();
	}

	/// <summary>
	/// 获取此 Bundle 中所有的 <see cref="IUnityObjectBase"/>。
	/// </summary>
	/// <returns>此 Bundle 中所有 <see cref="IUnityObjectBase"/> 的 IEnumerable。</returns>
	public IEnumerable<IUnityObjectBase> FetchAssets()
	{
		foreach (AssetCollection collection in collections)
		{
			foreach (IUnityObjectBase asset in collection)
			{
				yield return asset;
			}
		}
		foreach (Bundle bundle in bundles)
		{
			foreach (IUnityObjectBase asset in bundle.FetchAssets())
			{
				yield return asset;
			}
		}
	}

	/// <summary>
	/// 获取此 Bundle 中所有 AssetCollections 的 IEnumerable。
	/// </summary>
	/// <returns>包含层级中所有 AssetCollection 的 IEnumerable。</returns>
	public IEnumerable<AssetCollection> FetchAssetCollections()
	{
		foreach (AssetCollection collection in collections)
		{
			yield return collection;
		}
		foreach (Bundle bundle in bundles)
		{
			foreach (AssetCollection collection in bundle.FetchAssetCollections())
			{
				yield return collection;
			}
		}
	}

	public IEnumerable<ResourceFile> FetchResourceFiles()
	{
		foreach (ResourceFile resource in resources)
		{
			yield return resource;
		}
		foreach (Bundle bundle in bundles)
		{
			foreach (ResourceFile resource in bundle.FetchResourceFiles())
			{
				yield return resource;
			}
		}
	}

	public override string ToString()
	{
		return Name;
	}

	public SerializedAssetCollection AddCollectionFromSerializedFile(SerializedFile file, AssetFactoryBase factory, UnityVersion defaultVersion = default)
	{
		return SerializedAssetCollection.FromSerializedFile(this, file, factory, defaultVersion);
	}

	#region IDisposable Support
	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				// 先释放 AssetCollection：清空资产字典以断开对象图引用，让 GC 能尽早回收反序列化的资产
				foreach (AssetCollection collection in collections)
				{
					collection.Dispose();
				}

				foreach (ResourceFile resourceFile in resources)
				{
					resourceFile.Dispose();
				}

				foreach (Bundle bundle in bundles)
				{
					bundle.Dispose();
				}

				// 清空三个列表，让持有的引用立即可回收
				collections.Clear();
				resources.Clear();
				bundles.Clear();
				// 索引也要失效，避免继续滞留集合/资源引用；父 Bundle 的一层索引包含本 Bundle 的条目，同样失效
				InvalidateLevelIndexes();
				Parent?.InvalidateLevelIndexes();
			}

			disposedValue = true;
		}
	}

	// This code added to correctly implement the disposable pattern.
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
