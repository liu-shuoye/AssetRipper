using AssetRipper.IO.Files.Exceptions;
using AssetRipper.IO.Files.Streams;
using AssetRipper.IO.Files.Streams.Smart;
using K4os.Compression.LZ4;
using System.Buffers;

namespace AssetRipper.IO.Files.BundleFiles.FileStream;

internal sealed class BundleFileBlockReader : IDisposable
{
	/// <summary>
	/// Global threshold controlling the maximum decompressed block size (in bytes) that is
	/// kept in memory before spilling to a temporary file. This value is captured by each
	/// <see cref="BundleFileBlockReader"/> instance at construction time so that concurrent
	/// readers created with different thresholds do not interfere with each other.
	/// </summary>
	/// <remarks>
	/// AssetRipper's import pipeline is single-threaded, so concurrent mutation is not a
	/// concern in practice. If that changes, this should be replaced with an async-local or
	/// context-based mechanism.
	/// </remarks>
	public static int CurrentMaxInMemoryBundleBlockSize { get; set; } = 50 * 1024 * 1024;

	public BundleFileBlockReader(SmartStream stream, BlocksInfo blocksInfo)
	{
		m_stream = stream;
		m_blocksInfo = blocksInfo;
		m_dataOffset = stream.Position;
		maxMemoryStreamLength = CurrentMaxInMemoryBundleBlockSize;
		maxPreAllocatedMemoryStreamLength = CurrentMaxInMemoryBundleBlockSize * 6 / 10;
	}

	~BundleFileBlockReader()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public SmartStream ReadEntry(FileStreamNode entry)
	{
		ObjectDisposedException.ThrowIf(m_isDisposed, typeof(BundleFileBlockReader));

		// Avoid storing entire non-compresed entries in memory by mapping a stream to the block location.
		if (m_blocksInfo.StorageBlocks.Length == 1 && m_blocksInfo.StorageBlocks[0].CompressionType == CompressionType.None)
		{
			if (m_dataOffset + entry.Offset + entry.Size > m_stream.Length)
			{
				throw new InvalidFormatException("Entry extends beyond the end of the stream.");
			}
			return m_stream.CreatePartial(m_dataOffset + entry.Offset, entry.Size);
		}

		// find block offsets
		int blockIndex;
		long blockCompressedOffset = 0;
		long blockDecompressedOffset = 0;
		for (blockIndex = 0; blockDecompressedOffset + m_blocksInfo.StorageBlocks[blockIndex].UncompressedSize <= entry.Offset; blockIndex++)
		{
			blockCompressedOffset += m_blocksInfo.StorageBlocks[blockIndex].CompressedSize;
			blockDecompressedOffset += m_blocksInfo.StorageBlocks[blockIndex].UncompressedSize;
		}
		long entryOffsetInsideBlock = entry.Offset - blockDecompressedOffset;

		using SmartStream entryStream = CreateStream(entry.Size);
		long left = entry.Size;
		m_stream.Position = m_dataOffset + blockCompressedOffset;

		// copy data of all blocks used by current entry to new stream
		while (left > 0)
		{
			byte[]? rentedArray;

			long blockStreamOffset;
			Stream blockStream;
			StorageBlock block = m_blocksInfo.StorageBlocks[blockIndex];
			if (m_cachedBlockIndex == blockIndex)
			{
				// data of the previous entry is in the same block as this one
				// so we don't need to unpack it once again. Instead we can use cached stream
				blockStreamOffset = 0;
				blockStream = m_cachedBlockStream;
				rentedArray = null;
				m_stream.Position += block.CompressedSize;
			}
			else
			{
				CompressionType compressType = block.CompressionType;
				if (compressType is CompressionType.None)
				{
					blockStreamOffset = m_dataOffset + blockCompressedOffset;
					blockStream = m_stream;
					rentedArray = null;
				}
				else
				{
					blockStreamOffset = 0;
					m_cachedBlockIndex = blockIndex;
					m_cachedBlockStream.Move(CreateTemporaryStream(block.UncompressedSize, out rentedArray));
					switch (compressType)
					{
						case CompressionType.Lzma:
							LzmaCompression.DecompressLzmaStream(m_stream, block.CompressedSize, m_cachedBlockStream, block.UncompressedSize);
							break;

						case CompressionType.Lz4:
						case CompressionType.Lz4HC:
							uint uncompressedSize = block.UncompressedSize;
							byte[] uncompressedBytes = new byte[uncompressedSize];
							byte[] compressedBytes = new BinaryReader(m_stream).ReadBytes((int)block.CompressedSize);
							int bytesWritten = LZ4Codec.Decode(compressedBytes, uncompressedBytes);
							if (bytesWritten < 0)
							{
								DecompressionFailedException.ThrowNoBytesWritten(entry.PathFixed, compressType);
							}
							else if (bytesWritten != uncompressedSize)
							{
								DecompressionFailedException.ThrowIncorrectNumberBytesWritten(entry.PathFixed, compressType, uncompressedSize, bytesWritten);
							}
							new MemoryStream(uncompressedBytes).CopyTo(m_cachedBlockStream);
							break;

						case CompressionType.Lzham:
							UnsupportedBundleDecompression.ThrowLzham(entry.PathFixed);
							break;

						default:
							if (ZstdCompression.IsZstd(m_stream))
							{
								ZstdCompression.DecompressStream(m_stream, block.CompressedSize, m_cachedBlockStream, block.UncompressedSize);
							}
							else
							{
								UnsupportedBundleDecompression.Throw(entry.PathFixed, compressType);
							}
							break;
					}
					blockStream = m_cachedBlockStream;
				}
			}

			// consider next offsets:
			// 1) block - if it is new stream then offset is 0, otherwise offset of this block in the bundle file
			// 2) entry - if this is first block for current entry then it is offset of this entry related to this block
			//			  otherwise 0
			long blockSize = block.UncompressedSize - entryOffsetInsideBlock;
			blockStream.Position = blockStreamOffset + entryOffsetInsideBlock;
			entryOffsetInsideBlock = 0;

			long size = Math.Min(blockSize, left);
			if (blockStream.Position + size > blockStream.Length)
			{
				throw new InvalidFormatException("Block extends beyond the end of the stream.");
			}
			using PartialStream partialStream = new(blockStream, blockStream.Position, size);
			partialStream.CopyTo(entryStream);
			blockIndex++;

			blockCompressedOffset += block.CompressedSize;
			left -= size;

			if (rentedArray != null)
			{
				ArrayPool<byte>.Shared.Return(rentedArray);
			}
		}
		if (left < 0)
		{
			DecompressionFailedException.ThrowReadMoreThanExpected(entry.PathFixed, entry.Size, entry.Size - left);
		}
		entryStream.Position = 0;
		return entryStream.CreateReference();
	}

	private void Dispose(bool disposing)
	{
		m_isDisposed = true;
		m_cachedBlockStream.FreeReference();
	}

	private SmartStream CreateStream(long decompressedSize)
	{
		// Pre-allocate the memory buffer for small sizes to avoid later resizing.
		if (decompressedSize <= maxPreAllocatedMemoryStreamLength)
		{
			return SmartStream.CreateMemory(new byte[decompressedSize]);
		}

		// For larger sizes, delegate the memory-vs-temp-file decision to SmartStream.CreateBySize.
		// CreateBySize takes a 32-bit size; cap at int.MaxValue so that long values larger than
		// maxMemoryStreamLength still resolve to the temp-file path instead of overflowing.
		int size = decompressedSize > int.MaxValue ? int.MaxValue : (int)decompressedSize;
		return SmartStream.CreateBySize(size, maxMemoryStreamLength);
	}

	private SmartStream CreateTemporaryStream(long decompressedSize, out byte[]? rentedArray)
	{
		// CreateBySize takes a 32-bit size; cap at int.MaxValue so that long values larger than
		// maxMemoryStreamLength still resolve to the temp-file path instead of overflowing.
		int size = decompressedSize > int.MaxValue ? int.MaxValue : (int)decompressedSize;
		SmartStream stream = SmartStream.CreateBySize(size, maxMemoryStreamLength);
		if (stream.StreamType == SmartStreamType.File)
		{
			rentedArray = null;
			return stream;
		}

		// Memory path: replace the default MemoryStream with an ArrayPool-backed buffer
		// to avoid the allocation overhead of a separately owned buffer.
		stream.FreeReference();
		rentedArray = ArrayPool<byte>.Shared.Rent(size);
		return SmartStream.CreateMemory(rentedArray, 0, size);
	}

	/// <summary>
	/// The maximum size of a decompressed stream to be stored in RAM.
	/// </summary>
	/// <remarks>
	/// Captured from <see cref="CurrentMaxInMemoryBundleBlockSize"/> at construction time.
	/// Previously a <c>const</c> hard-coded to 50 MB; can now be tuned via
	/// <c>ImportSettings.MaxInMemoryBundleBlockSize</c>.
	/// </remarks>
	private readonly int maxMemoryStreamLength;
	/// <summary>
	/// The maximum size of a decompressed stream to be pre-allocated.
	/// </summary>
	/// <remarks>
	/// Derived as <c>maxMemoryStreamLength * 6 / 10</c> (i.e. 60% of
	/// <see cref="maxMemoryStreamLength"/>), preserving the original 30 MB / 50 MB ratio.
	/// </remarks>
	private readonly int maxPreAllocatedMemoryStreamLength;
	private readonly SmartStream m_stream;
	private readonly BlocksInfo m_blocksInfo = new();
	private readonly long m_dataOffset;

	private readonly SmartStream m_cachedBlockStream = SmartStream.CreateNull();
	private int m_cachedBlockIndex = -1;

	private bool m_isDisposed = false;
}
