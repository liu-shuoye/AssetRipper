namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 可被用户项目源文件替换的资产类别。
/// </summary>
public enum UserAssetKind
{
	/// <summary>Shader（.shader 源文件）。</summary>
	Shader,

	/// <summary>材质（.mat）。</summary>
	/// 
	Material,

	/// <summary>C# 脚本（.cs）。</summary>
	Script,

	/// <summary>二维纹理（常见图片格式）。</summary>
	Texture,

	/// <summary>音频（常见音频格式）。</summary>
	Audio,

	/// <summary>文本资产（常见文本格式）。</summary>
	Text,
}
