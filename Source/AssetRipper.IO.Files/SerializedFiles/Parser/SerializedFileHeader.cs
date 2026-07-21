using AssetRipper.IO.Endian;

namespace AssetRipper.IO.Files.SerializedFiles.Parser;

/// <summary>
/// 文件头位于资源文件的开头，始终采用大端序字节顺序。
/// </summary>
public sealed record class SerializedFileHeader
{
	/// <summary>
	/// 文件元数据部分的大小
	/// </summary>
	public long MetadataSize { get; set; }
	/// <summary>
	/// 文件总大小
	/// </summary>
	public long FileSize { get; set; }
	/// <summary>
	/// 文件格式版本。该数字用于向后兼容，并且通常在文件格式在主要更新后增加
	/// </summary>
	public FormatVersion Version { get; set; }
	/// <summary>
	/// 序列化对象数据的偏移量。它从第一个对象的数据开始
	/// </summary>
	public long DataOffset { get; set; }
	/// <summary>
	/// 恰好控制数据结构的字节顺序。该字段通常设置为0，可能表示小端字节顺序。
	/// </summary>
	public bool Endianess { get; set; }

	public const int HeaderMinSize = 16;

	public const int MetadataMinSize = 13;


	/// <summary>
	/// 3.5.0及更高版本 / 格式版本9 +
	/// </summary>
	public static bool HasEndianess(FormatVersion generation) => generation >= FormatVersion.Unknown_9;

	/// <summary>
	/// 2020.1.0及更高版本 / 格式版本22 +
	/// </summary>
	public static bool HasLargeFilesSupport(FormatVersion generation) => generation >= FormatVersion.LargeFilesSupport;

	public static bool IsSerializedFileHeader(EndianReader reader, long fileSize)
	{
		long initialPosition = reader.BaseStream.Position;

		//先检查这里是否有足够的空间。
		if (reader.BaseStream.Position + HeaderMinSize > reader.BaseStream.Length)
		{
			return false;
		}

		//Pre-22格式： 
		// - 元数据大小
		// - 文件大小
		// - Generation
		int metadataSize = reader.ReadInt32();
		ulong headerDefinedFileSize = reader.ReadUInt32();

		// 先读取Generation，因为格式在gen 22（unity 2020）中发生了巨大变化
		// Generation is always at [base + 0x8]
		FormatVersion generation = (FormatVersion)reader.ReadInt32();
		if (!Enum.IsDefined(generation))
		{
			reader.BaseStream.Position = initialPosition;
			return false;
		}

		if (generation >= FormatVersion.LargeFilesSupport)
		{
			//22 Format:
			//First known value is at 0x14, and is metadata size as a 32-bit integer.
			//Then the file size as a 64-bit integer.
			reader.BaseStream.Position = initialPosition + 0x14;
			metadataSize = reader.ReadInt32();
			headerDefinedFileSize = reader.ReadUInt64();
		}

		reader.BaseStream.Position = initialPosition;

		return metadataSize >= MetadataMinSize
			&& headerDefinedFileSize >= HeaderMinSize + MetadataMinSize
			&& fileSize >= 0
			&& headerDefinedFileSize == (ulong)fileSize;
	}

	public void Read(Stream stream)
	{
		using EndianReader reader = new(stream, EndianType.BigEndian);
		Read(reader);
	}

	public void Read(EndianReader reader)
	{
		//For gen 22+ these will be zero
		MetadataSize = reader.ReadInt32();
		FileSize = reader.ReadUInt32();

		//Read generation
		Version = (FormatVersion)reader.ReadInt32();

		//For gen 22+ this will be zero
		DataOffset = reader.ReadUInt32();

		if (HasEndianess(Version))
		{
			Endianess = reader.ReadBoolean();
			reader.AlignStream();
		}
		if (HasLargeFilesSupport(Version))
		{
			MetadataSize = reader.ReadUInt32();
			FileSize = reader.ReadInt64();
			DataOffset = reader.ReadInt64();
			reader.ReadInt64(); // unknown
		}

		if (MetadataSize <= 0)
		{
			throw new Exception($"Invalid metadata size {MetadataSize}");
		}

		if (!Enum.IsDefined(Version))
		{
			throw new Exception($"Unsupported file generation {Version}'");
		}
	}

	public void Write(Stream stream)
	{
		using EndianWriter writer = new(stream, EndianType.BigEndian);
		Write(writer);
	}

	public void Write(EndianWriter writer)
	{
		//0x00
		if (HasLargeFilesSupport(Version))
		{
			writer.Write(0);
			writer.Write(0u);
		}
		else
		{
			writer.Write((int)MetadataSize);
			writer.Write((uint)FileSize);
		}

		//0x08
		writer.Write((int)Version);

		//0x0c
		if (HasLargeFilesSupport(Version))
		{
			writer.Write(0u);
		}
		else
		{
			writer.Write((uint)DataOffset);
		}

		//0x10
		if (HasEndianess(Version))
		{
			writer.Write(Endianess);
			writer.AlignStream();
		}

		//0x14
		if (HasLargeFilesSupport(Version))
		{
			writer.Write((uint)MetadataSize);
			writer.Write(FileSize);
			writer.Write(DataOffset);
			writer.Write(0L);
		}
	}
}
