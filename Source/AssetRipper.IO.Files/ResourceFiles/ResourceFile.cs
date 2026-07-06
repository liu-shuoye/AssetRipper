using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.ResourceFiles;

public sealed class ResourceFile : FileBase
{
	public ResourceFile(SmartStream stream, string filePath, string name)
	{
		Stream = stream.CreateReference();
		FilePath = filePath;
		Name = name;
	}

	public ResourceFile(byte[] data, string filePath, string name, bool writable = true)
	{
		Stream = SmartStream.CreateMemory(data, 0, data.Length, writable);
		FilePath = filePath;
		Name = name;
	}

	public ResourceFile(string filePath, string name, FileSystem fileSystem)
	{
		Stream = SmartStream.OpenReadMulti(filePath, fileSystem);
		FilePath = filePath;
		Name = name;
	}

	public bool IsDefaultResourceFile() => IsDefaultResourceFile(Name);

	public static bool IsDefaultResourceFile(string fileName)
	{
		string extension = Path.GetExtension(fileName).ToLowerInvariant();
		return extension is ResourceFileExtension or StreamingFileExtension;
	}

	public override string ToString() => Name;

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		Stream.Dispose();
	}

	public override void Read(SmartStream stream)
	{
		throw new NotSupportedException();
	}

	public override void Write(Stream stream)
	{
		Stream.CopyTo(stream);
	}

	public override byte[] ToByteArray()
	{
		return Stream.ToArray();
	}

	/// <summary>
	/// The backing <see cref="SmartStream"/> for this resource's payload.
	/// </summary>
	/// <remarks>
	/// The setter is private so that <see cref="TrySpillToTempFile"/> can replace the
	/// backing stream when spilling a large in-memory payload to a temporary file.
	/// </remarks>
	public SmartStream Stream { get; private set; }

	/// <summary>
	/// Spills the in-memory payload of this <see cref="ResourceFile"/> to a temporary
	/// file when the stream is memory-backed and exceeds <paramref name="spillThreshold"/> bytes.
	/// After the spill, <see cref="Stream"/> references the new temp-file-backed
	/// <see cref="SmartStream"/> and the previous memory-backed stream is released.
	/// </summary>
	/// <param name="spillThreshold">The maximum size, in bytes, of a memory-backed stream
	/// that is allowed to remain in memory. Streams at or below this size are left untouched.</param>
	/// <returns>
	/// The temp-file-backed <see cref="SmartStream"/> that now backs <see cref="Stream"/>
	/// if a spill occurred; otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// The caller is expected to track the returned <see cref="SmartStream"/> so that the
	/// underlying temp file is deterministically deleted on disposal
	/// (see <see cref="SmartStream.CreateTemp"/>, which uses <see cref="FileOptions.DeleteOnClose"/>).
	/// </remarks>
	internal SmartStream? TrySpillToTempFile(int spillThreshold)
	{
		if (spillThreshold <= 0)
		{
			return null;
		}
		SmartStream current = Stream;
		if (current.StreamType != SmartStreamType.Memory)
		{
			// Already file-backed (or null); nothing to spill.
			return null;
		}
		long length = current.Length;
		if (length <= spillThreshold)
		{
			return null;
		}

		SmartStream tempStream = SmartStream.CreateTemp();
		long originalPosition = current.Position;
		current.Position = 0;
		try
		{
			current.CopyTo(tempStream);
		}
		catch
		{
			tempStream.Dispose();
			// Restore the original stream position even on failure.
			current.Position = originalPosition;
			throw;
		}
		tempStream.Position = 0;

		// Replace the backing stream. Take an extra reference on the new temp stream so that
		// disposing ResourceFile.Stream only releases the ResourceFile's contribution, leaving
		// the caller-tracked reference (returned below) alive until the caller disposes it.
		// This mirrors the constructor's `stream.CreateReference()` pattern.
		SmartStream oldStream = Stream;
		Stream = tempStream.CreateReference();
		oldStream.Dispose();

		return tempStream;
	}

	public const string ResourceFileExtension = ".resource";
	public const string StreamingFileExtension = ".ress";
}
