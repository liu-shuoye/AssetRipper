using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.CompressedFiles;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using Cpp2IL.Core.Logging;

namespace AssetRipper.Assets.Bundles;

partial class GameBundle
{
	/// <summary>
	/// 从一组路径创建并初始化一个 <see cref="GameBundle"/>。
	/// </summary>
	/// <param name="paths">The set of paths to load.</param>
	/// <param name="assetFactory">The factory for reading assets.</param>
	public static GameBundle FromPaths(IEnumerable<string> paths, AssetFactoryBase assetFactory, FileSystem fileSystem, IGameInitializer? initializer = null)
	{
		GameBundle gameBundle = new();
		initializer?.OnCreated(gameBundle, assetFactory);
		gameBundle.InitializeFromPaths(paths, assetFactory, fileSystem, initializer);
		initializer?.OnPathsLoaded(gameBundle, assetFactory);
		gameBundle.InitializeAllDependencyLists(initializer?.DependencyProvider);
		initializer?.OnDependenciesInitialized(gameBundle, assetFactory);
		return gameBundle;
	}

	/// <summary>
	/// 将一组路径加载到 <see cref="GameBundle"/> 中。
	/// </summary>
	/// <param name="paths"></param>
	/// <param name="assetFactory"></param>
	/// <param name="fileSystem"></param>
	/// <param name="initializer"></param>
	private void InitializeFromPaths(IEnumerable<string> paths, AssetFactoryBase assetFactory, FileSystem fileSystem, IGameInitializer? initializer)
	{
		ResourceProvider = initializer?.ResourceProvider;
		List<FileBase> fileStack = LoadFilesAndDependencies(paths, fileSystem, initializer?.DependencyProvider);
		UnityVersion defaultVersion = initializer?.DefaultVersion ?? default;
		LogMemoryDiagnostics("加载文件和依赖项后");
		while (fileStack.Count > 0)
		{
			switch (RemoveLastItem(fileStack))
			{
				case SerializedFile serializedFile:
					SerializedAssetCollection.FromSerializedFile(this, serializedFile, assetFactory, defaultVersion);
					break;
				case FileContainer container:
					SerializedBundle serializedBundle = SerializedBundle.FromFileContainer(container, assetFactory, defaultVersion);
					AddBundle(serializedBundle);
					break;
				case ResourceFile resourceFile:
					AddResource(resourceFile);
					break;
				case FailedFile failedFile:
					AddFailed(failedFile);
					break;
			}
		}

		LogMemoryDiagnostics("资源序列化后");
	}

	/// <summary>
	/// 输出当前内存状态，用于定位哪个阶段内存上涨最多。
	/// </summary>
	public static void LogMemoryDiagnostics(string stage)
	{
		// 强制 GC 后再统计，排除已可回收但未回收的对象干扰
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long managedMemory = GC.GetTotalMemory(false);
		long workingSet = Environment.WorkingSet;
		Logger.Info($"[内存诊断] {stage}: 托管: {managedMemory / 1024.0 / 1024.0:F1} MB | 工作集: {workingSet / 1024.0 / 1024.0:F1} MB");
	}

	private static FileBase RemoveLastItem(List<FileBase> list)
	{
		int index = list.Count - 1;
		FileBase file = list[index];
		list.RemoveAt(index);
		return file;
	}

	/// <summary> 加载文件及其依赖项。 </summary>
	private static List<FileBase> LoadFilesAndDependencies(IEnumerable<string> paths, FileSystem fileSystem, IDependencyProvider? dependencyProvider)
	{
		List<FileBase> files = new();
		HashSet<string> serializedFileNames = new(); //包含缺失的依赖项
		Dictionary<string, string> skipContent = new();
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
				file = new FailedFile() { Name = fileSystem.Path.GetFileName(path), FilePath = path, StackTrace = ex.ToString(), };
			}

			while (file is CompressedFile compressedFile)
			{
				file = compressedFile.UncompressedFile;
			}


			string assetsPath = path.Split(@"\assets\")[1];
			if (assetsPath.StartsWith(@"art\audio")
			    || assetsPath.StartsWith(@"art\ui")
			    || assetsPath.StartsWith(@"art\character"))
			{
				if (file is SerializedFile serializedFile)
				{
					skipContent[serializedFile.NameFixed] = serializedFile.FilePath;
					continue;
				}

				if (file is FileContainer container)
				{
					foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
					{
						skipContent[serializedFileInContainer.NameFixed] = serializedFileInContainer.FilePath;
					}
				}

				continue;
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

		// ReSharper disable once ForCanBeConvertedToForeach 循环中会添加，所以不能使用 foreach
		for (int i = 0; i < files.Count; i++)
		{
			FileBase file = files[i];
			switch (file)
			{
				case SerializedFile serializedFile:
					LoadDependencies(serializedFile, files, serializedFileNames, dependencyProvider,skipContent);
					break;
				case FileContainer container:
					foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
					{
						LoadDependencies(serializedFileInContainer, files, serializedFileNames, dependencyProvider, skipContent);
					}

					break;
			}
		}

		return files;
	}

	/// <summary> 加载文件的依赖项。 </summary>
	private static void LoadDependencies(SerializedFile serializedFile, List<FileBase> files, HashSet<string> serializedFileNames, IDependencyProvider? dependencyProvider, Dictionary<string, string> skipContent)
	{
		foreach (FileIdentifier fileIdentifier in serializedFile.Dependencies)
		{
			string name = fileIdentifier.GetFilePath();
			if (serializedFileNames.Add(name) && dependencyProvider?.FindDependency(fileIdentifier,skipContent) is { } dependency)
			{
				dependency.ReadContentsRecursively();
				if (dependency is ResourceFile or FailedFile)
				{
					files.Add(dependency);
				}
				else if (dependency is SerializedFile serializedFileDependency)
				{
					files.Add(dependency);
					serializedFileNames.Add(serializedFileDependency.NameFixed);
				}
				else if (dependency is FileContainer container)
				{
					files.Add(dependency);
					foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
					{
						serializedFileNames.Add(serializedFileInContainer.NameFixed);
					}
				}
			}
		}
	}
}
