# Checklist

- [x] `ProcessingSettings.EnableDeterministicGuids` 新增且默认值为 `false`，并已加入 `Log()` 输出
- [x] `DeterministicGuidCalculator.Calculate` 对同一资源两次调用结果一致（跨批次可复现）
- [x] 不同资源（`PathID`/名称/目录/源文件不同）的 GUID 不重复，目录+名称相同的资源也不会碰撞
- [x] 计算键包含 `GetBestDirectory`/`GetBestName`/`Collection.Name`/`PathID`，拼接格式符合 spec 要求
- [x] `MD5Hash` 结果格式与 `.meta` 中 32 位小写十六进制一致
- [x] `AssetExportCollection<T>.GUID` 默认仍为随机（关闭开关时行为与现状一致）
- [x] `UseDeterministicGuid()` 被调用后 `GUID` 变为计算器的确定性结果
- [x] `AssetsExportCollection<T>` 系集合自动继承确定性 GUID 行为，无需逐个修改
- [x] `SceneExportCollection` / `ScriptExportCollectionBase` / `UserAssetExportCollection` 的 GUID 不被 `UseDeterministicGuid()` 覆盖
- [x] `ProjectExporter.Export` 从 `SingletonData` 读取 `EnableDeterministicGuids`（与 `EnableAssetDeduplication` 同款方式）
- [x] `CreateCollection` 在豁免（非 `ExportCollection`/开关关闭）时不调用 `UseDeterministicGuid`
- [x] 开启开关时导出日志提示"确定性 GUID 已启用"；关闭时无相关日志
- [x] 开启开关并端到端导出两次：同名资源 `.meta` 中 guid 行一致
- [x] 默认（关闭）端到端导出两次：同名资源 `.meta` 中 guid 行不一致（随机）
- [x] 设置页 Experimental 分组出现 `EnableDeterministicGuids` 复选框，且勾选状态能保存生效
- [x] 所有语言 `Localizations/*.json` 含 `enable_deterministic_guids` 键，`SettingsPage.g.cs` 已重新生成（含 `WriteCheckBoxForEnableDeterministicGuids`）
- [x] 新增单元测试覆盖：同资源确定性、异资源不同、默认随机、开关开启端到端一致
- [x] 未修改场景/脚本/用户资产 GUID 的相关逻辑，未破坏去重（`deduplicate-exported-assets`）行为