using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.BundleFiles.RawWeb;
using AssetRipper.IO.Files.BundleFiles.RawWeb.Raw;
using AssetRipper.IO.Files.CompressedFiles.Brotli;
using AssetRipper.IO.Files.CompressedFiles.GZip;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using AssetRipper.IO.Files.WebFiles;
using System.IO.Compression;
using System.Reflection;

namespace AssetRipper.IO.Files.Tests;

/// <summary>
/// Verifies that the GZip / Brotli / WebFile / RawWebFile decompression paths route their
/// payloads through <see cref="SmartStream.CreateBySize"/> using the shared
/// <see cref="BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize"/> threshold, so that
/// large payloads spill to a temporary file instead of being buffered entirely in memory.
/// </summary>
/// <remarks>
/// Tests temporarily lower <see cref="BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize"/>
/// via <see cref="ThresholdScope"/> so that the spill path can be exercised with small payloads
/// (a few MB) instead of having to construct 200 MB of test data. The static property is restored
/// to its previous value in <c>finally</c> blocks.
/// </remarks>
public class CompressedFileThresholdTests
{
	/// <summary>
	/// 1 MiB threshold used by the "spill" tests. Small enough that we can drive the temp-file
	/// path with a few MB of payload, fast enough that the test suite stays sub-second.
	/// </summary>
	private const int SpillThreshold = 1 * 1024 * 1024;
	/// <summary>
	/// 4 KiB of payload data used to exercise the in-memory path. Stays well below
	/// <see cref="SpillThreshold"/> even when the threshold is lowered.
	/// </summary>
	private const int SmallPayloadSize = 4 * 1024;
	/// <summary>
	/// 2 MiB of payload data used to exercise the temp-file path. Larger than
	/// <see cref="SpillThreshold"/> so <see cref="SmartStream.CreateBySize"/> selects a temp file.
	/// </summary>
	private const int LargePayloadSize = 2 * 1024 * 1024;

	// -------------------- GZipFile --------------------

	[Test]
	public void GZipFile_SmallFile_StaysInMemory()
	{
		byte[] payload = RandomData.MakeRandomData(SmallPayloadSize);
		using SmartStream compressedStream = CreateGZipStream(payload);
		GZipFile file = new()
		{
			FilePath = "test.gz",
			Name = "test.gz",
		};

		file.Read(compressedStream);

		Assert.That(file.UncompressedFile, Is.InstanceOf<ResourceFile>());
		ResourceFile resourceFile = (ResourceFile)file.UncompressedFile!;
		Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory),
			"4 KiB GZip payload must stay in memory under the default 50 MiB threshold");
		AssertRoundTrip(resourceFile.Stream, payload);
	}

	[Test]
	public void GZipFile_LargeFile_SpillsToTempFile()
	{
		byte[] payload = RandomData.MakeRandomData(LargePayloadSize);
		using SmartStream compressedStream = CreateGZipStream(payload);
		GZipFile file = new()
		{
			FilePath = "test.gz",
			Name = "test.gz",
		};

		using (new ThresholdScope(SpillThreshold))
		{
			file.Read(compressedStream);
		}

		Assert.That(file.UncompressedFile, Is.InstanceOf<ResourceFile>());
		ResourceFile resourceFile = (ResourceFile)file.UncompressedFile!;
		Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.File),
			"2 MiB GZip payload must spill to a temp file when threshold is 1 MiB");
		AssertRoundTrip(resourceFile.Stream, payload);
	}

	// -------------------- BrotliFile --------------------

	[Test]
	public void BrotliFile_SmallFile_StaysInMemory()
	{
		byte[] payload = RandomData.MakeRandomData(SmallPayloadSize);
		using SmartStream compressedStream = CreateBrotliStream(payload);
		BrotliFile file = new()
		{
			FilePath = "test.br",
			Name = "test.br",
		};

		file.Read(compressedStream);

		Assert.That(file.UncompressedFile, Is.InstanceOf<ResourceFile>());
		ResourceFile resourceFile = (ResourceFile)file.UncompressedFile!;
		Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory),
			"4 KiB Brotli payload must stay in memory under the default 50 MiB threshold");
		AssertRoundTrip(resourceFile.Stream, payload);
	}

	[Test]
	public void BrotliFile_LargeFile_SpillsToTempFile()
	{
		byte[] payload = RandomData.MakeRandomData(LargePayloadSize);
		using SmartStream compressedStream = CreateBrotliStream(payload);
		BrotliFile file = new()
		{
			FilePath = "test.br",
			Name = "test.br",
		};

		using (new ThresholdScope(SpillThreshold))
		{
			file.Read(compressedStream);
		}

		Assert.That(file.UncompressedFile, Is.InstanceOf<ResourceFile>());
		ResourceFile resourceFile = (ResourceFile)file.UncompressedFile!;
		Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.File),
			"2 MiB Brotli payload must spill to a temp file when threshold is 1 MiB");
		AssertRoundTrip(resourceFile.Stream, payload);
	}

	[Test]
	public void BrotliFile_NoToArrayDoubleAllocation()
	{
		// 100 bytes is intentionally not a power of two, so a MemoryStream created via the
		// default constructor (capacity 0 -> 256 -> ...) ends up with GetBuffer().Length == 256.
		// If the previous `memoryStream.ToArray()` code path were still in place, the ResourceFile
		// would be backed by a precisely-sized byte[100], and GetBuffer().Length would equal 100.
		const int PayloadSize = 100;
		byte[] payload = RandomData.MakeRandomData(PayloadSize);
		using SmartStream compressedStream = CreateBrotliStream(payload);
		BrotliFile file = new()
		{
			FilePath = "test.br",
			Name = "test.br",
		};

		file.Read(compressedStream);

		Assert.That(file.UncompressedFile, Is.InstanceOf<ResourceFile>());
		ResourceFile resourceFile = (ResourceFile)file.UncompressedFile!;
		Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory));

		MemoryStream? memoryStream = GetUnderlyingMemoryStream(resourceFile.Stream);
		Assert.That(memoryStream, Is.Not.Null,
			"memory-backed ResourceFile must expose a MemoryStream");
		byte[] internalBuffer = memoryStream!.GetBuffer();
		Assert.That(internalBuffer.Length, Is.GreaterThan(PayloadSize),
			"internal buffer must be larger than the payload, proving MemoryStream auto-expanded " +
			"rather than being constructed from a precisely-sized ToArray() copy");
		AssertRoundTrip(resourceFile.Stream, payload);
	}

	// -------------------- WebFile --------------------

	[Test]
	public void WebFile_LargeEntry_SpillsToTempFile()
	{
		WebFile bundle = new();
		bundle.AddResourceFile(new ResourceFile(
			SmartStream.CreateMemory(RandomData.MakeRandomData(LargePayloadSize)),
			"test.data",
			"test.data"));

		using SmartStream written = SmartStream.CreateMemory();
		bundle.Write(written);
		written.Position = 0;

		WebFileScheme scheme = new();
		Assert.That(scheme.CanRead(written), Is.True);

		WebFile readBundle;
		using (new ThresholdScope(SpillThreshold))
		{
			readBundle = scheme.Read(written, bundle.FilePath, bundle.Name);
		}

		Assert.That(readBundle.ResourceFiles, Has.Count.EqualTo(1));
		Assert.That(readBundle.ResourceFiles[0].Stream.StreamType, Is.EqualTo(SmartStreamType.File),
			"2 MiB WebFile entry must spill to a temp file when threshold is 1 MiB");
		Assert.That(readBundle.ResourceFiles[0].Stream.Length, Is.EqualTo(LargePayloadSize));
	}

	// -------------------- RawWebBundleFile --------------------

	[Test]
	public void RawWebBundleFile_LargeData_SpillsToTempFile()
	{
		// RawWebBundleFile.Write throws NotImplementedException, so we cannot use a write/read
		// symmetry test like WebFile. Instead, build a RawBundleFile instance directly and invoke
		// the private ReadRawWebData method via reflection with a synthetic data stream.
		byte[] payload = RandomData.MakeRandomData(LargePayloadSize);
		using SmartStream dataStream = SmartStream.CreateMemory(payload);

		RawBundleFile bundle = new()
		{
			FilePath = "test.bundle",
			Name = "test.bundle",
		};
		bundle.DirectoryInfo.Nodes = new[]
		{
			new RawWebNode
			{
				Path = "test.resource",
				Offset = 0,
				Size = LargePayloadSize,
			},
		};

		using (new ThresholdScope(SpillThreshold))
		{
			InvokeReadRawWebData(bundle, dataStream, metadataOffset: 0L);
		}

		Assert.That(bundle.ResourceFiles, Has.Count.EqualTo(1));
		Assert.That(bundle.ResourceFiles[0].Stream.StreamType, Is.EqualTo(SmartStreamType.File),
			"2 MiB RawWeb node payload must spill to a temp file when threshold is 1 MiB");
		Assert.That(bundle.ResourceFiles[0].Stream.Length, Is.EqualTo(LargePayloadSize));
		AssertRoundTrip(bundle.ResourceFiles[0].Stream, payload);
	}

	// -------------------- ResourceFile temp-file readability --------------------

	[Test]
	public void ResourceFileStream_CanBeReadFromTempFile()
	{
		// A ResourceFile whose backing SmartStream is a temp file (FileStream) must still allow
		// on-demand reads via the public ResourceFile.Stream surface. This guards against the
		// spill path returning a stream that cannot be consumed downstream.
		byte[] payload = RandomData.MakeRandomData(256);
		SmartStream tempStream = SmartStream.CreateTemp();
		tempStream.Write(payload, 0, payload.Length);
		tempStream.Position = 0;
		ResourceFile resourceFile = new ResourceFile(tempStream, "test.resource", "test.resource");
		try
		{
			Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.File));

			resourceFile.Stream.Position = 0;
			byte[] readBack = new byte[payload.Length];
			resourceFile.Stream.ReadExactly(readBack);
			Assert.That(readBack, Is.EqualTo(payload),
				"temp-file-backed ResourceFile must round-trip the original payload");

			// Verify ToByteArray also works on the file-backed stream (used by some exporters).
			byte[] toArray = resourceFile.ToByteArray();
			Assert.That(toArray, Is.EqualTo(payload),
				"ResourceFile.ToByteArray must work for file-backed streams");
		}
		finally
		{
			resourceFile.Dispose();
			tempStream.Dispose();
		}
	}

	// -------------------- helpers --------------------

	private static SmartStream CreateGZipStream(byte[] payload)
	{
		MemoryStream compressed = new MemoryStream();
		using (GZipStream gz = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
		{
			gz.Write(payload, 0, payload.Length);
		}
		return SmartStream.CreateMemory(compressed.ToArray());
	}

	private static SmartStream CreateBrotliStream(byte[] payload)
	{
		MemoryStream compressed = new MemoryStream();
		using (BrotliStream br = new BrotliStream(compressed, CompressionMode.Compress, leaveOpen: true))
		{
			br.Write(payload, 0, payload.Length);
		}
		return SmartStream.CreateMemory(compressed.ToArray());
	}

	private static void AssertRoundTrip(SmartStream stream, byte[] expected)
	{
		stream.Position = 0;
		byte[] actual = new byte[expected.Length];
		stream.ReadExactly(actual);
		Assert.That(actual, Is.EqualTo(expected), "decompressed payload must round-trip exactly");
	}

	private static MemoryStream? GetUnderlyingMemoryStream(SmartStream smartStream)
	{
		Stream? underlying = GetUnderlyingStream(smartStream);
		return underlying as MemoryStream;
	}

	private static Stream? GetUnderlyingStream(SmartStream smartStream)
	{
		PropertyInfo? property = typeof(SmartStream).GetProperty(
			"Stream",
			BindingFlags.NonPublic | BindingFlags.Instance);
		return (Stream?)property?.GetValue(smartStream);
	}

	private static void InvokeReadRawWebData(RawBundleFile bundle, Stream dataStream, long metadataOffset)
	{
		// ReadRawWebData is a private instance method on RawWebBundleFile<THeader>; reflect it
		// so we can exercise the temp-file spill path without constructing a full RawBundle byte stream.
		MethodInfo? method = typeof(RawWebBundleFile<RawBundleHeader>)
			.GetMethod("ReadRawWebData", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Could not reflect ReadRawWebData");
		method.Invoke(bundle, new object[] { dataStream, metadataOffset });
	}

	/// <summary>
	/// Temporarily replaces <see cref="BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize"/>
	/// with the supplied value and restores the previous value on dispose. AssetRipper's import
	/// pipeline is single-threaded, so test-time mutation is safe as long as the original value
	/// is restored in <c>finally</c> (which <see cref="IDisposable.Dispose"/> guarantees).
	/// </summary>
	private readonly struct ThresholdScope : IDisposable
	{
		private readonly int previous;

		public ThresholdScope(int newThreshold)
		{
			previous = BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize;
			BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize = newThreshold;
		}

		public void Dispose()
		{
			BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize = previous;
		}
	}
}
