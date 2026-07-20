using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;

namespace AssetRipper.Assets.Collections;

/// <summary>
/// A collection of assets read from a <see cref="SerializedFile"/>.
/// </summary>
/// <remarks>
/// 此实现采用按需反序列化：加载阶段仅保留 <see cref="SerializedFile"/> 与 <see cref="AssetFactoryBase"/> 引用，
/// 不立即反序列化对象。首次访问资产时（通过 <see cref="TryGetAsset"/>、<see cref="GetEnumerator"/> 或 <see cref="Count"/>）
/// 才触发 <see cref="EnsureAssetsLoaded"/> 把全部对象反序列化到 assets 字典。
/// 这样加载阶段的内存峰值从"全部反序列化对象图"降到"仅元数据 + 文件流引用"。
/// </remarks>
public sealed class SerializedAssetCollection : AssetCollection
{
	private FileIdentifier[]? DependencyIdentifiers { get; set; }

	/// <summary>
	/// 懒加载所需的数据源引用。加载阶段保留，首次访问时用于反序列化。
	/// <see cref="UnloadAssets"/> 后这些引用仍然保留，以便再次访问时重新反序列化。
	/// </summary>
	private SerializedFile? _sourceFile;
	private AssetFactoryBase? _factory;
	private bool _assetsLoaded;

	private SerializedAssetCollection(Bundle bundle) : base(bundle)
	{
	}

	internal void InitializeDependencyList(IDependencyProvider? dependencyProvider)
	{
		if (Dependencies.Count > 1)
		{
			throw new Exception("Dependency list has already been initialized.");
		}
		if (DependencyIdentifiers is not null)
		{
			for (int i = 0; i < DependencyIdentifiers.Length; i++)
			{
				FileIdentifier identifier = DependencyIdentifiers[i];
				AssetCollection? dependency = Bundle.ResolveCollection(identifier);
				if (dependency is null)
				{
					dependencyProvider?.ReportMissingDependency(identifier);
				}
				SetDependency(i + 1, dependency);
			}
			DependencyIdentifiers = null;
		}
	}

	/// <summary>
	/// Creates a <see cref="SerializedAssetCollection"/> from a <see cref="SerializedFile"/>.
	/// </summary>
	/// <remarks>
	/// The new <see cref="SerializedAssetCollection"/> is automatically added to the <paramref name="bundle"/>.
	/// 此方法仅设置元数据与数据源引用，不立即反序列化对象。反序列化推迟到首次访问资产时。
	/// </remarks>
	/// <param name="bundle">The <see cref="Bundle"/> to add this collection to.</param>
	/// <param name="file">The <see cref="SerializedFile"/> from which to make this collection.</param>
	/// <param name="factory">A factory for creating assets.</param>
	/// <param name="defaultVersion">The default version to use if the file does not have a version, ie the version has been stripped.</param>
	/// <returns>The new collection.</returns>
	internal static SerializedAssetCollection FromSerializedFile(Bundle bundle, SerializedFile file, AssetFactoryBase factory, UnityVersion defaultVersion = default)
	{
		UnityVersion version = file.Version.Equals(0, 0, 0) ? defaultVersion : file.Version;
		SerializedAssetCollection collection = new SerializedAssetCollection(bundle)
		{
			Name = file.NameFixed,
			Version = version,
			OriginalVersion = version,
			Platform = file.Platform,
			Flags = file.Flags,
			EndianType = file.EndianType,
			// 保留数据源引用，不调用 ReadData，反序列化推迟到首次访问
			_sourceFile = file,
			_factory = factory,
		};
		ReadOnlySpan<FileIdentifier> fileDependencies = file.Dependencies;
		if (fileDependencies.Length > 0)
		{
			collection.DependencyIdentifiers = fileDependencies.ToArray();
		}
		return collection;
	}

	/// <summary>
	/// 首次访问时触发反序列化。把 <see cref="SerializedFile.Objects"/> 全部反序列化到 assets 字典。
	/// 后续访问直接查字典，不再重复反序列化，除非调用了 <see cref="UnloadAssets"/>。
	/// </summary>
	protected override void EnsureAssetsLoaded()
	{
		if (_assetsLoaded)
		{
			return;
		}
		_assetsLoaded = true;

		SerializedFile? file = _sourceFile;
		AssetFactoryBase? factory = _factory;
		if (file is null || factory is null)
		{
			return;
		}

		// 从 SerializedFile.Objects 按需反序列化所有对象。
		// ObjectInfo 是 struct，按值拷贝；其内部 _dataStream 是引用类型，
		// 拷贝仍指向同一个 SmartStream，LoadObjectData 能正确读取懒加载数据。
		ReadOnlySpan<ObjectInfo> objects = file.Objects;
		for (int i = 0; i < objects.Length; i++)
		{
			ObjectInfo objectInfo = objects[i];
			// 若该 PathID 已被 TryGetAssetOnly 提前反序列化加入字典，跳过避免重复添加（AddAsset 会抛重复键异常）
			if (assets.ContainsKey(objectInfo.FileID))
			{
				continue;
			}
			int classID = objectInfo.TypeID < 0 ? 114 : objectInfo.TypeID;
			AssetInfo assetInfo = new AssetInfo(this, objectInfo.FileID, classID);
			// LoadObjectData 从底层 SmartStream 按需读取，反序列化后即可被 GC 回收
			IUnityObjectBase? asset = factory.ReadAsset(assetInfo, objectInfo.LoadObjectData(), objectInfo.Type);
			if (asset is not null)
			{
				AddAsset(asset);
			}
		}
	}

	/// <summary>
	/// 直接遍历 <see cref="SerializedFile.Objects"/>（ObjectInfo struct 数组），不触发反序列化。
	/// ClassID 计算与 <see cref="EnsureAssetsLoaded"/> 中保持一致：TypeID &lt; 0 → 114（MonoBehaviour），否则用 TypeID。
	/// </summary>
	public override IEnumerable<AssetMetadata> EnumerateAssetMetadata()
	{
		SerializedFile? file = _sourceFile;
		if (file is null)
		{
			yield break;
		}
		// 不能把 ReadOnlySpan<ObjectInfo> 保存为局部变量，因为 ref struct 不能跨 yield 边界。
		// 每次循环通过索引访问 file.Objects[i]，由编译器在每次迭代中重新获取 span。
		int length = file.Objects.Length;
		for (int i = 0; i < length; i++)
		{
			ObjectInfo info = file.Objects[i];
			int classID = info.TypeID < 0 ? 114 : info.TypeID;
			yield return new AssetMetadata { PathID = info.FileID, ClassID = classID };
		}
	}

	/// <summary>
	/// 单对象反序列化：若已全量加载或此前已单对象反序列化过则直接走字典查询；否则从 ObjectInfo 数组按 PathID
	/// 查找并反序列化单个对象。反序列化后的对象加入 assets 字典避免重复反序列化，但 <see cref="_assetsLoaded"/>
	/// 保持 false，以保持懒加载状态兼容老代码（GetEnumerator 仍会触发全量加载）。
	/// </summary>
	public override IUnityObjectBase? TryGetAssetOnly(long pathID)
	{
		// 优先走字典：覆盖"已全量加载"和"此前已单对象反序列化"两种情况
		if (assets.TryGetValue(pathID, out IUnityObjectBase? existing))
		{
			return existing;
		}
		if (_assetsLoaded)
		{
			// 已全量加载但字典里没有，说明该 PathID 不存在
			return null;
		}

		SerializedFile? file = _sourceFile;
		AssetFactoryBase? factory = _factory;
		if (file is null || factory is null)
		{
			return null;
		}

		ReadOnlySpan<ObjectInfo> objects = file.Objects;
		for (int i = 0; i < objects.Length; i++)
		{
			ObjectInfo info = objects[i];
			if (info.FileID != pathID)
			{
				continue;
			}
			int classID = info.TypeID < 0 ? 114 : info.TypeID;
			AssetInfo assetInfo = new AssetInfo(this, info.FileID, classID);
			IUnityObjectBase? asset = factory.ReadAsset(assetInfo, info.LoadObjectData(), info.Type);
			if (asset is not null)
			{
				AddAsset(asset);
			}
			return asset;
		}
		return null;
	}

	/// <summary>
	/// 清空已反序列化的资产对象，但保留 <see cref="_sourceFile"/> 与 <see cref="_factory"/> 引用，
	/// 以便再次访问时通过 <see cref="EnsureAssetsLoaded"/> 重新反序列化。
	/// </summary>
	public override void UnloadAssets()
	{
		base.UnloadAssets();
		_assetsLoaded = false;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// DependencyIdentifiers 是加载阶段临时数据，加载完成后已不再需要
			DependencyIdentifiers = null;
			// 释放数据源引用：Dispose 后无法再懒加载
			_sourceFile = null;
			_factory = null;
			_assetsLoaded = true;
		}
		base.Dispose(disposing);
	}
}
