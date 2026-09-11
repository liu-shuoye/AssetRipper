using System.Runtime.CompilerServices;
using AssetRipper.Logging;

namespace AssetRipper.Import.Logging;

/// <summary>
/// 把 Cpp2IL 的日志输出桥接到 <see cref="Logger"/>。
/// </summary>
/// <remarks>
/// 之所以放在 Import 而不是 Logging 程序集：Logging 要保持在依赖图最底层、零依赖
/// （见 AssetRipper.Logging.csproj 的说明），而 Cpp2IL 是只有这里才需要的重依赖。
/// 用模块初始化器挂接，等价于原先把这段代码写在 <c>Logger</c> 静态构造函数里的效果：
/// 任何 Cpp2IL 日志都发生在 Import 的方法被调用之后，那时本模块的初始化器必然已经跑过。
/// </remarks>
internal static class Cpp2ILBridge
{
	[ModuleInitializer]
	internal static void Install()
	{
		Cpp2IL.Core.Logging.Logger.InfoLog += (message, source) => Logger.LogExternal(LogType.Info, message);
		// Cpp2IL 的 Warning 一直映射到 Verbose（沿用既有行为，避免改变现有日志噪音水平）
		Cpp2IL.Core.Logging.Logger.WarningLog += (message, source) => Logger.LogExternal(LogType.Verbose, message);
		Cpp2IL.Core.Logging.Logger.ErrorLog += (message, source) => Logger.LogExternal(LogType.Error, message);
		Cpp2IL.Core.Logging.Logger.VerboseLog += (message, source) => Logger.LogExternal(LogType.Verbose, message);
	}
}
