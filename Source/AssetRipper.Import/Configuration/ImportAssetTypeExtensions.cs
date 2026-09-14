using AssetRipper.SourceGenerated;

namespace AssetRipper.Import.Configuration;

/// <summary>
/// <see cref="ImportAssetType"/> 与 <see cref="ClassIDType"/> 之间的映射与判定辅助。
/// </summary>
/// <remarks>
/// 映射表集中在此处是刻意的：导入白名单要在「解析前」按 ClassID 判断是否需要处理，
/// 而 ClassID 数量众多且同一大类会分散在多个 ClassID 上（如贴图有 6 个 ClassID）。
/// 把映射写在配置层可以让导入与导出两侧共用同一份真相，避免两边规则漂移。
/// </remarks>
public static class ImportAssetTypeExtensions
{
	/// <summary>
	/// 各 <see cref="ImportAssetType"/> 覆盖的 <see cref="ClassIDType"/> 集合。
	/// </summary>
	private static readonly Dictionary<ImportAssetType, ClassIDType[]> ClassIdsByType = new()
	{
		[ImportAssetType.Mesh] =
		[
			ClassIDType.Mesh,
		],
		[ImportAssetType.Texture] =
		[
			ClassIDType.Texture2D,
			ClassIDType.Texture3D,
			ClassIDType.Texture2DArray,
			ClassIDType.Cubemap,
			ClassIDType.CubemapArray,
			ClassIDType.RenderTexture,
			ClassIDType.CustomRenderTexture,
			ClassIDType.SparseTexture,
			ClassIDType.WebCamTexture,
			ClassIDType.ProceduralTexture,
			ClassIDType.MovieTexture,
		],
		[ImportAssetType.Material] =
		[
			ClassIDType.Material,
			ClassIDType.ProceduralMaterial,
		],
		[ImportAssetType.Shader] =
		[
			ClassIDType.Shader,
			ClassIDType.ComputeShader,
			ClassIDType.RayTracingShader,
			ClassIDType.ShaderVariantCollection,
			ClassIDType.ShaderInclude,
		],
		[ImportAssetType.AnimationClip] =
		[
			ClassIDType.AnimationClip,
			ClassIDType.PreviewAnimationClip,
		],
		[ImportAssetType.AnimatorController] =
		[
			ClassIDType.AnimatorController,
			ClassIDType.AnimatorOverrideController,
			ClassIDType.RuntimeAnimatorController,
			ClassIDType.AnimatorState,
			ClassIDType.AnimatorStateMachine,
			ClassIDType.AnimatorTransition,
			ClassIDType.AnimatorStateTransition,
			ClassIDType.BlendTree,
			ClassIDType.Avatar,
			ClassIDType.AvatarMask_319,
			ClassIDType.AvatarMask_1011,
			ClassIDType.AvatarSkeletonMask,
		],
		[ImportAssetType.AudioClip] =
		[
			ClassIDType.AudioClip,
			ClassIDType.SampleClip,
			ClassIDType.AudioResource,
			ClassIDType.AudioContainerElement,
			ClassIDType.AudioRandomContainer,
		],
		[ImportAssetType.Font] =
		[
			ClassIDType.Font,
		],
		[ImportAssetType.TextAsset] =
		[
			ClassIDType.TextAsset,
			ClassIDType.LocalizationAsset,
		],
		[ImportAssetType.Sprite] =
		[
			ClassIDType.Sprite,
			ClassIDType.SpriteAtlas,
			ClassIDType.SpriteAtlasAsset,
			ClassIDType.SpriteRenderer,
		],
		// 层级对象与组件共同构成场景/预制体，拆开会导致引用残缺，故归为同一类。
		[ImportAssetType.GameObject] =
		[
			ClassIDType.GameObject,
			ClassIDType.Transform,
			ClassIDType.RectTransform,
			ClassIDType.PrefabInstance,
			ClassIDType.Prefab,
			ClassIDType.Component,
			ClassIDType.MeshRenderer,
			ClassIDType.MeshFilter,
			ClassIDType.SkinnedMeshRenderer,
			ClassIDType.SpriteMask,
			ClassIDType.Camera,
			ClassIDType.Light,
			ClassIDType.Canvas,
			ClassIDType.CanvasRenderer,
			ClassIDType.CanvasGroup,
			ClassIDType.Animator,
			ClassIDType.AudioSource,
			ClassIDType.AudioListener,
			ClassIDType.ParticleSystem,
			ClassIDType.ParticleSystemRenderer,
			ClassIDType.TrailRenderer,
			ClassIDType.LineRenderer,
			ClassIDType.Projector,
			ClassIDType.LODGroup,
			ClassIDType.SortingGroup,
			ClassIDType.Rigidbody,
			ClassIDType.Rigidbody2D,
			ClassIDType.BoxCollider,
			ClassIDType.BoxCollider2D,
			ClassIDType.SphereCollider,
			ClassIDType.CapsuleCollider,
			ClassIDType.CapsuleCollider2D,
			ClassIDType.CircleCollider2D,
			ClassIDType.PolygonCollider2D,
			ClassIDType.EdgeCollider2D,
			ClassIDType.CompositeCollider2D,
			ClassIDType.TilemapCollider2D,
			ClassIDType.MeshCollider,
			ClassIDType.TerrainCollider,
			ClassIDType.CharacterController,
			ClassIDType.NavMeshAgent,
			ClassIDType.NavMeshObstacle,
			ClassIDType.OffMeshLink,
			ClassIDType.ReflectionProbe,
			ClassIDType.LightProbeGroup,
			ClassIDType.LightProbeProxyVolume,
			ClassIDType.PlayableDirector,
			ClassIDType.VideoPlayer,
			ClassIDType.ArticulationBody,
			ClassIDType.VisualEffect,
		],
		[ImportAssetType.MonoBehaviour] =
		[
			ClassIDType.MonoBehaviour,
			ClassIDType.MonoScript,
		],
		[ImportAssetType.ScriptableObject] =
		[
			ClassIDType.ScriptableCamera,
		],
		[ImportAssetType.VideoClip] =
		[
			ClassIDType.VideoClip_327,
			ClassIDType.VideoClip_329,
		],
	};

	/// <summary>
	/// 反查表：ClassID → 所属的导入类型。
	/// </summary>
	/// <remarks>
	/// 一个 ClassID 理论上可归入多个大类（例如 MonoBehaviour 同时属于
	/// <see cref="ImportAssetType.MonoBehaviour"/> 与作为脚本资产的
	/// <see cref="ImportAssetType.ScriptableObject"/>）。建表时后写入者覆盖前者，
	/// 因此这里只保留首个登记项，保证判定结果稳定、可预测。
	/// </remarks>
	private static readonly Dictionary<int, ImportAssetType> TypeByClassId = BuildReverseLookup();

	private static Dictionary<int, ImportAssetType> BuildReverseLookup()
	{
		Dictionary<int, ImportAssetType> result = new();
		foreach ((ImportAssetType type, ClassIDType[] classIds) in ClassIdsByType)
		{
			foreach (ClassIDType classId in classIds)
			{
				// 已登记过的 ClassID 不再覆盖，避免字典枚举顺序影响判定结果。
				result.TryAdd((int)classId, type);
			}
		}

		return result;
	}

	/// <summary>
	/// 取该导入类型覆盖的全部 <see cref="ClassIDType"/> 取值。
	/// </summary>
	public static IReadOnlyList<ClassIDType> GetClassIds(this ImportAssetType type)
	{
		return ClassIdsByType.TryGetValue(type, out ClassIDType[]? classIds) ? classIds : [];
	}

	/// <summary>
	/// 判断某个 ClassID 是否属于给定的导入类型。
	/// </summary>
	public static bool Matches(this ImportAssetType type, int classId)
	{
		return TypeByClassId.TryGetValue(classId, out ImportAssetType owner) && owner == type;
	}

	/// <summary>
	/// 尝试求得某个 ClassID 所属的导入类型；未归类时返回 false。
	/// </summary>
	/// <remarks>
	/// 未归类的 ClassID 在启用白名单时会被视为「用户无法选择的类型」而跳过，
	/// 这是刻意的取舍：白名单模式下用户明确只想要选中的大类，未分类资源
	/// 若仍被解析会破坏「未选择就不解析」的预期。
	/// </remarks>
	public static bool TryGetImportType(int classId, out ImportAssetType type)
	{
		return TypeByClassId.TryGetValue(classId, out type);
	}

	/// <summary>
	/// 判断某个 ClassID 是否应当被导入。
	/// </summary>
	/// <param name="classId">资产的 Unity ClassID。</param>
	/// <param name="allowedTypes">
	/// 用户选中的类型集合；null 表示不启用过滤（全部导入）。
	/// 传入空集合表示「启用了过滤但一个都没选」，此时所有类型都不通过。
	/// </param>
	/// <remarks>
	/// 空集合与 null 必须区分：null 是「没开这个功能」，空集合是「开了但什么都没选」。
	/// 若把两者都当成不过滤，用户取消勾选全部类型后仍会导出全量资源，与界面预期不符。
	/// </remarks>
	public static bool IsClassIdAllowed(int classId, IReadOnlyCollection<ImportAssetType>? allowedTypes)
	{
		if (allowedTypes is null)
		{
			return true;
		}

		return TryGetImportType(classId, out ImportAssetType type) && allowedTypes.Contains(type);
	}
}
