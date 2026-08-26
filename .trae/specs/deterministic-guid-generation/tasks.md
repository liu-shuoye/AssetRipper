# Tasks

- [x] Task 1: 为 `ProcessingSettings` 新增 `EnableDeterministicGuids` 配置项（默认 `false`），并加入 `Log()` 输出。
- [x] Task 2: 新增 `DeterministicGuidCalculator` 静态类：`Calculate(IUnityObjectBase asset)` 按 `{ClassName}|{GetBestDirectory()}|{GetBestName()}|{Collection.Name}|{PathID}` 拼接键并返回 `UnityGuid.Md5Hash(key)`。
  - [x] 2.1: 验证 `MD5` 结果为 32 位小写十六进制（与 `.meta` 格式一致）。
- [x] Task 3: `ExportCollection` 基类新增 `public virtual void UseDeterministicGuid() { }`（默认空操作）；`AssetExportCollection<T>` 将 `GUID` 改为惰性缓存（`UnityGuid? m_guid`，首次访问 `??=` 随机生成），并覆写 `UseDeterministicGuid()` 为 `m_guid = DeterministicGuidCalculator.Calculate(Asset)`。
  - [x] 3.1: 确认 `AssetsExportCollection<T>` 系（Texture/Prefab/Shader 等）与直接派生类自动继承该行为，无需逐个修改。
  - [x] 3.2: 确认 `SceneExportCollection` / `ScriptExportCollectionBase` / `UserAssetExportCollection` 等集合调用 `UseDeterministicGuid()` 时为空操作，GUID 不被覆盖。
- [x] Task 4: `ProjectExporter` 接入开关：
  - [x] 4.1: `Export` 读取 `EnableDeterministicGuids`（从 `options.SingletonData` 的 `ProcessingSettings`，与 `EnableAssetDeduplication` 一致）。
  - [x] 4.2: 开关传入 `CreateCollections` → `CreateCollection(asset, useDeterministicGuid)`，集合创建完成后 `if (useDeterministicGuid && collection is ExportCollection ec) ec.UseDeterministicGuid();`。
  - [x] 4.3: 开启时输出"确定性 GUID 已启用"日志。
- [x] Task 5: GUI 设置项：
  - [x] 5.1: `ProcessingSettings.Log()` 已含新属性（在 Task 1）。
  - [x] 5.2: 在所有 `Localizations/*.json` 新增 `enable_deterministic_guids` 键（各语言译文）。
  - [x] 5.3: 构建后运行 `AssetRipper.GUI.SourceGenerator` 重新生成 `SettingsPage.g.cs`，确认生成 `WriteCheckBoxForEnableDeterministicGuids`。
  - [x] 5.4: 在 `SettingsPage.cs` Experimental 分组（去重复选框旁）加入 `WriteCheckBoxForEnableDeterministicGuids(writer, Localization.EnableDeterministicGuids);`。
- [x] Task 6: 单元测试 `DeterministicGuidTests.cs`：
  - [x] 6.1: 同一资源对象两次计算 GUID 相同；不同 `PathID`/名称/目录的资源 GUID 不同。
  - [x] 6.2: `AssetExportCollection` 默认 GUID 为随机（两个相同资源集合 GUID 不同）；调用 `UseDeterministicGuid()` 后 GUID 等于计算器结果。
  - [x] 6.3: 端到端：`EnableDeterministicGuids=true` 时两次导出到虚拟文件系统，同名资源 `.meta` 的 guid 行一致；`false`（默认）时不一致。

# Task Dependencies

- [Task 2] 依赖 [Task 1] 无（计算器独立）。
- [Task 3] 依赖 [Task 2]（`AssetExportCollection` 引用计算器）。
- [Task 4] 依赖 [Task 1]（读取配置）与 [Task 3]（调用 `UseDeterministicGuid`）。
- [Task 5] 依赖 [Task 1]（属性反射生成 UI 方法）与 [Task 4]（功能可用）。
- [Task 6] 依赖 [Task 4]（端到端用例）与 [Task 2]/[Task 3]（单元用例）。