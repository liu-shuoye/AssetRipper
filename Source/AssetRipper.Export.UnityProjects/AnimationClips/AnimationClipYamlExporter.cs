using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.UnityProjects.Project;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Processing.AnimationClips;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Export.UnityProjects.AnimationClips;

/// <summary>
/// .anim 的 YAML 导出器。AnimationClip 的 EditorFormat 曲线转换已从 Process 阶段延迟到导出阶段：
/// 此处按需读取 MuscleClip 生成 Rotation/Position/Scale/Float/PPtr 曲线（一次只处理一个剪辑），
/// 序列化完成后立即清空曲线字段（用完即弃）。相比 Process 阶段一次性批量转换（完整导入实测
/// Vector3f/Quaternionf/Keyframe 约 14.4GB 同时驻留），导出期峰值只等于单个动画剪辑的曲线大小。
/// </summary>
public sealed class AnimationClipYamlExporter : YamlExporterBase
{
	private readonly IAssemblyManager assemblyManager;

	public AnimationClipYamlExporter(IAssemblyManager assemblyManager)
	{
		this.assemblyManager = assemblyManager;
	}

	public override bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out IExportCollection? exportCollection)
	{
		exportCollection = new AssetExportCollection<IUnityObjectBase>(this, asset);
		return true;
	}

	public override bool Export(IExportContainer container, IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		if (asset is not IAnimationClip clip)
		{
			return base.Export(container, asset, path, fileSystem);
		}

		// 导出前按需转换。路径哈希缓存（Avatar/Animator/Animation 的路径名 → CRC）处理阶段已不再构建，
		// 这里从资产可达的集合集合重建；GameBundle 之外的独立集合退化为只含当前集合。
		IEnumerable<AssetCollection> collections = asset.Collection.Bundle is GameBundle gameBundle
			? gameBundle.FetchAssetCollections()
			: [asset.Collection];
		PathChecksumCache checksumCache = new(collections, assemblyManager);
		try
		{
			// 转换假定曲线字段为空：先清一遍，避免上次导出异常残留导致重复追加曲线
			StripConvertedCurves(clip);
			AnimationClipConverter.Process(clip, checksumCache);
			return base.Export(container, asset, path, fileSystem);
		}
		finally
		{
			// 用完即弃：导出完成后清空转换生成的曲线，防止全部动画曲线在导出过程中逐渐累积驻留
			StripConvertedCurves(clip);
		}
	}

	/// <summary>
	/// 清空 <see cref="AnimationClipConverter"/> 生成的 editor 曲线字段。
	/// 不触碰 MuscleClip 等 release 数据（导出 write-editor 时仍需要），MuscleClipInfo 为标量结构可忽略。
	/// </summary>
	private static void StripConvertedCurves(IAnimationClip clip)
	{
		clip.RotationCurves_C74.Clear();
		clip.EulerCurves_C74.Clear();
		clip.PositionCurves_C74.Clear();
		clip.ScaleCurves_C74.Clear();
		clip.FloatCurves_C74.Clear();
		if (clip.Has_PPtrCurves_C74())
		{
			clip.PPtrCurves_C74.Clear();
		}
	}
}
