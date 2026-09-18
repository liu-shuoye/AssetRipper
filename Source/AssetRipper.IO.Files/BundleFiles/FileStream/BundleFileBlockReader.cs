using AssetRipper.IO.Files.Exceptions;
using AssetRipper.IO.Files.Streams;
using AssetRipper.IO.Files.Streams.Smart;
using K4os.Compression.LZ4;
using System.Buffers;

namespace AssetRipper.IO.Files.BundleFiles.FileStream;

internal sealed class BundleFileBlockReader : IDisposable
{
	public BundleFileBlockReader(SmartStream stream, BlocksInfo blocksInfo, string name = "")
	{
		m_stream = stream;
		m_blocksInfo = blocksInfo;
		m_dataOffset = stream.Position;
		m_name = name;
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

		// 通过将流映射到块位置，避免将整个未压缩的条目存储在内存中。
		if (m_blocksInfo.StorageBlocks.Length == 1 && m_blocksInfo.StorageBlocks[0].CompressionType == CompressionType.None)
		{
			if (m_dataOffset + entry.Offset + entry.Size > m_stream.Length)
			{
				throw new InvalidFormatException("Entry extends beyond the end of the stream.");
			}
			return m_stream.CreatePartial(m_dataOffset + entry.Offset, entry.Size);
		}

		// 整包解压一次后由所有条目共享：条目只是这条流上的一个区间视图。
		// 逐条目复制会让一个 N 条目的 bundle 占用 N 条流与 N 个文件句柄；
		// 而条目偏移本身就是"各块解压后首尾相接"的拼接流内偏移，因此可以零拷贝地共享同一条流。
		if (TryGetSharedStream(out SmartStream shared))
		{
			if (entry.Offset + entry.Size > shared.Length)
			{
				throw new InvalidFormatException("Entry extends beyond the end of the stream.");
			}
			return shared.CreatePartial(entry.Offset, entry.Size);
		}

		return ReadEntryByCopyingBlocks(entry);
	}

	/// <summary>
	/// 取得整包解压后的共享流；构造失败时返回 <see langword="false"/>，由调用方回退到逐条目复制。
	/// </summary>
	/// <remarks>
	/// 失败必须被吞掉而不是抛出：整包解压会把任意坏块的影响扩散到整个 bundle，
	/// 回退之后失败范围仍只落在真正用到坏块的条目上，与改动前的行为保持一致。
	/// </remarks>
	private bool TryGetSharedStream(out SmartStream shared)
	{
		if (!m_sharedAttempted)
		{
			m_sharedAttempted = true;
			try
			{
				m_sharedStream.Move(DecompressAllBlocks());
			}
			catch (Exception)
			{
				m_sharedStream.Move(SmartStream.CreateNull());
			}
		}
		shared = m_sharedStream;
		return !shared.IsNull;
	}

	/// <summary>
	/// 把所有块按顺序解压到同一条流中，使其等价于"各块解压后首尾相接"的拼接流。
	/// </summary>
	private SmartStream DecompressAllBlocks()
	{
		long totalSize = 0;
		foreach (StorageBlock block in m_blocksInfo.StorageBlocks)
		{
			totalSize += block.UncompressedSize;
		}

		SmartStream result = CreateStream(totalSize);
		try
		{
			long compressedOffset = 0;
			foreach (StorageBlock block in m_blocksInfo.StorageBlocks)
			{
				m_stream.Position = m_dataOffset + compressedOffset;
				DecompressBlock(block, result);
				compressedOffset += block.CompressedSize;
			}
			result.Position = 0;
			return result;
		}
		catch
		{
			// 半途失败时不能留下一条只写了一半的流（连同它背后的临时文件）
			result.Dispose();
			throw;
		}
	}

	/// <summary>
	/// 把单个块解压并追加写入 <paramref name="destination"/>。
	/// </summary>
	private void DecompressBlock(StorageBlock block, SmartStream destination)
	{
		CompressionType compressType = block.CompressionType;
		switch (compressType)
		{
			case CompressionType.None:
				{
					using PartialStream partial = new(m_stream, m_stream.Position, block.UncompressedSize);
					partial.CopyTo(destination);
					break;
				}

			case CompressionType.Lzma:
				LzmaCompression.DecompressLzmaStream(m_stream, block.CompressedSize, destination, block.UncompressedSize);
				break;

			case CompressionType.Lz4:
			case CompressionType.Lz4HC:
				{
					uint uncompressedSize = block.UncompressedSize;
					byte[] uncompressedBytes = new byte[uncompressedSize];
					byte[] compressedBytes = new BinaryReader(m_stream).ReadBytes((int)block.CompressedSize);
					int bytesWritten = LZ4Codec.Decode(compressedBytes, uncompressedBytes);
					if (bytesWritten < 0)
					{
						DecompressionFailedException.ThrowNoBytesWritten(m_name, compressType);
					}
					else if (bytesWritten != uncompressedSize)
					{
						DecompressionFailedException.ThrowIncorrectNumberBytesWritten(m_name, compressType, uncompressedSize, bytesWritten);
					}
					destination.Write(uncompressedBytes, 0, uncompressedBytes.Length);
					break;
				}

			case CompressionType.Lzham:
				UnsupportedBundleDecompression.ThrowLzham(m_name);
				break;

			default:
				if (ZstdCompression.IsZstd(m_stream))
				{
					ZstdCompression.DecompressStream(m_stream, block.CompressedSize, destination, block.UncompressedSize);
				}
				else
				{
					UnsupportedBundleDecompression.Throw(m_name, compressType);
				}
				break;
		}
	}

	/// <summary>
	/// 回退路径：只为当前条目解压它用到的块，并把这些数据复制进一条独立的流。
	/// </summary>
	/// <remarks>
	/// 只有在整包共享流不可用时才会走到这里。它保留改动前的语义——
	/// 失败只影响使用坏块的条目——代价是每个条目各占一条流。
	/// </remarks>
	private SmartStream ReadEntryByCopyingBlocks(FileStreamNode entry)
	{
		// 查找块偏移量
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

		// 将所有当前条目使用的块数据复制到新流中
		while (left > 0)
		{
			byte[]? rentedArray;

			long blockStreamOffset;
			Stream blockStream;
			StorageBlock block = m_blocksInfo.StorageBlocks[blockIndex];
			if (m_cachedBlockIndex == blockIndex)
			{
				// 上一条记录的数据与当前条目在同一块中，因此我们无需再次解压。
				// 相反，可以使用缓存的流
				blockStreamOffset = 0;
				blockStream = m_cachedBlockStream;
				rentedArray = null;
				m_stream.Position += block.CompressedSize;
			}
			else
			{
				if (block.CompressionType is CompressionType.None)
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
					m_stream.Position = m_dataOffset + blockCompressedOffset;
					DecompressBlock(block, m_cachedBlockStream);
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
		m_sharedStream.FreeReference();
	}

	private static SmartStream CreateStream(long decompressedSize)
	{
		return decompressedSize switch
		{
			> MaxMemoryStreamLength => SmartStream.CreateTemp(),
			> MaxPreAllocatedMemoryStreamLength => SmartStream.CreateMemory(),
			_ => SmartStream.CreateMemory(new byte[decompressedSize]),
		};
	}

	private static SmartStream CreateTemporaryStream(long decompressedSize, out byte[]? rentedArray)
	{
		if (decompressedSize > MaxMemoryStreamLength)
		{
			rentedArray = null;
			return SmartStream.CreateTemp();
		}
		else
		{
			rentedArray = ArrayPool<byte>.Shared.Rent((int)decompressedSize);
			return SmartStream.CreateMemory(rentedArray, 0, (int)decompressedSize);
		}
	}

	/// <summary>
	/// 解压后数据允许留在内存中的最大体积（字节），超过则落到磁盘临时文件。
	/// </summary>
	/// <remarks>
	/// 这里刻意取得很小：真实项目里 bundle 数量可达数万，单个 bundle 解压后常在数百 KB，
	/// 若放宽到 MB 级，整批数据会直接压垮托管堆，因此默认值让它们几乎全部落盘。
	/// </remarks>
	private const int MaxMemoryStreamLength = 1024;
	/// <summary>
	/// 允许预先分配缓冲区的最大体积（字节），必须小于 <see cref="MaxMemoryStreamLength"/>。
	/// </summary>
	private const int MaxPreAllocatedMemoryStreamLength = 1023;
	private readonly SmartStream m_stream;
	private readonly BlocksInfo m_blocksInfo = new();
	private readonly long m_dataOffset;
	private readonly string m_name;

	private readonly SmartStream m_cachedBlockStream = SmartStream.CreateNull();
	private int m_cachedBlockIndex = -1;

	/// <summary>整包解压后的共享流，该 bundle 的所有条目共用。</summary>
	private readonly SmartStream m_sharedStream = SmartStream.CreateNull();
	/// <summary>共享流是否已尝试构造过；失败后不再重试，避免每个条目都白跑一遍解压。</summary>
	private bool m_sharedAttempted;

	private bool m_isDisposed = false;
}
