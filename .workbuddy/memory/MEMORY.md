# AssetRipper 项目长期记忆

## 设置/持久化（GUI Web）
- GUI = 本地 ASP.NET Core Web（Vue 前端 + C# 生成 HTML，`AssetRipper.GUI.Web`）。设置模型 `FullConfiguration`(Export/Processing/Import) → `AssetRipper.Settings.json`，仅 `ExportSettings.SaveSettingsToDisk` 勾选才落盘。
- **禁止往 `ExportSettings` 加"始终记住"的字段**：`SettingsPageGenerator` 反射其全部 public 属性生成设置页，会多出控件。→ 放独立文件（参考 `LastExportSettings`）。
- `SettingsPage.g.cs` 是**预生成静态文件**，构建不会更新。改设置字段后必须跑 `0Bins/Other/AssetRipper.GUI.SourceGenerator/Debug/*.exe`（顺带重排 en_US.json），或手工补 booleanProperties 条目 + WriteXxxFor。
- 前端初值：`VuePage.WriteScriptReferences` 里在 Vue 脚本前注入 `<script>window.x = {JSON};</script>`，JS 用 `window.x ?? default`；路径必须 `JsonSerializer.Serialize`（Windows 反斜杠）。

## Nikki4 专属类（AssetCreation/Nikki4）
- 手写专属类**必须复写 `ClassName`**（生成类由 AssemblyDumper Pass110 注入），否则继承基类名致 GUI/搜索/导出错乱。
- 组合/委托模式（如 Shader_Nikki4 内含 sealed 生成类）须 override WalkStandard/WalkEditor/WalkRelease 委托给内层，否则 YAML/JSON 只剩空壳基类字段。
- Nikki4 对象数据通常无 Object 头（hideFlags/PPtr），从名字字段直接开始。

## 构建与运行环境
- net10.0；.NET 10.0.301 已装。**PowerShell 的 `dotnet` 可用**（不回显 stdout → 写文件再 Read）。**优先走 Rider MCP**（`build_solution_start` → `build_solution_state --sessionId`）。
- 沙箱 bash 里 `dotnet restore` 必崩（NuGet path1 null）。用 `--no-restore` 前须 `export APPDATA/ProgramData/LOCALAPPDATA`（缺任一项报 NETSDK1060）。coreutils 缺失时改用 PowerShell。
- 新增工程/引用后必须先真实 restore，否则 NU1105。csc.exe 在黑名单。跨盘符绝对路径 ProjectReference 必失败，须同盘相对路径。
- 快速复现：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`；POST /Settings/Update、/LoadFile，GET /Collections/View、/Assets/Json|Yaml。
- `AssetRipper.GUI.Free` 是 `PublishAot=true`（本机 AOT publish 因缺 advapi32.lib 失败）。临时验证工程放 temp、勿落仓库；拷进 0Bins 的验证文件跑完必须删（`Remove-Item` 被 safe-delete 拦，用 `[System.IO.File]::Delete`）。

## 架构分层（硬约束）
- `AssetRipper.Logging` **零依赖**（禁止加任何 ProjectReference/重包，否则成环）；csproj 需 AllowUnsafeBlocks。取执行目录用 `AppContext.BaseDirectory`。Cpp2IL 桥接在 `Import/Logging/Cpp2ILBridge.cs`（ModuleInitializer）。
- `AssetRipper.Diagnostics`（含 `Memory.*`）：依赖 Logging + Assets + SourceGenerated 包 + ClrMD，**只被导出/入口层引用**。**不能放进 Assets**（SourceGenerated 反向引用 Assets 会成环，且会拖入 ClrMD）。
- 拆解的数据源是**显式参数** `IEnumerable<AssetCollection>?`，勿改静态注册（会钉住 GameData 致 Reset 后泄漏）。
- `MemoryDiagnostics.LogMemoryDiagnostics(stage, collections)`：先双轮 GC 再快照；增长 ≥ `RURI_MEM_BREAKDOWN_GROWTH_MB`（默认 1024）自动拆解，基线前移。口径 `RURI_MEM_BREAKDOWN_MODE`：默认=序列化字节数 / `live`=反射估算 / `clrmd`=整堆快照。另有 `RURI_MEM_BREAKDOWN_DUMP`、`_MAX_OBJECTS`。
- ClrMD 锁 **3.1.512801**（4.x 拖 Azure.Identity）。自快照可用；**不要**用 `AttachToProcess(suspend:false)` 读活进程。
- 生成类全名 `AssetRipper.SourceGenerated.Classes.ClassID_<n>.<类名>`（NuGet 包，仓库无源码），数字即 Unity ClassID。

## Bundle 名称/资源解析（性能敏感，勿退回线性扫描）
- 树形：`GameBundle`(根) → 每个 bundle 文件一个 `SerializedBundle`；松散 SerializedFile 直接进根的集合列表。大项目可达**几十万个子 Bundle**。
- `Bundle.ResolveCollection/ResolveResource` **必须**走“一层”索引 `_levelCollectionsByName` / `_levelResourcesByName`（自身条目 + 直接子 Bundle 条目，自身优先、子 Bundle 按序、首个匹配优先）。原实现对每层都遍历全部子 Bundle → 单次解析 O(子Bundle数)，30 万子 Bundle 时单次 9.6 ms，`InitializeAllDependencyLists` 直接数小时。
- 索引懒构建 + **条目数 ≤ 8 时线性扫描不建字典**（否则几十万个单集合 Bundle 各建一个微型字典）。条目数缓存用 `NotCounted = -1` 哨兵（0 是合法值）。
- 改动集合/资源/子 Bundle（`AddCollection`/`AddResource`/`AddBundle`/`Dispose`）后必须 `InvalidateLevelIndexes()`，且 `AddCollection`/`AddResource` 还要失效**父**节点。**不能改成增量 `TryAdd`**：新条目在“一层”顺序里排在子 Bundle 之前，同名时会输给已登记的子里条目，语义就变了。
- 语义约束（`AssetRipper.Assets.Tests/FileResolutionTests.cs` 已固化，24 例）：只向下探**一层**（孙 Bundle 的集合对根不可见）；同名取首个；资源键用 `NameFixed`（即 `FixFileIdentifier` 后的名字）。
- 缺失依赖日志必须去重（`Bundle.DependencyInitializationContext` + `DeduplicatingDependencyProvider`），逐条打印上限 1000 / 去重统计上限 10000，另有每 5 万集合的进度行。

## 文件扫描（`PlatformGameStructure.CollectFiles`，性能敏感）
- 瓶颈**不是目录枚举**，是**逐文件开关流**做类型判定（`SerializedFile.IsSerializedFile` / `BundleHeader.IsBundleHeader`）。30w 文件约半小时。
- 现走 `FileTypeScanner`：扩展名+体积预筛（零 syscall 排除）→ 候选批量并发读 32 字节头 → `HeaderProbe` 纯函数判定。**勿退回逐文件 `IsSerializedFile`**。
- `HeaderProbe` 是纯函数、无磁盘依赖，可单测；批量结果与原逐文件判定**必须完全一致**（`FileTypeScannerTests` 已固化 4 例）。
- `FileSystem.BatchReadHeaderPrefix` / `EnumerateFileInfos` 是**抽象原语**：改抽象签名写 `FileSystem.g.cs`，具体覆写写手写文件（否则再生成会丢）。`LocalFileSystem` 并发读头，基础类保持**串行**——`File.OpenRead` 的流池是进程级串行的，多线程用反而更慢。
- `AssetRipper.IO.Files.csproj` **零 ProjectReference**（只有 NuGet 包）→ **不能引用 `AssetRipper.Logging`**，诊断只能走可选 `Action<string>?` 回调参数。
- 扫描结果缓存 `FileScanCache`：键=`(目录, 扫描类型)`，按目录指纹（文件数+最后写入 UTC ticks）校验，**不符即该目录重扫、绝不静默信任**。默认**关闭**（`PlatformGameStructure.ScanCacheEnabled`/`ScanCachePath`），改动集合后须注意失效。
- `CollectAllSerializedFiles` 原有的“按名字去重”是**死代码**（`GameBundle.FromPaths` 只用 `Files.Values()`，名字被丢弃）→ 可安全改为 `AddRange`。

## ⚠️ .git 处于易失状态（操作前必须先备份）
- 本仓库 `.git/objects` 的 **loose 对象会消失**（曾被外部清理到 `loose_objects=0`），历史只在 3 个 pack 里（约 463MB / 4352 commit）。曾导致 `.git/refs/` 丢失 → 所有 git 命令报 `fatal: not a git repository`。
- **6 个 `optimize/*` 分支早已指向不存在的对象**（`invalid sha1 pointer`），`git fsck` 会有 244 条 reflog 错误——均为既存问题，**不要试图“修复”**。在用的 `alpha` / `master` 健康。
- 做任何 git 操作前，**先把改动文件复制到仓库外**；**不要用 `git stash`**（它要扫描全部对象，一遇缺失就整体失败且会留下半残状态）。
- 若再遇 `not a git repository`：`.git/refs/` 很可能被删。修复=从 `.git/logs/refs/**` reflog **最后一行第 2 个字段**取 SHA 重建 refs（注意 reflog 相对路径已含 `refs/`，别拼成 `refs/refs/`）；顶端对象若丢失，`git update-ref` 指回可读的父提交，再 `git read-tree <有效commit>` 重建索引（`reset --mixed` 会因索引引用缺失对象而失败）。
