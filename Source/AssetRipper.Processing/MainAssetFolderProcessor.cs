using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Logging;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.AssetInfo;

namespace AssetRipper.Processing;

/// <summary>
/// 为 <see cref="BundledAssetsExportMode.MainAssetFolder"/> 模式补齐导出位置。
/// </summary>
/// <remarks>
/// <para>
/// 规则：资源包内的资源若已有原始路径（由 <see cref="Scenes.OriginalPathProcessor"/> 从资源包容器恢复）则保持原位不动；
/// 若没有原始路径，则放到「主资产所在目录 / 以主资产文件名（去扩展名）命名的子文件夹」下。
/// </para>
/// <para>
/// 主资产按两级回退确定：<br/>
/// ① 资源自身的 <see cref="IUnityObjectBase.MainAsset"/>，但它必须已有真实路径才有意义
///   （<see cref="IUnityObjectBase.GetBestDirectory"/> 在无路径时会退化成 <c>Assets/&lt;类名&gt;</c>，
///    那不是真实的“主资产路径”，例如 <see cref="Textures.SpriteInformationObject"/> 这类生成对象）；<br/>
/// ② 该资源所在资源包的主资产，即资源包容器中带 <c>Assets/</c> 相对路径的条目。<br/>
/// 两级都取不到时保持默认行为（<c>Assets/&lt;类名&gt;/</c>）。
/// </para>
/// <para>
/// 必须在处理器链的最后注册：<see cref="IUnityObjectBase.MainAsset"/> 由 PrefabProcessor /
/// SpriteProcessor / ScriptableObjectProcessor 设置，比它们更早的处理器读到的主资产还是 <see langword="null"/>。
/// </para>
/// </remarks>
public sealed class MainAssetFolderProcessor : IAssetProcessor
{
	/// <summary>IAssetBundle 的 ClassID。</summary>
	private const int AssetBundleClassID = 142;

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "Main Asset Folder Assignment");
		Dictionary<Bundle, string> bundleDirectories = CollectBundleMainAssetDirectories(gameData);

		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			// 只有 SerializedBundle 下的才算“捆绑资源”：根 GameBundle 下挂的是松散文件，
			// ProcessedBundle 下挂的是处理器生成的资产（层级对象、设置等），它们没有原始的资源包归属。
			if (collection.Bundle is not SerializedBundle)
			{
				continue;
			}

			bundleDirectories.TryGetValue(collection.Bundle, out string? bundleDirectory);
			foreach (IUnityObjectBase asset in collection)
			{
				AssignDirectory(asset, bundleDirectory);
			}
		}
	}

	/// <summary>
	/// 收集「资源包 → 该资源包主资产所在目录」的映射，目录已去掉主资产文件的扩展名。
	/// </summary>
	/// <remarks>
	/// 同一个资源包理论上只对应一个 <see cref="IAssetBundle"/> 对象，这里用 <c>ContainsKey</c> 防止重复登记时相互覆盖。
	/// </remarks>
	private static Dictionary<Bundle, string> CollectBundleMainAssetDirectories(GameData gameData)
	{
		Dictionary<Bundle, string> result = [];
		foreach (IAssetBundle assetBundle in gameData.EnumerateAssetsByClassID<IAssetBundle>(AssetBundleClassID))
		{
			Bundle bundle = assetBundle.Collection.Bundle;
			if (bundle is not SerializedBundle || result.ContainsKey(bundle))
			{
				continue;
			}

			if (TryGetBundleMainAssetPath(assetBundle, out string? mainAssetPath))
			{
				// 去掉扩展名，让主资产文件本身变成同名文件夹：Assets/a/b/yyy.prefab → Assets/a/b/yyy
				// ChangeExtension 只作用于文件名末尾的扩展名，不会误截目录名里的点
				result[bundle] = Path.ChangeExtension(mainAssetPath, null) ?? mainAssetPath;
			}
		}

		return result;
	}

	/// <summary>
	/// 取资源包的主资产路径：优先与资源包同名的条目，否则取第一个带相对路径的条目。
	/// </summary>
	/// <param name="assetBundle">资源包对象。</param>
	/// <param name="mainAssetPath">主资产路径，以 <c>Assets/</c> 开头。</param>
	/// <returns>找到主资产时返回 <see langword="true"/>。</returns>
	private static bool TryGetBundleMainAssetPath(IAssetBundle assetBundle, [NotNullWhen(true)] out string? mainAssetPath)
	{
		string bundleName = Path.GetFileNameWithoutExtension(assetBundle.GetAssetBundleName());
		string? firstAssetPath = null;

		foreach (AccessPairBase<Utf8String, IAssetInfo> kvp in assetBundle.Container)
		{
			// 跳过共享资源：它们由别的资源包提供，其路径属于那个包，不代表本包的主资产
			if (kvp.Value.Asset.FileID != 0)
			{
				continue;
			}

			string assetPath = OriginalPathHelper.EnsurePathNotRooted(kvp.Key.String);
			if (string.IsNullOrEmpty(assetPath))
			{
				continue;
			}

			firstAssetPath ??= assetPath;
			if (bundleName.Length > 0
				&& string.Equals(Path.GetFileNameWithoutExtension(assetPath), bundleName, StringComparison.OrdinalIgnoreCase))
			{
				mainAssetPath = OriginalPathHelper.EnsureStartsWithAssets(assetPath);
				return true;
			}
		}

		if (firstAssetPath is null)
		{
			mainAssetPath = null;
			return false;
		}

		mainAssetPath = OriginalPathHelper.EnsureStartsWithAssets(firstAssetPath);
		return true;
	}

	/// <summary>
	/// 给没有原始路径的资源分配目录，已有路径的资产一律保持原位。
	/// </summary>
	/// <param name="asset">待处理的资源。</param>
	/// <param name="bundleDirectory">二级回退用的资源包主资产目录，可能为 <see langword="null"/>。</param>
	private static void AssignDirectory(IUnityObjectBase asset, string? bundleDirectory)
	{
		// 已有原始路径（含 collection 级别映射）的资产按原始路径位置导出
		if (asset.OriginalDirectory is not null || asset.OriginalName is not null)
		{
			return;
		}

		// 已被其他处理器覆盖位置的资产（如预制体根）不参与本模式的重排
		if (asset.OverrideDirectory is not null || asset.OverrideName is not null)
		{
			return;
		}

		// 一级回退：资源自身的主资产。MainAsset 指向自己说明它就是主资产本身，没有可归属的父级
		IUnityObjectBase? mainAsset = asset.MainAsset;
		string? directory = mainAsset is not null && !ReferenceEquals(mainAsset, asset)
			? GetMainAssetDirectory(mainAsset)
			: null;

		// 二级回退：所在资源包的主资产
		directory ??= bundleDirectory;
		if (directory is null)
		{
			return;
		}

		asset.OriginalDirectory = directory;
	}

	/// <summary>
	/// 取主资产同名文件夹：<c>&lt;主资产目录&gt;/&lt;主资产文件名去扩展名&gt;</c>。
	/// </summary>
	/// <returns>主资产自身没有真实路径时返回 <see langword="null"/>，交由调用方回退。</returns>
	private static string? GetMainAssetDirectory(IUnityObjectBase mainAsset)
	{
		string? mainAssetDirectory = mainAsset.OverrideDirectory ?? mainAsset.OriginalDirectory;
		if (mainAssetDirectory is null)
		{
			return null;
		}

		string folderName = Path.GetFileNameWithoutExtension(mainAsset.GetBestName());
		return string.IsNullOrEmpty(folderName) ? null : Path.Join(mainAssetDirectory, folderName);
	}
}
