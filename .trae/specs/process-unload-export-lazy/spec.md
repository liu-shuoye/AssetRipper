# 终极方案：Process 后 Unload + Export 懒加载 Spec（P4）

## Why

AssetRipper 经过 P1/P2/P3 三轮优化后，Load 阶段内存约 6.7GB（含 TypeTree 解析的 3-5GB，超出本 spec 范围），Process 阶段内存峰值已从 10.4GB 降到 7-8GB。但 **Export 阶段仍是全量反序列化的最后一道关口**：

- [ProjectExporter.CreateCollections](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L155) 通过 `fileCollection.FetchAssets()` → `GetEnumerator()` → `EnsureAssetsLoaded()` **一次性把所有 30 万对象反序列化到 `assets` 字典**，内存峰值瞬间回到 10GB+。
- Process 阶段已经只反序列化必要对象，但这些对象在 Process 完成后**一直驻留内存**直到 Export 结束。
- Export 完成后无 `UnloadAssets`，所有资产直到 `GameBundle.Dispose` 才释放。

memory（`project_memory.md`）记录的"终极方案"正是解决此问题：
> Process 后 UnloadAssets + Export 逐 collection 懒加载 + 重新应用 EditorFormat 修改。需要：CreateCollections 用 ClassID 分类而非 `is` 类型检查；EditorFormat 修改延迟到 Export 时重新计算（Transform/GameObject/AnimationClip 等修改均为确定性，可重算）。

本次改造通过三个关键步骤彻底根治 Export 阶段全量反序列化：
1. **Process 完成后对 `SerializedAssetCollection` 调用 `UnloadAssets()`**，释放 Process 阶段已反序列化的对象，让 Load/Process 阶段的内存峰值在 Export 开始前下降。
2. **`CreateCollections` 改用 `EnumerateAssetMetadata` + 按需 `TryGetAssetOnly`**，避免一次性遍历所有资产对象。
3. **`ProjectYamlWalker` / `ContentHashWalker` 的 `TryGetAsset` 改为 `TryGetAssetOnly`**，让 PPtr 解引用不触发目标 collection 的全量反序列化。

`EditorFormatProcessor` 的"导出时按需转换"机制（`PrepareForExport` + `ProcessForExport`）已在 P3 阶段实现并验证，本 spec 直接复用，不再重复实现。

## What Changes

### 阶段一：Process 后 UnloadAssets

- **新增 `AssetCollection.UnloadAssets()` 虚方法**：默认实现为空操作（兼容 `ProcessedAssetCollection` / `VirtualAssetCollection` 等非懒加载 collection）。`SerializedAssetCollection` 重写为：清空 `assets` 字典、重置 `_assetsLoaded = false`、**保留** `_sourceFile` / `_factory` / `_originalDirectoryOverrides`，让后续可重新懒加载。
- **在 `ExportHandler.Export` 中调用 `UnloadAssets`**：在 Process 阶段所有 processor 完成后、`PrepareForExport` 之前，遍历 `gameData.GameBundle.FetchAssetCollections()` 对每个 `SerializedAssetCollection` 调用 `UnloadAssets()`。`ProcessedAssetCollection` 不受影响（默认空实现）。
- **保留 collection 级别 OriginalDirectory 映射**：`_originalDirectoryOverrides` 在 Unload 后仍保留，因为 `UnityObjectBase.OriginalDirectory` getter 依赖此映射（P2 实现）。

### 阶段二：CreateCollections 改用元数据枚举

- **`ProjectExporter.CreateCollections` 改用 `EnumerateAssetMetadata`**：把 `foreach (IUnityObjectBase asset in fileCollection.FetchAssets())` 改为遍历 `FetchAssetCollections()` + 每个集合的 `EnumerateAssetMetadata()`，对每个 meta 调用 `TryGetAssetOnly(meta.PathID)` 反序列化单个资产，再调用 `CreateCollection(asset)`。
- **保留 `queued` HashSet 去重语义**：对反序列化后的 asset 仍用 `queued.Contains(asset)` 判断是否已处理。
- **不去重 ProcessedAssetCollection 的资产**：`ProcessedAssetCollection` 的 `EnumerateAssetMetadata` 默认实现触发 `EnsureAssetsLoaded`（这些 collection 本身已加载，资产是 Process 阶段新建的，不在 Unload 范围内）。
- **ApplyDeduplication 内 `ContentHashWalker` 的 `TryGetAsset` 改为 `TryGetAssetOnly`**：去重阶段的 PPtr 解引用不应触发目标 collection 的全量反序列化。

### 阶段三：Export 主循环 PPtr 解引用懒加载

- **`ProjectYamlWalker.CreateYamlNodeForPPtr` 的 `TryGetAsset` 改为 `TryGetAssetOnly`**：让 WalkEditor 遇到 PPtr 字段时只反序列化目标对象，不触发目标 collection 的全量反序列化。
- **`ProjectAssetContainer` 构造时 `fileCollection.FetchAssets()` 改为按需枚举**：构造 `ProjectAssetContainer` 时不再传入 `fileCollection.FetchAssets()`（会触发全量加载），改为传入 `fileCollection.FetchAssetCollections()`，让容器在需要时按需 `TryGetAssetOnly`。
- **各 ExportCollection 的 `Assets` / `ExportableAssets` 属性保持原语义**：这些属性返回的是已反序列化的 asset 引用（在 `CreateCollection` 时已通过 `TryGetAssetOnly` 反序列化加入字典），不会触发新的全量加载。

### 阶段四：EditorFormat 修改复用现有机制

- **Process 阶段不再执行 Convert / ConvertAsync**：把 `EditorFormatProcessor.Process` 中的 sequential `Convert` + Parallel `ConvertAsync` 调用移除，只保留 `PrepareForExport` 重建依赖。Convert 改为在 Export 阶段通过 `ProcessForExport` 按需调用。
- **`ProcessForExport` 已在 P3 实现**：[EditorFormatProcessor.ProcessForExport](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L159) 在 `ProjectYamlWalker.ExportYamlDocument` 中、`WalkEditor` 之前对单个资产调用，已通过 `container.EditorFormatConverter` 注入。
- **破坏性内存优化（AnimationClip 源数据清空等）移到 Export 阶段**：原 Process 阶段对 AnimationClip 的 `StreamedClip.Data.Clear()` / `DenseClip.SampleArray.Clear()` / `ConstantClip.Data.Clear()` 改在 `ProcessForExport` 调用时执行。这样 Unload 后重新反序列化的 AnimationClip 仍能正确解压。

### 兼容性

- **非破坏性**：`UnloadAssets` 是新增虚方法，默认空实现。`EnumerateAssetMetadata` / `TryGetAssetOnly` 是 P2/P3 已有的 API。`ProcessForExport` 是 P3 已有的 API。
- **`ProcessedAssetCollection` 不受影响**：Process 阶段新建的资产（SceneHierarchyObject / PrefabHierarchyObject / ScriptableObjectGroup 等）不会被 Unload，因为它们不在 `SerializedAssetCollection` 中。
- **`OriginalDirectory` 持久化**：collection 级别的 `_originalDirectoryOverrides` 在 Unload 后保留，`UnityObjectBase.OriginalDirectory` getter 仍能正确回退读取。
- **测试兼容**：现有单元测试不修改。Unload 后重新反序列化的 asset 与原 asset 引用不相等（新对象），但内容相等（同一 ObjectInfo 源）。

## Impact

- Affected specs:
  - [lazy-load-and-dispose](file:///e:/Project/Rider/AssetRipper/.trae/specs/lazy-load-and-dispose/spec.md)（P1：ObjectInfo 懒加载 + 显式 Dispose）—— 复用 P1 引入的 `ObjectInfo.LoadObjectData()` / `ReleaseDataStream()`。
  - [metadata-driven-lazy-processing](file:///e:/Project/Rider/AssetRipper/.trae/specs/metadata-driven-lazy-processing/spec.md)（P2：元数据驱动懒处理）—— 复用 P2 引入的 `EnumerateAssetMetadata()` / `TryGetAssetOnly(long pathID)` / `SetOriginalDirectory()`。
  - [cross-collection-pptr-lazy-resolution](file:///e:/Project/Rider/AssetRipper/.trae/specs/cross-collection-pptr-lazy-resolution/spec.md)（P3：跨 collection PPtr 懒解析）—— 复用 P3 引入的 `TryGetAssetOnly(int fileIndex, long pathID)` / `PPtrExtensions.TryGetAssetOnly<T>`。
- Affected code:
  - [Source/AssetRipper.Assets/Collections/AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs)（新增 `UnloadAssets` 虚方法，默认空实现）
  - [Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs)（重写 `UnloadAssets`：清空 assets 字典、重置 `_assetsLoaded`、保留 `_sourceFile`/`_factory`/`_originalDirectoryOverrides`）
  - [Source/AssetRipper.Export.UnityProjects/ExportHandler.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs)（Process 完成后调用 `UnloadAssets`）
  - [Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs)（`CreateCollections` 改用 `EnumerateAssetMetadata` + 按需 `TryGetAssetOnly`；`ProjectAssetContainer` 构造不再传 `FetchAssets()`）
  - [Source/AssetRipper.Export.UnityProjects/Project/ProjectYamlWalker.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ProjectYamlWalker.cs)（`CreateYamlNodeForPPtr` 的 `TryGetAsset` 改为 `TryGetAssetOnly`）
  - [Source/AssetRipper.Export.UnityProjects/Project/ContentHashWalker.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ContentHashWalker.cs)（`TryGetAsset` 改为 `TryGetAssetOnly`）
  - [Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs)（`Process` 移除 Convert/ConvertAsync 调用，破坏性清空移到 `Convert`/`ConvertAsync` 内部）

## ADDED Requirements

### Requirement: SerializedAssetCollection 可 Unload 并重新懒加载

系统 SHALL 提供 `AssetCollection.UnloadAssets()` 虚方法，让 `SerializedAssetCollection` 能在 Process 完成后清空已反序列化的 `assets` 字典，但保留重新懒加载所需的所有元数据与依赖。

#### Scenario: Process 后 Unload 释放内存

- **WHEN** Process 阶段所有 processor 完成后，调用 `serializedAssetCollection.UnloadAssets()`
- **THEN** 系统清空 `assets` 字典中所有已反序列化的 `IUnityObjectBase` 引用
- **AND** 重置 `_assetsLoaded = false`，让后续访问能重新触发 `EnsureAssetsLoaded`
- **AND** 保留 `_sourceFile` / `_factory` / `_originalDirectoryOverrides` / `dependencies`，让重新懒加载成为可能
- **AND** 保留 `Scene` 等 collection 级别元数据

#### Scenario: Unload 后重新访问触发懒加载

- **WHEN** Unload 后调用 `TryGetAssetOnly(pathID)` 或 `GetEnumerator()`
- **THEN** 系统从 `_sourceFile.Objects` 重新读取 ObjectInfo 并反序列化，行为与首次加载一致
- **AND** 重新反序列化的 asset 是新对象实例（引用不相等），但内容与原 asset 相等（同一 ObjectInfo 源）

#### Scenario: ProcessedAssetCollection 不受 Unload 影响

- **WHEN** 调用 `processedAssetCollection.UnloadAssets()`
- **THEN** 默认实现为空操作（不清理任何资产），因为 ProcessedAssetCollection 持有 Process 阶段新建的资产（SceneHierarchyObject 等），不能被释放

#### Scenario: Unload 幂等

- **WHEN** 同一个 `SerializedAssetCollection` 被多次 `UnloadAssets()`
- **THEN** 第二次及之后的调用直接返回，不抛异常（`_assetsLoaded` 已为 false，`assets` 字典已空）

### Requirement: ExportHandler 在 Process 后调用 UnloadAssets

`ExportHandler.Export` SHALL 在 Process 阶段所有 processor 完成后、`PrepareForExport` 之前，遍历 `gameData.GameBundle.FetchAssetCollections()` 对每个 `AssetCollection` 调用 `UnloadAssets()`。

#### Scenario: Process 完成后 Unload

- **WHEN** `ExportHandler.Export` 中所有 processor 执行完毕
- **THEN** 系统遍历 `gameData.GameBundle.FetchAssetCollections()`
- **AND** 对每个 collection 调用 `UnloadAssets()`（`SerializedAssetCollection` 实际清空，其他 collection 空操作）
- **AND** 随后调用 `EditorFormatProcessor.PrepareForExport(gameData)` 重建依赖
- **AND** 再调用 `projectExporter.Export(...)` 进入 Export 阶段

#### Scenario: Unload 后内存峰值下降

- **WHEN** Unload 完成后立即记录内存诊断
- **THEN** 托管内存应显著下降（预期从 7-8GB 降到 4-5GB，仅保留 TypeTree 与 ProcessedAssetCollection）
- **AND** 工作集下降可能滞后，依赖 GC 回收

### Requirement: CreateCollections 用元数据枚举替代 FetchAssets

`ProjectExporter.CreateCollections` SHALL 用 `FetchAssetCollections()` + `EnumerateAssetMetadata()` + 按需 `TryGetAssetOnly` 替代 `FetchAssets()` 全量遍历，避免一次性触发所有 collection 的 `EnsureAssetsLoaded`。

#### Scenario: CreateCollections 不触发全量反序列化

- **WHEN** 处理一个包含 30 万对象的大型项目
- **THEN** `CreateCollections` 执行期间，**任何** `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true（除非该 collection 的所有对象都被按需反序列化完，但这不是预期场景）
- **AND** 仅对 `CreateCollection` 需要的 asset 调用 `TryGetAssetOnly(meta.PathID)` 反序列化单个对象
- **AND** `queued` HashSet 去重语义保留（已处理的 asset 不重复创建 ExportCollection）

#### Scenario: 行为与改造前一致

- **WHEN** `CreateCollections` 处理完成后
- **THEN** 生成的 `List<IExportCollection>` 与改造前完全一致（每个 asset 都有对应的 ExportCollection）
- **AND** `queued` HashSet 的内容与改造前完全一致
- **AND** `skippedCollections` / `redirectMap`（若启用去重）与改造前完全一致

#### Scenario: ProcessedAssetCollection 正常遍历

- **WHEN** `CreateCollections` 遍历到 `ProcessedAssetCollection`
- **THEN** `ProcessedAssetCollection.EnumerateAssetMetadata` 触发 `EnsureAssetsLoaded`（默认实现），但这些 collection 的资产是 Process 阶段新建的，未被 Unload
- **AND** 行为与改造前一致

### Requirement: ProjectYamlWalker PPtr 解引用懒加载

`ProjectYamlWalker.CreateYamlNodeForPPtr` SHALL 用 `TryGetAssetOnly` 替代 `TryGetAsset`，让 WalkEditor 遇到 PPtr 字段时只反序列化目标对象，不触发目标 collection 的全量反序列化。

#### Scenario: PPtr 解引用不触发全量加载

- **WHEN** `WalkEditor` 遇到 PPtr 字段，调用 `CreateYamlNodeForPPtr`
- **AND** PPtr 指向的资产在另一个 `SerializedAssetCollection` 中且该 collection 未全量加载
- **THEN** 系统调用 `CurrentAsset.Collection.TryGetAssetOnly(pptr, out TAsset? asset)` 替代 `TryGetAsset`
- **AND** 只反序列化 PPtr 指向的单个对象
- **AND** 不触发目标 collection 的 `EnsureAssetsLoaded`

#### Scenario: PPtr 解引用失败回退到 MissingReference

- **WHEN** `TryGetAssetOnly` 返回 false（PPtr 目标不存在）
- **THEN** 系统用 `GetClassID(typeof(TAsset))` + `container.ToExportType(typeof(TAsset))` 构造 `MetaPtr.CreateMissingReference`
- **AND** 行为与改造前一致

### Requirement: ContentHashWalker PPtr 解引用懒加载

`ContentHashWalker` 在 `ApplyDeduplication` 阶段 SHALL 用 `TryGetAssetOnly` 替代 `TryGetAsset`，让去重阶段的 PPtr 解引用不触发目标 collection 的全量反序列化。

#### Scenario: 去重 PPtr 解引用不触发全量加载

- **WHEN** `ApplyDeduplication` 计算 primary asset 的内容哈希，`ContentHashWalker.VisitPPtr` 遇到 PPtr
- **THEN** 系统调用 `rootAsset.Collection?.TryGetAssetOnly(pptr.FileID, pptr.PathID)` 替代 `TryGetAsset`
- **AND** 只反序列化 PPtr 指向的单个对象
- **AND** 不触发目标 collection 的 `EnsureAssetsLoaded`

### Requirement: EditorFormatProcessor Process 阶段不执行 Convert

`EditorFormatProcessor.Process` SHALL 移除 sequential `Convert` + Parallel `ConvertAsync` 调用，只保留 `PrepareForExport` 重建依赖。Convert 改为在 Export 阶段通过 `ProcessForExport` 按需调用。

#### Scenario: Process 阶段不执行 Convert

- **WHEN** `EditorFormatProcessor.Process` 执行
- **THEN** 不调用任何 `Convert(asset)` 或 `ConvertAsync(asset)`
- **AND** 不触发 `GetReleaseAssets` 或元数据枚举（因为 Convert 不再在 Process 阶段执行）
- **AND** Process 阶段内存峰值进一步下降

#### Scenario: Export 阶段按需 Convert

- **WHEN** `ProjectYamlWalker.ExportYamlDocument` 在 `WalkEditor` 之前调用 `container.EditorFormatConverter?.Invoke(asset)`
- **THEN** 触发 `EditorFormatProcessor.ProcessForExport(asset)`
- **AND** 对 `ExportStageClassIDs` 中的 ClassID 调用 `Convert(asset)` + `ConvertAsync(asset)`
- **AND** 破坏性内存优化（AnimationClip 源数据清空等）在 Convert 内部执行，清空后源数据释放

#### Scenario: 破坏性清空在 Convert 内执行

- **WHEN** `Convert(IAnimationClip)` 执行
- **THEN** 解压 StreamedClip/DenseClip/ConstantClip 到 PositionCurves/RotationCurves 等字段后，清空源数据
- **AND** 清空操作在 `ProcessForExport` 调用时执行（Export 阶段），不在 Process 阶段执行
- **AND** Unload 后重新反序列化的 AnimationClip 仍能正确解压（因为源数据未在 Process 阶段被清空）

## MODIFIED Requirements

### Requirement: AssetCollection 资产生命周期

[AssetCollection](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 原行为（P3 后）：资产一旦反序列化加入 `assets` 字典就常驻内存，直到 `Dispose` 才释放。新行为：新增 `UnloadAssets()` 虚方法，让 `SerializedAssetCollection` 能在 Process 完成后清空字典、重置 `_assetsLoaded`，让后续访问重新触发懒加载。原 `Dispose` 行为不变（释放 `_sourceFile` / `_factory` 等底层引用）。

### Requirement: ProjectExporter.CreateCollections 资产遍历

[ProjectExporter.CreateCollections](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L149-180) 原行为：`foreach (IUnityObjectBase asset in fileCollection.FetchAssets())` 触发 `GetEnumerator` → `EnsureAssetsLoaded` 全量反序列化。新行为：遍历 `FetchAssetCollections()` + 每个集合的 `EnumerateAssetMetadata()`，对每个 meta 调用 `TryGetAssetOnly(meta.PathID)` 反序列化单个资产。

### Requirement: ProjectYamlWalker PPtr 解引用

[ProjectYamlWalker.CreateYamlNodeForPPtr](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ProjectYamlWalker.cs#L56-72) 原行为：`CurrentAsset.Collection.TryGetAsset(pptr, out TAsset? asset)` 触发目标 collection 的 `EnsureAssetsLoaded`。新行为：`CurrentAsset.Collection.TryGetAssetOnly(pptr, out TAsset? asset)` 只反序列化目标对象。

### Requirement: ContentHashWalker PPtr 解引用

[ContentHashWalker](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/ContentHashWalker.cs#L192) 原行为：`rootAsset.Collection?.TryGetAsset(pptr.FileID, pptr.PathID)` 触发目标 collection 的 `EnsureAssetsLoaded`。新行为：`rootAsset.Collection?.TryGetAssetOnly(pptr.FileID, pptr.PathID)` 只反序列化目标对象。

### Requirement: EditorFormatProcessor.Process 转换调用

[EditorFormatProcessor.Process](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs) 原行为（P3 后）：在 Process 阶段 sequential `Convert` + Parallel `ConvertAsync` 所有 `ExportStageClassIDs` 资产。新行为：Process 阶段不执行 Convert/ConvertAsync，仅 `PrepareForExport` 重建依赖；Convert/ConvertAsync 在 Export 阶段通过 `ProcessForExport` 按需调用。

## REMOVED Requirements

（无）
