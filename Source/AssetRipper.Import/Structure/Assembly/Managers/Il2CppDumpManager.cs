using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
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

		// 现代 Il2CppDumper（.NET 6+）生成的 DummyDll 以 System.Private.CoreLib 而非 mscorlib
		// 作为核心库作用域。若运行时上下文中缺少该名称的程序集，解析 System.Object 等基础类型时会
		// 回退到磁盘查找并抛出 FileNotFoundException，导致序列化类型计算（FieldSerializer）崩溃。
		// 因此始终用 Net100 参考程序集伪装出一个 System.Private.CoreLib 注册到运行时上下文；
		// 同时保留 mscorlib 以兼容仍引用 mscorlib 的旧版 dump。
		AssemblyDefinition privateCoreLib = CreateCorLib("System.Private.CoreLib", "System.Private.CoreLib.dll", new Version(10, 0, 0, 0));
		// 在 RuntimeContext 建立前先登记到 m_assemblies：此时 Add 仅写入字典、不会注册到运行时上下文，
		// 避免后续 AddAssembly 造成重复注册并抛出异常。
		Add(privateCoreLib);

		// mscorlib：优先使用 dump 自带，否则用参考程序集伪装
		AssemblyDefinition mscorlib;
		if (TryGetMscorlibPath(out string? mscorlibPath))
		{
			mscorlib = TryLoad(mscorlibPath) ?? CreateCorLib("mscorlib", "mscorlib.dll", new Version(4, 0, 0, 0));
		}
		else
		{
			mscorlib = CreateCorLib("mscorlib", "mscorlib.dll", new Version(4, 0, 0, 0));
		}
		Add(mscorlib);

		// 运行时信息本身无关紧要（dump 程序集仅用于类型与字段解析），但 AsmResolver 要求指定一个。
		// 以 System.Private.CoreLib 作为主核心库，使 CorLibTypeFactory 与 dump 的引用保持一致。
		RuntimeContext runtimeContext = new(DotNetRuntimeInfo.NetCoreApp(10, 0), (bool?)null, privateCoreLib);
		runtimeContext.AddAssembly(privateCoreLib);
		runtimeContext.AddAssembly(mscorlib);

		int loadedCount = 2;// System.Private.CoreLib 与 mscorlib 已计入

		foreach (string assemblyPath in DumpFileSystem.Directory.EnumerateFiles(AssemblyDirectory, "*.dll"))
		{
			// 文件名统一按大小写不敏感比较，兼容 Windows 下实际路径大小写与配置不一致的情况
			if (mscorlibPath is not null && string.Equals(assemblyPath, mscorlibPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			// 跳过 dump 若自带的 System.Private.CoreLib（极少见），避免与上方伪装版重复注册
			if (string.Equals(DumpFileSystem.Path.GetFileName(assemblyPath), "System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (TryLoad(assemblyPath) is not null)
			{
				loadedCount++;
			}
		}

		Logger.Info(LogCategory.Import, $"已从 dump 目录加载 {loadedCount} 个程序集。");

		// DummyDll 的调试标记会污染反编译导出的脚本，加载后统一清理
		RemoveDumpAttributes();

		// DummyDll 的属性/方法桩体常为 "ldnull; ret"，反编译会输出 (T)null；
		// 当 T 是值类型时该写法无法编译，故改写为等价 default(T) 的 IL
		ReplaceNullStubReturns();
	}

	/// <summary>
	/// Il2CppDumper 生成的 DummyDll 会给每个类型/字段/方法附加调试标记特性
	/// （TokenAttribute 记录元数据 token、FieldOffsetAttribute 记录字段偏移、AddressAttribute 记录 RVA 等）。
	/// 这些标记会被反编译器原样输出到导出的 C# 脚本中，既难看又依赖并不存在的特性类定义，因此加载后必须移除。
	/// 同时移除方法上的状态机特性：其 typeof 指向编译器生成的隐藏类，导出过滤掉隐藏类后
	/// 若不清理会造成悬挂引用、脚本无法编译。
	/// </summary>
	private void RemoveDumpAttributes()
	{
		foreach (AssemblyDefinition assembly in GetAssemblies())
		{
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (TypeDefinition type in module.GetAllTypes())
				{
					RemoveAttributes(type);
					RemoveAttributes(type.Fields);
					RemoveAttributes(type.Properties);
					RemoveAttributes(type.Events);
					RemoveAttributes(type.Methods);

					foreach (MethodDefinition method in type.Methods)
					{
						RemoveAttributes(method.ParameterDefinitions);
					}
				}
			}
		}
	}

	private static void RemoveAttributes<T>(IEnumerable<T> providers) where T : IHasCustomAttribute
	{
		foreach (T provider in providers)
		{
			RemoveAttributes(provider);
		}
	}

	private static void RemoveAttributes(IHasCustomAttribute provider)
	{
		for (int i = provider.CustomAttributes.Count - 1; i >= 0; i--)
		{
			if (IsDumpAttribute(provider.CustomAttributes[i]) || IsStateMachineAttribute(provider.CustomAttributes[i]))
			{
				provider.CustomAttributes.RemoveAt(i);
			}
		}
	}

	private static bool IsDumpAttribute(CustomAttribute attribute)
	{
		// 新版本 Il2CppDumper 将所有 dump 调试特性放在 Il2CppDummyDll 命名空间；
		// 老版本放在全局命名空间，仅保留少数几个已知特性名
		if (attribute.Constructor?.DeclaringType is not { } type)
		{
			return false;
		}

		string? ns = type.Namespace?.Value;
		if (ns == "Il2CppDummyDll")
		{
			return true;
		}
		return string.IsNullOrEmpty(ns) && type.Name?.Value is "TokenAttribute" or "FieldOffsetAttribute" or "AddressAttribute";
	}

	/// <summary>
	/// 判断是否为编译器生成的状态机特性（AsyncStateMachine / IteratorStateMachine / AsyncIteratorStateMachine）。
	/// 这些特性用 typeof 引用对应的状态机隐藏类，导出时隐藏类被过滤后必须同步移除，否则产生悬挂引用。
	/// </summary>
	private static bool IsStateMachineAttribute(CustomAttribute attribute)
	{
		return attribute.Constructor?.DeclaringType is { } type
			&& type.Namespace?.Value == "System.Runtime.CompilerServices"
			&& type.Name?.Value is "AsyncStateMachineAttribute" or "IteratorStateMachineAttribute" or "AsyncIteratorStateMachineAttribute";
	}

	/// <summary>
	/// 改写返回泛型参数的 "ldnull; ret" 桩体。
	/// 这类桩体被 ILSpy 反编译成 (T)null：对无 class 约束的泛型 T 而言，
	/// 若实例化为值类型则无法编译（null 不能转值类型）。改写为
	/// "ldloca+initobj+ldloc; ret"（即 default(T)）后，引用与值类型都成立，
	/// 反编译结果从 (T)null 变为 default(T)。
	/// </summary>
	private void ReplaceNullStubReturns()
	{
		foreach (ModuleDefinition module in GetAssemblies().SelectMany(assembly => assembly.Modules))
		{
			foreach (TypeDefinition type in module.GetAllTypes())
			{
				foreach (MethodDefinition method in type.Methods)
				{
					// 仅处理体为 "ldnull; ret"（允许中间夹杂 nop）且返回泛型参数的方法
					if (method.CilMethodBody is not { } body
						|| method.Signature?.ReturnType is not GenericParameterSignature returnType)
					{
						continue;
					}

					CilInstruction[] effectiveInstructions = body.Instructions
						.Where(instruction => instruction.OpCode != CilOpCodes.Nop)
						.ToArray();
					if (effectiveInstructions.Length != 2
						|| effectiveInstructions[0].OpCode != CilOpCodes.Ldnull
						|| effectiveInstructions[1].OpCode != CilOpCodes.Ret)
					{
						continue;
					}

					CilLocalVariable local = new(returnType);
					method.CilMethodBody = new CilMethodBody();
					method.CilMethodBody.LocalVariables.Add(local);
					CilInstructionCollection instructions = method.CilMethodBody.Instructions;
					instructions.Add(CilOpCodes.Ldloca, local);
					instructions.Add(CilOpCodes.Initobj, returnType.ToTypeDefOrRef());
					instructions.Add(CilOpCodes.Ldloc, local);
					instructions.Add(CilOpCodes.Ret);
				}
			}
		}
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
	/// 用 Net100 系统运行时参考程序集伪装出一个核心库程序集。
	/// Il2CppDumper 的 .NET 6+ DummyDll 以 System.Private.CoreLib 作为核心库作用域，
	/// 旧版则使用 mscorlib，因此两者都需注册到运行时上下文，否则解析 System.Object 等
	/// 基础类型会回退到磁盘查找并抛出 FileNotFoundException。
	/// </summary>
	private static AssemblyDefinition CreateCorLib(string name, string moduleName, Version version)
	{
		AssemblyDefinition assembly = AssemblyDefinition.FromBytes(Basic.Reference.Assemblies.Net100.ReferenceInfos.SystemRuntime.ImageBytes, createRuntimeContext: false);
		assembly.Name = name;
		assembly.ManifestModule!.Name = moduleName;
		assembly.Version = version;
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
