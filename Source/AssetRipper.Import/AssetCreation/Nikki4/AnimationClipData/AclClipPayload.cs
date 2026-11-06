namespace AssetRipper.Import.AssetCreation.Nikki4;

/// <summary>
/// ACL 映射表的一行：三个值都是展开后的曲线通道索引
/// （旋转 4 通道、位移 3 通道、缩放 3 通道）。
/// </summary>
public readonly record struct AclBindingCurveIds(uint Rotation, uint Position, uint Scale);

/// <summary>
/// 从 AnimationClip_Nikki4 保留下来、供 Processing 层解压的 ACL 数据。
/// Blob 是连续的 ACL compressed_tracks 缓冲；Mapping 与 ACL track 下标一一对应。
/// </summary>
public sealed class AclClipPayload
{
	/// <summary>
	/// ACL 映射表中的“无此分量绑定”哨兵。Unity 序列化字段是 uint，
	/// 而有效曲线通道索引远小于 uint.MaxValue；0xFFFFFFFF 表示该 track 没有对应 binding。
	/// </summary>
	public const uint InvalidCurveId = uint.MaxValue;

	public byte[] Blob { get; }
	public AclBindingCurveIds[] Mapping { get; }

	public AclClipPayload(byte[] blob, AclBindingCurveIds[] mapping)
	{
		Blob = blob;
		Mapping = mapping;
	}
}
