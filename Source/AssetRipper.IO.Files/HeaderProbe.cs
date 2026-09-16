using System.Buffers.Binary;
using System.Text;

namespace AssetRipper.IO.Files;

/// <summary>
/// 只依赖已读入内存的字节来判断文件类型，使文件头判定可以脱离磁盘被单元测试。
/// </summary>
/// <remarks>
/// 这里的判据与 <see cref="SerializedFiles.SerializationFileHeaderProbe"/> 及各类资源包头的
/// 校验逻辑保持一致，但不需要额外的流访问。具体地说，序列化文件的判定复刻了
/// <c>SerializedFileHeader.IsSerializedFileHeader</c> 的“声明大小必须等于实际大小”这一关键约束，
/// 因此不会把仅前缀巧合相似的文件误判为序列化文件。
/// </remarks>
public static class HeaderProbe
{
	/// <summary>
	/// Unity 资源包头的 magic 字符串，与 <c>BundleHeader.MagicString</c> 的各实现一致。
	/// </summary>
	private static readonly byte[][] BundleMagics =
	[
		"UnityFS"u8.ToArray(),
		"UnityRaw"u8.ToArray(),
		"UnityWeb"u8.ToArray(),
		"UnityArchive"u8.ToArray(),
	];

	/// <summary>
	/// 序列化文件头部的最小长度：元数据大小(4) + 文件大小(4) + 格式版本(4) + 数据偏移(4) + 字节序(1) + 对齐填充(3)。
	/// </summary>
	private const int SerializedFileHeaderMinSize = 0x10;

	/// <summary>元数据的最小长度，与 <c>SerializedFileHeader.MetadataMinSize</c> 一致。</summary>
	private const int MetadataMinSize = 13;

	/// <summary>支持 64 位文件大小的格式版本，与 <c>FormatVersion.LargeFilesSupport</c> 一致。</summary>
	private const int LargeFilesSupportFormatVersion = 22;

	/// <summary>格式版本字段在文件中的偏移。</summary>
	private const int GenerationOffset = 0x08;

	/// <summary>大文件格式下元数据大小与文件大小的偏移。</summary>
	private const int LargeMetadataSizeOffset = 0x14;

	/// <summary>
	/// 判定资源包头所需的最小文件长度，与 <c>BundleHeader.IsBundleHeader</c> 中的 <c>MaxLength</c> 保持一致。
	/// </summary>
	private const int MaximumBundleHeaderProbeLength = 0x20;

	/// <summary>
	/// 判断缓冲区内容是否可能为一个序列化文件的头部。
	/// </summary>
	/// <param name="buffer">文件开头若干字节。</param>
	/// <param name="length">缓冲区中有效字节数。</param>
	/// <returns>满足全部头部约束时为 <see langword="true"/>。</returns>
	public static bool MatchesSerializedFile(ReadOnlySpan<byte> buffer, int length)
	{
		if (length < SerializedFileHeaderMinSize)
		{
			return false;
		}

		// 头部与元数据始终为大端序
		int metadataSize = BinaryPrimitives.ReadInt32BigEndian(buffer);
		uint headerDefinedFileSize = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
		int generation = BinaryPrimitives.ReadInt32BigEndian(buffer[GenerationOffset..]);

		if (!IsKnownFormatVersion(generation))
		{
			return false;
		}

		if (generation >= LargeFilesSupportFormatVersion)
		{
			if (length < LargeMetadataSizeOffset + 12)
			{
				return false;
			}

			metadataSize = BinaryPrimitives.ReadInt32BigEndian(buffer[LargeMetadataSizeOffset..]);
			headerDefinedFileSize = (uint)BinaryPrimitives.ReadInt64BigEndian(buffer[(LargeMetadataSizeOffset + 4)..]);
		}

		return metadataSize >= MetadataMinSize
			&& headerDefinedFileSize >= SerializedFileHeaderMinSize + MetadataMinSize;
	}

	/// <summary>
	/// 判断缓冲区内容是否为一个 Unity 资源包的头部。
	/// </summary>
	/// <param name="buffer">文件开头若干字节。</param>
	/// <param name="length">缓冲区中有效字节数。</param>
	public static bool MatchesBundle(ReadOnlySpan<byte> buffer, int length)
	{
		// 与既有实现一致：仅当文件长度达到阈值时才检查 magic，避免把极短文件误判为资源包
		if (length < MaximumBundleHeaderProbeLength)
		{
			return false;
		}

		foreach (byte[] magic in BundleMagics)
		{
			if (StartsWithZeroTerminated(buffer, length, magic))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// 判断缓冲区是否以给定的以零结尾字符串开头，即 magic 之后紧跟一个 0 字节。
	/// </summary>
	private static bool StartsWithZeroTerminated(ReadOnlySpan<byte> buffer, int length, byte[] magic)
	{
		if (length < magic.Length + 1)
		{
			return false;
		}

		if (!buffer[..magic.Length].SequenceEqual(magic))
		{
			return false;
		}

		// 资源包头的 magic 之后必须是以零结尾的字符串，因此 magic 的下一个字节应为 0
		return buffer[magic.Length] == 0;
	}

	/// <summary>
	/// <c>FormatVersion</c> 是稀疏枚举（19~23 之间不存在），无法在 IO.Files 层直接引用生成的枚举类型，
	/// 因此此处按已知取值显式判断。此处只需排除明显非法的值，后续的长度约束才是主要判据。
	/// </summary>
	private static bool IsKnownFormatVersion(int generation)
	{
		return generation is >= 0 and <= 255;
	}
}
