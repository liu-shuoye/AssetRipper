using AssetRipper.Assets.Bundles;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles.Parser;

namespace AssetRipper.Import.Structure;

internal sealed partial record class GameInitializer
{
	private sealed record class StructureDependencyProvider(
		PlatformGameStructure? PlatformStructure,
		PlatformGameStructure? MixedStructure,
		FileSystem FileSystem,
		DependencyMap? DependencyMap)
		: IDependencyProvider
	{
		public FileBase? FindDependency(FileIdentifier identifier)
		{
			string? systemFilePath = RequestDependency(identifier.PathName);
			if (systemFilePath is not null)
			{
				return SchemeReader.LoadFile(systemFilePath, FileSystem);
			}

			// 打开的是游戏子文件夹时，依赖文件不在目录结构内，回退到依赖关系映射按名查找
			if (DependencyMap is not null && DependencyMap.TryResolve(identifier.PathName, out string mapPath))
			{
				try
				{
					Logger.Info(LogCategory.Import, $"依赖关系映射解析 '{identifier.PathNameOrigin}' -> '{mapPath}'");
					return SchemeReader.LoadFile(mapPath, FileSystem);
				}
				catch (Exception ex) // 映射指向的文件可能已被移动或损坏，失败时按未找到处理
				{
					Logger.Warning(LogCategory.Import, $"依赖关系映射解析 '{identifier.PathNameOrigin}' 失败：{ex.Message}");
					return null;
				}
			}

			return null;
		}

		/// <summary>
		/// Attempts to find the path for the dependency with that name.
		/// </summary>
		private string? RequestDependency(string dependency)
		{
			return PlatformStructure?.RequestDependency(dependency) ?? MixedStructure?.RequestDependency(dependency);
		}

		public void ReportMissingDependency(FileIdentifier identifier)
		{
			Logger.Log(LogType.Warning, LogCategory.Import, $"未找到依赖项 '{identifier.PathNameOrigin}',{identifier.PathName}");
		}
	}
}
