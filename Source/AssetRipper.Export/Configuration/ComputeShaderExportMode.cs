namespace AssetRipper.Export.Configuration;

/// <summary>ComputeShader（计算着色器）的导出方式。</summary>
public enum ComputeShaderExportMode
{
	/// <summary>
	/// 导出为 Yaml .asset 资源，保留原始序列化数据（默认）。
	/// </summary>
	Yaml,
	/// <summary>
	/// 还原为可读源码 .compute 文件。
	/// 优先直通随包携带的文本源码（HLSLcc 生成的 GLSL 等）；二进制字节码载荷以注释标注。
	/// 不保证能在 Unity Editor 中编译。
	/// </summary>
	Source,
}
