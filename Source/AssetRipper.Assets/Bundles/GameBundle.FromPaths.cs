using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.CompressedFiles;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.Logging;

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
		Logger.Info(LogCategory.Import, "路径加载完成，开始初始化依赖");
		gameBundle.InitializeAllDependencyLists(initializer?.DependencyProvider);
		Logger.Info(LogCategory.Import, "依赖初始化完成，开始初始化资源");
		initializer?.OnDependenciesInitialized(gameBundle, assetFactory);
		Logger.Info(LogCategory.Import, "资源初始化完成");
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
		Logger.LogMemoryDiagnostics("加载文件和依赖项前");
		List<FileBase> fileStack = LoadFilesAndDependencies(paths, fileSystem, initializer?.DependencyProvider);
		UnityVersion defaultVersion = initializer?.DefaultVersion ?? default;
		Logger.LogMemoryDiagnostics("加载文件和依赖项后");
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

		Logger.LogMemoryDiagnostics("资源序列化后");
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
		foreach (string path in paths)
		{
			if (files.Count % 100000 == 0)
			{
				Logger.Info(LogCategory.Import, $"{files.Count} 正在加载文件：'{path}'");
			}
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
					LoadDependencies(serializedFile, files, serializedFileNames, dependencyProvider);
					break;
				case FileContainer container:
					foreach (SerializedFile serializedFileInContainer in container.FetchSerializedFiles())
					{
						LoadDependencies(serializedFileInContainer, files, serializedFileNames, dependencyProvider);
					}

					break;
			}
		}

		return files;
	}

	/// <summary> 加载文件的依赖项。 </summary>
	private static void LoadDependencies(SerializedFile serializedFile, List<FileBase> files, HashSet<string> serializedFileNames, IDependencyProvider? dependencyProvider)
	{
		foreach (FileIdentifier fileIdentifier in serializedFile.Dependencies)
		{
			string name = fileIdentifier.GetFilePath();
			if (!serializedFileNames.Add(name))
			{
				continue;
			}
			if (dependencyProvider?.FindDependency(fileIdentifier) is not { } dependency)
			{
				continue;
			}
			// 与主路径一致：依赖可能被压缩，先逐层解包，否则主循环无法识别其类型
			while (dependency is CompressedFile compressedFile)
			{
				dependency = compressedFile.UncompressedFile ?? dependency;
			}
			// 集合文件（bundle）需展开内部内容，内部的 SerializedFile 才会被后续解析；普通文件此调用为空操作
			dependency.ReadContentsRecursively();
			files.Add(dependency);
		}
	}
}
