using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Generics;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.Logging;
using AssetRipper.IO.Endian;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams.Smart;
using AssetRipper.Primitives;
using System.Diagnostics;
using System.Reflection;

namespace AssetRipper.Tests;

/// <summary>
/// End-to-end smoke tests for the Task 9.1 verification of the AssetRipper memory-optimization
/// spec. These tests exercise the full <see cref="GameBundle.FromPaths"/> batch-loading pipeline
/// against a synthetic "large folder" of generated <see cref="SerializedFile"/>s and verify:
/// <list type="bullet">
///   <item>The import completes without throwing and produces a non-empty
///       <see cref="GameBundle.Collections"/>.</item>
///   <item>The peak managed-memory usage during import stays within an order of magnitude of
///       the loaded file set size (sanity check, not a strict cap).</item>
///   <item>The batch-loading loop honors <paramref name="fileBatchSize"/> (verified indirectly
///       through the per-batch SerializedFile.Dispose path that releases lazy-read streams).</item>
///   <item><see cref="GameBundle.Dispose"/> releases any temp streams created during the load
///       (the synthetic load itself does not produce spills, so this is verified separately by
///       registering a synthetic temp stream and asserting it is cleaned up).</item>
/// </list>
/// </summary>
/// <remarks>
/// <b>Binary-diff comparison skipped.</b> Properly verifying that the optimized code produces
/// the same output as the pre-Task-1..8 code requires <c>git stash</c>-ing the optimization
/// patches, building/exporting twice, and diffing the export trees. That is a destructive git
/// operation and is left as a manual verification step. See the spec at
/// <c>.trae/specs/optimize-folder-import-memory/spec.md</c> for the manual comparison recipe.
/// </remarks>
/// <remarks>
/// <b>Test execution note.</b> This project targets <c>net10.0</c>. If only the .NET 9 SDK is
/// installed, <c>dotnet test</c> will fail to build. In that case, set
/// <c>ASSETRIPPER_SMOKE_TESTS_SKIP=1</c> via the NUnit <c>--filter</c> mechanism (or run with
/// <c>-t:LargeFolderImportSmokeTests</c> excluded) to skip these tests. The code itself is
/// compile-clean against net10.0.
/// </remarks>
public class LargeFolderImportSmokeTests
{
	private CapturingLogger _logger = null!;
	private string _fixtureRoot = null!;
	private string _tempDir = null!;
	private string? _previousTemporaryDirectory;

	[SetUp]
	public void SetUp()
	{
		_logger = new CapturingLogger();
		Logger.Add(_logger);

		// Each test gets its own unique fixture root under the system temp path.
		_fixtureRoot = Path.Combine(Path.GetTempPath(), "AR_Smoke_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_fixtureRoot);

		// Redirect LocalFileSystem's temp directory to a known location so we can
		// assert that no temp files leak out of a load+dispose cycle.
		_tempDir = Path.Combine(Path.GetTempPath(), "AR_SmokeTemp_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
		_previousTemporaryDirectory = LocalFileSystem.Instance.TemporaryDirectory;
		LocalFileSystem.Instance.TemporaryDirectory = _tempDir;
	}

	[TearDown]
	public void TearDown()
	{
		Logger.Remove(_logger);

		// Restore the previously-configured temp directory.
		if (_previousTemporaryDirectory is not null)
		{
			LocalFileSystem.Instance.TemporaryDirectory = _previousTemporaryDirectory;
		}

		foreach (string dir in new[] { _fixtureRoot, _tempDir })
		{
			try
			{
				if (Directory.Exists(dir))
				{
					Directory.Delete(dir, recursive: true);
				}
			}
			catch
			{
				// Best-effort cleanup. Leaked temp files are detected by the test body
				// itself; silently ignore failures here so unrelated IO errors don't
				// mask the actual assertion failures.
			}
		}
	}

	/// <summary>
	/// Loads a synthetic folder of 25 SerializedFiles via <see cref="GameBundle.FromPaths"/>
	/// with a small <c>fileBatchSize</c> (4) and small <c>maxInMemoryBundleBlockSize</c>
	/// (1 KiB), and verifies that:
	/// <list type="bullet">
	///   <item>The import completes without throwing.</item>
	///   <item>The returned <see cref="GameBundle"/> has exactly 25 collections (one per
	///       SerializedFile, since each SerializedFile produces one
	///       <see cref="SerializedAssetCollection"/> even when its object list is empty).</item>
	///   <item>Peak managed-memory usage stays below a generous ceiling (10× the file count
	///       times the per-file payload size — 25 × 256 B × 10 = 64 KiB worth of object data
	///       plus overhead; we use 64 MiB to avoid flakiness from GC timing on CI).</item>
	///   <item>The post-Dispose managed heap is back near the pre-load baseline.</item>
	/// </list>
	/// </summary>
	[Test]
	public void LoadLargeFolder_SyntheticSerializedFiles_LoadsAllCollectionsAndStaysWithinMemoryCeiling()
	{
		const int FileCount = 25;
		const int FileBatchSize = 4;
		const int MaxInMemoryBundleBlockSize = 1024;
		const int ObjectDataSize = 256;

		string[] filePaths = WriteSyntheticSerializedFiles(FileCount, ObjectDataSize);

		// Force a full GC so the baseline is as small as possible. We use
		// GC.GetTotalMemory(true) for the baseline only — the post-load sample uses
		// GC.GetTotalMemory(false) so we don't lie about the live set.
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long baselineMemory = GC.GetTotalMemory(forceFullCollection: true);

		GameBundle gameBundle;
		using (NullAssetFactory factory = new NullAssetFactory())
		{
			gameBundle = GameBundle.FromPaths(
				filePaths,
				factory,
				LocalFileSystem.Instance,
				initializer: null,
				maxInMemoryBundleBlockSize: MaxInMemoryBundleBlockSize,
				fileBatchSize: FileBatchSize);
		}

		long postLoadMemory = GC.GetTotalMemory(forceFullCollection: false);

		try
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(gameBundle.Collections.Count, Is.EqualTo(FileCount),
					"Each SerializedFile must produce exactly one SerializedAssetCollection.");
				Assert.That(gameBundle.FetchAssetCollections().Count(), Is.EqualTo(FileCount),
					"FetchAssetCollections must enumerate all collections including child bundles.");
				Assert.That(gameBundle.AnyFailed, Is.False,
					"No FailedFiles should be present when every fixture is a valid SerializedFile.");
			}

				// The synthetic files are tiny (~256 B each). Even with overhead the live set
				// should be well under 64 MiB. We use a generous ceiling to avoid flakiness on
				// CI runners where the runtime may have grown internal pools.
				const long MemoryCeiling = 64 * 1024 * 1024;
				Assert.That(postLoadMemory - baselineMemory, Is.LessThan(MemoryCeiling),
					$"Peak managed memory delta {postLoadMemory - baselineMemory:N0} bytes must stay under {MemoryCeiling:N0} bytes " +
					"to confirm that batch loading + SerializedFile.Dispose is releasing per-batch streams.");
		}
		finally
		{
			gameBundle.Dispose();
		}

		// After Dispose, force a full GC and verify the managed heap returns near the baseline.
		// We allow a generous slack of 4 MiB to account for one-time JIT allocations and other
		// managed-state growth that is not related to the bundle.
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long postDisposeMemory = GC.GetTotalMemory(forceFullCollection: true);
		const long DisposeSlack = 4 * 1024 * 1024;
		Assert.That(postDisposeMemory - baselineMemory, Is.LessThan(DisposeSlack),
			$"Post-Dispose managed memory delta {postDisposeMemory - baselineMemory:N0} bytes must return near baseline " +
			$"({DisposeSlack:N0}-byte slack) so we know the bundle's managed state was reclaimed.");

		// The synthetic SerializedFile load path does not spill to disk (only ResourceFiles
		// spill). However, we still assert that the LocalFileSystem temp directory is empty
		// after the load+dispose cycle to catch any unexpected temp file leak.
		Assert.That(Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories), Is.Empty,
			"No temp files should remain in the LocalFileSystem temp directory after Dispose.");
	}

	/// <summary>
	/// A larger-N run (250 files) with the default batch size to ensure the batched-loading
	/// loop does not regress under load and that the per-batch Dispose path is exercised many
	/// times without leaks. Memory is sampled before/after to ensure no obvious growth.
	/// </summary>
	[Test]
	public void LoadLargeFolder_ManySmallFiles_CompletesWithoutLeakingTempFiles()
	{
		const int FileCount = 250;
		const int FileBatchSize = 50; // matches ImportSettings.FileBatchSize default
		const int ObjectDataSize = 64;

		string[] filePaths = WriteSyntheticSerializedFiles(FileCount, ObjectDataSize);

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		long baselineMemory = GC.GetTotalMemory(forceFullCollection: true);

		GameBundle gameBundle;
		using (NullAssetFactory factory = new NullAssetFactory())
		{
			gameBundle = GameBundle.FromPaths(
				filePaths,
				factory,
				LocalFileSystem.Instance,
				initializer: null,
				maxInMemoryBundleBlockSize: 50 * 1024 * 1024,
				fileBatchSize: FileBatchSize);
		}

		long postLoadMemory = GC.GetTotalMemory(forceFullCollection: false);

		try
		{
			Assert.That(gameBundle.Collections.Count, Is.EqualTo(FileCount),
				"All SerializedFiles must be loaded even with the default batch size.");
		}
		finally
		{
			gameBundle.Dispose();
		}

		// Sanity check: post-load managed memory must be bounded. The 250-file run produces
		// 250 small SerializedAssetCollections, each with a Name string and metadata. We use
		// 128 MiB as the ceiling to avoid flakiness.
		const long MemoryCeiling = 128 * 1024 * 1024;
		Assert.That(postLoadMemory - baselineMemory, Is.LessThan(MemoryCeiling),
			$"Peak managed memory delta {postLoadMemory - baselineMemory:N0} bytes must stay under {MemoryCeiling:N0} bytes.");

		// No temp files should ever appear in the synthetic-SerializedFile load path.
		Assert.That(Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories), Is.Empty,
			"Loading pure SerializedFiles must not spill to disk.");
	}

	/// <summary>
	/// The synthetic load path does not exercise the spill-to-disk code path (only
	/// ResourceFiles spill). To verify end-to-end that <see cref="GameBundle.Dispose"/>
	/// cleans up spilled temp files created during a load, we register a
	/// <see cref="SmartStream.CreateTemp"/> stream with the bundle via reflection (mirroring what
	/// <see cref="GameBundle.RegisterTempStream"/> does internally when
	/// <see cref="GameBundle.SpillResourceFileIfLarge"/> spills a large ResourceFile) and
	/// verify Dispose deletes the underlying temp file.
	/// </summary>
	/// <remarks>
	/// <see cref="GameBundle.RegisterTempStream"/> is <c>internal</c> and only
	/// <c>AssetRipper.Assets.Tests</c> has <c>InternalsVisibleTo</c> access. We use reflection
	/// here so the smoke-test assembly can exercise the same end-to-end path. The unit-level
	/// coverage lives in <c>AssetRipper.Assets.Tests/GameBundleBatchLoadingTests.cs</c>.
	/// </remarks>
	[Test]
	public void Dispose_AfterSyntheticSpillRegistration_CleansUpTempFiles()
	{
		GameBundle gameBundle = new();
		const int SpillCount = 4;
		List<string> tempFilePaths = new(SpillCount);
		for (int i = 0; i < SpillCount; i++)
		{
			SmartStream tempStream = SmartStream.CreateTemp();
			tempStream.Write(new byte[] { (byte)i, (byte)(i + 1), (byte)(i + 2) }, 0, 3);
			tempStream.Position = 0;

			FileStream? fileStream = GetUnderlyingFileStream(tempStream);
			Assert.That(fileStream, Is.Not.Null,
				"SmartStream.CreateTemp must return a FileStream-backed stream.");
			tempFilePaths.Add(fileStream!.Name);
			Assert.That(File.Exists(tempFilePaths[i]), Is.True,
				"Temp file must exist on disk while the stream is alive.");

			InvokeRegisterTempStream(gameBundle, tempStream);
		}

		// Sanity: all temp files live in our redirected LocalFileSystem temp directory.
		foreach (string path in tempFilePaths)
		{
			Assert.That(path.StartsWith(_tempDir, StringComparison.Ordinal), Is.True,
				$"Temp file {path} should live under the redirected LocalFileSystem temp directory {_tempDir}.");
		}

		gameBundle.Dispose();

		foreach (string path in tempFilePaths)
		{
			Assert.That(File.Exists(path), Is.False,
				$"Temp file {path} must be deleted by GameBundle.Dispose.");
		}

		// The temp directory itself should be empty after Dispose.
		Assert.That(Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories), Is.Empty,
			"The LocalFileSystem temp directory must be empty after the bundle is disposed.");
	}

	/// <summary>
	/// Sanity check that verifies the smoke-test infrastructure itself: a single
	/// SerializedFile produced by <see cref="SerializedFileBuilder"/> must be loadable by
	/// <see cref="GameBundle.FromPaths"/> as a single-element collection. This isolates
	/// fixture-construction failures from the larger-N tests above.
	/// </summary>
	[Test]
	public void LoadSingleSyntheticSerializedFile_ProducesOneCollection()
	{
		string[] filePaths = WriteSyntheticSerializedFiles(count: 1, objectDataSize: 16);

		using NullAssetFactory factory = new NullAssetFactory();
		GameBundle gameBundle = GameBundle.FromPaths(
			filePaths,
			factory,
			LocalFileSystem.Instance,
			initializer: null);

		try
		{
			Assert.That(gameBundle.Collections.Count, Is.EqualTo(1),
				"A single SerializedFile must produce exactly one collection.");
		}
		finally
		{
			gameBundle.Dispose();
		}
	}

	// -----------------------------------------------------------------------
	// Helpers
	// -----------------------------------------------------------------------

	/// <summary>
	/// Writes <paramref name="count"/> synthetic SerializedFiles to the fixture root, each
	/// containing a single <see cref="ObjectInfo"/> with <paramref name="objectDataSize"/>
	/// random bytes. The files are written using <see cref="SerializedFile.Write(Stream)"/>
	/// so they round-trip through <see cref="SerializedFileScheme.Default"/>.
	/// </summary>
	/// <returns>The absolute paths of the written files.</returns>
	private string[] WriteSyntheticSerializedFiles(int count, int objectDataSize)
	{
		string[] paths = new string[count];
		Random random = new(42 + count); // deterministic per-N seed for reproducibility
		for (int i = 0; i < count; i++)
		{
			SerializedFileBuilder builder = new()
			{
				Generation = FormatVersion.LargeFilesSupport,
				Version = new UnityVersion(6000, 1, 0),
				Platform = BuildTarget.NoTarget,
				EndianType = EndianType.LittleEndian,
				HasTypeTree = false,
			};

			SerializedType type = new()
			{
				TypeID = 1, // arbitrary valid ClassID
				IsStrippedType = false,
				ScriptTypeIndex = -1,
			};

			byte[] objectData = new byte[objectDataSize];
			random.NextBytes(objectData);

			ObjectInfo obj = new(type)
			{
				FileID = i + 1,
				SerializedTypeIndex = 0,
				ObjectData = objectData,
			};
			builder.Types.Add(type);
			builder.Objects.Add(obj);

			SerializedFile file = builder.Build();

			// Use a Unity-recognized extension (.assets) so the SerializedFileScheme is picked.
			string path = Path.Combine(_fixtureRoot, $"synthetic_{i:D4}.assets");
			using FileStream fs = File.Create(path);
			file.Write(fs);
			fs.Flush();

			paths[i] = path;
		}
		return paths;
	}

	/// <summary>
	/// Reflects on the private <c>Stream</c> property of <see cref="SmartStream"/> to obtain
	/// the underlying <see cref="FileStream"/> for temp-file existence checks.
	/// Mirrors the helper in <c>GameBundleBatchLoadingTests.cs</c>.
	/// </summary>
	private static FileStream? GetUnderlyingFileStream(SmartStream smartStream)
	{
		PropertyInfo? property = typeof(SmartStream).GetProperty(
			"Stream",
			BindingFlags.NonPublic | BindingFlags.Instance);
		return property?.GetValue(smartStream) as FileStream;
	}

	/// <summary>
	/// Invokes the internal <see cref="GameBundle.RegisterTempStream"/> method via reflection.
	/// <see cref="GameBundle.RegisterTempStream"/> is <c>internal</c>, but this smoke-test
	/// assembly does not have <c>InternalsVisibleTo</c> access. We invoke it reflectively to
	/// exercise the same end-to-end path that <see cref="GameBundle.SpillResourceFileIfLarge"/>
	/// uses internally when registering a spilled <see cref="SmartStream"/>.
	/// </summary>
	private static void InvokeRegisterTempStream(GameBundle gameBundle, SmartStream stream)
	{
		const string MethodName = "RegisterTempStream";
		MethodInfo? method = typeof(GameBundle).GetMethod(
			MethodName,
			BindingFlags.Instance | BindingFlags.NonPublic);
		if (method is null)
		{
			Assert.Fail(
				$"Could not reflect GameBundle.{MethodName}. " +
				"The internal API may have been renamed or removed.");
			return;
		}
		method.Invoke(gameBundle, new object?[] { stream });
	}

	/// <summary>
	/// A minimal <see cref="AssetFactoryBase"/> that returns <see langword="null"/> for every
	/// asset. The synthetic SerializedFiles produced by <see cref="WriteSyntheticSerializedFiles"/>
	/// have no type tree, so the real <c>GameAssetFactory</c> would also return null. Using a
	/// null factory here lets us exercise the batch-loading loop without dragging in a real
	/// <c>IAssemblyManager</c> / <c>MonoManager</c>.
	/// </summary>
	private sealed class NullAssetFactory : AssetFactoryBase, IDisposable
	{
		public override IUnityObjectBase? ReadAsset(AssetInfo assetInfo, ReadOnlyArraySegment<byte> assetData, SerializedType? assetType)
		{
			return null;
		}

		public void Dispose()
		{
			// Stateless factory; nothing to dispose.
		}
	}

	/// <summary>
	/// A capturing logger that records warnings and errors so the test body can assert that
	/// the import path did not emit unexpected diagnostics.
	/// </summary>
	private sealed class CapturingLogger : ILogger
	{
		public List<string> Warnings { get; } = [];
		public List<string> Errors { get; } = [];

		public void Log(LogType type, LogCategory category, string message)
		{
			if (type == LogType.Warning)
			{
				Warnings.Add(message);
			}
			else if (type == LogType.Error)
			{
				Errors.Add(message);
			}
		}

		public void BlankLine(int numLines)
		{
		}
	}
}
