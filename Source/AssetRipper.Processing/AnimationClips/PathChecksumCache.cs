using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.Checksum;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.SerializationLogic;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_111;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_90;
using AssetRipper.SourceGenerated.Classes.ClassID_95;
using AssetRipper.SourceGenerated.Extensions;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AssetRipper.Processing.AnimationClips;

/// <summary>
/// Attempts to recover field paths from <see cref="uint"/> hash values.
/// </summary>
/// <remarks>
/// Replicates Unity CRC32 checksum usage for field names and paths.
/// </remarks>
public readonly struct PathChecksumCache
{
	public PathChecksumCache(GameData gameData) : this(gameData.GameBundle.FetchAssetCollections(), gameData.AssemblyManager)
	{
	}

	/// <summary>
	/// 从一组资产集合构建路径哈希缓存。供处理阶段（<see cref="GameData"/>）与导出阶段
	/// （延迟转换时按需重建）共用：导出端没有 <see cref="GameData"/>，只有可达的集合与程序集管理器。
	/// </summary>
	public PathChecksumCache(IEnumerable<AssetCollection> collections, IAssemblyManager assemblyManager)
	{
		this.assemblyManager = assemblyManager;
		this.collections = collections as AssetCollection[] ?? collections.ToArray();
		BuildPathsCache(this.collections);
	}

	private readonly Dictionary<string, uint> cachedPropertyNames = new() { { string.Empty, 0 } };
	private readonly Dictionary<uint, string> cachedChecksums = new() { { 0, string.Empty } };
	private readonly HashSet<AssetInfo> processedAssets = new();
	private readonly HashSet<string> knownPaths = new();
	private readonly AssetCollection[] collections;
	private readonly List<string> avatarPaths = new();
	private readonly IAssemblyManager assemblyManager;

	/// <summary>
	/// 每个游戏根 Bundle 只建一次 AssetBundle 名索引：导出阶段每个剪辑都会新建缓存，
	/// 但集合与 AssetBundle 名通常不变，避免对 10 万级集合反复反序列化 IAssetBundle。
	/// </summary>
	private static readonly ConditionalWeakTable<Bundle, Dictionary<string, AssetCollection>> s_assetBundleNameIndexes = new();

	private void AddAnimatorPathsToCache(IAnimator animator)
	{
		IAvatar? avatar = animator.AvatarP;
		if (avatar != null)
		{
			AddAvatarTOS(avatar);
			return;
		}

		if (animator.Has_HasTransformHierarchy() && !animator.HasTransformHierarchy)
		{
			return;
		}

		IGameObject? gameObject = animator.GameObjectP;
		if (gameObject is null)
		{
			return;
		}
		AddGameObjectPathsToCacheRecursive(gameObject, string.Empty);
	}

	private void AddGameObjectPathsToCacheRecursive(IGameObject parent, string parentPath)
	{
		ITransform transform = parent.GetTransform();

		foreach (ITransform? childTransform in transform.Children_C4P)
		{
			IGameObject? child = childTransform?.GameObject_C4P;
			if (child is null)
			{
				continue;
			}

			string path = string.IsNullOrEmpty(parentPath)
				? child.Name
				: $"{parentPath}/{child.Name}";

			uint pathHash = Crc32Algorithm.HashUTF8(path);
			AddKeys(pathHash, path);

			AddGameObjectPathsToCacheRecursive(child, path);
		}
	}

	private void AddAnimationPathsToCache(IAnimation animation)
	{
		IGameObject? go = animation.GameObjectP;
		if (go is null)
		{
			return;
		}
		AddGameObjectPathsToCacheRecursive(go, string.Empty);
	}

	private void AddAvatarTOS(IAvatar avatar)
	{
		foreach ((uint key, Utf8String value) in avatar.TOS)
		{
			avatarPaths.Add(value.ToString());
			AddKeys(key, value);
		}
	}

	private void BuildPathsCache(IEnumerable<AssetCollection> collections)
	{
		// 用元数据枚举避免 bundle.FetchAssets() 触发全量反序列化。
		// 仅对 IAvatar (90) / IAnimator (95) / IAnimation (111) 调用 TryGetAssetOnly 做单对象反序列化。
		foreach (AssetCollection collection in collections)
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				switch (meta.ClassID)
				{
					case 90: // IAvatar
						IAvatar? avatar = collection.TryGetAssetOnly<IAvatar>(meta.PathID);
						if (avatar is not null)
						{
							AddAvatarTOS(avatar);
						}
						break;
					case 95: // IAnimator
						IAnimator? animator = collection.TryGetAssetOnly<IAnimator>(meta.PathID);
						if (animator is not null)
						{
							AddAnimatorPathsToCache(animator);
						}
						break;
					case 111: // IAnimation
						IAnimation? animation = collection.TryGetAssetOnly<IAnimation>(meta.PathID);
						if (animation is not null)
						{
							AddAnimationPathsToCache(animation);
						}
						break;
				}
			}
		}
	}

	public uint Add(string path)
	{
		if (cachedPropertyNames.TryGetValue(path, out uint value))
		{
			return value;
		}

		uint output = Crc32Algorithm.HashUTF8(path);

		AddKeys(output, path);
		return output;
	}

	public void Add(IMonoScript script)
	{
		if (!processedAssets.Add(script.AssetInfo))
		{
			return;
		}

		SerializableType? behaviour = script.GetBehaviourType(assemblyManager);

		if (behaviour is null)
		{
			return;
		}

		for (int f = 0; f < behaviour.Fields.Count; f++)
		{
			SerializableType.Field field = behaviour.Fields[f];
			AddFieldRecursively(field);
		}
	}

	private void AddFieldRecursively(SerializableType.Field field, string path = "", int depth = 0)
	{
		// Time out if we go too deeply to prevent infinite recursion
		if (depth > 10)
		{
			return;
		}

		if (field.Type.IsPrimitive())
		{
			// Only primitive fields can be animated.
			Add($"{path}{field.Name}");
		}
		else
		{
			string basePath = $"{path}{field.Name}.";
			for (int i = 0; i < field.Type.Fields.Count; i++)
			{
				AddFieldRecursively(field.Type.Fields[i], basePath, depth + 1);
			}
		}
	}

	public bool TryGetPath(uint identifier, [NotNullWhen(true)] out string? path)
	{
		return cachedChecksums.TryGetValue(identifier, out path);
	}

	/// <summary>
	/// Avatar TOS 里收集到的骨架路径，供 Nikki4 布料运行时挂点解析使用。
	/// </summary>
	public IReadOnlyList<string> AvatarPaths => avatarPaths;

	/// <summary>
	/// 缓存里出现过的全部路径名（Avatar/Animator/Animation 层级与字段路径）。
	/// 布料挂点解析用它作为候选父路径，避免只依赖可能为空的 Avatar TOS。
	/// </summary>
	public IReadOnlyCollection<string> KnownPaths => knownPaths;

	/// <summary>缓存覆盖的资产集合数量（用于分批/耗时评估）。</summary>
	public int CollectionCount => collections.Length;

	/// <summary>缓存里的路径名数量（用于分批/耗时评估）。</summary>
	public int KnownPathCount => knownPaths.Count;

	/// <summary>Avatar TOS 路径数量（用于分批/耗时评估）。</summary>
	public int AvatarPathCount => avatarPaths.Count;

	/// <summary>
	/// 按 bundle 文件名查找集合（例如 <c>ph00151.prefab</c>）。
	/// 只走一次构建的名称索引：布料解析会对每个 clip 尝试多个候选名，
	/// 不能对每个候选线性扫描全部集合。
	/// </summary>
	public AssetCollection? FindCollectionByBundleFileName(string fileName)
	{
		return GetAssetBundleNameIndex().TryGetValue(fileName, out AssetCollection? result) ? result : null;
	}

	private Dictionary<string, AssetCollection> GetAssetBundleNameIndex()
	{
		if (collections.Length == 0)
		{
			return new Dictionary<string, AssetCollection>(StringComparer.OrdinalIgnoreCase);
		}

		Bundle root = collections[0].Bundle.GetRoot();
		AssetCollection[] localCollections = collections;
		return s_assetBundleNameIndexes.GetValue(root, _ => BuildAssetBundleNameIndex(localCollections));
	}

	private static Dictionary<string, AssetCollection> BuildAssetBundleNameIndex(AssetCollection[] collections)
	{
		Dictionary<string, AssetCollection> index = new(StringComparer.OrdinalIgnoreCase);
		foreach (AssetCollection collection in collections)
		{
			// Bundle.Name 在 Nikki4 上通常是 CAB 哈希，但少数裸 SerializedFile 会带逻辑文件名。
			string collectionName = collection.Bundle.Name;
			if (collectionName.Length != 0)
			{
				index.TryAdd(collectionName, collection);
				string collectionFileName = Path.GetFileName(collectionName);
				if (collectionFileName.Length != 0)
				{
					index.TryAdd(collectionFileName, collection);
				}
			}

			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID != 142)
				{
					continue;
				}

				IAssetBundle? assetBundle = collection.TryGetAssetOnly<IAssetBundle>(meta.PathID);
				if (assetBundle is null)
				{
					continue;
				}

				string bundleName = assetBundle.GetAssetBundleName();
				string? assetName = assetBundle.TryGetName();
				if (!string.IsNullOrEmpty(bundleName))
				{
					index.TryAdd(bundleName, collection);
					string fileName = Path.GetFileName(bundleName);
					if (fileName.Length != 0)
					{
						index.TryAdd(fileName, collection);
					}
				}

				if (!string.IsNullOrEmpty(assetName))
				{
					index.TryAdd(assetName, collection);
					string fileName = Path.GetFileName(assetName);
					if (fileName.Length != 0)
					{
						index.TryAdd(fileName, collection);
					}
				}

				// AssetBundle 的资源清单 key 才是逻辑资产路径（如 .../ph00151.prefab）；
				// m_AssetBundleName / m_Name 在 Nikki4 上可能只是 CAB 哈希。
				foreach (AccessPairBase<Utf8String, AssetRipper.SourceGenerated.Subclasses.AssetInfo.IAssetInfo> pair in assetBundle.Container)
				{
					string assetPath = pair.Key.ToString();
					if (assetPath.Length == 0)
					{
						continue;
					}

					index.TryAdd(assetPath, collection);
					string assetFileName = Path.GetFileName(assetPath);
					if (assetFileName.Length != 0)
					{
						index.TryAdd(assetFileName, collection);
					}
				}
				break;
			}
		}
		return index;
	}

	public void Reset()
	{
		cachedPropertyNames.Clear();
		cachedChecksums.Clear();
		processedAssets.Clear();
		knownPaths.Clear();
		avatarPaths.Clear();
	}

	private void AddKeys(uint checksum, string propertyName)
	{
		knownPaths.Add(propertyName);
		cachedPropertyNames[propertyName] = checksum;
		cachedChecksums[checksum] = propertyName;
	}
}
