using AssetRipper.GUI.Web;
using AssetRipper.Import.Configuration;

namespace AssetRipper.GUI.Web.Tests;

/// <summary>
/// 覆盖"导入完成后自动导出"的路径推断与开关语义。
/// </summary>
/// <remarks>
/// 这些用例只触碰纯计算部分（路径拼接、记忆回退），不触发真实文件写入，
/// 因此测试可以安全地在 CI 中重复运行。真实的导出链路由导出页手动流程共用，不在此重复覆盖。
/// </remarks>
public class AutoExportTests
{
	[Test]
	public void AppendTimestampedSubfolderCreatesNestedDirectoryForAutoExport()
	{
		string result = GameFileLoader.AppendTimestampedSubfolder(@"D:\out", append: true);

		// 自动导出必须落在基目录的子目录内，否则会与用户既有文件混在一起
		Assert.That(result, Does.StartWith(@"D:\out"));
		Assert.That(result, Is.Not.EqualTo(@"D:\out"));
		Assert.That(Path.GetFileName(result), Does.StartWith("AssetRipper_export_"));
	}

	[Test]
	public void AppendTimestampedSubfolderReturnsBasePathWhenDisabled()
	{
		// 不追加子文件夹时应原样返回，保证与手动导出填写基础路径时行为一致
		Assert.That(GameFileLoader.AppendTimestampedSubfolder(@"D:\out", append: false), Is.EqualTo(@"D:\out"));
	}

	[Test]
	public void ResolveAutoExportBasePathFallsBackToImportParentDirectory()
	{
		// 未记住导出路径时，应取导入目录的同级目录，而不是导入目录自身
		string? resolved = GameFileLoader.ResolveAutoExportBasePath(@"D:\games\Nikki4");

		Assert.That(resolved, Is.EqualTo(@"D:\games"));
	}

	[Test]
	public void ResolveAutoExportBasePathIgnoresTrailingSeparator()
	{
		// 带结尾分隔符的路径若不做规范化，GetDirectoryName 会返回自身或空串
		Assert.That(GameFileLoader.ResolveAutoExportBasePath(@"D:\games\Nikki4\"), Is.EqualTo(@"D:\games"));
	}

	[Test]
	public void ResolveAutoExportBasePathReturnsNullWithoutAnySource()
	{
		// 既无记忆导出路径、又无导入目录时无法推断位置，调用方应据此放弃自动导出
		Assert.That(GameFileLoader.ResolveAutoExportBasePath(null), Is.Null);
		Assert.That(GameFileLoader.ResolveAutoExportBasePath("   "), Is.Null);
	}
}
