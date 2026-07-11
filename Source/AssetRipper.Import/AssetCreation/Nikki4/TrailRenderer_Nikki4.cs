using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_1113;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_96;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.GradientOld;
using AssetRipper.SourceGenerated.Subclasses.LineParameters;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_GameObject;
using AssetRipper.SourceGenerated.Subclasses.PPtr_LightmapParameters;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Material;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Transform;
using AssetRipper.SourceGenerated.Subclasses.StaticBatchInfo;
using AssetRipper.SourceGenerated.Subclasses.Vector4f;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class TrailRenderer_Nikki4 :
	Renderer_2019_3_0_a6,
	ITrailRenderer
{
	public TrailRenderer_2019_3_0_a6 m_base;
	AclDatabase m_RuntimeVirtualTexture = new();

	public TrailRenderer_Nikki4(AssetInfo info) : base(info)
	{
		m_base = new TrailRenderer_2019_3_0_a6(info);
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_base.GameObject.ReadRelease(ref reader);
		m_base.Enabled = reader.ReadBoolean();
		m_base.CastShadows_Byte = reader.ReadByte();
		m_base.ReceiveShadows_Byte = reader.ReadByte();
		m_base.DynamicOccludee = reader.ReadByte();
		m_base.MotionVectors = reader.ReadByte();
		m_base.LightProbeUsage = reader.ReadByte();
		m_base.ReflectionProbeUsage_Byte = reader.ReadByte();
		m_base.RayTracingMode = reader.ReadRelease_ByteAlign();
		m_base.RenderingLayerMask = reader.ReadUInt32();
		m_base.RendererPriority = reader.ReadInt32();
		m_base.LightmapIndex_UInt16 = reader.ReadUInt16();
		m_base.LightmapIndexDynamic = reader.ReadUInt16();
		m_base.LightmapTilingOffset.ReadRelease(ref reader);
		m_base.LightmapTilingOffsetDynamic.ReadRelease(ref reader);
		m_base.Materials.ReadRelease_ArrayAlign_Asset<PPtr_Material_5>(ref reader);
		m_base.StaticBatchInfo.ReadRelease(ref reader);
		m_base.StaticBatchRoot.ReadRelease(ref reader);
		m_base.ProbeAnchor.ReadRelease(ref reader);
		m_base.LightProbeVolumeOverride.ReadRelease_AssetAlign<PPtr_GameObject_5>(ref reader);
		m_base.SortingLayerID_Int32 = reader.ReadInt32();
		m_base.SortingLayer = reader.ReadInt16();
		m_base.SortingOrder = reader.ReadRelease_Int16Align();
		
		m_RuntimeVirtualTexture.ReadRelease(ref reader);
		
		m_base.Time = reader.ReadSingle();
		m_base.Parameters.ReadRelease(ref reader);
		m_base.MinVertexDistance = reader.ReadSingle();
		m_base.Autodestruct = reader.ReadBoolean();
		m_base.Emitting = reader.ReadBoolean();
	}

	public bool Has_ApplyActiveColorSpace() => m_base.Has_ApplyActiveColorSpace();

	public bool Has_AutoUVMaxAngle() => m_base.Has_AutoUVMaxAngle();

	public bool Has_AutoUVMaxDistance() => m_base.Has_AutoUVMaxDistance();

	public bool Has_CastShadows_Boolean() => m_base.Has_CastShadows_Boolean();

	public bool Has_CastShadows_Byte() => m_base.Has_CastShadows_Byte();

	public bool Has_Colors() => m_base.Has_Colors();

	public bool Has_DynamicOccludee() => m_base.Has_DynamicOccludee();

	public bool Has_Emitting() => m_base.Has_Emitting();

	public bool Has_EndWidth() => m_base.Has_EndWidth();

	public bool Has_ForceMeshLod() => m_base.Has_ForceMeshLod();

	public bool Has_GlobalIlluminationMeshLod() => m_base.Has_GlobalIlluminationMeshLod();

	public bool Has_IgnoreNormalsForChartDetection() => m_base.Has_IgnoreNormalsForChartDetection();

	public bool Has_ImportantGI() => m_base.Has_ImportantGI();

	public bool Has_LightmapIndex_Byte() => m_base.Has_LightmapIndex_Byte();

	public bool Has_LightmapIndex_UInt16() => m_base.Has_LightmapIndex_UInt16();

	public bool Has_LightmapIndexDynamic() => m_base.Has_LightmapIndexDynamic();

	public bool Has_LightmapParameters() => m_base.Has_LightmapParameters();

	public bool Has_LightmapTilingOffsetDynamic() => m_base.Has_LightmapTilingOffsetDynamic();

	public bool Has_LightProbeAnchor() => m_base.Has_LightProbeAnchor();

	public bool Has_LightProbeUsage() => m_base.Has_LightProbeUsage();

	public bool Has_LightProbeVolumeOverride() => m_base.Has_LightProbeVolumeOverride();

	public bool Has_MaskInteraction() => m_base.Has_MaskInteraction();

	public bool Has_MeshLodSelectionBias() => m_base.Has_MeshLodSelectionBias();

	public bool Has_MinimumChartSize() => m_base.Has_MinimumChartSize();

	public bool Has_MotionVectors() => m_base.Has_MotionVectors();

	public bool Has_Parameters() => m_base.Has_Parameters();

	public bool Has_PrefabAsset() => m_base.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_base.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_base.Has_PrefabInternal();

	public bool Has_PreserveUVs() => m_base.Has_PreserveUVs();

	public bool Has_PreviewTimeScale() => m_base.Has_PreviewTimeScale();

	public bool Has_ProbeAnchor() => m_base.Has_ProbeAnchor();

	public bool Has_RayTraceProcedural() => m_base.Has_RayTraceProcedural();

	public bool Has_RayTracingAccelStructBuildFlags() => m_base.Has_RayTracingAccelStructBuildFlags();

	public bool Has_RayTracingAccelStructBuildFlagsOverride() => m_base.Has_RayTracingAccelStructBuildFlagsOverride();

	public bool Has_RayTracingMode() => m_base.Has_RayTracingMode();

	public bool Has_ReceiveGI() => m_base.Has_ReceiveGI();

	public bool Has_ReceiveShadows_Boolean() => m_base.Has_ReceiveShadows_Boolean();

	public bool Has_ReceiveShadows_Byte() => m_base.Has_ReceiveShadows_Byte();

	public bool Has_ReflectionProbeUsage_Int32() => m_base.Has_ReflectionProbeUsage_Int32();

	public bool Has_ReflectionProbeUsage_Byte() => m_base.Has_ReflectionProbeUsage_Byte();

	public bool Has_RendererPriority() => m_base.Has_RendererPriority();

	public bool Has_RenderingLayerMask() => m_base.Has_RenderingLayerMask();

	public bool Has_SelectedEditorRenderState() => m_base.Has_SelectedEditorRenderState();

	public bool Has_SelectedWireframeHidden() => m_base.Has_SelectedWireframeHidden();

	public bool Has_SmallMeshCulling() => m_base.Has_SmallMeshCulling();

	public bool Has_SortingLayer() => m_base.Has_SortingLayer();

	public bool Has_SortingLayerID_UInt32() => m_base.Has_SortingLayerID_UInt32();

	public bool Has_SortingLayerID_Int32() => m_base.Has_SortingLayerID_Int32();

	public bool Has_SortingOrder() => m_base.Has_SortingOrder();

	public bool Has_StartWidth() => m_base.Has_StartWidth();

	public bool Has_StaticBatchInfo() => m_base.Has_StaticBatchInfo();

	public bool Has_StaticShadowCaster() => m_base.Has_StaticShadowCaster();

	public bool Has_StitchLightmapSeams() => m_base.Has_StitchLightmapSeams();

	public bool Has_StitchSeams() => m_base.Has_StitchSeams();

	public bool Has_SubsetIndices() => m_base.Has_SubsetIndices();

	public bool Has_UseLightProbes() => m_base.Has_UseLightProbes();

	public bool IsReleaseOnly_LightmapTilingOffset() => m_base.IsReleaseOnly_LightmapTilingOffset();

	public bool IsEditorOnly_SortingLayerID_UInt32() => m_base.IsEditorOnly_SortingLayerID_UInt32();

	public bool IsEditorOnly_SortingLayerID_Int32() => m_base.IsEditorOnly_SortingLayerID_Int32();

	public void CopyValues(ITrailRenderer source, PPtrConverter converter) => m_base.CopyValues(source, converter);

	public void CopyValues(ITrailRenderer source) => m_base.CopyValues(source);

	public bool ApplyActiveColorSpace
	{
		get => m_base.ApplyActiveColorSpace;
		set => m_base.ApplyActiveColorSpace = value;
	}

	public bool Autodestruct
	{
		get => m_base.Autodestruct;
		set => m_base.Autodestruct = value;
	}

	public float AutoUVMaxAngle
	{
		get => m_base.AutoUVMaxAngle;
		set => m_base.AutoUVMaxAngle = value;
	}

	public float AutoUVMaxDistance
	{
		get => m_base.AutoUVMaxDistance;
		set => m_base.AutoUVMaxDistance = value;
	}

	public bool CastShadows_Boolean
	{
		get => m_base.CastShadows_Boolean;
		set => m_base.CastShadows_Boolean = value;
	}

	public byte CastShadows_Byte
	{
		get => m_base.CastShadows_Byte;
		set => m_base.CastShadows_Byte = value;
	}

	public GradientOld? Colors => m_base.Colors;
	public IPPtr_EditorExtension CorrespondingSourceObject => m_base.CorrespondingSourceObject;

	public byte DynamicOccludee
	{
		get => m_base.DynamicOccludee;
		set => m_base.DynamicOccludee = value;
	}

	public bool Emitting
	{
		get => m_base.Emitting;
		set => m_base.Emitting = value;
	}

	public bool Enabled
	{
		get => m_base.Enabled;
		set => m_base.Enabled = value;
	}

	public float EndWidth
	{
		get => m_base.EndWidth;
		set => m_base.EndWidth = value;
	}

	public short ForceMeshLod
	{
		get => m_base.ForceMeshLod;
		set => m_base.ForceMeshLod = value;
	}

	public IPPtr_GameObject GameObject => m_base.GameObject;

	public ushort GlobalIlluminationMeshLod
	{
		get => m_base.GlobalIlluminationMeshLod;
		set => m_base.GlobalIlluminationMeshLod = value;
	}

	public uint HideFlags
	{
		get => m_base.HideFlags;
		set => m_base.HideFlags = value;
	}

	public bool IgnoreNormalsForChartDetection
	{
		get => m_base.IgnoreNormalsForChartDetection;
		set => m_base.IgnoreNormalsForChartDetection = value;
	}

	public bool ImportantGI
	{
		get => m_base.ImportantGI;
		set => m_base.ImportantGI = value;
	}

	public byte LightmapIndex_Byte
	{
		get => m_base.LightmapIndex_Byte;
		set => m_base.LightmapIndex_Byte = value;
	}

	public ushort LightmapIndex_UInt16
	{
		get => m_base.LightmapIndex_UInt16;
		set => m_base.LightmapIndex_UInt16 = value;
	}

	public ushort LightmapIndexDynamic
	{
		get => m_base.LightmapIndexDynamic;
		set => m_base.LightmapIndexDynamic = value;
	}

	public PPtr_LightmapParameters? LightmapParameters => m_base.LightmapParameters;
	public Vector4f LightmapTilingOffset => m_base.LightmapTilingOffset;
	public Vector4f? LightmapTilingOffsetDynamic => m_base.LightmapTilingOffsetDynamic;
	public PPtr_Transform_3_5? LightProbeAnchor => m_base.LightProbeAnchor;

	public byte LightProbeUsage
	{
		get => m_base.LightProbeUsage;
		set => m_base.LightProbeUsage = value;
	}

	public PPtr_GameObject_5? LightProbeVolumeOverride => m_base.LightProbeVolumeOverride;

	public int MaskInteraction
	{
		get => m_base.MaskInteraction;
		set => m_base.MaskInteraction = value;
	}

	public AccessListBase<IPPtr_Material> Materials => m_base.Materials;

	public float MeshLodSelectionBias
	{
		get => m_base.MeshLodSelectionBias;
		set => m_base.MeshLodSelectionBias = value;
	}

	public int MinimumChartSize
	{
		get => m_base.MinimumChartSize;
		set => m_base.MinimumChartSize = value;
	}

	public float MinVertexDistance
	{
		get => m_base.MinVertexDistance;
		set => m_base.MinVertexDistance = value;
	}

	public byte MotionVectors
	{
		get => m_base.MotionVectors;
		set => m_base.MotionVectors = value;
	}

	public ILineParameters? Parameters => m_base.Parameters;
	public PPtr_Prefab_2018_3? PrefabAsset => m_base.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_base.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_base.PrefabInternal;

	public bool PreserveUVs
	{
		get => m_base.PreserveUVs;
		set => m_base.PreserveUVs = value;
	}

	public float PreviewTimeScale
	{
		get => m_base.PreviewTimeScale;
		set => m_base.PreviewTimeScale = value;
	}

	public PPtr_Transform_5? ProbeAnchor => m_base.ProbeAnchor;

	public byte RayTraceProcedural
	{
		get => m_base.RayTraceProcedural;
		set => m_base.RayTraceProcedural = value;
	}

	public byte RayTracingAccelStructBuildFlags
	{
		get => m_base.RayTracingAccelStructBuildFlags;
		set => m_base.RayTracingAccelStructBuildFlags = value;
	}

	public byte RayTracingAccelStructBuildFlagsOverride
	{
		get => m_base.RayTracingAccelStructBuildFlagsOverride;
		set => m_base.RayTracingAccelStructBuildFlagsOverride = value;
	}

	public byte RayTracingMode
	{
		get => m_base.RayTracingMode;
		set => m_base.RayTracingMode = value;
	}

	public int ReceiveGI
	{
		get => m_base.ReceiveGI;
		set => m_base.ReceiveGI = value;
	}

	public bool ReceiveShadows_Boolean
	{
		get => m_base.ReceiveShadows_Boolean;
		set => m_base.ReceiveShadows_Boolean = value;
	}

	public byte ReceiveShadows_Byte
	{
		get => m_base.ReceiveShadows_Byte;
		set => m_base.ReceiveShadows_Byte = value;
	}

	public int ReflectionProbeUsage_Int32
	{
		get => m_base.ReflectionProbeUsage_Int32;
		set => m_base.ReflectionProbeUsage_Int32 = value;
	}

	public byte ReflectionProbeUsage_Byte
	{
		get => m_base.ReflectionProbeUsage_Byte;
		set => m_base.ReflectionProbeUsage_Byte = value;
	}

	public int RendererPriority
	{
		get => m_base.RendererPriority;
		set => m_base.RendererPriority = value;
	}

	public uint RenderingLayerMask
	{
		get => m_base.RenderingLayerMask;
		set => m_base.RenderingLayerMask = value;
	}

	public float ScaleInLightmap
	{
		get => m_base.ScaleInLightmap;
		set => m_base.ScaleInLightmap = value;
	}

	public int SelectedEditorRenderState
	{
		get => m_base.SelectedEditorRenderState;
		set => m_base.SelectedEditorRenderState = value;
	}

	public bool SelectedWireframeHidden
	{
		get => m_base.SelectedWireframeHidden;
		set => m_base.SelectedWireframeHidden = value;
	}

	public byte SmallMeshCulling
	{
		get => m_base.SmallMeshCulling;
		set => m_base.SmallMeshCulling = value;
	}

	public short SortingLayer
	{
		get => m_base.SortingLayer;
		set => m_base.SortingLayer = value;
	}

	public uint SortingLayerID_UInt32
	{
		get => m_base.SortingLayerID_UInt32;
		set => m_base.SortingLayerID_UInt32 = value;
	}

	public int SortingLayerID_Int32
	{
		get => m_base.SortingLayerID_Int32;
		set => m_base.SortingLayerID_Int32 = value;
	}

	public short SortingOrder
	{
		get => m_base.SortingOrder;
		set => m_base.SortingOrder = value;
	}

	public float StartWidth
	{
		get => m_base.StartWidth;
		set => m_base.StartWidth = value;
	}

	public StaticBatchInfo? StaticBatchInfo => m_base.StaticBatchInfo;
	public IPPtr_Transform StaticBatchRoot => m_base.StaticBatchRoot;

	public byte StaticShadowCaster
	{
		get => m_base.StaticShadowCaster;
		set => m_base.StaticShadowCaster = value;
	}

	public bool StitchLightmapSeams
	{
		get => m_base.StitchLightmapSeams;
		set => m_base.StitchLightmapSeams = value;
	}

	public bool StitchSeams
	{
		get => m_base.StitchSeams;
		set => m_base.StitchSeams = value;
	}

	public AssetList<uint>? SubsetIndices => m_base.SubsetIndices;

	public float Time
	{
		get => m_base.Time;
		set => m_base.Time = value;
	}

	public bool UseLightProbes
	{
		get => m_base.UseLightProbes;
		set => m_base.UseLightProbes = value;
	}

	public HideFlags HideFlagsE
	{
		get => m_base.HideFlagsE;
		set => m_base.HideFlagsE = value;
	}

	public LightProbeUsage LightProbeUsageE
	{
		get => m_base.LightProbeUsageE;
		set => m_base.LightProbeUsageE = value;
	}

	public SpriteMaskInteraction MaskInteractionE
	{
		get => m_base.MaskInteractionE;
		set => m_base.MaskInteractionE = value;
	}

	public RayTracingMode RayTracingModeE
	{
		get => m_base.RayTracingModeE;
		set => m_base.RayTracingModeE = value;
	}

	public ReflectionProbeUsage ReflectionProbeUsage_Int32E
	{
		get => m_base.ReflectionProbeUsage_Int32E;
		set => m_base.ReflectionProbeUsage_Int32E = value;
	}

	public ReflectionProbeUsage ReflectionProbeUsage_ByteE
	{
		get => m_base.ReflectionProbeUsage_ByteE;
		set => m_base.ReflectionProbeUsage_ByteE = value;
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

	public ILightmapParameters? LightmapParametersP
	{
		get => m_base.LightmapParametersP;
		set => m_base.LightmapParametersP = value;
	}

	public ITransform? LightProbeAnchorP
	{
		get => m_base.LightProbeAnchorP;
		set => m_base.LightProbeAnchorP = value;
	}

	public IGameObject? LightProbeVolumeOverrideP
	{
		get => m_base.LightProbeVolumeOverrideP;
		set => m_base.LightProbeVolumeOverrideP = value;
	}

	public PPtrAccessList<IPPtr_Material, IMaterial> MaterialsP => m_base.MaterialsP;

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

	public ITransform? ProbeAnchorP
	{
		get => m_base.ProbeAnchorP;
		set => m_base.ProbeAnchorP = value;
	}

	public ITransform? StaticBatchRootP
	{
		get => m_base.StaticBatchRootP;
		set => m_base.StaticBatchRootP = value;
	}
}
