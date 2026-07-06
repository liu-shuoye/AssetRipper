using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using System.IO.Compression;

namespace AssetRipper.IO.Files.CompressedFiles.GZip;

public sealed class GZipFile : CompressedFile
{
	private const ushort GZipMagic = 0x1F8B;

	/// <summary>
	/// Multiplier used as a fallback estimate of the uncompressed GZip payload size when the
	/// ISIZE trailer is missing or unreliable. 4× matches the typical deflate compression ratio
	/// seen on Unity bundle payloads and is also the heuristic used for Brotli below.
	/// </summary>
	private const int FallbackSizeMultiplier = 4;

	public override void Read(SmartStream stream)
	{
		try
		{
			int threshold = BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize;
			int estimatedSize = EstimateGZipUncompressedSize(stream);
			using SmartStream destStream = SmartStream.CreateBySize(estimatedSize, threshold);
			using (GZipStream gzipStream = new GZipStream(stream, CompressionMode.Decompress, true))
			{
				gzipStream.CopyTo(destStream);
			}
			destStream.Position = 0;
			UncompressedFile = new ResourceFile(destStream, FilePath, Name);
		}
		catch (Exception ex)
		{
			UncompressedFile = new FailedFile()
			{
				Name = Name,
				FilePath = FilePath,
				StackTrace = ex.ToString(),
			};
		}
	}

	public override void Write(Stream stream)
	{
		using MemoryStream memoryStream = new();
		UncompressedFile?.Write(memoryStream);
		memoryStream.Position = 0;
		using GZipStream gzipStream = new GZipStream(stream, CompressionMode.Compress, true);
		memoryStream.CopyTo(gzipStream);
	}

	internal static bool IsGZipFile(EndianReader reader)
	{
		long position = reader.BaseStream.Position;
		ushort gzipMagic = ReadGZipMagic(reader);
		reader.BaseStream.Position = position;
		return gzipMagic == GZipMagic;
	}

	private static ushort ReadGZipMagic(EndianReader reader)
	{
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		if (remaining >= sizeof(ushort))
		{
			return reader.ReadUInt16();
		}
		return 0;
	}

	/// <summary>
	/// Estimates the uncompressed size of a GZip payload by reading the ISIZE trailer
	/// (last 4 bytes of the stream, little-endian uint32, size mod 2^32).
	/// Falls back to <see cref="FallbackSizeMultiplier"/>× the compressed size when the trailer
	/// is missing, zero, or unreadable. The estimate only drives the memory-vs-temp-file
	/// decision via <see cref="SmartStream.CreateBySize"/>; the actual decompression still works
	/// regardless because <see cref="MemoryStream"/> auto-expands when the estimate is too small.
	/// </summary>
	private static int EstimateGZipUncompressedSize(Stream stream)
	{
		// ISIZE is only meaningful when we can seek to the trailer.
		if (stream.CanSeek)
		{
			try
			{
				long remaining = stream.Length - stream.Position;
				// GZip stream is at minimum 10 header bytes + 8 trailer bytes.
				if (remaining >= 18)
				{
					long originalPosition = stream.Position;
					stream.Position = stream.Length - 4;
					int isize = stream.ReadByte()
						| (stream.ReadByte() << 8)
						| (stream.ReadByte() << 16)
						| (stream.ReadByte() << 24);
					stream.Position = originalPosition;
					if (isize > 0)
					{
						return isize;
					}
				}
			}
			catch
			{
				// Best-effort: fall through to the compressed-size heuristic.
			}
		}

		long compressed = stream.CanSeek ? stream.Length - stream.Position : 0;
		if (compressed <= 0)
		{
			// Unknown compressed size; assume something moderate so CreateBySize still picks memory
			// for tiny streams and temp-file for anything above the default 50 MB threshold.
			return 0;
		}
		long estimated = compressed * FallbackSizeMultiplier;
		return estimated > int.MaxValue ? int.MaxValue : (int)estimated;
	}
}
