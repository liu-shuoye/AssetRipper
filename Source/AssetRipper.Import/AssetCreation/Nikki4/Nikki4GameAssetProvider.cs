using AssetRipper.Assets;
using AssetRipper.Assets.Metadata;
using AssetRipper.Import.AssetCreation;
using AssetRipper.SourceGenerated;

namespace AssetRipper.Import.AssetCreation.Nikki4;

/// <summary>
/// 闪耀暖暖专属资产提供者，为 Nikki4 定制格式的资产类型创建专属解析对象。
/// </summary>
public sealed class Nikki4GameAssetProvider : IGameAssetProvider
{
	/// <inheritdoc/>
	public IUnityObjectBase? TryCreateAsset(AssetInfo assetInfo, UnityVersion version)
	{
		// 返回 null 表示该类型不属于 Nikki4 专属解析范围，由调用方回退到默认解析
		return (ClassIDType)assetInfo.ClassID switch
		{
			ClassIDType.AnimationClip => new AnimationClip_Nikki4(assetInfo),
			ClassIDType.Material => new Material_Nikki4(assetInfo),
			ClassIDType.Shader => new Shader_Nikki4(assetInfo),
			ClassIDType.SkinnedMeshRenderer => new SkinnedMeshRenderer_Nikki4(assetInfo),
			ClassIDType.Mesh => new Mesh_Nikki4(assetInfo),
			ClassIDType.AnimatorController => new AnimatorController_Nikki4(assetInfo),
			ClassIDType.ParticleSystem => new ParticleSystem_Nikki4(assetInfo),
			ClassIDType.ParticleSystemRenderer => new ParticleSystemRenderer_Nikki4(assetInfo),
			ClassIDType.TrailRenderer => new TrailRenderer_Nikki4(assetInfo),
			ClassIDType.SpriteRenderer => new SpriteRenderer_Nikki4(assetInfo),
			ClassIDType.VisualEffect => new VisualEffect_Nikk4(assetInfo),
			ClassIDType.LineRenderer => new LineRenderer_nikki4(assetInfo),
			_ => null,
		};
	}
}
