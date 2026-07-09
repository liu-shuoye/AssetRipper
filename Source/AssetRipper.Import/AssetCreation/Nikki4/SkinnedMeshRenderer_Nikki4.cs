using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_1113;
using AssetRipper.SourceGenerated.Classes.ClassID_137;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.AABB;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_GameObject;
using AssetRipper.SourceGenerated.Subclasses.PPtr_LightmapParameters;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Material;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Mesh;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Transform;
using AssetRipper.SourceGenerated.Subclasses.StaticBatchInfo;
using AssetRipper.SourceGenerated.Subclasses.Vector4f;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class SkinnedMeshRenderer_Nikki4 : Renderer_2019_3_0_a6, ISkinnedMeshRenderer
{
	private SkinnedMeshRenderer_2019_3_0_a6 m_skinnedMeshRenderer;
	private int _runtimeVtFileID;
	private long _runtimeVtPathID;

	public SkinnedMeshRenderer_Nikki4(AssetInfo info) : base(info)
	{
		m_skinnedMeshRenderer = new SkinnedMeshRenderer_2019_3_0_a6(info);
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_skinnedMeshRenderer.GameObject.ReadRelease(ref reader);
		m_skinnedMeshRenderer.Enabled = reader.ReadBoolean();
		m_skinnedMeshRenderer.CastShadows_Byte = reader.ReadByte();
		m_skinnedMeshRenderer.ReceiveShadows_Byte = reader.ReadByte();
		m_skinnedMeshRenderer.DynamicOccludee = reader.ReadByte();
		m_skinnedMeshRenderer.MotionVectors = reader.ReadByte();
		m_skinnedMeshRenderer.LightProbeUsage = reader.ReadByte();
		m_skinnedMeshRenderer.ReflectionProbeUsage_Byte = reader.ReadByte();
		m_skinnedMeshRenderer.RayTracingMode = reader.ReadRelease_ByteAlign();
		m_skinnedMeshRenderer.RenderingLayerMask = reader.ReadUInt32();
		m_skinnedMeshRenderer.RendererPriority = reader.ReadInt32();
		m_skinnedMeshRenderer.LightmapIndex_UInt16 = reader.ReadUInt16();
		m_skinnedMeshRenderer.LightmapIndexDynamic = reader.ReadUInt16();
		m_skinnedMeshRenderer.LightmapTilingOffset.ReadRelease(ref reader);
		m_skinnedMeshRenderer.LightmapTilingOffsetDynamic.ReadRelease(ref reader);
		m_skinnedMeshRenderer.Materials.ReadRelease_ArrayAlign_Asset<PPtr_Material_5>(ref reader);
		m_skinnedMeshRenderer.StaticBatchInfo.ReadRelease(ref reader);
		m_skinnedMeshRenderer.StaticBatchRoot.ReadRelease(ref reader);
		m_skinnedMeshRenderer.ProbeAnchor.ReadRelease(ref reader);
		m_skinnedMeshRenderer.LightProbeVolumeOverride.ReadRelease_AssetAlign<PPtr_GameObject_5>(ref reader);
		m_skinnedMeshRenderer.SortingLayerID_Int32 = reader.ReadInt32();
		m_skinnedMeshRenderer.SortingLayer = reader.ReadInt16();
		m_skinnedMeshRenderer.SortingOrder = reader.ReadRelease_Int16Align();

		// 手动跳过或读取 PPtr<RuntimeVirtualTexture>
		_runtimeVtFileID = reader.ReadInt32();
		_runtimeVtPathID = reader.ReadInt64();

		m_skinnedMeshRenderer.Quality = reader.ReadInt32();
		m_skinnedMeshRenderer.UpdateWhenOffscreen = reader.ReadBoolean();
		m_skinnedMeshRenderer.SkinnedMotionVectors = reader.ReadRelease_BooleanAlign();
		m_skinnedMeshRenderer.Mesh.ReadRelease(ref reader);
		m_skinnedMeshRenderer.Bones.ReadRelease_ArrayAlign_Asset<PPtr_Transform_5>(ref reader);
		m_skinnedMeshRenderer.BlendShapeWeights.ReadRelease_ArrayAlign_Single(ref reader);
		m_skinnedMeshRenderer.RootBone.ReadRelease(ref reader);
		m_skinnedMeshRenderer.AABB.ReadRelease(ref reader);
		m_skinnedMeshRenderer.DirtyAABB = reader.ReadRelease_BooleanAlign();
	}

	public bool Has_AutoUVMaxAngle() => m_skinnedMeshRenderer.Has_AutoUVMaxAngle();
	public bool Has_AutoUVMaxDistance() => m_skinnedMeshRenderer.Has_AutoUVMaxDistance();

	public bool Has_BlendShapeWeights() => m_skinnedMeshRenderer.Has_BlendShapeWeights();

	public bool Has_CastShadows_Boolean() => m_skinnedMeshRenderer.Has_CastShadows_Boolean();

	public bool Has_CastShadows_Byte() => m_skinnedMeshRenderer.Has_CastShadows_Byte();

	public bool Has_DynamicOccludee() => m_skinnedMeshRenderer.Has_DynamicOccludee();

	public bool Has_ForceMeshLod() => m_skinnedMeshRenderer.Has_ForceMeshLod();

	public bool Has_GlobalIlluminationMeshLod() => m_skinnedMeshRenderer.Has_GlobalIlluminationMeshLod();

	public bool Has_IgnoreNormalsForChartDetection() => m_skinnedMeshRenderer.Has_IgnoreNormalsForChartDetection();

	public bool Has_ImportantGI() => m_skinnedMeshRenderer.Has_ImportantGI();

	public bool Has_LightmapIndex_Byte() => m_skinnedMeshRenderer.Has_LightmapIndex_Byte();

	public bool Has_LightmapIndex_UInt16() => m_skinnedMeshRenderer.Has_LightmapIndex_UInt16();

	public bool Has_LightmapIndexDynamic() => m_skinnedMeshRenderer.Has_LightmapIndexDynamic();

	public bool Has_LightmapParameters() => m_skinnedMeshRenderer.Has_LightmapParameters();

	public bool Has_LightmapTilingOffsetDynamic() => m_skinnedMeshRenderer.Has_LightmapTilingOffsetDynamic();

	public bool Has_LightProbeAnchor() => m_skinnedMeshRenderer.Has_LightProbeAnchor();

	public bool Has_LightProbeUsage() => m_skinnedMeshRenderer.Has_LightProbeUsage();

	public bool Has_LightProbeVolumeOverride() => m_skinnedMeshRenderer.Has_LightProbeVolumeOverride();

	public bool Has_MaskInteraction() => m_skinnedMeshRenderer.Has_MaskInteraction();

	public bool Has_MeshLodSelectionBias() => m_skinnedMeshRenderer.Has_MeshLodSelectionBias();

	public bool Has_MinimumChartSize() => m_skinnedMeshRenderer.Has_MinimumChartSize();

	public bool Has_MotionVectors() => m_skinnedMeshRenderer.Has_MotionVectors();

	public bool Has_PrefabAsset() => m_skinnedMeshRenderer.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_skinnedMeshRenderer.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_skinnedMeshRenderer.Has_PrefabInternal();

	public bool Has_PreserveUVs() => m_skinnedMeshRenderer.Has_PreserveUVs();

	public bool Has_ProbeAnchor() => m_skinnedMeshRenderer.Has_ProbeAnchor();

	public bool Has_RayTraceProcedural() => m_skinnedMeshRenderer.Has_RayTraceProcedural();

	public bool Has_RayTracingAccelStructBuildFlags() => m_skinnedMeshRenderer.Has_RayTracingAccelStructBuildFlags();

	public bool Has_RayTracingAccelStructBuildFlagsOverride() => m_skinnedMeshRenderer.Has_RayTracingAccelStructBuildFlagsOverride();

	public bool Has_RayTracingMode() => m_skinnedMeshRenderer.Has_RayTracingMode();

	public bool Has_ReceiveGI() => m_skinnedMeshRenderer.Has_ReceiveGI();

	public bool Has_ReceiveShadows_Boolean() => m_skinnedMeshRenderer.Has_ReceiveShadows_Boolean();

	public bool Has_ReceiveShadows_Byte() => m_skinnedMeshRenderer.Has_ReceiveShadows_Byte();

	public bool Has_ReflectionProbeUsage_Int32() => m_skinnedMeshRenderer.Has_ReflectionProbeUsage_Int32();

	public bool Has_ReflectionProbeUsage_Byte() => m_skinnedMeshRenderer.Has_ReflectionProbeUsage_Byte();

	public bool Has_RendererPriority() => m_skinnedMeshRenderer.Has_RendererPriority();

	public bool Has_RenderingLayerMask() => m_skinnedMeshRenderer.Has_RenderingLayerMask();

	public bool Has_SelectedEditorRenderState() => m_skinnedMeshRenderer.Has_SelectedEditorRenderState();

	public bool Has_SelectedWireframeHidden() => m_skinnedMeshRenderer.Has_SelectedWireframeHidden();

	public bool Has_SkinnedMotionVectors() => m_skinnedMeshRenderer.Has_SkinnedMotionVectors();

	public bool Has_SmallMeshCulling() => m_skinnedMeshRenderer.Has_SmallMeshCulling();

	public bool Has_SortingLayer() => m_skinnedMeshRenderer.Has_SortingLayer();

	public bool Has_SortingLayerID_UInt32() => m_skinnedMeshRenderer.Has_SortingLayerID_UInt32();

	public bool Has_SortingLayerID_Int32() => m_skinnedMeshRenderer.Has_SortingLayerID_Int32();

	public bool Has_SortingOrder() => m_skinnedMeshRenderer.Has_SortingOrder();

	public bool Has_StaticBatchInfo() => m_skinnedMeshRenderer.Has_StaticBatchInfo();

	public bool Has_StaticShadowCaster() => m_skinnedMeshRenderer.Has_StaticShadowCaster();

	public bool Has_StitchLightmapSeams() => m_skinnedMeshRenderer.Has_StitchLightmapSeams();

	public bool Has_StitchSeams() => m_skinnedMeshRenderer.Has_StitchSeams();

	public bool Has_SubsetIndices() => m_skinnedMeshRenderer.Has_SubsetIndices();

	public bool Has_UseLightProbes() => m_skinnedMeshRenderer.Has_UseLightProbes();

	public bool IsReleaseOnly_LightmapTilingOffset() => m_skinnedMeshRenderer.IsReleaseOnly_LightmapTilingOffset();

	public bool IsEditorOnly_SortingLayerID_UInt32() => m_skinnedMeshRenderer.IsEditorOnly_SortingLayerID_UInt32();

	public bool IsEditorOnly_SortingLayerID_Int32() => m_skinnedMeshRenderer.IsEditorOnly_SortingLayerID_Int32();

	public void CopyValues(ISkinnedMeshRenderer source, PPtrConverter converter) => m_skinnedMeshRenderer.CopyValues(source, converter);

	public void CopyValues(ISkinnedMeshRenderer source) => m_skinnedMeshRenderer.CopyValues(source);

	public AABB AABB => m_skinnedMeshRenderer.AABB;

	public float AutoUVMaxAngle
	{
		get => m_skinnedMeshRenderer.AutoUVMaxAngle;
		set => m_skinnedMeshRenderer.AutoUVMaxAngle = value;
	}

	public float AutoUVMaxDistance
	{
		get => m_skinnedMeshRenderer.AutoUVMaxDistance;
		set => m_skinnedMeshRenderer.AutoUVMaxDistance = value;
	}

	public AssetList<float>? BlendShapeWeights => m_skinnedMeshRenderer.BlendShapeWeights;
	public AccessListBase<IPPtr_Transform> Bones => m_skinnedMeshRenderer.Bones;

	public bool CastShadows_Boolean
	{
		get => m_skinnedMeshRenderer.CastShadows_Boolean;
		set => m_skinnedMeshRenderer.CastShadows_Boolean = value;
	}

	public byte CastShadows_Byte
	{
		get => m_skinnedMeshRenderer.CastShadows_Byte;
		set => m_skinnedMeshRenderer.CastShadows_Byte = value;
	}

	public IPPtr_EditorExtension CorrespondingSourceObject => m_skinnedMeshRenderer.CorrespondingSourceObject;

	public bool DirtyAABB
	{
		get => m_skinnedMeshRenderer.DirtyAABB;
		set => m_skinnedMeshRenderer.DirtyAABB = value;
	}

	public byte DynamicOccludee
	{
		get => m_skinnedMeshRenderer.DynamicOccludee;
		set => m_skinnedMeshRenderer.DynamicOccludee = value;
	}

	public bool Enabled
	{
		get => m_skinnedMeshRenderer.Enabled;
		set => m_skinnedMeshRenderer.Enabled = value;
	}

	public short ForceMeshLod
	{
		get => m_skinnedMeshRenderer.ForceMeshLod;
		set => m_skinnedMeshRenderer.ForceMeshLod = value;
	}

	public IPPtr_GameObject GameObject => m_skinnedMeshRenderer.GameObject;

	public ushort GlobalIlluminationMeshLod
	{
		get => m_skinnedMeshRenderer.GlobalIlluminationMeshLod;
		set => m_skinnedMeshRenderer.GlobalIlluminationMeshLod = value;
	}

	public uint HideFlags
	{
		get => m_skinnedMeshRenderer.HideFlags;
		set => m_skinnedMeshRenderer.HideFlags = value;
	}

	public bool IgnoreNormalsForChartDetection
	{
		get => m_skinnedMeshRenderer.IgnoreNormalsForChartDetection;
		set => m_skinnedMeshRenderer.IgnoreNormalsForChartDetection = value;
	}

	public bool ImportantGI
	{
		get => m_skinnedMeshRenderer.ImportantGI;
		set => m_skinnedMeshRenderer.ImportantGI = value;
	}

	public byte LightmapIndex_Byte
	{
		get => m_skinnedMeshRenderer.LightmapIndex_Byte;
		set => m_skinnedMeshRenderer.LightmapIndex_Byte = value;
	}

	public ushort LightmapIndex_UInt16
	{
		get => m_skinnedMeshRenderer.LightmapIndex_UInt16;
		set => m_skinnedMeshRenderer.LightmapIndex_UInt16 = value;
	}

	public ushort LightmapIndexDynamic
	{
		get => m_skinnedMeshRenderer.LightmapIndexDynamic;
		set => m_skinnedMeshRenderer.LightmapIndexDynamic = value;
	}

	public PPtr_LightmapParameters? LightmapParameters => m_skinnedMeshRenderer.LightmapParameters;
	public Vector4f LightmapTilingOffset => m_skinnedMeshRenderer.LightmapTilingOffset;
	public Vector4f? LightmapTilingOffsetDynamic => m_skinnedMeshRenderer.LightmapTilingOffsetDynamic;
	public PPtr_Transform_3_5? LightProbeAnchor => m_skinnedMeshRenderer.LightProbeAnchor;

	public byte LightProbeUsage
	{
		get => m_skinnedMeshRenderer.LightProbeUsage;
		set => m_skinnedMeshRenderer.LightProbeUsage = value;
	}

	public PPtr_GameObject_5? LightProbeVolumeOverride => m_skinnedMeshRenderer.LightProbeVolumeOverride;

	public int MaskInteraction
	{
		get => m_skinnedMeshRenderer.MaskInteraction;
		set => m_skinnedMeshRenderer.MaskInteraction = value;
	}

	public AccessListBase<IPPtr_Material> Materials => m_skinnedMeshRenderer.Materials;
	public IPPtr_Mesh Mesh => m_skinnedMeshRenderer.Mesh;

	public float MeshLodSelectionBias
	{
		get => m_skinnedMeshRenderer.MeshLodSelectionBias;
		set => m_skinnedMeshRenderer.MeshLodSelectionBias = value;
	}

	public int MinimumChartSize
	{
		get => m_skinnedMeshRenderer.MinimumChartSize;
		set => m_skinnedMeshRenderer.MinimumChartSize = value;
	}

	public byte MotionVectors
	{
		get => m_skinnedMeshRenderer.MotionVectors;
		set => m_skinnedMeshRenderer.MotionVectors = value;
	}

	public PPtr_Prefab_2018_3? PrefabAsset => m_skinnedMeshRenderer.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_skinnedMeshRenderer.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_skinnedMeshRenderer.PrefabInternal;

	public bool PreserveUVs
	{
		get => m_skinnedMeshRenderer.PreserveUVs;
		set => m_skinnedMeshRenderer.PreserveUVs = value;
	}

	public PPtr_Transform_5? ProbeAnchor => m_skinnedMeshRenderer.ProbeAnchor;

	public int Quality
	{
		get => m_skinnedMeshRenderer.Quality;
		set => m_skinnedMeshRenderer.Quality = value;
	}

	public byte RayTraceProcedural
	{
		get => m_skinnedMeshRenderer.RayTraceProcedural;
		set => m_skinnedMeshRenderer.RayTraceProcedural = value;
	}

	public byte RayTracingAccelStructBuildFlags
	{
		get => m_skinnedMeshRenderer.RayTracingAccelStructBuildFlags;
		set => m_skinnedMeshRenderer.RayTracingAccelStructBuildFlags = value;
	}

	public byte RayTracingAccelStructBuildFlagsOverride
	{
		get => m_skinnedMeshRenderer.RayTracingAccelStructBuildFlagsOverride;
		set => m_skinnedMeshRenderer.RayTracingAccelStructBuildFlagsOverride = value;
	}

	public byte RayTracingMode
	{
		get => m_skinnedMeshRenderer.RayTracingMode;
		set => m_skinnedMeshRenderer.RayTracingMode = value;
	}

	public int ReceiveGI
	{
		get => m_skinnedMeshRenderer.ReceiveGI;
		set => m_skinnedMeshRenderer.ReceiveGI = value;
	}

	public bool ReceiveShadows_Boolean
	{
		get => m_skinnedMeshRenderer.ReceiveShadows_Boolean;
		set => m_skinnedMeshRenderer.ReceiveShadows_Boolean = value;
	}

	public byte ReceiveShadows_Byte
	{
		get => m_skinnedMeshRenderer.ReceiveShadows_Byte;
		set => m_skinnedMeshRenderer.ReceiveShadows_Byte = value;
	}

	public int ReflectionProbeUsage_Int32
	{
		get => m_skinnedMeshRenderer.ReflectionProbeUsage_Int32;
		set => m_skinnedMeshRenderer.ReflectionProbeUsage_Int32 = value;
	}

	public byte ReflectionProbeUsage_Byte
	{
		get => m_skinnedMeshRenderer.ReflectionProbeUsage_Byte;
		set => m_skinnedMeshRenderer.ReflectionProbeUsage_Byte = value;
	}

	public int RendererPriority
	{
		get => m_skinnedMeshRenderer.RendererPriority;
		set => m_skinnedMeshRenderer.RendererPriority = value;
	}

	public uint RenderingLayerMask
	{
		get => m_skinnedMeshRenderer.RenderingLayerMask;
		set => m_skinnedMeshRenderer.RenderingLayerMask = value;
	}

	public IPPtr_Transform RootBone => m_skinnedMeshRenderer.RootBone;

	public float ScaleInLightmap
	{
		get => m_skinnedMeshRenderer.ScaleInLightmap;
		set => m_skinnedMeshRenderer.ScaleInLightmap = value;
	}

	public int SelectedEditorRenderState
	{
		get => m_skinnedMeshRenderer.SelectedEditorRenderState;
		set => m_skinnedMeshRenderer.SelectedEditorRenderState = value;
	}

	public bool SelectedWireframeHidden
	{
		get => m_skinnedMeshRenderer.SelectedWireframeHidden;
		set => m_skinnedMeshRenderer.SelectedWireframeHidden = value;
	}

	public bool SkinnedMotionVectors
	{
		get => m_skinnedMeshRenderer.SkinnedMotionVectors;
		set => m_skinnedMeshRenderer.SkinnedMotionVectors = value;
	}

	public byte SmallMeshCulling
	{
		get => m_skinnedMeshRenderer.SmallMeshCulling;
		set => m_skinnedMeshRenderer.SmallMeshCulling = value;
	}

	public short SortingLayer
	{
		get => m_skinnedMeshRenderer.SortingLayer;
		set => m_skinnedMeshRenderer.SortingLayer = value;
	}

	public uint SortingLayerID_UInt32
	{
		get => m_skinnedMeshRenderer.SortingLayerID_UInt32;
		set => m_skinnedMeshRenderer.SortingLayerID_UInt32 = value;
	}

	public int SortingLayerID_Int32
	{
		get => m_skinnedMeshRenderer.SortingLayerID_Int32;
		set => m_skinnedMeshRenderer.SortingLayerID_Int32 = value;
	}

	public short SortingOrder
	{
		get => m_skinnedMeshRenderer.SortingOrder;
		set => m_skinnedMeshRenderer.SortingOrder = value;
	}

	public StaticBatchInfo? StaticBatchInfo => m_skinnedMeshRenderer.StaticBatchInfo;
	public IPPtr_Transform StaticBatchRoot => m_skinnedMeshRenderer.StaticBatchRoot;

	public byte StaticShadowCaster
	{
		get => m_skinnedMeshRenderer.StaticShadowCaster;
		set => m_skinnedMeshRenderer.StaticShadowCaster = value;
	}

	public bool StitchLightmapSeams
	{
		get => m_skinnedMeshRenderer.StitchLightmapSeams;
		set => m_skinnedMeshRenderer.StitchLightmapSeams = value;
	}

	public bool StitchSeams
	{
		get => m_skinnedMeshRenderer.StitchSeams;
		set => m_skinnedMeshRenderer.StitchSeams = value;
	}

	public AssetList<uint>? SubsetIndices => m_skinnedMeshRenderer.SubsetIndices;

	public bool UpdateWhenOffscreen
	{
		get => m_skinnedMeshRenderer.UpdateWhenOffscreen;
		set => m_skinnedMeshRenderer.UpdateWhenOffscreen = value;
	}

	public bool UseLightProbes
	{
		get => m_skinnedMeshRenderer.UseLightProbes;
		set => m_skinnedMeshRenderer.UseLightProbes = value;
	}

	public HideFlags HideFlagsE
	{
		get => m_skinnedMeshRenderer.HideFlagsE;
		set => m_skinnedMeshRenderer.HideFlagsE = value;
	}

	public LightProbeUsage LightProbeUsageE
	{
		get => m_skinnedMeshRenderer.LightProbeUsageE;
		set => m_skinnedMeshRenderer.LightProbeUsageE = value;
	}

	public SkinQuality QualityE
	{
		get => m_skinnedMeshRenderer.QualityE;
		set => m_skinnedMeshRenderer.QualityE = value;
	}

	public RayTracingMode RayTracingModeE
	{
		get => m_skinnedMeshRenderer.RayTracingModeE;
		set => m_skinnedMeshRenderer.RayTracingModeE = value;
	}

	public ReflectionProbeUsage ReflectionProbeUsage_Int32E
	{
		get => m_skinnedMeshRenderer.ReflectionProbeUsage_Int32E;
		set => m_skinnedMeshRenderer.ReflectionProbeUsage_Int32E = value;
	}

	public ReflectionProbeUsage ReflectionProbeUsage_ByteE
	{
		get => m_skinnedMeshRenderer.ReflectionProbeUsage_ByteE;
		set => m_skinnedMeshRenderer.ReflectionProbeUsage_ByteE = value;
	}

	public PPtrAccessList<IPPtr_Transform, ITransform> BonesP => m_skinnedMeshRenderer.BonesP;

	public IEditorExtension? CorrespondingSourceObjectP
	{
		get => m_skinnedMeshRenderer.CorrespondingSourceObjectP;
		set => m_skinnedMeshRenderer.CorrespondingSourceObjectP = value;
	}

	public IGameObject? GameObjectP
	{
		get => m_skinnedMeshRenderer.GameObjectP;
		set => m_skinnedMeshRenderer.GameObjectP = value;
	}

	public ILightmapParameters? LightmapParametersP
	{
		get => m_skinnedMeshRenderer.LightmapParametersP;
		set => m_skinnedMeshRenderer.LightmapParametersP = value;
	}

	public ITransform? LightProbeAnchorP
	{
		get => m_skinnedMeshRenderer.LightProbeAnchorP;
		set => m_skinnedMeshRenderer.LightProbeAnchorP = value;
	}

	public IGameObject? LightProbeVolumeOverrideP
	{
		get => m_skinnedMeshRenderer.LightProbeVolumeOverrideP;
		set => m_skinnedMeshRenderer.LightProbeVolumeOverrideP = value;
	}

	public PPtrAccessList<IPPtr_Material, IMaterial> MaterialsP => m_skinnedMeshRenderer.MaterialsP;

	public IMesh? MeshP
	{
		get => m_skinnedMeshRenderer.MeshP;
		set => m_skinnedMeshRenderer.MeshP = value;
	}

	public IPrefab? PrefabAssetP
	{
		get => m_skinnedMeshRenderer.PrefabAssetP;
		set => m_skinnedMeshRenderer.PrefabAssetP = value;
	}

	public IPrefabInstance? PrefabInstanceP
	{
		get => m_skinnedMeshRenderer.PrefabInstanceP;
		set => m_skinnedMeshRenderer.PrefabInstanceP = value;
	}

	public IPrefabMarker? PrefabInternalP
	{
		get => m_skinnedMeshRenderer.PrefabInternalP;
		set => m_skinnedMeshRenderer.PrefabInternalP = value;
	}

	public ITransform? ProbeAnchorP
	{
		get => m_skinnedMeshRenderer.ProbeAnchorP;
		set => m_skinnedMeshRenderer.ProbeAnchorP = value;
	}

	public ITransform? RootBoneP
	{
		get => m_skinnedMeshRenderer.RootBoneP;
		set => m_skinnedMeshRenderer.RootBoneP = value;
	}

	public ITransform? StaticBatchRootP
	{
		get => m_skinnedMeshRenderer.StaticBatchRootP;
		set => m_skinnedMeshRenderer.StaticBatchRootP = value;
	}
}
