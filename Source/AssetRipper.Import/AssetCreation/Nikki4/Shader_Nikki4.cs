using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.IO.Writing;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_130;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.GUID;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Shader;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Texture;
using AssetRipper.SourceGenerated.Subclasses.SerializedShader;
using AssetRipper.SourceGenerated.Subclasses.ShaderCompilationInfo;
using AssetRipper.SourceGenerated.Subclasses.ShaderError;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class Shader_Nikki4 : NamedObject_2018_3, IShader
{
	private Shader_2019_3_0_b0 m_Shader;
	readonly AssetList<AssetList<uint>> m_CodeOffsets;
	readonly AssetList<AssetList<uint>> m_CodeCompressedLengths;
	internal readonly AssetList<AssetList<uint>> m_CodeDecompressedLengths;
	internal byte[] m_CodeCompressedBlob;
	public readonly AssetList<PPtr_Shader_5> m_PapeFallbackShader;

	public Shader_Nikki4(AssetInfo info) : base(info)
	{
		m_Shader = new Shader_2019_3_0_b0(info);
		m_CodeOffsets = new AssetList<AssetList<uint>>();
		m_CodeCompressedLengths = new AssetList<AssetList<uint>>();
		m_CodeDecompressedLengths = new AssetList<AssetList<uint>>();
		m_CodeCompressedBlob = System.Array.Empty<byte>();
		m_PapeFallbackShader = new AssetList<PPtr_Shader_5>();
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_Shader.Name = reader.ReadRelease_Utf8StringAlign();
		m_Shader.ParsedForm.ReadRelease(ref reader);
		m_Shader.Platforms.ReadRelease_ArrayAlign_UInt32(ref reader);
		m_Shader.Offsets_AssetList_AssetList_UInt32.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_Shader.CompressedLengths_AssetList_AssetList_UInt32.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_Shader.DecompressedLengths_AssetList_AssetList_UInt32.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_Shader.CompressedBlob = reader.ReadRelease_ArrayAlign_Byte();

		m_CodeOffsets.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_CodeCompressedLengths.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_CodeDecompressedLengths.ReadRelease_ArrayAlign_ArrayAlign_UInt32(ref reader);
		m_CodeCompressedBlob = reader.ReadRelease_ArrayAlign_Byte();

		m_Shader.Dependencies.ReadRelease_ArrayAlign_Asset<PPtr_Shader_5>(ref reader);
		m_Shader.NonModifiableTextures.ReadRelease_Map_Utf8StringAlign_PPtr_Texture_5(ref reader);
		m_Shader.ShaderIsBaked = reader.ReadRelease_BooleanAlign();

		m_PapeFallbackShader.ReadRelease_ArrayAlign_Asset<PPtr_Shader_5>(ref reader);

		m_Shader.Name = m_Shader.ParsedForm.Name;
	}

	public bool Has_AssetGUID() => m_Shader.Has_AssetGUID();

	public bool Has_AssetLocalIdentifierInFile() => m_Shader.Has_AssetLocalIdentifierInFile();

	public bool Has_CompileInfo() => m_Shader.Has_CompileInfo();

	public bool Has_CompileSmokeTestAfterImport() => m_Shader.Has_CompileSmokeTestAfterImport();

	public bool Has_CompressedBlob() => m_Shader.Has_CompressedBlob();

	public bool Has_CompressedLengths_AssetList_UInt32() => m_Shader.Has_CompressedLengths_AssetList_UInt32();

	public bool Has_CompressedLengths_AssetList_AssetList_UInt32() => m_Shader.Has_CompressedLengths_AssetList_AssetList_UInt32();

	public bool Has_DecompressedLengths_AssetList_UInt32() => m_Shader.Has_DecompressedLengths_AssetList_UInt32();

	public bool Has_DecompressedLengths_AssetList_AssetList_UInt32() => m_Shader.Has_DecompressedLengths_AssetList_AssetList_UInt32();

	public bool Has_DecompressedSize() => m_Shader.Has_DecompressedSize();

	public bool Has_DefaultTextures() => m_Shader.Has_DefaultTextures();

	public bool Has_Dependencies() => m_Shader.Has_Dependencies();

	public bool Has_Errors() => m_Shader.Has_Errors();

	public bool Has_NonModifiableTextures() => m_Shader.Has_NonModifiableTextures();

	public bool Has_Offsets_AssetList_UInt32() => m_Shader.Has_Offsets_AssetList_UInt32();

	public bool Has_Offsets_AssetList_AssetList_UInt32() => m_Shader.Has_Offsets_AssetList_AssetList_UInt32();

	public bool Has_ParsedForm() => m_Shader.Has_ParsedForm();

	public bool Has_PathName() => m_Shader.Has_PathName();

	public bool Has_Platforms() => m_Shader.Has_Platforms();

	public bool Has_PrefabAsset() => m_Shader.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_Shader.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_Shader.Has_PrefabInternal();

	public bool Has_Script() => m_Shader.Has_Script();

	public bool Has_ShaderIsBaked() => m_Shader.Has_ShaderIsBaked();

	public bool Has_StageCounts() => m_Shader.Has_StageCounts();

	public void CopyValues(IShader source, PPtrConverter converter) => m_Shader.CopyValues(source, converter);

	public void CopyValues(IShader source) => m_Shader.CopyValues(source);

	public GUID? AssetGUID => m_Shader.AssetGUID;
	public long AssetLocalIdentifierInFile { get => m_Shader.AssetLocalIdentifierInFile; set => m_Shader.AssetLocalIdentifierInFile = value; }
	public IShaderCompilationInfo? CompileInfo => m_Shader.CompileInfo;
	public bool CompileSmokeTestAfterImport { get => m_Shader.CompileSmokeTestAfterImport; set => m_Shader.CompileSmokeTestAfterImport = value; }

	public byte[]? CompressedBlob
	{
		get => m_Shader.CompressedBlob;
		set => m_Shader.CompressedBlob = value;
	}

	public AssetList<uint>? CompressedLengths_AssetList_UInt32 => m_Shader.CompressedLengths_AssetList_UInt32;
	public AssetList<AssetList<uint>>? CompressedLengths_AssetList_AssetList_UInt32 => m_Shader.CompressedLengths_AssetList_AssetList_UInt32;
	public IPPtr_EditorExtension CorrespondingSourceObject => m_Shader.CorrespondingSourceObject;
	public AssetList<uint>? DecompressedLengths_AssetList_UInt32 => m_Shader.DecompressedLengths_AssetList_UInt32;
	public AssetList<AssetList<uint>>? DecompressedLengths_AssetList_AssetList_UInt32 => m_Shader.DecompressedLengths_AssetList_AssetList_UInt32;

	public uint DecompressedSize
	{
		get => m_Shader.DecompressedSize;
		set => m_Shader.DecompressedSize = value;
	}

	public AccessDictionaryBase<Utf8String, IPPtr_Texture>? DefaultTextures => m_Shader.DefaultTextures;
	public AccessListBase<IPPtr_Shader>? Dependencies => m_Shader.Dependencies;
	public AccessListBase<IShaderError>? Errors => m_Shader.Errors;

	public uint HideFlags
	{
		get => m_Shader.HideFlags;
		set => m_Shader.HideFlags = value;
	}

	public Utf8String Name_R
	{
		get => m_Shader.Name_R;
		set => m_Shader.Name_R = value;
	}

	public AssetDictionary<Utf8String, PPtr_Texture_5>? NonModifiableTextures => m_Shader.NonModifiableTextures;
	public AssetList<uint>? Offsets_AssetList_UInt32 => m_Shader.Offsets_AssetList_UInt32;
	public AssetList<AssetList<uint>>? Offsets_AssetList_AssetList_UInt32 => m_Shader.Offsets_AssetList_AssetList_UInt32;
	public ISerializedShader? ParsedForm => m_Shader.ParsedForm;

	public Utf8String? PathName
	{
		get => m_Shader.PathName;
		set => m_Shader.PathName = value;
	}

	public AssetList<uint>? Platforms => m_Shader.Platforms;
	public PPtr_Prefab_2018_3? PrefabAsset => m_Shader.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_Shader.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_Shader.PrefabInternal;

	public Utf8String? Script
	{
		get => m_Shader.Script;
		set => m_Shader.Script = value;
	}

	public bool ShaderIsBaked
	{
		get => m_Shader.ShaderIsBaked;
		set => m_Shader.ShaderIsBaked = value;
	}

	public AssetList<uint>? StageCounts => m_Shader.StageCounts;

	public HideFlags HideFlagsE
	{
		get => m_Shader.HideFlagsE;
		set => m_Shader.HideFlagsE = value;
	}

	public IEditorExtension? CorrespondingSourceObjectP
	{
		get => m_Shader.CorrespondingSourceObjectP;
		set => m_Shader.CorrespondingSourceObjectP = value;
	}

	public PPtrAccessList<IPPtr_Shader, IShader> DependenciesP => m_Shader.DependenciesP;
	public IPrefab? PrefabAssetP { get; set; }
	public IPrefabInstance? PrefabInstanceP { get; set; }
	public IPrefabMarker? PrefabInternalP { get; set; }
}
