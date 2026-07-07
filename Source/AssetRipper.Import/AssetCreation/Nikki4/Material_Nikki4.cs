using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_21;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class Material_Nikki4(AssetInfo info) : Material_2018_3(info)
{
	protected uint m_MaterialType;

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		this.Name = reader.ReadRelease_Utf8StringAlign();
		this.Shader_C21.ReadRelease(ref reader);
		this.ShaderKeywords_C21_Utf8String = reader.ReadRelease_Utf8StringAlign();
		this.LightmapFlags_C21 = reader.ReadUInt32();
		this.EnableInstancingVariants_C21 = reader.ReadBoolean();
		this.DoubleSidedGI_C21 = reader.ReadRelease_BooleanAlign();
		this.CustomRenderQueue_C21 = reader.ReadInt32();
		this.m_MaterialType = reader.ReadUInt32();
		this.StringTagMap_C21.ReadRelease_Map_Utf8StringAlign_Utf8StringAlign(ref reader);
		this.DisabledShaderPasses_C21.ReadRelease_ArrayAlign_Utf8StringAlign(ref reader);
		this.SavedProperties_C21.ReadRelease(ref reader);
	}
}
