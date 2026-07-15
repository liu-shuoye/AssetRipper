# 导出资源去重 Spec

## Why

AssetRipper 在导出游戏资源时，同一内容（例如相同的 Texture2D、Mesh、AudioClip、ScriptableObject）经常以多个独立实例的形式存在于不同的 AssetCollection 中。当前导出流程仅按"引用相等"去重（`ProjectExporter.CreateCollections` 中的 `queued` HashSet），完全不检测内容相同的实例，导致导出项目中出现大量内容重复的文件，既浪费磁盘空间，也增加了 Unity 重新打开项目时的资源冗余。

项目中已经预留了配置开关 `ProcessingSettings.EnableAssetDeduplication`（默认 `false`）和对应的 UI 复选框，但**没有任何代码读取并执行该开关**。本变更负责把这一预留特性落地为真实可用的去重导出能力。

## What Changes

- **实现 `EnableAssetDeduplication` 开关的真实行为**：当该开关为 `true` 时，导出阶段对内容相同的资源进行去重，只导出一份副本，其余实例不写文件。
- **在 `ProjectExporter.CreateCollections` 中接入去重逻辑**：用 `AssetEqualityComparer` 对每个 `IExportCollection` 的主资产做内容分组，识别内容相同的组，每组仅保留一个 collection 参与导出。
- **PPtr 引用重定向**：被跳过的重复资产的所有外部 PPtr 引用必须重定向到"保留资产"，确保导出的 YAML 不产生 missing reference。重定向在 `ProjectAssetContainer` 中实现（`GetExportID` / `CreateExportPointer`）。
- **去重报告**：导出完成后输出日志，报告去重前数量、去重后数量、被跳过的重复资产数量及类型分布，便于用户确认效果。
- **粒度限定**：去重以 `IExportCollection` 的主资产为单位进行比较；不以 collection 内部子资产单独比较（避免破坏 collection 的整体性，例如场景、Prefab 层级）。
- **场景与不可比较资产豁免**：`SceneExportCollection` 及其等价的整体性 collection 不参与内容去重（场景已有 `IsSceneDuplicate` 机制）；导出器声明 `Exportable` 为 `false` 的 collection 不参与去重。
- **不改变默认行为**：`EnableAssetDeduplication` 默认仍为 `false`，保持向后兼容；用户需显式启用。
- **移除 Premium 限制**：去掉 `SettingsPage.cs` 中 `WriteCheckBoxForEnableAssetDeduplication` 传入的 `!GameFileLoader.Premium` 禁用参数，使该复选框对所有用户可用；去重功能对所有用户开放。

## Impact

- Affected specs: 无（本项目无前置 spec）。
- Affected code:
  - [ProjectExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs) — `CreateCollections` 接入去重分组逻辑，`Export` 阶段跳过被标记为重复的 collection。
  - [ProjectAssetContainer.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectAssetContainer.cs) — 增加"被跳过资产 → 保留资产"的重定向映射，`GetExportID` / `CreateExportPointer` 对被跳过资产返回保留资产的 id/pointer。
  - [ProcessingSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/Configuration/ProcessingSettings.cs) — 复用现有 `EnableAssetDeduplication`，不新增属性。
  - [AssetEqualityComparer.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Cloning/AssetEqualityComparer.cs) — 直接复用，不修改；注意其 `GetHashCode` 较弱，需先按 `(Type, Name)` 分桶。
  - 日志输出：复用 `AssetRipper.Import.Logging` 中的 `Logger`。
  - [SettingsPage.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs) — 第 98 行移除 `!GameFileLoader.Premium` 禁用参数。
- 不影响：导入、处理、序列化配置（已存在）。

## ADDED Requirements

### Requirement: 导出阶段内容去重

当 `ProcessingSettings.EnableAssetDeduplication` 为 `true` 时，系统 SHALL 在导出阶段对内容相同的资源进行去重，使每个唯一内容只产生一个导出文件。

#### Scenario: 启用去重时跳过内容相同的重复资产

- **WHEN** 用户启用 `EnableAssetDeduplication` 并触发导出
- **AND** 存在多个内容相同（按 `AssetEqualityComparer.Equals` 判定）的主资产实例
- **THEN** 系统仅导出其中一个实例对应的 collection
- **AND** 其余重复实例的 collection 不写文件
- **AND** 所有原本指向被跳过资产的 PPtr 引用在导出时重定向到保留资产

#### Scenario: 禁用去重时保持现有行为

- **WHEN** `EnableAssetDeduplication` 为 `false`（默认值）
- **THEN** 导出行为与未实现本特性前完全一致
- **AND** 不进行任何内容比较
- **AND** 不输出与去重相关的日志

### Requirement: 去重比较粒度

去重 SHALL 以 `IExportCollection` 的主资产为比较单位，而非 collection 内部的每个子资产。

#### Scenario: 主资产内容相同则整组去重

- **WHEN** 两个 collection 的主资产内容相同
- **THEN** 其中一个 collection 被标记为重复并跳过导出
- **AND** 保留的 collection 完整导出其全部子资产

#### Scenario: 主资产内容不同则各自导出

- **WHEN** 两个 collection 的主资产内容不同（即使类型、名称相同）
- **THEN** 两个 collection 各自独立导出

### Requirement: 整体性 collection 豁免

去重 SHALL NOT 对整体性 collection（场景等）进行内容去重，以避免破坏其内部结构。

#### Scenario: 场景不参与内容去重

- **WHEN** collection 为 `SceneExportCollection` 或其 `Exportable` 为 `false`
- **THEN** 该 collection 不参与去重比较
- **AND** 始终正常导出（或按其原有 `IsSceneDuplicate` 逻辑处理）

### Requirement: 去重结果报告

启用去重时，系统 SHALL 在导出日志中输出可读的去重统计。

#### Scenario: 输出去重统计

- **WHEN** 去重启用且导出完成
- **THEN** 日志包含：参与比较的 collection 数量、去重后保留的数量、被跳过的重复 collection 数量
- **AND** 被跳过的资源按类型分组统计（例如 `Texture2D: 12, Mesh: 3`）

### Requirement: PPtr 引用重定向正确性

被跳过的重复资产被任何保留资产通过 PPtr 引用时，导出结果 SHALL 把该引用指向保留资产的导出 id 与 guid。

#### Scenario: 跨 collection 引用被跳过资产

- **WHEN** 保留 collection 中的资产 A 通过 PPtr 引用被跳过 collection 中的资产 B
- **AND** B 的内容等价于保留资产 C
- **THEN** 导出的 YAML 中 A 对 B 的引用被替换为对 C 的引用（正确的 fileID 与 guid）

#### Scenario: 同 collection 内部引用不被重定向

- **WHEN** 被跳过 collection 内部资产的相互引用
- **THEN** 不发生重定向（因为这些资产本身不会被导出）

## MODIFIED Requirements

### Requirement: ProjectExporter.CreateCollections 资源分流

`CreateCollections` SHALL 在为每个资产生成 `IExportCollection` 后，当 `EnableAssetDeduplication` 为 `true` 时，对生成的 collections 做内容去重：按主资产内容分组，每组保留一个 collection，其余标记为重复。被标记为重复的 collection 仍被加入返回列表（用于 PPtr 重定向映射建立），但在 `Export` 阶段被跳过不写文件。

## REMOVED Requirements

### Requirement: EnableAssetDeduplication 的 Premium 限制

**Reason**: 去重功能已落地为真实可用的能力，应对所有用户开放，不再作为 Premium 专属特性。
**Migration**: 移除 `SettingsPage.cs` 第 98 行 `WriteCheckBoxForEnableAssetDeduplication` 调用中的 `!GameFileLoader.Premium` 参数。该参数控制复选框的 `disabled` 属性，移除后复选框对所有用户可勾选。无数据迁移需求。
