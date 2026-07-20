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

		//Create scene hierarchies
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
						if (meta.ClassID != 142)
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

		//Create hierarchies for prefabs with an existing PrefabInstance
		// 用元数据枚举找到所有 IPrefabInstance (ClassID 1001)，避免 FetchAssets 触发全量反序列化
		foreach (IPrefabInstance prefab in EnumerateAssetsByClassID<IPrefabInstance>(gameData.GameBundle, 1001))
		{
			if (prefab.RootGameObjectP is { } root && !gameObjectsAlreadyProcessed.Contains(root))
			{
				prefab.SetPrefabInternal();

				PrefabHierarchyObject prefabHierarchy = PrefabHierarchyObject.Create(prefabHierarchyCollection, root, prefab);
				gameObjectsAlreadyProcessed.AddRange(prefabHierarchy.GameObjects);
			}
		}

		//Create hierarchies for prefabs without an existing PrefabInstance
		// 用元数据枚举找到所有 IGameObject (ClassID 1)，避免 FetchAssets 触发全量反序列化
		foreach (IGameObject asset in EnumerateAssetsByClassID<IGameObject>(gameData.GameBundle, 1))
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

	/// <summary>
	/// 用元数据枚举遍历 bundle 中所有 AssetCollection，仅对指定 ClassID 的对象做单对象反序列化。
	/// 用于替代 FetchAssets().OfType&lt;T&gt;() 模式，避免触发全量反序列化。
	/// </summary>
	private static IEnumerable<T> EnumerateAssetsByClassID<T>(GameBundle bundle, int classID) where T : IUnityObjectBase
	{
		foreach (AssetCollection collection in bundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID != classID)
				{
					continue;
				}
				T? asset = collection.TryGetAssetOnly<T>(meta.PathID);
				if (asset is not null)
				{
					yield return asset;
				}
			}
		}
	}

	private static void AddMissingTransforms(GameData gameData, ProcessedBundle processedBundle, Dictionary<SceneDefinition, ProcessedAssetCollection> sceneCollectionDictionary)
	{
		ProcessedAssetCollection missingPrefabTransformCollection = processedBundle.AddNewProcessedCollection("Missing Prefab Transforms", gameData.ProjectVersion);
		// 用元数据枚举找到所有 IGameObject (ClassID 1)，避免 FetchAssets 触发全量反序列化
		foreach (IGameObject gameObject in EnumerateAssetsByClassID<IGameObject>(gameData.GameBundle, 1).Where(HasNoTransform))
		{
			Logger.Warning(LogCategory.Processing, $"GameObject {gameObject.Name} has no Transform. Adding one.");

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
