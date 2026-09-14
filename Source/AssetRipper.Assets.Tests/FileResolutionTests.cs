using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.ResourceFiles;
using AssetRipper.IO.Files.Streams.Smart;

namespace AssetRipper.Assets.Tests;

public class FileResolutionTests
{
	[Test]
	public void CollectionResolutionWorksAnywhereInTheHierarchy()
	{
		const string name1 = "name1";
		const string name2 = "name2";
		GameBundle gameBundle = new();

		ProcessedAssetCollection collection1 = new ProcessedAssetCollection(gameBundle);
		collection1.Name = name1;

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ProcessedAssetCollection collection2 = new ProcessedAssetCollection(processedBundle);
		collection2.Name = name2;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name1), Is.EqualTo(collection1));
			Assert.That(gameBundle.ResolveCollection(name2), Is.EqualTo(collection2));
			Assert.That(processedBundle.ResolveCollection(name1), Is.EqualTo(collection1));
			Assert.That(processedBundle.ResolveCollection(name2), Is.EqualTo(collection2));
		}
	}

	[Test]
	public void CollectionResolutionIsAbleToFindTheSecondFile()
	{
		const string name1 = "name1";
		const string name2 = "name2";
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle1 = new();
		gameBundle.AddBundle(processedBundle1);

		ProcessedAssetCollection collection1 = new ProcessedAssetCollection(processedBundle1);
		collection1.Name = name1;

		ProcessedBundle processedBundle2 = new();
		gameBundle.AddBundle(processedBundle2);

		ProcessedAssetCollection collection2 = new ProcessedAssetCollection(processedBundle2);
		collection2.Name = name2;
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name1), Is.EqualTo(collection1));
			Assert.That(gameBundle.ResolveCollection(name2), Is.EqualTo(collection2));
		}
	}

	[Test]
	public void CollectionResolutionIsAbleToFindUnityDefaultResourcesWithInconsistentUnderscores()
	{
		const string name = "unity_default_resources";
		GameBundle gameBundle = new();

		ProcessedAssetCollection collection = new ProcessedAssetCollection(gameBundle);
		collection.Name = name;

		Assert.That(gameBundle.ResolveCollection("library/unity default resources"), Is.EqualTo(collection));
	}

	[TestCase("unity default resources")]
	[TestCase("unity_default_resources")]
	[TestCase("unity editor resources")]
	[TestCase("unity builtin extra")]
	[TestCase("unity_builtin_extra")]
	public void CollectionResolutionIsAbleToFindEngineResourcess(string name)
	{
		GameBundle gameBundle = new();

		ProcessedAssetCollection collection = new ProcessedAssetCollection(gameBundle);
		collection.Name = name;
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name), Is.EqualTo(collection));
			Assert.That(gameBundle.ResolveCollection($"library/{name}"), Is.EqualTo(collection));
			Assert.That(gameBundle.ResolveCollection($"resources/{name}"), Is.EqualTo(collection));
		}
	}

	[TestCase("unity default resources")]
	[TestCase("unity_default_resources")]
	[TestCase("unity editor resources")]
	[TestCase("unity builtin extra")]
	[TestCase("unity_builtin_extra")]
	public void CollectionResolutionIsAbleToFindEngineResourcesNested(string name)
	{
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ProcessedAssetCollection collection = new ProcessedAssetCollection(processedBundle);
		collection.Name = name;
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name), Is.EqualTo(collection));
			Assert.That(gameBundle.ResolveCollection($"library/{name}"), Is.EqualTo(collection));
			Assert.That(gameBundle.ResolveCollection($"resources/{name}"), Is.EqualTo(collection));
		}
	}

	[Test]
	public void ResourceResolutionWorksAnywhereInTheHierarchy()
	{
		const string name1 = "name1";
		const string name2 = "name2";
		GameBundle gameBundle = new();

		ResourceFile resource1 = CreateNewResourceFile(name1);
		gameBundle.AddResource(resource1);

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ResourceFile resource2 = CreateNewResourceFile(name2);
		processedBundle.AddResource(resource2);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveResource(name1), Is.EqualTo(resource1));
			Assert.That(gameBundle.ResolveResource(name2), Is.EqualTo(resource2));
			Assert.That(processedBundle.ResolveResource(name1), Is.EqualTo(resource1));
			Assert.That(processedBundle.ResolveResource(name2), Is.EqualTo(resource2));
		}
	}

	[Test]
	public void ResourceResolutionIsAbleToFindTheSecondFile()
	{
		const string name1 = "name1";
		const string name2 = "name2";
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle1 = new();
		gameBundle.AddBundle(processedBundle1);

		ResourceFile resource1 = CreateNewResourceFile(name1);
		processedBundle1.AddResource(resource1);

		ProcessedBundle processedBundle2 = new();
		gameBundle.AddBundle(processedBundle2);

		ResourceFile resource2 = CreateNewResourceFile(name2);
		processedBundle2.AddResource(resource2);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveResource(name1), Is.EqualTo(resource1));
			Assert.That(gameBundle.ResolveResource(name2), Is.EqualTo(resource2));
		}
	}

	[Test]
	public void ResourceResolutionIsAbleToFindAnArchiveFile()
	{
		const string name = "archive:/name1";
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ResourceFile resource = CreateNewResourceFile(name);
		processedBundle.AddResource(resource);

		Assert.That(gameBundle.ResolveResource(name), Is.EqualTo(resource));
	}

	[Test]
	public void ResourceResolutionIsAbleToFindFilesWithCapitalLetters()
	{
		const string name = "ResourceName.resS";
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ResourceFile resource = CreateNewResourceFile(name);
		processedBundle.AddResource(resource);

		Assert.That(gameBundle.ResolveResource(name), Is.EqualTo(resource));
	}

	[Test]
	public void ResourceResolutionIsAbleToFindExternalFilesFromParentBundles()
	{
		const string resourceName = "resources.resource";
		GameBundle gameBundle = new();

		ProcessedBundle processedBundle = new();
		gameBundle.AddBundle(processedBundle);

		ResourceFile resource = CreateNewResourceFile(resourceName);
		gameBundle.ResourceProvider = new SingleResourceProvider(resource);

		Assert.That(processedBundle.ResolveResource(resourceName), Is.EqualTo(resource));
	}

	[Test]
	public void ResourceResolutionIsAbleToFindExternalFilesFromGameBundles()
	{
		const string resourceName = "resources.resource";
		GameBundle gameBundle = new();

		ResourceFile resource = CreateNewResourceFile(resourceName);
		gameBundle.ResourceProvider = new SingleResourceProvider(resource);

		Assert.That(gameBundle.ResolveResource(resourceName), Is.EqualTo(resource));
	}

	/// <summary>
	/// 同名集合同时存在于自身与子 Bundle 时，必须取自身的（原实现“先查自身集合”的语义）。
	/// </summary>
	[Test]
	public void CollectionResolutionPrefersOwnCollectionsOverChildBundles()
	{
		const string name = "duplicate";
		GameBundle gameBundle = new();

		ProcessedBundle childBundle = new();
		gameBundle.AddBundle(childBundle);

		ProcessedAssetCollection childCollection = new ProcessedAssetCollection(childBundle);
		childCollection.Name = name;

		ProcessedAssetCollection rootCollection = new ProcessedAssetCollection(gameBundle);
		rootCollection.Name = name;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name), Is.EqualTo(rootCollection));
			Assert.That(childBundle.ResolveCollection(name), Is.EqualTo(childCollection));
		}
	}

	/// <summary>
	/// 同名集合分散在多个兄弟 Bundle 时，取添加顺序最靠前的那个（资产 Bundle 变体场景）。
	/// </summary>
	[Test]
	public void CollectionResolutionAmongSiblingBundlesReturnsTheFirstMatch()
	{
		const string name = "duplicate";
		GameBundle gameBundle = new();

		ProcessedBundle firstBundle = new();
		gameBundle.AddBundle(firstBundle);
		ProcessedAssetCollection firstCollection = new ProcessedAssetCollection(firstBundle);
		firstCollection.Name = name;

		ProcessedBundle secondBundle = new();
		gameBundle.AddBundle(secondBundle);
		ProcessedAssetCollection secondCollection = new ProcessedAssetCollection(secondBundle);
		secondCollection.Name = name;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection(name), Is.EqualTo(firstCollection));
			// 从第二个子 Bundle 发起解析时自身集合优先
			Assert.That(secondBundle.ResolveCollection(name), Is.EqualTo(secondCollection));
		}
	}

	/// <summary>
	/// 解析只向下探一层：孙 Bundle 的集合对祖父可见性依赖于“父直接持有”，对根（隔两层）不可见。
	/// </summary>
	[Test]
	public void CollectionResolutionOnlyLooksOneLevelDownIntoChildBundles()
	{
		const string name = "deep";
		GameBundle gameBundle = new();

		ProcessedBundle parentBundle = new();
		gameBundle.AddBundle(parentBundle);

		ProcessedBundle grandchildBundle = new();
		parentBundle.AddBundle(grandchildBundle);

		ProcessedAssetCollection collection = new ProcessedAssetCollection(grandchildBundle);
		collection.Name = name;

		using (Assert.EnterMultipleScope())
		{
			Assert.That(grandchildBundle.ResolveCollection(name), Is.EqualTo(collection));
			Assert.That(parentBundle.ResolveCollection(name), Is.EqualTo(collection));
			Assert.That(gameBundle.ResolveCollection(name), Is.Null);
		}
	}

	/// <summary>
	/// 同名资源分散在多个兄弟 Bundle 时，取添加顺序最靠前的那个。
	/// </summary>
	[Test]
	public void ResourceResolutionPrefersOwnResourcesAndFirstSibling()
	{
		const string name = "duplicate.resS";
		GameBundle gameBundle = new();

		ProcessedBundle firstBundle = new();
		gameBundle.AddBundle(firstBundle);
		ResourceFile firstResource = CreateNewResourceFile(name);
		firstBundle.AddResource(firstResource);

		ProcessedBundle secondBundle = new();
		gameBundle.AddBundle(secondBundle);
		ResourceFile secondResource = CreateNewResourceFile(name);
		secondBundle.AddResource(secondResource);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveResource(name), Is.EqualTo(firstResource));
			Assert.That(secondBundle.ResolveResource(name), Is.EqualTo(secondResource));
		}
	}

	/// <summary>
	/// 名称索引建立之后再追加集合/资源时，索引必须失效并重建——否则后加入的条目会解析不到。
	/// </summary>
	/// <remarks>
	/// 条目数超过线性扫描阈值才会真正建立字典，因此这里刻意造出足够多的条目。
	/// </remarks>
	[Test]
	public void NameIndexIsRebuiltWhenEntriesAreAddedLater()
	{
		const int existingCount = 10;
		GameBundle gameBundle = new();

		ProcessedBundle bundle = new();
		gameBundle.AddBundle(bundle);

		for (int i = 0; i < existingCount; i++)
		{
			ProcessedAssetCollection existing = new ProcessedAssetCollection(bundle);
			existing.Name = $"existing{i}";
			bundle.AddResource(CreateNewResourceFile($"existing{i}.resS"));
		}

		// 先解析一次，确保索引已建立
		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection("existing0"), Is.Not.Null);
			Assert.That(gameBundle.ResolveResource("existing0.resS"), Is.Not.Null);
		}

		ProcessedAssetCollection lateCollection = new ProcessedAssetCollection(bundle);
		lateCollection.Name = "late";
		ResourceFile lateResource = CreateNewResourceFile("late.resS");
		bundle.AddResource(lateResource);

		using (Assert.EnterMultipleScope())
		{
			Assert.That(gameBundle.ResolveCollection("late"), Is.EqualTo(lateCollection));
			Assert.That(gameBundle.ResolveResource("late.resS"), Is.EqualTo(lateResource));
		}
	}

	private sealed record class SingleResourceProvider(ResourceFile Resource) : IResourceProvider
	{
		public ResourceFile? FindResource(string identifier)
		{
			string fixedName = SpecialFileNames.FixResourcePath(identifier);
			return fixedName == Resource.NameFixed ? Resource : null;
		}
	}

	private static ResourceFile CreateNewResourceFile(string name) => new ResourceFile(SmartStream.CreateMemory(), name, name);
}
