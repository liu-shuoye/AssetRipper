using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class AclDatabase : UnityAssetBase,IUnityObjectBase
{
	public int m_FileID { get; set; }
	public Int64 m_PathID { get; set; }

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_FileID = reader.ReadInt32();
		m_PathID = reader.ReadInt64();
	}

	public AssetInfo AssetInfo { get; }
	public int ClassID { get; }
	public string ClassName { get; }
	public AssetCollection Collection { get; }
	public long PathID { get; }
	public string? OriginalPath { get; set; }
	public string? OriginalDirectory { get; set; }
	public string? OriginalName { get; set; }
	public string? OriginalExtension { get; set; }
	public string? OverridePath { get; set; }
	public string? OverrideDirectory { get; set; }
	public string? OverrideName { get; set; }
	public string? OverrideExtension { get; set; }
	public string? AssetBundleName { get; set; }
	public IUnityObjectBase? MainAsset { get; set; }
}
