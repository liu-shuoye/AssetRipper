using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.CompressedFiles;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.Assets.Bundles;

partial class GameBundle
{
	/// <summary>
	/// Create and initialize a <see cref="GameBundle"/> from a set of paths.
	/// </summary>
	/// <param name="paths">The set of paths to load.</param>
	/// <param name="assetFactory">The factory for reading assets.</param>
	/// <param name="fileSystem">The file system used to resolve paths.</param>
	/// <param name="initializer">Optional initializer for dependency/resource providers and lifecycle hooks.</param>
	/// <param name="maxInMemoryBundleBlockSize">
	/// The maximum size in bytes of a single decompressed bundle block (or decompressed file)
	/// that is kept entirely in memory. When a decompressed payload exceeds this threshold it is
	/// spilled to a temporary file. Defaults to <c>50 * 1024 * 1024</c> (50 MB).
	/// </param>
	/// <param name="fileBatchSize">
	/// The number of files to deserialize per batch during
	/// <see cref="InitializeFromPaths"/>. Smaller batches reduce peak memory usage at the
	/// cost of more iterations. Values <c>&lt;= 0</c> are treated as <c>1</c>.
	/// Defaults to <see cref="GameBundleDefaults.DefaultFileBatchSize"/>.
	/// </param>
	/// <remarks>
	/// The threshold is propagated to <see cref="BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize"/>
	/// for the duration of this call and restored to its previous value afterwards. AssetRipper's
	/// import pipeline is single-threaded, so this global mutation is safe in practice.
	/// </remarks>
	public static GameBundle FromPaths(
		IEnumerable<string> paths,
		AssetFactoryBase assetFactory,
		FileSystem fileSystem,
		IGameInitializer? initializer = null,
		int maxInMemoryBundleBlockSize = GameBundleDefaults.DefaultMaxInMemoryBundleBlockSize,
		int fileBatchSize = GameBundleDefaults.DefaultFileBatchSize)
	{
		GameBundle gameBundle = new();
		initializer?.OnCreated(gameBundle, assetFactory);

		// Propagate the configured threshold to the block reader for the duration of this call.
		// The previous value is restored in `finally` so nested or subsequent calls are unaffected.
		int previousThreshold = BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize;
		BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize = maxInMemoryBundleBlockSize;
		try
		{
			gameBundle.InitializeFromPaths(paths, assetFactory, fileSystem, initializer, fileBatchSize, maxInMemoryBundleBlockSize);
			initializer?.OnPathsLoaded(gameBundle, assetFactory);
			gameBundle.InitializeAllDependencyLists(initializer?.DependencyProvider);
			initializer?.OnDependenciesInitialized(gameBundle, assetFactory);
		}
		finally
		{
			BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize = previousThreshold;
		}
		return gameBundle;
	}

	private void InitializeFromPaths(
		IEnumerable<string> paths,
		AssetFactoryBase assetFactory,
		FileSystem fileSystem,
		IGameInitializer? initializer,
		int fileBatchSize,
		int maxInMemoryBundleBlockSize)
	{
		ResourceProvider = initializer?.ResourceProvider;
		List<FileBase> fileStack = LoadFilesAndDependencies(paths, fileSystem, initializer?.DependencyProvider);
		UnityVersion defaultVersion = initializer is null ? default : initializer.DefaultVersion;

		// Validate and clamp the batch size. A non-positive value falls back to 1 so each
		// file is processed and disposed individually (the most memory-conservative behavior).
		int batchSize = fileBatchSize <= 0 ? 1 : fileBatchSize;
		int spillThreshold = maxInMemoryBundleBlockSize <= 0 ? GameBundleDefaults.DefaultMaxInMemoryBundleBlockSize : maxInMemoryBundleBlockSize;

		// Batched processing: pop up to `batchSize` files per batch, deserialize them into
		// collections/bundles, spill any oversized ResourceFiles to temp files, and finally
		// dispose the SerializedFiles in the batch so their owning streams (held open by
		// ObjectInfo for lazy reads) are released before the next batch is loaded.
		List<SerializedFile> batchSerializedFiles = new(batchSize);
		while (fileStack.Count > 0)
		{
			batchSerializedFiles.Clear();
			int batchCount = Math.Min(batchSize, fileStack.Count);
			for (int i = 0; i < batchCount; i++)
			{
				FileBase file = RemoveLastItem(fileStack);
				switch (file)
				{
					case SerializedFile serializedFile:
						SerializedAssetCollection.FromSerializedFile(this, serializedFile, assetFactory, defaultVersion);
						batchSerializedFiles.Add(serializedFile);
						break;
					case FileContainer container:
						SerializedBundle serializedBundle = SerializedBundle.FromFileContainer(container, assetFactory, defaultVersion);
						AddBundle(serializedBundle);
						// The container's SerializedFiles have been consumed by SerializedBundle.FromFileContainer.
						// Track them so their owning streams are released at the end of this batch.
						foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
						{
							batchSerializedFiles.Add(serializedFileInContainer);
						}
						break;
					case ResourceFile resourceFile:
						SpillResourceFileIfLarge(resourceFile, spillThreshold);
						AddResource(resourceFile);
						break;
					case FailedFile failedFile:
						AddFailed(failedFile);
						break;
				}
			}

			// Release the per-batch SerializedFile owning streams so the next batch does not
			// accumulate file handles. SerializedFile.Dispose releases the lazy-read SmartStream
			// references held by each ObjectInfo (Task 6) without invalidating the metadata
			// (Dependencies, Objects, Types) that may still be referenced by SerializedAssetCollection.
			foreach (SerializedFile serializedFile in batchSerializedFiles)
			{
				serializedFile.Dispose();
			}
		}
	}

	/// <summary>
	/// Spills a <see cref="ResourceFile"/>'s in-memory payload to a temporary file when its size
	/// exceeds <paramref name="spillThreshold"/>. The original in-memory <see cref="SmartStream"/>
	/// is replaced with a temp-file-backed stream tracked via <see cref="RegisterTempStream"/>.
	/// </summary>
	private void SpillResourceFileIfLarge(ResourceFile resourceFile, int spillThreshold)
	{
		SmartStream? tempStream = resourceFile.TrySpillToTempFile(spillThreshold);
		if (tempStream is not null)
		{
			RegisterTempStream(tempStream);
		}
	}

	private static FileBase RemoveLastItem(List<FileBase> list)
	{
		int index = list.Count - 1;
		FileBase file = list[index];
		list.RemoveAt(index);
		return file;
	}

	private static List<FileBase> LoadFilesAndDependencies(IEnumerable<string> paths, FileSystem fileSystem, IDependencyProvider? dependencyProvider)
	{
		List<FileBase> files = new();
		HashSet<string> serializedFileNames = new();//Includes missing dependencies
		foreach (string path in paths)
		{
			FileBase? file;
			try
			{
				file = SchemeReader.LoadFile(path, fileSystem);
				file.ReadContentsRecursively();
			}
			catch (Exception ex)
			{
				file = new FailedFile()
				{
					Name = fileSystem.Path.GetFileName(path),
					FilePath = path,
					StackTrace = ex.ToString(),
				};
			}
			while (file is CompressedFile compressedFile)
			{
				file = compressedFile.UncompressedFile;
			}
			if (file is ResourceFile or FailedFile)
			{
				files.Add(file);
			}
			else if (file is SerializedFile serializedFile)
			{
				files.Add(file);
				serializedFileNames.Add(serializedFile.NameFixed);
			}
			else if (file is FileContainer container)
			{
				files.Add(file);
				foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
				{
					serializedFileNames.Add(serializedFileInContainer.NameFixed);
				}
			}
		}

		// Note: dependency discovery is interleaved with file loading (a SerializedFile's
		// Dependencies property is read here, and any unresolved dependency is loaded via
		// dependencyProvider.FindDependency and appended to `files`). The growing-list
		// iteration below ensures transitive dependencies are discovered. Because metadata
		// loading is cheap relative to data deserialization, this pass is performed in full
		// before the caller deserializes assets in batches (Strategy B: load all metadata
		// first, then batch-deserialize and release).
		for (int i = 0; i < files.Count; i++)
		{
			FileBase file = files[i];
			if (file is SerializedFile serializedFile)
			{
				LoadDependencies(serializedFile, files, serializedFileNames, dependencyProvider);
			}
			else if (file is FileContainer container)
			{
				foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
				{
					LoadDependencies(serializedFileInContainer, files, serializedFileNames, dependencyProvider);
				}
			}
		}

		return files;
	}

	private static void LoadDependencies(SerializedFile serializedFile, List<FileBase> files, HashSet<string> serializedFileNames, IDependencyProvider? dependencyProvider)
	{
		foreach (FileIdentifier fileIdentifier in serializedFile.Dependencies)
		{
			string name = fileIdentifier.GetFilePath();
			if (serializedFileNames.Add(name) && dependencyProvider?.FindDependency(fileIdentifier) is { } dependency)
			{
				files.Add(dependency);
			}
		}
	}
}
