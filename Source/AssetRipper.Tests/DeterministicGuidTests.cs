using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Configuration;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.IO.Files;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Tests;

/// <summary>
/// 确定性 GUID（分批导出保持 GUID 稳定）功能的单元测试。
/// </summary>
internal class DeterministicGuidTests
{
	/// <summary>
	/// 同一资源（同源文件、同名、同 PathID）在任何时候计算 GUID 都必须得到相同结果。
	/// </summary>
	[Test]
	public void DeterministicGuidCalculator_SameAsset_ReturnsSameGuid()
	{
		ProcessedAssetCollection first = new GameBundle().AddNewProcessedCollection("SameBatch", UnityVersion.V_2022);
		ProcessedAssetCollection second = new GameBundle().AddNewProcessedCollection("SameBatch", UnityVersion.V_2022);

		IMonoBehaviour asset1 = first.CreateMonoBehaviour();
		asset1.Name = "Shared";
		IMonoBehaviour asset2 = second.CreateMonoBehaviour();
		asset2.Name = "Shared";

		UnityGuid guid1 = DeterministicGuidCalculator.Calculate(asset1);
		UnityGuid guid2 = DeterministicGuidCalculator.Calculate(asset2);

		// 两个"批次"的集合名、资源名、PathID 都一致 → 计算键一致 → GUID 一致。
		Assert.Multiple(() =>
		{
			Assert.That(guid2, Is.EqualTo(guid1));
			Assert.That(guid1.IsZero, Is.False);
		});
	}

	/// <summary>
	/// 不同资源（PathID 不同）即使目录与名称完全相同，GUID 也不得碰撞。
	/// </summary>
	[Test]
	public void DeterministicGuidCalculator_DifferentAssets_ReturnDifferentGuids()
	{
		ProcessedAssetCollection collection = new GameBundle().AddNewProcessedCollection("CollideTest", UnityVersion.V_2022);

		IMonoBehaviour first = collection.CreateMonoBehaviour();
		first.Name = "SameName";
		IMonoBehaviour second = collection.CreateMonoBehaviour();
		second.Name = "SameName";

		Assert.That(DeterministicGuidCalculator.Calculate(first), Is.Not.EqualTo(DeterministicGuidCalculator.Calculate(second)));
	}

	/// <summary>
	/// 默认（不启用确定性）时 GUID 仍是随机值：两个内容完全相同的集合 GUID 不同。
	/// </summary>
	[Test]
	public void AssetExportCollection_DefaultGuid_IsRandom()
	{
		ProcessedAssetCollection collection = new GameBundle().AddNewProcessedCollection("RandomTest", UnityVersion.V_2022);

		IMonoBehaviour first = collection.CreateMonoBehaviour();
		first.Name = "SameName";
		IMonoBehaviour second = collection.CreateMonoBehaviour();
		second.Name = "SameName";

		IExportCollection firstCollection = CreateCollection(first);
		IExportCollection secondCollection = CreateCollection(second);

		Assert.That(GetGuid(firstCollection), Is.Not.EqualTo(GetGuid(secondCollection)));
	}

	/// <summary>
	/// 调用 <see cref="ExportCollection.UseDeterministicGuid"/> 后，GUID 变为计算器的确定性结果，
	/// 且分属两个批次的同标识资源 GUID 一致。
	/// </summary>
	[Test]
	public void AssetExportCollection_UseDeterministicGuid_ProducesStableGuid()
	{
		ProcessedAssetCollection first = new GameBundle().AddNewProcessedCollection("SameBatch", UnityVersion.V_2022);
		ProcessedAssetCollection second = new GameBundle().AddNewProcessedCollection("SameBatch", UnityVersion.V_2022);

		IMonoBehaviour asset1 = first.CreateMonoBehaviour();
		asset1.Name = "Hero";
		asset1.OriginalName = "Hero";
		IMonoBehaviour asset2 = second.CreateMonoBehaviour();
		asset2.Name = "Hero";
		asset2.OriginalName = "Hero";

		IExportCollection collection1 = CreateCollection(asset1, useDeterministicGuid: true);
		IExportCollection collection2 = CreateCollection(asset2, useDeterministicGuid: true);

		UnityGuid guid1 = GetGuid(collection1);
		UnityGuid guid2 = GetGuid(collection2);

		Assert.Multiple(() =>
		{
			Assert.That(guid1, Is.EqualTo(DeterministicGuidCalculator.Calculate(asset1)));
			Assert.That(guid2, Is.EqualTo(guid1));
		});
	}

	/// <summary>
	/// 端到端：开启开关后，两次独立导出的同名资源 .meta 中 guid 一致；
	/// 关闭（默认）时 guid 为随机值，两次不一致。
	/// </summary>
	[Test]
	public void Export_MetaGuid_IsStableWhenEnabled_AndRandomWhenDisabled()
	{
		const string metaPath = "/output/ExportedProject/Assets/MonoBehaviour/Stable.asset.meta";

		// 开启确定性 GUID：两次导出应得到相同 guid。
		string guidOn = ExtractGuid(ExportToNewFileSystem(enableDeterministicGuids: true, metaPath));
		string guidOnSecondRun = ExtractGuid(ExportToNewFileSystem(enableDeterministicGuids: true, metaPath));

		// 默认随机：两次导出应得到不同 guid。
		string guidOff = ExtractGuid(ExportToNewFileSystem(enableDeterministicGuids: false, metaPath));
		string guidOffSecondRun = ExtractGuid(ExportToNewFileSystem(enableDeterministicGuids: false, metaPath));

		Assert.Multiple(() =>
		{
			Assert.That(guidOnSecondRun, Is.EqualTo(guidOn));
			Assert.That(guidOffSecondRun, Is.Not.EqualTo(guidOff));
		});
	}

	/// <summary>
	/// 构建一个包含同名资源的包并导出（可选开启确定性 GUID），返回指定 .meta 文件的内容。
	/// </summary>
	private static string ExportToNewFileSystem(bool enableDeterministicGuids, string metaPath)
	{
		GameBundle gameBundle = new();
		ProcessedAssetCollection collection = gameBundle.AddNewProcessedCollection("StableTest", UnityVersion.V_2022);
		IMonoBehaviour asset = collection.CreateMonoBehaviour();
		asset.Name = "Stable";

		VirtualFileSystem fileSystem = new();
		FullConfiguration configuration = new();
		configuration.ProcessingSettings = new ProcessingSettings { EnableDeterministicGuids = enableDeterministicGuids };
		configuration.SetProjectSettings(collection.Version);

		GameData gameData = new(gameBundle, collection.Version, new BaseManager(_ => { }), null);
		new ExportHandler(configuration).Export(gameData, "output", fileSystem);

		Assert.That(fileSystem.File.Exists(metaPath), Is.True);
		return fileSystem.File.ReadAllText(metaPath);
	}

	/// <summary>
	/// 从 .meta 文本中提取 "guid: xxxxx..." 一行。
	/// </summary>
	private static string ExtractGuid(string metaText)
	{
		string line = metaText.Split('\n').First(l => l.TrimStart().StartsWith("guid:"));
		return line.Trim();
	}

	/// <summary>
	/// 为资源创建导出集合，可选在创建后启用确定性 GUID。
	/// </summary>
	private static IExportCollection CreateCollection(IUnityObjectBase asset, bool useDeterministicGuid = false)
	{
		ScriptableObjectExporter exporter = new();
		Assert.That(exporter.TryCreateCollection(asset, out IExportCollection? collection), Is.True);
		if (useDeterministicGuid && collection is ExportCollection exportCollection)
		{
			exportCollection.UseDeterministicGuid();
		}

		return collection!;
	}

	private static UnityGuid GetGuid(IExportCollection collection)
	{
		Assert.That(collection, Is.InstanceOf<ExportCollection>());
		return ((ExportCollection)collection).GUID;
	}
}