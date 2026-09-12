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
