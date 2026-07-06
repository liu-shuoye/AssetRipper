using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.BundleFiles.RawWeb.Raw;
using AssetRipper.IO.Files.BundleFiles.RawWeb.Web;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.BundleFiles.RawWeb;

public abstract class RawWebBundleFile<THeader> : FileContainer where THeader : RawWebBundleHeader, new()
{
	public THeader Header { get; } = new();
	public DirectoryInfo<RawWebNode> DirectoryInfo { get; set; } = new();

	public override void Read(SmartStream stream)
	{
		long basePosition = stream.Position;
		Header.Read(stream);
		long headerSize = stream.Position - basePosition;
		if (headerSize != Header.HeaderSize)
		{
			throw new Exception($"Read {headerSize} but expected {Header.HeaderSize} bytes while reading the raw/web bundle header.");
		}
		ReadRawWebMetadata(stream, out Stream dataStream, out long metadataOffset);//ReadBlocksAndDirectory
		ReadRawWebData(dataStream, metadataOffset);//also ReadBlocksAndDirectory
	}

	public override void Write(Stream stream)
	{
		Header.Write(stream);
		throw new NotImplementedException();
	}

	private void ReadRawWebMetadata(Stream stream, out Stream dataStream, out long metadataOffset)
	{
		int metadataSize = RawWebBundleHeader.HasUncompressedBlocksInfoSize(Header.Version) ? Header.UncompressedBlocksInfoSize : 0;

		//These branches are collapsed by JIT
		if (typeof(THeader) == typeof(RawBundleHeader))
		{
			dataStream = stream;
			metadataOffset = stream.Position;
			ReadMetadata(dataStream, metadataSize);
		}
		else if (typeof(THeader) == typeof(WebBundleHeader))
		{
			// read only last chunk
			BundleScene chunkInfo = Header.Scenes[^1];
			dataStream = new MemoryStream(new byte[chunkInfo.DecompressedSize]);
			LzmaCompression.DecompressLzmaSizeStream(stream, chunkInfo.CompressedSize, dataStream);
			metadataOffset = 0;

			dataStream.Position = 0;
			ReadMetadata(dataStream, metadataSize);
		}
		else
		{
			throw new Exception($"Unsupported bundle type '{typeof(THeader)}'");
		}
	}

	private void ReadMetadata(Stream stream, int metadataSize)
	{
		long metadataPosition = stream.Position;
		using (EndianReader reader = new EndianReader(stream, EndianType.BigEndian))
		{
			DirectoryInfo = DirectoryInfo<RawWebNode>.Read(reader);
			reader.AlignStream();
		}
		if (metadataSize > 0)
		{
			if (stream.Position - metadataPosition != metadataSize)
			{
				throw new Exception($"Read {stream.Position - metadataPosition} but expected {metadataSize} while reading bundle metadata");
			}
		}
	}

	private void ReadRawWebData(Stream stream, long metadataOffset)
	{
		int threshold = BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize;
		foreach (RawWebNode entry in DirectoryInfo.Nodes)
		{
			// CreateBySize takes a 32-bit size; cap at int.MaxValue so that long values larger
			// than int.MaxValue still resolve to the temp-file path instead of overflowing.
			int entrySize = entry.Size > int.MaxValue ? int.MaxValue : (int)entry.Size;
			using SmartStream destStream = SmartStream.CreateBySize(entrySize, threshold);
			stream.Position = metadataOffset + entry.Offset;
			CopyExact(stream, destStream, entrySize);
			destStream.Position = 0;
			ResourceFile file = new ResourceFile(destStream, FilePath, entry.Path);
			AddResourceFile(file);
		}
	}

	/// <summary>
	/// Copies exactly <paramref name="size"/> bytes from <paramref name="source"/> to
	/// <paramref name="destination"/> using a small intermediate buffer, so that the destination
	/// can be either a memory-backed or temp-file-backed <see cref="SmartStream"/> without
	/// allocating a full <c>byte[<paramref name="size"/>]</c> array up-front.
	/// </summary>
	private static void CopyExact(Stream source, Stream destination, int size)
	{
		// 80 KiB matches Stream.CopyTo's default buffer size and keeps large entry reads off
		// the large object heap while still being efficient.
		byte[] buffer = new byte[Math.Min(size, 81920)];
		int remaining = size;
		while (remaining > 0)
		{
			int toRead = Math.Min(remaining, buffer.Length);
			source.ReadExactly(buffer, 0, toRead);
			destination.Write(buffer, 0, toRead);
			remaining -= toRead;
		}
	}
}
