using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.Assets.Traversal;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.StreamingInfo;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace AssetRipper.Export.UnityProjects;

/// <summary>
/// Computes a stable 64-bit content hash for an <see cref="IUnityObjectBase"/> by walking
/// its serialized fields via <see cref="IUnityAssetBase.WalkRelease"/> (which excludes
/// editor-only fields such as Prefab pointer bookkeeping).
/// </summary>
/// <remarks>
/// <para>
/// PPtr references are dereferenced and the target asset's content hash is incorporated,
/// so that two assets pointing to content-equal (but distinct) targets produce the same
/// hash. Dereferencing is bounded by a depth limit and protected by a visiting set to
/// prevent infinite recursion and excessive memory use.
/// </para>
/// <para>
/// <see cref="IStreamingInfo"/> fields (streamed texture/mesh data) are read via
/// <see cref="StreamingInfoExtensions.GetContent"/> so that the actual byte content is
/// hashed rather than just the (path, offset, size) metadata.
/// </para>
/// </remarks>
internal sealed class ContentHashWalker : AssetWalker
{
	/// <summary>
	/// Sentinel returned when an asset cannot be hashed (e.g. a MonoBehaviour whose
	/// script data is still in SerializableStructure form, or a streamed resource that
	/// cannot be read). Such assets are treated as non-comparable and kept entirely.
	/// </summary>
	public const ulong Unhashable = ulong.MaxValue;

	/// <summary>Maximum recursion depth for PPtr dereferencing.</summary>
	private const int MaxDepth = 4;

	/// <summary>Hash returned for a cyclic PPtr reference (a pointer back to an asset already being hashed on the current recursion stack).</summary>
	private const ulong CyclicReference = 0;

	private readonly Dictionary<IUnityObjectBase, ulong> hashCache;
	private readonly HashSet<IUnityObjectBase> visiting;
	private readonly IUnityObjectBase rootAsset;
	private readonly int depth;
	private XxHash64 hasher = new(0);

	private ContentHashWalker(Dictionary<IUnityObjectBase, ulong> hashCache, HashSet<IUnityObjectBase> visiting, IUnityObjectBase rootAsset, int depth)
	{
		this.hashCache = hashCache;
		this.visiting = visiting;
		this.rootAsset = rootAsset;
		this.depth = depth;
	}

	/// <summary>
	/// Computes the content hash of <paramref name="asset"/>. Results are cached in
	/// <paramref name="hashCache"/> (shared across all calls in one deduplication run)
	/// so each asset is hashed at most once.
	/// </summary>
	public static ulong ComputeHash(IUnityObjectBase asset,
		Dictionary<IUnityObjectBase, ulong>? hashCache = null,
		HashSet<IUnityObjectBase>? visiting = null,
		int depth = 0)
	{
		hashCache ??= new();
		visiting ??= new();

		if (hashCache.TryGetValue(asset, out ulong cached))
		{
			return cached;
		}

		if (depth > MaxDepth)
		{
			return Unhashable;
		}

		if (!visiting.Add(asset))
		{
			return CyclicReference;
		}

		try
		{
			ContentHashWalker walker = new(hashCache, visiting, asset, depth);
			asset.WalkRelease(walker);
			ulong hash = walker.hasher.GetCurrentHashAsUInt64();
			hashCache[asset] = hash;
			return hash;
		}
		catch (NotSupportedException)
		{
			return Unhashable;
		}
		finally
		{
			visiting.Remove(asset);
		}
	}

	public override bool EnterAsset(IUnityAssetBase asset)
	{
		// StreamingInfo: hash the actual streamed byte content instead of (path, offset, size) metadata.
		if (asset is IStreamingInfo streamingInfo)
		{
			if (streamingInfo.IsSet() && rootAsset.Collection is AssetCollection collection)
			{
				byte[] content;
				try
				{
					content = streamingInfo.GetContent(collection);
				}
				catch
				{
					throw new NotSupportedException();
				}

				if (content.Length == 0)
				{
					// Streamed resource could not be read; cannot reliably hash this asset.
					throw new NotSupportedException();
				}

				hasher.Append(content);
			}

			return false; // Do not visit StreamingInfo child fields (path/offset/size).
		}

		return true;
	}

	public override void ExitAsset(IUnityAssetBase asset) { }
	public override void DivideAsset(IUnityAssetBase asset) { }

	public override bool EnterField(IUnityAssetBase asset, string name)
	{
		// Field names participate in the hash so that structurally different assets
		// with the same primitive values are not falsely considered equal.
		hasher.Append(Encoding.UTF8.GetBytes(name));
		return true;
	}

	public override void ExitField(IUnityAssetBase asset, string name) { }

	public override bool EnterList<T>(IReadOnlyList<T> list)
	{
		hasher.Append(MemoryMarshalAsBytes(list.Count));
		return true;
	}

	public override void ExitList<T>(IReadOnlyList<T> list) { }
	public override void DivideList<T>(IReadOnlyList<T> list) { }

	public override bool EnterDictionary<TKey, TValue>(IReadOnlyCollection<KeyValuePair<TKey, TValue>> dictionary)
	{
		hasher.Append(MemoryMarshalAsBytes(dictionary.Count));
		return true;
	}

	public override void ExitDictionary<TKey, TValue>(IReadOnlyCollection<KeyValuePair<TKey, TValue>> dictionary) { }
	public override void DivideDictionary<TKey, TValue>(IReadOnlyCollection<KeyValuePair<TKey, TValue>> dictionary) { }

	public override void VisitPrimitive<T>(T value)
	{
		switch (value)
		{
			case null:
				return;
			case byte[] bytes:
				hasher.Append(bytes);
				return;
			case string s:
				hasher.Append(Encoding.UTF8.GetBytes(s));
				return;
			default:
				int h = EqualityComparer<T>.Default.GetHashCode(value);
				Span<byte> buffer = stackalloc byte[sizeof(int)];
				BinaryPrimitives.WriteInt32LittleEndian(buffer, h);
				hasher.Append(buffer);
				return;
		}
	}

	public override void VisitPPtr<TAsset>(PPtr<TAsset> pptr)
	{
		IUnityObjectBase? target = rootAsset.Collection?.TryGetAssetOnly(pptr.FileID, pptr.PathID);
		if (target is null)
		{
			// Target not resolvable; fall back to hashing the pointer coordinates.
			HashPPtrCoordinates(pptr.FileID, pptr.PathID);
			return;
		}

		ulong targetHash = ComputeHash(target, hashCache, visiting, depth + 1);
		if (targetHash == Unhashable)
		{
			// Target too deep or unhashable; fall back to coordinates to avoid false duplicates.
			HashPPtrCoordinates(pptr.FileID, pptr.PathID);
		}
		else
		{
			Span<byte> buffer = stackalloc byte[sizeof(ulong)];
			BinaryPrimitives.WriteUInt64LittleEndian(buffer, targetHash);
			hasher.Append(buffer);
		}
	}

	private void HashPPtrCoordinates(int fileID, long pathID)
	{
		Span<byte> buffer = stackalloc byte[12];
		BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], fileID);
		BinaryPrimitives.WriteInt64LittleEndian(buffer[4..], pathID);
		hasher.Append(buffer);
	}

	private static ReadOnlySpan<byte> MemoryMarshalAsBytes<T>(T value) where T : struct
	{
		return System.Runtime.InteropServices.MemoryMarshal.AsBytes(
			System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref value, 1));
	}
}
