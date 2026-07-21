using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.SerializedFiles.IO;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.SerializedFiles.Parser;

/// <summary>
/// 序列化文件元数据
/// </summary>
public sealed class SerializedFileMetadata
{
	private SmartStream _stream;

	/// <summary>
	/// Less than 3.5.0
	/// </summary>
	public static bool HasEndian(FormatVersion generation) => generation < FormatVersion.Unknown_9;
	/// <summary>
	/// Less than 3.5.0
	/// </summary>
	public static bool IsMetadataAtTheEnd(FormatVersion generation) => generation < FormatVersion.Unknown_9;

	/// <summary>
	/// 3.0.0b 及更高版本
	/// </summary>
	public static bool HasSignature(FormatVersion generation) => generation >= FormatVersion.Unknown_7;
	/// <summary>
	/// 3.0.0 and greater
	/// </summary>
	public static bool HasPlatform(FormatVersion generation) => generation >= FormatVersion.Unknown_8;
	/// <summary>
	/// 5.0.0Unk2 and greater
	/// </summary>
	public static bool HasEnableTypeTree(FormatVersion generation) => generation >= FormatVersion.HasTypeTreeHashes;
	/// <summary>
	/// 3.0.0b to 4.x.x
	/// </summary>
	public static bool HasLongFileID(FormatVersion generation) => generation >= FormatVersion.Unknown_7 && generation < FormatVersion.Unknown_14;
	/// <summary>
	/// 5.0.0Unk0 and greater
	/// </summary>
	public static bool HasScriptTypes(FormatVersion generation) => generation >= FormatVersion.HasScriptTypeIndex;
	/// <summary>
	/// 1.2.0 and greater
	/// </summary>
	public static bool HasUserInformation(FormatVersion generation) => generation >= FormatVersion.Unknown_5;
	/// <summary>
	/// 2019.2 and greater
	/// </summary>
	public static bool HasRefTypes(FormatVersion generation) => generation >= FormatVersion.SupportsRefObject;

	public void Read(SmartStream stream, SerializedFileHeader header)
	{
		bool swapEndianess = ReadSwapEndianess(stream, header);
		EndianType endianess = swapEndianess ? EndianType.BigEndian : EndianType.LittleEndian;
		using SerializedReader reader = new SerializedReader(stream, endianess, header.Version);
		Read(reader, header.DataOffset);
	}

	/// <summary>
	/// 读取交换端序
	/// </summary>
	private bool ReadSwapEndianess(SmartStream stream, SerializedFileHeader header)
	{
		if (HasEndian(header.Version))
		{
			int num = stream.ReadByte();
			//This is not and should not be aligned.
			//Aligment only happens for the endian boolean on version 9 and greater.
			//This coincides with endianess being stored in the header on version 9 and greater.
			return num switch
			{
				< 0 => throw new EndOfStreamException(),
				_ => SwapEndianess = num != 0,
			};
		}
		else
		{
			return header.Endianess;
		}
	}

	private void Read(SerializedReader reader, long dataOffset)
	{
		if (HasSignature(reader.Generation))
		{
			string signature = reader.ReadStringZeroTerm();
			if (!UnityVersion.TryParse(signature, out UnityVersion version, out _))
			{
				// 如果无法解析，则假设版本为已剥离。
				version = default;
			}
			UnityVersion = version;
			reader.Version = version;
		}
		if (HasPlatform(reader.Generation))
		{
			TargetPlatform = (BuildTarget)reader.ReadUInt32();
		}

		EnableTypeTree = ReadEnableTypeTree(reader);

		Types = reader.ReadSerializedTypeArray<SerializedType>(EnableTypeTree);

		if (HasLongFileID(reader.Generation))
		{
			LongFileID = reader.ReadUInt32();
		}

		Object = reader.ReadObjectInfoArray(LongFileID != 0, Types, dataOffset);

		if (HasScriptTypes(reader.Generation))
		{
			ScriptTypes = reader.ReadLocalSerializedObjectIdentifierArray();
		}

		Externals = reader.ReadFileIdentifierArray();

		if (HasRefTypes(reader.Generation))
		{
			RefTypes = reader.ReadSerializedTypeArray<SerializedTypeReference>(EnableTypeTree);
		}
		if (HasUserInformation(reader.Generation))
		{
			UserInformation = reader.ReadStringZeroTerm();
		}
	}

	private static bool ReadEnableTypeTree(SerializedReader reader)
	{
		if (HasEnableTypeTree(reader.Generation))
		{
			return reader.ReadBoolean();
		}
		else
		{
			return true;
		}
	}

	internal void Write(SerializedWriter writer)
	{
		if (HasEndian(writer.Generation))
		{
			writer.Write(writer.EndianType == EndianType.BigEndian ? (byte)1 : (byte)0);
		}
		if (HasSignature(writer.Generation))
		{
			writer.WriteStringZeroTerm(UnityVersion.ToString());
		}
		if (HasPlatform(writer.Generation))
		{
			writer.Write((uint)TargetPlatform);
		}
		if (HasEnableTypeTree(writer.Generation))
		{
			writer.Write(EnableTypeTree);
		}

		bool enableTypeTree = !HasEnableTypeTree(writer.Generation) || EnableTypeTree;
		writer.WriteSerializedTypeArray(Types, enableTypeTree);
		if (HasLongFileID(writer.Generation))
		{
			writer.Write(LongFileID);
		}

		writer.WriteObjectInfoArray(Object);

		if (HasScriptTypes(writer.Generation))
		{
			writer.WriteLocalSerializedObjectIdentifierArray(ScriptTypes);
		}
		writer.WriteFileIdentifierArray(Externals);
		if (HasRefTypes(writer.Generation))
		{
			writer.WriteSerializedTypeArray(RefTypes, EnableTypeTree);
		}
		if (HasUserInformation(writer.Generation))
		{
			writer.WriteStringZeroTerm(UserInformation);
		}
	}

	/// <summary> Unity 版本 </summary>
	public UnityVersion UnityVersion { get; set; }
	/// <summary> 目标平台 </summary>
	public BuildTarget TargetPlatform { get; set; }
	/// <summary> 是否启用类型树 </summary>
	public bool EnableTypeTree { get; set; }
	public SerializedType[] Types { get; set; } = [];
	/// <summary>
	/// 表示 <see cref="ObjectInfo.FileID"/> 为 8 字节大小<br/>
	/// 启用此字段的序列化文件理论上不存在
	/// </summary>
	public uint LongFileID { get; set; }
	public bool SwapEndianess { get; set; }
	/// <summary> 对象信息 </summary>
	public ObjectInfo[] Object { get; set; } = [];
	public LocalSerializedObjectIdentifier[] ScriptTypes { get; set; } = [];
	public FileIdentifier[] Externals { get; set; } = [];
	public Utf8String UserInformation { get; set; } = Utf8String.Empty;
	public SerializedTypeReference[] RefTypes { get; set; } = [];
}
