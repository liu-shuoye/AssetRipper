namespace AssetRipper.SourceGenerated.Extensions.Enums.Shader.VertexFormat;

/// <summary>
/// 2019.1 and greater
/// </summary>
public enum VertexFormat2019 : byte
{
	Float = 0,
	Float16 = 1,
	UNorm8 = 2,
	SNorm8 = 3,
	UNorm16 = 4,
	SNorm16 = 5,
	UInt8 = 6,
	SInt8 = 7,
	UInt16 = 8,
	SInt16 = 9,
	UInt32 = 10,
	SInt32 = 11,
}

public static class VertexFormat2019Extension
{
	public static VertexFormat ToVertexFormat(this VertexFormat2019 _this)
	{
		// 转换结果只用于计算 stride/字节宽度（见 ChannelInfoExtensions.GetStride），
		// 通道实际写回序列化的 Format 字节是原始值，不受此映射影响。
		// VertexFormat 枚举没有 16 位整数类成员，因此按字节宽度就近归类：
		// 1 字节格式（UNorm8/SNorm8/UInt8/SInt8）归入 Byte，
		// 2 字节格式（Float16/UNorm16/SNorm16/UInt16/SInt16）归入 Float16，
		// 4 字节格式（UInt32/SInt32）归入 Int。
		return _this switch
		{
			VertexFormat2019.Float => VertexFormat.Float,
			VertexFormat2019.Float16 => VertexFormat.Float16,
			VertexFormat2019.UNorm8 => VertexFormat.Byte,
			VertexFormat2019.SNorm8 => VertexFormat.Byte,
			VertexFormat2019.UNorm16 => VertexFormat.Float16,
			VertexFormat2019.SNorm16 => VertexFormat.Float16,
			VertexFormat2019.UInt8 => VertexFormat.Byte,
			VertexFormat2019.SInt8 => VertexFormat.Byte,
			VertexFormat2019.UInt16 => VertexFormat.Float16,
			VertexFormat2019.SInt16 => VertexFormat.Float16,
			VertexFormat2019.UInt32 => VertexFormat.Int,
			VertexFormat2019.SInt32 => VertexFormat.Int,
			_ => throw new Exception(_this.ToString()),
		};
	}
}
