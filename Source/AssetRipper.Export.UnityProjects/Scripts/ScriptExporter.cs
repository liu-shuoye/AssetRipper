using AssetRipper.Assets;
using AssetRipper.Export.Configuration;
using AssetRipper.Import.Logging;
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

	public MetaPtr CreateExportPointer(IMonoScript script)
	{
		return GetExportType(script) switch
		{
			AssemblyExportType.Decompile => new(MonoScriptDecompiledFileID, ScriptHashing.CalculateScriptGuid(script), AssetType.Meta),
			// Skip 分支：UPM 程序集优先用每个脚本的真实 .cs.meta GUID（fileID=11500000），
			// 与 Unity Package Manager 安装后的资产 GUID 一致；未命中时回退到程序集级 GUID
			AssemblyExportType.Skip => CreateSkipExportPointer(script),
			_ => new(ScriptHashing.CalculateScriptFileID(script), ScriptHashing.CalculateAssemblyGuid(script), AssetType.Meta),
		};
	}

	/// <summary>
	/// 为 Skip 路径下的脚本创建导出指针。
	/// UPM 程序集：优先用脚本级映射（.cs.meta GUID + fileID=11500000），保证与 Unity 包内脚本引用一致。
	/// 传统引用程序集或 UPM 脚本未命中映射时：回退到程序集级 GUID + MD4 fileID。
	/// </summary>
	private MetaPtr CreateSkipExportPointer(IMonoScript script)
	{
		string assemblyName = script.GetAssemblyNameFixed();
		if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out _))
		{
			// UPM 程序集：优先查脚本级 .cs.meta GUID
			// Namespace.Data 和 ClassName_R.Data 返回 ReadOnlySpan<byte>，需转为 string 用于字典查找
			string ns = Encoding.UTF8.GetString(script.Namespace.Data);
			string className = Encoding.UTF8.GetString(script.ClassName_R.Data);
			if (UnityPackageAssemblyMap.TryGetScriptGuid(assemblyName, ns, className, out UnityGuid scriptGuid))
			{
				// fileID=11500000 是 Unity 对 MonoScript 的固定导出 ID，与 Decompile 分支一致
				return new MetaPtr(MonoScriptDecompiledFileID, scriptGuid, AssetType.Meta);
			}
			// 脚本级映射未命中：回退到程序集级 GUID，并输出警告便于后续补充映射
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
