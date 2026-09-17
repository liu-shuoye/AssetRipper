using AssetRipper.Logging;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace AssetRipper.GUI.Web;

/// <summary>
/// P1-3 GC 配置评估的支撑：提供"运行时 LatencyMode 环境变量开关 + 启动期 GC 配置诊断"。
/// Server/Workstation GC、ConserveMemory、RetainVM、HeapHardLimit 都属于进程启动前配置
/// （runtimeconfig.json / DOTNET_* 环境变量），代码无法在启动后修改，因此这里只负责两件事：
///   1. 在 Main 之前最早执行点，按 <see cref="LatencyModeEnvVar"/> 应用 GCSettings.LatencyMode（运行时可改的唯一 GC 旋钮）；
///   2. 启动日志里打印当前生效的 GC 配置与已设置的 DOTNET_* 环境变量，保证"单变量对比"时日志能证明当前跑的配置。
/// </summary>
internal static class GcConfiguration
{
	/// <summary>运行时 GC 延迟模式的环境变量名，取值 Batch / Interactive / SustainedLowLatency。</summary>
	private const string LatencyModeEnvVar = "RURI_GC_LATENCY_MODE";

	/// <summary>
	/// 在 Main 之前最早执行点应用 LatencyMode：此时还没有任何业务代码运行，
	/// 避免中途切换触发阻塞式 GC。非法值静默回退 Interactive（Logger 尚未初始化，无法留日志）。
	/// 默认不设置（Interactive），即行为与改动前完全一致，实验时才通过环境变量开启。
	/// </summary>
	[ModuleInitializer]
	internal static void ApplyLatencyModeFromEnvironment()
	{
		string? mode = Environment.GetEnvironmentVariable(LatencyModeEnvVar);
		if (string.IsNullOrWhiteSpace(mode))
		{
			return;
		}

		GCSettings.LatencyMode = mode.Trim() switch
		{
			"Batch" or "batch" => GCLatencyMode.Batch,
			"SustainedLowLatency" or "sustainedLowLatency" or "LowLatency" or "lowLatency" => GCLatencyMode.SustainedLowLatency,
			_ => GCLatencyMode.Interactive,
		};
	}

	/// <summary>
	/// 打印当前生效的 GC 配置。放在 Logger 初始化完成后调用，
	/// 输出与本机内存曲线（1.2 阶段表）对照时用于确认"这次跑的是什么 GC 配置"。
	/// </summary>
	internal static void LogCurrentConfiguration()
	{
		Logger.Info(LogCategory.System,
			$"GC 配置：服务器GC {(GCSettings.IsServerGC ? "是" : "否")}，延迟模式 {GCSettings.LatencyMode}，LOH 压缩模式 {GCSettings.LargeObjectHeapCompactionMode}");

		// 启动前配置项无法在运行时读取生效值，只能回显环境变量本身；
		// 它们与 runtimeconfig 的优先级是"环境变量覆盖 runtimeconfig"，因此可直接用 DOTNET_* 做单变量对比。
		string[] configurableEnvVars =
		[
			"DOTNET_gcServer", "DOTNET_GC_Concurrent", "DOTNET_GC_ConserveMemory",
			"DOTNET_GC_RetainVM", "DOTNET_GC_HeapHardLimit", "DOTNET_GC_HeapHardLimitPercent",
			LatencyModeEnvVar,
		];
		foreach (string name in configurableEnvVars)
		{
			string? value = Environment.GetEnvironmentVariable(name);
			if (!string.IsNullOrEmpty(value))
			{
				Logger.Info(LogCategory.System, $"GC 环境变量 {name}={value}");
			}
		}
	}
}
