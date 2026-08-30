# Tasks

- [x] Task 1: `ImportSettings` 新增 `Il2CppDumpPath` 设置
  - [x] SubTask 1.1: 在 `Source/AssetRipper.Import/Configuration/ImportSettings.cs` 添加 `Il2CppDumpPath`（string?，默认 null），带中文注释说明用途（指向 Il2CppDumper 输出目录或直接指向 DummyDll 目录）
  - [x] SubTask 1.2: 更新 `Log()` 方法输出新设置
  - 说明：`ImportSettingsContext` 为 source-generated JSON 上下文，string? 属性自动序列化，无需修改

- [x] Task 2: 实现 `Il2CppDumpManager`
  - [x] SubTask 2.1: 新建 `Source/AssetRipper.Import/Structure/Assembly/Managers/Il2CppDumpManager.cs`，继承 `BaseManager`，`ScriptingBackend => ScriptingBackend.IL2Cpp`
  - [x] SubTask 2.2: 实现静态方法 `TryGetAssemblyDirectory(string path, out string? directory)`：若 `path/DummyDll` 存在则返回该子目录；否则若 `path` 自身含 `.dll` 文件则返回 `path`；否则返回 false（采用 `LocalFileSystem.Instance` 而非游戏的虚拟 FileSystem，因 dump 路径是外部用户路径）
  - [x] SubTask 2.3: 实现 `Initialize(PlatformGameStructure)`，参考 `MonoManager.Initialize` 模式：
    - 先加载 dump 目录中的 `mscorlib.dll`；若缺失，用 `Basic.Reference.Assemblies.Net100` 的 System Runtime 重建 mscorlib（照抄 `MonoManager.LoadSystemRuntimeAsMscorlib`）
    - 以 mscorlib 创建 `RuntimeContext`（`DotNetRuntimeInfo.NetCoreApp(10, 0)`）并注册，保证跨程序集类型解析
    - 依次加载目录中其余 `.dll`（用 `Load(path, fileSystem)`），单个失败（如 `BadImageFormatException`）记警告日志并跳过，不中断整体加载
    - 记录加载的程序集数量
  - [x] SubTask 2.4: 构造函数接收 `requestAssemblyCallback` 与 dump 程序集目录

- [x] Task 3: `GameStructure` 集成（依赖 Task 1、2）
  - [x] SubTask 3.1: 修改 `Source/AssetRipper.Import/Structure/GameStructure.cs` 的 `InitializeAssemblyManager`：IL2Cpp 分支改为调用新的私有方法 `CreateIl2CppManager(configuration)`
  - [x] SubTask 3.2: `CreateIl2CppManager` 逻辑：`Il2CppDumpPath` 非空时调用 `Il2CppDumpManager.TryGetAssemblyDirectory`；有效则返回 `new Il2CppDumpManager(OnRequestAssembly, directory)`（Info 日志记录实际目录）；无效则记录警告日志并回退 `IL2CppManager`；未配置则直接返回 `IL2CppManager`（现有构造参数不变）

- [x] Task 4: 本地化与 Web GUI 设置页（依赖 Task 1；4.3 依赖 4.1）
  - [x] SubTask 4.1: `Localizations/en_US.json` 添加键 `il2cpp_dump_path`（"IL2Cpp Dump Directory (Il2CppDumper DummyDll output)"）；`Localizations/zh_Hans.json` 添加同名键（"IL2Cpp dump 目录（Il2CppDumper 导出的 DummyDll，用于加密游戏等 Cpp2IL 无法解析的场景）"）；其余语言缺省回退英文，不需添加
  - [x] SubTask 4.2: `Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs`：参考 `WriteTextInputForCustomProjectPath` 新增 `WriteTextInputForIl2CppDumpPath`（文本框 + `browseForFolder` 选择文件夹按钮，标签用 `Localization.Il2cppDumpPath`），并在导入区块（`MenuImport` 下、DependencyMapPath 输入框之后）插入调用
  - [x] SubTask 4.3: 重新生成 `SettingsPage.g.cs`：构建并在其输出目录（`Source/0Bins/Other/AssetRipper.GUI.SourceGenerator/Debug/`）下运行 `AssetRipper.GUI.SourceGenerator.exe`（生成器相对路径常量要求以该目录为工作目录），已确认生成 `Il2CppDumpPath` 的 `SetProperty` case
  - 验证：生成器路径常量 `Paths.SourcePath = "../../../../"` 依赖运行工作目录，已从输出目录运行并确认 `SettingsPage.g.cs` 与 `en_US.json` 均正确更新

- [x] Task 5: 文档更新（依赖 Task 1-4）
  - [x] SubTask 5.1: 更新 `docs/articles/CommonIssues.md` 中 IL2Cpp 相关条目（第 29 行附近）：补充说明当 Cpp2IL 无法解析（如加密游戏）时，可用 Il2CppDumper（或其定制版/内存 dump 工具）导出 DummyDll，并在导入设置中配置 `Il2CppDumpPath` 加载

- [x] Task 6: 构建与验证（依赖 Task 1-5）
  - [x] SubTask 6.1: 构建 `AssetRipper.Import` 与 `AssetRipper.GUI.Web` 均零错误
  - [x] SubTask 6.2: 已按 `checklist.md` 逐项核对；并用真实 Il2CppDumper 输出（`C:\Unity\output`，106 个程序集）完成冒烟测试（临时测试项目已删除）

# Task Dependencies
- [Task 3] depends on [Task 1] [Task 2]
- [Task 4] depends on [Task 1]（4.1 先行，4.2 与 4.3 可并行，4.3 需 4.1 完成后运行生成器以纳入本地化清理）
- [Task 5] depends on [Task 1] [Task 2] [Task 3] [Task 4]
- [Task 6] depends on [Task 1] [Task 2] [Task 3] [Task 4] [Task 5]
- Task 1、Task 2 可并行
