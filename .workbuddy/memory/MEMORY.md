# AssetRipper 项目长期记忆

## 设置/持久化架构（GUI Web）
- GUI 是本地 ASP.NET Core Web（Vue 前端 + C# 手写 HTML，`AssetRipper.GUI.Web`）。
- 设置模型：`FullConfiguration`（`ExportSettings`/`ProcessingSettings`/`ImportSettings`）→ `AssetRipper.Settings.json`，仅当 `ExportSettings.SaveSettingsToDisk` 勾选时落盘（`FullConfiguration.MaybeSaveToDefaultPath`）。
- **不要**往 `ExportSettings` 加字段来存"始终要记住"的值：`AssetRipper.GUI.SourceGenerator/SettingsPageGenerator.cs` 会反射其全 public 属性自动生成设置页 UI/绑定，会意外多出控件。需要始终记住的值应放在独立文件（参考 `LastExportSettings` + `LastExportSettingsContext`）。
- 前端传初始值：在 `VuePage.WriteScriptReferences` 里、Vue 脚本之前用 `writer.Write("<script>window.x = {JsonSerializer.Serialize(...)};</script>")` 注入，JS 用 `window.x ?? default` 初始化；路径必须用 `JsonSerializer.Serialize` 编码以处理 Windows 反斜杠。

## Nikki4 专属类（AssetCreation/Nikki4）经验
- **手写专属类必须复写 `ClassName`**：SourceGenerated 生成类由 AssemblyDumper Pass110 统一注入 `ClassName => 原始Unity类名`（如 "Shader"）override；手写类（如 Shader_Nikki4）若不复写，会继承其基类的 ClassName（NamedObject_2018_3 → "NamedObject"），导致 GUI/搜索/导出类型错乱。
- **组合/委托模式需额外补遍历**：Shader_Nikki4 因具体版本生成类（Shader_2019_3_0_b0）为 sealed 无法继承，采用"继承 NamedObject_2018_3 + 内部 m_Shader(Shader_2019_3_0_b0) 委托"。真实数据都在 m_Shader，须 override WalkStandard/WalkEditor/WalkRelease 委托给 m_Shader，否则 YAML/JSON 只输出基类 4 个字段（空壳 NamedObject）。Material_Nikki4 等"直接继承具体版本类"的类无此问题。
- Nikki4 资产对象数据通常没有 Object 头（hideFlags/PPtr），从 m_Name 或名字字段直接开始。

## 构建环境
- 解决方案目标 net10.0；本机已装 .NET 10.0.301 SDK，可编译。
- 注意：本会话 Bash/PowerShell 里 dotnet build 的 NuGet restore 必崩（NuGet.Common.NuGetEnvironment 用 Environment.GetFolderPath 取 KnownFolder 返回 null → path1 null；设 APPDATA/ProgramData 无效），需在正常桌面会话构建；csc.exe 在黑名单。
- 快速复现验证：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`（Ookii POSIX 风格参数），POST /Settings/Update (ImportSettings.GameType=Nikki4)、/LoadFile(Path=)，GET /Collections/View、/Assets/Json|Yaml 观察（用 --noproxy curl 或 python urllib）。
