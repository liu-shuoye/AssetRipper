using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.IO.Files.SerializedFiles.IO;

internal sealed class SerializedReader : EndianReader
{
	public SerializedReader(Stream stream, EndianType endianess, FormatVersion generation) : base(stream, endianess)
	{
		Generation = generation;
	}

	/// <summary>
	/// Reads a block of object data from the given <paramref name="stream"/> at the specified <paramref name="offset"/>.
	/// </summary>
	/// <remarks>
	/// The <paramref name="stream"/> position is saved and restored, so this method is safe to call
	/// while iterating the same stream for other purposes. The stream is not thread-safe; concurrent
	/// calls to this method on the same <see cref="SmartStream"/> instance must be externally synchronized.
	/// </remarks>
	/// <param name="stream">The <see cref="SmartStream"/> to read from. Must not be null.</param>
	/// <param name="offset">The absolute offset within <paramref name="stream"/> to read from.</param>
	/// <param name="size">The number of bytes to read. Must be non-negative.</param>
	/// <returns>A new byte array of length <paramref name="size"/> containing the object data.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
	public static byte[] ReadAssetDataAt(SmartStream stream, long offset, int size)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentOutOfRangeException.ThrowIfNegative(size);
		byte[] buffer = new byte[size];
		if (size == 0)
		{
			return buffer;
		}
		long previousPosition = stream.Position;
		stream.Position = offset;
		try
		{
			stream.ReadExactly(buffer);
		}
		finally
		{
			stream.Position = previousPosition;
		}
		return buffer;
	}

	public FileIdentifier[] ReadFileIdentifierArray()
	{
		int count = ReadInt32();
		FileIdentifier[] array = new FileIdentifier[count];
		for (int i = 0; i < count; i++)
		{
			FileIdentifier instance = new();
			instance.Read(this);
			array[i] = instance;
		}
		return array;
	}

	public LocalSerializedObjectIdentifier[] ReadLocalSerializedObjectIdentifierArray()
	{
		int count = ReadInt32();
		LocalSerializedObjectIdentifier[] array = new LocalSerializedObjectIdentifier[count];
		for (int i = 0; i < count; i++)
		{
			LocalSerializedObjectIdentifier instance = new();
			instance.Read(this);
			array[i] = instance;
		}
		return array;
	}

	public T[] ReadSerializedTypeArray<T>(bool hasTypeTree) where T : SerializedTypeBase, new()
	{
		int count = ReadInt32();
		T[] array = new T[count];
		for (int i = 0; i < count; i++)
		{
			T instance = new();
			instance.Read(this, hasTypeTree);
			array[i] = instance;
		}
		return array;
	}

	public ObjectInfo[] ReadObjectInfoArray(bool longFileID, ReadOnlySpan<SerializedType> types, long dataOffset)
	{
		int count = ReadInt32();
		ObjectInfo[] array = new ObjectInfo[count];
		for (int i = 0; i < count; i++)
		{
			ObjectInfo instance = new();
			instance.Read(this, longFileID, types, dataOffset);
			array[i] = instance;
		}
		return array;
	}

	public FormatVersion Generation { get; }

	/// <summary>
	/// Gets set after reading the metadata version
	/// </summary>
	public UnityVersion Version { get; set; }
}
