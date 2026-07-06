using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using System.IO.Compression;

namespace AssetRipper.IO.Files.CompressedFiles.Brotli;

public sealed class BrotliFile : CompressedFile
{
	private static ReadOnlySpan<byte> BrotliSignature => "UnityWeb Compressed Content (brotli)"u8;

	/// <summary>
	/// Multiplier used as a heuristic estimate of the uncompressed Brotli payload size.
	/// Brotli has no reliable uncompressed-size header, so we use 4× the compressed size, which
	/// matches typical Unity bundle compression ratios. This only drives the memory-vs-temp-file
	/// decision via <see cref="SmartStream.CreateBySize"/>; the actual decompression still works
	/// regardless because <see cref="MemoryStream"/> auto-expands when the estimate is too small.
	/// </summary>
	private const int FallbackSizeMultiplier = 4;

	public override void Read(SmartStream stream)
	{
		try
		{
			using SmartStream decompressedStream = ReadBrotli(stream);
			UncompressedFile = new ResourceFile(decompressedStream, FilePath, Name);
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

	internal static bool IsBrotliFile(Stream stream)
	{
		long remaining = stream.Length - stream.Position;
		if (remaining < 4)
		{
			return false;
		}

		long position = stream.Position;

		stream.Position += 1;
		byte bt = (byte)stream.ReadByte(); // read 3 bits
		int sizeBytes = bt & 0x3;

		if (stream.Position + sizeBytes > stream.Length)
		{
			stream.Position = position;
			return false;
		}

		int length = 0;
		for (int i = 0; i < sizeBytes; i++)
		{
			byte nbt = (byte)stream.ReadByte();  // read next 8 bits
			int bits = (bt >> 2) | ((nbt & 0x3) << 6);
			bt = nbt;
			length += bits << (8 * i);
		}

		if (length != BrotliSignature.Length
			|| stream.Position + length > stream.Length)
		{
			stream.Position = position;
			return false;
		}

		Span<byte> buffer = stackalloc byte[BrotliSignature.Length];
		stream.ReadExactly(buffer);
		stream.Position = position;
		return buffer.SequenceEqual(BrotliSignature);
	}

	/// <summary>
	/// Decompresses the Brotli payload into a <see cref="SmartStream"/> selected via
	/// <see cref="SmartStream.CreateBySize"/> using the configured bundle-block threshold.
	/// </summary>
	/// <remarks>
	/// Replaces the previous <c>memoryStream.ToArray()</c> implementation, which double-allocated
	/// the entire decompressed payload (once in the <see cref="MemoryStream"/> internal buffer and
	/// once in the returned <c>byte[]</c>). The returned <see cref="SmartStream"/> shares its
	/// underlying buffer with the <see cref="ResourceFile"/> that consumes it, eliminating the copy.
	/// </remarks>
	private static SmartStream ReadBrotli(Stream stream)
	{
		int threshold = BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize;
		int estimatedSize = EstimateBrotliUncompressedSize(stream);
		SmartStream destStream = SmartStream.CreateBySize(estimatedSize, threshold);
		try
		{
			using BrotliStream brotliStream = new BrotliStream(stream, CompressionMode.Decompress);
			brotliStream.CopyTo(destStream);
			destStream.Position = 0;
			return destStream;
		}
		catch
		{
			destStream.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Estimates the uncompressed size of a Brotli payload by applying
	/// <see cref="FallbackSizeMultiplier"/>× the compressed size. The estimate only drives the
	/// memory-vs-temp-file decision; <see cref="MemoryStream"/> auto-expands if it is too small.
	/// </summary>
	private static int EstimateBrotliUncompressedSize(Stream stream)
	{
		if (!stream.CanSeek)
		{
			return 0;
		}
		long compressed = stream.Length - stream.Position;
		if (compressed <= 0)
		{
			return 0;
		}
		long estimated = compressed * FallbackSizeMultiplier;
		return estimated > int.MaxValue ? int.MaxValue : (int)estimated;
	}

	public override void Write(Stream stream)
	{
		throw new NotImplementedException();
	}
}
