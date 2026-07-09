
using AssetRipper.Assets;
using AssetRipper.IO.Endian;

public class UInt4StorageAligned : UnityAssetBase
{
	public uint X { get; set; }
	public uint Y { get; set; }
	public uint Z { get; set; }
	public uint W { get; set; }
	public override void ReadRelease(ref EndianSpanReader reader)
	{
		X = reader.ReadUInt32();
		Y = reader.ReadUInt32();
		Z = reader.ReadUInt32();
		W = reader.ReadUInt32();
	}
}
