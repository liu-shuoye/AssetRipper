namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 用户项目中的一个可替换资产条目。
/// </summary>
/// <param name="SourceFilePath">用户源文件的绝对路径。</param>
/// <param name="SourceMetaPath">与源文件同名的 .meta 文件绝对路径。</param>
/// <param name="Guid">从 .meta 解析出的用户 GUID。</param>
/// <param name="RelativeTargetPath">在导出项目中的相对目标路径（正斜杠分隔）。</param>
public sealed record UserAssetEntry(string SourceFilePath, string SourceMetaPath, UnityGuid Guid, string RelativeTargetPath)
{
	/// <summary>
	/// 是否已被某个导出集合消费。
	/// 同一用户文件只允许替换一个游戏资产，避免多个资产写到同一目标路径造成内容互相覆盖。
	/// </summary>
	public bool Consumed { get; set; }
}
