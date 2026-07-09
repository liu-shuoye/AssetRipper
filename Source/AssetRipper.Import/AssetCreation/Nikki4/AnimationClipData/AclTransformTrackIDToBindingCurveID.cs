using AssetRipper.Assets;
using AssetRipper.IO.Endian;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class AclTransformTrackIDToBindingCurveID : UnityAssetBase
{
	public uint RotationIDToBindingCurveID { get; set; }
	public uint PositionIDToBindingCurveID { get; set; }
	public uint ScaleIDToBindingCurveID { get; set; }

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		RotationIDToBindingCurveID = reader.ReadUInt32();
		PositionIDToBindingCurveID = reader.ReadUInt32();
		ScaleIDToBindingCurveID = reader.ReadUInt32();
	}
}
