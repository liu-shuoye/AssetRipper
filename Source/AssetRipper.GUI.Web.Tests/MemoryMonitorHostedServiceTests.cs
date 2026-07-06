using AssetRipper.GUI.Web;

namespace AssetRipper.GUI.Web.Tests;

/// <summary>
/// Unit tests for the configuration switch on <see cref="MemoryMonitorHostedService"/>.
/// The hosted service itself is end-to-end tested by manually launching the GUI with the
/// <c>ASSETRIPPER_MEMORY_MONITOR=1</c> environment variable set and watching the log file;
/// these tests cover only the pure logic that decides whether the service should run.
/// </summary>
public class MemoryMonitorHostedServiceTests
{
	[TestCase("1", ExpectedResult = true)]
	[TestCase("true", ExpectedResult = true)]
	[TestCase("TRUE", ExpectedResult = true)]
	[TestCase("yes", ExpectedResult = true)]
	[TestCase("Yes", ExpectedResult = true)]
	[TestCase("0", ExpectedResult = false)]
	[TestCase("false", ExpectedResult = false)]
	[TestCase("no", ExpectedResult = false)]
	[TestCase("", ExpectedResult = false)]
	[TestCase("   ", ExpectedResult = false)]
	[TestCase("anything-else", ExpectedResult = false)]
	[TestCase(null, ExpectedResult = false)]
	public bool IsEnabled_RecognizesTruthyAndFalsyValues(string? rawValue)
	{
		return MemoryMonitorHostedService.IsEnabled(rawValue);
	}

	[Test]
	public void EnvironmentVariableNameMatchesSpec()
	{
		// Pin the public constant so external integrators (docs, scripts) can rely on it.
		Assert.That(MemoryMonitorHostedService.EnvironmentVariableName, Is.EqualTo("ASSETRIPPER_MEMORY_MONITOR"));
	}

	[Test]
	public void SampleIntervalIsTenSeconds()
	{
		// The spec calls for "every 10 seconds". Pin this so accidental changes to the
		// interval surface in code review.
		Assert.That(MemoryMonitorHostedService.SampleInterval, Is.EqualTo(TimeSpan.FromSeconds(10)));
	}
}
