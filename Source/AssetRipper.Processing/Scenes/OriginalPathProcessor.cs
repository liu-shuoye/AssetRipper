using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Import.Logging;
using AssetRipper.Processing.Configuration;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_147;
using AssetRipper.SourceGenerated.Classes.ClassID_27;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.AssetInfo;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Object;

namespace AssetRipper.Processing.Scenes;

/// <summary>
/// 处理原始路径
/// </summary>
/// <param name="bundledAssetsExportMode"></param>
public sealed class OriginalPathProcessor(BundledAssetsExportMode bundledAssetsExportMode) : IAssetProcessor
{
	private const string ResourcesKeyword = "Resources";
	private const string DirectorySeparator = "/";
	private const string AssetsDirectory = AssetsKeyword + DirectorySeparator;
	private const string ResourceFullPath = AssetsDirectory + ResourcesKeyword;
	private const string AssetBundleFullPath = AssetsDirectory + "AssetBundles";
	private const string AssetsKeyword = "Assets";

	public void Process(GameData gameData)
	{
		Logger.Info(LogCategory.Processing, "处理原始路径");
		Dictionary<AssetCollection, (string BundleName, IAssetBundle BundleAsset)> dictionary = [];
		Dictionary<AssetCollection, string?> originalDirectories = [];
		// 用元数据枚举避免 FetchAssets() 触发全量反序列化。
		// 仅对 IResourceManager (147) 与 IAssetBundle (142) 调用 TryGetAssetOnly 做单对象反序列化。
		foreach (AssetCollection collection in gameData.GameBundle.FetchAssetCollections())
		{
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				if (meta.ClassID == 147) // IResourceManager
				{
					IResourceManager? resourceManager = collection.TryGetAssetOnly<IResourceManager>(meta.PathID);
					if (resourceManager is not null)
					{
						SetOriginalPaths(resourceManager);
					}
				}
				else if (meta.ClassID == 142) // IAssetBundle
				{
					IAssetBundle? assetBundle = collection.TryGetAssetOnly<IAssetBundle>(meta.PathID);
					if (assetBundle is null)
					{
						continue;
					}

					string originalPath = SetOriginalPaths(assetBundle, bundledAssetsExportMode);
					switch (bundledAssetsExportMode)
					{
						case BundledAssetsExportMode.GroupByBundleName:
							{
								string assetBundleName = EnsureDoesNotEndWithBundleExtension(assetBundle.GetAssetBundleName());
								if (assetBundle.Collection.Bundle is not GameBundle)
								{
									foreach (AssetCollection c in assetBundle.Collection.Bundle.Collections)
									{
										dictionary[c] = (assetBundleName, assetBundle);
									}
								}

								break;
							}
						case BundledAssetsExportMode.ContainerExport:
							// 获取资源文件夹
							originalDirectories[assetBundle.Collection] = originalPath;
							break;
					}
				}
			}
		}

		foreach ((AssetCollection collection, (string BundleName, IAssetBundle BundleAsset)) in dictionary)
		{
			// 用元数据枚举 + collection 级别 OriginalDirectory 持久化，避免反序列化 asset 实例。
			// ((ClassIDType)meta.ClassID).ToString() 与 asset.ClassName (GetType().Name) 一致——
			// ClassIDType 枚举名与生成的类型名完全匹配（如 ClassIDType.GameObject 对应类型 GameObject）。
			foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
			{
				// 保持原 ??= 语义：仅对未设置 OriginalDirectory 的 pathID 设置
				if (collection.TryGetOriginalDirectory(meta.PathID) is not null)
				{
					continue;
				}

				string className = ((ClassIDType)meta.ClassID).ToString();
				collection.SetOriginalDirectory(meta.PathID, Path.Join(AssetBundleFullPath, BundleName, className));
			}
		}


		// ContainerExport 模式：用元数据枚举 + 按需 TryGetAssetOnly 实现懒解析，
		// 仅在需要 GetBestName 比较时反序列化目标对象，避免 collection.Count / foreach 触发全量反序列化。
		if (bundledAssetsExportMode == BundledAssetsExportMode.ContainerExport)
		{
			foreach ((AssetCollection collection, string? originalPath) in originalDirectories)
			{
				if (originalPath == null)
				{
					return;
				}

				string? originalDirectory = Path.GetDirectoryName(originalPath);
				// 用元数据枚举 + TryGetAssetTransient 检查 GetBestName：
				// transient 反序列化不进入 assets 字典，循环结束后对象即可被 GC 回收，
				// 避免此前 TryGetAssetOnly 把 collection 中所有非 Shader 对象永久驻留字典造成的 1.6GB 内存峰值。
				// 短路：count > 30 时立即退出，等价于原 Count(predicate) > 30 判断
				int count = 0;
				foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
				{
					IUnityObjectBase? asset = collection.TryGetAssetTransient(meta.PathID);
					if (asset is not null && asset.GetBestName() != asset.ClassName)
					{
						count++;
						if (count > 30)
						{
							break;
						}
					}
				}

				if (count > 30)
				{
					// 移除扩展名
					originalDirectory = originalPath[..originalPath.LastIndexOf('.')];
				}

				// 用元数据枚举 + SetOriginalDirectory，避免 foreach 触发全量反序列化
				// ClassID 48 = IShader，跳过保持原 if (asset is IShader) continue; 语义
				// ??= 语义：通过 TryGetOriginalDirectory 检查是否已设置，已设置则跳过
				// originalDirectory 此处一定不为 null：外层 if (originalPath == null) return; 已早返回
				foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
				{
					if (meta.ClassID == 48) // IShader
					{
						continue;
					}

					if (collection.TryGetOriginalDirectory(meta.PathID) is not null)
					{
						continue;
					}

					collection.SetOriginalDirectory(meta.PathID, originalDirectory!);
				}
			}
		}
	}

	private static void SetOriginalPaths(IResourceManager manager)
	{
		foreach (AccessPairBase<Utf8String, IPPtr_Object> kvp in manager.Container)
		{
			// 使用懒解析：TryGetAssetOnly 仅反序列化目标对象，避免触发跨 collection 全量反序列化
			IUnityObjectBase? asset = kvp.Value.TryGetAssetOnly(manager.Collection);
			if (asset is null)
			{
				continue;
			}

			string resourcePath = Path.Join(ResourceFullPath, kvp.Key.String);
			if (asset.OriginalPath is null)
			{
				asset.OriginalPath = resourcePath;
				UndoPathLowercasing(asset);
				SetOverridePathIfShader(asset);
			}
			else if (asset.OriginalPath.Length < resourcePath.Length)
			{
				// for paths like "Resources/inner/resources/extra/file" engine creates 2 resource entries
				// "inner/resources/extra/file" and "extra/file"
				asset.OriginalPath = resourcePath;
				UndoPathLowercasing(asset);
				SetOverridePathIfShader(asset);
			}
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// Asset bundles usually contain more assets than listed in <see cref="IAssetBundle.Container"/>. 
	/// We need to export them in AssetBundleFullPath directory if <see cref="m_BundledAssetsExportMode"/> is <see cref="BundledAssetsExportMode.GroupByBundleName"/>.
	/// That is done in a separate function.
	/// </remarks>
	/// <param name="bundle"></param>
	/// <exception cref="Exception"></exception>
	private static string SetOriginalPaths(IAssetBundle bundle, BundledAssetsExportMode bundledAssetsExportMode)
	{
		string bundleName = EnsureDoesNotEndWithBundleExtension(bundle.GetAssetBundleName());
		string bundleDirectory = bundleName + DirectorySeparator;
		string directory = Path.Join(AssetBundleFullPath, bundleName);
		string? outAssetPath = string.Empty;
		foreach (AccessPairBase<Utf8String, IAssetInfo> kvp in bundle.Container)
		{
			// skip shared bundle assets, because we need to export them in their bundle directory
			if (kvp.Value.Asset.FileID != 0)
			{
				continue;
			}

			// 使用懒解析：TryGetAssetOnly 仅反序列化目标对象，避免触发跨 collection 全量反序列化
			IUnityObjectBase? asset = kvp.Value.Asset.TryGetAssetOnly(bundle.Collection);
			if (asset is null)
			{
				continue;
			}

			asset.AssetBundleName = bundleName;

			string assetPath = OriginalPathHelper.EnsurePathNotRooted(kvp.Key.String);
			if (string.IsNullOrEmpty(assetPath))
			{
				continue;
			}

			switch (bundledAssetsExportMode)
			{
				case BundledAssetsExportMode.ContainerExport:
				case BundledAssetsExportMode.DirectExport:
					asset.OriginalPath = OriginalPathHelper.EnsureStartsWithAssets(assetPath);
					break;
				case BundledAssetsExportMode.GroupByBundleName:
					if (assetPath.StartsWith(AssetsDirectory, StringComparison.OrdinalIgnoreCase))
					{
						assetPath = assetPath.Substring(AssetsDirectory.Length);
					}

					if (assetPath.StartsWith(bundleDirectory, StringComparison.OrdinalIgnoreCase))
					{
						assetPath = assetPath.Substring(bundleDirectory.Length);
					}

					asset.OriginalPath = Path.Join(directory, assetPath);
					break;
			}

			UndoPathLowercasing(asset);
			SetOverridePathIfShader(asset);
			outAssetPath = asset.OriginalPath;
		}

		return outAssetPath ?? string.Empty;
	}

	private static string EnsureDoesNotEndWithBundleExtension(string path)
	{
		// We need to remove the .bundle extension if present. Unity exhibits weird behavior if a folder name ends with ".bundle".
		// On 2019.4.3 for example, materials contained in such a folder (or any subfolder) will not preview in the editor and cannot be viewed in the inspector.
		// I could not find any official documentation on this behavior, but it seems to be for packaging native code on Mac and iOS.
		// https://docs.unity3d.com/2017.3/Documentation/Manual/PluginsForDesktop.html

		const string BundleExtension = ".bundle";
		if (path.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
		{
			return path[..^BundleExtension.Length];
		}

		return path;
	}

	/// <summary>
	/// During compilation, Unity often lowers all the characters in a path. This restores the proper capitalization for asset names.
	/// </summary>
	/// <param name="asset"></param>
	private static void UndoPathLowercasing(IUnityObjectBase asset)
	{
		string? assetName = (asset as INamed)?.Name;
		string? originalName = asset.OriginalName;
		if (assetName is not null
		    && originalName is not null
		    && assetName.Length == originalName.Length
		    && originalName.Equals(assetName, StringComparison.OrdinalIgnoreCase))
		{
			asset.OriginalName = assetName;
		}
	}

	private static void SetOverridePathIfShader(IUnityObjectBase asset)
	{
		// Original name is prioritized below the asset name, so we need to set the override path.
		// Otherwise, the shader will be exported with the wrong name.
		if (asset is IShader shader)
		{
			shader.OverrideDirectory ??= shader.OriginalDirectory;
			shader.OverrideName ??= shader.OriginalName;
			shader.OverrideExtension ??= shader.OriginalExtension;
		}
	}
}
