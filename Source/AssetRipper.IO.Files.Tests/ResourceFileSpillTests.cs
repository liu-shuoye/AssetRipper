using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using System.Reflection;

namespace AssetRipper.IO.Files.Tests;

public class ResourceFileSpillTests
{
	private const int SpillThreshold = 1024;

	[Test]
	public void TrySpillToTempFile_ZeroThreshold_ReturnsNull()
	{
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(RandomData.MakeRandomData(8));
		try
		{
			SmartStream? spilled = resourceFile.TrySpillToTempFile(0);
			Assert.That(spilled, Is.Null, "spillThreshold <= 0 should be a no-op");
			Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
		}
		finally
		{
			resourceFile.Dispose();
		}
	}

	[Test]
	public void TrySpillToTempFile_BelowThreshold_ReturnsNull()
	{
		byte[] data = RandomData.MakeRandomData(SpillThreshold);
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(data);
		try
		{
			SmartStream? spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
			Assert.That(spilled, Is.Null);
			Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
		}
		finally
		{
			resourceFile.Dispose();
		}
	}

	[Test]
	public void TrySpillToTempFile_AtThreshold_ReturnsNull()
	{
		// spillThreshold is inclusive: payloads with Length <= spillThreshold stay in memory.
		byte[] data = RandomData.MakeRandomData(SpillThreshold);
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(data);
		try
		{
			SmartStream? spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
			Assert.That(spilled, Is.Null);
			Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
		}
		finally
		{
			resourceFile.Dispose();
		}
	}

	[Test]
	public void TrySpillToTempFile_AboveThreshold_ReturnsTempStreamAndReplacesBackingStream()
	{
		byte[] data = RandomData.MakeRandomData(SpillThreshold + 1);
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(data);
		SmartStream? spilled = null;
		try
		{
			spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
			// Assert before disposing the resource file so the underlying stream is still alive.
			Assert.Multiple(() =>
			{
				Assert.That(spilled, Is.Not.Null, "spill should occur when memory payload exceeds threshold");
				Assert.That(spilled!.StreamType, Is.EqualTo(SmartStreamType.File), "spilled stream must be file-backed");
				Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.File), "ResourceFile.Stream must now reference the temp-backed stream");
				Assert.That(resourceFile.Stream.Length, Is.EqualTo(data.Length));
			});

			// Verify round-trip: the temp-backed stream contains the original bytes.
			resourceFile.Stream.Position = 0;
			byte[] readBack = new byte[data.Length];
			resourceFile.Stream.ReadExactly(readBack);
			Assert.That(readBack, Is.EqualTo(data));
		}
		finally
		{
			resourceFile.Dispose();
			spilled?.Dispose();
		}
	}

	[Test]
	public void TrySpillToTempFile_AboveThreshold_CreatesTempFileThatIsDeletedOnDispose()
	{
		// SmartStream.CreateTemp uses FileOptions.DeleteOnClose, so disposing the
		// spilled SmartStream must delete the backing temp file.
		byte[] data = RandomData.MakeRandomData(SpillThreshold + 8);
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(data);
		SmartStream? spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
		Assert.That(spilled, Is.Not.Null);

		FileStream fileStream = (FileStream)GetUnderlyingStream(spilled!)!;
		string tempFilePath = fileStream.Name;
		Assert.That(File.Exists(tempFilePath), Is.True, "temp file should exist while the stream is alive");

		// Dispose both the spilled stream and the resource file. Both share the underlying
		// file via SmartStream's refcount; only after both are disposed is the file deleted.
		spilled!.Dispose();
		Assert.That(File.Exists(tempFilePath), Is.True, "temp file must still exist while ResourceFile holds a reference");
		resourceFile.Dispose();
		Assert.That(File.Exists(tempFilePath), Is.False, "temp file must be deleted after both holders dispose");
		Assert.That(spilled.IsNull, Is.True, "spilled SmartStream must release its underlying stream on dispose");
	}

	[Test]
	public void TrySpillToTempFile_AlreadyFileBacked_ReturnsNull()
	{
		// File-backed streams (FileStream or MultiFileStream) should not be spilled again.
		SmartStream memoryStream = SmartStream.CreateMemory(RandomData.MakeRandomData(SpillThreshold + 1));
		SmartStream tempStream = SmartStream.CreateTemp();
		memoryStream.Position = 0;
		memoryStream.CopyTo(tempStream);
		tempStream.Position = 0;
		ResourceFile resourceFile = new ResourceFile(tempStream, "fp", "name");
		try
		{
			SmartStream? spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
			Assert.That(spilled, Is.Null, "file-backed streams should not be spilled again");
			Assert.That(resourceFile.Stream.StreamType, Is.EqualTo(SmartStreamType.File));
		}
		finally
		{
			resourceFile.Dispose();
			tempStream.Dispose();
			memoryStream.Dispose();
		}
	}

	[Test]
	public void TrySpillToTempFile_PreservesContentPositionAtStart()
	{
		// After spilling, ResourceFile.Stream.Position should be 0 so subsequent reads work.
		byte[] data = RandomData.MakeRandomData(SpillThreshold + 16);
		ResourceFile resourceFile = CreateMemoryBackedResourceFile(data);
		// Advance the memory stream position to verify it gets reset on the spilled stream.
		resourceFile.Stream.Position = 4;
		SmartStream? spilled = null;
		try
		{
			spilled = resourceFile.TrySpillToTempFile(SpillThreshold);
			Assert.That(spilled, Is.Not.Null);
			Assert.That(resourceFile.Stream.Position, Is.EqualTo(0), "spilled stream position must start at 0");
		}
		finally
		{
			resourceFile.Dispose();
			spilled?.Dispose();
		}
	}

	private static ResourceFile CreateMemoryBackedResourceFile(byte[] data)
	{
		// Use the byte[] constructor so the ResourceFile owns a memory-backed stream
		// without sharing refcount with a caller-held SmartStream.
		return new ResourceFile(data, "memory", "memory.resource");
	}

	/// <summary>
	/// Gets the private <see cref="SmartStream"/> backing stream via reflection,
	/// so the underlying <see cref="FileStream.Name"/> can be inspected in tests.
	/// Mirrors the helper in <see cref="SmartStreamSizeThresholdTests"/>.
	/// </summary>
	private static Stream? GetUnderlyingStream(SmartStream smartStream)
	{
		PropertyInfo? property = typeof(SmartStream).GetProperty(
			"Stream",
			BindingFlags.NonPublic | BindingFlags.Instance);
		return (Stream?)property?.GetValue(smartStream);
	}
}
