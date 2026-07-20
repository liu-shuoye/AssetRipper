using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files;
using AssetRipper.SourceGenerated.Classes.ClassID_1045;
using AssetRipper.SourceGenerated.Classes.ClassID_141;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_29;
using AssetRipper.SourceGenerated.Classes.ClassID_3;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.AssetInfo;
using AssetRipper.SourceGenerated.Subclasses.Scene;
using System.Diagnostics;

namespace AssetRipper.Processing.Scenes;

public sealed class SceneDefinitionProcessor : IAssetProcessor
{
	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "Creating Scene Definitions");
		IBuildSettings? buildSettings = null;
		HashSet<AssetCollection> sceneCollections = new();
		Dictionary<AssetCollection, string> scenePaths = new();
		Dictionary<AssetCollection, UnityGuid> sceneGuids = new();
		List<IAssetBundle> sceneAssetBundles = new();

		//Find the relevant assets in this single pass over all the assets.
		// 用元数据枚举避免触发 EnsureAssetsLoaded 全量反序列化（30 万对象）。
		// 仅对真正需要访问字段的 IOcclusionCullingSettings / IBuildSettings / IAssetBundle 调用 TryGetAssetOnly。
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				// ILevelGameManager 对应 ClassID 29 / 104 / 157 / 196（参见 ClassIDTypeExtention.IsSceneSettings）
				if (meta.ClassID is 29 or 104 or 157 or 196)
				{
					sceneCollections.Add(collection);
					// 仅 OcclusionCullingSettings (29) 需要 SceneGUID 字段
					if (meta.ClassID == 29)
					{
						IOcclusionCullingSettings? sceneSettings = collection.TryGetAssetOnly<IOcclusionCullingSettings>(meta.PathID);
						if (sceneSettings is not null && sceneSettings.Has_SceneGUID())
						{
							sceneGuids[collection] = sceneSettings.SceneGUID;
						}
					}
				}
				else if (meta.ClassID == 141) // IBuildSettings
				{
					buildSettings = collection.TryGetAssetOnly<IBuildSettings>(meta.PathID);
				}
				else if (meta.ClassID == 142) // IAssetBundle
				{
					IAssetBundle? assetBundle = collection.TryGetAssetOnly<IAssetBundle>(meta.PathID);
					if (assetBundle is not null && assetBundle.IsStreamedSceneAssetBundle)
					{
						sceneAssetBundles.Add(assetBundle);
					}
				}
			}
		}

		//Currently, these paths are treated as lower precedent than paths defined in asset bundles, but they should never conflict.
		foreach (AssetCollection sceneCollection in sceneCollections)
		{
			if (SceneHelpers.TryGetScenePath(sceneCollection, buildSettings, out string? scenePath))
			{
				scenePaths[sceneCollection] = scenePath;
			}
		}

		//Extract scene paths from asset bundles.
		foreach (IAssetBundle assetBundleAsset in sceneAssetBundles)
		{
			Bundle bundle = assetBundleAsset.Collection.Bundle;
			if (bundle is not SerializedBundle)
			{
				Logger.Log(LogType.Warning, LogCategory.Processing, $"Scene name recovery is not supported for bundles of type {bundle.GetType().Name}");
			}
			else if (assetBundleAsset.Has_SceneHashes() && assetBundleAsset.SceneHashes.Count > 0)
			{
				foreach ((Utf8String scenePath, Utf8String collectionName) in assetBundleAsset.SceneHashes)
				{
					string path = Path.ChangeExtension(scenePath, null);
					path = OriginalPathHelper.EnsurePathNotRooted(path);
					path = OriginalPathHelper.EnsureStartsWithAssets(path);

					string name = SpecialFileNames.FixFileIdentifier(collectionName);

					AssetCollection sceneCollection = bundle.Collections.First(collection => collection.Name == name);
					sceneCollections.Add(sceneCollection);//Just to be safe
					scenePaths[sceneCollection] = path;
				}
			}
			else
			{
				int startingIndex = 0;
				foreach (AccessPairBase<Utf8String, IAssetInfo> pair in assetBundleAsset.Container)
				{
					Debug.Assert(pair.Value.Asset.IsNull(), "Scene pointer is not null");

					string path = Path.ChangeExtension(pair.Key.String, null);
					path = OriginalPathHelper.EnsurePathNotRooted(path);
					path = OriginalPathHelper.EnsureStartsWithAssets(path);
					int index = IndexOf(bundle.Collections, sceneCollections, startingIndex);
					if (index < 0)
					{
						throw new Exception($"Scene collection not found in {bundle.Name} at or after index {startingIndex}");
					}

					AssetCollection sceneCollection = bundle.Collections[index];
					sceneCollections.Add(sceneCollection);//Just to be safe
					scenePaths[sceneCollection] = path;
					startingIndex = index + 1;
				}
			}
		}

		//Make the scene definitions
		List<SceneDefinition> sceneDefinitions = new();
		foreach (AssetCollection sceneCollection in sceneCollections)
		{
			SceneDefinition sceneDefinition;
			UnityGuid guid = sceneGuids.TryGetValue(sceneCollection, out UnityGuid sceneGuid) ? sceneGuid : default;
			if (scenePaths.TryGetValue(sceneCollection, out string? path))
			{
				sceneDefinition = SceneDefinition.FromPath(path, guid);
			}
			else
			{
				sceneDefinition = SceneDefinition.FromName(sceneCollection.Name, guid);
			}
			sceneDefinition.AddCollection(sceneCollection);
			sceneDefinitions.Add(sceneDefinition);
		}

		//Generate settings for the project
		{
			ProcessedAssetCollection processedCollection = gameData.AddNewProcessedCollection("Generated Settings");

			if (buildSettings is not null)
			{
				IEditorBuildSettings editorBuildSettings = processedCollection.CreateEditorBuildSettings();
				{
					int numScenes = buildSettings.Scenes.Count;
					editorBuildSettings.Scenes.Capacity = numScenes;
					for (int i = 0; i < numScenes; i++)
					{
						IScene scene = editorBuildSettings.Scenes.AddNew();
						scene.Enabled = true;
						scene.Path = buildSettings.Scenes[i];
						//Guid gets handled later.
					}
				}
			}

			//EditorSettings
			//Is this the best place to create this? It doesn't have anything to do with scenes.
			processedCollection.CreateEditorSettings()
				.SetToDefaults();
		}
	}

	private static int IndexOf(IReadOnlyList<AssetCollection> list, HashSet<AssetCollection> containingSet, int startingIndex)
	{
		for (int i = startingIndex; i < list.Count; i++)
		{
			if (containingSet.Contains(list[i]))
			{
				return i;
			}
		}
		return -1;
	}
}
