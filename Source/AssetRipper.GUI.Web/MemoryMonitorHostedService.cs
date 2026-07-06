using AssetRipper.Import.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;

namespace AssetRipper.GUI.Web;

/// <summary>
/// A background service that periodically logs AssetRipper's memory usage while a game is
/// loaded. Disabled by default; enabled by setting the
/// <c>ASSETRIPPER_MEMORY_MONITOR=1</c> environment variable before launch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose.</b> This service supports the memory-optimization spec (Task 9.2). It samples
/// three signals every 10 seconds and writes them to the standard
/// <see cref="AssetRipper.Import.Logging.Logger"/>:
/// </para>
/// <list type="bullet">
///   <item><c>GC.GetTotalMemory(false)</c> — the current size of the managed heap (live objects
///       only, no full GC is forced).</item>
///   <item><c>Process.WorkingSet64</c> — the OS-reported working set, which includes unmapped
///       pages and is closer to what the user actually sees in Task Manager.</item>
///   <item><c>GameBundle.Collections.Count</c> — only available when a game is loaded
///       (<c>GameFileLoader.IsLoaded</c>). When no game is loaded, the field is reported as
///       <c>N/A</c>.</item>
/// </list>
/// <para>
/// <b>Configuration.</b> The service is opt-in via the <c>ASSETRIPPER_MEMORY_MONITOR</c>
/// environment variable. The accepted truthy values are <c>1</c>, <c>true</c>, <c>yes</c>
/// (case-insensitive); any other value (or unset) disables the service. This avoids the
/// per-10-second logging cost in normal use.
/// </para>
/// <para>
/// <b>Logging target.</b> The service uses <see cref="Logger.Info(LogCategory, string)"/> so
/// the output is captured by the existing <c>ConsoleLogger</c> and <c>FileLogger</c>
/// instances registered by <see cref="WebApplicationLauncher.Launch(int, bool, bool, string?)"/>.
/// No additional file writer is added.
/// </para>
/// <para>
/// <b>Shutdown.</b> The service respects <see cref="IHostedService.StopAsync"/> via a
/// <see cref="CancellationTokenSource"/> that is canceled on stop. The 10-second interval
/// between samples is implemented with <c>Task.Delay</c>, so cancellation is prompt.
/// </para>
/// </remarks>
internal sealed class MemoryMonitorHostedService : IHostedService, IDisposable
{
	/// <summary>
	/// The environment variable used to enable this service. Set to <c>1</c>, <c>true</c>, or
	/// <c>yes</c> (case-insensitive) to enable. Unset or any other value disables the service.
	/// </summary>
	public const string EnvironmentVariableName = "ASSETRIPPER_MEMORY_MONITOR";

	/// <summary>
	/// The interval between memory samples. Defaults to 10 seconds.
	/// </summary>
	public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(10);

	private readonly CancellationTokenSource _cts = new();
	private Task? _loopTask;

	/// <summary>
	/// Returns <see langword="true"/> when the <c>ASSETRIPPER_MEMORY_MONITOR</c> environment
	/// variable is set to a truthy value (<c>1</c>, <c>true</c>, <c>yes</c>,
	/// case-insensitive).
	/// </summary>
	/// <param name="environment">
	/// Optional environment override (used for testing). When <see langword="null"/>,
	/// <see cref="Environment.GetEnvironmentVariable(string)"/> is consulted.
	/// </param>
	public static bool IsEnabled(string? environment = null)
	{
		string? raw = environment ?? Environment.GetEnvironmentVariable(EnvironmentVariableName);
		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}
		return raw.Trim().Equals("1", StringComparison.OrdinalIgnoreCase)
			|| raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
			|| raw.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		// The DI container only registers this service when IsEnabled() returns true, but
		// we double-check here so misuse does not silently start the background loop.
		if (!IsEnabled())
		{
			return Task.CompletedTask;
		}

		_loopTask = Task.Run(async () =>
		{
			try
			{
				await MonitorLoopAsync(_cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected on shutdown; swallow silently.
			}
			catch (Exception ex)
			{
				// Log but do not rethrow so the host process is not brought down by the
				// monitor. Subsequent failures will not be retried (the task is now faulted).
				Logger.Error(LogCategory.System, $"Memory monitor loop crashed: {ex}");
			}
		}, cancellationToken);

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_loopTask is null)
		{
			return;
		}

		_cts.Cancel();
		try
		{
			// Wait for the loop to observe the cancellation and exit. We use a hard cap so
			// that a stuck sample does not block shutdown indefinitely.
			Task completedTask = await Task.WhenAny(_loopTask, Task.Delay(TimeSpan.FromSeconds(15), cancellationToken));
			if (completedTask != _loopTask)
			{
				Logger.Warning(LogCategory.System, "Memory monitor did not stop within 15 seconds; continuing shutdown.");
			}
		}
		catch (OperationCanceledException)
		{
			// Host is shutting down; ignore.
		}
	}

	/// <summary>
	/// The main monitor loop. Samples memory every <see cref="SampleInterval"/> until the
	/// <paramref name="cancellationToken"/> is canceled.
	/// </summary>
	private static async Task MonitorLoopAsync(CancellationToken cancellationToken)
	{
		// Log once at startup so the operator sees that the monitor is active even when no
		// game has been loaded yet.
		Logger.Info(LogCategory.System, $"Memory monitor enabled (sampling every {SampleInterval.TotalSeconds:F0}s).");

		while (!cancellationToken.IsCancellationRequested)
		{
			LogMemorySnapshot();
			try
			{
				await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}

		Logger.Info(LogCategory.System, "Memory monitor stopping.");
	}

	/// <summary>
	/// Logs a single memory snapshot. Reads <see cref="GC.GetTotalMemory(bool)"/>,
	/// <see cref="Process.WorkingSet64"/>, and — if a game is loaded —
	/// <see cref="GameBundle.Collections"/> count. The collection count is read lazily so the
	/// monitor does not interfere with the loader by accessing <c>GameBundle</c> before it
	/// is initialized.
	/// </summary>
	private static void LogMemorySnapshot()
	{
		long managedMemory = GC.GetTotalMemory(forceFullCollection: false);

		long workingSet;
		try
		{
			// Process.GetCurrentProcess() can throw on some platforms or when the process
			// has exited; fall back to 0 in that case so the rest of the snapshot is still logged.
			using Process process = Process.GetCurrentProcess();
			workingSet = process.WorkingSet64;
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.System, $"Memory monitor could not read Process.WorkingSet64: {ex.Message}");
			workingSet = -1;
		}

		int collectionCount;
		bool isLoaded;
		try
		{
			isLoaded = GameFileLoader.IsLoaded;
			collectionCount = isLoaded ? GameFileLoader.GameBundle.Collections.Count : 0;
		}
		catch (Exception ex)
		{
			// GameFileLoader.GameBundle getter throws when IsLoaded is false (via
			// MemberNotNullWhen). We catch any other reflection-style failures here too.
			Logger.Warning(LogCategory.System, $"Memory monitor could not read GameBundle state: {ex.Message}");
			isLoaded = false;
			collectionCount = 0;
		}

		string loadedField = isLoaded ? collectionCount.ToString("N0") : "N/A";
		Logger.Info(LogCategory.System,
			$"Memory monitor: ManagedHeap={managedMemory:N0} bytes, WorkingSet={workingSet:N0} bytes, GameBundle.Collections.Count={loadedField}");
	}

	public void Dispose()
	{
		_cts.Dispose();
	}
}
