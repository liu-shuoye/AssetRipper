# 跨 collection PPtr 懒解析 Spec（P3：Processing 阶段全量反序列化根治）

## Why

AssetRipper 在 P2（metadata-driven-lazy-processing）完成后，加载 3.3GB 项目时 Load 阶段托管内存为 6.7GB（已含 TypeTree 解析的 3-5GB，超出本 spec 范围），但 Processing 阶段仍暴涨到 10.4GB 托管 / 12GB 工作集：

```
[内存诊断] Process后 - SceneDefinitionProcessor:  托管: 6791.9 MB  | 工作集: 7616.3 MB   （+25 MB，P2 已优化）
[内存诊断] Process后 - OriginalPathProcessor:     托管: 10363.5 MB | 工作集: 12922.2 MB  （+3571 MB，P2 未根治）
[内存诊断] Process后 - EditorFormatProcessor:     托管: 10410.9 MB | 工作集: 12332.3 MB  （+43 MB，依赖 OriginalPathProcessor 已触发全量反序列化）
[内存诊断] Process完成:                           托管: 10421.5 MB | 工作集: 12008.1 MB
```

源码审查定位到 **OriginalPathProcessor 的 PPtr 解引用是 3.6GB 增长的真正根源**：

- [OriginalPathProcessor.SetOriginalPaths(IResourceManager)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L134-L160) 遍历 `manager.Container`，对每个 PPtr 调用 `kvp.Value.TryGetAsset(manager.Collection)`。
- [OriginalPathProcessor.SetOriginalPaths(IAssetBundle, ...)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L172-L227) 遍历 `bundle.Container`，对每个 PPtr 调用 `kvp.Value.Asset.TryGetAsset(bundle.Collection)`。
- 这两个 `TryGetAsset` 调用最终走 [AssetCollection.TryGetAsset(int fileIndex, long pathID)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs#L335-L347) → `file.TryGetAsset(pathID, out asset)` → [EnsureAssetsLoaded()](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs#L95-L131)，**触发目标 collection 的全量反序列化**。

AssetBundle.Container / ResourceManager.Container 中的 PPtr 通常引用**其他 collection** 中的资产（FileID != 0）。大型项目有大量 AssetBundle，每个 bundle 的 Container 引用大量跨 collection 资产，导致几乎所有 collection 都被全量反序列化。这就是 P2 优化后 OriginalPathProcessor 仍暴涨 3.6GB 的原因。

更隐蔽的问题：**EditorFormatProcessor 当前内存增长仅 43MB，是因为 OriginalPathProcessor 已经触发全量反序列化、EditorFormatProcessor 共享了已填充的 `assets` 字典**。一旦 P3 修复 OriginalPathProcessor，EditorFormatProcessor 的 [GetReleaseAssets](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L98-L104) 通过 `c.SelectMany(c => c)` 触发 `GetEnumerator` → `EnsureAssetsLoaded` 会成为新的全量反序列化触发点，内存增长预计将达到 3-5GB。

本次改造通过 **跨 collection PPtr 懒解析** + **EditorFormatProcessor 元数据驱动**，让 Processing 阶段不再触发任何 `EnsureAssetsLoaded`，把内存峰值从 10.4GB 降到 7-8GB。

## What Changes

### 新增基础设施：跨 collection 单对象懒反序列化

- **`AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 虚方法**：与 `TryGetAsset(int fileIndex, long pathID)` 对应的懒加载版本。默认实现回退到 `TryGetAsset(fileIndex, pathID)`（兼容老代码）；调用路径为 `file.TryGetAssetOnly(pathID)` 而不是 `file.TryGetAsset(pathID)`，避免触发目标 collection 的 `EnsureAssetsLoaded`。
- **`AssetCollection.TryGetAssetOnly<T>(int fileIndex, long pathID)` 泛型重载**：与现有 `TryGetAsset<T>(int fileIndex, long pathID)` 对应。
- **`AssetCollection.TryGetAssetOnly(PPtr pptr)` 与 `TryGetAssetOnly<T>(PPtr<T> pptr)` 重载**：与现有 `TryGetAsset(PPtr)` / `TryGetAsset<T>(PPtr<T>)` 对应，让 PPtr 解引用支持懒加载模式。
- **`PPtrExtensions.TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法**：与现有 `PPtrExtensions.TryGetAsset<T>` 对应，让 `IPPtr<T>.TryGetAssetOnly(file)` 调用方式与 `TryGetAsset(file)` 完全对称，OriginalPathProcessor 只需把 `TryGetAsset` 改为 `TryGetAssetOnly`。

### 改造 OriginalPathProcessor：PPtr 解引用用懒解析

- **`SetOriginalPaths(IResourceManager manager)`**：`kvp.Value.TryGetAsset(manager.Collection)` 改为 `kvp.Value.TryGetAssetOnly(manager.Collection)`，让 ResourceManager.Container 中的跨 collection PPtr 只反序列化目标对象，不触发目标 collection 的全量反序列化。
- **`SetOriginalPaths(IAssetBundle bundle, BundledAssetsExportMode)`**：`kvp.Value.Asset.TryGetAsset(bundle.Collection)` 改为 `kvp.Value.Asset.TryGetAssetOnly(bundle.Collection)`，让 AssetBundle.Container 中的跨 collection PPtr 只反序列化目标对象。
- **ContainerExport 分支第二段 foreach 设置 OriginalDirectory**：用 `EnumerateAssetMetadata()` 替代 `foreach (IUnityObjectBase asset in collection)`，对 ClassID == 48 (IShader) 跳过，其余用 `collection.SetOriginalDirectory(meta.PathID, originalDirectory)` 设置，**完全不反序列化**。
- **ContainerExport 分支第一段 count 判断**：保留原 `collection.Count(asset => asset.GetBestName() != asset.ClassName)` 逻辑，但改为基于元数据枚举 + 按需 `TryGetAssetOnly` 逐个解引用检查 `GetBestName()`，避免触发全量反序列化。这是 ContainerExport 模式下唯一仍需解引用的少量资产（最多检查到 count > 30 即可短路返回）。

### 改造 EditorFormatProcessor：元数据驱动 Convert

- **`GetReleaseAssets` 改为元数据枚举 + 按需 `TryGetAssetOnly`**：用 `EnumerateAssetMetadata()` 替代 `c.SelectMany(c => c)`，只对 Convert / ConvertAsync 涉及的 ClassID 调用 `TryGetAssetOnly`，其余 ClassID 跳过。
- **Convert / ConvertAsync 涉及的 ClassID 集合**（基于源码审查）：
  - Convert（sequential）：1 (GameObject) / 所有 Renderer 派生 ClassID / 687078895 (SpriteAtlas) / 74 (AnimationClip) / 196 (NavMeshSettings) / 129 (PlayerSettings via TypeTreeObject)
  - ConvertAsync（Parallel）：4 (Transform) / 43 (Mesh) / 218 (Terrain) / 320 (PlayableDirector) / 142 (AssetBundle) / 30 (GraphicsSettings) / 47 (QualitySettings) / 19 (Physics2DSettings) / 157 (LightmapSettings) / 850595691 (LightingSettings) / 310 (UnityConnectSettings)
  - Renderer 派生 ClassID：23 (MeshRenderer) / 137 (SkinnedMeshRenderer) / 199 (ParticleSystemRenderer) / 212 (SpriteRenderer) / 26 (ParticleRenderer) / 96 (TrailRenderer) / 120 (LineRenderer) / 161 (ClothRenderer) / 222 (CanvasRenderer) / 227 (BillboardRenderer) / 483693784 (TilemapRenderer) / 1971053207 (SpriteShapeRenderer) / 1931382933 (UIRenderer) / 73398921 (VFXRenderer)
- **保留 Convert / ConvertAsync 内部 switch 逻辑不变**：仍用 `switch asset` 模式匹配，确保行为一致。元数据枚举只用于过滤候选 ClassID，减少不必要的反序列化。

### 兼容性

- **非破坏性**：`AssetCollection.TryGetAsset(int fileIndex, long pathID)` / `TryGetAsset(PPtr)` / `TryGetAsset<T>(PPtr<T>)` 行为不变，仍触发 `EnsureAssetsLoaded`，老代码兼容。
- **新增 API 是纯新增**：`TryGetAssetOnly(int fileIndex, long pathID)` / `TryGetAssetOnly(PPtr)` / `TryGetAssetOnly<T>(PPtr<T>)` / `PPtrExtensions.TryGetAssetOnly<T>` 为新增虚方法与扩展方法，默认实现回退到原行为。
- **`IPPtr<T>` 接口不变**：不新增接口方法，避免破坏所有生成的 PPtr 类型。改用 `PPtrExtensions.TryGetAssetOnly<T>` 扩展方法实现 PPtr 懒解析。
- **OriginalPathProcessor 行为一致**：`SetOriginalPaths` 设置的 `OriginalPath` / `OriginalDirectory` / `AssetBundleName` 与改造前完全一致，只是反序列化路径不同（懒解析 vs 全量）。
- **EditorFormatProcessor 行为一致**：Convert / ConvertAsync 处理的 asset 集合与改造前完全一致（基于 ClassID 集合覆盖所有需要处理的类型），只是反序列化路径不同。
- **测试兼容**：现有单元测试不修改。

## Impact

- Affected specs:
  - [metadata-driven-lazy-processing](file:///e:/Project/Rider/AssetRipper/.trae/specs/metadata-driven-lazy-processing/spec.md)（P2：元数据驱动懒处理）—— 本 spec 是 P2 的延续，复用 P2 引入的 `EnumerateAssetMetadata()` / `TryGetAssetOnly(long pathID)` / `SetOriginalDirectory()`。
  - [lazy-load-and-dispose](file:///e:/Project/Rider/AssetRipper/.trae/specs/lazy-load-and-dispose/spec.md)（P1：ObjectInfo 懒加载 + 显式 Dispose）—— 复用 P1 引入的 `ObjectInfo.LoadObjectData()`。
- Affected code:
  - [Source/AssetRipper.Assets/Collections/AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs)（新增 `TryGetAssetOnly(int fileIndex, long pathID)` / `TryGetAssetOnly<T>(int fileIndex, long pathID)` / `TryGetAssetOnly(PPtr)` / `TryGetAssetOnly<T>(PPtr<T>)` 虚方法）
  - [Source/AssetRipper.SourceGenerated.Extensions/PPtrExtensions.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.SourceGenerated.Extensions/PPtrExtensions.cs)（新增 `TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法）
  - [Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs)（`SetOriginalPaths` 两个重载用 `TryGetAssetOnly` 替代 `TryGetAsset`；ContainerExport 分支第二段用元数据驱动；第一段 count 判断用元数据 + 短路）
  - [Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs)（`GetReleaseAssets` 改为元数据枚举 + 按需 `TryGetAssetOnly`；新增 `NeedsConversion(int classID)` 判断方法）

## ADDED Requirements

### Requirement: 跨 collection 单对象懒反序列化

系统 SHALL 提供 `AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 方法，与 `TryGetAsset(int fileIndex, long pathID)` 行为对称，但调用 `file.TryGetAssetOnly(pathID)` 而不是 `file.TryGetAsset(pathID)`，避免触发目标 collection 的 `EnsureAssetsLoaded`。

#### Scenario: 跨 collection PPtr 解引用不触发全量反序列化

- **WHEN** 调用 `collection.TryGetAssetOnly(fileIndex, pathID)` 且 `fileIndex != 0` 且目标 collection 是 `SerializedAssetCollection` 且 `_assetsLoaded` 为 false
- **THEN** 系统通过 `TryGetDependency(fileIndex)` 找到目标 collection
- **AND** 调用目标 collection 的 `TryGetAssetOnly(pathID)`（只反序列化单个对象，不触发 `EnsureAssetsLoaded`）
- **AND** 返回反序列化后的 asset（或 null 如果找不到）

#### Scenario: 同一 collection 内 PPtr 解引用

- **WHEN** 调用 `collection.TryGetAssetOnly(fileIndex, pathID)` 且 `fileIndex == 0`
- **THEN** 系统调用 `this.TryGetAssetOnly(pathID)`（同 collection 单对象反序列化，P2 已实现）
- **AND** 不触发 `EnsureAssetsLoaded`

#### Scenario: 默认实现回退兼容

- **WHEN** 调用 `AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 且目标 collection 未重写 `TryGetAssetOnly(long pathID)`（如 `ProcessedAssetCollection`、`VirtualAssetCollection`）
- **THEN** 系统回退到 `TryGetAsset(fileIndex, pathID)`（触发 `EnsureAssetsLoaded`），保证老代码行为不变

### Requirement: PPtr 扩展方法懒解析

系统 SHALL 在 `PPtrExtensions` 中提供 `TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法，与现有 `TryGetAsset<T>` 对称，让 PPtr 解引用支持懒加载模式。

#### Scenario: PPtrExtensions.TryGetAssetOnly 调用路径

- **WHEN** 调用 `pptr.TryGetAssetOnly(collection)` 其中 `pptr` 是 `IPPtr<T>`
- **THEN** 系统调用 `collection.TryGetAssetOnly<T>(pptr.FileID, pptr.PathID)`
- **AND** 进一步调用 `file.TryGetAssetOnly<T>(pathID)`（跨 collection 懒解析）
- **AND** 不触发目标 collection 的 `EnsureAssetsLoaded`

#### Scenario: 与 TryGetAsset 行为对称

- **WHEN** 同一 PPtr 在同一 collection 上分别调用 `TryGetAsset` 与 `TryGetAssetOnly`
- **THEN** 两者返回的 asset 引用相等（同一反序列化对象）
- **AND** 区别仅在反序列化路径：`TryGetAsset` 触发全量 `EnsureAssetsLoaded`，`TryGetAssetOnly` 只反序列化单个对象

### Requirement: OriginalPathProcessor.SetOriginalPaths 用 PPtr 懒解析

`SetOriginalPaths(IResourceManager)` 与 `SetOriginalPaths(IAssetBundle, BundledAssetsExportMode)` SHALL 用 `kvp.Value.TryGetAssetOnly(...)` 替代 `kvp.Value.TryGetAsset(...)`，让 Container 中的跨 collection PPtr 只反序列化目标对象。

#### Scenario: ResourceManager.Container PPtr 懒解析

- **WHEN** 处理 `IResourceManager.Container` 中的 PPtr 引用
- **THEN** 调用 `kvp.Value.TryGetAssetOnly(manager.Collection)` 替代 `kvp.Value.TryGetAsset(manager.Collection)`
- **AND** 不触发任何 `SerializedAssetCollection` 的 `EnsureAssetsLoaded`
- **AND** 设置的 `asset.OriginalPath` / `asset.OriginalName` / `shader.OverrideDirectory` 等字段与改造前完全一致

#### Scenario: AssetBundle.Container PPtr 懒解析

- **WHEN** 处理 `IAssetBundle.Container` 中的 PPtr 引用
- **THEN** 调用 `kvp.Value.Asset.TryGetAssetOnly(bundle.Collection)` 替代 `kvp.Value.Asset.TryGetAsset(bundle.Collection)`
- **AND** 不触发任何 `SerializedAssetCollection` 的 `EnsureAssetsLoaded`
- **AND** 设置的 `asset.AssetBundleName` / `asset.OriginalPath` / `asset.OriginalName` 等字段与改造前完全一致

### Requirement: OriginalPathProcessor ContainerExport 分支元数据驱动

`OriginalPathProcessor.Process` ContainerExport 分支 SHALL 用 `EnumerateAssetMetadata()` 替代 `foreach (IUnityObjectBase asset in collection)`，对 ClassID 48 (IShader) 跳过，其余用 `SetOriginalDirectory` 设置，**完全不反序列化 asset 实例**。

#### Scenario: ContainerExport 第二段 foreach 不触发全量反序列化

- **WHEN** `bundledAssetsExportMode == ContainerExport` 且处理第二段（设置 OriginalDirectory）
- **THEN** 用 `EnumerateAssetMetadata()` 遍历 collection
- **AND** 对 ClassID == 48 (IShader) 跳过（保持原 `if (asset is IShader) continue;` 语义）
- **AND** 对其余 ClassID 调用 `collection.SetOriginalDirectory(meta.PathID, originalDirectory)` 设置 OriginalDirectory
- **AND** 不触发任何 `SerializedAssetCollection` 的 `EnsureAssetsLoaded`

#### Scenario: ContainerExport 第一段 count 判断用元数据 + 短路

- **WHEN** `bundledAssetsExportMode == ContainerExport` 且处理第一段（count 判断）
- **THEN** 用 `EnumerateAssetMetadata()` 遍历 collection，对每个 meta 调用 `TryGetAssetOnly(meta.PathID)` 解引用检查 `GetBestName() != ClassName`
- **AND** 当 count 超过 30 时短路退出循环（避免不必要的后续解引用）
- **AND** 不触发任何 `SerializedAssetCollection` 的 `EnsureAssetsLoaded`

#### Scenario: ContainerExport 行为一致

- **WHEN** 处理完成后
- **THEN** 设置的 `originalDirectory` 与改造前完全一致（count > 30 时移除扩展名，否则用 `Path.GetDirectoryName`）
- **AND** 设置的 `asset.OriginalDirectory` 与改造前完全一致（通过 collection 级别映射持久化，getter 回退读取）

### Requirement: EditorFormatProcessor 元数据驱动 Convert

`EditorFormatProcessor.Process` SHALL 用 `EnumerateAssetMetadata()` + 按需 `TryGetAssetOnly` 替代 `GetReleaseAssets().SelectMany(c => c)`，只对 Convert / ConvertAsync 涉及的 ClassID 反序列化，其余 ClassID 跳过。

#### Scenario: 不触发全量反序列化

- **WHEN** 处理一个包含 30 万对象的大型项目
- **THEN** `EditorFormatProcessor.Process` 执行期间，**任何** `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true
- **AND** 仅对 Convert / ConvertAsync 涉及的 ClassID 调用 `TryGetAssetOnly` 反序列化

#### Scenario: Convert 涉及的 ClassID 集合完整覆盖

- **WHEN** 元数据枚举遇到 ClassID ∈ {1, 23, 137, 199, 212, 26, 96, 120, 161, 222, 227, 483693784, 1971053207, 1931382933, 73398921, 687078895, 74, 196, 129, 4, 43, 218, 320, 142, 30, 47, 19, 157, 850595691, 310}
- **THEN** 调用 `TryGetAssetOnly(meta.PathID)` 反序列化该对象
- **AND** 调用 `Convert(asset)` 与 `ConvertAsync(asset)` 处理
- **AND** 处理结果与改造前完全一致

#### Scenario: 不涉及的 ClassID 跳过

- **WHEN** 元数据枚举遇到 ClassID ∉ 上述集合（如 ClassID 142 以外的 AssetBundle 子类、未列出的 Renderer 派生类等）
- **THEN** 跳过该对象，不调用 `TryGetAssetOnly`
- **AND** 不影响 Convert / ConvertAsync 的处理结果（因为这些 ClassID 在 `switch asset` 中本来就走 default 分支，无操作）

#### Scenario: Release collection 过滤保留

- **WHEN** 遍历 `gameData.GameBundle.FetchAssetCollections()`
- **THEN** 仍用 `c.Flags.IsRelease()` 过滤 release collection（保持原 `GetReleaseCollections` 语义）
- **AND** 非 release collection 不参与 Convert（保持原行为）

## MODIFIED Requirements

### Requirement: AssetCollection 资产访问 API

[AssetCollection](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 原行为（P2 后）：`TryGetAsset(int fileIndex, long pathID)` / `TryGetAsset(PPtr)` / `TryGetAsset<T>(PPtr<T>)` 触发 `EnsureAssetsLoaded` 全量反序列化。新行为：新增 `TryGetAssetOnly(int fileIndex, long pathID)` / `TryGetAssetOnly(PPtr)` / `TryGetAssetOnly<T>(PPtr<T>)` 虚方法，默认实现回退到 `TryGetAsset`，子类（`SerializedAssetCollection`）通过继承 P2 的 `TryGetAssetOnly(long pathID)` 实现自动获得懒加载行为。原 `TryGetAsset` 行为不变。

### Requirement: PPtrExtensions 扩展方法

[PPtrExtensions](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.SourceGenerated.Extensions/PPtrExtensions.cs) 原行为：提供 `TryGetAsset<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法，触发全量反序列化。新行为：新增 `TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file)` 扩展方法，调用 `file.TryGetAssetOnly<T>(pptr.FileID, pptr.PathID)` 实现懒解析。原 `TryGetAsset<T>` 行为不变。

### Requirement: OriginalPathProcessor.Process 资产遍历

[OriginalPathProcessor.Process](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L34-L132) 原行为（P2 后）：第一段用元数据枚举 + `TryGetAssetOnly`，但 `SetOriginalPaths` 内部仍用 `TryGetAsset` 解引用 PPtr，触发跨 collection 全量反序列化；ContainerExport 分支仍用 `foreach (asset in collection)` 触发全量反序列化。新行为：`SetOriginalPaths` 内部改用 `TryGetAssetOnly` 解引用 PPtr；ContainerExport 分支第二段用元数据枚举 + `SetOriginalDirectory`；第一段 count 判断用元数据 + 短路。

### Requirement: EditorFormatProcessor.Process 资产遍历

[EditorFormatProcessor.Process](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L70-L109) 原行为（P2 后）：tagManager 查找用元数据枚举，但 `GetReleaseAssets` 仍通过 `c.SelectMany(c => c)` 触发 `GetEnumerator` → `EnsureAssetsLoaded` 全量反序列化。新行为：`GetReleaseAssets` 改为元数据枚举 + 按需 `TryGetAssetOnly`，只对 Convert / ConvertAsync 涉及的 ClassID 反序列化。

## REMOVED Requirements

（无）
