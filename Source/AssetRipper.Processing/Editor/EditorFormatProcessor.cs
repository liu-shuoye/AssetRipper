using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Assembly.Serializable;
using AssetRipper.IO.Files.SerializedFiles;
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
public class EditorFormatProcessor(BundledAssetsExportMode bundledAssetsExportMode) : IAssetProcessor
{
	private ITagManager? tagManager;
	private readonly BundledAssetsExportMode bundledAssetsExportMode = bundledAssetsExportMode;
	private IAssemblyManager? assemblyManager;

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

		//Sequential processing
		foreach (IUnityObjectBase asset in GetReleaseAssets(gameData))
		{
			Convert(asset);
		}

		//Parallel processing
		Parallel.ForEach(GetReleaseAssets(gameData), ConvertAsync);

	assemblyManager = null;
	tagManager = null;
}

/// <summary>
/// Process 阶段真正需要反序列化的 ClassID 集合：只有能命中 <see cref="Convert"/> /
/// <see cref="ConvertAsync"/> 中任一 switch case 的对象才有处理价值，其余类型
/// （Texture2D、AudioClip、通用 MonoBehaviour 等）在 Process 阶段完全没有必要实例化。
/// 集合由反射从源生成程序集收集，避免手写 ClassID 列表与 switch 的 is 判定漂移
/// （尤其 Renderer 存在多个版本子类）。
/// </summary>
private static readonly HashSet<int> RequiredClassIDs = CollectRequiredClassIDs();

private static HashSet<int> CollectRequiredClassIDs()
{
	// 与 Convert / ConvertAsync 两个 switch 的所有 case 一一对应的目标接口
	Type[] targetInterfaces =
	[
		typeof(IGameObject),
		typeof(IRenderer),
		typeof(ISpriteAtlas),
		// IAnimationClip 的曲线转换已延迟到导出阶段，Process 阶段无需再物化动画剪辑
		typeof(INavMeshSettings),
		typeof(ITransform),
		typeof(IMesh),
		typeof(ITerrain),
		typeof(IPlayableDirector),
		typeof(IAssetBundle),
		typeof(IGraphicsSettings),
		typeof(IQualitySettings),
		typeof(IPhysics2DSettings),
		typeof(ILightmapSettings),
		typeof(ILightingSettings),
		typeof(IUnityConnectSettings),
	];
	HashSet<int> result = [];
	// 生成类全名形如 AssetRipper.SourceGenerated.Classes.ClassID_43.Mesh_2019，
	// 与 MemoryDiagnostics 的解析规则一致：取 ClassID_ 后的数字作为 ClassID
	const string marker = ".ClassID_";
	foreach (Type type in typeof(IGameObject).Assembly.GetTypes())
	{
		if (!type.IsClass || type.IsAbstract || type.FullName is null)
		{
			continue;
		}

		int markerIndex = type.FullName.IndexOf(marker, StringComparison.Ordinal);
		if (markerIndex < 0)
		{
			continue;
		}
		int digitsStart = markerIndex + marker.Length;
		int digitsEnd = digitsStart;
		while (digitsEnd < type.FullName.Length && char.IsAsciiDigit(type.FullName[digitsEnd]))
		{
			digitsEnd++;
		}
		// 数字后必须紧跟类名分隔符或字符串结束，否则可能把 ClassID_128 配成 ClassID_1280
		if (digitsEnd == digitsStart || (digitsEnd < type.FullName.Length && type.FullName[digitsEnd] != '.'))
		{
			continue;
		}

		if (!int.TryParse(type.FullName.AsSpan(digitsStart, digitsEnd - digitsStart), out int classID))
		{
			continue;
		}
		if (targetInterfaces.Any(interfaceType => interfaceType.IsAssignableFrom(type)))
		{
			result.Add(classID);
		}
	}

	// PlayerSettings (129)：魔改引擎里以 TypeTreeObject（NullObject 子类，位于 AssetRipper.Import）形式
	// 出现，不在源生成程序集内；对应 switch 的 `TypeTreeObject { IsPlayerSettings: true }` 判定恰好等于
	// ClassID == 129（见 TypeTreeObject.IsPlayerSettings），按接口反射收集不到，显式补充。
	result.Add(129);

	// Mesh_Nikki4 / AnimationClip_Nikki4 等运行时自定义类型复用生成类 ClassID（43 / 74），已被上述反射收录。
	return result;
}

private static IEnumerable<IUnityObjectBase> GetReleaseAssets(GameData gameData)
{
	// 原先 SelectMany(c => c) 走 AssetCollection.GetEnumerator() → EnsureAssetsLoaded()，
	// 会把整个集合全量物化（OOM 根因 R1-b）。改为元数据枚举 + ClassID 预筛，
	// 只对 switch 真正需要的类型调 TryGetAssetOnly 做单对象反序列化。
	foreach (AssetCollection collection in GetReleaseCollections(gameData))
	{
		foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
		{
			if (!RequiredClassIDs.Contains(meta.ClassID))
			{
				continue;
			}

			IUnityObjectBase? asset = collection.TryGetAssetOnly(meta.PathID);
			if (asset is not null)
			{
				yield return asset;
			}
		}
	}
}

	private static IEnumerable<AssetCollection> GetReleaseCollections(GameData gameData)
	{
		return gameData.GameBundle.FetchAssetCollections().Where(c => c.Flags.IsRelease());
	}

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
			// IAnimationClip 的 EditorFormat 曲线转换已延迟到导出 .anim 时按需执行（AnimationClipYamlExporter），
			// 避免 Process 阶段一次性生成 14.4GB 的 Vector3f/Quaternionf/Keyframe 曲线对象，降低内存峰值。
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
