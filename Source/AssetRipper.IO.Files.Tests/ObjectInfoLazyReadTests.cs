using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.IO;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams.Smart;
using System.Reflection;

namespace AssetRipper.IO.Files.Tests;

public class ObjectInfoLazyReadTests
{
	[Test]
	public void ReadAssetDataAt_ReadsCorrectBytes()
	{
		byte[] data = RandomData.MakeRandomData(64);
		using SmartStream stream = SmartStream.CreateMemory(data);
		byte[] read = SerializedReader.ReadAssetDataAt(stream, offset: 0, size: data.Length);
		Assert.That(read, Is.EqualTo(data));
	}

	[Test]
	public void ReadAssetDataAt_ReadsFromOffset()
	{
		byte[] data = RandomData.MakeRandomData(64);
		using SmartStream stream = SmartStream.CreateMemory(data);
		byte[] read = SerializedReader.ReadAssetDataAt(stream, offset: 16, size: 32);
		Assert.That(read, Is.EqualTo(data.AsSpan(16, 32).ToArray()));
	}

	[Test]
	public void ReadAssetDataAt_RestoresStreamPosition()
	{
		byte[] data = RandomData.MakeRandomData(64);
		using SmartStream stream = SmartStream.CreateMemory(data);
		stream.Position = 7;
		_ = SerializedReader.ReadAssetDataAt(stream, offset: 16, size: 32);
		Assert.That(stream.Position, Is.EqualTo(7));
	}

	[Test]
	public void ReadAssetDataAt_ZeroSizeReturnsEmptyArray()
	{
		byte[] data = RandomData.MakeRandomData(16);
		using SmartStream stream = SmartStream.CreateMemory(data);
		byte[] read = SerializedReader.ReadAssetDataAt(stream, offset: 0, size: 0);
		Assert.That(read, Is.Empty);
	}

	[Test]
	public void ReadAssetDataAt_NullStreamThrows()
	{
		Assert.Throws<ArgumentNullException>(() => SerializedReader.ReadAssetDataAt(null!, offset: 0, size: 1));
	}

	[Test]
	public void ReadAssetDataAt_NegativeSizeThrows()
	{
		using SmartStream stream = SmartStream.CreateMemory(new byte[4]);
		Assert.Throws<ArgumentOutOfRangeException>(() => SerializedReader.ReadAssetDataAt(stream, offset: 0, size: -1));
	}

	[Test]
	public void ObjectData_IsNotLoadedAfterSerializedFileRead()
	{
		SerializedFile original = BuildTestFile(RandomData.MakeRandomData(100));
		SerializedFile read = WriteAndReadBack(original);

		// Take a fresh struct copy without touching ObjectData so the lazy cache stays empty.
		ObjectInfo info = read.Objects[0];
		Assert.That(GetObjectDataField(info), Is.Null, "object data should not be loaded eagerly during metadata read");
		Assert.That(GetOwningStreamField(info), Is.Not.Null, "the owning SmartStream reference should be captured for lazy reads");
	}

	[Test]
	public void ObjectData_LazyReadReturnsCorrectBytes()
	{
		byte[] originalData = RandomData.MakeRandomData(100);
		SerializedFile original = BuildTestFile(originalData);
		SerializedFile read = WriteAndReadBack(original);

		ObjectInfo info = read.Objects[0];
		byte[] firstRead = info.ObjectData;
		Assert.That(firstRead, Is.EqualTo(originalData));
		Assert.That(GetObjectDataField(info), Is.Not.Null, "lazy read should cache the byte[] after first access");
	}

	[Test]
	public void ObjectData_SecondAccessReturnsCachedInstance()
	{
		SerializedFile original = BuildTestFile(RandomData.MakeRandomData(50));
		SerializedFile read = WriteAndReadBack(original);

		ObjectInfo info = read.Objects[0];
		byte[] firstRead = info.ObjectData;
		byte[] secondRead = info.ObjectData;
		Assert.That(ReferenceEquals(firstRead, secondRead), Is.True, "second access should return the cached byte[] instance");
	}

	[Test]
	public void ReleaseObjectData_ClearsCache()
	{
		SerializedFile original = BuildTestFile(RandomData.MakeRandomData(50));
		SerializedFile read = WriteAndReadBack(original);

		ObjectInfo info = read.Objects[0];
		_ = info.ObjectData;
		Assert.That(GetObjectDataField(info), Is.Not.Null);
		info.ReleaseObjectData();
		Assert.That(GetObjectDataField(info), Is.Null, "ReleaseObjectData should clear the cached byte[]");
	}

	[Test]
	public void ObjectData_CanBeRereadAfterRelease()
	{
		byte[] originalData = RandomData.MakeRandomData(75);
		SerializedFile original = BuildTestFile(originalData);
		SerializedFile read = WriteAndReadBack(original);

		ObjectInfo info = read.Objects[0];
		byte[] firstRead = info.ObjectData;
		Assert.That(firstRead, Is.EqualTo(originalData));
		info.ReleaseObjectData();

		// Re-reading after release should not throw and should return the correct bytes.
		byte[] secondRead = info.ObjectData;
		Assert.That(secondRead, Is.EqualTo(originalData));
		Assert.That(ReferenceEquals(firstRead, secondRead), Is.False, "release should force a fresh read rather than reusing the cached array");
	}

	[Test]
	public void ReleaseStreamReference_MakesObjectDataReturnEmpty()
	{
		SerializedFile original = BuildTestFile(RandomData.MakeRandomData(40));
		SerializedFile read = WriteAndReadBack(original);

		ObjectInfo info = read.Objects[0];
		Assert.That(GetOwningStreamField(info), Is.Not.Null);
		info.ReleaseStreamReference();
		Assert.That(GetOwningStreamField(info), Is.Null);
		Assert.That(info.ObjectData, Is.Empty, "with the stream released, ObjectData should fall back to an empty array");
	}

	[Test]
	public void SerializedFile_DisposeReleasesStreamReferences()
	{
		SerializedFile original = BuildTestFile(RandomData.MakeRandomData(40));
		SerializedFile read = WriteAndReadBack(original);

		// Before dispose the array element holds a stream reference.
		Assert.That(GetOwningStreamField(read.Objects[0]), Is.Not.Null);
		read.Dispose();
		// After dispose the array element's stream reference should be released.
		Assert.That(GetOwningStreamField(read.Objects[0]), Is.Null);
	}

	private static SerializedFile BuildTestFile(byte[] objectData)
	{
		SerializedFileBuilder builder = new()
		{
			Generation = FormatVersion.LargeFilesSupport,
			Version = new(6000, 1, 0),
			Platform = BuildTarget.StandaloneWin64Player,
			EndianType = EndianType.LittleEndian,
			HasTypeTree = false,
		};
		SerializedType type = new()
		{
			TypeID = 1,
			IsStrippedType = false,
			ScriptTypeIndex = -1,
		};
		ObjectInfo obj = new(type)
		{
			FileID = 1,
			SerializedTypeIndex = 0,
			ObjectData = objectData,
		};
		builder.Types.Add(type);
		builder.Objects.Add(obj);
		return builder.Build();
	}

	private static SerializedFile WriteAndReadBack(SerializedFile original)
	{
		using SmartStream stream = SmartStream.CreateMemory();
		original.Write(stream);
		stream.Flush();
		stream.Position = 0;
		return SerializedFileScheme.Default.Read(stream, original.FilePath, original.Name);
	}

	private static byte[]? GetObjectDataField(ObjectInfo info)
	{
		FieldInfo? field = typeof(ObjectInfo).GetField("objectData", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.That(field, Is.Not.Null, "objectData backing field should exist");
		return (byte[]?)field!.GetValue(info);
	}

	private static SmartStream? GetOwningStreamField(ObjectInfo info)
	{
		FieldInfo? field = typeof(ObjectInfo).GetField("owningStream", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.That(field, Is.Not.Null, "owningStream field should exist");
		return (SmartStream?)field!.GetValue(info);
	}
}
