using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Import.Logging;

namespace AssetRipper.Export.UnityProjects.UserAssets;

/// <summary>
/// 复制用户源文件与 .meta 的单资产导出集合，其他资产对它的引用指向用户 GUID。
/// </summary>
public sealed class UserAssetExportCollection : ExportCollection
{
	public UserAssetExportCollection(IAssetExporter assetExporter, IUnityObjectBase asset, UserAssetEntry entry)
	{
		AssetExporter = assetExporter ?? throw new ArgumentNullException(nameof(assetExporter));
		Asset = asset ?? throw new ArgumentNullException(nameof(asset));
		Entry = entry ?? throw new ArgumentNullException(nameof(entry));
	}

	/// <summary>被替换的游戏资产。</summary>
	public IUnityObjectBase Asset { get; }

	/// <summary>匹配到的用户资产条目。</summary>
	public UserAssetEntry Entry { get; }

	public override IAssetExporter AssetExporter { get; }

	public override AssetCollection File => Asset.Collection;

	public override IEnumerable<IUnityObjectBase> Assets => [Asset];

	public override string Name => Asset.GetBestName();

	/// <summary>
	/// 返回用户的 GUID（而非随机生成），这是其他资产的引用指向用户资产的关键。
	/// </summary>
	public override UnityGuid GUID => Entry.Guid;

	public override bool Export(IExportContainer container, string projectDirectory, FileSystem fileSystem)
	{
		// 目标路径保持用户项目内的相对结构，便于用户识别；引用只依赖 GUID + fileID，与路径无关。
		string relativePath = FileSystem.FixInvalidPathCharacters(Entry.RelativeTargetPath);
		string targetPath = fileSystem.Path.Join(projectDirectory, relativePath);
		string? targetDirectory = fileSystem.Path.GetDirectoryName(targetPath);
		if (!string.IsNullOrEmpty(targetDirectory))
		{
			fileSystem.Directory.Create(targetDirectory);
		}

		if (fileSystem.File.Exists(targetPath))
		{
			Logger.Warning(LogCategory.Export, $"用户资产目标路径 '{targetPath}' 已存在，将被用户文件覆盖。");
		}

		// 流式复制源文件：FileSystem 抽象没有 Copy 方法，且避免大文件一次性读入内存
		using (Stream source = fileSystem.File.OpenRead(Entry.SourceFilePath))
		using (Stream destination = fileSystem.File.Create(targetPath))
		{
			source.CopyTo(destination);
		}

		// 原样复制 .meta，保留用户 GUID 与 importer 设置（不经 Meta.ExportYamlDocument 重建）
		using (Stream source = fileSystem.File.OpenRead(Entry.SourceMetaPath))
		using (Stream destination = fileSystem.File.Create(targetPath + ".meta"))
		{
			source.CopyTo(destination);
		}

		Logger.Info(LogCategory.Export, $"已用用户资产替换 '{Name}' -> '{Entry.SourceFilePath}'");
		(AssetExporter as UserProjectAssetExporter)?.NotifyReplaced();
		return true;
	}

	public override bool Contains(IUnityObjectBase asset)
	{
		return Asset.AssetInfo == asset.AssetInfo;
	}

	public override long GetExportID(IExportContainer container, IUnityObjectBase asset)
	{
		// 主资产 fileID 现算即得 Unity 原生约定值（classID * 100000）：
		// Shader 4800000 / Material 2100000 / MonoScript 11500000 / Texture2D 2800000 / AudioClip 8300000 / TextAsset 4900000
		if (asset.AssetInfo == Asset.AssetInfo)
		{
			return ExportIdHandler.GetMainExportID(Asset);
		}
		throw new ArgumentException(null, nameof(asset));
	}

	public override MetaPtr CreateExportPointer(IExportContainer container, IUnityObjectBase asset, bool isLocal)
	{
		long exportID = GetExportID(container, asset);
		// isLocal 分支按 AssetExportCollection 惯例处理；本集合只复制文件不写 YAML，实际不会触发
		return isLocal
			? new MetaPtr(exportID)
			: new MetaPtr(exportID, GUID, AssetExporter.ToExportType(Asset));
	}
}
