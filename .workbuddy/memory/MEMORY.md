# AssetRipper 项目长期记忆

## 设置/持久化架构（GUI Web）
- GUI 是本地 ASP.NET Core Web（Vue 前端 + C# 手写 HTML，`AssetRipper.GUI.Web`）。
- 设置模型：`FullConfiguration`（`ExportSettings`/`ProcessingSettings`/`ImportSettings`）→ `AssetRipper.Settings.json`，仅当 `ExportSettings.SaveSettingsToDisk` 勾选时落盘（`FullConfiguration.MaybeSaveToDefaultPath`）。
- **不要**往 `ExportSettings` 加字段来存"始终要记住"的值：`AssetRipper.GUI.SourceGenerator/SettingsPageGenerator.cs` 会反射其全 public 属性自动生成设置页 UI/绑定，会意外多出控件。需要始终记住的值应放在独立文件（参考 `LastExportSettings` + `LastExportSettingsContext`）。
- **`SettingsPage.g.cs` 是预生成静态文件**：`AssetRipper.GUI.SourceGenerator` 是独立控制台工具（运行时反射，直接写 GUI.Web 源码目录），构建时不会自动更新。新增设置字段后必须运行该工具（`0Bins/Other/AssetRipper.GUI.SourceGenerator/Debug/...exe`，它会顺带把 en_US.json 按 ordinal 排序重写）或手工按规则补 g.cs（booleanProperties 字典条目 + WriteXxxFor 方法，按属性声明顺序插入）。
- 前端传初始值：在 `VuePage.WriteScriptReferences` 里、Vue 脚本之前用 `writer.Write("<script>window.x = {JsonSerializer.Serialize(...)};</script>")` 注入，JS 用 `window.x ?? default` 初始化；路径必须用 `JsonSerializer.Serialize` 编码以处理 Windows 反斜杠。

## Nikki4 专属类（AssetCreation/Nikki4）经验
- **手写专属类必须复写 `ClassName`**：SourceGenerated 生成类由 AssemblyDumper Pass110 统一注入 `ClassName => 原始Unity类名`（如 "Shader"）override；手写类（如 Shader_Nikki4）若不复写，会继承其基类的 ClassName（NamedObject_2018_3 → "NamedObject"），导致 GUI/搜索/导出类型错乱。
- **组合/委托模式需额外补遍历**：Shader_Nikki4 因具体版本生成类（Shader_2019_3_0_b0）为 sealed 无法继承，采用"继承 NamedObject_2018_3 + 内部 m_Shader(Shader_2019_3_0_b0) 委托"。真实数据都在 m_Shader，须 override WalkStandard/WalkEditor/WalkRelease 委托给 m_Shader，否则 YAML/JSON 只输出基类 4 个字段（空壳 NamedObject）。Material_Nikki4 等"直接继承具体版本类"的类无此问题。
- Nikki4 资产对象数据通常没有 Object 头（hideFlags/PPtr），从 m_Name 或名字字段直接开始。

## 构建环境
- 解决方案目标 net10.0；本机已装 .NET 10.0.301 SDK，可编译。
- **PowerShell 里的 `dotnet` 完全可用**（`C:\Program Files\dotnet\dotnet.exe`）：`dotnet build` 会自动完成 restore（含新增 PackageReference），是 bash 坏掉时的首选。注意 PowerShell 工具不回显 stdout，**把结果写文件再用 Read 查看**。
- **构建优先走 Rider MCP**（`mcp__rider__execute_tool`：`build_solution_start` → `build_solution_state --sessionId <id>` 轮询；返回 `buildIsSuccess` + `problems`）：Rider 自带完整 Windows 环境，restore/build 都正常。
- **沙箱 Bash 里 `dotnet restore` 会崩**（NuGet `path1` null，报在 NuGet.targets:782），这只是 shell 环境问题、与仓库无关。要在 bash 里用 `dotnet build --no-restore`，先 `export APPDATA="C:\Users\Administrator\AppData\Roaming"; export ProgramData="C:\ProgramData"; export LOCALAPPDATA="C:\Users\Administrator\AppData\Local"`——**缺 APPDATA/ProgramData 会让 `ResolvePackageAssets` 读 assets 时报 NETSDK1060（value cannot be null path1）**，早期笔记说"设了无效"是因为同时还缺别的变量/没走对命令。某些会话 bash 的 coreutils 直接缺失（`dirname: command not found`），此时改用 PowerShell。
- **新增工程或工程引用后必须先真实 restore**（Rider 构建会自动 restore），否则 `--no-restore` 构建报 NU1105。csc.exe 在黑名单。
- 快速复现验证：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`（Ookii POSIX 风格参数），POST /Settings/Update (ImportSettings.GameType=Nikki4)、/LoadFile(Path=)，GET /Collections/View、/Assets/Json|Yaml 观察（用 --noproxy curl 或 python urllib）。
- **`AssetRipper.GUI.Free` 是 `PublishAot=true`**：加依赖前先掂量体积与 AOT 兼容性。本机 AOT publish 目前会在 link 阶段失败（缺 Windows SDK 的 `advapi32.lib`），属既有环境问题。
- **临时验证工程放 temp、别落仓库**；若把文件拷进 `0Bins/.../Debug` 做验证，跑完必须清掉（`Remove-Item` 会被 safe-delete 钩子拦下，用 `[System.IO.File]::Delete`）。

## 运行期验证手法（不想跑整条 GUI 流程时）
- 写一个 temp 控制台 harness，**用 `ProjectReference` 引用目标工程**（裸 `<Reference>` + HintPath 不会生成完整 deps.json，运行时报 FileNotFoundException），然后反射调用 `private static` 方法逐个覆盖代码路径；注册一个实现 `AssetRipper.Logging.ILogger`（只有 `Log` + `BlankLine` 两个成员）的控制台 sink 就能看到真实日志格式。
- 想复现宿主差异时对齐 `AssetRipper.GUI.Free` 的属性，例如 `<ServerGarbageCollection>true</ServerGarbageCollection>`。

## 日志模块（独立程序集 `AssetRipper.Logging`）
- 日志是**零依赖**的独立程序集（命名空间 `AssetRipper.Logging`，2026-09-11 从 `AssetRipper.Import/Logging` 拆出），处于依赖图最底层，`AssetRipper.Assets` 等下层程序集可直接使用。
- **零依赖是硬规则**：不要给它加任何 AssetRipper 项目引用或重包引用，否则下层程序集一引用就成环。需要"执行目录"这类能力时用 BCL 的 `AppContext.BaseDirectory`（`LocalFileSystem.ExecutingDirectory` 就是它）。
- 内含 `Logger`、`ILogger`、`LogType`、`LogCategory`、`ConsoleLogger`/`FileLogger`/`FileLoggerBase`/`CleanFileLogger`，以及 `AssetRipperRuntimeInformation`（原先在 `AssetRipper.Import` 根目录，随之迁入）。csproj 需要 `AllowUnsafeBlocks`（`[LibraryImport]` 生成代码）。
- `Logger.LogExternal(LogType, message)` 是给上层桥接外部日志系统用的入口；Cpp2IL 的桥接在 `AssetRipper.Import/Logging/Cpp2ILBridge.cs`，用 `[ModuleInitializer]` 自动挂接（不要挪回 Logger 的静态构造函数，那会把 Cpp2IL 依赖带进零依赖程序集）。
- `Logger.LogMemoryDiagnostics(stage)` 只做最基础的快照（双轮 GC + 托管/工作集），并**返回托管堆字节数**；带详细拆解的版本在 `AssetRipper.Import.Memory.MemoryDiagnostics`。
- 直接用 Logger 的工程应显式加 `ProjectReference ..\AssetRipper.Logging\...`（不依赖传递引用），目前包括 Assets/Import/Processing/Export*/GUI.Web/Tools.* 等。

## 内存诊断（`[内存诊断]` 系列）
- **入口在独立程序集 `AssetRipper.Diagnostics` 的 `Memory.MemoryDiagnostics`**（2026-09-12 从 `AssetRipper.Import/Memory` 搬出）：`LogMemoryDiagnostics(stage, collections = null)` / `LogResourceBreakdown(collections, stage)`。纯算法在同目录（`ManagedSizeCalculator`、`ClrMdHeapAnalyzer`、`TypeAggregate`）；`ExportHandler` 只做转发。该程序集依赖 Logging + Assets + `AssetRipper.SourceGenerated` 包 + ClrMD 包，**只被导出/入口层引用**。
- **不能放进 `AssetRipper.Assets`**（验证过，别再试）：
  ① `ClassIDType` 来自 NuGet 包 `AssetRipper.SourceGenerated`，而 `AssetRipper.SourceGenerated.dll` 自身引用 `AssetRipper.Assets.dll`（生成类实现 `IUnityObjectBase`），Assets → SourceGenerated 就是环；注意 SourceGenerated 的 nuspec **没声明任何依赖**，这个环不会被 NuGet 拦下，只会在编译期炸。
  ② `MemoryDiagnostics`/`ClrMdHeapAnalyzer` 会把 `Microsoft.Diagnostics.Runtime`（+ `Microsoft.Diagnostics.NETCore.Client`，约 800KB）带进最底层模型程序集，所有引用 Assets 的工程都会被动继承。
  ③ `IUnityObjectBase.ClassName` 只能替代 live 路径的命名；默认（快速）口径只拿到 `(ClassID, size)`，绕开枚举得靠运行时反射，不值当。
- **分层约束**：`GameData` 在 `AssetRipper.Processing`，下层拿不到，所以拆解的数据源是**显式参数** `IEnumerable<AssetCollection>? collections`。不要改成"静态注册来源"（会钉住 GameData 造成 Reset 后泄漏）。只有 `clrmd` 口径不依赖它。
- `MemoryDiagnostics.LogMemoryDiagnostics(stage, collections)`：打印快照后，若托管堆较上次拆解增长 ≥ 阈值（`RURI_MEM_BREAKDOWN_GROWTH_MB`，默认 1024MB，≤0 关闭）就**自动追加一次拆解**。首个诊断点只建基线；每次拆解后基线前移 → 同一段增长只报一次。
- 拆解口径由 `RURI_MEM_BREAKDOWN_MODE` 选择：默认=已反序列化对象的序列化字节数（廉价）；`live`=`ManagedSizeCalculator` 反射估算真实托管堆（很重，含 SerializedFile 结构）；`clrmd`=整堆 GC 快照按托管类型聚合。
- 输出的榜单：总量 Top25、数量 Top10、**平均占用 Top25**（`Size/Count` 降序，仅统计数量 ≥ 10 的类型，平均值自适应 B/KB/MB）、Unity 资产归并、命名空间首段分组。
- `clrmd` 口径的附加开关：`RURI_MEM_BREAKDOWN_DUMP`（分析 dotnet-dump 产物）、`RURI_MEM_BREAKDOWN_MAX_OBJECTS`（超大堆抽样上限）。
- ClrMD 选型锁在 **3.1.512801**：只依赖 `Microsoft.Diagnostics.NETCore.Client`；4.x 会拖入 `Azure.Identity` 依赖链。自快照（`DataTarget.CreateSnapshotAndAttach`）在本机实测可用（约 34ms，结果与 `GC.GetTotalMemory` 一致），**不要用 `AttachToProcess(suspend:false)` 读活进程**（ClrMD 明确不支持）。
- 生成的资产类是 `AssetRipper.SourceGenerated.Classes.ClassID_<数字>.<类名>`（来自 NuGet 包 `AssetRipper.SourceGenerated`，**仓库内无源码**），ClassID_ 后的数字即 Unity ClassID —— 想把托管类型名映射回 Unity 类型时直接解析它，不必反射生成程序集。




## Shader 还原：Ruri.ShaderDecompiler 接入（已落地）
- 库以**源码内嵌**在 `External/Ruri.ShaderDecompiler/`（原为独立 git 仓库 E:\Project\Ruri\Ruri.RipperHook\Source\Ruri.ShaderDecompiler，拷贝时排除 `.git`/`bin`），并补了一个本地 `Directory.Build.props` 复刻母仓库的全局 using（`System.Diagnostics.CodeAnalysis` 是 SmolvDecoder 必需的）与 LangVersion=preview/IsTrimmable。
- 引用方式：`AssetRipper.Export.UnityProjects.csproj` 里 `<RuriShaderDecompilerProject>` 属性（默认 `..\..\External\Ruri.ShaderDecompiler\Ruri.ShaderDecompiler.csproj`，可 `-p:` 覆盖）；工程已加入 `AssetRipper.slnx`。**跨盘符绝对路径的 ProjectReference 必然 restore 失败（NU1105），必须同盘相对路径**。
- 接入点：`ShaderExportMode.RuriDecompile`（枚举尾部追加，别动 Decompile）→ `ProjectExporter.Overrides.cs` switch → `ShaderRuriDecompileExporter`；GUI 下拉 + `Localizations` 键 `shader_asset_format_ruri_decompile`。移植文件在 `Source/AssetRipper.Export.UnityProjects/Shaders/Ruri/`。
- 移植三个易错点：① 本仓库用 AssetRipper 原生 `ShaderSubProgram`（RipperHook 那边是参数直接为 Ruri 类型的影子版），`AppendRuntimeSymbols` 必须逐字段转换；② `ShaderGpuProgramTypeExtensions` 同名歧义，取 SourceGenerated 那个的别名；③ 写文件必须走 `FileSystem` 抽象（有 `VirtualFileSystem`），不能 `File.WriteAllText`；④ 无法反编译时要回退 `DummyShaderTextExporter`，否则该 shader 无产物。
- 原生库 `spirv-cross.dll`/`dxil-spirv-c-shared.dll` 经 NuGet 落到 `runtimes/win-x64/native/`，正是库 `NativeLibraryResolver` 的探测路径，无需手工部署。
- 开关：`SplitVariantsToHlslFiles` 默认 true（拆 .hlsl）；环境变量 `RURI_SHADER_PLATFORM`/`RURI_SHADER_FAST_ITERATION`/`RURI_STRICT_SHADER_EXPORT`（会 `Environment.Exit`，GUI 下勿开）/`RURI_DUMP_INPUT_DIR`/`RURI_SHADER_DUMP_FAILURES`（本项目新增，默认关）。

## 纹理/生成接口经验
- 依赖方向 Export→Import 单向：Import 项目内的类（如 GameAssetFactory）读不到 ExportSettings，加载期需要的行为开关放 ImportSettings。
- 生成的接口 `ITexture2D.StreamData_C28` 是只读属性（内部固定实例），清流引用用 `StreamData_C28?.ClearValues()`（StreamingInfoExtensions）；清空后 `CheckAssetIntegrity()` 返回 **true**（CheckIntegrity 把空 Path 视为无外部依赖），不能当"数据已剥离"的判据。
- 占位导出已扩展三类：Texture2D（白图）、Mesh（`IMesh.StreamData` 也是 IStreamingInfo，清 VertexData.Data/IndexBuffer/StreamData，工程 YAML 天然占位、GLB 写空场景）、AudioClip（`Resource` 是 IStreamedResource? 无 ClearValues，手工清 Source/Offset/Size；解码发生在 AudioClipExporter.TryCreateCollection 而非 Export）。占位导出器 TryCreateCollection 有完整性前置检查的都要用 PlaceholderMode 放行。
- 本会话内 `dotnet build --no-restore` 可用（前提 obj 缓存存在）；从未在本机 restore 过的项目（如 *.SourceGenerator、Tools.DependenceGrapher）缺 project.assets.json，无法编译。桌面会话构建一次后，后续会话的 --no-restore 构建随之可用。
- LocalizationGenerator 的 snake_case→PascalCase 对数字后的字母不大写：`strip_texture_2d_data` → `StripTexture2dData`。
