using AssetRipper.Logging;
using AssetRipper.Import.Platforms;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.BundleFiles;
using AssetRipper.IO.Files.BundleFiles.FileStream;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.Streams;
using System.Text.RegularExpressions;

namespace AssetRipper.Import.Structure.Platforms;

public abstract partial class PlatformGameStructure
{
	public FileSystem FileSystem { get; }
	public string? Name { get; protected set; }
	public string? RootPath { get; }
	public string? GameDataPath { get; protected set; }
	public string? StreamingAssetsPath { get; protected set; }
	public string? ResourcesPath { get; protected set; }
	public ScriptingBackend Backend { get; protected set; } = ScriptingBackend.Unknown;
	public string? ManagedPath { get; protected set; }
	public string? Il2CppGameAssemblyPath { get; protected set; }
	public string? Il2CppMetaDataPath { get; protected set; }
	public string? UnityPlayerPath { get; protected set; }
	public UnityVersion? Version { get; protected set; }

	public IReadOnlyList<string> DataPaths { get; protected set; } = [];

	/// <summary>Name : FullName</summary>
	public List<KeyValuePair<string, string>> Files { get; } = [];

	/// <summary>AssemblyName : AssemblyPath</summary>
	public Dictionary<string, string> Assemblies { get; } = [];

	protected const string DataFolderName = "Data";
	protected const string ManagedName = "Managed";
	protected const string LibName = "lib";
	protected const string ResourcesName = "Resources";
	protected const string UnityName = "unity";
	protected const string StreamingName = "StreamingAssets";
	protected const string MetadataName = "Metadata";
	protected const string DefaultUnityPlayerName = "UnityPlayer.dll";
	protected const string DefaultGameAssemblyName = "GameAssembly.dll";
	protected const string DefaultGlobalMetadataName = "global-metadata.dat";

	protected const string DataName = "data";
	protected const string DataBundleName = DataName + AssetBundleExtension;
	protected const string DataPackBundleName = DataName + "pack" + AssetBundleExtension;
	protected const string MainDataName = "mainData";
	protected const string GlobalGameManagersName = "globalgamemanagers";
	protected const string GlobalGameManagerAssetsName = "globalgamemanagers.assets";
	protected const string ResourcesAssetsName = "resources.assets";
	protected const string LevelPrefix = "level";

	protected const string AssetBundleExtension = ".unity3d";
	protected const string AlternateBundleExtension = ".bundle";
	protected const string Lz4BundleName = DataName + AssetBundleExtension;

	public PlatformGameStructure(FileSystem fileSystem)
	{
		ArgumentNullException.ThrowIfNull(fileSystem);
		FileSystem = fileSystem;
	}

	public PlatformGameStructure(string rootPath, FileSystem fileSystem) : this(fileSystem)
	{
		ArgumentException.ThrowIfNullOrEmpty(rootPath);
		if (!FileSystem.Directory.Exists(rootPath))
		{
			throw new DirectoryNotFoundException($"Root directory '{rootPath}' doesn't exist");
		}

		RootPath = rootPath;
	}

	public static bool IsPrimaryEngineFile(string fileName)
	{
		if (fileName == MainDataName ||
		    fileName == GlobalGameManagersName ||
		    fileName == GlobalGameManagerAssetsName ||
		    fileName == ResourcesAssetsName ||
		    LevelTemplateRegex.IsMatch(fileName) ||
		    SharedAssetTemplateRegex.IsMatch(fileName))
		{
			return true;
		}

		return false;
	}

	/// <summary>尝试查找具有该名称的依赖项路径。</summary>
	public string? RequestDependency(string dependency)
	{
		string? dependencyPath = Files.FirstOrDefault(t => t.Key == dependency).Value;
		if (!string.IsNullOrEmpty(dependencyPath))
		{
			return dependencyPath;
		}

		foreach (string dataPath in DataPaths)
		{
			string filePath = FileSystem.Path.Join(dataPath, dependency);
			if (MultiFileStream.Exists(filePath, FileSystem))
			{
				return filePath;
			}

			if (SpecialFileNames.IsDefaultResource(dependency))
			{
				return FindEngineDependency(dataPath, SpecialFileNames.DefaultResourceName1) ??
				       FindEngineDependency(dataPath, SpecialFileNames.DefaultResourceName2);
			}
			else if (SpecialFileNames.IsBuiltinExtra(dependency))
			{
				return FindEngineDependency(dataPath, SpecialFileNames.BuiltinExtraName1) ??
				       FindEngineDependency(dataPath, SpecialFileNames.BuiltinExtraName2);
			}
		}

		return null;
	}

	public string? RequestAssembly(string assembly)
	{
		string assemblyName = $"{assembly}{MonoManager.AssemblyExtension}";
		if (Assemblies.TryGetValue(assemblyName, out string? assemblyPath))
		{
			return assemblyPath;
		}

		return null;
	}

	public string? RequestResource(string resource)
	{
		foreach (string dataPath in DataPaths)
		{
			string path = FileSystem.Path.Join(dataPath, resource);
			if (MultiFileStream.Exists(path, FileSystem))
			{
				return path;
			}
		}

		return null;
	}

	public virtual void CollectFiles(bool skipStreamingAssets)
	{
		if (this is MixedGameStructure)
		{
			return;
		}

		LoadScanCache();

		foreach (string dataPath in DataPaths)
		{
			CollectGameFiles(dataPath, Files);
		}

		CollectMainAssemblies();
		if (!skipStreamingAssets)
		{
			CollectStreamingAssets();
		}

		SaveScanCache();
	}

	/// <summary>
	/// 缓存中的扫描类型名 → 枚举值。缓存文件跨版本可读，因此这里显式列出全部合法取值。
	/// </summary>
	private static readonly Dictionary<string, FileScanKind> ScanKindsByName = new(StringComparer.Ordinal)
	{
		[nameof(FileScanKind.SerializedFiles)] = FileScanKind.SerializedFiles,
		[nameof(FileScanKind.Bundles)] = FileScanKind.Bundles,
	};

	/// <summary>
	/// 尝试加载上一次的扫描结果缓存。
	/// </summary>
	/// <remarks>
	/// 缓存仅在显式启用时读取；任何异常或校验不通过都静默退化为完整扫描，缓存永远不能成为失败点。
	/// </remarks>
	private void LoadScanCache()
	{
		if (!ScanCacheEnabled || string.IsNullOrWhiteSpace(ScanCachePath))
		{
			return;
		}

		Dictionary<FileScanKind, List<KeyValuePair<string, string>>>? loaded = FileScanCache.TryLoad(
			ScanCachePath,
			RootPath,
			FileSystem,
			ScanKindsByName,
			message => Logger.Warning(LogCategory.Import, message));
		if (loaded is null)
		{
			return;
		}

		_scanCache = [];
		foreach (KeyValuePair<FileScanKind, List<KeyValuePair<string, string>>> pair in loaded)
		{
			// 缓存键不含目录，这里按扫描结果中第一条记录的所在目录还原，与落盘时的键构造方式对称
			if (pair.Value.Count > 0 && FileSystem.Path.GetDirectoryName(pair.Value[0].Value) is { } directory)
			{
				_scanCache[(directory, pair.Key)] = pair.Value;
			}
		}

		Logger.Info(LogCategory.Import, $"已加载文件扫描缓存：{ScanCachePath}");
	}

	/// <summary>
	/// 把本次新扫描出的结果写入缓存文件，供下次导入直接复用。
	/// </summary>
	private void SaveScanCache()
	{
		if (!ScanCacheEnabled || string.IsNullOrWhiteSpace(ScanCachePath) || _scanCacheAdditions is null)
		{
			return;
		}

		FileScanCache.Save(
			ScanCachePath,
			RootPath,
			FileSystem,
			_scanCacheAdditions,
			kind => kind.ToString(),
			message => Logger.Warning(LogCategory.Import, message));
		Logger.Info(LogCategory.Import, $"文件扫描缓存已保存：{ScanCachePath}");
	}

	/// <summary>
	/// 是否启用扫描结果缓存。默认关闭，避免在磁盘上产生用户未预期的文件。
	/// </summary>
	public bool ScanCacheEnabled { get; set; }

	/// <summary>
	/// 扫描结果缓存文件的路径。
	/// </summary>
	public string? ScanCachePath { get; set; }

	protected void CollectGameFiles(string root, List<KeyValuePair<string, string>> files)
	{
		Logger.Info(LogCategory.Import, "正在收集游戏文件...");
		CollectCompressedGameFiles(root, files);
		CollectDefaultSerializedFiles(root, files);
	}

	/// <summary>
	/// Finds data.unity3d and datapack.unity3d when Lz4 compressed
	/// 
	/// Accoding to comments in Unity source file in the function at
	/// PlatformDependent/AndroidPlayer/Source/ApkFile.cpp:268,
	/// the datapack asset is only present if Gradle built an AAB with Unity
	/// data asset pack inside and then bundletool converted AAB into universal APK.
	/// </summary>
	protected void CollectCompressedGameFiles(string root, List<KeyValuePair<string, string>> files)
	{
		string dataBundlePath = FileSystem.Path.Join(root, DataBundleName);
		if (MultiFileStream.Exists(dataBundlePath, FileSystem))
		{
			AddAssetBundle(files, DataBundleName, dataBundlePath);
		}

		string dataPackBundlePath = FileSystem.Path.Join(root, DataPackBundleName);
		if (MultiFileStream.Exists(dataPackBundlePath, FileSystem))
		{
			AddAssetBundle(files, DataPackBundleName, dataPackBundlePath);
		}
	}

	/// <summary>
	/// Collects global game managers and all the level files
	/// </summary>
	/// <remarks>
	/// Files are selected based on the file name, using a regex for level files.
	/// </remarks>
	protected void CollectDefaultSerializedFiles(string root, List<KeyValuePair<string, string>> files)
	{
		string filePath = FileSystem.Path.Join(root, GlobalGameManagersName);
		if (MultiFileStream.Exists(filePath, FileSystem))
		{
			AddFile(files, GlobalGameManagersName, filePath);
		}
		else
		{
			filePath = FileSystem.Path.Join(root, MainDataName);
			if (MultiFileStream.Exists(filePath, FileSystem))
			{
				AddFile(files, MainDataName, filePath);
			}
		}

		foreach (string levelFile in FileSystem.Directory.EnumerateFiles(root))
		{
			string name = FileSystem.Path.GetFileName(levelFile);
			if (LevelTemplateRegex.IsMatch(name))
			{
				string levelName = MultiFileStream.GetFileName(name);
				AddFile(files, levelName, levelFile);
			}
		}
	}

	/// <summary>
	/// Collects all serialized files in the directory
	/// </summary>
	/// <remarks>
	/// The search is top-level only.
	/// Files are selected based on their file header.
	/// 判定交由 <see cref="FileTypeScanner"/> 完成：它先用名称与体积筛掉不可能命中的文件，
	/// 再对候选并发读取文件头，避免对每个文件都单独打开一次流。
	/// </remarks>
	protected void CollectAllSerializedFiles(string root, List<KeyValuePair<string, string>> files)
	{
		if (TryGetCachedScan(root, FileScanKind.SerializedFiles, out List<KeyValuePair<string, string>>? cached))
		{
			files.AddRange(cached);
			return;
		}

		List<KeyValuePair<string, string>> scanned = FileTypeScanner.ScanSerializedFiles(FileSystem, root);
		StoreCachedScan(root, FileScanKind.SerializedFiles, scanned);
		files.AddRange(scanned);
	}

	/// <summary>
	/// 从 Streaming Assets 文件夹中收集资源包
	/// </summary>
	private void CollectStreamingAssets()
	{
		if (string.IsNullOrWhiteSpace(StreamingAssetsPath))
		{
			return;
		}

		Logger.Info(LogCategory.Import, "正在收集流媒体资源...");
		if (FileSystem.Directory.Exists(StreamingAssetsPath))
		{
			CollectAssetBundlesRecursively(StreamingAssetsPath, Files);
		}
	}

	/// <summary>
	/// 仅从该目录收集资产包
	/// </summary>
	/// <remarks>
	/// 判定交由 <see cref="FileTypeScanner"/> 完成：先用名称与体积筛掉不可能命中的文件，
	/// 再对候选并发读取文件头。大型项目下该目录可能包含数十万文件，这一层筛选是主要提速点。
	/// </remarks>
	protected void CollectAssetBundles(string root, List<KeyValuePair<string, string>> files)
	{
		if (TryGetCachedScan(root, FileScanKind.Bundles, out List<KeyValuePair<string, string>>? cached))
		{
			LogBundleProgress(files, cached);
			files.AddRange(cached);
			return;
		}

		List<KeyValuePair<string, string>> scanned = FileTypeScanner.ScanBundles(FileSystem, root);
		StoreCachedScan(root, FileScanKind.Bundles, scanned);
		LogBundleProgress(files, scanned);
		files.AddRange(scanned);
	}

	/// <summary>
	/// 按原实现的粒度输出资源包进度日志，保证扫描过程对使用者仍可见。
	/// </summary>
	private void LogBundleProgress(List<KeyValuePair<string, string>> files, List<KeyValuePair<string, string>> scanned)
	{
		int startCount = files.Count;
		for (int i = 0; i < scanned.Count; i++)
		{
			// 与 AddAssetBundle 的每 1000 个一次保持一致，只是改为在批量收集后统一计算
			if ((startCount + i + 1) % 1000 == 0)
			{
				Logger.Info(LogCategory.Import, $"已找到资源包 {startCount + i + 1}:'{scanned[i].Key}'");
			}
		}
	}

	/// <summary>
	/// 从该目录及其所有子目录中收集资源包
	/// </summary>
	protected void CollectAssetBundlesRecursively(string root, List<KeyValuePair<string, string>> files)
	{
		CollectAssetBundles(root, files);
		foreach (string directory in FileSystem.Directory.EnumerateDirectories(root))
		{
			CollectAssetBundlesRecursively(directory, files);
		}
	}

	protected void CollectAssemblies(string root)
	{
		foreach (string file in FileSystem.Directory.EnumerateFiles(root))
		{
			string name = FileSystem.Path.GetFileName(file);
			if (MonoManager.IsMonoAssembly(name))
			{
				if (!Assemblies.TryAdd(name, file))
				{
					Logger.Log(LogType.Warning, LogCategory.Import, $"Duplicate assemblies found: '{Assemblies[name]}' & '{file}'");
				}
			}
		}
	}

	protected void CollectMainAssemblies()
	{
		if (Backend != ScriptingBackend.Mono)
		{
			return;//Only needed for Mono
		}
		else if (!string.IsNullOrWhiteSpace(ManagedPath) && FileSystem.Directory.Exists(ManagedPath))
		{
			CollectAssemblies(ManagedPath);
		}
		else if (!string.IsNullOrEmpty(GameDataPath))
		{
			string libPath = FileSystem.Path.Join(FileSystem.Path.GetFullPath(GameDataPath), LibName);
			if (FileSystem.Directory.Exists(libPath))
			{
				CollectAssemblies(GameDataPath);
				CollectAssemblies(libPath);
			}
		}
	}

	/// <summary>
	/// 目录扫描结果的缓存，键为 (目录, 扫描类型)。
	/// </summary>
	/// <remarks>
	/// 同一目录可能被多个平台结构重复扫描；缓存既避免重复 IO，也让“扫描结果落盘”可以按目录粒度复用。
	/// </remarks>
	private Dictionary<(string Directory, FileScanKind Kind), List<KeyValuePair<string, string>>>? _scanCache;

	/// <summary>
	/// 本次导入过程中被写入缓存的新扫描结果，供结束时统一落盘。
	/// </summary>
	private Dictionary<(string Directory, FileScanKind Kind), List<KeyValuePair<string, string>>>? _scanCacheAdditions;

	/// <summary>
	/// 扫描类型，用于区分同一目录下不同的筛选口径。
	/// </summary>
	private enum FileScanKind
	{
		SerializedFiles,
		Bundles,
	}

	/// <summary>
	/// 尝试从缓存取出该目录的扫描结果。
	/// </summary>
	private bool TryGetCachedScan(string directory, FileScanKind kind, [NotNullWhen(true)] out List<KeyValuePair<string, string>>? files)
	{
		files = null;
		return _scanCache is not null && _scanCache.TryGetValue((directory, kind), out files);
	}

	/// <summary>
	/// 记录本次扫描结果，既写入内存缓存也登记待落盘条目。
	/// </summary>
	private void StoreCachedScan(string directory, FileScanKind kind, List<KeyValuePair<string, string>> files)
	{
		_scanCache ??= [];
		_scanCacheAdditions ??= [];
		_scanCache[(directory, kind)] = files;
		_scanCacheAdditions[(directory, kind)] = files;
	}

	private string? FindEngineDependency(string path, string dependency)
	{
		string filePath = FileSystem.Path.Join(path, dependency);
		if (FileSystem.File.Exists(filePath))
		{
			return filePath;
		}

		string resourcePath = FileSystem.Path.Join(path, ResourcesName);
		filePath = FileSystem.Path.Join(resourcePath, dependency);
		if (FileSystem.File.Exists(filePath))
		{
			return filePath;
		}

		// really old versions contains file in this directory
		string unityPath = FileSystem.Path.Join(path, UnityName);
		filePath = FileSystem.Path.Join(unityPath, dependency);
		if (FileSystem.File.Exists(filePath))
		{
			return filePath;
		}
		return null;
	}

	/// <summary>
	/// Add game file
	/// </summary>
	protected static void AddFile(List<KeyValuePair<string, string>> files, string name, string path)
	{
		files.Add(name, path);
		Logger.Info(LogCategory.Import, $"Game file '{name}' has been found");
	}

	protected static void AddAssetBundle(List<KeyValuePair<string, string>> files, string name, string path)
	{
		files.Add(name, path);
		if (files.Count%1000==0)
		{
			Logger.Info(LogCategory.Import, $"已找到资源包 {files.Count}:'{name}'");
		}
	}

	protected UnityVersion GetUnityVersionFromSerializedFile(string filePath)
	{
		// 使用 using 确保 SerializedFile 持有的 SmartStream 引用被释放，
		// 避免仅为读取 Version 而长期占用底层文件流。
		using SerializedFile file = SerializedFile.FromFile(filePath, FileSystem);
		return file.Version;
	}

	protected UnityVersion GetUnityVersionFromBundleFile(string filePath)
	{
		using Stream stream = FileSystem.File.OpenRead(filePath);
		FileStreamBundleHeader header = new();
		header.Read(stream);
		return UnityVersion.Parse(header.UnityWebMinimumRevision);
	}

	protected UnityVersion? GetUnityVersionFromDataDirectory(string dataDirectoryPath)
	{
		string globalGameManagersPath = FileSystem.Path.Join(dataDirectoryPath, GlobalGameManagersName);
		if (FileSystem.File.Exists(globalGameManagersPath))
		{
			return GetUnityVersionFromSerializedFile(globalGameManagersPath);
		}

		string dataBundlePath = FileSystem.Path.Join(dataDirectoryPath, DataBundleName);
		if (FileSystem.File.Exists(dataBundlePath))
		{
			return GetUnityVersionFromBundleFile(dataBundlePath);
		}

		return null;
	}

	protected bool HasMonoAssemblies(string managedDirectory)
	{
		if (string.IsNullOrEmpty(managedDirectory) || !FileSystem.Directory.Exists(managedDirectory))
		{
			return false;
		}

		return FileSystem.Directory.GetFiles(managedDirectory, "*.dll").Length > 0;
	}

	protected bool HasIl2CppFiles()
	{
		return Il2CppGameAssemblyPath != null &&
		       Il2CppMetaDataPath != null &&
		       FileSystem.File.Exists(Il2CppGameAssemblyPath) &&
		       FileSystem.File.Exists(Il2CppMetaDataPath);
	}

	[GeneratedRegex("^level(?:0|[1-9][0-9]*)(?:\\.split0)?$", RegexOptions.Compiled)]
	private static partial Regex LevelTemplateRegex { get; }

	[GeneratedRegex("^sharedassets[0-9]+\\.assets", RegexOptions.Compiled)]
	private static partial Regex SharedAssetTemplateRegex { get; }
}
