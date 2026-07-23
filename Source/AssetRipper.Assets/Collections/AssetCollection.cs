using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using System.Collections;

namespace AssetRipper.Assets.Collections;

/// <summary>
/// 一组 <see cref="IUnityObjectBase"/> 资源。
/// </summary>
public abstract class AssetCollection : IReadOnlyCollection<IUnityObjectBase>, IDisposable
{
	protected AssetCollection(Bundle bundle)
	{
		dependencies.Add(this);
		Bundle = bundle;
		bundle.AddCollection(this);
	}

	public Bundle Bundle { get; }
	public string Name { get; protected set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;

	/// <summary>
	/// 仅暴露 PathID 与 ClassID 的轻量元数据视图，用于在不触发反序列化的前提下识别资产类型。
	/// </summary>
	/// <remarks>
	/// 调用方通过 <see cref="EnumerateAssetMetadata"/> 获取此结构序列，可凭 ClassID 决定是否需要
	/// 通过 <see cref="TryGetAssetOnly"/> 进一步反序列化对象本体。
	/// </remarks>
	public readonly struct AssetMetadata
	{
		public required long PathID { get; init; }
		public required int ClassID { get; init; }
	}

	/// <summary>
	/// 此集合的依赖项列表。
	/// </summary>
	/// <remarks>
	/// 零索引项是 <see langword="this"/>，以确保文件索引与依赖项列表的索引对应。
	/// 如果依赖项未找到，则该项为 null。
	/// </remarks>
	public IReadOnlyList<AssetCollection?> Dependencies => dependencies;

	private readonly List<AssetCollection?> dependencies = new();

	public IReadOnlyDictionary<long, IUnityObjectBase> Assets => assets;

	// 改为 protected 以便子类（如 SerializedAssetCollection.TryGetAssetOnly）直接查字典判断是否已反序列化
	protected readonly Dictionary<long, IUnityObjectBase> assets = new();

	/// <summary>
	/// collection 级别的 OriginalDirectory 持久化映射，用于在不反序列化 asset 实例的情况下设置路径。
	/// 让 <see cref="OriginalPathProcessor"/> GroupByBundleName 模式能用元数据枚举设置 OriginalDirectory。
	/// </summary>
	private Dictionary<long, string>? _originalDirectoryOverrides;

	public UnityVersion OriginalVersion { get; protected set; }
	public UnityVersion Version { get; protected set; }
	public BuildTarget Platform { get; protected set; }
	public TransferInstructionFlags Flags { get; protected set; }
	public EndianType EndianType { get; protected set; }

	[MemberNotNullWhen(true, nameof(Scene))]
	public bool IsScene => Scene is not null;

	public SceneDefinition? Scene { get; internal set; }

	public int AddDependency(AssetCollection dependency)
	{
		int index = dependencies.IndexOf(dependency);
		if (index >= 0)
		{
			return index;
		}
		else if (IsCompatibleDependency(dependency))
		{
			dependencies.Add(dependency);
			return dependencies.Count - 1;
		}
		else
		{
			throw new ArgumentException($"Dependency is not compatible with this {nameof(AssetCollection)}.", nameof(dependency));
		}
	}

	protected void SetDependency(int index, AssetCollection? collection)
	{
		if (index < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}
		else if (index < dependencies.Count)
		{
			dependencies[index] = collection;
		}
		else
		{
			while (dependencies.Count < index)
			{
				dependencies.Add(null);
			}

			dependencies.Add(collection);
		}
	}

	/// <summary>
	/// 确定给定的依赖集合是否可以从该集合中引用。
	/// </summary>
	/// <param name="dependency"></param>
	/// <returns></returns>
	protected virtual bool IsCompatibleDependency(AssetCollection dependency) => true;

	public PPtr<T> CreatePPtr<T>(T? asset) where T : IUnityObjectBase
	{
		if (asset is null)
		{
			return default;
		}

		int fileIndex = dependencies.IndexOf(asset.Collection);
		if (fileIndex < 0)
		{
			throw new ArgumentException($"Asset doesn't belong to this {nameof(AssetCollection)} or any of its dependencies", nameof(asset));
		}

		return new PPtr<T>(fileIndex, asset.PathID);
	}

	public PPtr<T> ForceCreatePPtr<T>(T? asset) where T : IUnityObjectBase
	{
		if (asset is null)
		{
			return default;
		}

		int fileIndex = AddDependency(asset.Collection);
		return new PPtr<T>(fileIndex, asset.PathID);
	}

	protected void AddAsset(IUnityObjectBase asset)
	{
		ValidateAsset(asset);

		assets.Add(asset.PathID, asset);

		void ValidateAsset(IUnityObjectBase asset)
		{
			if (asset.Collection != this)
			{
				throw new ArgumentException("AssetInfo must have this marked as its collection.", nameof(asset));
			}

			if (asset.PathID is 0)
			{
				throw new ArgumentException("The zero path ID is reserved for null PPtr's.", nameof(asset));
			}
		}
	}

	/// <summary>
	/// 替换此集合中的资产。
	/// </summary>
	/// <remarks>
	/// 这对于切换底层实现非常有用，例如进行版本转换。
	/// </remarks>
	/// <param name="replacement"></param>
	public void ReplaceAsset(IUnityObjectBase replacement)
	{
		ValidateAsset(replacement);
		assets[replacement.PathID] = replacement;

		void ValidateAsset(IUnityObjectBase replacement)
		{
			if (replacement.Collection != this)
			{
				throw new ArgumentException("AssetInfo must have this marked as its collection.", nameof(replacement));
			}

			if (!assets.TryGetValue(replacement.PathID, out IUnityObjectBase? original))
			{
				throw new ArgumentException("There is no existing asset with this PathID.", nameof(replacement));
			}

			if (replacement.ClassID != original.ClassID)
			{
				throw new ArgumentException("The replacement asset's class id is not equal to the original asset's class id.", nameof(replacement));
			}
		}
	}

	public override string ToString()
	{
		return Name;
	}

	#region GetAsset Methods

	/// <summary>
	/// 首次访问资产前触发懒加载。默认实现为空，子类（如 SerializedAssetCollection）
	/// 重写此方法以在首次访问时按需反序列化对象，避免加载阶段一次性占用全部内存。
	/// </summary>
	protected virtual void EnsureAssetsLoaded() { }

	/// <summary>
	/// 枚举集合内所有资产的元数据，默认实现触发 EnsureAssetsLoaded 后枚举 assets 字典（兼容老代码）。
	/// 子类（如 <see cref="SerializedAssetCollection"/>）应重写为直接遍历底层数据源，不触发反序列化。
	/// </summary>
	/// <returns>返回每个资产的 (PathID, ClassID) 元数据序列。</returns>
	public virtual IEnumerable<AssetMetadata> EnumerateAssetMetadata()
	{
		EnsureAssetsLoaded();
		foreach (IUnityObjectBase asset in assets.Values)
		{
			yield return new AssetMetadata { PathID = asset.PathID, ClassID = asset.ClassID };
		}
	}

	/// <summary>
	/// 仅反序列化指定 PathID 对应的单个对象，不触发全量 EnsureAssetsLoaded。
	/// 默认实现回退到 <see cref="TryGetAsset(long)"/>（触发全量加载），兼容老代码。
	/// 子类应重写为按需单对象反序列化。
	/// </summary>
	public virtual IUnityObjectBase? TryGetAssetOnly(long pathID)
	{
		return TryGetAsset(pathID);
	}

	/// <summary>
	/// 仅反序列化指定 PathID 对应的单个对象并转换为指定类型。
	/// </summary>
	public T? TryGetAssetOnly<T>(long pathID) where T : IUnityObjectBase
	{
		IUnityObjectBase? asset = TryGetAssetOnly(pathID);
		return asset is T t ? t : default;
	}

	/// <summary>
	/// 在 collection 级别持久化 OriginalDirectory，避免反序列化 asset 实例即可设置路径。
	/// </summary>
	public void SetOriginalDirectory(long pathID, string directory)
	{
		_originalDirectoryOverrides ??= new();
		_originalDirectoryOverrides[pathID] = directory;
	}

	/// <summary>
	/// 从 collection 级别映射读取 OriginalDirectory。
	/// </summary>
	/// <remarks>
	/// 设为 public 以便 <see cref="AssetRipper.Processing.Scenes.OriginalPathProcessor"/> 等跨程序集 processor
	/// 能在不反序列化 asset 实例的前提下检查 OriginalDirectory 是否已设置（保持原 ??= 语义）。
	/// </remarks>
	public string? TryGetOriginalDirectory(long pathID)
	{
		return _originalDirectoryOverrides?.TryGetValue(pathID, out string? dir) == true ? dir : null;
	}

	/// <summary>
	/// 清空已反序列化的资产对象，但保留重新加载所需的数据源引用。
	/// 调用后再次访问资产会触发 <see cref="EnsureAssetsLoaded"/> 重新反序列化。
	/// 默认实现清空 assets 字典；子类可重写以重置懒加载标志。
	/// </summary>
	public virtual void UnloadAssets()
	{
		assets.Clear();
	}

	public IUnityObjectBase? TryGetAsset(long pathID)
	{
		TryGetAsset(pathID, out IUnityObjectBase? asset);
		return asset;
	}

	public T? TryGetAsset<T>(long pathID) where T : IUnityObjectBase
	{
		TryGetAsset(pathID, out T? asset);
		return asset;
	}

	public bool TryGetAsset(long pathID, [NotNullWhen(true)] out IUnityObjectBase? asset)
	{
		return TryGetAsset<IUnityObjectBase>(pathID, out asset);
	}

	public bool TryGetAsset<T>(long pathID, [NotNullWhen(true)] out T? asset) where T : IUnityObjectBase
	{
		// 触发懒加载：确保 assets 字典已填充
		EnsureAssetsLoaded();
		if (assets.TryGetValue(pathID, out IUnityObjectBase? unityObject))
		{
			if (typeof(T).IsAssignableTo(typeof(NullObject)))
			{
				//T 继承自 NullObject，因此我们允许找到空对象。
				switch (unityObject)
				{
					case T t:
						asset = t;
						return true;
					default:
						asset = default;
						return false;
				}
			}
			else
			{
				switch (unityObject)
				{
					case NullObject:
						asset = default;
						return false;
					case T t:
						asset = t;
						return true;
					default:
						asset = default;
						return false;
				}
			}
		}
		else
		{
			asset = default;
			return false;
		}
	}

	public IUnityObjectBase? TryGetAsset(int fileIndex, long pathID)
	{
		TryGetAsset(fileIndex, pathID, out IUnityObjectBase? asset);
		return asset;
	}

	public T? TryGetAsset<T>(int fileIndex, long pathID) where T : IUnityObjectBase
	{
		TryGetAsset(fileIndex, pathID, out T? asset);
		return asset;
	}

	public bool TryGetAsset(int fileIndex, long pathID, [NotNullWhen(true)] out IUnityObjectBase? asset)
	{
		AssetCollection? file = TryGetDependency(fileIndex);
		if (file is not null)
		{
			return file.TryGetAsset(pathID, out asset);
		}
		else
		{
			asset = null;
			return false;
		}
	}

	public bool TryGetAsset<T>(int fileIndex, long pathID, [NotNullWhen(true)] out T? asset) where T : IUnityObjectBase
	{
		AssetCollection? file = TryGetDependency(fileIndex);
		if (file is not null)
		{
			return file.TryGetAsset(pathID, out asset);
		}
		else
		{
			asset = default;
			return false;
		}
	}

	public IUnityObjectBase? TryGetAsset(PPtr pptr) => TryGetAsset(pptr.FileID, pptr.PathID);

	public T? TryGetAsset<T>(PPtr<T> pptr) where T : IUnityObjectBase
	{
		return TryGetAsset<T>(pptr.FileID, pptr.PathID);
	}

	public bool TryGetAsset(PPtr pptr, [NotNullWhen(true)] out IUnityObjectBase? asset) => TryGetAsset(pptr.FileID, pptr.PathID, out asset);

	public bool TryGetAsset<T>(PPtr<T> pptr, [NotNullWhen(true)] out T? asset) where T : IUnityObjectBase
	{
		return TryGetAsset(pptr.FileID, pptr.PathID, out asset);
	}

	private AssetCollection? TryGetDependency(int fileIndex)
	{
		if (fileIndex < 0 || fileIndex >= Dependencies.Count)
		{
			return null;
		}
		else
		{
			return Dependencies[fileIndex];
		}
	}

	#endregion

	#region IReadOnlyCollection

	public IEnumerator<IUnityObjectBase> GetEnumerator()
	{
		// 触发懒加载：确保 assets 字典已填充
		EnsureAssetsLoaded();
		return assets.Values.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public int Count
	{
		get
		{
			// 触发懒加载：确保 assets 字典已填充
			EnsureAssetsLoaded();
			return assets.Count;
		}
	}

	#endregion

	#region IDisposable Support

	private bool disposedValue;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				// 清空资产字典以断开对象图引用，让 GC 能尽早回收反序列化的资产
				assets.Clear();
				_originalDirectoryOverrides?.Clear();
				_originalDirectoryOverrides = null;
				dependencies.Clear();
				Scene = null;
			}

			disposedValue = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	#endregion
}
