# Tasks

- [x] Task 1: 在 `ProjectExporter` 中接入 `EnableAssetDeduplication` 开关
  - [x] SubTask 1.1: 修改 `ProjectExporter` 构造或 `Export` 方法，使其能读取 `FullConfiguration.ProcessingSettings.EnableAssetDeduplication`
  - [x] SubTask 1.2: 在 `Export` 方法中根据开关决定是否进入去重路径；开关为 `false` 时保持现有逻辑完全不变

- [x] Task 2: 实现去重分组逻辑
  - [x] SubTask 2.1: 在 `CreateCollections`（或新增辅助方法）中，先按现有方式生成全部 `IExportCollection`
  - [x] SubTask 2.2: 跳过豁免类型（`SceneExportCollection`、`Exportable == false` 的 collection）
  - [x] SubTask 2.3: 对参与去重的 collection，先用 `(Type, GetBestName())` 分桶（规避 `AssetEqualityComparer.GetHashCode` 过弱的问题）
  - [x] SubTask 2.4: 桶内使用 `AssetEqualityComparer.Equals` 精确比较主资产，每组选首个作为保留 collection，其余标记为重复
  - [x] SubTask 2.5: 为每个 collection 增加一个"是否被跳过"的标记（例如在 `ProjectExporter` 内维护 `HashSet<IExportCollection> skipped` 或在 collection 上加 `IsDuplicate` 字段）

- [x] Task 3: 在 `ProjectAssetContainer` 中实现 PPtr 重定向
  - [x] SubTask 3.1: 增加 `Dictionary<IUnityObjectBase, IUnityObjectBase> redirectMap`（被跳过资产 → 保留资产）
  - [x] SubTask 3.2: 在建立 `m_assetCollections` 时，若资产属于被跳过 collection，则把它映射到对应保留 collection 的主资产
  - [x] SubTask 3.3: 修改 `GetExportID` 与 `CreateExportPointer`：遇到 `redirectMap` 中的 key 时，返回重定向目标的 id/pointer
  - [x] SubTask 3.4: 确保现有 DEBUG 断言 `CheckIfAlreadyAdded` 不会因重定向误报

- [x] Task 4: 导出阶段跳过重复 collection
  - [x] SubTask 4.1: 在 `ProjectExporter.Export` 的 collection 遍历中，对被标记为重复的 collection 直接 `continue`（不调用 `collection.Export`）
  - [x] SubTask 4.2: 确保跳过的 collection 仍参与 `ProjectAssetContainer` 的重定向映射（Task 3 已覆盖）

- [x] Task 5: 去重统计日志
  - [x] SubTask 5.1: 在去重完成后、导出开始前，输出参与比较数量、保留数量、跳过数量
  - [x] SubTask 5.2: 按类型分组统计被跳过的资产，输出可读摘要
  - [x] SubTask 5.3: 仅在 `EnableAssetDeduplication` 为 `true` 时输出，关闭时完全静默

- [x] Task 6: 移除 Premium 限制
  - [x] SubTask 6.1: 修改 `SettingsPage.cs` 第 98 行，移除 `WriteCheckBoxForEnableAssetDeduplication` 调用中的 `!GameFileLoader.Premium` 参数，使复选框对所有用户可勾选

- [x] Task 7: 单元测试与验证
  - [x] SubTask 7.1: 新增测试 `ProjectExporterDeduplicationTests`，构造包含两个内容相同 Texture2D 实例的 `GameBundle`，启用去重导出，断言只生成一份文件
  - [x] SubTask 7.2: 测试 PPtr 重定向：保留资产引用被跳过资产时，导出 YAML 引用指向保留资产
  - [x] SubTask 7.3: 测试开关关闭时行为不变（不比较、不跳过、不输出日志）
  - [x] SubTask 7.4: 测试场景豁免：`SceneExportCollection` 即使内容相同也不去重

# Task Dependencies

- Task 2 依赖 Task 1（需要开关决定是否进入去重路径）
- Task 3 依赖 Task 2（需要知道哪些 collection 被跳过）
- Task 4 依赖 Task 2 与 Task 3（跳过导出 + 重定向映射就绪）
- Task 5 依赖 Task 2（需要去重统计结果）
- Task 6 无依赖，可与 Task 1–5 并行
- Task 7 依赖 Task 1–6 全部完成
