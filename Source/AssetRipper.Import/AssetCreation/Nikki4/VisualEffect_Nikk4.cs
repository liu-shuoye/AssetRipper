using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_2058629509;
using AssetRipper.SourceGenerated.Classes.ClassID_2083052967;
using AssetRipper.SourceGenerated.Classes.ClassID_8;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_GameObject;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.PPtr_VisualEffectAsset;
using AssetRipper.SourceGenerated.Subclasses.VFXPropertySheetSerializedBase_VFXEntryExposed;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class VisualEffect_Nikk4 :
	Behaviour_2018_3,
	IVisualEffect
{
	private VisualEffect_2019_3_0_a6 m_base;
	private float m_SimulationSpeed;

	public VisualEffect_Nikk4(AssetInfo info) : base(info)
	{
		m_base = new VisualEffect_2019_3_0_a6(info);
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_base.GameObject.ReadRelease(ref reader);
		m_base.Enabled = reader.ReadRelease_ByteAlign();
		m_base.Asset.ReadRelease(ref reader);
		m_base.InitialEventName = reader.ReadRelease_Utf8StringAlign();
		m_base.InitialEventNameOverriden = reader.ReadRelease_ByteAlign();
		m_base.StartSeed = reader.ReadUInt32();
		m_base.ResetSeedOnPlay_Byte = reader.ReadRelease_ByteAlign();
		
		m_SimulationSpeed = reader.ReadSingle();
		
		m_base.PropertySheet.ReadRelease(ref reader);
	}

	public bool Has_AllowInstancing() => m_base.Has_AllowInstancing();

	public bool Has_InitialEventName() => m_base.Has_InitialEventName();

	public bool Has_InitialEventNameOverriden() => m_base.Has_InitialEventNameOverriden();

	public bool Has_ReleaseInstanceOnDisable() => m_base.Has_ReleaseInstanceOnDisable();

	public bool Has_ResetSeedOnPlay_Boolean() => m_base.Has_ResetSeedOnPlay_Boolean();

	public bool Has_ResetSeedOnPlay_Byte() => m_base.Has_ResetSeedOnPlay_Byte();

	public bool Has_ResourceVersion() => m_base.Has_ResourceVersion();

	public void CopyValues(IVisualEffect source, PPtrConverter converter) => m_base.CopyValues(source, converter);

	public void CopyValues(IVisualEffect source) => m_base.CopyValues(source);

	public byte AllowInstancing
	{
		get => m_base.AllowInstancing;
		set => m_base.AllowInstancing = value;
	}

	public PPtr_VisualEffectAsset Asset => m_base.Asset;
	public PPtr_EditorExtension_5 CorrespondingSourceObject => m_base.CorrespondingSourceObject;

	public byte Enabled
	{
		get => m_base.Enabled;
		set => m_base.Enabled = value;
	}

	public PPtr_GameObject_5 GameObject => m_base.GameObject;

	public uint HideFlags
	{
		get => m_base.HideFlags;
		set => m_base.HideFlags = value;
	}

	public Utf8String? InitialEventName
	{
		get => m_base.InitialEventName;
		set => m_base.InitialEventName = value;
	}

	public byte InitialEventNameOverriden
	{
		get => m_base.InitialEventNameOverriden;
		set => m_base.InitialEventNameOverriden = value;
	}

	public PPtr_Prefab_2018_3 PrefabAsset => m_base.PrefabAsset;
	public PPtr_PrefabInstance PrefabInstance => m_base.PrefabInstance;
	public IVFXPropertySheetSerializedBase_VFXEntryExposed PropertySheet => m_base.PropertySheet;

	public byte ReleaseInstanceOnDisable
	{
		get => m_base.ReleaseInstanceOnDisable;
		set => m_base.ReleaseInstanceOnDisable = value;
	}

	public bool ResetSeedOnPlay_Boolean
	{
		get => m_base.ResetSeedOnPlay_Boolean;
		set => m_base.ResetSeedOnPlay_Boolean = value;
	}

	public byte ResetSeedOnPlay_Byte
	{
		get => m_base.ResetSeedOnPlay_Byte;
		set => m_base.ResetSeedOnPlay_Byte = value;
	}

	public uint ResourceVersion
	{
		get => m_base.ResourceVersion;
		set => m_base.ResourceVersion = value;
	}

	public uint StartSeed
	{
		get => m_base.StartSeed;
		set => m_base.StartSeed = value;
	}

	public HideFlags HideFlagsE
	{
		get => m_base.HideFlagsE;
		set => m_base.HideFlagsE = value;
	}

	public IVisualEffectAsset? AssetP
	{
		get => m_base.AssetP;
		set => m_base.AssetP = value;
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
}
