# 去重实现改造：GUID 统一 + 全资源重定向

## 摘要（Summary）

按用户提出的"先标记真正导出的资源、重复资源共享同一 GUID、重复资源不导出"思路改造导出去重。具体落地为：

1. 先依据**完整集合内容（含全部子资源）**识别重复组，每组只保留一个真正导出的集合（keeper）；
2. 组内所有被跳过集合的**每一个资源**（含主资源与子资源）统一重定向到 keeper 中对应资源，从而在引用层面让所有重复对象解析为 keeper 的 GUID + 真实存在的 fileID，引用不再丢失。

同时修复现有实现的两处缺陷：只重定向"主资源"导致子资源引用丢失；以及"主资源内容相同但子资源不同"被误判为重复而误删。

## 现状分析（Current State Analysis）

事实（均来自源码核对）：

- `ProjectExporter.ApplyDeduplication`（`Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs:174-262`）只把被跳过集合的**主资源** `Assets.FirstOrDefault()` 写入重定向表：
  `redirectMap[skip.Assets.FirstOrDefault()!] = keep.Assets.FirstOrDefault()!`（第 229-230 行）。
- 去重 key 仅取主资源的 `(Type, ContentHash)`（第 216 行），`ContentHashWalker.ComputeHash` 只对单个主资源哈希。
- `ProjectAssetContainer`（`ProjectAssetContainer.cs`）构造时把被跳过集合整体排除出 `m_assetCollections`（第 32-39 行），`GetExportID` / `CreateExportPointer` 先查 `redirectMap`（第 64-77、84-97 行）。
- 因此：主资源引用正确重定向、不丢；但被跳过集合的**非主资源（子资源）**既不命中 `redirectMap` 也不在 `m_assetCollections`，`CreateExportPointer` 落到 `MetaPtr.CreateMissingReference`、`GetExportID` 落到 `ExportIdHandler.GetMainExportID` → 引用丢失。
- 子资源集合实例：`TextureExportCollection`（`Textures/TextureExportCollection.cs:25-35`，主资源 Texture2D + 多个 Sprite），`FontAssetExportCollection`（`Miscellaneous/FontAssetExportCollection.cs:19-28`，Font + Material + Texture），`PrefabExportCollection`（`Project/PrefabExportCollection.cs:20-22`，整个层级）。
- 子资源 fileID：Texture 用 `m_nextExportID` 递增（`TextureExportCollection.cs:75-80`），Font 用 `GetMainExportID`，默认用 `GetPseudoRandomExportId(asset, m_exportIDs.Count)`（`AssetsExportCollection.cs:53-56`）——不同 bundle 的子资源插入顺序可能不同，故不能按位置配对。
- 集合 GUID 在构造时即确定：`AssetExportCollection<T>.GUID => UnityGuid.NewGuid()`（`AssetExportCollection.cs`），与导出顺序无关 → 直接复用 keeper 自身 GUID 即可让整组重复对象"共享同一 GUID"，无需额外生成。
- 引用形式：场景/对象对子资源的引用是 `MetaPtr(fileID, guid, type)`（`AssetExportCollection.CreateExportPointer` → `MetaPtr.ExportYaml`），其中 fileID 必须是 keeper 导出文件中**真实存在**的子资源 id。

结论：用户思路方向正确，但仅有"共享 GUID"不足以修复子资源引用，还需满足两个前提——(a) 以完整集合内容为准判重复，杜绝"字节相同但子资源不同"的误判；(b) 对每个资源（含子资源）做一对一重定向，使引用的 fileID 落在 keeper 文件中真实存在的位置。

## 变更方案（Proposed Changes）

### 1. `Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs` — 重写 `ApplyDeduplication` 并新增辅助

**改：** 去重 key 由"主资源 (Type, hash)"改为"完整集合内容指纹"：

- 对集合内每个资源计算 `(ClassID, contentHash)`，整体排序后得到一个确定性复合 key；
- **仅当两个集合的复合 key 完全相同才视为重复组**，杜绝"主资源相同但子资源不同"被误判；
- 任一资源为 `ContentHashWalker.Unhashable` 时，该集合保守不去重（保留），沿用现有安全策略。

**改：** 分组方式由"边扫边覆盖 keeper"改为"两遍式"，规避现实现中 keeper 被替换后旧 redirect 指向新 skip 的悬空隐患：

- 第一遍：收集每个集合的指纹并按指纹分组；
- 第二遍：对每组用 `IsCollectionPreferred`（现有确定性规则：目录名与文件名相似度优先、同分回退字典序）在组内决出唯一 keeper；组内其余集合全部入 `skippedCollections`。

**改：** 为每个被跳过集合构建**全资源重定向**：

- 对 `skip.Assets` 中每个资源 b，在 `keeper.Assets` 中按 `(ClassID, contentHash)` 找到对应资源 a，写入 `redirectMap[b] = a`；
- 找不到对应者（防御分支）则不写入，交回既有 fallback 逻辑；
- 复用同一 `hashCache` / `visiting`，对每个资源只哈希一次，控制成本。
- 对主资源而言，其对应者即 keeper 主资源，行为与现状一致。

**保留：** `skippedByType` 汇总日志与总计数日志（第 241-261 行）不变。

### 2. `Source/AssetRipper.Export.UnityProjects/ProjectAssetContainer.cs` — 无需改动

`redirectMap` 语义从"仅主资源"扩展为"全部资源"后，容器无需改动：`GetExportID` / `CreateExportPointer` 已最先查询 `redirectMap`，命中即解析；被跳过集合仍排除在 `m_assetCollections` 之外，不会重复导出。

### 3. `Source/AssetRipper.Export.UnityProjects/ContentHashWalker.cs` — 可选小改

可在 `ProjectExporter` 内循环调用现有 `ComputeHash(asset, hashCache, visiting)` 并复用缓存，无需改动本文件。若希望 API 更清晰，可新增一个接收"资产列表 + 共享缓存"的便捷重载；非必需，作为可选项。

### 4. `Source/AssetRipper.Tests/DeduplicationTests.cs` — 新增用例

- **T1（子资源全资源重定向）**：构造两个"含子资源且内容完全相同"的 `TextureExportCollection`（Texture2D + 相同 Sprite）。去重后，引用被跳过集合 sprite 的 `CreateExportPointer` 结果与引用 keeper sprite 的结果相同（guid 与 fileID 均一致）。
- **T2（主资源相同、子资源不同 → 不去重）**：两个集合 Texture2D 字节相同但 sprite 子资源不同，去重后 `skippedCollections` 不含其中任何一个，两个集合都被导出。
- **T3（守护既有行为）**：现有"主资源重定向"与"场景豁免"用例保持通过。

## 假设与决策（Assumptions & Decisions）

- **本方案以用户提出的 GUID 统一思路为准实现**，而非"仅排除多资源集合"的最小改动；因为用户明确要求换思路，且该思路在补足下述两前提后是正确且覆盖面更广的。
- **仅共享 GUID 不足以修复子资源**：Unity 引用为 `(guid, fileID)`，fileID 必须指向 keeper 导出文件中真实存在的子资源，故必须做全资源一对一重定向。
- **GUID 复用 keeper 构造时已生成的 GUID**，不新增生成逻辑，天然实现"组内重复资源共享同一 GUID"。
- **keeper 判定沿用 `IsCollectionPreferred`** 的确定性规则，保证同一批资源去重结果可复现。
- **代价权衡**：判定"完整集合内容相同"比"仅主资源相同"更严格，会减少可去重的资源数（凡含不同子资源的集合不再合并），但换取引用正确性；完全重复的内建/图集贴图、重复预制体等仍正常去重。

## 验证（Verification）

1. `dotnet build` 编译通过。
2. 运行 `DeduplicationTests`，新增 T1/T2/T3 及既有用例全部通过。
3. 手工验证：对含"重复内建/图集贴图 + 重复预制体"的游戏包，在开启 `EnableAssetDeduplication` 下导出，检查场景/预制体 YAML 中对被去重子资源的 `guid`/`fileID` 均指向保留文件且可解析；关闭去重时行为与改造前一致。