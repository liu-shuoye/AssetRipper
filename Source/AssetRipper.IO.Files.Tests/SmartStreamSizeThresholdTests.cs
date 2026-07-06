using AssetRipper.IO.Files.Streams.Smart;
using System.Reflection;

namespace AssetRipper.IO.Files.Tests;

public class SmartStreamSizeThresholdTests
{
	/// <summary>
	/// 50 MB, matching the default value of
	/// <see cref="BundleFiles.FileStream.BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize"/>.
	/// </summary>
	private const int Threshold = 50 * 1024 * 1024;

	[Test]
	public void CreateBySize_ZeroSize_ReturnsMemoryStream()
	{
		using SmartStream stream = SmartStream.CreateBySize(0, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
	}

	[Test]
	public void CreateBySize_SizeBelowThreshold_ReturnsMemoryStream()
	{
		using SmartStream stream = SmartStream.CreateBySize(Threshold - 1, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
	}

	[Test]
	public void CreateBySize_SizeEqualsThreshold_ReturnsMemoryStream()
	{
		// size <= threshold selects memory (boundary check).
		using SmartStream stream = SmartStream.CreateBySize(Threshold, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.Memory));
	}

	[Test]
	public void CreateBySize_SizeAboveThreshold_ReturnsTempFile()
	{
		using SmartStream stream = SmartStream.CreateBySize(Threshold + 1, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.File));
	}

	[Test]
	public void CreateBySize_IntMaxSize_ReturnsTempFile()
	{
		using SmartStream stream = SmartStream.CreateBySize(int.MaxValue, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.File));
	}

	[Test]
	public void CreateBySize_NegativeSize_ThrowsArgumentOutOfRangeException()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => SmartStream.CreateBySize(-1, Threshold));
	}

	[Test]
	public void CreateBySize_TempFileStream_RoundTripsWrittenBytes()
	{
		const int DataLength = 256;
		byte[] data = RandomData.MakeRandomData(DataLength);

		using SmartStream stream = SmartStream.CreateBySize(Threshold + 1, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.File));

		stream.Write(data, 0, DataLength);
		stream.Position = 0;

		byte[] readBack = new byte[DataLength];
		stream.ReadExactly(readBack);
		Assert.That(readBack, Is.EqualTo(data));
	}

	[Test]
	public void CreateBySize_TempFile_DisposeDeletesUnderlyingFile()
	{
		// SmartStream.CreateTemp uses FileOptions.DeleteOnClose, so disposing the
		// SmartStream must delete the backing temp file.
		SmartStream stream = SmartStream.CreateBySize(Threshold + 1, Threshold);
		Assert.That(stream.StreamType, Is.EqualTo(SmartStreamType.File));

		FileStream fileStream = (FileStream)GetUnderlyingStream(stream)!;
		string filePath = fileStream.Name;
		Assert.That(File.Exists(filePath), Is.True);

		stream.Dispose();

		Assert.That(File.Exists(filePath), Is.False);
		Assert.That(stream.IsNull, Is.True);
	}

	/// <summary>
	/// Gets the private <see cref="SmartStream"/> backing stream via reflection,
	/// so the underlying <see cref="FileStream.Name"/> can be inspected in tests.
	/// </summary>
	private static Stream? GetUnderlyingStream(SmartStream smartStream)
	{
		PropertyInfo? property = typeof(SmartStream).GetProperty(
			"Stream",
			BindingFlags.NonPublic | BindingFlags.Instance);
		return (Stream?)property?.GetValue(smartStream);
	}
}
