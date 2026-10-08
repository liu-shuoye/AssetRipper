# AssetRipper 项目长期记忆

> 历史细节见 `memory/2026-09-*.md`；本文件只保留仍然有效的硬约束与结论。

## 构建与运行环境
- net10.0。**优先 Rider MCP**（`build_solution_start` → `build_solution_state --sessionId`）。
  - ⚠️ Rider 偶尔**假失败**（`buildIsSuccess:false` + `problems:[]`）→ 用 dotnet CLI 复核：沙箱 bash 先 `export APPDATA/ProgramData/LOCALAPPDATA`，再 `dotnet build --no-restore -v m > out.txt 2>&1`，Read（GBK）。
  - 沙箱 `dotnet restore` 必崩；沙箱缺 coreutils（head/grep/find 可能 127）→ 用内置 Grep/Glob。
- 新增工程/引用后必须真实 restore，否则 NU1105。跨盘符绝对路径 ProjectReference 必失败，须同盘相对路径。
- 快速复现：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`（Ookii 风格参数）。
  - POST `/Settings/Update` 字段名是**不带类名前缀**的 `nameof(...)`（`GameType=Nikki4`）；`/LoadFolder` 用 `Path=`。加载同步阻塞。
  - GET `/Collections/View`、`/Assets/Json|Yaml`。用 `--noproxy curl` 或 python urllib。
  - `/Settings/Update` **整体重写所有布尔项**：表单没带的键一律置 false。改前备份或直接改 JSON。
- `AssetRipper.GUI.Free` 是 `PublishAot=true`（本机 AOT 因缺 advapi32.lib 失败）。临时验证工程放 temp；拷进 0Bins 的文件跑完必须删（`[System.IO.File]::Delete`）。

## ⚠️ 用户配置文件 = 用户数据（2026-10-07 踩坑）
- `0Bins/AssetRipper.GUI.Free/Debug/` 下 `AssetRipper.Settings.json` 与 `AssetRipper.LastExport.json` 是**用户真实配置**（0Bins 被 gitignore，无法 git 恢复）。
- 验证脚本必须**先备份 → 跑 → 用备份覆盖回**。删目录用 `[System.IO.Directory]::Delete(path,$true)`（绕过 safe-delete shim）。
- 误删恢复：`Debug/AssetRipper_*.log` 里有每次加载的完整 `Configuration Settings:` 块与导出路径，据此重建并逐项比对。

## ⚠️ 本沙箱陷阱
- **后台进程撑不过一次工具调用边界**；端到端 HTTP 验证须在**同一个 Bash 调用**里完成启动→请求→收结果→kill。
- `Select-Object -First N` 接进程输出会提前关闭管道并终止进程，别用它截日志。
- **Python 会把字符串字面量里的反斜杠改成正斜杠**；写 Windows 反斜杠路径用 PowerShell + .NET `WriteAllText`。
- 日志编码：应用自写 `AssetRipper_*.log` 是 **UTF-8**；`> out.txt` 重定向捕获的 stdout 是 **GBK**。

## 用户当前环境（Nikki4，2026-10）
- 游戏数据：`D:\UserData\GameData\閃耀暖暖_4.1.2328503`；依赖图 `D:\UserData\閃耀暖暖_4.1.2328503\AssetRipper.DependencyMap.json`。
- IL2Cpp dump：`E:\Unpack\Il2CppDumper\input\output\DummyDll`；Unity 项目：`C:\Unity\Nikki4Test`。
- 导出目录：`D:\UserData\ex`（自动导出在建 `AssetRipper_export_<时间戳>`）。
- 关键设置：`GameType=Nikki4`、`ShaderExportMode=Disassembly`、`BundledAssetsExportMode=MainAssetFolder`、`MemoryBreakdownMode=ClrMd`、`AutoExportKind=UnityProject`、`LanguageCode=zh-Hans`、`SaveSettingsToDisk=true`。

## 设置/持久化（GUI Web）
- 设置模型 `FullConfiguration`(Export/Processing/Import) → `AssetRipper.Settings.json`，仅 `ExportSettings.SaveSettingsToDisk` 勾选才落盘。
- **禁止往 `ExportSettings` 加"始终记住"字段**：`SettingsPageGenerator` 反射全部 public 属性生成控件，会多出 UI。→ 用独立文件（参考 `LastExportSettings`）。
- **导入阶段行为放 `ImportSettings`**（设置页只在未加载时可编辑）；`ImportSettings` **不能反向引用 `GUI.Web`**。
- **自动导出**（`GameFileLoader.ExportAfterLoadAuto`）**必须强制追加时间戳子文件夹**（无法弹窗确认清空），统一走 `AppendTimestampedSubfolder`。
- `SettingsPage.g.cs` 是**预生成静态文件**，构建不更新。改设置字段后须跑 `0Bins/Other/AssetRipper.GUI.SourceGenerator/Debug/*.exe`（顺带重排 en_US.json）。
  - ⚠️ 生成器用**它自己目录里那份 `AssetRipper.Import.dll`**：先构建 Import → 复制 dll/pdb 进生成器目录（备份、用完还原）→ 再跑 exe。
- 新增设置项清单：字段 + `Log()` 一行 + `SettingsPage.cs` 控件 + 跑生成器 + `Localizations/{en_US,zh_Hans,zh_Hant}.json` 的 `xxx`/`xxx_description`（仅 en_US 参与生成）。
- 设置**下发时机**：`GameStructure` 构造函数里 CollectFiles **之前**（`ApplyScanCacheSettings`）。
- 前端初值：`VuePage.WriteScriptReferences` 注入 `<script>window.x = {JSON};</script>`，路径必须 `JsonSerializer.Serialize`。
- `Configuration Settings:` 块在 **`GameFileLoader.LoadAndProcess` 打印（每次加载一次）**，可核对上次 POST 是否生效。
- **导出路径优先级**（`ResolveBasePathFromCandidates`）：`ImportSettings.AutoExportPath` → `LastExport.ExportPath` → 导入目录同级。预填用 `PreferredExportPath`（不做第三级回退）。手动导出经 `RememberExportPath` 双写。

## 架构分层（硬约束）
- `AssetRipper.Logging` **零依赖**；csproj 需 AllowUnsafeBlocks；用 `AppContext.BaseDirectory`。Cpp2IL 桥在 `Import/Logging/Cpp2ILBridge.cs`（ModuleInitializer）。
- `AssetRipper.Diagnostics`（含 `Memory.*`）：依赖 Logging + Assets + SourceGenerated + ClrMD，只被导出/入口层引用；**不能放进 Assets**（成环 + 拖入 ClrMD）。
- ClrMD 锁 **3.1.512801**（4.x 拖 Azure.Identity）。自快照可用；**不要** `AttachToProcess(suspend:false)`。
- 拆解数据源是显式参数 `IEnumerable<AssetCollection>?`，勿改静态注册（钉住 GameData 致泄漏）。
- 生成类全名 `AssetRipper.SourceGenerated.Classes.ClassID_<n>.<类名>`（NuGet 包，仓库无源码）。

## Nikki4 专属类（AssetCreation/Nikki4）
- 手写专属类**必须复写 `ClassName`**，否则 GUI/搜索/导出错乱。
- 组合/委托模式须 override WalkStandard/WalkEditor/WalkRelease 委托给内层。
- Nikki4 对象数据通常无 Object 头，从名字字段直接开始。

## Bundle 名称/资源解析（性能敏感）
- `Bundle.ResolveCollection/ResolveResource` **必须**走"一层"索引（自身条目 + 直接子 Bundle，自身优先、首个匹配）。曾线性扫描致单次 9.6 ms。
- 索引懒构建 + 条目数 ≤ 8 时线性扫描（`NotCounted = -1` 哨兵）。改集合后 `InvalidateLevelIndexes()`，含**父**节点；**不能**改增量 `TryAdd`（语义变）。
- 语义（`FileResolutionTests` 24 例固化）：只探一层；同名取首；资源键 `NameFixed`。
- 缺失依赖日志去重：`DependencyInitializationContext` + `DeduplicatingDependencyProvider`；逐条上限 1000 / 统计上限 10000。

## 文件扫描（`CollectFiles`）
- 走 `FileTypeScanner`：扩展名+体积预筛 → 批量并发读 32 字节头 → `HeaderProbe` 纯函数。**勿退回逐文件 `IsSerializedFile`**。
- **一个文件只能被一种扫描收集**；`MatchesSerializedFile` 要「头部声明大小 == 实际大小」。口诀：**目录文件数 × 2 == 最大编号 → 收集了两遍**。
- `BatchReadHeaderPrefix`/`EnumerateFileInfos` 抽象原语：改签名写 `FileSystem.g.cs`，具体覆写写手写文件。
- `AssetRipper.IO.Files` **零 ProjectReference** → 不能引用 Logging，诊断走 `Action<string>?` 回调。
- 扫描缓存 `FileScanCache`：键 `(目录, 类型)`，按目录指纹校验，不符即重扫；默认关闭；`ImportSettings.EnableFileScanCache/FileScanCachePath`。
- Unity 版本记忆化 `VersionCache`，清空入口 `PlatformChecker.CheckPlatform`；`ClearVersionCache` 为 `internal static`。
- 导入链路**单线程** → 缓存用普通 `Dictionary`。

## Bundle 条目读取/落盘
- `BundleFileBlockReader.ReadEntry` 整包解压共享一条流（`CreatePartial` 区间视图）。回退：单块未压缩直接映射；异常时 `ReadEntryByCopyingBlocks`。
- `RandomAccessStream` 用 `RandomAccess.Read` 绕过缓冲，勿加大缓冲（条目数远多于 bundle 数）。
- `CreateStream`：`> MaxMemoryStreamLength` → `SmartStream.CreateTemp()`。**下调阈值不省内存**（每落盘条目 ≈ 4.3KB 托管 + 1 句柄）；要省改 `bufferSize: 0`。
- 现状：`MaxMemoryStreamLength = 1024`、`MaxPreAllocatedMemoryStreamLength = 1023`（注释与代码不一致，信代码）。

## 闪暖（Nikki4）资产结构与跨 bundle 依赖解析（2026-10-07 调查）
- 数据布局：`assets/art/**` 一资源一 UnityFS bundle；cab 名=bundle 内 node 名 `CAB-<32hex>`；`FixFileIdentifier` 后小写 `cab-<hex>`（= 集合名 NameFixed）。`bin/Data`（252 个 cab）、`assetbundles`（1.9MB 索引）。
- externals 引用格式 `archive:/CAB-<hash>/CAB-<hash>` → Fix 后 `cab-<hash>`。**文件名解析三条路**：`_filesIndex`（键=文件基名去扩展名小写）与 `join(dataPath, dep)` **都匹配不上 cab-hash**；**只有 `DependencyMap.TryResolve`（依赖映射文件、d 键=NameFixed）能解析** → **无 DependencyMap 时此类依赖全断**（导入设置"依赖关系文件路径"极关键）。
- **同名多副本普遍存在**：同一资产（同 pathID）可在多个 bundle 各有一份副本（内容可不同），导出为「同名文件 + `_0`」两个资产（guid 不同但各自跨批次稳定）。
- 手写 UnityFS 解析（python+lz4）：blocksInfo 从 header 后 **16 对齐**处；数据区**紧跟其后不对齐**；blocks 字段=未压缩在前(u32,u32,u16)；sfVer21 布局：types=int32+bool+int16+[scriptID16B]+16B+typeTree(nodes×32B)+typeDeps；objects=align4+int64+u32+int32+int32；externals=串+16B guid+int32+串；Animator 数据=(GameObject12B)(Enabled1+3pad)(Avatar12B)(Controller12B)。

## ⚠️ .git 易失（操作前先备份）
- `.git/objects` loose 对象会消失，历史只在 3 个 pack（463MB/4352 commit）。曾致 refs 丢失。**6 个 `optimize/*` 分支已损坏，不要"修复"**；`alpha`/`master` 健康。
- git 操作前把改动复制到仓库外；**不要用 `git stash`**。
- 恢复：从 `.git/logs/refs/**` reflog 末行第 2 字段取 SHA 重建 refs；`git update-ref` + `git read-tree`（`reset --mixed` 会失败）。
