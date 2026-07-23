using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.Processing.AnimationClips;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_147;
using AssetRipper.SourceGenerated.Classes.ClassID_157;
using AssetRipper.SourceGenerated.Classes.ClassID_19;
using AssetRipper.SourceGenerated.Classes.ClassID_196;
using AssetRipper.SourceGenerated.Classes.ClassID_218;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_30;
using AssetRipper.SourceGenerated.Classes.ClassID_310;
using AssetRipper.SourceGenerated.Classes.ClassID_320;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Classes.ClassID_47;
using AssetRipper.SourceGenerated.Classes.ClassID_687078895;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Classes.ClassID_78;
using AssetRipper.SourceGenerated.Classes.ClassID_850595691;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;
using System.Diagnostics;

namespace AssetRipper.Processing.Editor;

/// <summary>
/// <para>
/// This processor primarily handles "editor-only" fields.
/// These fields exist in the Unity Editor, but not in compiled game files.
/// Without this processing, those fields would have C# default values of zero.
/// </para>
/// <para>
/// For most fields, this is just setting the field to the Unity default.
/// However for some fields, there can be a calculation to recover an appropriate
/// value for the field. For example, <see cref="ITransform.LocalEulerAnglesHint_C4"/>
/// is set using <see cref="ITransform.LocalRotation_C4"/> with a Quaternion to
/// Euler angle conversion. Similarly, <see cref="ITransform.RootOrder_C4"/> is
/// calculated from <see cref="ITransform.Father_C4P"/> and <see cref="ITransform.Children_C4P"/>.
/// </para>
/// <para>
/// Compiled game files can be identified from binary editor files by the 
/// <see cref="TransferInstructionFlags.SerializeGameRelease"/> flag.
/// However, those binary editor files are not commonly ripped with AssetRipper.
/// More often, generated <see cref="ProcessedAssetCollection"/>s are given editor flags
/// so as to exclude them from unnecessary processing. This is the default for
/// <see cref="GameBundle.AddNewProcessedCollection(string, UnityVersion)"/>.
/// </para>
/// </summary>
public class EditorFormatProcessor : IAssetProcessor
{
	private ITagManager? tagManager;
	private readonly BundledAssetsExportMode bundledAssetsExportMode;
	private IAssemblyManager? assemblyManager;
	private PathChecksumCache? checksumCache;

	public EditorFormatProcessor(BundledAssetsExportMode bundledAssetsExportMode)
	{
		this.bundledAssetsExportMode = bundledAssetsExportMode;
	}

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "Editor Format Conversion");
		// 用元数据枚举找到第一个 ITagManager (ClassID 78)，避免 FetchAssets 触发全量反序列化
		tagManager = null;
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			if (tagManager is not null)
			{
				break;
			}
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID != 78)
				{
					continue;
				}
				tagManager = collection.TryGetAssetOnly<ITagManager>(meta.PathID);
				if (tagManager is not null)
				{
					break;
				}
			}
		}
		assemblyManager = gameData.AssemblyManager;
		checksumCache = new PathChecksumCache(gameData);

		// 元数据驱动：仅对 NeedsConversion 命中的 ClassID 调用 TryGetAssetOnly，
		// 避免原 SelectMany(c => c) 触发 GetEnumerator → EnsureAssetsLoaded 的全量反序列化
		// TryGetAssetOnly 内部写入 assets 字典非线程安全，故先 sequential 反序列化到 List，
		// 再由后续 Parallel.ForEach 处理 List，避免并发写字典
		List<IUnityObjectBase> assetsToConvert = new();
		foreach (AssetCollection collection in GetReleaseCollections(gameData))
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (!NeedsConversion(meta.ClassID))
				{
					continue;
				}
				IUnityObjectBase? asset = collection.TryGetAssetOnly(meta.PathID);
				if (asset is not null)
				{
					assetsToConvert.Add(asset);
				}
			}
		}

		//Sequential processing
		foreach (IUnityObjectBase asset in assetsToConvert)
		{
			Convert(asset);
		}

		//Parallel processing
		Parallel.ForEach(assetsToConvert, ConvertAsync);

		checksumCache = null;
		assemblyManager = null;
		tagManager = null;
	}

	private static IEnumerable<AssetCollection> GetReleaseCollections(GameData gameData)
	{
		return gameData.GameBundle.FetchAssetCollections().Where(c => c.Flags.IsRelease());
	}

	/// <summary>
	/// Convert / ConvertAsync 涉及的 ClassID 集合。
	/// 元数据驱动改造时只对集合内的 ClassID 调用 TryGetAssetOnly 反序列化，其余 ClassID 跳过。
	/// 若未来 Convert / ConvertAsync 新增 case，需同步更新此集合。
	/// </summary>
	private static readonly HashSet<int> ConvertableClassIDs = new()
	{
		1,    // IGameObject (Convert)
		4,    // ITransform (ConvertAsync)
		19,   // IPhysics2DSettings (ConvertAsync)
		23,   // IMeshRenderer (Convert - IRenderer)
		26,   // IParticleRenderer (Convert - IRenderer)
		30,   // IGraphicsSettings (ConvertAsync)
		43,   // IMesh (ConvertAsync)
		47,   // IQualitySettings (ConvertAsync)
		74,   // IAnimationClip (Convert)
		96,   // ITrailRenderer (Convert - IRenderer)
		120,  // ILineRenderer (Convert - IRenderer)
		129,  // PlayerSettings (Convert - TypeTreeObject.IsPlayerSettings)
		137,  // ISkinnedMeshRenderer (Convert - IRenderer)
		142,  // IAssetBundle (ConvertAsync)
		157,  // ILightmapSettings (ConvertAsync)
		161,  // IClothRenderer (Convert - IRenderer)
		196,  // INavMeshSettings (Convert)
		199,  // IParticleSystemRenderer (Convert - IRenderer)
		212,  // ISpriteRenderer (Convert - IRenderer)
		218,  // ITerrain (ConvertAsync)
		222,  // ICanvasRenderer (Convert - IRenderer)
		227,  // IBillboardRenderer (Convert - IRenderer)
		310,  // IUnityConnectSettings (ConvertAsync)
		320,  // IPlayableDirector (ConvertAsync)
		687078895,    // ISpriteAtlas (Convert)
		73398921,     // IVFXRenderer (Convert - IRenderer)
		850595691,    // ILightingSettings (ConvertAsync)
		483693784,    // ITilemapRenderer (Convert - IRenderer)
		1931382933,   // IUIRenderer (Convert - IRenderer)
		1971053207,   // ISpriteShapeRenderer (Convert - IRenderer)
	};

	private static bool NeedsConversion(int classID) => ConvertableClassIDs.Contains(classID);

	private void Convert(IUnityObjectBase asset)
	{
		switch (asset)
		{
			//ordered by approximate frequency
			case IGameObject gameObject:
				gameObject.ConvertToEditorFormat(tagManager);
				break;
			case IRenderer renderer:
				EditorFormatConverter.Convert(renderer);
				break;
			case ISpriteAtlas spriteAtlas:
				spriteAtlas.ConvertToEditorFormat();
				break;
			case IAnimationClip animationClip:
				AnimationClipConverter.Process(animationClip, checksumCache!.Value);
				break;
			case INavMeshSettings navMeshSettings:
				navMeshSettings.ConvertToEditorFormat();
				break;
			case TypeTreeObject { IsPlayerSettings: true } playerSettings:
				SerializableStructure editorStructure = playerSettings.EditorFields;
				if (editorStructure.ContainsField("webGLLinkerTarget"))
				{
					editorStructure["webGLLinkerTarget"].AsInt32 = 1;
				}
				if (editorStructure.ContainsField("allowUnsafeCode"))
				{
					editorStructure["allowUnsafeCode"].AsBoolean = true;
				}
				ApiCompatibilityLevel compatibilityLevel;
				ScriptingRuntimeVersion runtimeVersion;
				Debug.Assert(assemblyManager is not null);
				if (assemblyManager.HasMscorlib2)
				{
					compatibilityLevel = ApiCompatibilityLevel.NET_2_0;
					runtimeVersion = ScriptingRuntimeVersion.Legacy;
				}
				else
				{
					compatibilityLevel = ApiCompatibilityLevel.NET_Unity_4_8;
					runtimeVersion = ScriptingRuntimeVersion.Latest;
				}
				if (editorStructure.ContainsField("apiCompatibilityLevel"))
				{
					editorStructure["apiCompatibilityLevel"].AsInt32 = (int)compatibilityLevel;
				}
				if (editorStructure.ContainsField("scriptingRuntimeVersion"))
				{
					editorStructure["scriptingRuntimeVersion"].AsInt32 = (int)runtimeVersion;
				}
				break;
		}
	}

	private static void ConvertAsync(IUnityObjectBase asset)
	{
		switch (asset)
		{
			//ordered by approximate frequency
			case ITransform transform:
				EditorFormatConverterAsync.Convert(transform);
				break;
			case IMesh mesh:
				mesh.SetMeshOptimizationFlags(MeshOptimizationFlags.Everything);
				break;
			case ITerrain terrain:
				terrain.ScaleInLightmap = 0.0512f;
				break;
			case IPlayableDirector playableDirector:
				EditorFormatConverterAsync.Convert(playableDirector);
				break;
			case IAssetBundle assetBundle:
				// PreloadTable is not used by AssetRipper and can be very large, so clear it to save memory and processing time.
				assetBundle.PreloadTable.Clear();
				assetBundle.PreloadTable.Capacity = 0;
				break;
			case IGraphicsSettings graphicsSettings:
				graphicsSettings.ConvertToEditorFormat();
				break;
			case IQualitySettings qualitySettings:
				qualitySettings.ConvertToEditorFormat();
				break;
			case IPhysics2DSettings physics2DSettings:
				physics2DSettings.ConvertToEditorFormat();
				break;
			case ILightmapSettings lightmapSettings:
				lightmapSettings.ConvertToEditorFormat();
				break;
			case ILightingSettings lightingSettings:
				lightingSettings.ConvertToEditorFormat();
				break;
			case IUnityConnectSettings unityConnectSettings:
				unityConnectSettings.ConvertToEditorFormat();
				break;
		}
	}
}
