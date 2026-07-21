using AssetRipper.Assets.Bundles;
using AssetRipper.Import.AssetCreation;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Platforms;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.ResourceFiles;

namespace AssetRipper.Import.Structure;

/// <summary> 表示游戏的结构，包含所有文件和资源。 </summary>
public sealed class GameStructure : IDisposable
{
	/// <summary> 所有文件和资源的集合。 </summary>
	public GameBundle FileCollection { get; private set; }

	/// <summary> 平台游戏结构，如果游戏结构仅包含一个平台，则为非空。 </summary>
	public PlatformGameStructure? PlatformStructure { get; private set; }

	/// <summary> 混合游戏结构，如果游戏结构包含多个平台，则为非空。 </summary>
	public PlatformGameStructure? MixedStructure { get; private set; }

	/// <summary> 程序集管理器，用于加载和管理程序集。 </summary>
	public IAssemblyManager AssemblyManager { get; set; }

	/// <summary> 文件系统，用于访问游戏文件。 </summary>
	public FileSystem FileSystem { get; }

	private GameStructure(List<string> paths, FileSystem fileSystem, CoreConfiguration configuration)
	{
		Logger.SendStatusChange("loading_step_detect_platform");
		FileSystem = fileSystem;
		PlatformChecker.CheckPlatform(paths, fileSystem, out PlatformGameStructure? platformStructure, out MixedGameStructure? mixedStructure);
		PlatformStructure = platformStructure;
		PlatformStructure?.CollectFiles(configuration.ImportSettings.IgnoreStreamingAssets);
		MixedStructure = mixedStructure;
		//MixedStructure?.CollectFiles(configuration.IgnoreStreamingAssets);
		//The PlatformGameStructure constructor adds all the paths to the Assemblies and Files dictionaries
		//No bundles or assemblies have been loaded yet

		Logger.SendStatusChange("loading_step_initialize_layout");

		InitializeAssemblyManager(configuration);

		Logger.SendStatusChange("loading_step_begin_scheme_processing");

		InitializeGameCollection(configuration.ImportSettings.DefaultVersion, configuration.ImportSettings.TargetVersion);

		if (!FileCollection.HasAnyAssetCollections())
		{
			Logger.Log(LogType.Warning, LogCategory.Import, "游戏结构处理器无法找到任何有效的资源。");
		}
	}

	public bool IsValid => FileCollection.HasAnyAssetCollections();

	public string? Name => PlatformStructure?.Name ?? MixedStructure?.Name;

	/// <summary> 加载游戏结构。 </summary>
	public static GameStructure Load(IEnumerable<string> paths, FileSystem fileSystem, CoreConfiguration configuration)
	{
		List<string> toProcess = ZipExtractor.Process(paths, fileSystem);
		if (toProcess.Count == 0)
		{
			throw new ArgumentException("Game files not found", nameof(paths));
		}

		return new GameStructure(toProcess, fileSystem, configuration);
	}

	/// <summary> 初始化游戏文件集合。 </summary>
	[MemberNotNull(nameof(FileCollection))]
	private void InitializeGameCollection(UnityVersion defaultVersion, UnityVersion targetVersion)
	{
		Logger.SendStatusChange("loading_step_create_file_collection");

		GameAssetFactory assetFactory = new GameAssetFactory(AssemblyManager);

		IEnumerable<string> filePaths;
		if (PlatformStructure is null || MixedStructure is null)
		{
			filePaths = (PlatformStructure ?? MixedStructure)?.Files.Values() ?? [];
		}
		else
		{
			filePaths = PlatformStructure.Files.Union(MixedStructure.Files).Select(pair => pair.Value);
		}

		FileCollection = GameBundle.FromPaths(
			filePaths,
			assetFactory,
			FileSystem,
			new GameInitializer(PlatformStructure, MixedStructure, FileSystem, defaultVersion, targetVersion));
	}

	/// <summary> 初始化程序集管理器。 </summary>
	[MemberNotNull(nameof(AssemblyManager))]
	private void InitializeAssemblyManager(CoreConfiguration configuration)
	{
		ScriptingBackend scriptBackend = GetScriptingBackend(configuration.DisableScriptImport);
		Logger.Info(LogCategory.Import, $"文件使用 '{scriptBackend}' 脚本后端。");

		AssemblyManager = scriptBackend switch
		{
			ScriptingBackend.Mono => new MonoManager(OnRequestAssembly),
			ScriptingBackend.IL2Cpp => new IL2CppManager(OnRequestAssembly, configuration.ImportSettings.ScriptContentLevel),
			_ => new BaseManager(OnRequestAssembly),
		};

		Logger.SendStatusChange("loading_step_load_assemblies");

		try
		{
			//Loads any Mono or IL2Cpp assemblies
			AssemblyManager.Initialize(PlatformStructure ?? MixedStructure ?? throw new Exception("No platform structure"));
		}
		catch (Exception ex)
		{
			Logger.Error(LogCategory.Import, "无法初始化程序集管理器。正在切换到“未知”脚本后端。");
			Logger.Error(ex);
			AssemblyManager = new BaseManager(OnRequestAssembly);
		}
	}

	/// <summary> 获取脚本后端。 </summary>
	private ScriptingBackend GetScriptingBackend(bool disableScriptImport)
	{
		if (disableScriptImport)
		{
			Logger.Info(LogCategory.Import, "由于设置已禁用脚本导入。");
			return ScriptingBackend.Unknown;
		}

		if (PlatformStructure != null)
		{
			ScriptingBackend backend = PlatformStructure.Backend;
			if (backend != ScriptingBackend.Unknown)
			{
				return backend;
			}
		}

		if (MixedStructure != null)
		{
			ScriptingBackend backend = MixedStructure.Backend;
			if (backend != ScriptingBackend.Unknown)
			{
				return backend;
			}
		}

		return ScriptingBackend.Unknown;
	}

	/// <summary> 当请求程序集时调用。 </summary>
	private void OnRequestAssembly(string assembly)
	{
		string assemblyName = $"{assembly}.dll";
		ResourceFile? resFile = FileCollection.ResolveResource(assemblyName);
		if (resFile is not null)
		{
			resFile.Stream.Position = 0;
			AssemblyManager.Read(resFile.Stream.CreateReference(), assemblyName);
		}
		else
		{
			string? path = PlatformStructure?.RequestAssembly(assembly) ?? MixedStructure?.RequestAssembly(assembly);
			if (path is null)
			{
				Logger.Log(LogType.Warning, LogCategory.Import, $"未找到程序集 '{assembly}'");
				return;
			}

			AssemblyManager.Load(path, FileSystem);
		}

		Logger.Info(LogCategory.Import, $"已加载程序集 '{assembly}'");
	}

	/// <summary> 释放游戏结构。 </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary> 释放游戏结构。 </summary>
	private void Dispose(bool _)
	{
		AssemblyManager?.Dispose();
		FileCollection?.Dispose();
	}
}
