using AssetRipper.IO.Files.SerializedFiles.IO;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.SerializedFiles.Parser;

/// <summary>
/// 包含一组原始序列化对象数据的信息。
/// </summary>
public struct ObjectInfo
{
	/// <summary>
	/// 5.0.0unk 及更高版本 / 格式版本至少为 14
	/// </summary>
	public static bool IsLongID(FormatVersion generation) => generation >= FormatVersion.Unknown_14;
	/// <summary>
	/// 小于 5.5.0 / 格式版本小于 16
	/// </summary>
	public static bool HasClassID(FormatVersion generation) => generation < FormatVersion.RefactoredClassId;
	/// <summary>
	/// 小于 5.0.0unk / 格式版本小于 11
	/// </summary>
	public static bool HasIsDestroyed(FormatVersion generation) => generation < FormatVersion.HasScriptTypeIndex;
	/// <summary>
	/// 5.0.0unk 到 5.5.0unk 之间（不包括 5.5.0unk）/ 格式版本至少为 11 但小于 17
	/// </summary>
	public static bool HasScriptTypeIndex(FormatVersion generation) => generation >= FormatVersion.HasScriptTypeIndex && generation < FormatVersion.RefactorTypeData;
	/// <summary>
	/// 5.0.1 到 5.5.0unk 之间（不包括 5.5.0unk）/ 格式版本至少为 15 但小于 17
	/// </summary>
	public static bool HasStripped(FormatVersion generation) => generation >= FormatVersion.SupportsStrippedObject && generation < FormatVersion.RefactorTypeData;
	/// <summary>
	/// 5.5.0unk 及更高版本 / 格式版本至少为 17
	/// </summary>
	public static bool HasSerializedTypeIndex(FormatVersion generation) => generation >= FormatVersion.RefactorTypeData;
	/// <summary>
	/// 2020.1.0 及更高版本 / 格式版本至少为 22
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

		// Defer reading the object data until ObjectData is accessed.
		// Holding a SmartStream reference keeps the backing stream alive for lazy reads.
		// CreateReference() gives this ObjectInfo its own refcount contribution, so
		// disposing this ObjectInfo only releases its own reference and does not
		// nullify the stream for any other holder.
		owningStream = ((SmartStream)reader.BaseStream).CreateReference();
		byteOffset = dataOffset + byteStart;
		this.byteSize = byteSize;

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
	/// The stream from which the object data can be lazily read. Held via <see cref="SmartStream.CreateReference"/>
	/// so that releasing it only decrements this <see cref="ObjectInfo"/>'s own refcount contribution.
	/// </summary>
	private SmartStream? owningStream;
	/// <summary>
	/// The absolute offset within <see cref="owningStream"/> of the object data.
	/// </summary>
	private long byteOffset;
	/// <summary>
	/// The size in bytes of the object data.
	/// </summary>
	private int byteSize;
	/// <summary>
	/// The cached object data, or null if it has not yet been read or has been released.
	/// </summary>
	private byte[]? objectData;

	/// <summary>
	/// The data for the object.
	/// </summary>
	/// <remarks>
	/// The data is read lazily from <see cref="owningStream"/> the first time it is accessed.
	/// Once read, it is cached in <see cref="objectData"/> until <see cref="ReleaseObjectData"/> is called.
	/// </remarks>
	[AllowNull]
	public byte[] ObjectData
	{
		get
		{
			if (objectData is not null)
			{
				return objectData;
			}
			// Capture the field into a local so the compiler can track its non-null state
			// through the call. Fields are not assumed to keep their checked value.
			SmartStream? stream = owningStream;
			if (stream is not null)
			{
				objectData = SerializedReader.ReadAssetDataAt(stream, byteOffset, byteSize);
				return objectData;
			}
			return [];
		}
		set => objectData = value;
	}

	/// <summary>
	/// Releases the cached object data so it can be garbage collected.
	/// </summary>
	/// <remarks>
	/// The data can be re-read from <see cref="owningStream"/> after this method is called,
	/// provided the stream has not been released via <see cref="ReleaseStreamReference"/>.
	/// </remarks>
	public void ReleaseObjectData()
	{
		objectData = null;
	}

	/// <summary>
	/// Releases the reference to the owning <see cref="SmartStream"/>.
	/// </summary>
	/// <remarks>
	/// After this is called, <see cref="ObjectData"/> can no longer be lazily read and will
	/// return an empty array. This is intended to be called from <see cref="SerializedFile.Dispose"/>.
	/// </remarks>
	internal void ReleaseStreamReference()
	{
		owningStream?.FreeReference();
		owningStream = null;
	}

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

	public override readonly bool Equals([NotNullWhen(true)] object? obj)
	{
		return obj is ObjectInfo other && Equals(other);
	}

	public readonly bool Equals(ObjectInfo other)
	{
		return FileID == other.FileID
			&& TypeID == other.TypeID
			&& SerializedTypeIndex == other.SerializedTypeIndex
			&& ClassID == other.ClassID
			&& IsDestroyed == other.IsDestroyed
			&& ScriptTypeIndex == other.ScriptTypeIndex
			&& Stripped == other.Stripped
			&& EqualityComparer<SerializedType>.Default.Equals(Type, other.Type)
			&& ObjectData.AsSpan().SequenceEqual(other.ObjectData);
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
		hash.AddBytes(ObjectData);
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
