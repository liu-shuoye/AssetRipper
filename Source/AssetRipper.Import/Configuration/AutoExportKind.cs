namespace AssetRipper.Import.Configuration;

/// <summary>
/// 导入完成后的自动导出方式，决定加载结束后是否立刻导出、以及走哪条导出链路。
/// </summary>
/// <remarks>
/// 定义在导入程序集而非 GUI 层是刻意的：本枚举是 <see cref="ImportSettings"/> 的字段类型，
/// 而 ImportSettings 不能反向引用 GUI 程序集。GUI 层只做"按值分派到对应导出方法"。
/// </remarks>
public enum AutoExportKind
{
	/// <summary>不自动导出，保持导入后手动点击导出的原有行为。</summary>
	None = 0,

	/// <summary>导入完成后自动导出为 Unity 项目。</summary>
	UnityProject = 1,

	/// <summary>导入完成后自动导出主要内容。</summary>
	PrimaryContent = 2,
}
