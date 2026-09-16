using AssetRipper.IO.Files.BundleFiles;
using AssetRipper.IO.Files.SerializedFiles;

namespace AssetRipper.IO.Files.Tests;

/// <summary>
/// 验证批量扫描路径与原有的逐文件判定实现结果一致。
/// </summary>
/// <remarks>
/// 扫描是导入流程里最先执行、也最容易改错语义的一步：它决定了后续会加载哪些文件。
/// 因此这里以「原实现」为基准逐项比对，而不是只断言新实现自己的输出。
/// </remarks>
public class FileTypeScannerTests
{
	/// <summary>
	/// 构造一个同时包含资源包、序列化文件与干扰文件的数据集，逐项比对两种判定方式的结论。
	/// </summary>
	[Test]
	public void BatchScanAgreesWithPerFileDetection()
	{
		using TempDirectory temp = new();

		// 资源包：magic + 版本号 + 两个以零结尾的版本串，长度需达到读头阈值
		temp.WriteFile("bundle_a.unity3d", CreateBundleBytes());
		temp.WriteFile("bundle_b.bundle", CreateBundleBytes());

		// 非资源包：同尺寸但 magic 不匹配，用于确认判据不是仅凭长度
		temp.WriteFile("not_a_bundle.unity3d", CreateNonBundleBytes());

		// 干扰项：扩展名已足以排除，且内容为任意数据
		temp.WriteFile("noise.png", new byte[512]);
		temp.WriteFile("noise.small", new byte[4]);

		List<string> allFiles = [.. System.IO.Directory.EnumerateFiles(temp.Path)];

		// 原实现：逐个文件打开流读头
		List<string> expectedBundles = [];
		foreach (string path in allFiles)
		{
			if (BundleHeader.IsBundleHeader(path, temp.FileSystem))
			{
				expectedBundles.Add(path);
			}
		}

		List<KeyValuePair<string, string>> actual = FileTypeScanner.ScanBundles(temp.FileSystem, temp.Path);

		Assert.That(
			actual.Select(pair => pair.Value).OrderBy(path => path),
			Is.EqualTo(expectedBundles.OrderBy(path => path)),
			"批量扫描的资源包集合必须与逐文件判定完全一致");

		// 名称约定：不含扩展名并小写化，供 Files 字典按名索引使用
		foreach (KeyValuePair<string, string> pair in actual)
		{
			Assert.That(pair.Key, Is.EqualTo(Path.GetFileNameWithoutExtension(pair.Value).ToLowerInvariant()));
		}
	}

	/// <summary>
	/// 序列化文件同样需要与逐文件判定保持一致。
	/// </summary>
	[Test]
	public void SerializedFileScanAgreesWithPerFileDetection()
	{
		using TempDirectory temp = new();

		temp.WriteFile("sample.assets", CreateSerializedFileBytes());
		temp.WriteFile("trap.assets", CreateNonBundleBytes());
		temp.WriteFile("noise.wav", new byte[256]);

		List<string> allFiles = [.. System.IO.Directory.EnumerateFiles(temp.Path)];

		List<string> expected = [];
		foreach (string path in allFiles)
		{
			if (SerializedFile.IsSerializedFile(path, temp.FileSystem))
			{
				expected.Add(path);
			}
		}

		List<KeyValuePair<string, string>> actual = FileTypeScanner.ScanSerializedFiles(temp.FileSystem, temp.Path);

		Assert.That(
			actual.Select(pair => pair.Value).OrderBy(path => path),
			Is.EqualTo(expected.OrderBy(path => path)),
			"批量扫描的序列化文件集合必须与逐文件判定完全一致");
	}

	/// <summary>
	/// 扩展名与体积筛选只允许排除「不可能命中」的文件，不能漏掉任何真实命中项。
	/// </summary>
	[Test]
	public void SizeAndExtensionFiltersNeverExcludeRealMatches()
	{
		Assert.That(FileTypeScanner.CouldBeSerializedFile(FileSystem.UnknownSize), Is.True, "大小未知时必须保守命中");
		Assert.That(FileTypeScanner.CouldBeBundle(FileSystem.UnknownSize), Is.True, "大小未知时必须保守命中");

		// 体积筛选只应砍掉明显过小的文件
		Assert.That(FileTypeScanner.CouldBeBundle(0), Is.False);
		Assert.That(FileTypeScanner.CouldBeSerializedFile(0), Is.False);
		Assert.That(FileTypeScanner.CouldBeBundle(1024), Is.True);
		Assert.That(FileTypeScanner.CouldBeSerializedFile(1024), Is.True);

		// 媒体类扩展名可确定排除；资源类扩展名必须保留
		Assert.That(FileTypeScanner.IsDefinitelyNotUnityContent("foo.png"), Is.True);
		Assert.That(FileTypeScanner.IsDefinitelyNotUnityContent("foo.unity3d"), Is.False);
		Assert.That(FileTypeScanner.IsDefinitelyNotUnityContent("foo.assets"), Is.False);
		Assert.That(FileTypeScanner.IsDefinitelyNotUnityContent("foo"), Is.False);
	}

	/// <summary>
	/// 批量读取应返回每个文件的实际可读字节数，缺失文件以 -1 表示而不是抛异常。
	/// </summary>
	[Test]
	public void BatchReadReturnsLengthsAndToleratesMissingFiles()
	{
		using TempDirectory temp = new();
		string small = temp.WriteFile("small.bin", new byte[5]);
		string big = temp.WriteFile("big.bin", new byte[100]);
		string missing = System.IO.Path.Combine(temp.Path, "missing.bin");

		List<string> paths = [small, big, missing];
		byte[][] buffers = paths.Select(_ => new byte[32]).ToArray();

		int[] lengths = temp.FileSystem.BatchReadHeaderPrefix(paths, 32, buffers, concurrency: 4);

		Assert.That(lengths[0], Is.EqualTo(5), "读取长度应如实反映文件较短的情况");
		Assert.That(lengths[1], Is.EqualTo(32), "读取长度应被缓冲区大小封顶");
		Assert.That(lengths[2], Is.EqualTo(-1), "缺失文件应返回 -1 而不是抛出");
	}

	/// <summary>构造一个合法的 UnityFS 资源包头字节序列，并在其后填充至读头阈值以上。</summary>
	private static byte[] CreateBundleBytes()
	{
		byte[] data = new byte[64];
		"UnityFS"u8.CopyTo(data);
		// magic 之后是以零结尾的字符串，随后的整型版本号在此不必合法，因为判据只看 magic
		return data;
	}

	/// <summary>构造长度足够但 magic 不匹配的数据，用于确认判定不是仅凭长度。</summary>
	private static byte[] CreateNonBundleBytes()
	{
		byte[] data = new byte[64];
		"NotABundle"u8.CopyTo(data);
		return data;
	}

	/// <summary>
	/// 构造一个能通过序列化文件头校验的字节序列：元数据大小、文件大小与格式版本都必须自洽。
	/// </summary>
	private static byte[] CreateSerializedFileBytes()
	{
		const int Size = 128;
		byte[] data = new byte[Size];

		// 头部与元数据始终为大端序
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(data, 13); // 元数据大小 = 最小值
		System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), Size); // 声明文件大小必须等于真实大小
		System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), 17); // 一个已知的格式版本
		return data;
	}
}
