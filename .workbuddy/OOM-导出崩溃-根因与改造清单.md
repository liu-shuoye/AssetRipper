# 导出 OOM 崩溃：根因分析与改造清单（待评审）

> 现象：Nikki4 全量导出运行到 `ExportProgress : (10374/725277) 正在导出 'img_egypt_book_pop_bg'` 时进程抛出
> `System.OutOfMemoryException`，栈顶为 `AssetRipper.Conversions.FastPng.fpng_encode_image_to_memory`
> 的原生 `std::vector` 扩容，异常穿透到 ASP.NET 中间件，整场导出中断。
>
> 结论：**FastPng 的无内存可用只是表象，根因是托管堆在处理阶段膨胀到 ~59 GB，远超本机 32 GB 物理内存。**

---

## 一、证据

### 1.1 环境

| 项 | 值 |
|---|---|
| 进程位数 | 64 位（PE `machine=0x8664`） |
| 物理内存 | 33,386,488 KB ≈ 31.8 GB |
| 页面文件 | 已分配 57,344 MB |
| GC 模式 | Server GC（日志：`服务器GC 是`） |
| 数据源日志 | `Source/0Bins/AssetRipper.GUI.Free/Debug/AssetRipper_20260916_220724.log` |
| 运行时长 | 22:07 起，09:15 崩溃 → 约 11 小时只推进 1.4%（≈3.8 s / 集合） |

### 1.2 阶段内存曲线（`[内存诊断]` 行）

| 阶段 | 托管堆 | 工作集 | 增量 |
|---|---|---|---|
| 加载文件和依赖项前 | 1,988 MB | 2,434 MB | — |
| 加载文件和依赖项后 | 5,950 MB | 8,415 MB | +3,962 MB |
| Load 完成（懒加载，未反序列化） | 5,402 MB | 8,309 MB | — |
| Process 开始 | 5,403 MB | 8,000 MB | — |
| Process 后 - SceneDefinitionProcessor | 6,266 MB | 8,636 MB | +864 MB |
| **Process 后 - OriginalPathProcessor** | **42,908 MB** | **243 MB** | **+36,642 MB** |
| Process 后 - MainAssetProcessor | 42,913 MB | 141 MB | ~0 |
| **Process 后 - EditorFormatProcessor** | **58,613 MB** | **191 MB** | **+15,701 MB** |
| Process 完成 | 59,055 MB | 4,261 MB | +442 MB |
| Export 前 - DoFinalOverrides 完成 | 59,061 MB | 13,048 MB | +6 MB |
| 崩溃（10374/725277） | — | — | — |

工作集在多个阶段跌到 **15 / 78 / 140 / 243 MB**：只有系统级内存极度紧张、内核强制裁剪工作集时才会出现这种数值，说明整个运行过程都在硬缺页换页。

### 1.3 ClrMD 整堆快照（`Process 后 - OriginalPathProcessor`，51,188 MB / 561,204,450 个对象）

| 排名 | 类型 | 数量 | 占用 |
|---|---|---|---|
| 1 | `System.Byte[]` | 36,968,778 | 14,548 MB |
| 2 | `Free`（碎片空洞） | 110,350 | **8,467 MB（16.5%）** |
| 3 | `System.UInt32[]` | 454,996 | 4,366 MB |
| 4 | `SerializableValue[]` | 30,927,688 | 2,513 MB |
| 5 | `System.String` | 24,519,294 | 1,681 MB |
| 6 | `SerializableStructure` | 30,927,688 | 1,416 MB |
| 7 | `AnimationCurve_Single_2018` | 29,241,704 | 1,339 MB |
| 8 | `Utf8String` | 37,960,525 | 1,159 MB |
| 9 | `ColorRGBAf` | 36,978,245 | 1,129 MB |
| 10 | `UInt4StorageAligned` | 29,571,133 | 902 MB |

命名空间分布：`System` 21,375 MB（41.8%）／`AssetRipper` 19,908 MB（38.9%）／其它 9,598 MB（18.8%）。
其中 Unity 资产类共 5,176,964 个对象 / 960 MB，最大的几类：GameObject 1,402,768、Transform 1,182,987、MonoBehaviour 542,526、ParticleSystem(-Renderer) 各 193,433、SkinnedMeshRenderer 180,725、Texture2D 181,912。

### 1.4 崩溃点为什么是 FastPng

`DirectBitmap<TColor,TChannel>.SaveAsPng`（`Source/AssetRipper.Export.Modules.Textures/DirectBitmap`1.cs:176-206`）在图像尺寸 ≤ 65535 时走 `FPng.EncodeImageToMemory`。fpng 的实现（`fpng.cpp`）在单次调用内固定分配两块与像素数据等长的缓冲：

- `temp_buf`：`(bpl + 1) * h + 7` ≈ N（N = w × h × channels）
- `out_buf`：`(58 + N + h + 7) & ~7` ≈ N

即**一次编码需要额外 2N 的连续原生内存**（N=64 MiB 的 4K RGBA 纹理即需 ~128 MiB），加上托管侧 `data` 与返回的 `byte[]`，峰值约 4N。这些原生分配走 `VirtualAlloc`，**无法复用 GC 已提交的段**；在系统提交量已被 59 GB 托管堆吃满时必然失败。

**所以这不是"这张图太大"，而是"进程已经没有可提交的内存"。**

---

## 二、根因拆解

### R1（主因）两处代码击穿了 fork 自己的懒加载设计

fork 已经为懒加载做了大量改造（`AssetCollection.EnumerateAssetMetadata()` / `TryGetAssetOnly()`、`SerializedAssetCollection` 的单对象反序列化），但下面两处仍在走"全集合物化"的老路径：

**R1-a `OriginalPathProcessor` 的容器遍历**

`Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs`

- L96-119 `SetOriginalPaths(IResourceManager)`：`kvp.Value.TryGetAsset(manager.Collection)`
- L138-186 `SetOriginalPaths(IAssetBundle, ...)`：`kvp.Value.Asset.TryGetAsset(bundle.Collection)`

调用链：

```
AssetCollection.TryGetAsset(long pathID)            // AssetCollection.cs:290
  └─ EnsureAssetsLoaded()                            // AssetCollection.cs:293 ← 触发整集合全量反序列化
```

这两条路径对**每个 bundle 容器的每个条目**触发一次；而 bundle 容器基本覆盖全游戏，因此等价于全量物化。同类里 `GroupByBundleName` 分支已经改成"元数据枚举 + 集合级 OriginalDirectory"，说明改造方向已被验证，只是这两条容器路径漏了。
紧接其后的 `UndoPathLowercasing(asset)`（L211-222）读取 `asset.Name`，会进一步把 `INamed` 资产的字段树整体拉起来。

**R1-b `EditorFormatProcessor` 的资产枚举**

`Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs:106-114`

```csharp
private static IEnumerable<IUnityObjectBase> GetReleaseAssets(GameData gameData)
    => GetReleaseCollections(gameData).SelectMany(c => c);
```

`SelectMany(c => c)` 走 `AssetCollection.GetEnumerator()`（`AssetCollection.cs:402-406`）→ `EnsureAssetsLoaded()`。
并且 L93（顺序）与 L99（`Parallel.ForEach`）各枚举一次。

**这两处是整个 59 GB 曲线的直接来源（+36.6 GB、+15.7 GB）。**

### R2（结构性）导出阶段本身要求全量物化

`ProjectExporter.CreateCollections` 与 `ProjectAssetContainer` 都使用 `fileBundle.FetchAssets()`，`FetchAssets` 最终也走 `GetEnumerator()`。因此：

> **即使修好 R1，导出阶段的峰值仍然由"全游戏物化"决定，只是物化时机推后、曲线更平缓。**

要真正把峰值压到 32 GB 以下，必须做"分批"或"流式"，见 P2。

### R3（放大器）白名单没有在"物化之前"生效

`ExportSettings`/`ImportSettings.EffectiveImportAssetTypes` 已存在，且在 `ProjectExporter.CreateCollections` 中用于过滤集合。但 **Process 阶段完全无视白名单**：R1-a / R1-b 会把白名单外的类型（例如只导纹理时的 Mesh / AnimationClip / AudioClip）也全部物化成对象。

这是最直接的杠杆：**白名单外的资产不应被物化，甚至不应被反序列化。**

### R4（放大器）LOH 碎片 8.5 GB

`Free` 块 110,350 个 / 8,467 MB（16.5%）。全程 Server GC + 无 LOH 压缩 + 反复申请释放大数组（`Byte[]` 3,697 万个、平均 412 B，但顶端有大块），碎片几乎无法回收。这部分是"可用内存里白扔掉的 1/6"。

### R5（放大器）纹理数据与动画曲线体量

`AnimationClip_Nikki4` 28,772 个，对应 `AnimationCurve_Single_2018` 2,924 万个——即平均每个动画剪辑约 1,000 条曲线，属于游戏本身规模（不是 bug），但它决定了 `AssetRipper` 命名空间里 19.9 GB 的地板高度。
`Texture2D` 181,912 个，是 `Byte[]` 14.5 GB 的重要来源；`StripTexture2DData`（占位模式）已在 fork 中实现，可显著削减。

### R6（可用性）导出循环没有异常隔离

`ProjectExporter.Export`（`Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs:121-137`）：

```csharp
bool exportedSuccessfully = collection.Export(container, options.ProjectRootPath, fileSystem);
```

没有 try/catch。`OutOfMemoryException` 直接穿透 `ExportHandler` → `GameFileLoader.ExportUnityProject` → ASP.NET 中间件。**一个资产的失败作废了整场 11 小时的运行。**

### R7（配置）Server GC 默认不归还内存

`服务器GC 是`。Server GC 为每个核心维护独立堆段，段归还更保守，且对 32 GB 单机 + 大 LOH 场景并不总是最优。结合 `.NET` 8+ 的 `System.GC.ConserveMemory` / `GCHeapHardLimit` 值得评估。

---

## 三、改造清单

> 优先级定义：**P0 = 立刻可用且低风险**；**P1 = 显著改善，需回归**；**P2 = 结构性，需设计评审**。

### P0-1 导出循环加异常隔离与失败清单

- 文件：`Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs`（L121-137）
- 做法：
  1. `collection.Export(...)` 包 `try/catch`，捕获 `OutOfMemoryException` 与一般 `Exception` 分别记录（资产名、集合类型、异常类型、消息）；
  2. 失败计数 + 按异常类型聚合，循环结束打印汇总（如 `导出完成：成功 N，失败 M（OOM a / 其它 b）`）；
  3. OOM 分支做一次"降压"处理：`GCSettings.LargeObjectHeapCompactionMode = CompactOnce` → `GC.Collect()`，让下一轮有可复用空间；
  4. 清理半成品：`TextureAssetExporter.Export` 中 `fileSystem.File.Create(path)` 已建的文件在异常时必须删除，`.meta` 不应生成。
- 风险：OOM 后进程状态是否可信。此处 OOM 来自**一次性原生分配失败**，托管侧对象图完整，捕获后继续是安全的；但仍应把连续 OOM 计数作为熔断条件（例如连续 50 个 OOM 则主动终止并提示内存不足）。
- 收益：任何单个坏资产不再作废整场导出。

### P0-2 `OriginalPathProcessor` 两处容器遍历改单对象反序列化

- 文件：`Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs`（L96、L146）
- 做法：`collection.TryGetAsset(pathID)` → `collection.TryGetAssetOnly(pathID)`；`TryGetAssetOnly` 只反序列化目标对象、不触发 `EnsureAssetsLoaded`（`SerializedAssetCollection.cs:198-215` 已实现）。
- 注意：
  - `TryGetAssetOnly` 会把对象放进 `assets` 字典，所以"不再有整集合爆炸"指的是**不会因为容器里的一个条目而连带物化同集合的其余 100 个资产**；这是收敛物化范围的必要前提（配合 P0-3）。
  - 该返回 `IUnityObjectBase?`，原有强类型逻辑需按需 `as`/`is` 转换。
- 风险：低。语义等价（同一个 PathID、同一个对象），只是加载范围变小。

### P0-3 `EditorFormatProcessor` 按 ClassID 过滤后再反序列化

- 文件：`Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs:106-114`
- 做法：
  1. `GetReleaseAssets` 改为遍历 `EnumerateAssetMetadata()`；
  2. 只对 `Convert` / `ConvertAsync` 两个 switch 真正需要的 ClassID 调 `TryGetAssetOnly`；
  3. 需要的 ClassID 集合从 switch 的接口反推（GameObject 1、Transform 4、Mesh 43、Renderer 系列 23/199/137/…、SpriteAtlas 687078895、AnimationClip 74、NavMeshSettings 196、PlayerSettings 129、AssetBundle 142、Terrain 218、PlayableDirector 320、MeshFilter 33、GraphicsSettings 30、QualitySettings 47、Physics2DSettings 19、LightmapSettings 157、LightingSettings 850595691、UnityConnectSettings 310）。
- 关键收益：**`Texture2D`(28)、`AudioClip`(83)、`Mesh`(43) 之外的大量类型不再被 Process 阶段物化**（与 P0-4 叠加时矩阵最大）。
- 风险：ClassID → 接口的映射需与 switch 的 `is` 判定严格对齐（特别是 Renderer 的多版本 ClassID）；建议写一个单元测试，用一个包含全部 ClassID 的假集合断言"Process 后目标集合的已反序列化数量 == 预期集合大小"。

### P0-4 白名单在物化之前生效

- 文件：`Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs`、`OriginalPathProcessor.cs`（或统一放到处理器入口）
- 做法：把 `options.ImportSettings.EffectiveImportAssetTypes` / `ImportAssetTypeExtensions.IsClassIdAllowed` 判定前置到"决定是否 `TryGetAssetOnly`"这一步。白名单外的 ClassID 直接跳过，连元数据以外的东西都不加载。
- 收益：这是把 59 GB 变成可控数字的唯一低风险杠杆。只导纹理/只导脚本的批次，峰值可下降一个数量级。
- 风险：与 `ProjectExporter.CreateCollections` 的过滤必须**同源同规则**（现有代码已经共用 `IsClassIdAllowed`，保持一致即可）。

### P1-1 Process 结束与导出期做 LOH 压缩

- 文件：`Source/AssetRipper.Export.UnityProjects/ExportHandler.cs`（`Export` 开头）/ `ProjectExporter.cs`
- 做法：`GCSettings.LargeObjectHeapCompactionMode = CompactOnce; GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`，并按需 `GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency` 之外的选择评估。
- 收益：直接回收 8.5 GB 量级的碎片（R4），并让段归还给 OS，为 fpng 的原生分配腾出地址空间与提交量。
- 风险：压缩是一次 stop-the-world，几十秒量级，对 11 小时的任务可忽略。

### P1-2 导出期内存看门狗

- 文件：`ProjectExporter.cs`（循环内）
- 做法：每 N 个集合（例如 500）打印一次"托管堆 / 工作集 / 累计失败数"；当托管堆超过阈值或 OOM 次数上升时主动压缩并告警。
- 收益：下次出问题时**能在崩溃前**看到拐点，而不是事后翻日志；同时天然暴露 R3 这类"白名单没生效"的回归。

### P1-3 GC 配置评估

- 做法：评估 `ServerGarbageCollection=false`、`System.GC.ConserveMemory=9`、`System.GC.RetainVM=0`、必要时 `GCHeapHardLimit`（给原生分配留出硬预算）。
- 风险：需实测，可能换来吞吐下降；建议先在 `AssetRipper.GUI.Free` 上单变量对比。

### P2-1 分批导出（不改架构即可用）

- 做法：用现有类型白名单把一次全量导出拆成若干批次（例：纹理批 / 网格与模型批 / 动画批 / 场景与脚本批），每批独立进程运行、共用同一输出目录。
- 收益：每批峰值独立，最直接地绕开 32 GB 上限。
- 代价：需要确认跨批次的 GUID/引用一致性（`EnableDeterministicGuids` 已存在，正好解决跨批次 GUID 漂移）。

### P2-2 真正的流式导出（长期）

- 做法：让 `ProjectExporter` 按集合（或按 bundle）处理：`EnsureAssetsLoaded` → 建集合 → 导出 → `UnloadAssets()` + 编译器级释放；前提是 `AssetCollection.UnloadAssets()`（`AssetCollection.cs:268-271`）与 `SerializedAssetCollection` 的 `_sourceFile` 生命周期支持"处理完再重载"。
- 现状：`SerializedAssetCollection` 注释明确写着 "`UnloadAssets 无调用方`"，且 `Dispose` 会把 `_sourceFile = null` 变成不可重载。这条路需要先把数据源生命周期设计清楚。
- 收益：峰值从"全游戏"降到"最大单集合 + 常数"。

### P2-3 纹理侧

- `StripTexture2DData`（占位模式，`TextureAssetExporter.cs:47-58`）可削减 181,912 个 Texture2D 的 `Byte[]` 占用；代价是导出的是同尺寸纯白占位图，取决于这次导出的目的。
- 另一个可选方向：对超过某像素预算的纹理**跳过 fpng**（回落到 `StbImageWriteSharp`）或直接降低单次编码的并行度，以降低 2N 的原生尖峰。注意 stb 的 PNG 写同样是内存内缓冲，收益有限——**这一层只是缓冲，不是解法**，优先级应低于 P0/P1。

---

## 四、如何验证修好了

1. **不崩**：构造一个会让单资产抛异常的场景（或临时注入），确认导出完整跑完并打印失败汇总（P0-1）。
2. **峰值下降**：复跑后对比 `Export 前` 的托管堆。当前基线 **59,061 MB**；预期 P0-2 + P0-3 后显著下降，P0-4（配白名单批次）后可降到个位数 GB。
3. **对象数下降**：`RURI_MEM_BREAKDOWN_MODE=clrmd` 已开启，对比 `共 561,204,450 个对象` 这一行。
4. **速率回归**：当前 ≈3.8 s/集合（换页导致）。修好后应显著加快——这是"内存压力是否解除"最灵敏的指标。
5. **碎片**：整堆快照里 `Free` 应从 8,467 MB（16.5%）明显下降（P1-1）。

---

## 五、明确不做的部分（请确认）

- 不改 `AssetRipper.Conversions.FastPng`（NuGet 包，且 fpng 的实现本身就要求 2N 连续缓冲）。
- 不动 `DirectBitmap.SaveAsPng` 的编码策略（P2-3 只作为可选缓冲，默认不做）。
- 不改 `_sourceFile`/`UnloadAssets` 的生命周期（P2-2 需要独立设计评审后再动）。

---

## 六、实施记录

### P0-1 导出循环异常隔离与失败清单 —— 已完成（2026-09-17）

改动文件：

| 文件 | 改动 |
|---|---|
| `Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs` | 循环内 try/catch（OOM 与一般异常分开处理）、失败记账与汇总、OOM 熔断；新增私有嵌套类 `ExportFailureTracker` |
| `Source/AssetRipper.Export.UnityProjects/ExportCollection.cs` | 新增 `DeletePartialExportFiles`；`ExportAsset` 失败时清理半成品并重抛 |
| `Source/AssetRipper.Export.UnityProjects/AssetExportCollection.cs` | `Export` 失败时清理半成品并重抛 |

策略取值与理由：

- **熔断阈值：连续 5 次 OOM，或累计 20 次 OOM**。取小值是刻意的：每次 OOM 后要做一次 LOH 压缩，
  而 59 GB 堆上的压缩代价以分钟计；连续发生即说明是系统级内存不足而非偶发尖峰，继续跑只会拖慢失败。
  注意熔断后仍会走 `EventExportProgressUpdated` / `EventExportFinished`，UI 状态不会卡住。
- **完整堆栈只在同类异常首次出现时输出**，其余只输出一行摘要：否则几十万个失败会把日志刷爆。
- **LOH 压缩只在 OOM 分支做**，正常路径不付这个 stop-the-world 成本。

清理半成品的覆盖范围：`AssetExportCollection<T>.Export`（含 `AssetsExportCollection<T>`、纹理、音频、
YAML 音频/着色器等所有走 `ExportInner` 的集合）与 `ExportCollection.ExportAsset`（走 YAML 多文件写入的集合）。
**直接重写 `Export(string, string, FileSystem)` 而不经过这两条路径的集合（场景、脚本、Manager、
EditorBuildSettings、UserAsset、Unreadable/Unknown）不在覆盖范围内**，它们写入中断仍可能留下半成品文件；
这些集合数量少、写入内容小，暂按已知缺口处理。

验证情况：`AssetRipper.Export.UnityProjects` 与 `AssetRipper.GUI.Free` 均编译通过（0 error），
`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe` 已刷新。
**尚未做真实导出验证**——需要一次完整跑批（或至少跑到出现失败资产）才能确认汇总行与熔断行为；
判定成功的标志是日志末尾出现 `导出完成：N/N 个集合全部成功。` 或 `导出失败汇总：……`。

