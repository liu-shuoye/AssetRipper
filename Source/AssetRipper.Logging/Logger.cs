using System.Text;

namespace AssetRipper.Logging;

/// <summary>
/// 全局日志入口。本程序集刻意零依赖，因此 AssetRipper 最底层的程序集（如 AssetRipper.Assets）也能直接使用。
/// Cpp2IL 日志桥接与内存诊断等"上层才知道怎么办"的内容，分别放在 <c>AssetRipper.Import</c> 里。
/// </summary>
public static class Logger
{
	private static readonly object _lock = new();
	private static readonly List<ILogger> loggers = new();
	public static bool AllowVerbose { get; set; }

	/// <summary>
	/// 是否在每条日志前附加时间戳前缀（如 "[12:34:56.789] "）。默认开启。
	/// 关闭后日志保持原有格式，便于与旧日志对比或对接只认纯文本的下游。
	/// </summary>
	public static bool IncludeTimestamp { get; set; } = true;

	/// <summary>
	/// 当前正在分发的日志行对应的时间戳前缀。
	/// 在 <see cref="Log(LogType, LogCategory, string)"/> 的锁内赋值，供各 <see cref="ILogger"/> 读取，
	/// 保证同一条日志在控制台与文件里打出的时间一致，且只计算一次。
	/// </summary>
	internal static string CurrentTimestamp { get; private set; } = string.Empty;

	public static event Action<string, object?> OnStatusChanged = (_, _) => { };

	/// <summary>
	/// 供上层把外部日志系统的输出桥接进来（见 AssetRipper.Import 的 Cpp2ILBridge）：
	/// 统一裁掉首尾空白并打上 <see cref="LogCategory.Cpp2IL"/> 分类标记。
	/// </summary>
	/// <param name="logType">映射后的日志级别。</param>
	/// <param name="message">外部日志系统的原始消息。</param>
	public static void LogExternal(LogType logType, string message) => Log(logType, LogCategory.Cpp2IL, message.Trim());

	public static void Log(LogType type, LogCategory category, string message)
	{
		if (AssetRipperRuntimeInformation.Build.Debug && type == LogType.Debug)
		{
			return;
		}

		if (type == LogType.Verbose && !AllowVerbose)
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(message);

		// 在锁外先算好时间戳，锁内只做一次赋值，避免每条日志重复取时间
		string timestamp = IncludeTimestamp ? $"[{DateTime.Now:HH:mm:ss.fff}] " : string.Empty;
		lock (_lock)
		{
			CurrentTimestamp = timestamp;
			foreach (ILogger instance in loggers)
			{
				instance?.Log(type, category, message);
			}
		}
	}

	public static void Log(LogType type, LogCategory category, string[] messages)
	{
		ArgumentNullException.ThrowIfNull(messages);

		foreach (string message in messages)
		{
			Log(type, category, message);
		}
	}

	public static void BlankLine() => BlankLine(1);
	public static void BlankLine(int numLines)
	{
		foreach (ILogger instance in loggers)
		{
			instance?.BlankLine(numLines);
		}
	}

	public static void Info(string message) => Log(LogType.Info, LogCategory.None, message);
	public static void Info(LogCategory category, string message) => Log(LogType.Info, category, message);
	public static void Warning(string message) => Log(LogType.Warning, LogCategory.None, message);
	public static void Warning(LogCategory category, string message) => Log(LogType.Warning, category, message);
	public static void Error(string message) => Log(LogType.Error, LogCategory.None, message);
	public static void Error(LogCategory category, string message) => Log(LogType.Error, category, message);
	public static void Error(Exception e) => Error(LogCategory.None, null, e);
	public static void Error(string message, Exception e) => Error(LogCategory.None, message, e);
	public static void Error(LogCategory category, string? message, Exception e)
	{
		StringBuilder sb = new();
		if (message != null)
		{
			sb.AppendLine(message);
		}

		sb.AppendLine(e.ToString());
		Log(LogType.Error, category, sb.ToString());
	}
	public static void Verbose(string message) => Log(LogType.Verbose, LogCategory.None, message);
	public static void Verbose(LogCategory category, string message) => Log(LogType.Verbose, category, message);
	public static void Debug(string message) => Log(LogType.Debug, LogCategory.None, message);
	public static void Debug(LogCategory category, string message) => Log(LogType.Debug, category, message);

	private static void ErrorIfBigEndian()
	{
		if (!BitConverter.IsLittleEndian)
		{
			Error("Big Endian processors are not supported!");
		}
	}

	public static void LogSystemInformation(string programName)
	{
		Log(LogType.Info, LogCategory.System, programName);
		Log(LogType.Info, LogCategory.System, $"System Version: {AssetRipperRuntimeInformation.OS.Version}");
		Log(LogType.Info, LogCategory.System, $"Operating System: {AssetRipperRuntimeInformation.OS.Name} {AssetRipperRuntimeInformation.ProcessArchitecture}");
		ErrorIfBigEndian();
		Log(LogType.Info, LogCategory.System, $"AssetRipper Version: {AssetRipperRuntimeInformation.Build.Version}");
		Log(LogType.Info, LogCategory.System, $"AssetRipper Build Type: {AssetRipperRuntimeInformation.Build.Configuration} {AssetRipperRuntimeInformation.Build.Type}");
		Log(LogType.Info, LogCategory.System, $"UTC Current Time: {AssetRipperRuntimeInformation.CurrentTime}");
		Log(LogType.Info, LogCategory.System, $"UTC Compile Time: {AssetRipperRuntimeInformation.CompileTime}");
	}
	public static void Add(ILogger logger) => loggers.Add(logger);

	/// <summary>
	/// 输出当前内存状态，用于定位哪个阶段内存上涨最多。
	/// 只做最基础的快照（强制 GC 后读取托管堆与工作集），更详细的按类型拆解在
	/// <c>AssetRipper.Diagnostics.Memory.MemoryDiagnostics</c> 里——那部分需要资产对象模型，放不在本程序集。
	/// </summary>
	/// <param name="stage">当前阶段标识。</param>
	/// <returns>强制回收后的托管堆字节数，便于调用方自行比较涨幅。</returns>
	public static long LogMemoryDiagnostics(string stage)
	{
		// 强制 GC 后再统计，排除已可回收但未回收的对象干扰
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long managedMemory = GC.GetTotalMemory(false);
		long workingSet = Environment.WorkingSet;
		Info(LogCategory.Processing, $"[内存诊断] {stage}: 托管: {managedMemory / 1024.0 / 1024.0:F1} MB | 工作集: {workingSet / 1024.0 / 1024.0:F1} MB");
		return managedMemory;
	}

	public static void Remove(ILogger logger) => loggers.Remove(logger);

	public static void Clear() => loggers.Clear();

	public static void SendStatusChange(string newStatus, object? context = null) => OnStatusChanged(newStatus, context);
}
