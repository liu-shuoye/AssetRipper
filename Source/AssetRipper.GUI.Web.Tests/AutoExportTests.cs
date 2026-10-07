using AssetRipper.GUI.Web;

namespace AssetRipper.GUI.Web.Tests;

/// <summary>
/// 覆盖"导出路径"的判定优先级与自动导出的目录拼接规则。
/// </summary>
/// <remarks>
/// 优先级逻辑走 <see cref="GameFileLoader.ResolveBasePathFromCandidates"/> 这个纯函数，
/// 测试因此不依赖磁盘上的设置文件，结果稳定可重复；只有接线部分才触碰全局状态。
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
	public void ConfiguredExportPathWinsOverRememberedAndImportDirectory()
	{
		string? result = GameFileLoader.ResolveBasePathFromCandidates(
			@"D:\configured", @"D:\remembered", @"D:\games\Nikki4");

		Assert.That(result, Is.EqualTo(@"D:\configured"));
	}

	[Test]
	public void RememberedExportPathWinsOverImportDirectory()
	{
		string? result = GameFileLoader.ResolveBasePathFromCandidates(
			configuredPath: null, @"D:\remembered", @"D:\games\Nikki4");

		Assert.That(result, Is.EqualTo(@"D:\remembered"));
	}

	[Test]
	public void BlankConfiguredPathIsIgnored()
	{
		// 设置页留空时表单提交的是空白串，不能把它当路径用，应继续回退
		Assert.That(
			GameFileLoader.ResolveBasePathFromCandidates("   ", null, @"D:\games\Nikki4"),
			Is.EqualTo(@"D:\games"));
	}

	[Test]
	public void FallsBackToImportParentDirectory()
	{
		// 无配置也无记忆时，取导入目录的同级目录，而不是导入目录自身
		Assert.That(
			GameFileLoader.ResolveBasePathFromCandidates(null, null, @"D:\games\Nikki4"),
			Is.EqualTo(@"D:\games"));
	}

	[Test]
	public void IgnoresTrailingSeparatorWhenFallingBackToImportParent()
	{
		// 带结尾分隔符的路径若不做规范化，GetDirectoryName 会返回自身或空串
		Assert.That(
			GameFileLoader.ResolveBasePathFromCandidates(null, null, @"D:\games\Nikki4\"),
			Is.EqualTo(@"D:\games"));
	}

	[Test]
	public void ReturnsNullWhenNothingAvailable()
	{
		// 三者皆无时无法推断位置，调用方应据此放弃导出，而不是猜一个目录
		Assert.That(GameFileLoader.ResolveBasePathFromCandidates(null, null, null), Is.Null);
		Assert.That(GameFileLoader.ResolveBasePathFromCandidates(null, null, "   "), Is.Null);
	}

	[Test]
	public void SettingsExportPathIsActuallyUsedByExportFlow()
	{
		// 只测纯函数证明不了"设置页填的值真的会被用上"，这里覆盖这段接线
		string? original = GameFileLoader.Settings.ImportSettings.AutoExportPath;
		try
		{
			GameFileLoader.Settings.ImportSettings.AutoExportPath = @"D:\configured";

			Assert.That(GameFileLoader.ResolveAutoExportBasePath(@"D:\games\Nikki4"), Is.EqualTo(@"D:\configured"));
			// 导出页预填同样以配置值为准
			Assert.That(GameFileLoader.PreferredExportPath, Is.EqualTo(@"D:\configured"));
		}
		finally
		{
			GameFileLoader.Settings.ImportSettings.AutoExportPath = original;
		}
	}

	[Test]
	public void PreferredExportPathDoesNotFallBackToImportDirectory()
	{
		// 导出页拿不到导入目录，绝不能凭空造一个"导入目录同级"填进输入框
		string? original = GameFileLoader.Settings.ImportSettings.AutoExportPath;
		try
		{
			GameFileLoader.Settings.ImportSettings.AutoExportPath = null;

			Assert.That(GameFileLoader.PreferredExportPath, Is.EqualTo(GameFileLoader.LastExport.ExportPath));
		}
		finally
		{
			GameFileLoader.Settings.ImportSettings.AutoExportPath = original;
		}
	}
}
