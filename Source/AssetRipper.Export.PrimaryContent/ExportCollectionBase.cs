using AssetRipper.Assets;

namespace AssetRipper.Export.PrimaryContent;

/// <summary> 导出集合的基类 </summary>
public abstract class ExportCollectionBase
{
	/// <summary> 是否包含指定的资产。 </summary>
	public abstract bool Contains(IUnityObjectBase asset);

	/// <summary> 出集合。 </summary>
	public abstract bool Export(string projectDirectory, FileSystem fileSystem);

	protected void ExportAsset(IUnityObjectBase asset, string path, string name, FileSystem fileSystem)
	{
		if (!fileSystem.Directory.Exists(path))
		{
			fileSystem.Directory.Create(path);
		}

		string fullName = $"{name}.{GetExportExtension(asset)}";
		string uniqueName = fileSystem.GetUniqueName(path, fullName, FileSystem.MaxFileNameLength);
		string filePath = fileSystem.Path.Join(path, uniqueName);
		ContentExtractor.Export(asset, filePath, fileSystem);
	}

	protected string GetUniqueFileName(IUnityObjectBase asset, string dirPath, FileSystem fileSystem)
	{
		string fileName = asset.GetBestName();
		fileName = FileSystem.RemoveCloneSuffixes(fileName);
		fileName = FileSystem.RemoveInstanceSuffixes(fileName);
		fileName = fileName.Trim();
		if (string.IsNullOrEmpty(fileName))
		{
			fileName = asset.ClassName;
		}
		else
		{
			fileName = FileSystem.FixInvalidFileNameCharacters(fileName);
		}

		fileName = $"{fileName}.{GetExportExtension(asset)}";
		return GetUniqueFileName(dirPath, fileName, fileSystem);
	}

	protected virtual string GetExportExtension(IUnityObjectBase asset)
	{
		return ExportExtension;
	}

	protected virtual string ExportExtension => "asset";

	protected static string GetUniqueFileName(string directoryPath, string fileName, FileSystem fileSystem)
	{
		return fileSystem.GetUniqueName(directoryPath, fileName, FileSystem.MaxFileNameLength);
	}

	public abstract IContentExtractor ContentExtractor { get; }
	public abstract IEnumerable<IUnityObjectBase> Assets { get; }
	public virtual IEnumerable<IUnityObjectBase> ExportableAssets => Assets;
	public virtual bool Exportable => true;
	public abstract string Name { get; }
}
