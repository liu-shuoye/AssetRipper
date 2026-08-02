# AssetRipper 项目长期记忆

## 设置/持久化架构（GUI Web）
- GUI 是本地 ASP.NET Core Web（Vue 前端 + C# 手写 HTML，`AssetRipper.GUI.Web`）。
- 设置模型：`FullConfiguration`（`ExportSettings`/`ProcessingSettings`/`ImportSettings`）→ `AssetRipper.Settings.json`，仅当 `ExportSettings.SaveSettingsToDisk` 勾选时落盘（`FullConfiguration.MaybeSaveToDefaultPath`）。
- **不要**往 `ExportSettings` 加字段来存"始终要记住"的值：`AssetRipper.GUI.SourceGenerator/SettingsPageGenerator.cs` 会反射其全 public 属性自动生成设置页 UI/绑定，会意外多出控件。需要始终记住的值应放在独立文件（参考 `LastExportSettings` + `LastExportSettingsContext`）。
- 前端传初始值：在 `VuePage.WriteScriptReferences` 里、Vue 脚本之前用 `writer.Write("<script>window.x = {JsonSerializer.Serialize(...)};</script>")` 注入，JS 用 `window.x ?? default` 初始化；路径必须用 `JsonSerializer.Serialize` 编码以处理 Windows 反斜杠。

## 构建环境
- 解决方案目标 net10.0，本机仅 .NET 8/9 SDK，且沙箱拦截网络，无法安装 .NET 10 或完整构建。验证 C# 改动可用独立 net9.0 临时工程 + 桩类型做编译/逻辑验证。
