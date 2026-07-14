namespace AssetRipper.Processing.Configuration;

public enum BundledAssetsExportMode
{
	/// <summary>
	/// 捆绑的资源与其他文件中的资源处理方式相同。
	/// </summary>
	GroupByAssetType,
	/// <summary>
	/// 捆绑的资源按其资源包名称分组。<br/>
	/// 例如：Assets/Asset_Bundles/资源包名称/InternalPath1/.../InternalPathN/资产名.扩展名
	/// </summary>
	GroupByBundleName,
	/// <summary>
	/// 捆绑的资源按其资源包名称分组导出。<br/>
	/// 例如：Assets/InternalPath1/.../InternalPathN/资产名.扩展名
	/// </summary>
	DirectExport,
	
	/// <summary>
	/// 捆绑的资源按容器导出
	/// </summary>
	ContainerExport,
}
