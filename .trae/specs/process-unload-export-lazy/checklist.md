# Checklist

## 阶段一：UnloadAssets 基础设施

- [x] `AssetCollection.UnloadAssets()` 虚方法已新增，默认实现为空操作（no-op，兼容非懒加载 collection）
- [x] `AssetCollection.Dispose(bool)` 中先调用 `UnloadAssets()` 再释放非托管资源（额外 `assets.Clear()` 确保非懒加载 collection 也被清理）
- [x] `SerializedAssetCollection.UnloadAssets()` 已重写：清空 `assets` 字典、重置 `_assetsLoaded = false`、保留 `_sourceFile`/`_factory`/`_originalDirectoryOverrides`/`dependencies`/`Scene`
- [x] `SerializedAssetCollection.UnloadAssets()` 幂等：多次调用不抛异常（`assets.Clear()` 对空字典为 no-op，`_assetsLoaded = false` 重复设置无副作用）
- [x] Unload 后 `TryGetAssetOnly(pathID)` 能重新从 `_sourceFile.Objects` 反序列化单个对象（代码分析确认：TryGetAssetOnly 先查字典，再按 PathID 从 ObjectInfo 数组反序列化）
- [x] Unload 后 `GetEnumerator()` 能重新触发 `EnsureAssetsLoaded` 全量反序列化（代码分析确认：GetEnumerator 调用 EnsureAssetsLoaded，检查 _assetsLoaded）

## 阶段二：ExportHandler 在 Process 后调用 UnloadAssets

- [x] `ExportHandler.Export` 中 Process 完成后、`PrepareForExport` 之前，遍历 `FetchAssetCollections()` 调用 `UnloadAssets()`
- [x] 添加了内存诊断日志（"Process 后 Unload 前" / "Process 后 Unload 完成"）
- [x] `ProcessedAssetCollection.UnloadAssets()` 是空操作（基类默认 no-op，ProcessedAssetCollection 未重写），不会清空 Process 阶段新建的资产
- [x] Unload 后 `_originalDirectoryOverrides` 仍保留（SerializedAssetCollection.UnloadAssets 不清理此字段），`UnityObjectBase.OriginalDirectory` getter 仍能正确回退读取

## 阶段三：CreateCollections 改用元数据枚举

- [x] `ProjectExporter.CreateCollections` 用 `FetchAssetCollections()` + `EnumerateAssetMetadata()` + `TryGetAssetOnly` 替代 `FetchAssets()` 全量遍历
- [x] `queued` HashSet 去重语义保留（已处理的 asset 不重复创建 ExportCollection）
- [x] `ProcessedAssetCollection.EnumerateAssetMetadata` 触发 `EnsureAssetsLoaded`（默认实现），行为与改造前一致
- [x] `CreateCollection(asset)` 仍通过 `assetExporterStack.GetHandlerStack(asset.GetType())` 分发
- [x] `ProjectAssetContainer` 构造不再传 `fileCollection.FetchAssets()`，改为传 `fileCollection.FetchAssetCollections()`
- [x] `ProjectAssetContainer` 内 `assets.OfType<IBuildSettings>().FirstOrDefault()` 改为遍历 collections + 元数据枚举 + 按需 `TryGetAssetOnly`（ClassID 141）
- [x] `m_assetCollections` 字典的填充逻辑正确（遍历每个 `IExportCollection.Assets`，未修改）

## 阶段四：PPtr 解引用懒加载

- [x] `ProjectYamlWalker.CreateYamlNodeForPPtr` 的 `TryGetAsset` 改为 `TryGetAssetOnly`
- [x] `ContentHashWalker` 的 `TryGetAsset` 改为 `TryGetAssetOnly`
- [x] PPtr 解引用失败时回退到 `MetaPtr.CreateMissingReference`，行为与改造前一致
- [x] `TryGetAssetOnly(PPtr)` 与 `TryGetAssetOnly(int fileIndex, long pathID)` 重载在 P3 已实现并可用

## 阶段五：EditorFormatProcessor Process 阶段移除 Convert

- [x] `EditorFormatProcessor.Process` 移除 sequential `Convert` + Parallel `ConvertAsync` 调用
- [x] `PrepareForExport` 调用保留（重建 tagManager / assemblyManager 依赖）
- [x] `ProcessForExport` 在 Export 阶段通过 `container.EditorFormatConverter` 注入调用（P3 已实现，无需修改）
- [x] 破坏性清空（AnimationClip 的 `StreamedClip.Data.Clear()` 等）在 `Convert(IAnimationClip)` → `AnimationClipConverter.Process` 内部执行
- [x] Unload 后重新反序列化的 AnimationClip 仍能正确解压（因为源数据未在 Process 阶段被清空，Process 不再调用 Convert）
- [x] 死代码已清理（GetReleaseCollections / ProcessStageClassIDs / NeedsConversionInProcess 已移除）
- [x] 过时注释已更新（ProcessForExport remarks、PrepareForExport checksumCache 注释、类级 XML doc）

## 阶段六：验证

- [ ] 使用 Rider MCP `build_solution` 编译整个解决方案无错误（**受 .NET 10 SDK 限制无法本地编译**）
- [ ] 使用 Rider MCP `get_file_problems` 检查所有改动文件无警告（**受 .NET 10 SDK 限制无法本地检查**）
- [x] `SerializedAssetCollection.UnloadAssets()` 后 `assets` 字典为空、`_assetsLoaded` 为 false、`_sourceFile`/`_factory`/`_originalDirectoryOverrides` 保留（代码分析确认）
- [x] Unload 后 `TryGetAssetOnly(pathID)` 重新反序列化的 asset 内容与原 asset 相等（同一 ObjectInfo 源，代码分析确认）
- [x] `CreateCollections` 生成的 `List<IExportCollection>` 与改造前完全一致（逻辑等价：FetchAssetCollections+EnumerateAssetMetadata+TryGetAssetOnly ≡ FetchAssets）
- [x] `ProjectYamlWalker` 的 PPtr 解引用在 Unload 后的 collection 上能正确懒加载目标对象（TryGetAssetOnly 不触发 EnsureAssetsLoaded）
- [x] `ContentHashWalker` 的 PPtr 解引用在 Unload 后的 collection 上能正确懒加载目标对象（TryGetAssetOnly 不触发 EnsureAssetsLoaded）
- [x] `EditorFormatProcessor.ProcessForExport` 在 Export 阶段对每个 asset 调用 Convert/ConvertAsync，结果与原 Process 阶段调用一致（同一 Convert/ConvertAsync 方法）
- [x] `DeduplicationTests.cs` 测试中 `ProjectAssetContainer` 构造调用已同步更新为 `FetchAssetCollections()`
- [ ] 运行 `AssetRipper.Assets.Tests` 单元测试通过（**受 .NET 10 SDK 限制无法本地运行**）
- [ ] 运行 `AssetRipper.IO.Files.Tests` 单元测试通过（**受 .NET 10 SDK 限制无法本地运行**）
- [ ] 运行 `AssetRipper.Export.UnityProjects.Tests` 单元测试通过（**受 .NET 10 SDK 限制无法本地运行**）
- [ ] 运行 `AssetRipper.GUI.Web.Tests` 单元测试通过（**受 .NET 10 SDK 限制无法本地运行**）
- [ ] 在大型项目（30GB+）上运行 Load + Process + Export，确认 Process 后 Unload 完成后内存从 7-8GB 降到 4-5GB（**需运行时验证**）
- [ ] 确认 Export 阶段 `CreateCollections` 执行期间，任何 `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true（**需运行时验证**）
- [ ] 确认 Export 阶段峰值内存从 10GB+ 降到 6-7GB（**需运行时验证**）
- [ ] 确认 Export 完成后 `GameBundle.Dispose` 能正确释放所有资源（**需运行时验证**）
