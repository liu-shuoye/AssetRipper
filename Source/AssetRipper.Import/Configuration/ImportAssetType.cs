namespace AssetRipper.Import.Configuration;

/// <summary>
/// 可被导入白名单筛选的 Unity 资产类型。
/// </summary>
/// <remarks>
/// 这里的成员是面向用户的「资源大类」，一个成员通常对应多个
/// <see cref="AssetRipper.SourceGenerated.ClassIDType"/> 取值
/// （例如贴图类同时覆盖 Texture2D/Texture3D/Cubemap/RenderTexture）。
/// 映射关系集中在 <see cref="ImportAssetTypeExtensions"/>，避免散落在各处导致遗漏。
/// </remarks>
public enum ImportAssetType
{
	/// <summary>网格（Mesh）。</summary>
	Mesh,

	/// <summary>贴图（Texture2D/Texture3D/Cubemap/RenderTexture 等）。</summary>
	Texture,

	/// <summary>材质（Material）。</summary>
	Material,

	/// <summary>着色器（Shader/ComputeShader/RayTracingShader）。</summary>
	Shader,

	/// <summary>动画片段（AnimationClip）。</summary>
	AnimationClip,

	/// <summary>动画控制器（AnimatorController/AnimatorOverrideController）。</summary>
	AnimatorController,

	/// <summary>音频片段（AudioClip）。</summary>
	AudioClip,

	/// <summary>字体（Font）。</summary>
	Font,

	/// <summary>文本资产（TextAsset）。</summary>
	TextAsset,

	/// <summary>精灵（Sprite）。</summary>
	Sprite,

	/// <summary>预制体与场景层级对象（GameObject/Transform/组件等）。</summary>
	GameObject,

	/// <summary>MonoBehaviour 脚本实例。</summary>
	MonoBehaviour,

	/// <summary>ScriptableObject（本质是 MonoBehaviour，单独列出便于理解）。</summary>
	ScriptableObject,

	/// <summary>视频（VideoClip）。</summary>
	VideoClip,
}
