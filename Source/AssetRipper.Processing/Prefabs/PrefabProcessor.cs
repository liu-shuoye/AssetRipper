using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Extensions;
using System.Diagnostics;

namespace AssetRipper.Processing.Prefabs;

public sealed class PrefabProcessor : IAssetProcessor
{
	public void Process(GameData gameData)
	{
		ProcessedBundle processedBundle = gameData.GameBundle.AddNewProcessedBundle("Generated Hierarchy Assets");
		ProcessedAssetCollection prefabHierarchyCollection = processedBundle.AddNewProcessedCollection("Prefab Hierarchies", gameData.ProjectVersion);
		ProcessedAssetCollection prefabInstanceCollection = processedBundle.AddNewProcessedCollection("Generated Prefabs", gameData.ProjectVersion);
		Dictionary<SceneDefinition, ProcessedAssetCollection> sceneCollectionDictionary = new();

		AddMissingTransforms(gameData, processedBundle, sceneCollectionDictionary);

		HashSet<IGameObject> gameObjectsAlreadyProcessed = new();
		

		// 创建场景层级
		foreach (SceneDefinition scene in gameData.GameBundle.Scenes.ToList())
		{
			ProcessedAssetCollection sceneCollection = GetOrCreateSceneCollection(gameData, processedBundle, sceneCollectionDictionary, scene);
			SceneHierarchyObject sceneHierarchy = SceneHierarchyObject.Create(sceneCollection, scene);
			gameObjectsAlreadyProcessed.AddRange(sceneHierarchy.GameObjects);

			Bundle? bundle = scene.Collections.Select(c => c.Bundle).FirstOrDefault(b => b is SerializedBundle);
			if (bundle is not null)
			{
				// 用元数据枚举找到第一个 IAssetBundle (ClassID 142)，避免 FetchAssets 触发全量反序列化
				IAssetBundle? assetBundleAsset = null;
				foreach (AssetCollection c in bundle.FetchAssetCollections())
				{
					if (assetBundleAsset is not null)
					{
						break;
					}

					foreach (AssetCollection.AssetMetadata meta in c.EnumerateAssetMetadata())
					{
						if (meta.ClassID != (int)ClassIDType.AssetBundle)
						{
							continue;
						}

						assetBundleAsset = c.TryGetAssetOnly<IAssetBundle>(meta.PathID);
						if (assetBundleAsset is not null)
						{
							break;
						}
					}
				}

				if (assetBundleAsset is not null)
				{
					Debug.Assert(!assetBundleAsset.Has_IsStreamedSceneAssetBundle() || assetBundleAsset.IsStreamedSceneAssetBundle);
					sceneHierarchy.AssetBundleName = assetBundleAsset.GetAssetBundleName();
				}
				else
				{
					sceneHierarchy.AssetBundleName = bundle.Name;
				}
			}
		}

		// 使用现有的 PrefabInstance 创建预制件的层级结构
		// 用元数据枚举找到所有 IPrefabInstance (ClassID 1001)，避免 FetchAssets 触发全量反序列化
		foreach (IPrefabInstance prefab in gameData.EnumerateAssetsByClassID<IPrefabInstance>((int)ClassIDType.PrefabInstance))
		{
			if (prefab.RootGameObjectP is { } root && !gameObjectsAlreadyProcessed.Contains(root))
			{
				prefab.SetPrefabInternal();

				PrefabHierarchyObject prefabHierarchy = PrefabHierarchyObject.Create(prefabHierarchyCollection, root, prefab);
				gameObjectsAlreadyProcessed.AddRange(prefabHierarchy.GameObjects);
			}
		}
		// TODO 这里加载了所有资源？
		// 为不存在的预制件实例创建层级关系
		// 用元数据枚举找到所有 IGameObject (ClassID 1)，避免 FetchAssets 触发全量反序列化
		foreach (IGameObject asset in gameData.EnumerateAssetsByClassID<IGameObject>((int)ClassIDType.GameObject))
		{
			if (gameObjectsAlreadyProcessed.Contains(asset))
			{
				continue;
			}

			IGameObject root = asset.GetRoot();
			if (gameObjectsAlreadyProcessed.Add(root))
			{
				IPrefabInstance prefab = root.CreatePrefabForRoot(prefabInstanceCollection);

				PrefabHierarchyObject prefabHierarchy = PrefabHierarchyObject.Create(prefabHierarchyCollection, root, prefab);
				gameObjectsAlreadyProcessed.AddRange(prefabHierarchy.GameObjects);
			}
		}
	}


	private static void AddMissingTransforms(GameData gameData, ProcessedBundle processedBundle, Dictionary<SceneDefinition, ProcessedAssetCollection> sceneCollectionDictionary)
	{
		ProcessedAssetCollection missingPrefabTransformCollection = processedBundle.AddNewProcessedCollection("Missing Prefab Transforms", gameData.ProjectVersion);
		// 用元数据枚举找到所有 IGameObject (ClassID 1)，避免 FetchAssets 触发全量反序列化
		foreach (IGameObject gameObject in gameData.EnumerateAssetsByClassID<IGameObject>((int)ClassIDType.GameObject).Where(HasNoTransform))
		{
			Logger.Warning(LogCategory.Processing, $"游戏对象 {gameObject.Name} 没有 Transform。正在添加一个。");

			ProcessedAssetCollection collection;
			if (gameObject.Collection.IsScene)
			{
				SceneDefinition scene = gameObject.Collection.Scene;
				collection = GetOrCreateSceneCollection(gameData, processedBundle, sceneCollectionDictionary, scene);
			}
			else
			{
				collection = missingPrefabTransformCollection;
			}

			ITransform transform = collection.CreateTransform();

			transform.InitializeDefault();

			transform.GameObject_C4P = gameObject;
			gameObject.AddComponent(ClassIDType.Transform, transform);
		}
	}

	private static ProcessedAssetCollection GetOrCreateSceneCollection(GameData gameData, ProcessedBundle processedBundle, Dictionary<SceneDefinition, ProcessedAssetCollection> sceneCollectionDictionary, SceneDefinition scene)
	{
		ProcessedAssetCollection collection;
		if (sceneCollectionDictionary.TryGetValue(scene, out ProcessedAssetCollection? sceneCollection))
		{
			collection = sceneCollection;
		}
		else
		{
			collection = processedBundle.AddNewProcessedCollection(scene.Name + " (Generated Assets)", gameData.ProjectVersion);
			scene.AddCollection(collection);
			sceneCollectionDictionary.Add(scene, collection);
		}

		return collection;
	}

	private static bool HasNoTransform(IGameObject gameObject)
	{
		return !gameObject.TryGetComponent<ITransform>(out _);
	}
}
