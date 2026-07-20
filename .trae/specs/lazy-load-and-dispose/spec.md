# 大型项目内存优化（P1：ObjectInfo 懒加载 + 显式 Dispose）Spec

## Why
AssetRipper 在加载 30GB+ / 30万文件级别的大型游戏项目时会出现内存溢出。源码分析表明，根本原因是 `ObjectInfo.Read` 把每个 Unity 对象的二进制数据全量 `ReadBytes` 到 `byte[] ObjectData` 中（[ObjectInfo.cs:73](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs#L73)），导致 30GB 项目在加载阶段就常驻 30GB+ 的 LOH 大对象数组。同时 [GameFileLoader.Reset](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs#L42-L50) 未调用 `Bundle.Dispose()`，仅依赖 GC 回收，资源释放链不完整。本次改造通过懒加载和显式 Dispose 显著降低内存峰值。

## What Changes

### 方案 A：ObjectInfo 懒加载
- 在 `ObjectInfo` 中新增 `DataStream`（持有 `SmartStream` 引用计数）和 `DataAbsoluteOffset`、`DataSize` 字段，加载阶段不再调用 `ReadBytes` 全量读取对象二进制数据。
- 新增 `LoadObjectData()` 方法按需从 `DataStream` 读取单次 `byte[]`。
- 修改 `SerializedAssetCollection.ReadData`：调用 `factory.ReadAsset` 时使用 `LoadObjectData()` 按需读取，读取完成后立即释放 `DataStream` 引用。
- 保持 `ObjectData` 属性对外契约不变（兼容 Write 路径与现有测试），但加载时不填充。
- `SerializedFile` 实现 `IDisposable`：在 `Dispose` 中释放 `ObjectInfo.DataStream` 引用与底层 `SmartStream`。

### 方案 C：显式 Dispose 链
- 修复 [GameFileLoader.Reset](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs#L42-L50)：在置空 `GameData` 之前显式调用 `GameBundle.Dispose()`，并增加 `GC.WaitForPendingFinalizers()` + 二次 `GC.Collect()`。
- 增强 [Bundle.Dispose(bool)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Bundles/Bundle.cs#L431-L449)：先遍历 `collections` 调用 `Dispose()`（若 `AssetCollection` 实现 `IDisposable`），再清理 `resources` 和子 `bundles`，最后 `Clear()` 三个列表以便对象图尽早可回收。
- `AssetCollection` 实现 `IDisposable`：清空 `assets` 字典和 `dependencies` 列表，断开对象图引用。
- `SerializedAssetCollection` 重写 `Dispose`：释放持有的 `DependencyIdentifiers` 等临时数据。

### 兼容性
- **非破坏性**：对外 API 不变，`ObjectInfo.ObjectData` 仍可读写，仅在加载路径上延迟到首次访问。
- **Write 路径不受影响**：`SerializedFile.Write` 仍从 `ObjectData` 写入，因为写路径本来就是显式赋值的。
- **测试兼容**：现有单元测试不修改，验证加载与导出行为一致。

## Impact

- Affected specs: 无
- Affected code:
  - [Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs)（新增字段、修改 Read、新增 LoadObjectData）
  - [Source/AssetRipper.IO.Files/SerializedFiles/SerializedFile.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/SerializedFile.cs)（实现 IDisposable、释放 SmartStream）
  - [Source/AssetRipper.Assets/Collections/AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs)（实现 IDisposable）
  - [Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs)（使用 LoadObjectData、重写 Dispose）
  - [Source/AssetRipper.Assets/Bundles/Bundle.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Bundles/Bundle.cs)（增强 Dispose、清理 collections）
  - [Source/AssetRipper.GUI.Web/GameFileLoader.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs)（Reset 调用 Dispose）

## ADDED Requirements

### Requirement: ObjectInfo 懒加载
系统 SHALL 在加载 SerializedFile 时，仅为每个 ObjectInfo 记录数据偏移量和长度，并持有底层 `SmartStream` 引用，而不是在加载阶段就把对象二进制数据全量读入 `byte[]`。

#### Scenario: 大型项目加载
- **WHEN** 用户加载一个包含 30 万对象、共 30GB 数据的大型项目
- **THEN** 加载完成后，`ObjectInfo.ObjectData` 字段不应持有任何 `byte[]`，仅在 `SerializedAssetCollection.ReadData` 中按需通过 `LoadObjectData()` 短暂读取并立即释放

#### Scenario: 单对象按需读取
- **WHEN** `factory.ReadAsset` 调用前需要对象的二进制数据
- **THEN** 系统通过 `ObjectInfo.LoadObjectData()` 从 `DataStream` 在指定偏移处读取 `DataSize` 字节并返回 `byte[]`，调用方使用完后无引用即可被 GC 回收

#### Scenario: 反复读取安全
- **WHEN** 同一 `ObjectInfo` 被多次调用 `LoadObjectData()`
- **THEN** 每次 `DataStream` 非空时都能正确读取，读完后调用方负责释放 `DataStream` 引用；释放后再调用 `LoadObjectData()` 返回 `ObjectData` 字段（可能为空数组）

### Requirement: SmartStream 引用计数管理
系统 SHALL 在 `ObjectInfo` 中以引用计数方式持有 `SmartStream`，确保在 `SerializedFile` 释放前底层数据流不会被关闭。

#### Scenario: 引用计数递增
- **WHEN** `ObjectInfo.Read` 通过 `CreateReference()` 或类似方式获取 `DataStream`
- **THEN** 底层 `SmartStream` 的引用计数增加 1

#### Scenario: 引用计数递减
- **WHEN** `SerializedFile.Dispose()` 被调用
- **THEN** 所有 `ObjectInfo.DataStream` 通过 `FreeReference()` 释放，引用计数下降；当引用计数为 0 时底层 `FileStream` 关闭

### Requirement: AssetCollection 资源释放
系统 SHALL 让 `AssetCollection` 实现 `IDisposable`，在 `Bundle.Dispose` 时清空资产字典，断开对象图引用。

#### Scenario: Bundle Dispose 链
- **WHEN** `GameBundle.Dispose()` 被调用
- **THEN** 系统按顺序：1) 遍历 `collections` 调用每个 `AssetCollection.Dispose()` 清空 `assets` 字典；2) 释放所有 `ResourceFile`；3) 递归 `Dispose` 所有子 `Bundle`；4) `Clear()` 三个列表

#### Scenario: 重复 Dispose 幂等
- **WHEN** 同一个 `Bundle` 或 `AssetCollection` 被多次 `Dispose()`
- **THEN** 第二次及之后的调用应直接返回，不抛异常、不重复释放

### Requirement: GameFileLoader 显式释放
系统 SHALL 在 `GameFileLoader.Reset()` 中显式调用 `GameBundle.Dispose()`，而不是仅依赖 GC。

#### Scenario: 用户重置加载
- **WHEN** 用户在 UI 上点击 "Reset" 或重新加载新项目
- **THEN** 系统调用 `GameData.GameBundle.Dispose()`，随后置空 `GameData`，再执行 `GC.Collect()` + `GC.WaitForPendingFinalizers()` + `GC.Collect()` 双轮回收

## MODIFIED Requirements

### Requirement: ObjectInfo.Read 行为
[ObjectInfo.Read](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs#L39-L126) 原行为：在读取元数据时同步 `ReadBytes(byteSize)` 到 `ObjectData`。新行为：仅记录 `byteStart` 与 `byteSize`，并通过 `reader.BaseStream` 的 `SmartStream` 创建引用赋值给 `DataStream`；`ObjectData` 字段保持默认值（空数组或 null），不进行同步读取。`DataAbsoluteOffset` 等于 `dataOffset + byteStart`。

### Requirement: Bundle.Dispose(bool) 行为
[Bundle.Dispose(bool)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Bundles/Bundle.cs#L431-L449) 原行为：仅释放 `resources` 与递归 `bundles`。新行为：先遍历 `collections` 调用 `Dispose()`（若为 `IDisposable`），再释放 `resources` 和 `bundles`，最后 `Clear()` 三个列表。

### Requirement: GameFileLoader.Reset 行为
[GameFileLoader.Reset](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs#L42-L50) 原行为：仅 `GameData = null; GC.Collect();`。新行为：先 `GameData.GameBundle.Dispose()`，再 `GameData = null; GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`。

## REMOVED Requirements
（无）
