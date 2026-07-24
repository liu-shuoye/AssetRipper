using AssetRipper.Assets;
using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Files.BundleFiles;
using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.SourceGenerated.Classes.ClassID_0;
using AssetRipper.SourceGenerated.Classes.ClassID_1032;
using AssetRipper.SourceGenerated.Classes.ClassID_104;
using AssetRipper.SourceGenerated.Classes.ClassID_108;
using AssetRipper.SourceGenerated.Classes.ClassID_1120;
using AssetRipper.SourceGenerated.Classes.ClassID_157;
using AssetRipper.SourceGenerated.Classes.ClassID_218;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_258;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_33;
using AssetRipper.SourceGenerated.Classes.ClassID_850595691;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.LightmapData;
using AssetRipper.SourceGenerated.Subclasses.RendererData;
using AssetRipper.SourceGenerated.Subclasses.SceneObjectIdentifier;

namespace AssetRipper.Processing;

public class LightingDataProcessor : IAssetProcessor
{
	/// <summary>
	/// 新创建的 <see cref="ILightingDataAsset"/> 的默认名称。
	/// </summary>
	private static Utf8String LightingDataName { get; } = new("LightingData");

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "照明数据资产");
		ProcessedAssetCollection processedCollection = gameData.AddNewProcessedCollection("Generated Lighting Data Assets");

		Dictionary<ILightmapSettings, SceneDefinition> lightmapSettingsDictionary = new();
		Dictionary<ILightProbes, SceneDefinition?> lightProbeDictionary = new();
		Dictionary<ILightingSettings, SceneDefinition?> lightingSettingsDictionary = new();

		foreach (SceneDefinition scene in gameData.GameBundle.Scenes)
		{
			// 只有场景才能包含 LightmapSettings 资产。
			ILightmapSettings? lightmapSettings = scene.Assets.OfType<ILightmapSettings>().FirstOrDefault();
			if (lightmapSettings is null)
			{
				continue;
			}

			IRenderSettings? renderSettings = scene.Assets.OfType<IRenderSettings>().FirstOrDefault();
			if (renderSettings is null)
			{
				// 这应该永远不会发生。所有场景都需要一个 RenderSettings 资产。
				continue;
			}

			lightmapSettingsDictionary.Add(lightmapSettings, scene);

			if (lightmapSettings.LightProbesP is { } lightProbes && !lightProbeDictionary.TryAdd(lightProbes, scene))
			{
				//这个光探针集在场景之间共享。
				lightProbeDictionary[lightProbes] = null;
			}

			if (lightmapSettings.LightingSettingsP is { } lightingSettings && !lightingSettingsDictionary.TryAdd(lightingSettings, scene))
			{
				//这个 LightingSettings 在场景之间共享。
				lightingSettingsDictionary[lightingSettings] = null;
			}

			if (!lightmapSettings.Has_LightingDataAsset())
			{
				continue;
			}

			ILightingDataAsset lightingDataAsset = processedCollection.CreateLightingDataAsset();

			lightmapSettings.LightingDataAssetP = lightingDataAsset;

			PPtrConverter converter = new PPtrConverter(lightmapSettings, lightingDataAsset);

			lightingDataAsset.LightmapsMode = lightmapSettings.LightmapsMode;
			lightingDataAsset.EnlightenData = CreateEnlightenData(lightingDataAsset.Collection.Version);

			SetEnlightenSceneMapping(lightingDataAsset, lightmapSettings, converter);
			SetBakedAmbientProbes(lightingDataAsset, renderSettings);
			AddSkyboxReflection(lightingDataAsset, renderSettings);
			SetLightmaps(lightingDataAsset, lightmapSettings.Lightmaps, converter);
			SetScene(lightingDataAsset, scene, processedCollection);
			SetLightProbes(lightingDataAsset, lightmapSettings);
			SetEnlightenDataVersion(lightingDataAsset);

			// TODO 内存击穿?
			foreach (IUnityObjectBase asset in scene.Assets)
			{
				switch (asset)
				{
					case IRenderer renderer:
						AddRenderer(lightingDataAsset, renderer);
						break;
					case ITerrain terrain:
						AddTerrain(lightingDataAsset, terrain);
						break;
					case ILight light:
						AddLight(lightingDataAsset, light);
						break;
				}
			}
		}

		foreach ((ILightmapSettings lightmapSettings, SceneDefinition scene) in lightmapSettingsDictionary)
		{
			ILightProbes? lightProbes = lightmapSettings.LightProbesP;
			if (lightProbes is not null && lightProbeDictionary[lightProbes] is null)
			{
				lightProbes = null; //共享光探针不应设置其路径。
			}

			ILightingSettings? lightingSettings = lightmapSettings.LightingSettingsP;
			if (lightingSettings is not null && lightingSettingsDictionary[lightingSettings] is null)
			{
				lightingSettings = null; //共享光设置不应设置其路径。
			}

			SetPathsAndMainAsset(lightmapSettings, lightProbes, lightingSettings, scene);
		}
	}

	private static void AddRenderer(ILightingDataAsset lightingDataAsset, IRenderer renderer)
	{
		//-1 表示它不属于光照贴图的一部分。
		ushort lightmapIndex = renderer.GetLightmapIndex();
		if (lightmapIndex != ushort.MaxValue) // || renderer.LightmapIndexDynamic_C25 != ushort.MaxValue)
		{
			//Scene object identifiers for the renderer associated with each value in the lightmapped renderer data array
			SceneObjectIdentifier identifier = lightingDataAsset.LightmappedRendererDataIDs.AddNew();
			identifier.TargetObjectReference = renderer;

			//The lightmap index, lightmap uv scale/offset value, etc
			IRendererData rendererData = lightingDataAsset.LightmappedRendererData.AddNew();
			rendererData.LightmapIndex = lightmapIndex;

			//This seems to crash the editor when it's not set to -1.
			//See: https://github.com/AssetRipper/AssetRipper/issues/811
			rendererData.LightmapIndexDynamic = ushort.MaxValue; //renderer.LightmapIndexDynamic_C25;

			rendererData.LightmapST.CopyValues(renderer.LightmapTilingOffset_C25);
			rendererData.LightmapSTDynamic.CopyValues(renderer.LightmapTilingOffsetDynamic_C25);
			rendererData.UvMesh.SetAsset(lightingDataAsset.Collection, renderer.GameObject_C25P?.TryGetComponent<IMeshFilter>()?.MeshP);
		}
		else
		{
			// No lightmap data associated with the renderer
		}
	}

	private static void AddTerrain(ILightingDataAsset lightingDataAsset, ITerrain terrain)
	{
		//-1 表示它不属于光照贴图的一部分。
		if (terrain.LightmapIndex != ushort.MaxValue) // || terrain.LightmapIndexDynamic != ushort.MaxValue)
		{
			//Scene object identifiers for the terrain associated with each value in the lightmapped renderer data array
			SceneObjectIdentifier identifier = lightingDataAsset.LightmappedRendererDataIDs.AddNew();
			identifier.TargetObjectReference = terrain;

			//The lightmap index, lightmap uv scale/offset value, etc
			IRendererData rendererData = lightingDataAsset.LightmappedRendererData.AddNew();
			rendererData.LightmapIndex = terrain.LightmapIndex;

			//This seems to crash the editor when it's not set to -1.
			//See: https://github.com/AssetRipper/AssetRipper/issues/811
			rendererData.LightmapIndexDynamic = ushort.MaxValue; //terrain.LightmapIndexDynamic;

			rendererData.LightmapST.CopyValues(terrain.LightmapTilingOffset);
			rendererData.LightmapSTDynamic.CopyValues(terrain.LightmapTilingOffsetDynamic);

			rendererData.TerrainDynamicUVST.CopyValues(terrain.DynamicUVST);
			rendererData.TerrainChunkDynamicUVST.CopyValues(terrain.ChunkDynamicUVST);
			rendererData.ExplicitProbeSetHash?.CopyValues(terrain.ExplicitProbeSetHash);
		}
		else
		{
			// No lightmap data associated with the terrain
		}
	}

	private static void AddLight(ILightingDataAsset lightingDataAsset, ILight light)
	{
		// 我们不确定最合适的检查灯光是否属于这些数组的方法是什么，
		// 在这些数组中或不是，但只是包含所有它们也没有害处。

		SceneObjectIdentifier identifier = lightingDataAsset.Lights.AddNew();
		identifier.TargetObjectReference = light;

		//Information about whether a light is baked or not
		if (light.Has_BakingOutput())
		{
			lightingDataAsset.LightBakingOutputs?.AddNew().CopyValues(light.BakingOutput);
		}
	}

	private static void SetEnlightenSceneMapping(ILightingDataAsset lightingDataAsset, ILightmapSettings lightmapSettings, PPtrConverter converter)
	{
		lightingDataAsset.EnlightenSceneMapping.CopyValues(lightmapSettings.EnlightenSceneMapping, converter);

		foreach (IObject? renderer in lightingDataAsset.EnlightenSceneMapping.Renderers.Select(r => r.Renderer.TryGetAsset(lightingDataAsset.Collection)))
		{
			lightingDataAsset.EnlightenSceneMappingRendererIDs.AddNew().TargetObjectReference = renderer;
		}
	}

	private static void SetBakedAmbientProbes(ILightingDataAsset lightingDataAsset, IRenderSettings renderSettings)
	{
		if (renderSettings.Has_AmbientProbeInGamma())
		{
			if (lightingDataAsset.Has_BakedAmbientProbeInGamma())
			{
				lightingDataAsset.BakedAmbientProbeInGamma.CopyValues(renderSettings.AmbientProbeInGamma);
			}
			else if (lightingDataAsset.Has_BakedAmbientProbesInGamma())
			{
				lightingDataAsset.BakedAmbientProbesInGamma.AddNew().CopyValues(renderSettings.AmbientProbeInGamma);
			}
		}

		if (renderSettings.Has_AmbientProbe())
		{
			if (lightingDataAsset.Has_BakedAmbientProbeInLinear())
			{
				lightingDataAsset.BakedAmbientProbeInLinear.CopyValues(renderSettings.AmbientProbe);
			}
			else if (lightingDataAsset.Has_BakedAmbientProbesInLinear())
			{
				lightingDataAsset.BakedAmbientProbesInLinear.AddNew().CopyValues(renderSettings.AmbientProbe);
			}
		}
	}

	private static void AddSkyboxReflection(ILightingDataAsset lightingDataAsset, IRenderSettings renderSettings)
	{
		if (renderSettings.GeneratedSkyboxReflectionP is { } skyboxReflection)
		{
			lightingDataAsset.BakedReflectionProbeCubemapsP.Add(skyboxReflection);
		}
	}

	/// <summary>
	/// 将多个 <see cref="ILightmapData"/> 添加到 <see cref="ILightingDataAsset.Lightmaps"/> 中。
	/// </summary>
	/// <param name="lightingDataAsset"></param>
	/// <param name="lightmaps"></param>
	/// <param name="converter"></param>
	private static void SetLightmaps(ILightingDataAsset lightingDataAsset, AccessListBase<ILightmapData> lightmaps, PPtrConverter converter)
	{
		foreach (ILightmapData lightmapData in lightmaps)
		{
			lightingDataAsset.Lightmaps.AddNew().CopyValues(lightmapData, converter);
		}
	}

	/// <summary>
	/// 设置光照数据资源、光照探针和光照设置的路径及主资源。
	/// </summary>
	/// <param name="lightmapSettings"></param>
	/// <param name="lightProbes"></param>
	/// <param name="lightingSettings"></param>
	/// <param name="scene"></param>
	private static void SetPathsAndMainAsset(ILightmapSettings lightmapSettings, ILightProbes? lightProbes, ILightingSettings? lightingSettings, SceneDefinition scene)
	{
		//几个资产应该都导出在场景旁边的子文件夹中。
		//Example:
		//场景
		//  MyScene.unity
		//  照明数据资产 //这是默认名称来自 Unity。
		//    LightProbes.asset //可选；这可以放在任何地方
		//    <一堆光照贴图纹理> //可选；纹理可以放在任何地方

		ILightingDataAsset? lightingDataAsset = lightmapSettings.LightingDataAssetP;
		if (lightingDataAsset is not null)
		{
			lightingDataAsset.MainAsset = lightingDataAsset;

			lightingDataAsset.OriginalDirectory ??= scene.Path;
			if (lightingDataAsset.Name.IsEmpty)
			{
				lightingDataAsset.Name = LightingDataName;
			}

			// 此原始名称仅用于界面。名称用于导出资源。
			lightingDataAsset.OriginalName ??= scene.Name;
		}

		// 如果存在且未与其他场景共享，请将光探针移动到场景的子文件夹中。
		if (lightProbes is not null)
		{
			lightProbes.OriginalDirectory ??= scene.Path;
		}

		// 如果存在且未与其他场景共享，请将灯光设置移动到场景的子文件夹中。  
		// 虽然没有强制要求必须放置于此，但有助于组织管理。  
		// 当多个 LightingSettings 具有相同名称时，此操作尤为有用。
		if (lightingSettings is not null)
		{
			lightingSettings.OriginalDirectory ??= scene.Path;
		}

		// 将光照贴图纹理移动到场景子文件夹中。
		foreach (ILightmapData lightmapData in lightmapSettings.Lightmaps)
		{
			if (lightmapData.DirLightmap?.TryGetAsset(lightmapSettings.Collection, out ITexture2D? dirLightmap) ?? false)
			{
				dirLightmap.OriginalDirectory ??= scene.Path;
				dirLightmap.MainAsset = lightingDataAsset;
			}

			if (lightmapData.IndirectLightmap?.TryGetAsset(lightmapSettings.Collection, out ITexture2D? indirectLightmap) ?? false)
			{
				indirectLightmap.OriginalDirectory ??= scene.Path;
				indirectLightmap.MainAsset = lightingDataAsset;
			}

			if (lightmapData.Lightmap.TryGetAsset(lightmapSettings.Collection, out ITexture2D? lightmap))
			{
				lightmap.OriginalDirectory ??= scene.Path;
				lightmap.MainAsset = lightingDataAsset;
			}

			if (lightmapData.ShadowMask?.TryGetAsset(lightmapSettings.Collection, out ITexture2D? shadowMask) ?? false)
			{
				shadowMask.OriginalDirectory ??= scene.Path;
				shadowMask.MainAsset = lightingDataAsset;
			}
		}
	}

	/// <summary>
	/// Sets <see cref="ILightingDataAsset.LightProbesP"/> from <see cref="ILightmapSettings.LightProbesP"/>.
	/// </summary>
	/// <remarks>
	/// Note: it is possible for a LightProbes asset to be shared between multiple LightingDataAsset.<br/>
	/// However, that happened when multiple scenes were loaded additively and baked together.<br/>
	/// In that situation, the LightProbes asset and each LightingDataAsset were all in one binary file.<br/>
	/// A LightingDataAssetParent was also in the file and acted as the main asset in the NativeFormatImporter.
	/// </remarks>
	/// <param name="lightingDataAsset"></param>
	/// <param name="lightmapSettings"></param>
	private static void SetLightProbes(ILightingDataAsset lightingDataAsset, ILightmapSettings lightmapSettings)
	{
		lightingDataAsset.LightProbesP = lightmapSettings.LightProbesP;
	}

	/// <summary>
	/// Sets <see cref="ILightingDataAsset.SceneP"/> or <see cref="ILightingDataAsset.SceneGUID"/>.
	/// </summary>
	/// <param name="lightingDataAsset"></param>
	/// <param name="scene"></param>
	/// <param name="processedCollection"></param>
	private static void SetScene(ILightingDataAsset lightingDataAsset, SceneDefinition scene, ProcessedAssetCollection processedCollection)
	{
		if (lightingDataAsset.Has_Scene())
		{
			ISceneAsset sceneAsset = CreateSceneAsset(processedCollection, scene);
			lightingDataAsset.SceneP = sceneAsset;
		}
		else if (lightingDataAsset.Has_SceneGUID())
		{
			lightingDataAsset.SceneGUID.CopyValues(scene.GUID);
		}
	}

	/// <summary>
	/// 设置 <see cref="ILightingDataAsset.EnlightenDataVersion"/>
	/// </summary>
	/// <remarks>
	/// 此值必须正确设置。版本会因 Unity 版本而有很大差异。<br/>
	/// 看来 -1 是不够用的。2021.1 和 2021.2 使用的是 112 版本。<br/>
	/// 由于 Enlighten 已不再维护，任何后续版本也应使用 112。<br/>
	/// 据说，112 自 2017 年起已被使用，甚至可能在 Unity 5 的较晚版本中仍在使用。<br/>
	/// 要提取每个 Unity 版本中的 Enlighten 版本，需要为每个版本创建一个测试项目，然后在该测试项目中烘焙光照。<br/>
	/// 目前没有合适的 API 可以创建 LightingDataAsset。
	/// </remarks>
	/// <param name="lightingDataAsset"></param>
	private static void SetEnlightenDataVersion(ILightingDataAsset lightingDataAsset)
	{
		lightingDataAsset.EnlightenDataVersion = 112;
	}

	private static ISceneAsset CreateSceneAsset(ProcessedAssetCollection collection, SceneDefinition targetScene)
	{
		ISceneAsset asset = collection.CreateSceneAsset();
		asset.TargetScene = targetScene;
		return asset;
	}

	private static byte[] CreateEnlightenData(UnityVersion version)
	{
		// 我遇到的许多照明数据资产，仅仅包含了一个空资产包的字节。
		BundleVersion bundleVersion = false switch
		{
			_ when version.GreaterThanOrEquals(2022, 2) => BundleVersion.BF_2022_2,
			_ when version.GreaterThanOrEquals(2020) => BundleVersion.BF_LargeFilesSupport, // 这始于2019年4.X版本的某个时间点，因此我们使用2020年作为备用。
			_ when version.GreaterThanOrEquals(5, 2, 0, UnityVersionType.Final) => BundleVersion.BF_520_x,
			_ => BundleVersion.BF_350_4x,
		};

		FileStreamBundleFile bundle = new();
		FileStreamBundleHeader header = bundle.Header;
		header.Version = bundleVersion;
		header.UnityWebBundleVersion = "5.x.x";
		header.UnityWebMinimumRevision = version.ToString();

		using MemoryStream stream = new();
		bundle.Write(stream);

		return stream.ToArray();
	}
}
