using AssetRipper.Assets;
using AssetRipper.Assets.Metadata;
using AssetRipper.SourceGenerated.Subclasses.SceneObjectIdentifier;
using AssetRipper.Yaml;

namespace AssetRipper.Export.UnityProjects.Project;

public sealed class ProjectYamlWalker : YamlWalker
{
	private readonly IExportContainer container;

	public ProjectYamlWalker(IExportContainer container)
	{
		this.container = container;
		WithUnityVersion(container.ExportVersion);
	}

	public IUnityObjectBase CurrentAsset { get; set; } = null!;

	public YamlDocument ExportYamlDocument(IUnityObjectBase asset)
	{
		CurrentAsset = asset;
		// 导出阶段按需转换：在 WalkEditor 写出编辑器字段之前，对单个资产执行非破坏性 EditorFormat 转换。
		// 这样 Process 阶段无需反序列化全部资产做 Convert，只对真正导出（未被去重跳过）的资产做转换。
		// 幂等：多次调用安全；为 null 时表示调用方未启用延迟转换。
		container.EditorFormatConverter?.Invoke(asset);
		return ExportYamlDocument(asset, container.GetExportID(asset));
	}

	public YamlNode ExportYamlNode(IUnityObjectBase asset)
	{
		CurrentAsset = asset;
		return base.ExportYamlNode(asset);
	}

	public override bool EnterAsset(IUnityAssetBase asset)
	{
		if (asset is SceneObjectIdentifier sceneObjectIdentifier)
		{
			long targetObject = sceneObjectIdentifier.TargetObjectReference is not null
				? container.CreateExportPointer(sceneObjectIdentifier.TargetObjectReference).FileID
				: sceneObjectIdentifier.TargetObject;
			long targetPrefab = sceneObjectIdentifier.TargetPrefabReference is not null
				? container.CreateExportPointer(sceneObjectIdentifier.TargetPrefabReference).FileID
				: sceneObjectIdentifier.TargetPrefab;
			YamlMappingNode yamlMappingNode = new() { { YamlScalarNode.Create("targetObject"), targetObject }, { YamlScalarNode.Create("targetPrefab"), targetPrefab }, };
			AddNode(yamlMappingNode);
			return false;
		}
		else
		{
			return base.EnterAsset(asset);
		}
	}

	public override YamlNode CreateYamlNodeForPPtr<TAsset>(PPtr<TAsset> pptr)
	{
		if (pptr.PathID == 0)
		{
			return MetaPtr.NullPtr.ExportYaml(container.ExportVersion);
		}
		else if (CurrentAsset.Collection.TryGetAssetOnly(pptr.PathID, out TAsset? asset))
		{
			return container.CreateExportPointer(asset).ExportYaml(container.ExportVersion);
		}
		else
		{
			AssetType assetType = container.ToExportType(typeof(TAsset));
			MetaPtr pointer = MetaPtr.CreateMissingReference(GetClassID(typeof(TAsset)), assetType);
			return pointer.ExportYaml(container.ExportVersion);
		}
	}
}
