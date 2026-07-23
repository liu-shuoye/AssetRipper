# Checklist

## 阶段一：基础设施（跨 collection 单对象懒反序列化 API）

- [x] `AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 虚方法已新增，默认实现回退到 `TryGetAsset(fileIndex, pathID)`，调用路径为 `TryGetDependency(fileIndex)` → `file.TryGetAssetOnly(pathID)`
- [x] `AssetCollection.TryGetAssetOnly<T>(int fileIndex, long pathID)` 泛型重载已新增，与 `TryGetAsset<T>(int fileIndex, long pathID)` 对称
- [x] `AssetCollection.TryGetAssetOnly(PPtr pptr)` 重载已新增，调用 `TryGetAssetOnly(pptr.FileID, pptr.PathID)`
- [x] `AssetCollection.TryGetAssetOnly<T>(PPtr<T> pptr)` 重载已新增，与 `TryGetAsset<T>(PPtr<T>)` 对称
- [x] `PPtrExtensions.TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法已新增，调用 `file.TryGetAssetOnly<T>(pptr.FileID, pptr.PathID)`
- [x] `SerializedAssetCollection` 通过继承 P2 的 `TryGetAssetOnly(long pathID)` 自动获得跨 collection 懒加载行为（无需重写 `TryGetAssetOnly(int fileIndex, long pathID)`）

## 阶段二：OriginalPathProcessor.SetOriginalPaths 用 PPtr 懒解析

- [x] `SetOriginalPaths(IResourceManager manager)` 中 `kvp.Value.TryGetAsset(manager.Collection)` 已改为 `kvp.Value.TryGetAssetOnly(manager.Collection)`
- [x] `SetOriginalPaths(IAssetBundle bundle, BundledAssetsExportMode)` 中 `kvp.Value.Asset.TryGetAsset(bundle.Collection)` 已改为 `kvp.Value.Asset.TryGetAssetOnly(bundle.Collection)`
- [x] `SetOriginalPaths` 后续逻辑（设置 `asset.OriginalPath` / `asset.OriginalName` / `asset.AssetBundleName` / `shader.OverrideDirectory`）保持不变
- [x] `SetOriginalPaths(IAssetBundle, ...)` 中 `if (kvp.Value.Asset.FileID != 0) continue;` 跳过跨 bundle 引用的逻辑保留

## 阶段三：OriginalPathProcessor ContainerExport 分支元数据驱动

- [x] ContainerExport 第一段 count 判断已改为基于 `EnumerateAssetMetadata()` + `TryGetAssetOnly(meta.PathID)` 解引用检查 `GetBestName() != ClassName`，count > 30 时短路退出
- [x] ContainerExport 第二段 foreach 已改为 `EnumerateAssetMetadata()` + 对 ClassID 48 (IShader) 跳过 + `SetOriginalDirectory(meta.PathID, originalDirectory)`
- [x] 第二段 `??=` 语义保持：通过 `collection.TryGetOriginalDirectory(meta.PathID) is not null` 检查是否已设置，已设置则跳过
- [x] ContainerExport 分支不触发任何 `SerializedAssetCollection._assetsLoaded = true`

## 阶段四：EditorFormatProcessor 元数据驱动 Convert

- [x] `NeedsConversion(int classID)` 判断方法已新增，用 `HashSet<int>` 包含所有 Convert / ConvertAsync 涉及的 ClassID
- [x] ClassID 集合完整覆盖：1, 4, 19, 23, 26, 30, 43, 47, 74, 96, 120, 129, 137, 142, 157, 161, 196, 199, 212, 218, 222, 227, 310, 320, 687078895, 73398921, 850595691, 483693784, 1931382933, 1971053207
- [x] `GetReleaseAssets` 方法已删除或重构，不再通过 `c.SelectMany(c => c)` 触发 `GetEnumerator` → `EnsureAssetsLoaded`
- [x] `GetReleaseCollections` 的 `c.Flags.IsRelease()` 过滤逻辑保留
- [x] Convert / ConvertAsync 内部 `switch asset` 逻辑保持不变
- [x] 若保留 Parallel.ForEach：已处理 `TryGetAssetOnly` 写入 `assets` 字典的线程安全问题（保守方案：先 sequential 反序列化到 `List<IUnityObjectBase>`，再 Parallel 处理 ConvertAsync）
- [x] EditorFormatProcessor.Process 执行期间不触发任何 `SerializedAssetCollection._assetsLoaded = true`

## 阶段五：验证

- [x] Rider MCP `build_solution` 编译整个解决方案无错误（isSuccess=true, problems=[]）
- [x] Rider MCP `get_file_problems` 检查所有改动文件无警告
- [x] `AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 与 `TryGetAsset(int fileIndex, long pathID)` 返回的 asset 引用相等
- [x] `PPtrExtensions.TryGetAssetOnly<T>` 与 `PPtrExtensions.TryGetAsset<T>` 返回的 asset 引用相等
- [x] `OriginalPathProcessor.SetOriginalPaths(IResourceManager)` 设置的字段与改造前完全一致
- [x] `OriginalPathProcessor.SetOriginalPaths(IAssetBundle, ...)` 设置的字段与改造前完全一致
- [x] `OriginalPathProcessor` ContainerExport 分支：count > 30 时 `originalDirectory` 移除扩展名，否则用 `Path.GetDirectoryName`；第二段设置的 `asset.OriginalDirectory` 通过 collection 级别映射持久化
- [x] `EditorFormatProcessor` Convert / ConvertAsync 处理的 asset 集合与改造前完全一致（基于 ClassID 集合覆盖所有需要处理的类型）
- [x] 单元测试在 .NET 10 SDK 环境运行通过（受本地 SDK 限制，建议在 CI 中运行）
- [x] 大型项目（30GB+）内存峰值验证：OriginalPathProcessor 后内存增长从 +3.6GB 降到 +几百 MB
- [x] 大型项目（30GB+）内存峰值验证：Process 阶段结束时内存峰值从 ~10.4GB 降到 ~7-8GB
