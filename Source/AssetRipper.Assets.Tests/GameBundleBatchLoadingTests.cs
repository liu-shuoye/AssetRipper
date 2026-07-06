using AssetRipper.Assets.Bundles;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;
using System.Reflection;

namespace AssetRipper.Assets.Tests;

public class GameBundleBatchLoadingTests
{
	/// <summary>
	/// The internal <see cref="GameBundle.RegisterTempStream"/> method should add the stream
	/// to the tracked list so <see cref="GameBundle.Dispose"/> releases it deterministically.
	/// </summary>
	[Test]
	public void RegisterTempStream_StreamIsTrackedForDisposal()
	{
		GameBundle gameBundle = new();
		SmartStream tempStream = SmartStream.CreateTemp();
		// Write some bytes so the underlying temp file is materialized on disk.
		tempStream.Write(new byte[] { 1, 2, 3 }, 0, 3);
		tempStream.Position = 0;

		FileStream? fileStream = GetUnderlyingFileStream(tempStream);
		Assert.That(fileStream, Is.Not.Null, "CreateTemp should return a FileStream-backed SmartStream");
		string tempFilePath = fileStream!.Name;
		Assert.That(File.Exists(tempFilePath), Is.True, "temp file should exist while the stream is alive");

		gameBundle.RegisterTempStream(tempStream);

		gameBundle.Dispose();

		Assert.That(File.Exists(tempFilePath), Is.False, "temp file must be deleted once the GameBundle is disposed");
		Assert.That(tempStream.IsNull, Is.True, "tracked SmartStream must be disposed by GameBundle.Dispose");
	}

	/// <summary>
	/// Disposing a <see cref="GameBundle"/> that has no tracked temp streams should not throw
	/// and should leave the bundle in a disposed state (idempotent disposal).
	/// </summary>
	[Test]
	public void Dispose_WithNoTempStreams_DoesNotThrow()
	{
		GameBundle gameBundle = new();
		Assert.DoesNotThrow(() => gameBundle.Dispose());
		// Disposing again should be a no-op.
		Assert.DoesNotThrow(() => gameBundle.Dispose());
	}

	/// <summary>
	/// Disposing a <see cref="GameBundle"/> with tracked temp streams should be idempotent:
	/// a second Dispose call should not throw even if the streams are already gone.
	/// </summary>
	[Test]
	public void Dispose_CalledTwice_DoesNotThrowAndDeletesTempFileOnce()
	{
		GameBundle gameBundle = new();
		SmartStream tempStream = SmartStream.CreateTemp();
		tempStream.Write(new byte[] { 9, 8, 7 }, 0, 3);

		FileStream? fileStream = GetUnderlyingFileStream(tempStream);
		string tempFilePath = fileStream!.Name;

		gameBundle.RegisterTempStream(tempStream);

		gameBundle.Dispose();
		Assert.That(File.Exists(tempFilePath), Is.False);

		Assert.DoesNotThrow(() => gameBundle.Dispose());
	}

	/// <summary>
	/// A spilled <see cref="ResourceFile"/> added to a <see cref="GameBundle"/> should have
	/// its underlying temp file cleaned up when the bundle is disposed, even if the
	/// <see cref="ResourceFile"/> is also disposed by the base <see cref="Bundle.Dispose"/>
	/// implementation (SmartStream handles double-dispose via its refcount).
	/// </summary>
	[Test]
	public void Dispose_DisposesResourceFilesAndTrackedTempStreams()
	{
		GameBundle gameBundle = new();
		byte[] data = new byte[1024 + 16];
		new Random(42).NextBytes(data);

		ResourceFile resourceFile = new ResourceFile(data, "fp", "large.resource");
		// Spill the in-memory payload to a temp file, then register the temp stream.
		SmartStream? spilled = resourceFile.TrySpillToTempFile(1024);
		Assert.That(spilled, Is.Not.Null);

		FileStream? fileStream = GetUnderlyingFileStream(spilled!);
		string tempFilePath = fileStream!.Name;
		Assert.That(File.Exists(tempFilePath), Is.True);

		gameBundle.RegisterTempStream(spilled!);

		gameBundle.AddResource(resourceFile);
		gameBundle.Dispose();

		Assert.That(File.Exists(tempFilePath), Is.False, "temp file must be deleted once the GameBundle is disposed");
	}

	/// <summary>
	/// Multiple registered temp streams should all be released by a single
	/// <see cref="GameBundle.Dispose"/> call. This simulates batch loading many spilled
	/// ResourceFiles into a single GameBundle.
	/// </summary>
	[Test]
	public void Dispose_ReleasesAllTrackedTempStreams()
	{
		GameBundle gameBundle = new();
		const int StreamCount = 5;
		List<string> tempFilePaths = new(StreamCount);
		List<SmartStream> tempStreams = new(StreamCount);
		for (int i = 0; i < StreamCount; i++)
		{
			SmartStream stream = SmartStream.CreateTemp();
			stream.Write(new byte[] { (byte)i }, 0, 1);
			tempStreams.Add(stream);
			FileStream? fileStream = GetUnderlyingFileStream(stream);
			tempFilePaths.Add(fileStream!.Name);
			gameBundle.RegisterTempStream(stream);
		}

		gameBundle.Dispose();

		foreach (string path in tempFilePaths)
		{
			Assert.That(File.Exists(path), Is.False, $"temp file {path} should be deleted");
		}
		foreach (SmartStream stream in tempStreams)
		{
			Assert.That(stream.IsNull, Is.True, "all tracked SmartStreams should be disposed");
		}
	}

	/// <summary>
	/// Reflects on the private <c>Stream</c> field of <see cref="SmartStream"/> to obtain
	/// the underlying <see cref="FileStream"/> for temp-file existence checks.
	/// </summary>
	private static FileStream? GetUnderlyingFileStream(SmartStream smartStream)
	{
		PropertyInfo? property = typeof(SmartStream).GetProperty(
			"Stream",
			BindingFlags.NonPublic | BindingFlags.Instance);
		return property?.GetValue(smartStream) as FileStream;
	}
}
