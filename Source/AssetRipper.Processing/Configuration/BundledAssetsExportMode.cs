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
	/// 捆绑的资源带原始路径的按原始路径位置导出；没有原始路径的导出到主资产路径下、以主资产文件名为名的文件夹。<br/>
	/// 例如：主资产 Assets/a/b/yyy.prefab → 无原始路径的资源导出到 Assets/a/b/yyy/资产名.扩展名
	/// </summary>
	/// <remarks>
	/// 主资产按两级回退确定：① 资源自身的 <see cref="AssetRipper.Assets.IUnityObjectBase.MainAsset"/>；
	/// ② 所在资源包的主资产（资源包容器中带 <c>Assets/</c> 相对路径的条目）。两级都取不到时保持默认的 <c>Assets/&lt;类名&gt;</c> 行为。
	/// </remarks>
	MainAssetFolder,

}
