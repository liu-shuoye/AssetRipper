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

		this.m_ACLClip.ReadRelease(ref reader);
		// Nikki4 魔改引擎在标准 MuscleClip 之外附加了一份 ACL 压缩动画数据（m_ACLClip 及 cache）。
		// 本 fork 的动画曲线由标准 MuscleClip 经 AnimationClipConverter 转换导出，这些字段除
		// ReadRelease 外无任何消费端（grep 确认），读取后立即释放，避免 2957 万 UInt4StorageAligned
		// 等对象驻留堆上（约 1GB）。TODO 若未来实现 ACL 解压导出，需在此恢复数据。
		StripAclData();

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
