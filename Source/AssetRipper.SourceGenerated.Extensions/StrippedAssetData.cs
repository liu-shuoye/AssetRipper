using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_198;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Subclasses.AnimationCurve_Single;
using AssetRipper.SourceGenerated.Subclasses.MinMaxCurve;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

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

	// ============================ ParticleSystem 曲线剥离/回补 ============================

	/// <summary>
	/// 清空 ParticleSystem 全部曲线关键帧（AnimationCurve/MinMaxCurve 的 Keyframe 列表）。
	/// 只清内嵌关键帧数组（加载期内存大头），保留标量/模式/PPtr 等瘦身结构。
	/// </summary>
	public static void ClearEmbeddedData(this IParticleSystem particleSystem)
	{
		foreach (IMinMaxCurve curve in EnumerateMinMaxCurves(particleSystem))
		{
			curve.MinCurve.Curve.Clear();
			curve.MaxCurve.Curve.Clear();
		}
	}

	/// <summary>
	/// 是否存在任意一条非空曲线。用于免恢复判定：有数据即认为未剥离，直接跳过重建。
	/// </summary>
	public static bool HasAnyCurveData(this IParticleSystem particleSystem)
	{
		foreach (IMinMaxCurve curve in EnumerateMinMaxCurves(particleSystem))
		{
			if (curve.MinCurve.Curve.Count != 0 || curve.MaxCurve.Curve.Count != 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// 导出前回补 ParticleSystem 曲线数据并取得用完即弃句柄。
	/// 曲线尚在（未剥离）时免恢复；被剥离时重建对象，把各曲线关键帧按同构索引拷回当前对象，
	/// Dispose 时再次清空。重建对象不参与集合缓存，由 GC 回收（见 <see cref="AssetCollection.MaterializeAssetOnly"/>）。
	/// </summary>
	public static AssetDataRestoreHandle TryAcquire(IParticleSystem particleSystem)
	{
		// 曲线非空说明数据完整（未剥离资产），免恢复
		if (particleSystem.HasAnyCurveData())
		{
			return AssetDataRestoreHandle.None;
		}
		if (particleSystem.Collection.MaterializeAssetOnly(particleSystem.PathID) is IParticleSystem materialized)
		{
			CopyCurveData(materialized, particleSystem);
			if (particleSystem.HasAnyCurveData())
			{
				return new AssetDataRestoreHandle(particleSystem.ClearEmbeddedData);
			}
		}
		return AssetDataRestoreHandle.None;
	}

	/// <summary>
	/// 逐条曲线把源对象的关键帧列表搬进目标对象（两者为同一运行类型，枚举顺序一致，按序号对齐）。
	/// 回补期间对象只读（YAML 序列化），共享 Keyframe 实例安全；Dispose 清空后即无残留引用。
	/// </summary>
	private static void CopyCurveData(IParticleSystem source, IParticleSystem target)
	{
		foreach ((IMinMaxCurve sourceCurve, IMinMaxCurve targetCurve) in ZipMinMaxCurves(source, target))
		{
			CopyAnimationCurve(sourceCurve.MinCurve, targetCurve.MinCurve);
			CopyAnimationCurve(sourceCurve.MaxCurve, targetCurve.MaxCurve);
		}
	}

	private static void CopyAnimationCurve(IAnimationCurve_Single source, IAnimationCurve_Single target)
	{
		target.Curve.Clear();
		// 源与目标为同一运行类型，Curve 的元素类型一致，直接复用实例（导出期只读，Dispose 清空后无残留引用）。
		// var 推断的 Keyframe 类型与 target.Curve 的元素类型一致，无需 dynamic，兼容 AOT/修剪
		foreach (var keyframe in source.Curve)
		{
			target.Curve.Add(keyframe);
		}
	}

	/// <summary>
	/// 同时枚举源/目标对象的全部 IMinMaxCurve，按同构顺序成对返回。
	/// 剥离/回补共用同一枚举逻辑，天然对称，不会漏字段。
	/// </summary>
	private static IEnumerable<(IMinMaxCurve, IMinMaxCurve)> ZipMinMaxCurves(IParticleSystem a, IParticleSystem b)
	{
		List<IMinMaxCurve> first = CollectMinMaxCurves(a);
		List<IMinMaxCurve> second = CollectMinMaxCurves(b);
		int count = Math.Min(first.Count, second.Count);
		for (int i = 0; i < count; i++)
		{
			yield return (first[i], second[i]);
		}
	}

	private static List<IMinMaxCurve> CollectMinMaxCurves(IParticleSystem ps)
	{
		List<IMinMaxCurve> list = [];
		foreach (IMinMaxCurve curve in EnumerateMinMaxCurves(ps))
		{
			list.Add(curve);
		}
		return list;
	}

	/// <summary>
	/// 枚举 ParticleSystem 及其全部模块上的 IMinMaxCurve 字段。
	/// 所有曲线字段（含 VelocityModule 等以具体类 MinMaxCurve_2018 暴露的字段）都实现 <see cref="IMinMaxCurve"/>，
	/// 属性按运行时类型 + 元数据顺序稳定，故用反射过滤而不用手写几十条字段路径。
	/// </summary>
	private static IEnumerable<IMinMaxCurve> EnumerateMinMaxCurves(IParticleSystem particleSystem)
	{
		// 顶层 StartDelay 本身就是一个 IMinMaxCurve，与各模块分开枚举
		if (particleSystem.StartDelay_MinMaxCurve is { } startDelay)
		{
			yield return startDelay;
		}
		foreach (object module in EnumerateModules(particleSystem))
		{
			foreach (IMinMaxCurve curve in EnumerateCurveProperties(module))
			{
				yield return curve;
			}
		}
	}

	private static IEnumerable<object> EnumerateModules(IParticleSystem ps)
	{
		// null（模块未启用/未读取）直接跳过；顺序与源/目标对象一致（同类型读取顺序相同）
		object?[] modules =
		[
			ps.InitialModule, ps.ShapeModule, ps.EmissionModule, ps.SizeModule, ps.RotationModule,
			ps.ColorModule, ps.UVModule, ps.VelocityModule, ps.InheritVelocityModule, ps.ForceModule,
			ps.ExternalForcesModule, ps.ClampVelocityModule, ps.NoiseModule, ps.SizeBySpeedModule,
			ps.RotationBySpeedModule, ps.ColorBySpeedModule, ps.CollisionModule, ps.TriggerModule,
			ps.SubModule, ps.LightsModule, ps.TrailModule, ps.CustomDataModule,
		];
		foreach (object? module in modules)
		{
			if (module is not null)
			{
				yield return module;
			}
		}
	}

	/// <summary>
	/// 反射枚举对象上所有声明类型实现 <see cref="IMinMaxCurve"/> 的属性值；属性元数据按类型缓存，避免每次反射。
	/// </summary>
	private static readonly ConcurrentDictionary<Type, PropertyInfo[]> CurvePropertyCache = new();

	private static IEnumerable<IMinMaxCurve> EnumerateCurveProperties(object module)
	{
		Type type = module.GetType();
		if (!CurvePropertyCache.TryGetValue(type, out PropertyInfo[]? properties))
		{
			properties = type.GetProperties().Where(p => typeof(IMinMaxCurve).IsAssignableFrom(p.PropertyType)).ToArray();
			CurvePropertyCache[type] = properties;
		}
		foreach (PropertyInfo property in properties)
		{
			if (property.GetValue(module) is IMinMaxCurve curve)
			{
				yield return curve;
			}
		}
	}
}