using AssetRipper.Assets;
using AssetRipper.SourceGenerated.Classes.ClassID_198;
using System;
using System.Collections.Generic;

namespace AssetRipper.SourceGenerated.Extensions;

/// <summary>
/// ParticleSystem 曲线数据「导出前批量回补 + 用完即弃」的作用域对象。
/// 集合导出（Prefab/Scene YAML 写出）前对其中全部 <see cref="IParticleSystem"/> 调
/// <see cref="StrippedAssetData.TryAcquire"/> 回补被剥离的曲线关键帧，作用域释放时统一清空。
/// 未剥离的资产返回空操作句柄，不受影响。
/// 用法：<c>using var scope = ParticleSystemRestoreScope.Create(exportableAssets);</c>
/// </summary>
public sealed class ParticleSystemRestoreScope : IDisposable
{
	private readonly List<AssetDataRestoreHandle> m_handles = [];

	private ParticleSystemRestoreScope(IEnumerable<IUnityObjectBase> assets)
	{
		foreach (IUnityObjectBase asset in assets)
		{
			if (asset is IParticleSystem particleSystem)
			{
				m_handles.Add(StrippedAssetData.TryAcquire(particleSystem));
			}
		}
	}

	/// <summary>
	/// 为资产序列创建回补作用域；资产可来自 <c>ExportableAssets</c>（Prefab 层级 / 场景组件）。
	/// </summary>
	public static ParticleSystemRestoreScope Create(IEnumerable<IUnityObjectBase> assets)
	{
		return new ParticleSystemRestoreScope(assets);
	}

	/// <summary>
	/// 释放作用域：对每个回补过曲线数据的 ParticleSystem 再次清空，曲线数据不驻留内存。
	/// </summary>
	public void Dispose()
	{
		foreach (AssetDataRestoreHandle handle in m_handles)
		{
			handle.Dispose();
		}
		m_handles.Clear();
	}
}