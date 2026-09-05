using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

// 探针：验证 Il2CppDumper 生成的 DummyDll 中，
// 显式接口实现（方法名含点号）在 ILSpy 反编译输出中会被如何转义。

string input = args.Length > 0
	? args[0]
	: @"E:\BaiduNetdiskDownload\解包数据\Tool\Il2CppDumper-win-v6.7.46\DummyDll\Assembly-CSharp-firstpass.dll";

// ---------- 1. 先看元数据里方法名到底长什么样 ----------
PEFile module = new(input);
DecompilerTypeSystem typeSystem = new(module, new UniversalAssemblyResolver(input, false, null));

Console.WriteLine("=== 元数据中含点号的方法名（前 10 个）===");
int shown = 0;
foreach (ITypeDefinition type in typeSystem.MainModule.TypeDefinitions)
{
	foreach (IMethod method in type.Methods)
	{
		if (method.Name.Contains('.'))
		{
			// ExplicitInterfaceImplementations 为空说明该 DLL 没有写入 MethodImpl 表
			int implCount = method.ExplicitInterfaceImplementations.Length;
			Console.WriteLine($"{type.FullName} :: {method.Name}   ExplicitInterfaceImplementations={implCount}");
			if (++shown >= 10)
			{
				goto afterList;
			}
		}
	}
}
afterList:

// ---------- 2. 实际反编译一个含点号方法的类型 ----------
Console.WriteLine();
Console.WriteLine("=== C# 反编译输出片段 ===");
DecompilerSettings settings = new();
settings.SetLanguageVersion(LanguageVersion.CSharp7_3);
settings.AlwaysShowEnumMemberValues = true;
settings.ShowXmlDocumentation = true;
settings.UseNestedDirectoriesForNamespaces = true;

CSharpDecompiler decompiler = new(typeSystem, settings);
ITypeDefinition target = typeSystem.MainModule.TypeDefinitions
	.FirstOrDefault(t => t.Methods.Any(m => m.Name.Contains('.')));
if (target is not null)
{
	string code = decompiler.DecompileTypeAsString(new FullTypeName(target.ReflectionName));
	foreach (string line in code.Split('\n'))
	{
		if (line.Contains('.'))
		{
			Console.WriteLine(line.TrimEnd('\r'));
		}
	}
}

// ---------- 3. 整项目反编译，观察文件名 ----------
Console.WriteLine();
Console.WriteLine("=== 整项目反编译后的文件名（含转义嫌疑的）===");
string outDir = Path.Combine(Path.GetTempPath(), "probe_out");
if (Directory.Exists(outDir))
{
	Directory.Delete(outDir, true);
}
Directory.CreateDirectory(outDir);

WholeProjectDecompiler project = new(settings, new UniversalAssemblyResolver(input, false, null), null, null, null);
project.DecompileProject(module, outDir, TextWriter.Null);

int checked_ = 0;
foreach (string file in Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories))
{
	string relative = Path.GetRelativePath(outDir, file);
	if (relative.Contains(@"\u") || relative.Contains(@"u002E"))
	{
		Console.WriteLine(relative);
		checked_++;
	}
}
Console.WriteLine($"含 \\u 转义的文件数：{checked_}");
Console.WriteLine($"总文件数：{Directory.EnumerateFiles(outDir, "*", SearchOption.AllDirectories).Count()}");
