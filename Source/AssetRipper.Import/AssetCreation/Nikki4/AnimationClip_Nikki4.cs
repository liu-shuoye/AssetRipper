using AssetRipper.Assets.Generics;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Subclasses.AnimationEvent;
using AssetRipper.SourceGenerated.Subclasses.Clip;
using AssetRipper.SourceGenerated.Subclasses.FloatCurve;
using AssetRipper.SourceGenerated.Subclasses.OffsetPtr_Clip;
using AssetRipper.SourceGenerated.Subclasses.PPtrCurve;
using AssetRipper.SourceGenerated.Subclasses.QuaternionCurve;
using AssetRipper.SourceGenerated.Subclasses.Vector3Curve;
using System.Buffers.Binary;

namespace AssetRipper.Import.AssetCreation.Nikki4;

public class AnimationClip_Nikki4 : AnimationClip_2018_3
{
	// 在 AnimationClip 类中添加字段
	private AssetList<byte> m_aclTransformCache;
	private AssetList<byte> m_aclScalarCache;
	private AssetList<int> m_aclTransformTrackId2CurveId; // 或 uint
	private AssetList<uint> m_aclScalarTrackId2CurveId;
	private ulong m_aclDatabaseHashCache;
	private readonly Clip m_ACLClip;

	/// <summary>
	/// 仅当标准 MuscleClip 三段没有有效曲线、且存在 ACL 压缩轨道时保留。
	/// Blob 已转成连续字节，原 AssetList 仍由 StripAclData 释放，避免长期持有大量小对象。
	/// </summary>
	public AclClipPayload? AclPayload { get; private set; }

	public AnimationClip_Nikki4(AssetInfo info) : base(info)
	{
		m_aclTransformCache = new AssetList<byte>();
		m_aclScalarCache = new AssetList<byte>();
		m_aclTransformTrackId2CurveId = new AssetList<int>();
		m_aclScalarTrackId2CurveId = new AssetList<uint>();
		m_aclDatabaseHashCache = 0;
		m_ACLClip = new Clip();
	}

	public override void ReadRelease(ref EndianSpanReader reader)
	{
		this.Name_C130 = reader.ReadRelease_Utf8StringAlign();
		this.Legacy_C74 = reader.ReadBoolean();
		// ========== 定制版 ACL 字段 ==========
		this.m_aclTransformCache.ReadRelease_ArrayAlign_Byte(ref reader);
		this.m_aclScalarCache.ReadRelease_ArrayAlign_Byte(ref reader);
		this.m_aclTransformTrackId2CurveId.ReadRelease_ArrayAlign_Int32(ref reader);
		this.m_aclScalarTrackId2CurveId.ReadRelease_ArrayAlign_UInt32(ref reader);
		this.m_aclDatabaseHashCache = reader.ReadUInt64();

		this.Compressed_C74 = reader.ReadBoolean();
		this.UseHighQualityCurve_C74 = reader.ReadRelease_BooleanAlign();
		this.RotationCurves_C74.ReadRelease_ArrayAlign_Asset<QuaternionCurve_2018>(ref reader);
		this.CompressedRotationCurves_C74.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.CompressedAnimationCurve.CompressedAnimationCurve>(ref reader);
		this.EulerCurves_C74.ReadRelease_ArrayAlign_Asset<Vector3Curve_2018>(ref reader);
		this.PositionCurves_C74.ReadRelease_ArrayAlign_Asset<Vector3Curve_2018>(ref reader);
		this.ScaleCurves_C74.ReadRelease_ArrayAlign_Asset<Vector3Curve_2018>(ref reader);
		this.FloatCurves_C74.ReadRelease_ArrayAlign_Asset<FloatCurve_2018>(ref reader);
		this.PPtrCurves_C74.ReadRelease_ArrayAlign_Asset<PPtrCurve_2017>(ref reader);
		this.SampleRate_C74 = reader.ReadSingle();
		this.WrapMode_C74 = reader.ReadInt32();
		this.Bounds_C74.ReadRelease(ref reader);
		this.MuscleClipSize_C74 = reader.ReadUInt32();
		// this.MuscleClip_C74.ReadRelease(ref reader);

		this.MuscleClip_C74.StartX.ReadRelease(ref reader);
		this.MuscleClip_C74.StopX?.ReadRelease(ref reader);
		this.MuscleClip_C74.AverageSpeed3?.ReadRelease(ref reader);
		var Clip = (OffsetPtr_Clip_2018_3)this.MuscleClip_C74.Clip;
		// Clip.ReadRelease(ref reader);
		Clip.Data.StreamedClip.ReadRelease(ref reader);
		Clip.Data.DenseClip.ReadRelease(ref reader);
		Clip.Data.ConstantClip?.ReadRelease(ref reader);
		// 空壳 clip 的标准三段只有空帧/空数组，真正的曲线在后面的 ACL 段。
		bool standardClipEmpty = Clip.Data.StreamedClip.Data.Count <= 2
			&& Clip.Data.DenseClip.CurveCount == 0
			&& (!Clip.Data.Has_ConstantClip() || Clip.Data.ConstantClip.Data.Count == 0);

		this.m_ACLClip.ReadRelease(ref reader);
		// Nikki4 魔改引擎在标准 MuscleClip 之外附加了一份 ACL 压缩动画数据（m_ACLClip 及 cache）。
		// 标准三段为空时，这里才是唯一曲线来源；先在 ACL 段读完后保留连续 blob 与映射表，
		// 其余大对象仍交给 StripAclData 释放。处理阶段消费完可调用 ClearAclPayload 提前释放。

		this.MuscleClip_C74.StartTime = reader.ReadSingle();
		this.MuscleClip_C74.StopTime = reader.ReadSingle();
		this.MuscleClip_C74.OrientationOffsetY = reader.ReadSingle();
		this.MuscleClip_C74.Level = reader.ReadSingle();
		this.MuscleClip_C74.CycleOffset = reader.ReadSingle();
		this.MuscleClip_C74.AverageAngularSpeed = reader.ReadSingle();
		this.MuscleClip_C74.IndexArray.ReadRelease_Array_Int32(ref reader);
		this.MuscleClip_C74.ValueArrayDelta.ReadRelease_Array_Asset<AssetRipper.SourceGenerated.Subclasses.ValueDelta.ValueDelta>(ref reader);
		this.MuscleClip_C74.ValueArrayReferencePose?.ReadRelease_Array_Single(ref reader);

		this.MuscleClip_C74.LoopTime = reader.ReadBoolean();
		this.MuscleClip_C74.LoopBlend = reader.ReadBoolean();
		this.MuscleClip_C74.LoopBlendOrientation = reader.ReadBoolean();
		this.MuscleClip_C74.LoopBlendPositionY = reader.ReadBoolean();
		this.MuscleClip_C74.LoopBlendPositionXZ = reader.ReadBoolean();
		this.MuscleClip_C74.KeepOriginalOrientation = reader.ReadBoolean();
		this.MuscleClip_C74.KeepOriginalPositionY = reader.ReadBoolean();
		this.MuscleClip_C74.KeepOriginalPositionXZ = reader.ReadBoolean();

		this.MuscleClip_C74.DeltaPose.ReadRelease(ref reader);
		this.MuscleClip_C74.LeftFootStartX.ReadRelease(ref reader);
		this.MuscleClip_C74.RightFootStartX.ReadRelease(ref reader);
		this.MuscleClip_C74.Mirror = reader.ReadBoolean();

		this.MuscleClip_C74.StartAtOrigin = reader.ReadBoolean();
		this.MuscleClip_C74.HeightFromFeet = reader.ReadRelease_BooleanAlign();


		this.ClipBindingConstant_C74.ReadRelease(ref reader);
		this.HasGenericRootTransform_C74 = reader.ReadBoolean();
		this.HasMotionFloatCurves_C74 = reader.ReadRelease_BooleanAlign();
		this.Events_C74.ReadRelease_ArrayAlign_Asset<AnimationEvent_5>(ref reader);

		if (standardClipEmpty && m_ACLClip.CompressedTransformTracks.Count > 0)
		{
			AclPayload = BuildAclPayload();
		}
		StripAclData();
	}

	/// <summary>
	/// 把 ACL 轨道数组压成连续 blob，并把映射表复制成紧凑结构。
	/// 注意：每个 UInt4StorageAligned 是 16 字节，ACL 原始数据按 16 字节补齐。
	/// </summary>
	private AclClipPayload BuildAclPayload()
	{
		int trackCount = m_ACLClip.CompressedTransformTracks.Count;
		byte[] blob = new byte[checked(trackCount * 16)];
		int offset = 0;
		foreach (UInt4StorageAligned entry in m_ACLClip.CompressedTransformTracks)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset + 0), entry.X);
			BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset + 4), entry.Y);
			BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset + 8), entry.Z);
			BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(offset + 12), entry.W);
			offset += 16;
		}

		int mappingCount = m_ACLClip.AclTransformTrackIDToBindingCurveID.Count;
		AclBindingCurveIds[] mapping = new AclBindingCurveIds[mappingCount];
		for (int i = 0; i < mappingCount; i++)
		{
			AclTransformTrackIDToBindingCurveID entry = m_ACLClip.AclTransformTrackIDToBindingCurveID[i];
			mapping[i] = new AclBindingCurveIds(
				entry.RotationIDToBindingCurveID,
				entry.PositionIDToBindingCurveID,
				entry.ScaleIDToBindingCurveID);
		}

		return new AclClipPayload(blob, mapping);
	}

	/// <summary>
	/// Processing 层消费完 ACL 曲线后调用，尽快释放 blob。
	/// </summary>
	public void ClearAclPayload()
	{
		AclPayload = null;
	}

	/// <summary>
	/// 释放 ACL 压缩动画数据：m_aclTransformCache/m_aclScalarCache 原始字节、轨道映射数组，
	/// 以及 m_ACLClip 内的压缩轨道与 LOD 列表。这些对象仅本次反序列化使用，之后不被任何
	/// 导出/处理代码读取，立即清空让 GC 回收，降低动画剪辑的内存驻留。
	/// </summary>
	private void StripAclData()
	{
		m_aclTransformCache.Clear();
		m_aclScalarCache.Clear();
		m_aclTransformTrackId2CurveId.Clear();
		m_aclScalarTrackId2CurveId.Clear();
		m_ACLClip.CompressedTransformTracks.Clear();
		m_ACLClip.CompressedScalarTracks.Clear();
		m_ACLClip.AclTransformTrackIDToBindingCurveID.Clear();
		m_ACLClip.AclScalarTrackIDToBindingCurveID.Clear();
		m_ACLClip.decompressTrackLOD.Clear();
	}
}
