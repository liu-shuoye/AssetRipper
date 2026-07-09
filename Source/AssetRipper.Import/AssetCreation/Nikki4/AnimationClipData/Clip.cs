using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation.Nikki4;
using AssetRipper.IO.Endian;
using System.Collections;

public sealed class Clip : UnityAssetBase
{
	public uint CurveCount { get; set; }
	public uint CompressedTransformTracksSize { get; set; }
	public uint CompressedScalarTracksSize { get; set; }
	public uint AclTransformCount { get; set; }
	public uint AclScalarCount { get; set; }
	public AssetList<UInt4StorageAligned> CompressedTransformTracks { get; set; } = new();
	public AssetList<UInt4StorageAligned> CompressedScalarTracks { get; set; } = new();
	public AssetList<AclTransformTrackIDToBindingCurveID> AclTransformTrackIDToBindingCurveID { get; set; } = new();
	public AssetList<uint> AclScalarTrackIDToBindingCurveID { get; set; } = new();
	public int sampleRoundingPolicy;
	public bool enableAclDatabase;
	public AclDatabase aclDatabase = new AclDatabase();
	public uint decompressTrackLODSize;
	public AssetList<uint> decompressTrackLOD { get; set; } = new();

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		CurveCount = reader.ReadUInt32();
		CompressedTransformTracksSize = reader.ReadUInt32();
		CompressedScalarTracksSize = reader.ReadUInt32();
		AclTransformCount = reader.ReadUInt32();
		AclScalarCount = reader.ReadUInt32();

		CompressedTransformTracks.ReadRelease_ArrayAlign_Asset(ref reader);
		CompressedScalarTracks.ReadRelease_ArrayAlign_Asset(ref reader);
		AclTransformTrackIDToBindingCurveID.ReadRelease_ArrayAlign_Asset(ref reader);
		AclScalarTrackIDToBindingCurveID.ReadRelease_ArrayAlign_UInt32(ref reader);

		sampleRoundingPolicy = reader.ReadInt32();
		enableAclDatabase = reader.ReadBoolean();

		aclDatabase.ReadRelease(ref reader);

		decompressTrackLODSize = reader.ReadUInt32();
		decompressTrackLOD.ReadRelease_ArrayAlign_UInt32(ref reader);
	}
}
