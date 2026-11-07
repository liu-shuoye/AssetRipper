using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Checksum;
using AssetRipper.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.GenericBinding;
using System;
using System.Collections.Generic;

namespace AssetRipper.Processing.AnimationClips;

/// <summary>
/// 为 Nikki4 的布料动画补充运行时骨架路径。
/// </summary>
/// <remarks>
/// 布料预制体在运行时会被 <c>NN4Skeleton.AddPart</c> 移植到角色骨架挂点（例如
/// <c>head_neck_upper</c>），因此动画剪辑里的 path 哈希对应的是
/// <c>root/hips/.../head_neck_upper/&lt;DB 链&gt;</c>，而不是布料预制体的
/// <c>PH00151/__DB/...</c> 静态层级。AssetRipper 的 <see cref="PathChecksumCache"/>
/// 只从当前资产图里的 Avatar/Animator/Animation 收集路径，收集不到运行时移植后的
/// 路径，于是把哈希写成 <c>path_0x...</c> 占位。
/// <para>
/// 本解析器用剪辑自身的 32 位 path 哈希做精确校验：把布料预制体的 DB / REG 路径分别拼到
/// Avatar TOS 里的每个骨架路径后面，选择命中数最高的挂点，再把该挂点下全部骨骼路径
/// 注入缓存。只有真实存在的节点组合才会被注入，未命中的哈希仍保留占位。
/// </para>
/// <para>
/// 部分宠物/挂件（PABA 系列）的骨骼路径是三段式：<c>挂点/REG 骨骼链/DB 链</c>，
/// 因为运行时 REG 子树会整体移植到挂点，DB 链再挂在 REG 节点下。两段式探针找不到
/// 这类路径时，用 REG 节点 + DB 链根做二级探针。
/// </para>
/// </remarks>
public static class Nikki4ClothAnimationPathResolver
{
	private const int MaxDepth = 64;
	/// <summary>探针至少命中数；低于此值认为不是该部件的运行时路径。</summary>
	private const int MinProbeHits = 3;
	/// <summary>三段式搜索的规模上限，防止超大预制体让扫描退化成分钟级。</summary>
	private const int MaxRegPathsForDeepSearch = 400;
	private const int MaxDbRootsForProbe = 200;
	/// <summary>三段式粗筛使用的 DB 链根数量。</summary>
	private const int CoarseProbeCount = 3;
	/// <summary>粗筛后进入精算的组合数量。</summary>
	private const int RefineCandidateCount = 8;

	/// <summary>部件 prefab 的骨骼布局：DB 链、REG 子树、两者的链根。</summary>
	private sealed class BoneLayout
	{
		public string[] DbPaths = [];
		public string[] DbRoots = [];
		public string[] RegPaths = [];
	}

	private static readonly Dictionary<string, BoneLayout> s_dbPathCache = new(StringComparer.OrdinalIgnoreCase);
	/// <summary>clip 名 → 解析出的部件名（null 表示不是部件动画），按进程缓存。</summary>
	private static readonly Dictionary<string, string?> s_partNameCache = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// 尝试给当前剪辑补路径；失败时静默返回，不改变既有行为。
	/// </summary>
	public static void TryAddPaths(IAnimationClip clip, PathChecksumCache cache)
	{
		string clipName = clip.Name_C130.ToString();
		string? partName = ResolvePartName(GetPartName(clipName), cache);
		if (partName is null)
		{
			return;
		}

		HashSet<uint> hashes = GetBindingHashes(clip);
		if (hashes.Count == 0)
		{
			return;
		}

		BoneLayout layout = GetBoneLayout(cache, partName);
		if (layout.DbPaths.Length == 0 && layout.RegPaths.Length == 0)
		{
			return;
		}

		// 两段式：挂点 + DB 链（普通布料）。
		string? bestAvatar = FindBestAvatarPath(cache, layout.DbPaths, hashes);
		if (bestAvatar is not null)
		{
			foreach (string dbPath in layout.DbPaths)
			{
				cache.Add($"{bestAvatar}/{dbPath}");
			}
			LogResolved(clipName, bestAvatar, layout.DbPaths.Length, hashes, cache, "DB");
			return;
		}

		// 三段式：挂点 + REG 骨骼链 + DB 链（宠物/挂件）。
		(string Mount, string Reg)? regMount = FindBestRegMount(cache, layout, hashes);
		if (regMount is null)
		{
			return;
		}

		int injected = 0;
		foreach (string regPath in layout.RegPaths)
		{
			cache.Add($"{regMount.Value.Mount}/{regPath}");
			injected++;
			foreach (string dbPath in layout.DbPaths)
			{
				string full = $"{regMount.Value.Mount}/{regPath}/{dbPath}";
				// 只注入真正命中哈希的组合，避免把大量无关键乘进缓存。
				if (hashes.Contains(Crc32Algorithm.HashUTF8(full)))
				{
					cache.Add(full);
					injected++;
				}
			}
		}
		LogResolved(clipName, regMount.Value.Mount, injected, hashes, cache, $"REG/DB，REG 前缀 '{regMount.Value.Reg}'");
	}

	private static void LogResolved(string clipName, string mount, int injected, HashSet<uint> hashes, PathChecksumCache cache, string kind)
	{
		int resolved = 0;
		foreach (uint hash in hashes)
		{
			if (cache.TryGetPath(hash, out _))
			{
				resolved++;
			}
		}

		Logger.Info(LogCategory.Processing,
			$"Nikki4 布料路径：clip '{clipName}' 挂点 '{mount}'，注入 {injected} 条 {kind} 路径，"
			+ $"已解析 {resolved}/{hashes.Count} 个哈希。");
	}

	private static string GetPartName(string clipName)
	{
		int slash = clipName.LastIndexOf('/');
		if (slash >= 0)
		{
			clipName = clipName[(slash + 1)..];
		}
		if (clipName.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
		{
			clipName = clipName[..^5];
		}
		return clipName;
	}

	/// <summary>
	/// 布料部件 ID 形如 PH00151 / PABA00234 / S0656H_Y1；普通动作名（idle03、ClothK0）不参与解析。
	/// </summary>
	private static bool LooksLikePartName(string name)
	{
		if (name.Length < 5)
		{
			return false;
		}

		bool hasLetter = false;
		bool hasDigit = false;
		foreach (char c in name)
		{
			if (c >= 'A' && c <= 'Z')
			{
				hasLetter = true;
			}
			else if (c >= '0' && c <= '9')
			{
				hasDigit = true;
			}
			else if (c != '_')
			{
				return false;
			}
		}
		return hasLetter && hasDigit;
	}

	/// <summary>
	/// 从 clip 名里解析出部件 ID。
	/// 纯部件 clip（PH00151）直接命中；组合动画名（idle_cloth_S0656H_Y1_01、
	/// self_cloth_S0671ABS_06）本身不是 prefab 名，需要按段组合找出真实存在的 prefab。
	/// 只返回能对应到实际 prefab 集合的候选，避免把普通动作误判成部件。
	/// </summary>
	private static string? ResolvePartName(string clipName, PathChecksumCache cache)
	{
		if (s_partNameCache.TryGetValue(clipName, out string? cached))
		{
			return cached;
		}

		string? result = null;
		if (LooksLikePartName(clipName) && ExistsPrefab(cache, clipName))
		{
			result = clipName;
		}
		else
		{
			string[] segments = clipName.Split('_');
			// 从最左边、最长的组合开始，保证 S0656H_Y1_01 先落到 S0656H_Y1 而不是 S0656H。
			for (int start = 0; start < segments.Length && result is null; start++)
			{
				// end > start 才能取到“单段”候选（如 idle_cloth_1008_S0680ABJ_idle01 里的 S0680ABJ）。
				for (int end = segments.Length; end > start; end--)
				{
					string candidate = string.Join('_', segments, start, end - start);
					if (LooksLikePartName(candidate) && ExistsPrefab(cache, candidate))
					{
						result = candidate;
						break;
					}
				}
			}
		}

		s_partNameCache[clipName] = result;
		return result;
	}

	private static bool ExistsPrefab(PathChecksumCache cache, string partName)
	{
		return cache.FindCollectionByBundleFileName($"{partName}.prefab") is not null;
	}

	private static HashSet<uint> GetBindingHashes(IAnimationClip clip)
	{
		HashSet<uint> hashes = new();
		if (!clip.Has_ClipBindingConstant_C74())
		{
			return hashes;
		}

		foreach (IGenericBinding binding in clip.ClipBindingConstant_C74.GenericBindings)
		{
			hashes.Add(binding.Path);
		}
		return hashes;
	}

	private static string? FindBestAvatarPath(PathChecksumCache cache, string[] dbPaths, HashSet<uint> hashes)
	{
		// 用每条 DB 链的根节点作为探针，避免对每个 Avatar 路径都遍历全部节点。
		string[] probes = GetProbePaths(dbPaths);
		if (probes.Length == 0)
		{
			return null;
		}

		string? bestAvatar = null;
		int bestHits = 0;
		foreach (string parentPath in cache.KnownPaths)
		{
			if (parentPath.Length == 0)
			{
				continue;
			}

			int hits = 0;
			foreach (string probe in probes)
			{
				if (hashes.Contains(Crc32Algorithm.HashUTF8($"{parentPath}/{probe}")))
				{
					hits++;
				}
			}
			if (hits > bestHits)
			{
				bestHits = hits;
				bestAvatar = parentPath;
			}
		}

		return bestHits >= MinProbeHits ? bestAvatar : null;
	}

	/// <summary>
	/// 三段式挂点搜索：用 <c>挂点/REG 节点/DB 链根</c> 的哈希命中数定位挂点与 REG 前缀。
	/// 先用少量 DB 链根粗筛，再对候选组合精算，避免 REG × 挂点 × DB 的全量乘积。
	/// </summary>
	private static (string Mount, string Reg)? FindBestRegMount(PathChecksumCache cache, BoneLayout layout, HashSet<uint> hashes)
	{
		if (layout.RegPaths.Length == 0 || layout.RegPaths.Length > MaxRegPathsForDeepSearch)
		{
			return null;
		}
		if (layout.DbRoots.Length == 0 || layout.DbRoots.Length > MaxDbRootsForProbe)
		{
			return null;
		}

		List<string> mounts = new();
		HashSet<string> seenMounts = new();
		foreach (string path in cache.AvatarPaths)
		{
			if (path.Length != 0 && path.Contains('/') && seenMounts.Add(path))
			{
				mounts.Add(path);
			}
		}
		if (mounts.Count == 0)
		{
			return null;
		}

		int coarseCount = Math.Min(CoarseProbeCount, layout.DbRoots.Length);
		int bestCoarse = 0;
		string? bestMount = null;
		string? bestReg = null;
		foreach (string mount in mounts)
		{
			foreach (string reg in layout.RegPaths)
			{
				int hits = 0;
				for (int i = 0; i < coarseCount; i++)
				{
					if (hashes.Contains(Crc32Algorithm.HashUTF8($"{mount}/{reg}/{layout.DbRoots[i]}")))
					{
						hits++;
					}
				}
				if (hits > bestCoarse)
				{
					bestCoarse = hits;
					bestMount = mount;
					bestReg = reg;
				}
			}
		}
		if (bestMount is null || bestReg is null || bestCoarse == 0)
		{
			return null;
		}

		// 精算：对粗筛靠前的候选组合统计全部 DB 链根的命中数。
		List<(int Hits, string Mount, string Reg)> candidates = new();
		foreach (string mount in mounts)
		{
			foreach (string reg in layout.RegPaths)
			{
				int hits = 0;
				foreach (string dbRoot in layout.DbRoots)
				{
					if (hashes.Contains(Crc32Algorithm.HashUTF8($"{mount}/{reg}/{dbRoot}")))
					{
						hits++;
					}
				}
				if (hits > 0)
				{
					candidates.Add((hits, mount, reg));
				}
			}
		}
		candidates.Sort((a, b) => b.Hits - a.Hits);

		if (candidates.Count == 0 || candidates[0].Hits < MinProbeHits)
		{
			return null;
		}

		// 只保留命中数最高的若干组合里最好的那个；同一部件的 DB 链通常挂在同一个 REG 前缀下。
		return (candidates[0].Mount, candidates[0].Reg);
	}

	private static string[] GetProbePaths(string[] dbPaths)
	{
		// 链根路径不含 '/'，用它做挂点探针已经足够；没有链根时退化为最短的若干条。
		List<string> probes = new();
		foreach (string path in dbPaths)
		{
			if (!path.Contains('/'))
			{
				probes.Add(path);
			}
		}
		return probes.Count > 0 ? probes.ToArray() : dbPaths[..Math.Min(dbPaths.Length, 64)];
	}

	private static BoneLayout GetBoneLayout(PathChecksumCache cache, string partName)
	{
		if (s_dbPathCache.TryGetValue(partName, out BoneLayout? cached))
		{
			return cached;
		}

		BoneLayout empty = new();
		AssetCollection? collection = cache.FindCollectionByBundleFileName($"{partName}.prefab");
		if (collection is null)
		{
			s_dbPathCache[partName] = empty;
			return empty;
		}

		IGameObject? root = FindRootGameObject(collection, partName);
		if (root is null)
		{
			s_dbPathCache[partName] = empty;
			return empty;
		}

		ITransform? rootTransform = root.GetTransform();
		if (rootTransform is null)
		{
			s_dbPathCache[partName] = empty;
			return empty;
		}

		ITransform? dbTransform = FindContainer(rootTransform, "__DB");
		ITransform? regTransform = FindContainer(rootTransform, "__REG");

		List<string> dbPaths = new();
		List<string> regPaths = new();
		if (dbTransform is not null)
		{
			CollectBonePaths(dbTransform.GameObject_C4P, string.Empty, dbPaths, 0);
		}
		if (regTransform is not null)
		{
			// REG 子树会整体移植到挂点，链上每一级都可能成为 DB 链的父节点。
			CollectAllPaths(regTransform.GameObject_C4P, string.Empty, regPaths, 0);
		}
		if (dbPaths.Count == 0 && regPaths.Count == 0)
		{
			// 少数预制体没有 __DB/__REG 容器：从根的直接子级开始收集骨骼节点。
			CollectBonePaths(root, string.Empty, dbPaths, 0);
			CollectAllPaths(root, string.Empty, regPaths, 0);
		}

		BoneLayout layout = new()
		{
			DbPaths = dbPaths.ToArray(),
			DbRoots = GetProbePaths(dbPaths.ToArray()),
			RegPaths = regPaths.ToArray(),
		};
		s_dbPathCache[partName] = layout;
		return layout;
	}

	private static ITransform? FindContainer(ITransform rootTransform, string containerName)
	{
		foreach (ITransform? child in rootTransform.Children_C4P)
		{
			IGameObject? childGameObject = child?.GameObject_C4P;
			if (childGameObject is not null && childGameObject.Name.ToString() == containerName)
			{
				return child;
			}
		}
		return null;
	}

	private static IGameObject? FindRootGameObject(AssetCollection collection, string partName)
	{
		IGameObject? fallback = null;
		foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
		{
			if (meta.ClassID != 1)
			{
				continue;
			}

			IGameObject? gameObject = collection.TryGetAssetOnly<IGameObject>(meta.PathID);
			if (gameObject is null || !gameObject.IsRoot())
			{
				continue;
			}

			if (gameObject.Name.ToString() == partName)
			{
				return gameObject;
			}
			fallback ??= gameObject;
		}
		return fallback;
	}

	private static void CollectBonePaths(IGameObject? gameObject, string parentPath, List<string> output, int depth)
	{
		if (gameObject is null || depth > MaxDepth)
		{
			return;
		}

		ITransform? transform = gameObject.GetTransform();
		if (transform is null)
		{
			return;
		}

		foreach (ITransform? childTransform in transform.Children_C4P)
		{
			IGameObject? child = childTransform?.GameObject_C4P;
			if (child is null)
			{
				continue;
			}

			string name = child.Name.ToString();
			string path = parentPath.Length == 0 ? name : $"{parentPath}/{name}";
			// __REG 下是常规蒙皮骨骼，__DB 下是布料动态骨骼，两者都可能出现在运行时路径里。
			if (name.StartsWith("DB_", StringComparison.Ordinal) || name.StartsWith("REG_", StringComparison.Ordinal))
			{
				output.Add(path);
			}
			CollectBonePaths(child, path, output, depth + 1);
		}
	}

	private static void CollectAllPaths(IGameObject? gameObject, string parentPath, List<string> output, int depth)
	{
		if (gameObject is null || depth > MaxDepth)
		{
			return;
		}

		ITransform? transform = gameObject.GetTransform();
		if (transform is null)
		{
			return;
		}

		foreach (ITransform? childTransform in transform.Children_C4P)
		{
			IGameObject? child = childTransform?.GameObject_C4P;
			if (child is null)
			{
				continue;
			}

			string name = child.Name.ToString();
			// __REG / 无容器情况下网格节点也可能夹在骨骼链里，全收才能拼出完整运行时路径。
			if (name.Length != 0 && !name.StartsWith("__", StringComparison.Ordinal))
			{
				string path = parentPath.Length == 0 ? name : $"{parentPath}/{name}";
				output.Add(path);
				CollectAllPaths(child, path, output, depth + 1);
			}
		}
	}
}
