namespace AssetRipper.Diagnostics.Memory;

/// <summary>按类型的聚合项：对象数量与占用的字节数。</summary>
/// <param name="TypeName">类型可读名（托管类型全名，或归并后的 Unity ClassID 名）。</param>
/// <param name="Count">对象数量。</param>
/// <param name="Size">占用的字节数（口径由具体统计决定：序列化字节数 / 估算的托管堆字节数 / 整堆实测字节数）。</param>
public readonly record struct TypeAggregate(string TypeName, long Count, long Size);
