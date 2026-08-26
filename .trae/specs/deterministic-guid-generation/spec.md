# 确定性 GUID 导出 Spec

## Why

AssetRipper 每次导出时，[AssetExportCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/AssetExportCollection.cs) 中 `GUID` 属性都会调用 `UnityGuid.NewGuid()` 生成**随机** GUID。当用户以"分批导出"的方式工作（例如先加载一部分文件导出，之后补充加载更多文件再导出到同一目录）时，同一资源在不同导出批次中会得到**不同**的 GUID：后一批次重新导出的 `.asset/.meta` 使用了新 GUID，而先前批次写入的 YAML 文件中对该资源的引用仍指向旧 GUID，导致 Unity 打开项目后引用全部丢失（Missing）。

## What Changes

- **新增配置项** `ProcessingSettings.EnableDeterministicGuids`（默认 `false`）：开启后，导出时不再随机生成 GUID，而是用确定性算法（MD5）基于资源的稳定标识计算 GUID，使同一资源在任意多次导出中得到完全相同的结果。
  - 与 `EnableAssetDeduplication` 一样放在 `ProcessingSettings` 中，因为 `ProjectExporter`（`AssetRipper.Export.UnityProjects`）需要通过 `CoreConfiguration.SingletonData` 读取该开关（该程序集不引用 `AssetRipper.Export`）。
- **新增 `DeterministicGuidCalculator`**（`AssetRipper.Export.UnityProjects` 命名空间）：将资产的稳定标识拼接为字符串后做 MD5 哈希，返回 `UnityGuid`。
  - 稳定标识 = `{ClassName}|{GetBestDirectory()}|{GetBestName()}|{Collection.Name}|{PathID}`。
  - 该标识全部来自已解析的游戏数据（与导出批次、导出顺序、输出目录已有文件无关），因此跨批次完全可复现；`PathID` 保证同一批内不同资源不会碰撞（即使目录+名称相同也不会产生重复 GUID，规避 Unity 重复 GUID 错误）。
- **`ExportCollection` 基类**新增虚方法 `UseDeterministicGuid()`（默认空操作）。场景、脚本、UserAsset、RawAssets 等集合不受影响：
  - `SceneExportCollection.GUID` 沿用场景原始 `SceneGUID`（来自游戏数据，本身稳定），保持不覆盖。
  - `ScriptExportCollectionBase.GUID` 本来就是 `ScriptHashing.CalculateScriptGuid`（确定性）。
  - `UserAssetExportCollection.GUID` 使用用户既有项目的真实 `.meta` GUID，**不得**被本特性覆盖。
- **`AssetExportCollection<T>`**：`GUID` 改为惰性计算（首次访问时随机生成并缓存），并覆写 `UseDeterministicGuid()` —— 将缓存的 GUID 替换为 `DeterministicGuidCalculator.Calculate(Asset)` 的结果。所有派生类（`AssetsExportCollection<T>` 系：Texture、Prefab、Material、AnimationClip、Shader、AudioClip、ScriptableObject 等）自动继承该行为。
- **`ProjectExporter`**：
  - `Export` 入口从 `options.SingletonData` 读取 `EnableDeterministicGuids`（与现有 `EnableAssetDeduplication` 读取方式一致）。
  - 开关传入 `CreateCollections` → `CreateCollection`，在集合创建完成后，若集合是 `ExportCollection` 且开关开启，则调用 `UseDeterministicGuid()`。
  - 开启时输出一条日志说明已启用确定性 GUID。
- **GUI**：设置页新增复选框 `EnableDeterministicGuids`（放于 Experimental 分组，紧邻去重复选框），并对所有语言新增本地化键 `enable_deterministic_guids`；重新生成 `SettingsPage.g.cs`。
- **不改默认行为**：开关默认关闭，导出行为与现状完全一致（随机 GUID）。

## Impact

- Affected specs: `deduplicate-exported-assets`（两者可协同：去重被跳过集合的 PPtr 引用重定向到保留集合的（确定性）GUID，引用依然正确）。
- Affected code:
  - [ProcessingSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/Configuration/ProcessingSettings.cs) — 新增属性并加入 `Log()`。
  - [ProjectExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs) — `Export` / `CreateCollections` / `CreateCollection`。
  - [ExportCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportCollection.cs) — 新增虚方法。
  - [AssetExportCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/AssetExportCollection.cs) — GUID 惰性化 + 覆写。
  - 新增 [DeterministicGuidCalculator.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/DeterministicGuidCalculator.cs)。
  - [SettingsPage.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs) + [SettingsPage.g.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.g.cs)（重新生成）+ `Localizations/*.json`。
  - 新增 [DeterministicGuidTests.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Tests/DeterministicGuidTests.cs)。
- 不影响：导入、场景 GUID（沿用原始数据）、脚本 GUID、用户项目资产 GUID。

## ADDED Requirements

### Requirement: 确定性 GUID 计算

启用 `EnableDeterministicGuids` 时，系统 SHALL 为 `AssetExportCollection<T>` 及其全部派生集合的导出 GUID 使用 `DeterministicGuidCalculator` 计算的结果，而非随机值。

计算键格式：`{ClassName}|{GetBestDirectory()}|{GetBestName()}|{Collection.Name}|{PathID}`；结果 = `UnityGuid.Md5Hash(key)`。

#### Scenario: 同一资源跨批次 GUID 相同

- **GIVEN** 同一份游戏文件两次加载
- **AND** `EnableDeterministicGuids` 已开启
- **WHEN** 两次分别导出到同一目录
- **THEN** 同一资源两次生成的 GUID 完全相同
- **AND** 两次写入的 `.meta` 中 `guid` 一致

#### Scenario: 不同资源 GUID 不同

- **WHEN** 两个资源（`PathID` 或目录或名称或源文件不同）
- **THEN** 计算出的 GUID 不同
- **AND** 即使目录与名称完全相同（在输出中会得到 `name` / `name (2)` 两个文件），GUID 也不重复

#### Scenario: 关闭开关保持现状

- **WHEN** `EnableDeterministicGuids` 为 `false`（默认）
- **THEN** 导出行为与实现本特性前完全一致（随机 GUID）
- **AND** 不输出任何相关日志

### Requirement: 分批导出引用稳定

开启开关并分批导出时，系统 SHALL 保证此前批次导出的引用与后导出/重新导出的 `.meta` GUID 保持一致，避免引用丢失。

#### Scenario: 先导出一批、再补充加载并导出

- **WHEN** 批次 1 导出资源 A（guid=g1）
- **AND** 批次 2 重新导出 A 并对引用 A 的资源写 YAML
- **THEN** 批次 2 中 A 的 `.meta` guid 仍为 g1
- **AND** 引用 A 的 YAML 使用的 guid 与 `.meta` 一致，无 Missing 引用

#### Scenario: 场景与脚本集合不受覆盖

- **WHEN** 开启开关导出
- **THEN** `SceneExportCollection.GUID` / `ScriptExportCollectionBase.GUID` / `UserAssetExportCollection.GUID` 保持原值不变

### Requirement: UI 设置项

系统 SHALL 在设置页提供 `EnableDeterministicGuids` 复选框，位于 Experimental 分组、`EnableAssetDeduplication` 复选框旁边。

#### Scenario: 打开设置页

- **WHEN** 用户打开设置页
- **THEN** 可以看到"确定性 GUID"复选框
- **AND** 勾选状态能保存到配置（JSON 序列化、开关传递到导出）

#### Scenario: 非英文语言

- **WHEN** 语言切换为非英文
- **THEN** 复选框标签显示对应语言的本地化文案（`enable_deterministic_guids` 键）

### Requirement: 启用日志

系统 SHALL 在导出开始时，若 `EnableDeterministicGuids` 开启，输出提示日志。

#### Scenario: 导出日志

- **WHEN** 开启开关并触发导出
- **THEN** 日志包含"确定性 GUID"相关开启提示

## MODIFIED Requirements

### Requirement: ProjectExporter 导出管线接入开关

`ProjectExporter.Export` SHALL 从 `options.SingletonData` 读取 `ProcessingSettings.EnableDeterministicGuids`（沿用 `EnableAssetDeduplication` 的读取方式），并将其传入 `CreateCollections` 与 `CreateCollection`；集合创建完成后，若开关开启且集合为 `ExportCollection`，则调用 `UseDeterministicGuid()`。

## REMOVED Requirements

无。