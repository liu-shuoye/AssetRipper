namespace AssetRipper.Import.Configuration;

/// <summary>
/// 内存拆解口径，决定内存诊断输出资源占用拆解日志时的统计方式。
/// </summary>
public enum MemoryBreakdownMode
{
	/// <summary>已反序列化对象的序列化字节数（廉价、不额外反射）。</summary>
	Serialized = 0,

	/// <summary>反射估算真实托管堆字节数（较重，需遍历对象图）。</summary>
	Live = 1,

	/// <summary>用 ClrMD 抓取整个进程 GC 堆快照并按托管类型聚合（最重，不依赖资产集合）。</summary>
	ClrMd = 2,
}
