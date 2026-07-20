using AssetRipper.IO.Files.SerializedFiles.IO;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.SerializedFiles.Parser;

/// <summary>
/// Contains information for a block of raw serialized object data.
/// </summary>
public struct ObjectInfo
{
	/// <summary>
	/// 5.0.0unk and greater / Format Version at least 14
	/// </summary>
	public static bool IsLongID(FormatVersion generation) => generation >= FormatVersion.Unknown_14;
	/// <summary>
	/// Less than 5.5.0 / Format Version less than 16
	/// </summary>
	public static bool HasClassID(FormatVersion generation) => generation < FormatVersion.RefactoredClassId;
	/// <summary>
	/// Less than 5.0.0unk / Format Version less than 11
	/// </summary>
	public static bool HasIsDestroyed(FormatVersion generation) => generation < FormatVersion.HasScriptTypeIndex;
	/// <summary>
	/// 5.0.0unk to 5.5.0unk exclusive / Format Version at least 11 but less than 17
	/// </summary>
	public static bool HasScriptTypeIndex(FormatVersion generation) => generation >= FormatVersion.HasScriptTypeIndex && generation < FormatVersion.RefactorTypeData;
	/// <summary>
	/// 5.0.1 to 5.5.0unk exclusive / Format Version at least 15 but less than 17
	/// </summary>
	public static bool HasStripped(FormatVersion generation) => generation >= FormatVersion.SupportsStrippedObject && generation < FormatVersion.RefactorTypeData;
	/// <summary>
	/// 5.5.0unk and greater / Format Version at least 17
	/// </summary>
	public static bool HasSerializedTypeIndex(FormatVersion generation) => generation >= FormatVersion.RefactorTypeData;
	/// <summary>
	/// 2020.1.0 and greater / Format Version at least 22
	/// </summary>
	public static bool HasLargeFilesSupport(FormatVersion generation) => generation >= FormatVersion.LargeFilesSupport;

	internal void Read(SerializedReader reader, bool longFileID, ReadOnlySpan<SerializedType> types, long dataOffset)
	{
		if (IsLongID(reader.Generation))
		{
			reader.AlignStream();
			FileID = reader.ReadInt64();
		}
		else
		{
			FileID = reader.ReadInt32();
		}

		/// <summary>
		/// Offset to the object data.<br/>
		/// Add to <see cref="SerializedFileHeader.DataOffset"/> to get the absolute offset within the serialized file.
		/// </summary>
		long byteStart;
		if (HasLargeFilesSupport(reader.Generation))
		{
			byteStart = reader.ReadInt64();
		}
		else
		{
			byteStart = reader.ReadUInt32();
		}

		// Size of the object data.
		int byteSize = reader.ReadInt32();

		// 懒加载：仅记录偏移和长度，并持有 SmartStream 引用，
		// 避免在读取阶段立即分配大块 byte[]，减少峰值内存占用。
		// 真正的字节读取推迟到 LoadObjectData() 被调用时。
		_dataStream = (reader.BaseStream as SmartStream)?.CreateReference();
		_dataAbsoluteOffset = dataOffset + byteStart;
		_dataSize = byteSize;

		if (HasSerializedTypeIndex(reader.Generation))
		{
			SerializedTypeIndex = reader.ReadInt32();
		}
		else
		{
			SerializedTypeIndex = -1;
			TypeID = reader.ReadInt32();
		}
		if (HasClassID(reader.Generation))
		{
			ClassID = reader.ReadInt16();
		}
		if (HasScriptTypeIndex(reader.Generation))
		{
			ScriptTypeIndex = reader.ReadInt16();
		}
		else if (HasIsDestroyed(reader.Generation))
		{
			IsDestroyed = reader.ReadUInt16();
		}
		bool? stripped;
		if (HasStripped(reader.Generation))
		{
			Stripped = reader.ReadBoolean();
			stripped = Stripped;
		}
		else
		{
			Stripped = false;
			stripped = null;
		}
		Type = GetSerializedType(types, stripped);
		if (Type is not null)
		{
			TypeID = Type.TypeID;
			if (!HasClassID(reader.Generation) && Type.TypeID >= short.MinValue && Type.TypeID <= short.MaxValue)
			{
				ClassID = (short)Type.TypeID;
			}
			if (!HasScriptTypeIndex(reader.Generation))
			{
				ScriptTypeIndex = Type.ScriptTypeIndex;
			}
			if (!HasStripped(reader.Generation))
			{
				Stripped = Type.IsStrippedType;
			}
		}
	}

	internal readonly void Write(SerializedWriter writer, long byteStart)
	{
		if (IsLongID(writer.Generation))
		{
			writer.AlignStream();
			writer.Write(FileID);
		}
		else
		{
			writer.Write((int)FileID);
		}

		if (HasLargeFilesSupport(writer.Generation))
		{
			writer.Write(byteStart);
		}
		else
		{
			writer.Write((uint)byteStart);
		}

		writer.Write(ObjectData.Length);
		if (HasSerializedTypeIndex(writer.Generation))
		{
			writer.Write(SerializedTypeIndex);
		}
		else
		{
			writer.Write(TypeID);
		}
		if (HasClassID(writer.Generation))
		{
			writer.Write(ClassID);
		}
		if (HasScriptTypeIndex(writer.Generation))
		{
			writer.Write(ScriptTypeIndex);
		}
		else if (HasIsDestroyed(writer.Generation))
		{
			writer.Write(IsDestroyed);
		}
		if (HasStripped(writer.Generation))
		{
			writer.Write(Stripped);
		}
	}

	public override readonly string ToString()
	{
		return $"{ClassID}[{FileID}]";
	}

	private readonly SerializedType? GetSerializedType(ReadOnlySpan<SerializedType> types, bool? stripped)
	{
		if (SerializedTypeIndex >= 0)
		{
			return types[SerializedTypeIndex];
		}
		else if (types.Length == 0)
		{
			return default; //It's common on Unity 4 and lower for the array to be empty.
		}
		else
		{
			SerializedType? result = null;
			foreach (SerializedType type in types)
			{
				if (type.TypeID == TypeID)
				{
					if (stripped.HasValue && type.IsStrippedType != stripped.Value)
					{
						// If the caller specified a stripped value, skip types that don't match it.
					}
					else if (result is null)
					{
						result = type;
					}
					else
					{
						throw new Exception($"Multiple types with the same ID {TypeID} and stripped {Stripped} found");
					}
				}
			}
			return result ?? throw new Exception($"Type with ID {TypeID} and stripped {Stripped} not found");
		}
	}

	/// <summary>
	/// ObjectID<br/>
	/// Unique ID that identifies the object. Can be used as a key for a map.
	/// </summary>
	public long FileID { get; set; }
	/// <summary>
	/// Type ID of the object, which is mapped to <see cref="SerializedType.TypeID"/><br/>
	/// Equals to classID if the object is not MonoBehaviour"/>
	/// </summary>
	public int TypeID { get; set; }
	/// <summary>
	/// Type index in <see cref="SerializedFileMetadata.Types"/> array<br/>
	/// </summary>
	public int SerializedTypeIndex { get; set; }
	/// <summary>
	/// Class ID of the object.
	/// </summary>
	public short ClassID { get; set; }
	public ushort IsDestroyed { get; set; }
	public short ScriptTypeIndex { get; set; }
	public bool Stripped { get; set; }
	public SerializedType? Type { get; set; }
	/// <summary>
	/// The data for the object.
	/// </summary>
	[AllowNull]
	public byte[] ObjectData { readonly get => field ?? []; set; }

	// 懒加载相关字段：持有 SmartStream 引用与数据位置信息，
	// 让 Read 阶段不必立即分配 byte[]，将内存峰值降到与单个对象大小相关而非全部对象总和。
	private SmartStream? _dataStream;
	private long _dataAbsoluteOffset;
	private int _dataSize;

	public ObjectInfo()
	{
	}

	public ObjectInfo(SerializedType type)
	{
		Type = type;
		TypeID = type.TypeID;
		if (type.TypeID >= short.MinValue && type.TypeID <= short.MaxValue)
		{
			ClassID = (short)type.TypeID;
		}
		ScriptTypeIndex = type.ScriptTypeIndex;
		Stripped = type.IsStrippedType;
	}

	/// <summary>
	/// 按需从底层 SmartStream 读取对象二进制数据。
	/// 若之前已通过 <see cref="ObjectData"/> 属性显式赋值，则返回该值；
	/// 否则使用懒加载路径从底层流按需读取，以减少读取阶段一次性内存占用。
	/// </summary>
	/// <returns>对象二进制数据；若无任何数据来源则返回空数组。</returns>
	public readonly byte[] LoadObjectData()
	{
		// 优先返回已显式赋值的 ObjectData，兼容 Write 路径与外部赋值场景。
		// ObjectData getter 在 field 为 null 时返回 []，因此 Length == 0 可同时覆盖 null 与空数组。
		byte[] currentData = ObjectData;
		if (currentData.Length > 0)
		{
			return currentData;
		}

		// 懒加载路径：从 SmartStream 按需读取。
		// 读取前后恢复 Position，避免影响其他读取者对同一流的共享使用。
		// 注意：这里修改的是 SmartStream（引用类型）的状态，不会触发 struct 的防御性拷贝。
		if (_dataStream is not null && !_dataStream.IsNull)
		{
			byte[] buffer = new byte[_dataSize];
			long originalPosition = _dataStream.Position;
			_dataStream.Position = _dataAbsoluteOffset;
			_dataStream.ReadExactly(buffer);
			_dataStream.Position = originalPosition;
			return buffer;
		}

		return [];
	}

	/// <summary>
	/// 显式释放底层 SmartStream 引用，使引用计数下降。
	/// 调用后 <see cref="LoadObjectData"/> 将回退到 <see cref="ObjectData"/> 字段。
	/// </summary>
	public void ReleaseDataStream()
	{
		if (_dataStream is not null)
		{
			_dataStream.FreeReference();
			_dataStream = null;
		}
	}

	public override readonly bool Equals([NotNullWhen(true)] object? obj)
	{
		return obj is ObjectInfo other && Equals(other);
	}

	public readonly bool Equals(ObjectInfo other)
	{
		// 当双方都持有 SmartStream 时，按 (offset, size) 比较，避免触发流读取；
		// 否则退化到字节比较，通过 LoadObjectData 兼容懒加载与已赋值两种情况，
		// 保证读取后的 ObjectInfo 与构造赋值的 ObjectInfo 能正确相等（用于测试与序列化往返一致性）。
		bool dataEqual = (_dataStream is not null && other._dataStream is not null)
			? _dataAbsoluteOffset == other._dataAbsoluteOffset && _dataSize == other._dataSize
			: LoadObjectData().AsSpan().SequenceEqual(other.LoadObjectData());

		return FileID == other.FileID
			&& TypeID == other.TypeID
			&& SerializedTypeIndex == other.SerializedTypeIndex
			&& ClassID == other.ClassID
			&& IsDestroyed == other.IsDestroyed
			&& ScriptTypeIndex == other.ScriptTypeIndex
			&& Stripped == other.Stripped
			&& EqualityComparer<SerializedType>.Default.Equals(Type, other.Type)
			&& dataEqual;
	}

	public override readonly int GetHashCode()
	{
		HashCode hash = new();
		hash.Add(FileID);
		hash.Add(TypeID);
		hash.Add(SerializedTypeIndex);
		hash.Add(ClassID);
		hash.Add(IsDestroyed);
		hash.Add(ScriptTypeIndex);
		hash.Add(Stripped);
		hash.Add(Type);
		// 与 Equals 保持一致：通过 LoadObjectData 取真实字节计算 hash，
		// 确保 Equals 相等的两个 ObjectInfo 实例具有相同 hash code。
		hash.AddBytes(LoadObjectData());
		return hash.ToHashCode();
	}

	public static bool operator ==(ObjectInfo left, ObjectInfo right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(ObjectInfo left, ObjectInfo right)
	{
		return !(left == right);
	}
}
