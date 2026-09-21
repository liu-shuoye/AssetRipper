using AssetRipper.Assets;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Processing.Prefabs;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_468431735;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.MarkerInterfaces;

namespace AssetRipper.Export.UnityProjects.Project;

public class PrefabExportCollection : AssetsExportCollection<IPrefabInstance>
{
	public PrefabExportCollection(IAssetExporter assetExporter, PrefabHierarchyObject prefabHierarchyObject)
		: base(assetExporter, prefabHierarchyObject.Prefab)
	{
		RootGameObject = prefabHierarchyObject.Root;
		Prefab = prefabHierarchyObject.Prefab;
		Hierarchy = prefabHierarchyObject;
		AddAssets(prefabHierarchyObject.Assets);
		AddAsset(prefabHierarchyObject);
	}

	protected override string GetExportExtension(IUnityObjectBase asset) => PrefabKeyword;

	public override TransferInstructionFlags Flags => base.Flags | TransferInstructionFlags.SerializeForPrefabSystem;
	public IGameObject RootGameObject { get; }
	public IPrefabInstance Prefab { get; }
	public PrefabHierarchyObject Hierarchy { get; }
	/// <summary>
	/// Prior to 2018.3, Prefab was an actual asset inside "*.prefab" files.
	/// After that, PrefabImporter and PrefabInstance were introduced as a replacement.
	/// </summary>
	public bool EmitPrefabAsset => Prefab is IPrefabMarker;
	public override string Name => RootGameObject.Name;

	protected override IUnityObjectBase CreateImporter(IExportContainer container)
	{
		if (EmitPrefabAsset)
		{
			return base.CreateImporter(container);
		}
		else
		{
			IPrefabImporter importer = PrefabImporter.Create(container.File, container.ExportVersion);
			if (RootGameObject.AssetBundleName is not null)
			{
				importer.AssetBundleName_R = RootGameObject.AssetBundleName;
			}
			return importer;
		}
	}

	public override IEnumerable<IUnityObjectBase> ExportableAssets
	{
		get
		{
			foreach (IUnityObjectBase asset in Hierarchy.ExportableAssets)
			{
				m_file = asset.Collection;
				yield return asset;
			}
		}
	}

	/// <summary>
	/// 写出 .prefab 前批量回补集合内 ParticleSystem 的曲线数据（占位模式），写完统一释放（用完即弃）。
	/// </summary>
	protected override bool ExportInner(IExportContainer container, string filePath, string dirPath, FileSystem fileSystem)
	{
		using ParticleSystemRestoreScope restore = ParticleSystemRestoreScope.Create(ExportableAssets);
		return base.ExportInner(container, filePath, dirPath, fileSystem);
	}

	/// <summary>
	/// Used for <see cref="IPrefabInstance.SourcePrefabP"/>
	/// </summary>
	/// <returns></returns>
	public MetaPtr GenerateMetaPtrForPrefab()
	{
		return new MetaPtr(
			ExportIdHandler.GetMainExportID((int)ClassIDType.PrefabInstance),
			GUID,
			EmitPrefabAsset ? AssetType.Serialized : AssetType.Meta);
	}

	public const string PrefabKeyword = "prefab";
}
