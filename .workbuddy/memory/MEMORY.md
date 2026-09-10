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
- **构建优先走 Rider MCP**（`mcp__rider__execute_tool`：`build_solution_start` → `build_solution_state` 轮询）：Rider 自带完整 Windows 环境，restore/build 都正常，并且能顺带做真实 NuGet restore。
- **沙箱 Bash 里 `dotnet restore` 会崩**（NuGet `path1` null，报在 NuGet.targets:782），这只是 shell 环境问题、与仓库无关。要在 bash 里用 `dotnet build --no-restore`，先 `export APPDATA="C:\Users\Administrator\AppData\Roaming"; export ProgramData="C:\ProgramData"; export LOCALAPPDATA="C:\Users\Administrator\AppData\Local"`——**缺 APPDATA/ProgramData 会让 `ResolvePackageAssets` 读 assets 时报 NETSDK1060（value cannot be null path1）**，早期笔记说"设了无效"是因为同时还缺别的变量/没走对命令。
- **新增工程或工程引用后必须先真实 restore**（Rider 终端跑 `dotnet restore <csproj>` 即可），否则 `--no-restore` 构建报 NU1105。csc.exe 在黑名单。
- 快速复现验证：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`（Ookii POSIX 风格参数），POST /Settings/Update (ImportSettings.GameType=Nikki4)、/LoadFile(Path=)，GET /Collections/View、/Assets/Json|Yaml 观察（用 --noproxy curl 或 python urllib）。

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
