using AsmResolver.DotNet;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects.Scripts;
using AssetRipper.Processing;

namespace AssetRipper.Export.UnityProjects.Project;

public class PackageManifestPostExporter : IPostExporter
{
	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		string packagesDirectory = fileSystem.Path.Join(settings.ProjectRootPath, "Packages");
		fileSystem.Directory.Create(packagesDirectory);
		string path = fileSystem.Path.Join(packagesDirectory, "manifest.json");
		using Stream stream = fileSystem.File.Create(path);
		// 收集实际加载的程序集名，供 CreateManifest 判断需要补哪些 UPM 包依赖；
		// AssemblyManager 在异常情况下可能为 null，此时退化为空集合，仅写默认模块依赖
		IEnumerable<string> assemblyNames = (gameData.AssemblyManager?.GetAssemblies() ?? Enumerable.Empty<AssemblyDefinition>())
			.Where(a => a.Name is not null)
			.Select(a => (string)a.Name!);
		CreateManifest(settings.Version, assemblyNames).Save(stream);
	}

	protected virtual PackageManifest CreateManifest(UnityVersion version, IEnumerable<string> assemblyNames)
	{
		PackageManifest manifest = PackageManifest.CreateDefault(version);
		// 遍历实际加载的程序集，把命中 UPM 映射的包写入 dependencies。
		// 使用 TryAdd 语义：不覆盖默认模块依赖，同一包被多个程序集引用时只写入一次
		foreach (string assemblyName in assemblyNames)
		{
			if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out UnityPackageAssemblyInfo info))
			{
				manifest.Dependencies.TryAdd(info.PackageName, info.Version);
			}
		}
		return manifest;
	}
}
