using AssetRipper.Assets;
using AssetRipper.Assets.Generics;
using AssetRipper.IO.Endian;
using AssetRipper.SourceGenerated.Subclasses.AnimationEvent;
using AssetRipper.SourceGenerated.Subclasses.AssetInfo;
using AssetRipper.SourceGenerated.Subclasses.ComputeShaderKernel;
using AssetRipper.SourceGenerated.Subclasses.DefaultPreset;
using AssetRipper.SourceGenerated.Subclasses.FloatCurve;
using AssetRipper.SourceGenerated.Subclasses.Hash128;
using AssetRipper.SourceGenerated.Subclasses.OffsetPtr_LayerConstant;
using AssetRipper.SourceGenerated.Subclasses.OffsetPtr_StateMachineConstant;
using AssetRipper.SourceGenerated.Subclasses.PPtr_AnimationClip;
using AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorState;
using AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorStateMachine;
using AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorStateTransition;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Material;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Object;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Shader;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Texture;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Texture2D;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Transform;
using AssetRipper.SourceGenerated.Subclasses.PPtrCurve;
using AssetRipper.SourceGenerated.Subclasses.QuaternionCurve;
using AssetRipper.SourceGenerated.Subclasses.RenderPassInfo;
using AssetRipper.SourceGenerated.Subclasses.SampleSettings;
using AssetRipper.SourceGenerated.Subclasses.SecondaryTextureSettings;
using AssetRipper.SourceGenerated.Subclasses.SpriteAtlasData;
using AssetRipper.SourceGenerated.Subclasses.SpriteRenderData;
using AssetRipper.SourceGenerated.Subclasses.SubMesh;
using AssetRipper.SourceGenerated.Subclasses.UnityTexEnv;
using AssetRipper.SourceGenerated.Subclasses.Vector3Curve;
using AssetRipper.SourceGenerated.Subclasses.VertexData;

namespace AssetRipper.Import.AssetCreation.Nikki4;

static class ReadReleaseMethods
{
	public static void ReadRelease_AssetAlign<T>(this T value, ref EndianSpanReader reader) where T : UnityAssetBase
	{
		value.ReadRelease(ref reader);
		reader.Align();
	}

	public static void ReadRelease_AssetAlign<T>(this IVertexData value, ref EndianSpanReader reader) where T : UnityAssetBase
	{
		value.ReadRelease(ref reader);
		reader.Align();
	}

	public static void ReadRelease_Array_Asset<T>(
		this AssetList<T> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Asset<T>(
		this AccessListBase<IOffsetPtr_LayerConstant> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			AssetList<GenericMaskElement> m_GenericMask = new();
			OffsetPtr_LayerConstant_2018_2 addNew = (OffsetPtr_LayerConstant_2018_2)value.AddNew();
			// addNew.ReadRelease(ref reader);
			addNew.Data.StateMachineIndex = reader.ReadUInt32();
			addNew.Data.StateMachineSynchronizedLayerIndex = reader.ReadUInt32();
			addNew.Data.BodyMask.ReadRelease(ref reader);
			addNew.Data.SkeletonMask.ReadRelease(ref reader);
			m_GenericMask.ReadRelease_Array_Asset<GenericMaskElement>(ref reader);
			addNew.Data.Binding = reader.ReadUInt32();
			addNew.Data.LayerBlendingMode = reader.ReadInt32();
			addNew.Data.DefaultWeight = reader.ReadSingle();
			addNew.Data.IKPass = reader.ReadBoolean();
			addNew.Data.SyncedLayerAffectsTiming = reader.ReadRelease_BooleanAlign();
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Asset<T>(
		this AccessListBase<IOffsetPtr_StateMachineConstant> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AssetList<T> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IQuaternionCurve> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IPPtr_AnimationClip> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IPPtr_Transform> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IPPtr_Shader> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IVector3Curve> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IFloatCurve> value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IPPtrCurve>? value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<PPtr_Material_5>? value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<ISubMesh>? value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IPPtr_Material>? value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Asset<T>(
		this AccessListBase<IAnimationEvent>? value,
		ref EndianSpanReader reader)
		where T : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_Pair_Asset_Asset<TKey, TValue>(
		this AssetPair<TKey, TValue> value,
		ref EndianSpanReader reader)
		where TKey : UnityAssetBase, new()
		where TValue : UnityAssetBase, new()
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_PairAlign_Asset_Asset<TKey, TValue>(
		this AssetPair<TKey, TValue> value,
		ref EndianSpanReader reader)
		where TKey : UnityAssetBase, new()
		where TValue : UnityAssetBase, new()
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease(ref reader);
		reader.Align();
	}

	public static void ReadRelease_Map_Asset_Asset<TKey, TValue>(
		this AssetDictionary<TKey, TValue> value,
		ref EndianSpanReader reader)
		where TKey : UnityAssetBase, new()
		where TValue : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Asset_Asset<TKey, TValue>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_MapAlign_Asset_Asset<TKey, TValue>(
		this AssetDictionary<TKey, TValue> value,
		ref EndianSpanReader reader)
		where TKey : UnityAssetBase, new()
		where TValue : UnityAssetBase, new()
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Asset_Asset<TKey, TValue>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_Array_Array_Vector2f(
		this AssetList<AssetList<AssetRipper.SourceGenerated.Subclasses.Vector2f.Vector2f>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Array_Asset<AssetRipper.SourceGenerated.Subclasses.Vector2f.Vector2f>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Array_Vector2Long(
		this AssetList<AssetList<AssetRipper.SourceGenerated.Subclasses.Vector2Long.Vector2Long>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Array_Asset<AssetRipper.SourceGenerated.Subclasses.Vector2Long.Vector2Long>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Int32(this AssetList<int> value, ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadInt32());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Pair_Int32_Single(
		this AssetList<AssetPair<int, float>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_Single(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Pair_Int32_UInt32(
		this AssetList<AssetPair<int, uint>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_UInt32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Pair_PPtr_SphereCollider_PPtr_SphereCollider(
		this AssetList<AssetPair<AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider, AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Asset_Asset<AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider, AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Pair_Utf8StringAlign_PPtr_Object_3_5(
		this AssetList<AssetPair<Utf8String, PPtr_Object_3_5>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Object_3_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Pair_Utf8StringAlign_PPtr_Object_5(
		this AssetList<AssetPair<Utf8String, PPtr_Object_5>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Object_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_SByte(
		this AssetList<sbyte> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadSByte());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Single(
		this AssetList<float> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadSingle());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_UInt16(
		this AssetList<ushort> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadUInt16());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_UInt32(
		this AssetList<uint> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadUInt32());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Array_Utf8StringAlign(
		this AssetList<Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadRelease_Utf8StringAlign());
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_ArrayAlign_ArrayAlign_SerializedPlayerSubProgram(
		this AssetList<AssetList<AssetRipper.SourceGenerated.Subclasses.SerializedPlayerSubProgram.SerializedPlayerSubProgram>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.SerializedPlayerSubProgram.SerializedPlayerSubProgram>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_ArrayAlign_UInt32(
		this AssetList<AssetList<uint>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_ArrayAlign_UInt32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_ArrayAlign_Vector2f(
		this AssetList<AssetList<AssetRipper.SourceGenerated.Subclasses.Vector2f.Vector2f>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.Vector2f.Vector2f>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_ArrayAlign_Vector2Long(
		this AssetList<AssetList<AssetRipper.SourceGenerated.Subclasses.Vector2Long.Vector2Long>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.Vector2Long.Vector2Long>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Boolean(
		this AssetList<bool> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadBoolean());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static byte[] ReadByteArray(ref EndianSpanReader reader, int count)
	{
		return reader.ReadBytesExact(count).ToArray();
	}

	public static byte[] ReadRelease_ArrayAlign_Byte(ref this EndianSpanReader reader)
	{
		int count = reader.ReadInt32();
		byte[] numArray = ReadByteArray(ref reader, count);
		reader.Align();
		return numArray;
	}

	public static void ReadRelease_ArrayAlign_Int16(
		this AssetList<short> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadInt16());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Int32(
		this AssetList<int> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadInt32());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Byte(
		this AssetList<byte> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadByte());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Int64(
		this AssetList<long> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadInt64());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Pair_Int32_Int64_Utf8StringAlign(
		this AssetList<AssetPair<AssetPair<int, long>, Utf8String>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_Int32_Int64_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_PPtr_SphereCollider_PPtr_SphereCollider(
		this AssetList<AssetPair<AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider, AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Asset_Asset<AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider, AssetRipper.SourceGenerated.Subclasses.PPtr_SphereCollider.PPtr_SphereCollider>(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Utf8StringAlign_Boolean(
		this AssetList<AssetPair<Utf8String, bool>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Boolean(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Utf8StringAlign_PPtr_Object_5(
		this AssetList<AssetPair<Utf8String, PPtr_Object_5>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Object_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Utf8StringAlign_PPtr_Texture_3_5(
		this AssetList<AssetPair<Utf8String, PPtr_Texture_3_5>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Texture_3_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Utf8StringAlign_PPtr_Texture_5(
		this AssetList<AssetPair<Utf8String, PPtr_Texture_5>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Texture_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Pair_Utf8StringAlign_UInt32(
		this AssetList<AssetPair<Utf8String, uint>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_UInt32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Single(
		this AssetList<float> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadSingle());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_UInt16(
		this AssetList<ushort> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadUInt16());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_UInt32(
		this AssetList<uint> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadUInt32());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_ArrayAlign_Utf8StringAlign(
		this AssetList<Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.Add(reader.ReadRelease_Utf8StringAlign());
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static bool ReadRelease_BooleanAlign(ref this EndianSpanReader reader)
	{
		int num = reader.ReadBoolean() ? 1 : 0;
		reader.Align();
		return num != 0;
	}

	public static byte ReadRelease_ByteAlign(ref this EndianSpanReader reader)
	{
		int num = (int)reader.ReadByte();
		reader.Align();
		return (byte)num;
	}

	public static short ReadRelease_Int16Align(ref this EndianSpanReader reader)
	{
		int num = (int)reader.ReadInt16();
		reader.Align();
		return (short)num;
	}

	public static int ReadRelease_Int32Align(ref this EndianSpanReader reader)
	{
		int num = reader.ReadInt32();
		reader.Align();
		return num;
	}

	public static long ReadRelease_Int64Align(ref this EndianSpanReader reader)
	{
		long num = reader.ReadInt64();
		reader.Align();
		return num;
	}

	public static void ReadRelease_Map_AssetImporterHashKey_UInt32(
		this AssetDictionary<AssetRipper.SourceGenerated.Subclasses.AssetImporterHashKey.AssetImporterHashKey, uint> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_AssetImporterHashKey_UInt32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_FastPropertyName_Single(
		this AssetDictionary<AssetRipper.SourceGenerated.Subclasses.FastPropertyName.FastPropertyName, float> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_FastPropertyName_Single(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_GUID_Utf8StringAlign(
		this AssetDictionary<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_GUID_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Hash128_5_Int32(
		this AssetDictionary<Hash128_5, int> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Hash128_5_Int32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_AssetBundleFullName(
		this AssetDictionary<int, AssetRipper.SourceGenerated.Subclasses.AssetBundleFullName.AssetBundleFullName> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_AssetBundleFullName(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_AssetBundleInfo(
		this AssetDictionary<int, AssetRipper.SourceGenerated.Subclasses.AssetBundleInfo.AssetBundleInfo> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_AssetBundleInfo(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_Hash128_5(
		this AssetDictionary<int, Hash128_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_Hash128_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_Int32(
		this AssetDictionary<int, int> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_Int32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_SampleSettings_2022_2_0_a17(
		this AssetDictionary<int, SampleSettings_2022_2_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_SampleSettings_2022_2_0_a17(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_SampleSettings_5(
		this AssetDictionary<int, SampleSettings_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_SampleSettings_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_UInt32(
		this AssetDictionary<int, uint> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_UInt32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_Utf8StringAlign(
		this AssetDictionary<int, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int32_VideoClipImporterTargetSettings(
		this AssetDictionary<int, AssetRipper.SourceGenerated.Subclasses.VideoClipImporterTargetSettings.VideoClipImporterTargetSettings> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int32_VideoClipImporterTargetSettings(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Int64_Utf8StringAlign(
		this AssetDictionary<long, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Int64_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int32_SpriteRenderData_4_3(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, int>, SpriteRenderData_4_3> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int32_SpriteRenderData_4_3(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int32_SpriteRenderData_4_5(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, int>, SpriteRenderData_4_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int32_SpriteRenderData_4_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteAtlasData_2017(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteAtlasData_2017_1_1_p0(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_1_1_p0> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_1_1_p0(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteAtlasData_2017_2(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteAtlasData_2017_2_0_b9(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_2_0_b9> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_2_0_b9(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteAtlasData_2020_2(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2020_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2020_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2017(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2017_1_0_b4(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017_1_0_b4> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017_1_0_b4(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2017_3(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017_3> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017_3(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2018(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2018> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2018(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2018_2(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2018_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2018_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_2019(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2019> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2019(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_2(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_4_6(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_4_6> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_4_6(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_5(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_5_3(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_5_3> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_5_3(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_6(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_6_0_b10(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6_0_b10> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6_0_b10(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_5_6_2(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_GUID_Int64_SpriteRenderData_6000_5_0_a7(
		this AssetDictionary<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_6000_5_0_a7> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_6000_5_0_a7(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_UInt16_UInt16_Single(
		this AssetDictionary<AssetPair<ushort, ushort>, float> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_UInt16_UInt16_Single(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Pair_Utf8StringAlign_Utf8StringAlign_PlatformSettingsData_Plugin(
		this AssetDictionary<AssetPair<Utf8String, Utf8String>, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Plugin.PlatformSettingsData_Plugin> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Pair_Utf8StringAlign_Utf8StringAlign_PlatformSettingsData_Plugin(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PPtr_AnimatorState_4_Array_PPtr_AnimatorStateTransition_4(
		this AssetDictionary<PPtr_AnimatorState_4, AssetList<PPtr_AnimatorStateTransition_4>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PPtr_AnimatorState_4_Array_PPtr_AnimatorStateTransition_4(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PPtr_AnimatorStateMachine_5_Array_PPtr_AnimatorTransition(
		this AssetDictionary<PPtr_AnimatorStateMachine_5, AssetList<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PPtr_AnimatorStateMachine_5_Array_PPtr_AnimatorTransition(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PPtr_AnimatorStateMachine_5_ArrayAlign_PPtr_AnimatorTransition(
		this AssetDictionary<PPtr_AnimatorStateMachine_5, AssetList<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PPtr_AnimatorStateMachine_5_ArrayAlign_PPtr_AnimatorTransition(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PPtr_Shader_3_5_Utf8StringAlign(
		this AssetDictionary<PPtr_Shader_3_5, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PPtr_Shader_3_5_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PPtr_Shader_5_Utf8StringAlign(
		this AssetDictionary<PPtr_Shader_5, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PPtr_Shader_5_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PresetType_ArrayAlign_DefaultPreset_2019_3_0_a10(
		this AssetDictionary<AssetRipper.SourceGenerated.Subclasses.PresetType.PresetType, AssetList<DefaultPreset_2019_3_0_a10>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PresetType_ArrayAlign_DefaultPreset_2019_3_0_a10(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_PresetType_ArrayAlign_DefaultPreset_2020_1_0_a23(
		this AssetDictionary<AssetRipper.SourceGenerated.Subclasses.PresetType.PresetType, AssetList<DefaultPreset_2020_1_0_a23>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_PresetType_ArrayAlign_DefaultPreset_2020_1_0_a23(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt32_Utf8StringAlign(
		this AssetDictionary<uint, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt32_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_RenderPassInfo_6000_0_0_b15(
		this AssetDictionary<ulong, RenderPassInfo_6000_0_0_b15> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_RenderPassInfo_6000_0_0_b15(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_RenderPassInfo_6000_1_0_a6(
		this AssetDictionary<ulong, RenderPassInfo_6000_1_0_a6> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_RenderPassInfo_6000_1_0_a6(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_RenderPassInfo_6000_2_0_a8(
		this AssetDictionary<ulong, RenderPassInfo_6000_2_0_a8> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_RenderPassInfo_6000_2_0_a8(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_RenderPassInfo_6000_6(
		this AssetDictionary<ulong, RenderPassInfo_6000_6> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_RenderPassInfo_6000_6(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_RenderStateInfo(
		this AssetDictionary<ulong, AssetRipper.SourceGenerated.Subclasses.RenderStateInfo.RenderStateInfo> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_RenderStateInfo(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_UInt64_VertexLayoutInfo(
		this AssetDictionary<ulong, AssetRipper.SourceGenerated.Subclasses.VertexLayoutInfo.VertexLayoutInfo> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_UInt64_VertexLayoutInfo(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_Array_Utf8StringAlign(
		this AssetDictionary<Utf8String, AssetList<Utf8String>> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Array_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_AssetInfo_3_5(
		this AssetDictionary<Utf8String, AssetInfo_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_AssetInfo_3_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_AssetInfo_5(
		this AssetDictionary<Utf8String, AssetInfo_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_AssetInfo_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_AssetTimeStamp(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.AssetTimeStamp.AssetTimeStamp> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_AssetTimeStamp(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ColorRGBAf(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.ColorRGBAf.ColorRGBAf> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ColorRGBAf(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a16(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2020_1_0_a16> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a16(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a17(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2020_1_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a17(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a9(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2020_1_0_a9> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a9(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2020_2_0_a15(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2020_2_0_a15> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_2_0_a15(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2020_3_2(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2020_3_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_3_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2021(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2021> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2021(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ComputeShaderKernel_2021_1_0_b7(
		this AssetDictionary<Utf8String, ComputeShaderKernel_2021_1_0_b7> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2021_1_0_b7(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_ConfigSetting(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.ConfigSetting.ConfigSetting> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_ConfigSetting(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_GUID(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.GUID.GUID> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_GUID(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_Int32(
		this AssetDictionary<Utf8String, int> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Int32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_Int64(
		this AssetDictionary<Utf8String, long> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Int64(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PlatformSettingsData_Editor(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Editor.PlatformSettingsData_Editor> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PlatformSettingsData_Editor(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PlatformSettingsData_Plugin(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Plugin.PlatformSettingsData_Plugin> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PlatformSettingsData_Plugin(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PPtr_Object_3_5(
		this AssetDictionary<Utf8String, PPtr_Object_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Object_3_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PPtr_Object_5(
		this AssetDictionary<Utf8String, PPtr_Object_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Object_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PPtr_Texture_5(
		this AssetDictionary<Utf8String, PPtr_Texture_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Texture_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PPtr_Texture2D_3_5(
		this AssetDictionary<Utf8String, PPtr_Texture2D_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Texture2D_3_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_PPtr_Texture2D_5(
		this AssetDictionary<Utf8String, PPtr_Texture2D_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_PPtr_Texture2D_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SampleSettings_2022_2_0_a17(
		this AssetDictionary<Utf8String, SampleSettings_2022_2_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SampleSettings_2022_2_0_a17(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SecondaryTextureSettings_2020_2_0_a12(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2020_2_0_a12> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2020_2_0_a12(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SecondaryTextureSettings_2022_2_20(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2022_2_20> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2022_2_20(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SecondaryTextureSettings_2023(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2023> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SecondaryTextureSettings_2023_2_0_a12(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2023_2_0_a12> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023_2_0_a12(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_SecondaryTextureSettings_2023_3_0_a11(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2023_3_0_a11> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023_3_0_a11(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_Single(
		this AssetDictionary<Utf8String, float> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Single(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_UnityTexEnv_5(
		this AssetDictionary<Utf8String, UnityTexEnv_5> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_UnityTexEnv_5(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_Utf8StringAlign(
		this AssetDictionary<Utf8String, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Utf8StringAlign(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_Map_Utf8StringAlign_VideoClipImporterTargetSettings(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.VideoClipImporterTargetSettings.VideoClipImporterTargetSettings> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_VideoClipImporterTargetSettings(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
	}

	public static void ReadRelease_MapAlign_Utf8StringAlign_Boolean(
		this AssetDictionary<Utf8String, bool> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Boolean(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_MapAlign_Utf8StringAlign_Int32(
		this AssetDictionary<Utf8String, int> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_Int32(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_MapAlign_Utf8StringAlign_NonAlignedStruct(
		this AssetDictionary<Utf8String, AssetRipper.SourceGenerated.Subclasses.NonAlignedStruct.NonAlignedStruct> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_NonAlignedStruct(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_MapAlign_Utf8StringAlign_SecondaryTextureSettings_2020_2(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2020_2> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2020_2(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_MapAlign_Utf8StringAlign_SecondaryTextureSettings_2020_2_0_a12(
		this AssetDictionary<Utf8String, SecondaryTextureSettings_2020_2_0_a12> value,
		ref EndianSpanReader reader)
	{
		value.Clear();
		int num1 = reader.ReadInt32();
		int num2 = 0;
		while (num2 < num1)
		{
			value.AddNew().ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2020_2_0_a12(ref reader);
			checked { ++num2; }
		}

		value.Capacity = num1;
		reader.Align();
	}

	public static void ReadRelease_Pair_AssetImporterHashKey_UInt32(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.AssetImporterHashKey.AssetImporterHashKey, uint> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadUInt32();
	}

	public static void ReadRelease_Pair_FastPropertyName_Single(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.FastPropertyName.FastPropertyName, float> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadSingle();
	}

	public static void ReadRelease_Pair_GUID_Int32(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, int> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadInt32();
	}

	public static void ReadRelease_Pair_GUID_Int64(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadInt64();
	}

	public static void ReadRelease_Pair_GUID_Utf8StringAlign(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_Hash128_5_Int32(
		this AssetPair<Hash128_5, int> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadInt32();
	}

	public static void ReadRelease_Pair_Int32_AssetBundleFullName(
		this AssetPair<int, AssetRipper.SourceGenerated.Subclasses.AssetBundleFullName.AssetBundleFullName> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int32_AssetBundleInfo(
		this AssetPair<int, AssetRipper.SourceGenerated.Subclasses.AssetBundleInfo.AssetBundleInfo> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int32_Hash128_5(
		this AssetPair<int, Hash128_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int32_Int32(
		this AssetPair<int, int> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value = reader.ReadInt32();
	}

	public static void ReadRelease_Pair_Int32_Int64(
		this AssetPair<int, long> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value = reader.ReadInt64();
	}

	public static void ReadRelease_Pair_Int32_SampleSettings_2022_2_0_a17(
		this AssetPair<int, SampleSettings_2022_2_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int32_SampleSettings_5(
		this AssetPair<int, SampleSettings_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int32_Single(
		this AssetPair<int, float> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value = reader.ReadSingle();
	}

	public static void ReadRelease_Pair_Int32_UInt32(
		this AssetPair<int, uint> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value = reader.ReadUInt32();
	}

	public static void ReadRelease_Pair_Int32_Utf8StringAlign(
		this AssetPair<int, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_Int32_VideoClipImporterTargetSettings(
		this AssetPair<int, AssetRipper.SourceGenerated.Subclasses.VideoClipImporterTargetSettings.VideoClipImporterTargetSettings> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt32();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Int64_Utf8StringAlign(
		this AssetPair<long, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadInt64();
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_Pair_GUID_Int32_SpriteRenderData_4_3(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, int>, SpriteRenderData_4_3> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int32(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int32_SpriteRenderData_4_5(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, int>, SpriteRenderData_4_5> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int32(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_1_1_p0(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_1_1_p0> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_2(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_2> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2017_2_0_b9(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2017_2_0_b9> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteAtlasData_2020_2(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteAtlasData_2020_2> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017_1_0_b4(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017_1_0_b4> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2017_3(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2017_3> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2018(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2018> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2018_2(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2018_2> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_2019(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_2019> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_2(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_2> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_4_6(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_4_6> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_5(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_5> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_5_3(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_5_3> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6_0_b10(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6_0_b10> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_5_6_2(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_5_6_2> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_GUID_Int64_SpriteRenderData_6000_5_0_a7(
		this AssetPair<AssetPair<AssetRipper.SourceGenerated.Subclasses.GUID.GUID, long>, SpriteRenderData_6000_5_0_a7> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_GUID_Int64(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Pair_Int32_Int64_Utf8StringAlign(
		this AssetPair<AssetPair<int, long>, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_Int32_Int64(ref reader);
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_Pair_UInt16_UInt16_Single(
		this AssetPair<AssetPair<ushort, ushort>, float> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_UInt16_UInt16(ref reader);
		value.Value = reader.ReadSingle();
	}

	public static void ReadRelease_Pair_Pair_Utf8StringAlign_Utf8StringAlign_PlatformSettingsData_Plugin(
		this AssetPair<AssetPair<Utf8String, Utf8String>, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Plugin.PlatformSettingsData_Plugin> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease_Pair_Utf8StringAlign_Utf8StringAlign(ref reader);
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_PPtr_AnimatorState_4_Array_PPtr_AnimatorStateTransition_4(
		this AssetPair<PPtr_AnimatorState_4, AssetList<PPtr_AnimatorStateTransition_4>> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease_Array_Asset<PPtr_AnimatorStateTransition_4>(ref reader);
	}

	public static void ReadRelease_Pair_PPtr_AnimatorStateMachine_5_Array_PPtr_AnimatorTransition(
		this AssetPair<PPtr_AnimatorStateMachine_5, AssetList<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease_Array_Asset<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>(ref reader);
	}

	public static void ReadRelease_Pair_PPtr_AnimatorStateMachine_5_ArrayAlign_PPtr_AnimatorTransition(
		this AssetPair<PPtr_AnimatorStateMachine_5, AssetList<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease_ArrayAlign_Asset<AssetRipper.SourceGenerated.Subclasses.PPtr_AnimatorTransition.PPtr_AnimatorTransition>(ref reader);
	}

	public static void ReadRelease_Pair_PPtr_Shader_3_5_Utf8StringAlign(
		this AssetPair<PPtr_Shader_3_5, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_PPtr_Shader_5_Utf8StringAlign(
		this AssetPair<PPtr_Shader_5, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_PresetType_ArrayAlign_DefaultPreset_2019_3_0_a10(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.PresetType.PresetType, AssetList<DefaultPreset_2019_3_0_a10>> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease_ArrayAlign_Asset<DefaultPreset_2019_3_0_a10>(ref reader);
	}

	public static void ReadRelease_Pair_PresetType_ArrayAlign_DefaultPreset_2020_1_0_a23(
		this AssetPair<AssetRipper.SourceGenerated.Subclasses.PresetType.PresetType, AssetList<DefaultPreset_2020_1_0_a23>> value,
		ref EndianSpanReader reader)
	{
		value.Key.ReadRelease(ref reader);
		value.Value.ReadRelease_ArrayAlign_Asset<DefaultPreset_2020_1_0_a23>(ref reader);
	}

	public static void ReadRelease_Pair_UInt16_UInt16(
		this AssetPair<ushort, ushort> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt16();
		value.Value = reader.ReadUInt16();
	}

	public static void ReadRelease_Pair_UInt32_Utf8StringAlign(
		this AssetPair<uint, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt32();
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_UInt64_RenderPassInfo_6000_0_0_b15(
		this AssetPair<ulong, RenderPassInfo_6000_0_0_b15> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_UInt64_RenderPassInfo_6000_1_0_a6(
		this AssetPair<ulong, RenderPassInfo_6000_1_0_a6> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_UInt64_RenderPassInfo_6000_2_0_a8(
		this AssetPair<ulong, RenderPassInfo_6000_2_0_a8> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_UInt64_RenderPassInfo_6000_6(
		this AssetPair<ulong, RenderPassInfo_6000_6> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_UInt64_RenderStateInfo(
		this AssetPair<ulong, AssetRipper.SourceGenerated.Subclasses.RenderStateInfo.RenderStateInfo> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_UInt64_VertexLayoutInfo(
		this AssetPair<ulong, AssetRipper.SourceGenerated.Subclasses.VertexLayoutInfo.VertexLayoutInfo> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadUInt64();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Array_Utf8StringAlign(
		this AssetPair<Utf8String, AssetList<Utf8String>> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease_Array_Utf8StringAlign(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_AssetInfo_3_5(
		this AssetPair<Utf8String, AssetInfo_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_AssetInfo_5(
		this AssetPair<Utf8String, AssetInfo_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_AssetTimeStamp(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.AssetTimeStamp.AssetTimeStamp> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Boolean(
		this AssetPair<Utf8String, bool> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadBoolean();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ColorRGBAf(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.ColorRGBAf.ColorRGBAf> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a16(
		this AssetPair<Utf8String, ComputeShaderKernel_2020_1_0_a16> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a17(
		this AssetPair<Utf8String, ComputeShaderKernel_2020_1_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_1_0_a9(
		this AssetPair<Utf8String, ComputeShaderKernel_2020_1_0_a9> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_2_0_a15(
		this AssetPair<Utf8String, ComputeShaderKernel_2020_2_0_a15> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2020_3_2(
		this AssetPair<Utf8String, ComputeShaderKernel_2020_3_2> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2021(
		this AssetPair<Utf8String, ComputeShaderKernel_2021> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ComputeShaderKernel_2021_1_0_b7(
		this AssetPair<Utf8String, ComputeShaderKernel_2021_1_0_b7> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_ConfigSetting(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.ConfigSetting.ConfigSetting> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_GUID(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.GUID.GUID> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Int32(
		this AssetPair<Utf8String, int> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadInt32();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Int64(
		this AssetPair<Utf8String, long> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadInt64();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_NonAlignedStruct(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.NonAlignedStruct.NonAlignedStruct> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PlatformSettingsData_Editor(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Editor.PlatformSettingsData_Editor> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PlatformSettingsData_Plugin(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.PlatformSettingsData_Plugin.PlatformSettingsData_Plugin> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Object_3_5(
		this AssetPair<Utf8String, PPtr_Object_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Object_5(
		this AssetPair<Utf8String, PPtr_Object_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Texture_3_5(
		this AssetPair<Utf8String, PPtr_Texture_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Texture_5(
		this AssetPair<Utf8String, PPtr_Texture_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Texture2D_3_5(
		this AssetPair<Utf8String, PPtr_Texture2D_3_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_PPtr_Texture2D_5(
		this AssetPair<Utf8String, PPtr_Texture2D_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SampleSettings_2022_2_0_a17(
		this AssetPair<Utf8String, SampleSettings_2022_2_0_a17> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2020_2(
		this AssetPair<Utf8String, SecondaryTextureSettings_2020_2> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2020_2_0_a12(
		this AssetPair<Utf8String, SecondaryTextureSettings_2020_2_0_a12> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2022_2_20(
		this AssetPair<Utf8String, SecondaryTextureSettings_2022_2_20> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023(
		this AssetPair<Utf8String, SecondaryTextureSettings_2023> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023_2_0_a12(
		this AssetPair<Utf8String, SecondaryTextureSettings_2023_2_0_a12> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_SecondaryTextureSettings_2023_3_0_a11(
		this AssetPair<Utf8String, SecondaryTextureSettings_2023_3_0_a11> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Single(
		this AssetPair<Utf8String, float> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadSingle();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_UInt32(
		this AssetPair<Utf8String, uint> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadUInt32();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_UnityTexEnv_5(
		this AssetPair<Utf8String, UnityTexEnv_5> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static void ReadRelease_Pair_Utf8StringAlign_Utf8StringAlign(
		this AssetPair<Utf8String, Utf8String> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value = reader.ReadRelease_Utf8StringAlign();
	}

	public static void ReadRelease_Pair_Utf8StringAlign_VideoClipImporterTargetSettings(
		this AssetPair<Utf8String, AssetRipper.SourceGenerated.Subclasses.VideoClipImporterTargetSettings.VideoClipImporterTargetSettings> value,
		ref EndianSpanReader reader)
	{
		value.Key = reader.ReadRelease_Utf8StringAlign();
		value.Value.ReadRelease(ref reader);
	}

	public static sbyte ReadRelease_SByteAlign(ref this EndianSpanReader reader)
	{
		int num = (int)reader.ReadSByte();
		reader.Align();
		return (sbyte)num;
	}

	public static float ReadRelease_SingleAlign(ref this EndianSpanReader reader)
	{
		double num = (double)reader.ReadSingle();
		reader.Align();
		return (float)num;
	}

	public static byte[] ReadRelease_TypelessDataAlign(ref this EndianSpanReader reader)
	{
		int count = reader.ReadInt32();
		byte[] numArray = ReadByteArray(ref reader, count);
		reader.Align();
		return numArray;
	}

	public static ushort ReadRelease_UInt16Align(ref this EndianSpanReader reader)
	{
		int num = (int)reader.ReadUInt16();
		reader.Align();
		return (ushort)num;
	}

	public static uint ReadRelease_UInt32Align(ref this EndianSpanReader reader)
	{
		int num = (int)reader.ReadUInt32();
		reader.Align();
		return (uint)num;
	}

	public static ulong ReadRelease_UInt64Align(ref this EndianSpanReader reader)
	{
		long num = (long)reader.ReadUInt64();
		reader.Align();
		return (ulong)num;
	}

	public static Utf8String ReadRelease_Utf8StringAlign(ref this EndianSpanReader reader)
	{
		Utf8String utf8String = reader.ReadUtf8String();
		reader.Align();
		return utf8String;
	}
}
