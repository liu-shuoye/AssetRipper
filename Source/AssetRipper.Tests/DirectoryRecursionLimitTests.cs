using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Platforms;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;

namespace AssetRipper.Tests;

/// <summary>
/// Tests for the directory recursion depth and collected file count limits added in
/// <see cref="ImportSettings.MaxRecursiveDirectoryDepth"/> and
/// <see cref="ImportSettings.MaxCollectedFiles"/>. The limits are exercised both through
/// <see cref="MixedGameStructure"/>'s <c>CollectFromDirectory</c> (via the public
/// constructor) and <see cref="PlatformGameStructure.CollectAssetBundlesRecursively"/>.
/// </summary>
/// <remarks>
/// These tests use <see cref="LocalFileSystem"/> with a temporary directory on disk rather
/// than <see cref="VirtualFileSystem"/>. <see cref="VirtualFileSystem"/> has two limitations
/// that prevent it from being used here:
/// <list type="number">
///   <item>
///     <see cref="MultiFileStream.Exists"/> internally calls
///     <c>Directory.GetFiles(dir, "name.split*")</c>. <see cref="VirtualFileSystem"/> only
///     supports <c>*</c> and <c>*.</c><c>ext</c> search patterns and throws
///     <see cref="NotImplementedException"/> for <c>prefix*</c> patterns.
///   </item>
///   <item>
///     <see cref="VirtualFileSystem"/>'s <c>OpenRead</c> returns a reference to the shared
///     underlying stream without resetting <see cref="Stream.Position"/> to zero. After
///     <c>WriteAllBytes</c>, the position is at the end of the stream, causing
///     <see cref="BundleHeader.IsBundleHeader"/> to throw <see cref="EndOfStreamException"/>.
///   </item>
/// </list>
/// <see cref="LocalFileSystem"/> delegates to <see cref="System.IO.Directory"/> and
/// <see cref="System.IO.File"/>, which support arbitrary search patterns and always open
/// files at position zero.
/// </remarks>
public class DirectoryRecursionLimitTests
{
	private CapturingLogger _logger = null!;
	private string _tempRoot = null!;

	[SetUp]
	public void SetUp()
	{
		_logger = new CapturingLogger();
		Logger.Add(_logger);
		// Use a short, unique subdirectory of the system temp path to avoid collisions
		// between parallel test runs and to keep paths well under any OS limit.
		_tempRoot = Path.Combine(Path.GetTempPath(), "AR_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempRoot);
	}

	[TearDown]
	public void TearDown()
	{
		Logger.Remove(_logger);
		try
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
		catch
		{
			// Best-effort cleanup; ignore failures (e.g. files locked by another process).
		}
	}

	/// <summary>
	/// Creates a minimal UnityFS bundle header. <see cref="BundleHeader.IsBundleHeader"/>
	/// requires the file to be at least <c>0x20</c> bytes and to start with the magic
	/// string followed by a null terminator. The remaining bytes are zero-padded.
	/// </summary>
	private static byte[] CreateUnityBundleBytes()
	{
		byte[] bytes = new byte[0x20];
		byte[] magic = System.Text.Encoding.ASCII.GetBytes("UnityFS");
		Buffer.BlockCopy(magic, 0, bytes, 0, magic.Length);
		// bytes[magic.Length] is already 0 (null terminator)
		return bytes;
	}

	/// <summary>
	/// Creates a chain of nested directories
	/// <c>{root}/l0/l1/.../l{depth-1}</c> with one bundle file placed in each
	/// <c>lN</c> directory. The <paramref name="root"/> itself contains no bundle.
	/// </summary>
	private void CreateDeepBundleTree(string root, int depth, byte[] bundleBytes)
	{
		Directory.CreateDirectory(root);
		string current = root;
		for (int i = 0; i < depth; i++)
		{
			current = Path.Combine(current, $"l{i}");
			Directory.CreateDirectory(current);
			File.WriteAllBytes(Path.Combine(current, $"b{i}.unity3d"), bundleBytes);
		}
	}

	/// <summary>
	/// A <see cref="PlatformGameStructure"/> subclass that exposes the protected
	/// <see cref="PlatformGameStructure.CollectAssetBundlesRecursively"/> method for testing.
	/// </summary>
	private sealed class TestablePlatformGameStructure : PlatformGameStructure
	{
		public TestablePlatformGameStructure(FileSystem fileSystem, ImportSettings? importSettings = null)
			: base(fileSystem, importSettings)
		{
		}

		public void InvokeCollectAssetBundlesRecursively(string root, List<KeyValuePair<string, string>> files, int currentDepth = 0)
			=> CollectAssetBundlesRecursively(root, files, currentDepth);
	}

	private sealed class CapturingLogger : ILogger
	{
		public List<string> Warnings { get; } = [];

		public void Log(LogType type, LogCategory category, string message)
		{
			if (type == LogType.Warning)
			{
				Warnings.Add(message);
			}
		}

		public void BlankLine(int numLines)
		{
		}
	}

	// ---------------------------------------------------------------------------
	// CollectFromDirectory (via MixedGameStructure constructor)
	// ---------------------------------------------------------------------------

	[Test]
	public void CollectFromDirectory_NormalDepth_ScansAllFiles()
	{
		// Default MaxRecursiveDirectoryDepth = 32, tree depth = 5. Nothing is truncated.
		string root = Path.Combine(_tempRoot, "game");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 5, bundleBytes);

		MixedGameStructure structure = new([root], LocalFileSystem.Instance);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(structure.Files.Count, Is.EqualTo(5), "All five bundles should be collected.");
			Assert.That(_logger.Warnings, Is.Empty, "No warnings should be emitted within the configured depth.");
		}
	}

	[Test]
	public void CollectFromDirectory_ExceedsMaxDepth_StopsAndWarnsWithoutThrowing()
	{
		// Default MaxRecursiveDirectoryDepth = 32, tree depth = 100. The scan must stop at
		// depth 32 and emit a warning instead of recursing further (or overflowing).
		string root = Path.Combine(_tempRoot, "game");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 100, bundleBytes);

		MixedGameStructure structure = null!;
		Assert.DoesNotThrow(() => structure = new MixedGameStructure([root], LocalFileSystem.Instance));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(_logger.Warnings, Is.Not.Empty, "A depth-truncation warning should be emitted.");
			Assert.That(_logger.Warnings[0], Does.Contain("maximum directory recursion depth"));
			// Root is depth 0 (no bundle). l0..l30 are scanned at depths 1..31, and
			// l31 at depth 32 is skipped. That yields 31 bundles (b0..b30).
			Assert.That(structure.Files.Count, Is.LessThan(100), "Truncation must prevent scanning the full tree.");
			Assert.That(structure.Files.Count, Is.EqualTo(31));
		}
	}

	[Test]
	public void CollectFromDirectory_ExceedsMaxFiles_StopsAndWarnsWithoutThrowing()
	{
		// Use a small MaxCollectedFiles to avoid creating hundreds of thousands of on-disk
		// files. The truncation logic is identical regardless of the configured limit, so the
		// default 100_000 limit would behave the same way once that many files are present.
		string root = Path.Combine(_tempRoot, "game");
		byte[] bundleBytes = CreateUnityBundleBytes();
		// 30 nested directories each with one bundle. MaxRecursiveDirectoryDepth stays at
		// its default (32) so only the file count limit triggers.
		CreateDeepBundleTree(root, depth: 30, bundleBytes);

		ImportSettings settings = new() { MaxCollectedFiles = 10 };

		MixedGameStructure structure = null!;
		Assert.DoesNotThrow(() => structure = new MixedGameStructure([root], LocalFileSystem.Instance, settings));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(_logger.Warnings, Is.Not.Empty, "A file-count warning should be emitted.");
			Assert.That(_logger.Warnings[0], Does.Contain("maximum collected files limit"));
			Assert.That(structure.Files.Count, Is.EqualTo(10), "Collection must stop once the limit is reached.");
		}
	}

	// ---------------------------------------------------------------------------
	// CollectAssetBundlesRecursively (via TestablePlatformGameStructure)
	// ---------------------------------------------------------------------------

	[Test]
	public void CollectAssetBundlesRecursively_NormalDepth_ScansAllFiles()
	{
		string root = Path.Combine(_tempRoot, "root");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 5, bundleBytes);

		TestablePlatformGameStructure structure = new(LocalFileSystem.Instance);
		List<KeyValuePair<string, string>> files = [];

		structure.InvokeCollectAssetBundlesRecursively(root, files);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(files.Count, Is.EqualTo(5));
			Assert.That(_logger.Warnings, Is.Empty);
		}
	}

	[Test]
	public void CollectAssetBundlesRecursively_ExceedsMaxDepth_StopsAndWarnsWithoutThrowing()
	{
		string root = Path.Combine(_tempRoot, "root");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 100, bundleBytes);

		TestablePlatformGameStructure structure = new(LocalFileSystem.Instance);
		List<KeyValuePair<string, string>> files = [];

		Assert.DoesNotThrow(() => structure.InvokeCollectAssetBundlesRecursively(root, files));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(_logger.Warnings, Is.Not.Empty);
			Assert.That(_logger.Warnings[0], Does.Contain("maximum directory recursion depth"));
			Assert.That(files.Count, Is.EqualTo(31));
			Assert.That(files.Count, Is.LessThan(100));
		}
	}

	[Test]
	public void CollectAssetBundlesRecursively_ExceedsMaxFiles_StopsAndWarnsWithoutThrowing()
	{
		string root = Path.Combine(_tempRoot, "root");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 30, bundleBytes);

		ImportSettings settings = new() { MaxCollectedFiles = 10 };
		TestablePlatformGameStructure structure = new(LocalFileSystem.Instance, settings);
		List<KeyValuePair<string, string>> files = [];

		Assert.DoesNotThrow(() => structure.InvokeCollectAssetBundlesRecursively(root, files));

		using (Assert.EnterMultipleScope())
		{
			Assert.That(_logger.Warnings, Is.Not.Empty);
			Assert.That(_logger.Warnings[0], Does.Contain("maximum collected files limit"));
			Assert.That(files.Count, Is.EqualTo(10));
		}
	}

	[Test]
	public void CollectAssetBundlesRecursively_CustomLowerDepth_IsRespected()
	{
		// Ensures the configured depth is read from ImportSettings rather than always 32.
		string root = Path.Combine(_tempRoot, "root");
		byte[] bundleBytes = CreateUnityBundleBytes();
		CreateDeepBundleTree(root, depth: 10, bundleBytes);

		ImportSettings settings = new() { MaxRecursiveDirectoryDepth = 4 };
		TestablePlatformGameStructure structure = new(LocalFileSystem.Instance, settings);
		List<KeyValuePair<string, string>> files = [];

		structure.InvokeCollectAssetBundlesRecursively(root, files);

		// Root is depth 0 (no bundle). l0..l2 are scanned at depths 1..3, and l3
		// at depth 4 is skipped. That yields 3 bundles (b0..b2).
		using (Assert.EnterMultipleScope())
		{
			Assert.That(files.Count, Is.EqualTo(3));
			Assert.That(_logger.Warnings, Is.Not.Empty);
		}
	}
}
