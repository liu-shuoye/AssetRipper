using AsmResolver.DotNet;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;

namespace AssetRipper.Import.Structure.Assembly.Managers;

/// <summary>
/// 从外部工具（如 Il2CppDumper）导出的 dump 目录加载程序集的管理器。
/// 用于 Cpp2IL 无法解析的游戏（如元数据加密、二进制加固），
/// 由用户在外部完成解析后直接加载导出的 DummyDll 等托管程序集。
/// </summary>
public sealed class Il2CppDumpManager : BaseManager
{
	/// <summary>Il2CppDumper 默认的程序集输出子目录名。</summary>
	public const string DummyDllFolderName = "DummyDll";

	/// <summary>dump 程序集所在的实际目录。</summary>
	public string AssemblyDirectory { get; }

	/// <summary>
	/// dump 目录指向外部用户路径而非游戏文件，因此固定使用本地文件系统访问，
	/// 避免游戏本身通过虚拟文件系统（如压缩包）加载时外部路径不可见。
	/// </summary>
	private static LocalFileSystem DumpFileSystem => LocalFileSystem.Instance;

	public Il2CppDumpManager(Action<string> requestAssemblyCallback, string assemblyDirectory) : base(requestAssemblyCallback)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(assemblyDirectory);
		AssemblyDirectory = assemblyDirectory;
	}

	public override ScriptingBackend ScriptingBackend => ScriptingBackend.IL2Cpp;

	/// <summary>
	/// 解析 dump 目录：优先识别 Il2CppDumper 输出根目录下的 <see cref="DummyDllFolderName"/> 子目录，
	/// 否则要求目录本身包含程序集。目录无效时返回 false 以便调用方回退到 Cpp2IL。
	/// </summary>
	public static bool TryGetAssemblyDirectory(string path, [NotNullWhen(true)] out string? assemblyDirectory)
	{
		assemblyDirectory = null;
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		// Il2CppDumper 的标准输出结构为 output/DummyDll/*.dll
		string dummyDllPath = DumpFileSystem.Path.Join(path, DummyDllFolderName);
		if (HasAssemblies(dummyDllPath))
		{
			assemblyDirectory = dummyDllPath;
			return true;
		}

		// 也允许直接指向包含 dump 程序集的目录（如直接选择 DummyDll 目录本身）
		if (HasAssemblies(path))
		{
			assemblyDirectory = path;
			return true;
		}

		return false;

		static bool HasAssemblies(string directory)
		{
			return DumpFileSystem.Directory.Exists(directory)
				&& DumpFileSystem.Directory.EnumerateFiles(directory, "*.dll").Any();
		}
	}

	public override void Initialize(PlatformGameStructure gameStructure)
	{
		Logger.Info(LogCategory.Import, $"正在从外部 IL2Cpp dump 目录加载程序集：{AssemblyDirectory}");

		// 与 Mono 流程一致：先确保 mscorlib 可用并以此建立运行时上下文，
		// 否则跨程序集的类型引用（如游戏类型继承 MonoBehaviour）无法解析。
		AssemblyDefinition mscorlib;
		if (TryGetMscorlibPath(out string? mscorlibPath))
		{
			mscorlib = TryLoad(mscorlibPath) ?? LoadSystemRuntimeAsMscorlib();
		}
		else
		{
			mscorlib = LoadSystemRuntimeAsMscorlib();
		}

		// 运行时信息本身无关紧要（dump 程序集仅用于类型与字段解析），但 AsmResolver 要求指定一个。
		RuntimeContext runtimeContext = new(DotNetRuntimeInfo.NetCoreApp(10, 0), (bool?)null, mscorlib);
		runtimeContext.AddAssembly(mscorlib);

		int loadedCount = 1;// mscorlib 已计入
		foreach (string assemblyPath in DumpFileSystem.Directory.EnumerateFiles(AssemblyDirectory, "*.dll"))
		{
			// 文件名统一按大小写不敏感比较，兼容 Windows 下实际路径大小写与配置不一致的情况
			if (mscorlibPath is not null && string.Equals(assemblyPath, mscorlibPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (TryLoad(assemblyPath) is not null)
			{
				loadedCount++;
			}
		}

		Logger.Info(LogCategory.Import, $"已从 dump 目录加载 {loadedCount} 个程序集。");
	}

	private bool TryGetMscorlibPath([NotNullWhen(true)] out string? mscorlibPath)
	{
		string path = DumpFileSystem.Path.Join(AssemblyDirectory, "mscorlib.dll");
		if (DumpFileSystem.File.Exists(path))
		{
			mscorlibPath = path;
			return true;
		}
		mscorlibPath = null;
		return false;
	}

	/// <summary>
	/// dump 中缺少 mscorlib 时的兜底：用系统运行时程序集伪装成 mscorlib，
	/// 保证运行时上下文有可用的核心库。
	/// </summary>
	private AssemblyDefinition LoadSystemRuntimeAsMscorlib()
	{
		AssemblyDefinition assembly = AssemblyDefinition.FromBytes(Basic.Reference.Assemblies.Net100.ReferenceInfos.SystemRuntime.ImageBytes, createRuntimeContext: false);
		assembly.Name = "mscorlib";
		assembly.ManifestModule!.Name = "mscorlib.dll";
		assembly.Version = new Version(4, 0, 0, 0);
		Add(assembly);
		return assembly;
	}

	/// <summary>
	/// 单个程序集加载失败（如损坏或非托管 DLL）时记录警告并跳过，避免中断整体加载。
	/// </summary>
	private AssemblyDefinition? TryLoad(string assemblyPath)
	{
		try
		{
			return Load(assemblyPath, DumpFileSystem);
		}
		catch (Exception ex)
		{
			string assemblyName = DumpFileSystem.Path.GetFileName(assemblyPath);
			Logger.Warning(LogCategory.Import, $"跳过无法加载的 dump 程序集 '{assemblyName}'：{ex.Message}");
			return null;
		}
	}
}
