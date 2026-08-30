using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.IO;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;

namespace AssetRipper.Import.Structure;

/// <summary>
/// 默认游戏结构的游戏初始化器。
/// </summary>
internal sealed partial record class GameInitializer : DefaultGameInitializer
{
	public UnityVersion TargetVersion { get; }

	// dependencyMap 为可选参数，保持既有调用方兼容；仅在启用依赖关系映射时参与依赖解析
	public GameInitializer(PlatformGameStructure? platformStructure, PlatformGameStructure? mixedStructure, FileSystem fileSystem, UnityVersion defaultVersion, UnityVersion targetVersion, DependencyMap? dependencyMap = null)
		: base(new StructureDependencyProvider(platformStructure, mixedStructure, fileSystem, dependencyMap), new CustomResourceProvider(platformStructure, mixedStructure, fileSystem), defaultVersion)
	{
		TargetVersion = targetVersion;
	}

	public override void OnPathsLoaded(GameBundle gameBundle, AssetFactoryBase assetFactory)
	{
		EngineResourceInjector.InjectEngineFilesIfNecessary(gameBundle, TargetVersion);
	}

	public override void OnDependenciesInitialized(GameBundle gameBundle, AssetFactoryBase assetFactory)
	{
		if (TargetVersion != default)
		{
			VersionChanger.ChangeVersions(gameBundle.FetchAssetCollections(), TargetVersion);
		}
	}
}
