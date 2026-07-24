# Tasks

## 阶段一：UnloadAssets 基础设施

- [x] Task 1: 在 AssetCollection 新增 UnloadAssets 虚方法
  - [x] SubTask 1.1: 在 [AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 新增 `public virtual void UnloadAssets()` 虚方法，默认实现为空操作（兼容 `ProcessedAssetCollection` / `VirtualAssetCollection` 等非懒加载 collection）
  - [x] SubTask 1.2: 在 `Dispose(bool disposing)` 中先调用 `UnloadAssets()`（清理托管引用），再释放非托管资源，保证 Dispose 语义完整

- [x] Task 2: 在 SerializedAssetCollection 重写 UnloadAssets
  - [x] SubTask 2.1: 在 [SerializedAssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs) 重写 `UnloadAssets()`：
    - 清空 `assets` 字典（`assets.Clear()`）
    - 重置 `_assetsLoaded = false`
    - **保留** `_sourceFile` / `_factory` / `_originalDirectoryOverrides` / `dependencies` / `Scene`
  - [x] SubTask 2.2: 验证 Unload 幂等：若 `_assetsLoaded` 已为 false 且 `assets` 已空，直接返回不抛异常
  - [x] SubTask 2.3: 验证 Unload 后 `TryGetAssetOnly(pathID)` 能重新从 `_sourceFile.Objects` 反序列化单个对象
  - [x] SubTask 2.4: 验证 Unload 后 `GetEnumerator()` 能重新触发 `EnsureAssetsLoaded` 全量反序列化

## 阶段二：ExportHandler 在 Process 后调用 UnloadAssets

- [x] Task 3: 在 ExportHandler.Export 中调用 UnloadAssets
  - [x] SubTask 3.1: 在 [ExportHandler.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs) 的 `Export` 方法中，Process 阶段所有 processor 执行完毕后、`PrepareForExport` 之前，新增遍历 `gameData.GameBundle.FetchAssetCollections()` 调用 `collection.UnloadAssets()`
  - [x] SubTask 3.2: 添加内存诊断日志 `Logger.LogMemoryDiagnostics("Process 后 Unload 前")` 与 `Logger.LogMemoryDiagnostics("Process 后 Unload 完成")`，便于验证内存下降
  - [x] SubTask 3.3: 验证 `ProcessedAssetCollection` 的 `UnloadAssets` 是空操作（不会清空 Process 阶段新建的 SceneHierarchyObject / PrefabHierarchyObject / ScriptableObjectGroup 等）
  - [x] SubTask 3.4: 验证 Unload 后 `_originalDirectoryOverrides` 仍保留，`UnityObjectBase.OriginalDirectory` getter 仍能正确回退读取

## 阶段三：CreateCollections 改用元数据枚举

- [x] Task 4: 重写 ProjectExporter.CreateCollections 用元数据枚举
  - [x] SubTask 4.1: 在 [ProjectExporter.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L149-180) 把 `foreach (IUnityObjectBase asset in fileCollection.FetchAssets())` 改为：
    ```
    foreach (AssetCollection collection in fileCollection.FetchAssetCollections())
    {
        foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())
        {
            IUnityObjectBase? asset = collection.TryGetAssetOnly(meta.PathID);
            if (asset is null || queued.Contains(asset)) continue;
            IExportCollection exportCollection = CreateCollection(asset);
            foreach (IUnityObjectBase element in exportCollection.Assets)
            {
                queued.Add(element);
            }
            collections.Add(exportCollection);
        }
    }
    ```
  - [x] SubTask 4.2: 验证 `queued` HashSet 去重语义保留：已处理的 asset 不重复创建 ExportCollection
  - [x] SubTask 4.3: 验证 `ProcessedAssetCollection.EnumerateAssetMetadata` 触发 `EnsureAssetsLoaded`（默认实现），但这些 collection 的资产是 Process 阶段新建的，未被 Unload，行为与改造前一致
  - [x] SubTask 4.4: 验证 `CreateCollection(asset)` 仍通过 `assetExporterStack.GetHandlerStack(asset.GetType())` 分发，`asset.GetType()` 在 `TryGetAssetOnly` 反序列化后可用

- [x] Task 5: 修改 ProjectAssetContainer 构造不传 FetchAssets
  - [x] SubTask 5.1: 在 [ProjectExporter.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L107) 把 `new ProjectAssetContainer(this, options, fileCollection.FetchAssets(), collections, skippedCollections, redirectMap)` 改为传入 `fileCollection.FetchAssetCollections()`（让容器按需 `TryGetAssetOnly` 而非全量 `FetchAssets`）
  - [x] SubTask 5.2: 在 [ProjectAssetContainer.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectAssetContainer.cs) 修改构造函数签名：把 `IEnumerable<IUnityObjectBase> assets` 参数改为 `IEnumerable<AssetCollection> collections`，内部按需遍历
  - [x] SubTask 5.3: 验证 `ProjectAssetContainer` 内 `assets.OfType<IBuildSettings>().FirstOrDefault()` 等查询改为遍历 collections + 元数据枚举 + 按需 `TryGetAssetOnly`
  - [x] SubTask 5.4: 验证 `m_assetCollections` 字典的填充逻辑：遍历每个 `IExportCollection.Assets`（这些 asset 在 `CreateCollection` 时已通过 `TryGetAssetOnly` 反序列化加入字典）

## 阶段四：PPtr 解引用懒加载

- [x] Task 6: ProjectYamlWalker 的 TryGetAsset 改为 TryGetAssetOnly
  - [x] SubTask 6.1: 在 [ProjectYamlWalker.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ProjectYamlWalker.cs#L62) 把 `CurrentAsset.Collection.TryGetAsset(pptr, out TAsset? asset)` 改为 `CurrentAsset.Collection.TryGetAssetOnly(pptr, out TAsset? asset)`
  - [x] SubTask 6.2: 验证 `TryGetAssetOnly(PPtr)` 重载在 P3 已实现（[AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 中）
  - [x] SubTask 6.3: 验证 PPtr 解引用失败时回退到 `MetaPtr.CreateMissingReference`，行为与改造前一致

- [x] Task 7: ContentHashWalker 的 TryGetAsset 改为 TryGetAssetOnly
  - [x] SubTask 7.1: 在 [ContentHashWalker.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ContentHashWalker.cs#L192) 把 `rootAsset.Collection?.TryGetAsset(pptr.FileID, pptr.PathID)` 改为 `rootAsset.Collection?.TryGetAssetOnly(pptr.FileID, pptr.PathID)`
  - [x] SubTask 7.2: 验证 `TryGetAssetOnly(int fileIndex, long pathID)` 重载在 P3 已实现

## 阶段五：EditorFormatProcessor Process 阶段移除 Convert

- [x] Task 8: EditorFormatProcessor.Process 移除 Convert 调用
  - [x] SubTask 8.1: 在 [EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs) 的 `Process` 方法中移除 sequential `foreach (asset in ...) Convert(asset)` 与 `Parallel.ForEach(..., ConvertAsync)` 调用
  - [x] SubTask 8.2: 保留 `PrepareForExport` 调用（重建 tagManager / assemblyManager 依赖）
  - [x] SubTask 8.3: 验证 `ProcessForExport` 仍在 Export 阶段通过 `container.EditorFormatConverter` 注入调用（P3 已实现，无需修改）
  - [x] SubTask 8.4: 验证破坏性清空（AnimationClip 的 `StreamedClip.Data.Clear()` / `DenseClip.SampleArray.Clear()` / `ConstantClip.Data.Clear()`）在 `Convert(IAnimationClip)` 内部执行，Unload 后重新反序列化的 AnimationClip 仍能正确解压

## 阶段六：验证

- [ ] Task 9: 编译与静态分析
  - [ ] SubTask 9.1: 使用 Rider MCP `build_solution` 编译整个解决方案，确认无错误（isSuccess=true, problems=[]）（**受 .NET 10 SDK 限制无法本地编译**）
  - [ ] SubTask 9.2: 使用 Rider MCP `get_file_problems` 检查所有改动文件无警告（**受 .NET 10 SDK 限制无法本地检查**）

- [x] Task 10: 行为一致性验证（通过代码分析完成）
  - [x] SubTask 10.1: 验证 `SerializedAssetCollection.UnloadAssets()` 后 `assets` 字典为空、`_assetsLoaded` 为 false、`_sourceFile`/`_factory`/`_originalDirectoryOverrides` 保留
  - [x] SubTask 10.2: 验证 Unload 后 `TryGetAssetOnly(pathID)` 重新反序列化的 asset 内容与原 asset 相等（同一 ObjectInfo 源，字段值一致）
  - [x] SubTask 10.3: 验证 `CreateCollections` 生成的 `List<IExportCollection>` 与改造前完全一致（每个 asset 都有对应的 ExportCollection）
  - [x] SubTask 10.4: 验证 `ProjectYamlWalker` 的 PPtr 解引用在 Unload 后的 collection 上能正确懒加载目标对象
  - [x] SubTask 10.5: 验证 `ContentHashWalker` 的 PPtr 解引用在 Unload 后的 collection 上能正确懒加载目标对象
  - [x] SubTask 10.6: 验证 `EditorFormatProcessor.ProcessForExport` 在 Export 阶段对每个 asset 调用 Convert/ConvertAsync，结果与原 Process 阶段调用一致

- [ ] Task 11: 单元测试（受环境限制）
  - [ ] SubTask 11.1: 运行 `AssetRipper.Assets.Tests` 单元测试（受 .NET 10 SDK 限制可能无法本地运行，建议在 CI 中运行）
  - [ ] SubTask 11.2: 运行 `AssetRipper.IO.Files.Tests` 单元测试
  - [ ] SubTask 11.3: 运行 `AssetRipper.Export.UnityProjects.Tests` 单元测试
  - [ ] SubTask 11.4: 运行 `AssetRipper.GUI.Web.Tests` 单元测试

- [ ] Task 12: 内存峰值验证
  - [ ] SubTask 12.1: 在大型项目（30GB+）上运行 Load + Process + Export，确认 Process 后 Unload 完成后内存从 7-8GB 降到 4-5GB
  - [ ] SubTask 12.2: 确认 Export 阶段 `CreateCollections` 执行期间，**任何** `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true（除非该 collection 的所有对象都被按需反序列化完）
  - [ ] SubTask 12.3: 确认 Export 阶段峰值内存从 10GB+ 降到 6-7GB（Load 阶段的 TypeTree 3-5GB + ProcessedAssetCollection + 按需反序列化的少量资产）
  - [ ] SubTask 12.4: 确认 Export 完成后 `GameBundle.Dispose` 能正确释放所有资源

# Task Dependencies

- Task 2 依赖 Task 1（需要 AssetCollection.UnloadAssets 虚方法）
- Task 3 依赖 Task 2（需要 SerializedAssetCollection.UnloadAssets 实现）
- Task 4 依赖 Task 2（CreateCollections 改造前需确认 Unload 后 TryGetAssetOnly 可用）
- Task 5 依赖 Task 4（ProjectAssetContainer 构造改造依赖 CreateCollections 改造）
- Task 6、Task 7 独立（PPtr 解引用改造，依赖 P3 已实现的 TryGetAssetOnly 重载）
- Task 8 独立（EditorFormatProcessor 改造，依赖 P3 已实现的 ProcessForExport）
- Task 9-12 依赖 Task 1-8 全部完成
- Task 1、Task 6、Task 7、Task 8 可并行（无依赖）
- Task 4、Task 5 需顺序执行（ProjectAssetContainer 依赖 CreateCollections 改造）

# 备注

## 已知限制

- Load 阶段 TypeTree 立即解析仍占 3-5GB（需后续 spec 优化，超出 P4 范围）
- `ProcessedAssetCollection` 不参与 Unload（其资产是 Process 阶段新建的，不能释放）
- `AudioMixerExportCollection` / `ScriptExportCollection` / `OcclusionCullingDataExtensions` 中的 `FetchAssetsInHierarchy` 调用仍会触发全量加载，但这些通常在 `CreateCollections` 之后执行，此时已通过 `TryGetAssetOnly` 反序列化必要资产，`FetchAssetsInHierarchy` 触发的 `EnsureAssetsLoaded` 只会反序列化剩余对象。后续可进一步优化为 `FetchAssetsInHierarchyOnly`，但超出 P4 范围
- `PathIdMapExporter` 的 PostExport 阶段 `FetchAssetCollections()` + `foreach (asset in collection)` 仍触发全量加载，但这是 PostExport 阶段，不影响 Export 主流程内存峰值。后续可优化

## 单元测试运行受限说明

- 本地环境仅有 .NET SDK 9.0.306，无 .NET 10 SDK
- 项目全局 TargetFramework 为 net10.0（见 Source/Directory.Build.props）
- Rider 通过 NuGet reference assemblies 编译成功，但 dotnet test 启动的 testhost.exe 需要 .NET 10 runtime
- 建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证

## 风险评估

1. **Unload 后引用不相等风险**：Unload 后重新反序列化的 asset 是新对象实例，与 Process 阶段的 asset 引用不相等。若代码中用 `ReferenceEquals` 或 `HashSet<IUnityObjectBase>` 比较资产，可能出错。需检查 `m_assetCollections` 字典、`queued` HashSet、`m_redirectMap` 等是否在 Unload 前后混用引用。**缓解**：`CreateCollections` 在 Unload 后执行，所有引用都是新对象，不存在混用。
2. **EditorFormat Convert 幂等性风险**：`ProcessForExport` 声明幂等，但 AnimationClip 的破坏性清空（`StreamedClip.Data.Clear()`）在第二次调用时会因 `Data` 已空而 no-op。需确认 Unload 后重新反序列化的 AnimationClip 的 `StreamedClip.Data` 未被清空（因为 Process 阶段不再执行 Convert）。**缓解**：Task 8 明确移除 Process 阶段的 Convert 调用，破坏性清空只在 Export 阶段的 `ProcessForExport` 中执行。
3. **CreateCollection 类型分发风险**：`asset.GetType()` 需要 asset 已反序列化。`TryGetAssetOnly` 反序列化后 `GetType()` 可用，与原 `FetchAssets` 行为一致。**缓解**：无需额外处理。
