using AssetRipper.Assets.Cloning;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_1001;
using AssetRipper.SourceGenerated.Classes.ClassID_1001480554;
using AssetRipper.SourceGenerated.Classes.ClassID_130;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.MarkerInterfaces;
using AssetRipper.SourceGenerated.Subclasses.AABB;
using AssetRipper.SourceGenerated.Subclasses.BlendShapeData;
using AssetRipper.SourceGenerated.Subclasses.BoneWeights4;
using AssetRipper.SourceGenerated.Subclasses.CompressedMesh;
using AssetRipper.SourceGenerated.Subclasses.Matrix4x4f;
using AssetRipper.SourceGenerated.Subclasses.MeshBlendShape;
using AssetRipper.SourceGenerated.Subclasses.MeshBlendShapeVertex;
using AssetRipper.SourceGenerated.Subclasses.MeshLodInfo;
using AssetRipper.SourceGenerated.Subclasses.MinMaxAABB;
using AssetRipper.SourceGenerated.Subclasses.PPtr_EditorExtension;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Prefab;
using AssetRipper.SourceGenerated.Subclasses.PPtr_PrefabInstance;
using AssetRipper.SourceGenerated.Subclasses.StreamingInfo;
using AssetRipper.SourceGenerated.Subclasses.SubMesh;
using AssetRipper.SourceGenerated.Subclasses.VariableBoneCountWeights;
using AssetRipper.SourceGenerated.Subclasses.VertexData;
using System.Runtime.CompilerServices;

namespace AssetRipper.Import.AssetCreation.Nikki4;
public class Mesh_Nikki4 : NamedObject_2018_3, IMesh
{
	private Mesh_2019 m_mesh;
	AssetList<float> VarintVertices { get; }
	public Mesh_Nikki4(AssetInfo info) : base(info)
	{
		m_mesh = new Mesh_2019(info);
		VarintVertices = new AssetList<float>();
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		m_mesh.Name = reader.ReadRelease_Utf8StringAlign();
		m_mesh.SubMeshes.ReadRelease_ArrayAlign_Asset<SubMesh_2017_3>(ref reader);
		
		// m_mesh.Shapes.ReadRelease(ref reader);
		m_mesh.Shapes.Vertices.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.BlendShapeVertex.BlendShapeVertex>(ref reader);
		m_mesh.Shapes.Shapes.ReadRelease_ArrayAlign_Asset<MeshBlendShape_4_3>(ref reader);
		m_mesh.Shapes.Channels.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.MeshBlendShapeChannel.MeshBlendShapeChannel>(ref reader);
		m_mesh.Shapes.FullWeights.ReadRelease_ArrayAlign_Single(ref reader);
		VarintVertices.ReadRelease_ArrayAlign_Single(ref reader);
		
		m_mesh.BindPose.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.Matrix4x4f.Matrix4x4f>(ref reader);
		m_mesh.BoneNameHashes.ReadRelease_ArrayAlign_UInt32(ref reader);
		m_mesh.RootBoneNameHash = reader.ReadUInt32();
		m_mesh.BonesAABB.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.MinMaxAABB.MinMaxAABB>(ref reader);
		m_mesh.VariableBoneCountWeights.ReadRelease(ref reader);
		m_mesh.MeshCompression = reader.ReadByte();
		m_mesh.IsReadable = reader.ReadBoolean();
		m_mesh.KeepVertices = reader.ReadBoolean();
		m_mesh.KeepIndices = reader.ReadRelease_BooleanAlign();
		m_mesh.IndexFormat = reader.ReadInt32();
		m_mesh.IndexBuffer = reader.ReadRelease_ArrayAlign_Byte();
		m_mesh.VertexData.ReadRelease_AssetAlign<VertexData_2019>(ref reader);
		m_mesh.CompressedMesh.ReadRelease(ref reader);
		m_mesh.LocalAABB.ReadRelease(ref reader);
		m_mesh.MeshUsageFlags = reader.ReadInt32();
		m_mesh.BakedConvexCollisionMesh = reader.ReadRelease_ArrayAlign_Byte();
		m_mesh.BakedTriangleCollisionMesh = reader.ReadRelease_ArrayAlign_Byte();
		m_mesh.MeshMetrics_0_ = reader.ReadSingle();
		m_mesh.MeshMetrics_1_ = reader.ReadRelease_SingleAlign();
		m_mesh.StreamData.ReadRelease(ref reader);
	}
	public bool Has_BakedConvexCollisionMesh() => m_mesh.Has_BakedConvexCollisionMesh();

	public bool Has_BakedTriangleCollisionMesh() => m_mesh.Has_BakedTriangleCollisionMesh();

	public bool Has_BoneNameHashes() => m_mesh.Has_BoneNameHashes();

	public bool Has_BonesAABB() => m_mesh.Has_BonesAABB();

	public bool Has_CookingOptions() => m_mesh.Has_CookingOptions();

	public bool Has_IndexFormat() => m_mesh.Has_IndexFormat();

	public bool Has_IsReadable() => m_mesh.Has_IsReadable();

	public bool Has_KeepIndices() => m_mesh.Has_KeepIndices();

	public bool Has_KeepVertices() => m_mesh.Has_KeepVertices();

	public bool Has_MeshLodInfo() => m_mesh.Has_MeshLodInfo();

	public bool Has_MeshMetrics_0_() => m_mesh.Has_MeshMetrics_0_();

	public bool Has_MeshMetrics_1_() => m_mesh.Has_MeshMetrics_1_();

	public bool Has_MeshOptimizationFlags() => m_mesh.Has_MeshOptimizationFlags();

	public bool Has_MeshOptimized() => m_mesh.Has_MeshOptimized();

	public bool Has_PrefabAsset() => m_mesh.Has_PrefabAsset();

	public bool Has_PrefabInstance() => m_mesh.Has_PrefabInstance();

	public bool Has_PrefabInternal() => m_mesh.Has_PrefabInternal();

	public bool Has_RootBoneNameHash() => m_mesh.Has_RootBoneNameHash();

	public bool Has_Shapes() => m_mesh.Has_Shapes();

	public bool Has_ShapesList() => m_mesh.Has_ShapesList();

	public bool Has_ShapeVertices() => m_mesh.Has_ShapeVertices();

	public bool Has_Skin() => m_mesh.Has_Skin();

	public bool Has_StreamCompression() => m_mesh.Has_StreamCompression();

	public bool Has_StreamData() => m_mesh.Has_StreamData();

	public bool Has_VariableBoneCountWeights() => m_mesh.Has_VariableBoneCountWeights();

	public void CopyValues(IMesh source, PPtrConverter converter) => m_mesh.CopyValues(source, converter);

	public void CopyValues(IMesh source) => m_mesh.CopyValues(source);

	public byte[]? BakedConvexCollisionMesh
	{
		get => m_mesh.BakedConvexCollisionMesh;
		set => m_mesh.BakedConvexCollisionMesh = value;
	}

	public byte[]? BakedTriangleCollisionMesh
	{
		get => m_mesh.BakedTriangleCollisionMesh;
		set => m_mesh.BakedTriangleCollisionMesh = value;
	}

	public AssetList<Matrix4x4f> BindPose => m_mesh.BindPose;
	public AssetList<uint>? BoneNameHashes => m_mesh.BoneNameHashes;
	public AssetList<MinMaxAABB>? BonesAABB => m_mesh.BonesAABB;
	public ICompressedMesh CompressedMesh => m_mesh.CompressedMesh;

	public int CookingOptions
	{
		get => m_mesh.CookingOptions;
		set => m_mesh.CookingOptions = value;
	}

	public IPPtr_EditorExtension CorrespondingSourceObject => m_mesh.CorrespondingSourceObject;

	public uint HideFlags
	{
		get => m_mesh.HideFlags;
		set => m_mesh.HideFlags = value;
	}

	public byte[] IndexBuffer
	{
		get => m_mesh.IndexBuffer;
		set => m_mesh.IndexBuffer = value;
	}

	public int IndexFormat
	{
		get => m_mesh.IndexFormat;
		set => m_mesh.IndexFormat = value;
	}

	public bool IsReadable
	{
		get => m_mesh.IsReadable;
		set => m_mesh.IsReadable = value;
	}

	public bool KeepIndices
	{
		get => m_mesh.KeepIndices;
		set => m_mesh.KeepIndices = value;
	}

	public bool KeepVertices
	{
		get => m_mesh.KeepVertices;
		set => m_mesh.KeepVertices = value;
	}

	public AABB LocalAABB => m_mesh.LocalAABB;

	public byte MeshCompression
	{
		get => m_mesh.MeshCompression;
		set => m_mesh.MeshCompression = value;
	}

	public MeshLodInfo? MeshLodInfo => m_mesh.MeshLodInfo;

	public float MeshMetrics_0_
	{
		get => m_mesh.MeshMetrics_0_;
		set => m_mesh.MeshMetrics_0_ = value;
	}

	public float MeshMetrics_1_
	{
		get => m_mesh.MeshMetrics_1_;
		set => m_mesh.MeshMetrics_1_ = value;
	}

	public int MeshOptimizationFlags
	{
		get => m_mesh.MeshOptimizationFlags;
		set => m_mesh.MeshOptimizationFlags = value;
	}

	public bool MeshOptimized
	{
		get => m_mesh.MeshOptimized;
		set => m_mesh.MeshOptimized = value;
	}

	public int MeshUsageFlags
	{
		get => m_mesh.MeshUsageFlags;
		set => m_mesh.MeshUsageFlags = value;
	}

	public Utf8String Name_R
	{
		get => m_mesh.Name_R;
		set => m_mesh.Name_R = value;
	}

	public PPtr_Prefab_2018_3? PrefabAsset => m_mesh.PrefabAsset;
	public PPtr_PrefabInstance? PrefabInstance => m_mesh.PrefabInstance;
	public IPPtr_Prefab? PrefabInternal => m_mesh.PrefabInternal;

	public uint RootBoneNameHash
	{
		get => m_mesh.RootBoneNameHash;
		set => m_mesh.RootBoneNameHash = value;
	}

	public IBlendShapeData? Shapes => m_mesh.Shapes;
	public AssetList<MeshBlendShape_4_1>? ShapesList => m_mesh.ShapesList;
	public AssetList<MeshBlendShapeVertex>? ShapeVertices => m_mesh.ShapeVertices;
	public AccessListBase<IBoneWeights4>? Skin => m_mesh.Skin;

	public byte StreamCompression
	{
		get => m_mesh.StreamCompression;
		set => m_mesh.StreamCompression = value;
	}

	public IStreamingInfo? StreamData => m_mesh.StreamData;
	public AccessListBase<ISubMesh> SubMeshes => m_mesh.SubMeshes;
	public VariableBoneCountWeights? VariableBoneCountWeights => m_mesh.VariableBoneCountWeights;
	public IVertexData VertexData => m_mesh.VertexData;

	public HideFlags HideFlagsE
	{
		get => m_mesh.HideFlagsE;
		set => m_mesh.HideFlagsE = value;
	}

	public IndexFormat IndexFormatE
	{
		get => m_mesh.IndexFormatE;
		set => m_mesh.IndexFormatE = value;
	}

	public ModelImporterMeshCompression MeshCompressionE
	{
		get => m_mesh.MeshCompressionE;
		set => m_mesh.MeshCompressionE = value;
	}

	public IEditorExtension? CorrespondingSourceObjectP
	{
		get => m_mesh.CorrespondingSourceObjectP;
		set => m_mesh.CorrespondingSourceObjectP = value;
	}

	public IPrefab? PrefabAssetP
	{
		get => m_mesh.PrefabAssetP;
		set => m_mesh.PrefabAssetP = value;
	}

	public IPrefabInstance? PrefabInstanceP
	{
		get => m_mesh.PrefabInstanceP;
		set => m_mesh.PrefabInstanceP = value;
	}

	public IPrefabMarker? PrefabInternalP
	{
		get => m_mesh.PrefabInternalP;
		set => m_mesh.PrefabInternalP = value;
	}
}
