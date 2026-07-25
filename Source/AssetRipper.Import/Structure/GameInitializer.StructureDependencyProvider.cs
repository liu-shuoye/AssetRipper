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
		FileSystem FileSystem)
		: IDependencyProvider
	{
		public FileBase? FindDependency(FileIdentifier identifier, Dictionary<string, string> skipContent)
		{
			string? systemFilePath = RequestDependency(identifier.PathName) ?? skipContent.GetValueOrDefault(identifier.PathName);
			if (systemFilePath is null)
			{
				Logger.Log(LogType.Warning, LogCategory.Import, $"未找到依赖项 '{identifier.PathName}',{identifier.PathName}");
				return null;
			}

			return SchemeReader.LoadFile(systemFilePath, FileSystem);
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
