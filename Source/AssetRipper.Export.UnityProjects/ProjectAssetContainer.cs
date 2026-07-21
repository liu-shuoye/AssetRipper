using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Configuration;
using AssetRipper.Processing.Scenes;
using AssetRipper.SourceGenerated.Classes.ClassID_141;
using System.Diagnostics;


namespace AssetRipper.Export.UnityProjects;

public class ProjectAssetContainer : IExportContainer
{
	public ProjectAssetContainer(ProjectExporter exporter, CoreConfiguration options, IEnumerable<IUnityObjectBase> assets,
		IReadOnlyList<IExportCollection> collections, HashSet<IExportCollection> skippedCollections,
		Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap)
	{
		m_exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
		m_skippedCollections = skippedCollections ?? throw new ArgumentNullException(nameof(skippedCollections));
		m_redirectMap = redirectMap ?? throw new ArgumentNullException(nameof(redirectMap));
		CurrentCollection = null!;

		ExportVersion = options.Version;

		m_buildSettings = assets.OfType<IBuildSettings>().FirstOrDefault();

		List<SceneExportCollection> scenes = new();
		foreach (IExportCollection collection in collections)
		{
			// 被跳过的集合不会被添加到 m_assetCollections 中。
			// 它们的资源将在查询时通过 m_redirectMap 进行重定向（任务 3）。
			if (m_skippedCollections.Contains(collection))
			{
				if (collection is SceneExportCollection skippedScene)
				{
					scenes.Add(skippedScene);
				}
				continue;
			}

			foreach (IUnityObjectBase asset in collection.Assets)
			{
				CheckIfAlreadyAdded(this, asset, collection);
				m_assetCollections.Add(asset, collection);
			}
			if (collection is SceneExportCollection scene)
			{
				scenes.Add(scene);
			}
		}
		m_scenes = scenes.ToArray();

		//检查资源是否已经添加到容器中。
		[Conditional("DEBUG")]
		static void CheckIfAlreadyAdded(ProjectAssetContainer container, IUnityObjectBase asset, IExportCollection currentCollection)
		{
			if (container.m_assetCollections.TryGetValue(asset, out IExportCollection? previousCollection))
			{
				throw new ArgumentException($"Asset {asset} is already added by {previousCollection}");
			}
		}
	}

	public long GetExportID(IUnityObjectBase asset)
	{
		if (m_redirectMap.TryGetValue(asset, out IUnityObjectBase? target))
		{
			return GetExportID(target);
		}

		if (m_assetCollections.TryGetValue(asset, out IExportCollection? collection))
		{
			return collection.GetExportID(this, asset);
		}

		return ExportIdHandler.GetMainExportID(asset);
	}

	public AssetType ToExportType(Type type)
	{
		return m_exporter.ToExportType(type);
	}

	public MetaPtr CreateExportPointer(IUnityObjectBase asset)
	{
		if (m_redirectMap.TryGetValue(asset, out IUnityObjectBase? target))
		{
			return CreateExportPointer(target);
		}

		if (m_assetCollections.TryGetValue(asset, out IExportCollection? collection))
		{
			return collection.CreateExportPointer(this, asset, collection == CurrentCollection);
		}

		return MetaPtr.CreateMissingReference(asset.ClassID, AssetType.Meta);
	}

	public UnityGuid ScenePathToGUID(string path)
	{
		foreach (SceneExportCollection scene in m_scenes)
		{
			if (scene.Scene.Path == path)
			{
				return scene.GUID;
			}
		}
		return default;
	}

	public bool IsSceneDuplicate(int sceneIndex) => SceneHelpers.IsSceneDuplicate(sceneIndex, m_buildSettings);

	public IExportCollection CurrentCollection { get; set; }
	public AssetCollection File => CurrentCollection.File;
	public UnityVersion ExportVersion { get; }

	private readonly ProjectExporter m_exporter;
	private readonly Dictionary<IUnityObjectBase, IExportCollection> m_assetCollections = new();
	private readonly HashSet<IExportCollection> m_skippedCollections;
	private readonly Dictionary<IUnityObjectBase, IUnityObjectBase> m_redirectMap;

	private readonly IBuildSettings? m_buildSettings;
	private readonly SceneExportCollection[] m_scenes;
}
