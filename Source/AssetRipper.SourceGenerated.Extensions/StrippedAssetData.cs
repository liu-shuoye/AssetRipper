using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using System;

namespace AssetRipper.SourceGenerated.Extensions;

/// <summary>
/// 占位模式「回读真实数据 + 用完即弃」的恢复句柄。
/// 句柄在获取时只回补被剥离的内嵌数据；导出器用它包住导出动作，
/// 离开 using 块时 <see cref="Dispose"/> 把恢复过的字段再次清空，数据不驻留内存。
/// 未发生任何恢复时句柄为空操作（None），Dispose 不影响对象。
/// </summary>
public sealed class AssetDataRestoreHandle : IDisposable
{
	private readonly Action? _clearAction;

	internal AssetDataRestoreHandle(Action? clearAction) => _clearAction = clearAction;

	/// <summary>免恢复句柄：Dispose 不产生任何副作用。</summary>
	public static AssetDataRestoreHandle None { get; } = new(null);

	public void Dispose() => _clearAction?.Invoke();
}

/// <summary>
/// 占位模式的数据剥离与恢复原语。
///
/// 剥离只清内嵌大数组（加载期内存大头），保留 path/offset/size 等流式引用——
/// 导出阶段 GetImageData / GetAudioData 等访问器据此懒读 .resS/.resource 即得到真实数据；
/// 完全内嵌（无流）的资产在加载期被剥离后无法原地回读，
/// 由各 <c>TryAcquire</c> 方法按 <see cref="AssetCollection.MaterializeAssetOnly"/> 重建对象补回。
/// </summary>
public static class StrippedAssetData
{
	/// <summary>赋空 Texture2D 内嵌像素数据（保留流式引用）。</summary>
	public static void ClearEmbeddedData(this ITexture2D texture) => texture.ImageData_C28 = [];

	/// <summary>赋空 Mesh 内嵌顶点与索引数据（保留流式引用）。</summary>
	public static void ClearEmbeddedData(this IMesh mesh)
	{
		mesh.VertexData.Data = [];
		mesh.IndexBuffer = [];
	}

	/// <summary>赋空 AudioClip 内嵌音频数据（保留流式引用）。</summary>
	public static void ClearEmbeddedData(this IAudioClip audioClip)
	{
		if (audioClip.Has_AudioData())
		{
			audioClip.AudioData = [];
		}
	}

	/// <summary>
	/// 导出前回读 Texture2D 真实数据并取得用完即弃句柄。
	/// 内嵌数据尚在或保留了流式引用时免恢复；完全内嵌且被剥离时重建对象补回像素数据。
	/// </summary>
	public static AssetDataRestoreHandle TryAcquire(ITexture2D texture)
	{
		// 内嵌数据尚在，或保留了流式引用（导出时 GetImageData 会懒读 .resS），均无需恢复
		if (texture.ImageData_C28.Length != 0 || (texture.StreamData_C28 is not null && texture.StreamData_C28.IsSet()))
		{
			return AssetDataRestoreHandle.None;
		}
		// 完全内嵌且已被剥离：重建对象补回像素数据，Dispose 时再次清空
		if (texture.Collection.MaterializeAssetOnly(texture.PathID) is ITexture2D materialized && materialized.ImageData_C28.Length != 0)
		{
			texture.ImageData_C28 = materialized.ImageData_C28;
			return new AssetDataRestoreHandle(texture.ClearEmbeddedData);
		}
		return AssetDataRestoreHandle.None;
	}

	/// <summary>
	/// 导出前回读 Mesh 真实数据并取得用完即弃句柄。
	/// 索引缓冲总内嵌在序列化体里，非空即数据完整；被剥离时重建对象补回索引（及内嵌网格的顶点数据），
	/// 流式网格的顶点本在 .resS 中（重建对象里也为空），由导出路径懒读。
	/// </summary>
	public static AssetDataRestoreHandle TryAcquire(IMesh mesh)
	{
		// 索引缓冲总是内嵌在序列化体里，非空说明数据完整（非剥离资产），免恢复
		if (mesh.IndexBuffer.Length != 0)
		{
			return AssetDataRestoreHandle.None;
		}
		if (mesh.Collection.MaterializeAssetOnly(mesh.PathID) is IMesh materialized)
		{
			if (materialized.IndexBuffer.Length != 0)
			{
				mesh.IndexBuffer = materialized.IndexBuffer;
			}
			if (materialized.VertexData.Data.Length != 0)
			{
				mesh.VertexData.Data = materialized.VertexData.Data;
			}
			if (mesh.IndexBuffer.Length != 0 || mesh.VertexData.Data.Length != 0)
			{
				return new AssetDataRestoreHandle(mesh.ClearEmbeddedData);
			}
		}
		return AssetDataRestoreHandle.None;
	}

	/// <summary>
	/// 导出前回读 AudioClip 真实数据并取得用完即弃句柄。
	/// 内嵌音频尚在或保留了流式引用时免恢复；被剥离时重建对象补回音频数据。
	/// 注意这里不能调用 <see cref="AudioClipExtensions.GetAudioData"/> 做存在性检查——
	/// 它会直接读入整段音频（.resource / 内嵌），检查阶段就把全部数据加载进内存。
	/// </summary>
	public static AssetDataRestoreHandle TryAcquire(IAudioClip audioClip)
	{
		if (audioClip.Has_AudioData() && audioClip.AudioData.Length > 0)
		{
			return AssetDataRestoreHandle.None;
		}
		if (audioClip.Has_Resource() && audioClip.Resource is { } resource && resource.IsSet())
		{
			return AssetDataRestoreHandle.None;
		}
		if (audioClip.Collection.MaterializeAssetOnly(audioClip.PathID) is IAudioClip materialized
			&& materialized.Has_AudioData() && materialized.AudioData.Length > 0)
		{
			audioClip.AudioData = materialized.AudioData;
			return new AssetDataRestoreHandle(audioClip.ClearEmbeddedData);
		}
		return AssetDataRestoreHandle.None;
	}
}