using AssetRipper.Assets.Collections;
using AssetRipper.IO.Files.ResourceFiles;

namespace AssetRipper.Assets.Bundles;

/// <summary>
/// 一个包含整个游戏的 <see cref="Bundle"/>。
/// </summary>
public sealed partial class GameBundle : Bundle
{
	/// <summary>
	/// 用于此捆绑包的 <see cref="IResourceProvider"/>。
	/// </summary>
	public IResourceProvider? ResourceProvider { get; set; }

	/// <summary>
	/// 此捆绑包的名称，为 'GameBundle'。
	/// </summary>
	public override string Name => nameof(GameBundle);

	/// <summary>
	/// 如果给定的捆绑包与此捆绑包兼容，则返回 true。
	/// </summary>
	/// <param name="bundle">The bundle to check compatibility with.</param>
	protected override bool IsCompatibleBundle(Bundle bundle)
	{
		return bundle is not GameBundle;
	}

	/// <summary>
	/// 解析外部资源文件，如果无法找到则返回 null。
	/// </summary>
	/// <param name="originalName">The original name of the ResourceFile.</param>
	protected override ResourceFile? ResolveExternalResource(string originalName)
	{
		if (ResourceProvider is not null)
		{
			ResourceFile? resourceFile = ResourceProvider.FindResource(originalName);
			if (resourceFile is not null)
			{
				AddResource(resourceFile);
			}
			return resourceFile;
		}
		else
		{
			return base.ResolveExternalResource(originalName);
		}
	}

	[Obsolete($"{nameof(GameBundle)} 没有 {nameof(Parent)}。使用 {nameof(FetchAssets)} 代替。", true)]
	public new IEnumerable<IUnityObjectBase> FetchAssetsInHierarchy() => base.FetchAssetsInHierarchy();

	/// <summary>
	/// 初始化所有依赖列表。
	/// </summary>
	public new void InitializeAllDependencyLists(IDependencyProvider? dependencyProvider = null) => base.InitializeAllDependencyLists(dependencyProvider);

	/// <summary>
	/// 此捆绑包是否有任何资产集合。
	/// </summary>
	public bool HasAnyAssetCollections()
	{
		return FetchAssetCollections().Any();
	}

	/// <summary>
	/// 为此捆绑包添加一个新的处理资产集合。
	/// </summary>
	/// <param name="name">The name of the new asset collection.</param>
	/// <param name="version">The Unity version of the new asset collection.</param>
	public ProcessedAssetCollection AddNewProcessedCollection(string name, UnityVersion version)
	{
		ProcessedAssetCollection processedCollection = new ProcessedAssetCollection(this);
		processedCollection.Name = name;
		processedCollection.SetLayout(version);
		return processedCollection;
	}

	/// <summary>
	/// 为此捆绑包添加一个新的处理捆绑包。
	/// </summary>
	public ProcessedBundle AddNewProcessedBundle(string? name = null)
	{
		ProcessedBundle processedBundle = new ProcessedBundle(name);
		AddBundle(processedBundle);
		return processedBundle;
	}

	/// <summary>
	/// 获取此捆绑包中所有资产集合的最大 Unity 版本。
	/// </summary>
	public UnityVersion GetMaxUnityVersion()
	{
		return FetchAssetCollections().Select(t => t.Version).Append(UnityVersion.MinVersion).Max();
	}
}
