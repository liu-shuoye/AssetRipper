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
using AssetRipper.SourceGenerated;
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
/// 此处理器主要处理“仅限编辑器”的字段。
/// 这些字段存在于 Unity 编辑器中，但在编译后的游戏文件中不存在。
/// 如果不进行此处理，这些字段将具有 C# 默认值零。
/// </para>
/// <para>
/// 对于大多数字段，该处理器只需将其设置为 Unity 的默认值。
/// 然而，对于某些字段，可能需要进行计算以恢复其适当的值。
/// 例如，<see cref="ITransform.LocalEulerAnglesHint_C4"/> 是通过使用 <see cref="ITransform.LocalRotation_C4"/> 并结合 Quaternion 转换为欧拉角来设置的。同样，<see cref="ITransform.RootOrder_C4"/> 是根据 <see cref="ITransform.Father_C4P"/> 和 <see cref="ITransform.Children_C4P"/> 计算得出的。
/// </para>
/// <para>
/// 可通过 <see cref="TransferInstructionFlags.SerializeGameRelease"/> 标志来识别编译后的游戏文件，该标志来自二进制编辑器文件。
/// 然而，这些二进制编辑器文件通常不会被 AssetRipper 打包提取。
/// 更常见的是，生成的 <see cref="ProcessedAssetCollection"/> 会带有编辑器标志，以便排除不必要的处理。这是默认行为。
/// <see cref="GameBundle.AddNewProcessedCollection(string, UnityVersion)"/>.
/// </para>
/// <para>
/// <b>分阶段执行策略：</b>
/// <list type="bullet">
/// <item><see cref="Process"/> 阶段仅构建 <see cref="checksumCache"/> 并通过 <see cref="PrepareForExport"/> 准备依赖，不再执行 Convert。</item>
/// <item>所有 Convert 由 <see cref="ProcessForExport"/> 在导出阶段按需处理（见 <see cref="ExportStageClassIDs"/>）。</item>
/// <item>调用方需在导出开始前先调用 <see cref="PrepareForExport"/> 重建 <see cref="tagManager"/> 与 <see cref="assemblyManager"/> 依赖。</item>
/// </list>
/// </para>
/// </summary>
public class EditorFormatProcessor(BundledAssetsExportMode bundledAssetsExportMode) : IAssetProcessor
{
	private ITagManager? tagManager;
	private readonly BundledAssetsExportMode bundledAssetsExportMode = bundledAssetsExportMode;
	private IAssemblyManager? assemblyManager;
	private PathChecksumCache? checksumCache;

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "编辑格式转换（准备阶段）");

		// checksumCache 供 Export 阶段的 ProcessForExport → Convert(IAnimationClip) 使用。
		// 必须在 Process 阶段构建（此时资产尚未被 Unload），PathChecksumCache 构造函数会反序列化
		// IAvatar/IAnimator/IAnimation 来构建路径缓存，这些资产在 Unload 后释放但缓存本身仍有效。
		checksumCache = new PathChecksumCache(gameData);

		PrepareForExport(gameData);
	}

	/// <summary>
	/// 为导出阶段准备 <see cref="tagManager"/> 与 <see cref="assemblyManager"/> 依赖。
	/// 必须在首次调用 <see cref="ProcessForExport"/> 之前调用一次。
	/// </summary>
	/// <remarks>
	/// 此方法不反序列化资产本体——只通过 <see cref="AssetCollection.EnumerateAssetMetadata"/>
	/// 定位 <see cref="ITagManager"/> 并单对象反序列化它。其余资产由
	/// <see cref="ProjectYamlWalker"/> 在 <c>WalkEditor</c> 触发时按需反序列化。
	/// </remarks>
	public void PrepareForExport(GameData gameData)
	{
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
				if (meta.ClassID != (int)ClassIDType.TagManager)
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
		// checksumCache 在 Process 阶段已构建，此处无需重建；供 Export 阶段的 ProcessForExport 使用
	}

	/// <summary>
	/// 导出阶段按需对单个资产执行 EditorFormat 转换。
	/// 在 <see cref="ProjectYamlWalker.ExportYamlDocument"/> 中、<c>WalkEditor</c> 之前调用。
	/// </summary>
	/// <remarks>
	/// <para>幂等：字段被设置为确定值，多次调用不会累积错误。</para>
	/// <para>仅处理 <see cref="ExportStageClassIDs"/> 中的类型——Process 阶段不再执行任何 Convert，
	/// 所有转换（含 AnimationClip 解压与源数据清空）均在此处按需执行。</para>
	/// </remarks>
	public void ProcessForExport(IUnityObjectBase asset)
	{
		if (!NeedsConversionForExport(asset.ClassID))
		{
			return;
		}

		Convert(asset);
		ConvertAsync(asset);
	}

	/// <summary>
	/// 导出阶段需处理的 ClassID 集合（非破坏性字段设置/计算）。
	/// 这些 Convert 只是设置编辑器默认值或重算字段，可安全延迟到导出阶段按需执行。
	/// 若未来 Convert / ConvertAsync 新增 case，需同步更新此集合。
	/// </summary>
	private static readonly HashSet<int> ExportStageClassIDs = new()
	{
		(int)ClassIDType.GameObject, // IGameObject (Convert)
		(int)ClassIDType.Transform, // ITransform (ConvertAsync)
		(int)ClassIDType.Physics2DSettings, // IPhysics2DSettings (ConvertAsync)
		(int)ClassIDType.MeshRenderer, // IMeshRenderer (Convert - IRenderer)
		(int)ClassIDType.ParticleRenderer, // IParticleRenderer (Convert - IRenderer)
		(int)ClassIDType.GraphicsSettings, // IGraphicsSettings (ConvertAsync)
		(int)ClassIDType.Mesh, // IMesh (ConvertAsync)
		(int)ClassIDType.QualitySettings, // IQualitySettings (ConvertAsync)
		(int)ClassIDType.AnimationClip, // IAnimationClip (Convert)
		(int)ClassIDType.TrailRenderer, // ITrailRenderer (Convert - IRenderer)
		(int)ClassIDType.LineRenderer, // ILineRenderer (Convert - IRenderer)
		129, // PlayerSettings (Convert - TypeTreeObject.IsPlayerSettings)
		(int)ClassIDType.SpriteRenderer, // ISpriteRenderer (Convert - IRenderer)
		(int)ClassIDType.SkinnedMeshRenderer, // ISkinnedMeshRenderer (Convert - IRenderer)
		(int)ClassIDType.AssetBundle, // IAssetBundle (ConvertAsync)
		(int)ClassIDType.LightmapSettings, // ILightmapSettings (ConvertAsync)
		(int)ClassIDType.ClothRenderer, // IClothRenderer (Convert - IRenderer)
		(int)ClassIDType.NavMeshSettings, // INavMeshSettings (Convert)
		(int)ClassIDType.ParticleSystemRenderer, // IParticleSystemRenderer (Convert - IRenderer)
		(int)ClassIDType.Terrain, // ITerrain (ConvertAsync)
		(int)ClassIDType.CanvasRenderer, // ICanvasRenderer (Convert - IRenderer)
		(int)ClassIDType.BillboardRenderer, // IBillboardRenderer (Convert - IRenderer)
		(int)ClassIDType.UnityConnectSettings, // IUnityConnectSettings (ConvertAsync)
		(int)ClassIDType.PlayableDirector, // IPlayableDirector (ConvertAsync)
		(int)ClassIDType.SpriteAtlas, // ISpriteAtlas (Convert)
		(int)ClassIDType.VFXRenderer, // IVFXRenderer (Convert - IRenderer)
		(int)ClassIDType.LightingSettings, // ILightingSettings (ConvertAsync)
		(int)ClassIDType.TilemapRenderer, // ITilemapRenderer (Convert - IRenderer)
		(int)ClassIDType.UIRenderer, // IUIRenderer (Convert - IRenderer)
		(int)ClassIDType.SpriteShapeRenderer, // ISpriteShapeRenderer (Convert - IRenderer)
	};

	private static bool NeedsConversionForExport(int classID) => ExportStageClassIDs.Contains(classID);

	/// <summary>
	/// 处理仅限编辑器的字段。
	/// </summary>
	/// <param name="asset"></param>
	public void Convert(IUnityObjectBase asset)
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

	/// <summary>
	/// 处理仅限编辑器的字段。
	/// </summary>
	/// <param name="asset"></param>
	public static void ConvertAsync(IUnityObjectBase asset)
	{
		switch (asset)
		{
			// 按大致频率排序
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
				// PreloadTable 未被 AssetRipper 使用，且可能非常大，因此请清除它以节省内存和处理时间。
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
