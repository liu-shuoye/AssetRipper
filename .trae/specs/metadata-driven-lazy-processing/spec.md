# 元数据驱动的懒处理 Spec（P2：Processing 阶段内存优化）

## Why

AssetRipper 在加载 30GB+ / 30 万文件级别的大型游戏项目时，Processing 阶段内存从 6.7GB 暴涨到 10.4GB（托管内存）/ 12GB（工作集），导致系统卡死。

内存诊断日志定位到 **SceneDefinitionProcessor** 一次性贡献 +3.5GB 托管内存，是最大、最集中的增长点：

```
[内存诊断] Process前 - SceneDefinitionProcessor: 托管: 6767.1 MB | 工作集: 7363.6 MB
Creating Scene Definitions
[内存诊断] Process后 - SceneDefinitionProcessor: 托管: 10323.0 MB | 工作集: 12772.2 MB
```

源码分析表明，根本原因是 `SceneDefinitionProcessor.Process` 第 31-52 行 `foreach (IUnityObjectBase asset in collection)` 通过 [AssetCollection.GetEnumerator](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs#L315-L320) → [SerializedAssetCollection.EnsureAssetsLoaded](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs#L95-L126) **击穿了 P1 阶段做的 ObjectInfo 懒加载**，把整个 GameBundle 中所有 30 万对象一次性反序列化到各自的 `assets` 字典中。

更深层的问题是：**几乎所有 processor 都用 `gameData.GameBundle.FetchAssets().OfType<T>()` 或 `foreach (asset in collection)` 遍历资产**，任何一个都会触发 `EnsureAssetsLoaded` 全量反序列化。当前日志中其他 processor 内存增长很小，仅仅是因为 SceneDefinitionProcessor 已经触发了反序列化、后续 processor 共享了 `assets` 字典。如果只优化 SceneDefinitionProcessor，OriginalPathProcessor / MainAssetProcessor / PrefabProcessor 等会依次成为新的触发点，峰值不变。

本次改造通过 **元数据驱动的懒处理**，让 processor 仅用 `ObjectInfo.ClassID` 识别资产类型，只对真正需要访问字段的少量对象调用单对象反序列化，从根本上避免在 Processing 阶段触发全量反序列化。

## What Changes

### 新增基础设施

- **`AssetCollection.AssetMetadata` 只读结构**：暴露 `PathID` 与 `ClassID`，不触发反序列化。
- **`AssetCollection.EnumerateAssetMetadata()` 虚方法**：默认实现回退到 `EnsureAssetsLoaded` 后枚举（兼容老代码）；`SerializedAssetCollection` 重写为直接遍历 `SerializedFile.Objects`（ObjectInfo 数组），完全不触发反序列化。
- **`AssetCollection.TryGetAssetOnly(long pathID)` 虚方法**：单对象反序列化。默认实现回退到 `EnsureAssetsLoaded`；`SerializedAssetCollection` 重写为：若 `_assetsLoaded` 为 true 走原字典查询，否则从 `ObjectInfo` 数组按 `PathID` 找到对应项 → `LoadObjectData()` → `factory.ReadAsset()` → `AddAsset()`，**不触发全量 EnsureAssetsLoaded**。
- **`AssetCollection.SetOriginalDirectory(long pathID, string directory)` 与 `TryGetOriginalDirectory(long pathID)`**：在 collection 级别维护 `Dictionary<long, string>?` 持久化 OriginalDirectory，让 OriginalPathProcessor 不必反序列化 asset 实例就能设置路径。
- **`UnityObjectBase.OriginalDirectory` getter 修改**：当 `originalPathDetails?.Directory` 为 null 时，回退到 `Collection.TryGetOriginalDirectory(PathID)`，让 collection 级别的持久化映射能被 asset 实例读取到。

### 改造 Processing 阶段所有触发全量反序列化的 processor

所有改造遵循同一模式：**用 `EnumerateAssetMetadata()` 识别候选 ClassID → 只对真正需要访问字段的少量对象调用 `TryGetAssetOnly()`**。

- **SceneDefinitionProcessor**：用元数据枚举识别 `ILevelGameManager`（ClassID 29/104/157/196）/ `IBuildSettings`（141）/ `IAssetBundle`（142），只对 `IOcclusionCullingSettings`(读 SceneGUID)、`IBuildSettings`(读 Scenes)、`IAssetBundle`(读 SceneHashes/Container) 三类调用 `TryGetAssetOnly()`。
- **OriginalPathProcessor**：第一段 `FetchAssets()` 改为元数据枚举，只对 `IResourceManager`(ClassID 27) 与 `IAssetBundle`(142) 调用 `TryGetAssetOnly()`；第二段 `foreach (asset in collection)` 设置 OriginalDirectory 改为 `EnumerateAssetMetadata()` + `SetOriginalDirectory()`，**完全不反序列化**（GroupByBundleName 模式）；ContainerExport 模式因需要 `GetBestName()` 保留原逻辑（标注为已知限制）。
- **MainAssetProcessor**：`FetchAssets()` 改为元数据枚举，只对 `IFont`(ClassID 128) 与 `ITerrainData`(156) 调用 `TryGetAssetOnly()`。
- **PrefabProcessor**：多处 `FetchAssets().OfType<T>()` 改为元数据枚举 + `TryGetAssetOnly()`，只反序列化 `IAssetBundle`(142) / `IPrefabInstance` / `IGameObject`(1)。
- **AudioMixerProcessor**：`FetchAssets().OfType<IAudioMixerGroup>()` 与 `FetchAssets().OfType<IAudioMixer>()` 改为元数据枚举 + `TryGetAssetOnly()`。
- **EditorFormatProcessor**：`FetchAssets().OfType<ITagManager>().FirstOrDefault()` 改为元数据枚举找到第一个 ClassID 为 TagManager 的 PathID，再 `TryGetAssetOnly()`。
- **ScriptableObjectProcessor**：`FetchAssets().OfType<IMonoBehaviour>()` 改为元数据枚举（ClassID 114），逐个 `TryGetAssetOnly()`。
- **PathChecksumCache**（用于 AnimationClip 处理）：`bundle.FetchAssets()` 改为元数据枚举 + 按需 `TryGetAssetOnly()`。

### 兼容性

- **非破坏性**：`AssetCollection.GetEnumerator` / `Count` / `TryGetAsset(pathID)` 行为不变，仍触发 `EnsureAssetsLoaded`，老代码兼容。
- **新增 API 是纯新增**：`EnumerateAssetMetadata` / `TryGetAssetOnly` / `SetOriginalDirectory` / `TryGetOriginalDirectory` 为新增虚方法，默认实现回退到原行为。
- **`UnityObjectBase.OriginalDirectory` getter 语义微调**：当 asset 实例本身未设置 `originalPathDetails` 时，从 collection 级别映射读取。`setter` 行为不变（仍写入 asset 实例的 `originalPathDetails`，覆盖 collection 级别映射）。这是向后兼容的扩展：原行为下 collection 级别映射为空，getter 返回 null。
- **测试兼容**：现有单元测试不修改。`SerializedAssetCollection.EnumerateAssetMetadata` 返回的 `(PathID, ClassID)` 与 `EnsureAssetsLoaded` 后遍历 `assets.Values` 得到的 `(PathID, ClassID)` 完全一致（同源 `ObjectInfo`）。

## Impact

- Affected specs:
  - [lazy-load-and-dispose](file:///e:/Project/Rider/AssetRipper/.trae/specs/lazy-load-and-dispose/spec.md)（P1：ObjectInfo 懒加载 + 显式 Dispose）—— 本 spec 是 P1 的延续，复用 P1 引入的 `ObjectInfo.LoadObjectData()` / `ReleaseDataStream()`。
- Affected code:
  - [Source/AssetRipper.Assets/Collections/AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs)（新增 `AssetMetadata` 结构、`EnumerateAssetMetadata` / `TryGetAssetOnly` / `SetOriginalDirectory` / `TryGetOriginalDirectory` 虚方法、`_originalDirectoryOverrides` 字段）
  - [Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs)（重写 `EnumerateAssetMetadata` / `TryGetAssetOnly`）
  - [Source/AssetRipper.Assets/UnityObjectBase.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/UnityObjectBase.cs)（修改 `OriginalDirectory` getter 回退到 collection 映射）
  - [Source/AssetRipper.Processing/Scenes/SceneDefinitionProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/SceneDefinitionProcessor.cs)（元数据驱动重写 Process 第 31-52 行）
  - [Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs)（元数据驱动重写第一段、第二段 GroupByBundleName 分支）
  - [Source/AssetRipper.Processing/MainAssetProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/MainAssetProcessor.cs)（元数据驱动重写）
  - [Source/AssetRipper.Processing/Prefabs/PrefabProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Prefabs/PrefabProcessor.cs)（元数据驱动重写多处 `FetchAssets().OfType<T>()`）
  - [Source/AssetRipper.Processing/AudioMixers/AudioMixerProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/AudioMixers/AudioMixerProcessor.cs)（元数据驱动重写）
  - [Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs)（元数据驱动重写 `OfType<ITagManager>` 调用）
  - [Source/AssetRipper.Processing/ScriptableObject/ScriptableObjectProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/ScriptableObject/ScriptableObjectProcessor.cs)（元数据驱动重写 `OfType<IMonoBehaviour>` 遍历）
  - [Source/AssetRipper.Processing/AnimationClips/PathChecksumCache.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/AnimationClips/PathChecksumCache.cs)（元数据驱动重写 `bundle.FetchAssets()` 遍历）

## ADDED Requirements

### Requirement: 元数据枚举不触发反序列化

系统 SHALL 提供 `AssetCollection.EnumerateAssetMetadata()` 方法，返回 `(PathID, ClassID)` 序列，**不触发 `EnsureAssetsLoaded`**，让调用方仅凭 ClassID 识别资产类型而无需反序列化对象本体。

#### Scenario: SerializedAssetCollection 元数据枚举

- **WHEN** 调用 `SerializedAssetCollection.EnumerateAssetMetadata()`
- **THEN** 系统直接遍历 `SerializedFile.Objects`（ObjectInfo struct 数组），对每个 ObjectInfo 计算 `classID = TypeID < 0 ? 114 : TypeID` 并 yield `(FileID, classID)`
- **AND** 不访问 `ObjectInfo.LoadObjectData()`，不调用 `factory.ReadAsset`，不修改 `_assetsLoaded` 标志

#### Scenario: 默认实现回退兼容

- **WHEN** 调用 `AssetCollection.EnumerateAssetMetadata()` 且子类未重写（如 `ProcessedAssetCollection`、`VirtualAssetCollection`）
- **THEN** 系统触发 `EnsureAssetsLoaded()` 后枚举 `assets.Values`，对每个 `IUnityObjectBase` 返回 `(PathID, ClassID)`

### Requirement: 单对象懒反序列化

系统 SHALL 提供 `AssetCollection.TryGetAssetOnly(long pathID)` 方法，**仅反序列化指定 PathID 对应的单个对象**，不触发全量 `EnsureAssetsLoaded`。反序列化后的对象加入 `assets` 字典，避免后续重复反序列化。

#### Scenario: SerializedAssetCollection 单对象反序列化（首次访问）

- **WHEN** 调用 `SerializedAssetCollection.TryGetAssetOnly(pathID)` 且 `_assetsLoaded` 为 false
- **THEN** 系统遍历 `SerializedFile.Objects` 找到 `FileID == pathID` 的 ObjectInfo
- **AND** 调用 `objectInfo.LoadObjectData()` 读取字节、`factory.ReadAsset()` 反序列化为 `IUnityObjectBase`
- **AND** 调用 `AddAsset(asset)` 把对象加入 `assets` 字典（后续访问直接命中字典）
- **AND** **不**设置 `_assetsLoaded = true`（保持懒加载状态，后续 `GetEnumerator` 仍会触发全量加载以兼容老代码）
- **AND** 返回反序列化后的 asset（或 null 如果找不到）

#### Scenario: 已全量加载后走字典查询

- **WHEN** 调用 `SerializedAssetCollection.TryGetAssetOnly(pathID)` 且 `_assetsLoaded` 为 true
- **THEN** 系统直接调用 `TryGetAsset(pathID)`（走字典查询，不重复反序列化）

#### Scenario: 重复调用同一 PathID 不重复反序列化

- **WHEN** 同一 PathID 第二次调用 `TryGetAssetOnly(pathID)` 且首次调用已成功反序列化并加入字典
- **THEN** 系统直接从 `assets` 字典返回已反序列化的对象，不再次调用 `factory.ReadAsset`

#### Scenario: 默认实现回退兼容

- **WHEN** 调用 `AssetCollection.TryGetAssetOnly(pathID)` 且子类未重写
- **THEN** 系统回退到 `TryGetAsset(pathID)`（触发 `EnsureAssetsLoaded`），保证老代码行为不变

### Requirement: OriginalDirectory collection 级别持久化

系统 SHALL 在 `AssetCollection` 级别维护 `Dictionary<long, string>?` 持久化 OriginalDirectory，让 processor 能在不反序列化 asset 实例的情况下设置路径。

#### Scenario: 设置 OriginalDirectory 不触发反序列化

- **WHEN** processor 调用 `collection.SetOriginalDirectory(pathID, directory)`
- **THEN** 系统在 `_originalDirectoryOverrides` 字典中写入 `pathID → directory` 映射
- **AND** **不**反序列化该 pathID 对应的 asset 实例

#### Scenario: UnityObjectBase.OriginalDirectory getter 回退到 collection 映射

- **WHEN** 访问 `asset.OriginalDirectory` 且 asset 实例的 `originalPathDetails?.Directory` 为 null
- **THEN** 系统回退到 `Collection.TryGetOriginalDirectory(PathID)` 查询 collection 级别映射
- **AND** 返回映射中的 directory（或 null 如果映射中不存在）

#### Scenario: asset 实例级 OriginalDirectory 优先

- **WHEN** 访问 `asset.OriginalDirectory` 且 asset 实例已通过 setter 设置 `originalPathDetails.Directory`
- **THEN** 系统返回 asset 实例级别的 directory，**不**查询 collection 级别映射
- **AND** 保证 `asset.OriginalDirectory = "X"` 后 `asset.OriginalDirectory == "X"`（向后兼容）

#### Scenario: setter 不写入 collection 映射

- **WHEN** 调用 `asset.OriginalDirectory = "X"`
- **THEN** 系统只写入 asset 实例的 `originalPathDetails.Directory`，**不**写入 collection 级别映射
- **AND** 后续 getter 返回 asset 实例级别的 "X"（优先级高于 collection 映射）

### Requirement: SceneDefinitionProcessor 元数据驱动

`SceneDefinitionProcessor.Process` SHALL 用 `EnumerateAssetMetadata()` 替代 `foreach (asset in collection)`，只对 `IOcclusionCullingSettings`(读 SceneGUID)、`IBuildSettings`(读 Scenes)、`IAssetBundle`(读 SceneHashes/Container) 三类调用 `TryGetAssetOnly()`。

#### Scenario: 不触发全量反序列化

- **WHEN** 处理一个包含 30 万对象的大型项目
- **THEN** `SceneDefinitionProcessor.Process` 执行期间，**任何** `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true
- **AND** 仅对 ClassID 为 29 / 141 / 142 的对象调用 `TryGetAssetOnly()` 反序列化（数量级为场景数 + AssetBundle 数，远小于 30 万）

#### Scenario: 行为与改造前一致

- **WHEN** 处理完成后
- **THEN** `sceneCollections` / `scenePaths` / `sceneGuids` / `sceneAssetBundles` / `buildSettings` 的内容与改造前完全一致
- **AND** 生成的 `SceneDefinition` 列表与改造前完全一致
- **AND** 生成的 `EditorBuildSettings` 与 `EditorSettings` 与改造前完全一致

### Requirement: OriginalPathProcessor GroupByBundleName 模式不反序列化

`OriginalPathProcessor.Process` SHALL 用 `EnumerateAssetMetadata()` + `SetOriginalDirectory()` 替代 `foreach (asset in collection)` 设置 OriginalDirectory，**在 GroupByBundleName 模式下完全不反序列化 asset 实例**。

#### Scenario: GroupByBundleName 模式不触发全量反序列化

- **WHEN** `bundledAssetsExportMode == GroupByBundleName` 且处理大型项目
- **THEN** `OriginalPathProcessor.Process` 第二段（设置 OriginalDirectory）执行期间，**任何** `SerializedAssetCollection` 的 `_assetsLoaded` 标志都不被设置为 true
- **AND** OriginalDirectory 通过 `collection.SetOriginalDirectory(pathID, ...)` 写入 collection 级别映射
- **AND** 后续 Export 阶段访问 `asset.OriginalDirectory` 时，从 collection 映射读取

#### Scenario: ContainerExport 模式保留原逻辑

- **WHEN** `bundledAssetsExportMode == ContainerExport`
- **THEN** 保留原 `foreach (asset in collection)` 逻辑（因为需要 `GetBestName()` 访问 asset 字段）
- **AND** 这是已知限制，后续 spec 可优化

#### Scenario: 第一段识别不触发全量反序列化

- **WHEN** `OriginalPathProcessor.Process` 第一段遍历所有 assets 识别 `IResourceManager` / `IAssetBundle`
- **THEN** 用 `EnumerateAssetMetadata()` 替代 `FetchAssets()`，只对 ClassID 27 / 142 调用 `TryGetAssetOnly()`
- **AND** 不触发全量 `EnsureAssetsLoaded`

### Requirement: 其他 processor 元数据驱动

`MainAssetProcessor` / `PrefabProcessor` / `AudioMixerProcessor` / `EditorFormatProcessor` / `ScriptableObjectProcessor` / `PathChecksumCache` SHALL 用 `EnumerateAssetMetadata()` + `TryGetAssetOnly()` 替代 `FetchAssets().OfType<T>()`，只反序列化目标 ClassID 的对象。

#### Scenario: MainAssetProcessor 只反序列化 IFont / ITerrainData

- **WHEN** 处理大型项目
- **THEN** `MainAssetProcessor.Process` 只对 ClassID 128 (Font) / 156 (TerrainData) 调用 `TryGetAssetOnly()`
- **AND** 不触发全量 `EnsureAssetsLoaded`

#### Scenario: PrefabProcessor 只反序列化 IAssetBundle / IPrefabInstance / IGameObject

- **WHEN** 处理大型项目
- **THEN** `PrefabProcessor.Process` 多处 `FetchAssets().OfType<T>()` 都改为元数据枚举 + `TryGetAssetOnly()`
- **AND** 只反序列化 ClassID 142 / 1 (GameObject) / PrefabInstance 对应的 PathID

#### Scenario: AudioMixerProcessor 只反序列化 IAudioMixerGroup / IAudioMixer

- **WHEN** 处理大型项目
- **THEN** `AudioMixerProcessor.Process` 只对目标 ClassID 调用 `TryGetAssetOnly()`

#### Scenario: EditorFormatProcessor 只反序列化 ITagManager

- **WHEN** 处理大型项目
- **THEN** `EditorFormatProcessor.Process` 用元数据枚举找到第一个 ClassID 为 TagManager 的 PathID，再 `TryGetAssetOnly()`
- **AND** 不触发全量 `EnsureAssetsLoaded`

#### Scenario: ScriptableObjectProcessor 只反序列化 IMonoBehaviour

- **WHEN** 处理大型项目
- **THEN** `ScriptableObjectProcessor.Process` 用元数据枚举识别 ClassID 114 (MonoBehaviour)，逐个 `TryGetAssetOnly()`

#### Scenario: PathChecksumCache 只反序列化必要对象

- **WHEN** `PathChecksumCache` 遍历 `bundle.FetchAssets()`
- **THEN** 改为元数据枚举 + 按需 `TryGetAssetOnly()`

## MODIFIED Requirements

### Requirement: AssetCollection 资产访问 API

[AssetCollection](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 原行为：`GetEnumerator` / `Count` / `TryGetAsset(pathID)` 都触发 `EnsureAssetsLoaded` 全量反序列化。新行为：新增 `EnumerateAssetMetadata()` / `TryGetAssetOnly(pathID)` / `SetOriginalDirectory` / `TryGetOriginalDirectory` 虚方法，默认实现回退到原行为，子类（`SerializedAssetCollection`）重写为懒加载。原 `GetEnumerator` / `Count` / `TryGetAsset(pathID)` 行为不变（仍触发全量加载，兼容老代码）。

### Requirement: UnityObjectBase.OriginalDirectory getter

[UnityObjectBase.OriginalDirectory](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/UnityObjectBase.cs#L51-L66) 原行为：`get => originalPathDetails?.Directory`。新行为：`get => originalPathDetails?.Directory ?? Collection.TryGetOriginalDirectory(PathID)`。当 asset 实例未设置 `originalPathDetails` 时，从 collection 级别映射读取。setter 行为不变。

### Requirement: SceneDefinitionProcessor.Process 资产遍历

[SceneDefinitionProcessor.Process](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/SceneDefinitionProcessor.cs#L31-L52) 原行为：`foreach (IUnityObjectBase asset in collection)` 触发 `EnsureAssetsLoaded` 全量反序列化。新行为：`foreach (AssetMetadata meta in collection.EnumerateAssetMetadata())` 仅枚举元数据，对必要对象调用 `TryGetAssetOnly()`。

### Requirement: OriginalPathProcessor.Process 资产遍历

[OriginalPathProcessor.Process](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L38-L108) 原行为：`FetchAssets()` 与多处 `foreach (asset in collection)` 触发全量反序列化。新行为：第一段用元数据枚举识别 IResourceManager / IAssetBundle；第二段 GroupByBundleName 分支用元数据枚举 + `SetOriginalDirectory` 不反序列化；ContainerExport 分支保留原逻辑。

## REMOVED Requirements

（无）
