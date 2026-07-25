using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_120;
using AssetRipper.SourceGenerated.Subclasses.PPtr_GameObject;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Material;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class LineRenderer_nikki4 : LineRenderer_2019_3_0_a6
{
	AclDatabase m_RuntimeVirtualTexture = new();

	public LineRenderer_nikki4(AssetInfo info) : base(info)
	{
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		this.GameObject_C2.ReadRelease(ref reader);
		this.Enabled_C25 = reader.ReadBoolean();
		this.CastShadows_C25_Byte = reader.ReadByte();
		this.ReceiveShadows_C25_Byte = reader.ReadByte();
		this.DynamicOccludee_C25 = reader.ReadByte();
		this.MotionVectors_C25 = reader.ReadByte();
		this.LightProbeUsage_C25 = reader.ReadByte();
		this.ReflectionProbeUsage_C25_Byte = reader.ReadByte();
		this.RayTracingMode_C25 = reader.ReadRelease_ByteAlign();
		this.RenderingLayerMask_C25 = reader.ReadUInt32();
		this.RendererPriority_C25 = reader.ReadInt32();
		this.LightmapIndex_C25_UInt16 = reader.ReadUInt16();
		this.LightmapIndexDynamic_C25 = reader.ReadUInt16();
		this.LightmapTilingOffset_C25.ReadRelease(ref reader);
		this.LightmapTilingOffsetDynamic_C25.ReadRelease(ref reader);
		this.Materials_C25.ReadRelease_ArrayAlign_Asset<PPtr_Material_5>(ref reader);
		this.StaticBatchInfo_C25.ReadRelease(ref reader);
		this.StaticBatchRoot_C25.ReadRelease(ref reader);
		this.ProbeAnchor_C25.ReadRelease(ref reader);
		this.LightProbeVolumeOverride_C25.ReadRelease_AssetAlign<PPtr_GameObject_5>(ref reader);
		this.SortingLayerID_C25_Int32 = reader.ReadInt32();
		this.SortingLayer_C25 = reader.ReadInt16();
		this.SortingOrder_C25 = reader.ReadRelease_Int16Align();

		m_RuntimeVirtualTexture.ReadRelease(ref reader);

		this.Positions_C120.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.Vector3f.Vector3f>(ref reader);
		this.Parameters_C120.ReadRelease(ref reader);
		this.UseWorldSpace_C120 = reader.ReadBoolean();
		this.Loop_C120 = reader.ReadBoolean();
	}
}
