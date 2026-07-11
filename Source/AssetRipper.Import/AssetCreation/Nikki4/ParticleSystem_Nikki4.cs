using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_198;
using AssetRipper.SourceGenerated.Classes.ClassID_2;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.ClampVelocityModule;
using AssetRipper.SourceGenerated.Subclasses.CollisionModule;
using AssetRipper.SourceGenerated.Subclasses.ColorBySpeedModule;
using AssetRipper.SourceGenerated.Subclasses.ColorModule;
using AssetRipper.SourceGenerated.Subclasses.CustomDataModule;
using AssetRipper.SourceGenerated.Subclasses.EmissionModule;
using AssetRipper.SourceGenerated.Subclasses.ExternalForcesModule;
using AssetRipper.SourceGenerated.Subclasses.ForceModule;
using AssetRipper.SourceGenerated.Subclasses.InheritVelocityModule;
using AssetRipper.SourceGenerated.Subclasses.InitialModule;
using AssetRipper.SourceGenerated.Subclasses.LifetimeByEmitterSpeedModule;
using AssetRipper.SourceGenerated.Subclasses.LightsModule;
using AssetRipper.SourceGenerated.Subclasses.MinMaxCurve;
using AssetRipper.SourceGenerated.Subclasses.MultiModeParameter_MeshSpawn;
using AssetRipper.SourceGenerated.Subclasses.NoiseModule;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_GameObject;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Transform;
using AssetRipper.SourceGenerated.Subclasses.RotationBySpeedModule;
using AssetRipper.SourceGenerated.Subclasses.RotationModule;
using AssetRipper.SourceGenerated.Subclasses.ShapeModule;
using AssetRipper.SourceGenerated.Subclasses.SizeBySpeedModule;
using AssetRipper.SourceGenerated.Subclasses.SizeModule;
using AssetRipper.SourceGenerated.Subclasses.SubModule;
using AssetRipper.SourceGenerated.Subclasses.TrailModule;
using AssetRipper.SourceGenerated.Subclasses.TriggerModule;
using AssetRipper.SourceGenerated.Subclasses.UVModule;
using AssetRipper.SourceGenerated.Subclasses.Vector2f;
using AssetRipper.SourceGenerated.Subclasses.VelocityModule;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class ParticleSystem_Nikki4 : Component_2018_3,
	IParticleSystem
{
	private ParticleSystem_2019_2_0_a9 m_base;
	private float m_prewarmTime;
	private float m_prewarmTickRate;
	private int m_ModulesMask;

	public ParticleSystem_Nikki4(AssetInfo info) : base(info)
	{
		m_base = new ParticleSystem_2019_2_0_a9(info);
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		// IDA sub_2D3004 的字段顺序。
		m_base.GameObject.ReadRelease(ref reader);
		// IDA sub_7FB318 的字段顺序。
		m_base.LengthInSec = reader.ReadSingle();
		m_base.SimulationSpeed = reader.ReadSingle();
		m_base.StopAction = reader.ReadInt32();
		m_base.CullingMode = reader.ReadInt32();
		m_base.RingBufferMode = reader.ReadInt32();
		m_base.RingBufferLoopRange.ReadRelease(ref reader);
		m_base.Looping = reader.ReadBoolean();
		m_base.Prewarm = reader.ReadBoolean();
		m_base.PlayOnAwake = reader.ReadBoolean();
		m_base.UseUnscaledTime = reader.ReadBoolean();
		m_base.AutoRandomSeed = reader.ReadBoolean();
		m_base.UseRigidbodyForVelocity = reader.ReadRelease_BooleanAlign();
		m_base.StartDelay_MinMaxCurve.ReadRelease_AssetAlign<MinMaxCurve_2018>(ref reader);
		m_base.MoveWithTransform_Int32 = reader.ReadRelease_Int32Align();
		m_base.MoveWithCustomTransform.ReadRelease(ref reader);
		m_base.ScalingMode = reader.ReadInt32();
		m_base.RandomSeed_Int32 = reader.ReadInt32();

		// IDA sub_7FB318 新增的字段
		m_prewarmTime = reader.ReadSingle();
		m_prewarmTickRate = reader.ReadSingle();

		// IDA sub_7EC250新增的字段
		m_ModulesMask = reader.ReadInt32();

		// IDA sub_7EC250的字段顺序。
		m_base.InitialModule.ReadRelease(ref reader);
		// 使用自定义 ShapeModule 读取器，确保与定制版 Unity 的序列化格式匹配
		ReadReleaseShapeModule(m_base.ShapeModule, ref reader);
		m_base.EmissionModule.ReadRelease(ref reader);
		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.SizeModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.RotationModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.ColorModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.UVModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.VelocityModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.InheritVelocityModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.ForceModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.ExternalForcesModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.ClampVelocityModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.NoiseModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.SizeBySpeedModule.ReadRelease(ref reader);
		}
		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.RotationBySpeedModule.ReadRelease(ref reader);
		}
		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.ColorBySpeedModule.ReadRelease(ref reader);
		}
		

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.CollisionModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.TriggerModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.SubModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.LightsModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.TrailModule.ReadRelease(ref reader);
		}

		if (reader.ReadRelease_BooleanAlign())
		{
			reader.Position -= 4;
			m_base.CustomDataModule.ReadRelease(ref reader);
		}
	}


	private void ReadReleaseShapeModule(IShapeModule shapeModule, ref EndianSpanReader reader)
	{
		shapeModule.Enabled = reader.ReadRelease_BooleanAlign();
		shapeModule.Type = reader.ReadInt32();
		shapeModule.Angle = reader.ReadSingle();
		shapeModule.Length = reader.ReadSingle();
		shapeModule.BoxThickness!.ReadRelease(ref reader);
		shapeModule.RadiusThickness = reader.ReadSingle();
		shapeModule.DonutRadius = reader.ReadSingle();
		shapeModule.Position!.ReadRelease(ref reader);
		shapeModule.Rotation!.ReadRelease(ref reader);
		shapeModule.Scale!.ReadRelease(ref reader);
		shapeModule.PlacementMode = reader.ReadInt32();
		shapeModule.MeshMaterialIndex = reader.ReadInt32();
		shapeModule.MeshNormalOffset = reader.ReadSingle();

		// 使用自定义 MeshSpawn 读取器，替代标准读取器
		ReadReleaseMeshSpawn(shapeModule.MeshSpawn!, ref reader);

		shapeModule.Mesh.ReadRelease(ref reader);
		shapeModule.MeshRenderer!.ReadRelease(ref reader);
		shapeModule.SkinnedMeshRenderer!.ReadRelease(ref reader);
		shapeModule.Sprite!.ReadRelease(ref reader);
		shapeModule.SpriteRenderer!.ReadRelease(ref reader);
		shapeModule.UseMeshMaterialIndex = reader.ReadBoolean();
		shapeModule.UseMeshColors = reader.ReadBoolean();
		shapeModule.AlignToDirection = reader.ReadRelease_BooleanAlign();
		shapeModule.Texture!.ReadRelease(ref reader);
		shapeModule.TextureClipChannel = reader.ReadInt32();
		shapeModule.TextureClipThreshold = reader.ReadSingle();
		shapeModule.TextureUVChannel = reader.ReadInt32();
		shapeModule.TextureColorAffectsParticles = reader.ReadBoolean();
		shapeModule.TextureAlphaAffectsParticles = reader.ReadBoolean();
		shapeModule.TextureBilinearFiltering = reader.ReadRelease_BooleanAlign();
		shapeModule.RandomDirectionAmount = reader.ReadSingle();
		shapeModule.SphericalDirectionAmount = reader.ReadSingle();
		shapeModule.RandomPositionAmount = reader.ReadSingle();
		shapeModule.RadiusParameter!.ReadRelease(ref reader);
		shapeModule.ArcParameter!.ReadRelease(ref reader);
	}

	private void ReadReleaseMeshSpawn(MultiModeParameter_MeshSpawn meshSpawn, ref EndianSpanReader reader)
	{
		// 定制版 MultiModeParameter_MeshSpawn 的传输函数(sub_A3DC5C)中，
		// value 字段是条件传输的(取决于结构体内部标志位 a1+48)。
		// 经测试确认定制版不传输 value 字段（添加 value 读取后错误位置提前）。
		meshSpawn.Mode = reader.ReadInt32();
		meshSpawn.Spread = reader.ReadSingle();
		meshSpawn.Speed.ReadRelease(ref reader);
	}

	/// <summary>
	/// 自定义 RotationModule 读取器，参照 IDA sub_80E310 的字段顺序。
	/// 使用 Console.WriteLine 确保即时输出，避免 Logger 缓冲导致丢失日志。
	/// </summary>
	private void ReadReleaseRotationModule(IRotationModule rotationModule, ref EndianSpanReader reader)
	{
	}

	public bool Has_AutoRandomSeed() => m_base.Has_AutoRandomSeed();

	public bool Has_CullingMode() => m_base.Has_CullingMode();

	public bool Has_CustomDataModule() => m_base.Has_CustomDataModule();

	public bool Has_EmitterVelocityMode() => m_base.Has_EmitterVelocityMode();

	public bool Has_ExternalForcesModule() => m_base.Has_ExternalForcesModule();

	public bool Has_InheritVelocityModule() => m_base.Has_InheritVelocityModule();

	public bool Has_LifetimeByEmitterSpeedModule() => m_base.Has_LifetimeByEmitterSpeedModule();

	public bool Has_LightsModule() => m_base.Has_LightsModule();

	public bool Has_MoveWithCustomTransform() => m_base.Has_MoveWithCustomTransform();

	public bool Has_MoveWithTransform_Boolean() => m_base.Has_MoveWithTransform_Boolean();

	public bool Has_MoveWithTransform_Int32() => m_base.Has_MoveWithTransform_Int32();

	public bool Has_NoiseModule() => m_base.Has_NoiseModule();

	public bool Has_PrefabAsset() => m_base.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_base.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_base.Has_PrefabInternal();

	public bool Has_RandomSeed_UInt32() => m_base.Has_RandomSeed_UInt32();

	public bool Has_RandomSeed_Int32() => m_base.Has_RandomSeed_Int32();

	public bool Has_RingBufferLoopRange() => m_base.Has_RingBufferLoopRange();

	public bool Has_RingBufferMode() => m_base.Has_RingBufferMode();

	public bool Has_ScalingMode() => m_base.Has_ScalingMode();

	public bool Has_SimulationSpeed() => m_base.Has_SimulationSpeed();

	public bool Has_Speed() => m_base.Has_Speed();

	public bool Has_StartDelay_MinMaxCurve() => m_base.Has_StartDelay_MinMaxCurve();

	public bool Has_StartDelay_Single() => m_base.Has_StartDelay_Single();

	public bool Has_StopAction() => m_base.Has_StopAction();

	public bool Has_TrailModule() => m_base.Has_TrailModule();

	public bool Has_TriggerModule() => m_base.Has_TriggerModule();

	public bool Has_UseRigidbodyForVelocity() => m_base.Has_UseRigidbodyForVelocity();

	public bool Has_UseUnscaledTime() => m_base.Has_UseUnscaledTime();

	public void CopyValues(IParticleSystem source, PPtrConverter converter) => m_base.CopyValues(source, converter);

	public void CopyValues(IParticleSystem source) => m_base.CopyValues(source);

	public bool AutoRandomSeed
	{
		get => m_base.AutoRandomSeed;
		set => m_base.AutoRandomSeed = value;
	}

	public IClampVelocityModule ClampVelocityModule => m_base.ClampVelocityModule;
	public ICollisionModule CollisionModule => m_base.CollisionModule;
	public IColorBySpeedModule ColorBySpeedModule => m_base.ColorBySpeedModule;
	public IColorModule ColorModule => m_base.ColorModule;
	public IPPtr_EditorExtension CorrespondingSourceObject => m_base.CorrespondingSourceObject;

	public int CullingMode
	{
		get => m_base.CullingMode;
		set => m_base.CullingMode = value;
	}

	public ICustomDataModule? CustomDataModule => m_base.CustomDataModule;
	public IEmissionModule EmissionModule => m_base.EmissionModule;

	public int EmitterVelocityMode
	{
		get => m_base.EmitterVelocityMode;
		set => m_base.EmitterVelocityMode = value;
	}

	public IExternalForcesModule? ExternalForcesModule => m_base.ExternalForcesModule;
	public IForceModule ForceModule => m_base.ForceModule;
	public IPPtr_GameObject GameObject => m_base.GameObject;

	public uint HideFlags
	{
		get => m_base.HideFlags;
		set => m_base.HideFlags = value;
	}

	public IInheritVelocityModule? InheritVelocityModule => m_base.InheritVelocityModule;
	public IInitialModule InitialModule => m_base.InitialModule;

	public float LengthInSec
	{
		get => m_base.LengthInSec;
		set => m_base.LengthInSec = value;
	}

	public LifetimeByEmitterSpeedModule? LifetimeByEmitterSpeedModule => m_base.LifetimeByEmitterSpeedModule;
	public ILightsModule? LightsModule => m_base.LightsModule;

	public bool Looping
	{
		get => m_base.Looping;
		set => m_base.Looping = value;
	}

	public PPtr_Transform_5? MoveWithCustomTransform => m_base.MoveWithCustomTransform;

	public bool MoveWithTransform_Boolean
	{
		get => m_base.MoveWithTransform_Boolean;
		set => m_base.MoveWithTransform_Boolean = value;
	}

	public int MoveWithTransform_Int32
	{
		get => m_base.MoveWithTransform_Int32;
		set => m_base.MoveWithTransform_Int32 = value;
	}

	public INoiseModule? NoiseModule => m_base.NoiseModule;

	public bool PlayOnAwake
	{
		get => m_base.PlayOnAwake;
		set => m_base.PlayOnAwake = value;
	}

	public PPtr_Prefab_2018_3? PrefabAsset => m_base.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_base.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_base.PrefabInternal;

	public bool Prewarm
	{
		get => m_base.Prewarm;
		set => m_base.Prewarm = value;
	}

	public uint RandomSeed_UInt32
	{
		get => m_base.RandomSeed_UInt32;
		set => m_base.RandomSeed_UInt32 = value;
	}

	public int RandomSeed_Int32
	{
		get => m_base.RandomSeed_Int32;
		set => m_base.RandomSeed_Int32 = value;
	}

	public Vector2f? RingBufferLoopRange => m_base.RingBufferLoopRange;

	public int RingBufferMode
	{
		get => m_base.RingBufferMode;
		set => m_base.RingBufferMode = value;
	}

	public IRotationBySpeedModule RotationBySpeedModule => m_base.RotationBySpeedModule;
	public IRotationModule RotationModule => m_base.RotationModule;

	public int ScalingMode
	{
		get => m_base.ScalingMode;
		set => m_base.ScalingMode = value;
	}

	public IShapeModule ShapeModule => m_base.ShapeModule;

	public float SimulationSpeed
	{
		get => m_base.SimulationSpeed;
		set => m_base.SimulationSpeed = value;
	}

	public ISizeBySpeedModule SizeBySpeedModule => m_base.SizeBySpeedModule;
	public ISizeModule SizeModule => m_base.SizeModule;

	public float Speed
	{
		get => m_base.Speed;
		set => m_base.Speed = value;
	}

	public IMinMaxCurve? StartDelay_MinMaxCurve => m_base.StartDelay_MinMaxCurve;

	public float StartDelay_Single
	{
		get => m_base.StartDelay_Single;
		set => m_base.StartDelay_Single = value;
	}

	public int StopAction
	{
		get => m_base.StopAction;
		set => m_base.StopAction = value;
	}

	public ISubModule SubModule => m_base.SubModule;
	public ITrailModule? TrailModule => m_base.TrailModule;
	public ITriggerModule? TriggerModule => m_base.TriggerModule;

	public bool UseRigidbodyForVelocity
	{
		get => m_base.UseRigidbodyForVelocity;
		set => m_base.UseRigidbodyForVelocity = value;
	}

	public bool UseUnscaledTime
	{
		get => m_base.UseUnscaledTime;
		set => m_base.UseUnscaledTime = value;
	}

	public IUVModule UVModule => m_base.UVModule;
	public IVelocityModule VelocityModule => m_base.VelocityModule;

	public HideFlags HideFlagsE
	{
		get => m_base.HideFlagsE;
		set => m_base.HideFlagsE = value;
	}

	public ParticleSystemScalingMode ScalingModeE
	{
		get => m_base.ScalingModeE;
		set => m_base.ScalingModeE = value;
	}

	public IEditorExtension? CorrespondingSourceObjectP
	{
		get => m_base.CorrespondingSourceObjectP;
		set => m_base.CorrespondingSourceObjectP = value;
	}

	public IGameObject? GameObjectP
	{
		get => m_base.GameObjectP;
		set => m_base.GameObjectP = value;
	}

	public ITransform? MoveWithCustomTransformP
	{
		get => m_base.MoveWithCustomTransformP;
		set => m_base.MoveWithCustomTransformP = value;
	}

	public IPrefab? PrefabAssetP
	{
		get => m_base.PrefabAssetP;
		set => m_base.PrefabAssetP = value;
	}

	public IPrefabInstance? PrefabInstanceP
	{
		get => m_base.PrefabInstanceP;
		set => m_base.PrefabInstanceP = value;
	}

	public IPrefabMarker? PrefabInternalP
	{
		get => m_base.PrefabInternalP;
		set => m_base.PrefabInternalP = value;
	}
}
