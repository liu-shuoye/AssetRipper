using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Logging;
using AssetRipper.Import.Structure.Assembly;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_115;
using System.Text;

namespace AssetRipper.Export.UnityProjects.Scripts;

public class ScriptExporter : IAssetExporter
{
	public ScriptExporter(IAssemblyManager assemblyManager, FullConfiguration configuration)
	{
		AssemblyManager = assemblyManager;
		Decompiler = new ScriptDecompiler(AssemblyManager)
		{
			LanguageVersion = configuration.ExportSettings.ScriptLanguageVersion.ToCSharpLanguageVersion(configuration.Version),
			ScriptContentLevel = configuration.ImportSettings.ScriptContentLevel,
			FullyQualifiedTypeNames = configuration.ExportSettings.ScriptTypesFullyQualified,
			// dump 场景的 DummyDll 无方法体，编译器生成的隐藏类无法还原，导出时直接过滤
			FilterCompilerGeneratedTypes = AssemblyManager is Il2CppDumpManager,
		};
		ExportMode = configuration.ExportSettings.ScriptExportMode;
		ReferenceAssemblyDictionary = ReferenceAssemblies.GetReferenceAssemblies(AssemblyManager, configuration.Version);
	}

	public IAssemblyManager AssemblyManager { get; }
	public ScriptExportMode ExportMode { get; }
	internal ScriptDecompiler Decompiler { get; }
	internal Dictionary<string, UnityGuid> ReferenceAssemblyDictionary { get; }
	private bool HasDecompiled { get; set; } = false;
	private static long MonoScriptDecompiledFileID { get; } = ExportIdHandler.GetMainExportID((int)ClassIDType.MonoScript);

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		if (asset is IMonoScript script)
		{
			if (HasDecompiled)
			{
				exportCollection = new SingleRedirectExportCollection(asset, CreateExportPointer(script));
			}
			else
			{
				HasDecompiled = true;
				if (AssemblyManager.IsSet)
				{
					exportCollection = new ScriptExportCollection(this, script);
				}
				else
				{
					exportCollection = new EmptyScriptExportCollection(this, script);
				}
			}
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public AssemblyExportType GetExportType(IMonoScript script)
	{
		return GetExportType(script.GetAssemblyNameFixed());
	}

	/// <summary>
	/// 创建指向该脚本资源的导出指针（供场景、预制体中的 m_Script 等引用使用）。
	/// 采集到 .cs.meta 原始 GUID 的脚本一律使用 Unity 原生脚本引用格式；其余按导出类型沿用哈希算法。
	/// </summary>
	public MetaPtr CreateExportPointer(IMonoScript script)
	{
		return GetExportType(script) switch
		{
			// Decompile 会把脚本以 .cs 形式导出，其 .cs.meta 也走 ResolveScriptGuid，
			// 两边共用同一解析结果才能保证引用与资源 GUID 一致
			AssemblyExportType.Decompile => new(MonoScriptDecompiledFileID, ResolveScriptGuid(script), AssetType.Meta),
			// Skip 分支的脚本不随工程导出，引用必须指向目标工程中已存在的脚本资源
			AssemblyExportType.Skip => CreateSkipExportPointer(script),
			_ => new(ScriptHashing.CalculateScriptFileID(script), ScriptHashing.CalculateAssemblyGuid(script), AssetType.Meta),
		};
	}

	/// <summary>
	/// 解析脚本资源应使用的 GUID：优先取采集到的 .cs.meta 原始 GUID（UPM 包脚本与工程 Assets/Scripts 脚本），
	/// 未命中时回退到基于 (程序集名, 命名空间, 类名) 的哈希，保持历史导出结果不变。
	/// </summary>
	/// <param name="script">待解析的 MonoScript 资源。</param>
	/// <returns>该脚本在导出工程中应使用的资产 GUID。</returns>
	/// <remarks>
	/// 引用指针与导出脚本的 .cs.meta 必须共用此入口，否则预制体中的 m_Script 会指向一个不存在的 GUID。
	/// </remarks>
	public static UnityGuid ResolveScriptGuid(IMonoScript script)
	{
		return TryGetCollectedScriptGuid(script, out UnityGuid guid) ? guid : ScriptHashing.CalculateScriptGuid(script);
	}

	/// <summary>
	/// 尝试查询采集器记录的脚本原始 GUID。
	/// </summary>
	private static bool TryGetCollectedScriptGuid(IMonoScript script, out UnityGuid guid)
	{
		string assemblyName = script.GetAssemblyNameFixed();
		// Namespace.Data 和 ClassName_R.Data 返回 ReadOnlySpan<byte>，需转为 string 用于字典查找
		string ns = Encoding.UTF8.GetString(script.Namespace.Data);
		string className = Encoding.UTF8.GetString(script.ClassName_R.Data);
		return UnityPackageAssemblyMap.TryGetScriptGuid(assemblyName, ns, className, out guid);
	}

	/// <summary>
	/// 为 Skip 路径下的脚本创建导出指针。
	/// 只要命中脚本级 .cs.meta GUID 就用 fileID=11500000 + 该 GUID，不再限定于 UPM 程序集；
	/// 未命中时：UPM 程序集回退到程序集级 GUID，传统引用程序集用 ReferenceAssemblyDictionary 的 GUID。
	/// </summary>
	private MetaPtr CreateSkipExportPointer(IMonoScript script)
	{
		// 采集到的脚本在目标工程里已有对应 .cs 资源，按 Unity 原生脚本引用格式指向它
		if (TryGetCollectedScriptGuid(script, out UnityGuid scriptGuid))
		{
			return new MetaPtr(MonoScriptDecompiledFileID, scriptGuid, AssetType.Meta);
		}

		string assemblyName = script.GetAssemblyNameFixed();
		if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out _))
		{
			// 脚本级映射未命中：回退到程序集级 GUID，并输出警告便于后续补充映射
			string ns = Encoding.UTF8.GetString(script.Namespace.Data);
			string className = Encoding.UTF8.GetString(script.ClassName_R.Data);
			Logger.Warning(LogCategory.Export, $"UPM 程序集 '{assemblyName}' 的脚本 '{ns}.{className}' 未命中脚本级 GUID 映射，回退到程序集级 GUID。");
			return new MetaPtr(ScriptHashing.CalculateScriptFileID(script), ResolveSkipGuid(assemblyName), AssetType.Meta);
		}
		// 传统引用程序集（UnityEngine.* 等）：用 ReferenceAssemblyDictionary 的 GUID
		return new MetaPtr(ScriptHashing.CalculateScriptFileID(script), ReferenceAssemblyDictionary[assemblyName], AssetType.Meta);
	}

	/// <summary>
	/// 解析 Skip 路径下 UPM 程序集对应的资产 GUID（程序集级，仅用于兜底）。
	/// </summary>
	private UnityGuid ResolveSkipGuid(string assemblyName)
	{
		if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out UnityPackageAssemblyInfo info))
		{
			return info.Guid;
		}
		return ReferenceAssemblyDictionary[assemblyName];
	}

	public AssemblyExportType GetExportType(string assemblyName)
	{
		// UPM 包程序集优先跳过导出，改由 Packages/manifest.json 引用对应包。
		// 必须放在 ReferenceAssemblyDictionary 判定之前，因为 UPM 程序集不在该字典里。
		if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out _))
		{
			return AssemblyExportType.Skip;
		}
		if (ReferenceAssemblyDictionary.ContainsKey(assemblyName))
		{
			return AssemblyExportType.Skip;
		}
		else if (!AssemblyManager.IsSet)
		{
			return AssemblyExportType.Decompile;
		}
		else if (ExportMode is ScriptExportMode.Decompiled)
		{
			return AssemblyExportType.Decompile;
		}
		else if (ExportMode is ScriptExportMode.Hybrid)
		{
			return ReferenceAssemblies.IsPredefinedAssembly(assemblyName)
				? AssemblyExportType.Decompile
				: AssemblyExportType.Save;
		}
		else
		{
			return AssemblyExportType.Save;
		}
	}

	AssetType IAssetExporter.ToExportType(IUnityObjectBase asset) => AssetType.Meta;

	bool IAssetExporter.ToUnknownExportType(Type type, out AssetType assetType)
	{
		assetType = AssetType.Meta;
		return true;
	}
}
