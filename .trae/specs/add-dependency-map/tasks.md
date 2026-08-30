# Tasks

- [x] Task 1: 实现 DependencyMap 模型（`Source/AssetRipper.Import/Structure/DependencyMap.cs`）
  - [x] SubTask 1.1: 定义 `DependencyMap` 类：`Version`、`Entries`（`Dictionary<string, string>` 名称→绝对路径）、`TryResolve(string name, out string path)`（键统一小写）
  - [x] SubTask 1.2: 实现 `Load(string path)`（文件不存在/非法返回 null 并记录警告）与 `Save(string path)`
  - [x] SubTask 1.3: 新增 `JsonSourceGenerationOptions` 序列化上下文（参照 `LastExportSettingsContext`，WriteIndented = true，internal）
- [x] Task 2: 实现 DependencyMapScanner（`Source/AssetRipper.Import/Structure/DependencyMapScanner.cs`）
  - [x] SubTask 2.1: 递归枚举文件夹所有文件（用 `FileSystem.Directory` 抽象）
  - [x] SubTask 2.2: 逐文件 `SchemeReader.LoadFile` + `ReadContentsRecursively` + `FetchSerializedFiles`，记录四类名称键（文件名、去扩展名 bundle 名、SerializedFile.NameFixed、相对根路径），处理后 `Dispose`
  - [x] SubTask 2.3: 实现输出逻辑（默认 `<扫描文件夹>/AssetRipper.DependencyMap.json`）与统计日志（成功/失败/条目数）
- [x] Task 3: 添加设置项并重新生成设置绑定代码
  - [x] SubTask 3.1: `ImportSettings.cs` 添加 `LoadDependencyMap`（bool，默认 false）与 `DependencyMapPath`（string?），更新 `Log()`
  - [x] SubTask 3.2: 运行 `AssetRipper.GUI.SourceGenerator` 重新生成 `SettingsPage.g.cs`（含新的 SetProperty/booleanProperties/WriteCheckBoxForLoadDependencyMap）
  - [x] SubTask 3.3: `SettingsPage.cs` 在导入分区添加复选框 `WriteCheckBoxForLoadDependencyMap` 与路径输入框（模仿 `WriteTextInputForCustomProjectPath`，带选择文件按钮）
- [x] Task 4: 集成加载链路（文件夹外依赖加载）
  - [x] SubTask 4.1: `GameInitializer.StructureDependencyProvider.cs`：`FindDependency` 在结构查找失败后用 `DependencyMap.TryResolve(identifier.PathName)` 后备查找，命中则 `SchemeReader.LoadFile` 加载
  - [x] SubTask 4.2: `GameInitializer.cs` 构造函数增加 `DependencyMap?` 参数并传给 `StructureDependencyProvider`
  - [x] SubTask 4.3: `GameStructure.cs` 构造函数按 `configuration.ImportSettings.LoadDependencyMap`/`DependencyMapPath` 加载 DependencyMap 并传入 `GameInitializer`
- [x] Task 5: GUI 扫描命令与页面入口
  - [x] SubTask 5.1: `Commands.cs` 新增 `GenerateDependencyMap : ICommand`（读取 Path/OutputPath 表单字段，调用扫描器）
  - [x] SubTask 5.2: `WebApplicationLauncher.cs` 映射 `POST /Commands/GenerateDependencyMap`
  - [x] SubTask 5.3: `CommandsPage.cs` 未加载分支添加扫描表单（文件夹路径输入 + 提交按钮）
- [x] Task 6: 本地化字符串
  - [x] SubTask 6.1: `Localizations/en_US.json` 添加：`load_dependency_map`、`dependency_map_path`、`generate_dependency_map`、`scan_dependency_map_description` 等键
  - [x] SubTask 6.2: `Localizations/zh_Hans.json` 添加对应中文翻译
- [x] Task 7: 构建与验证
  - [x] SubTask 7.1: `dotnet build` 整个解决方案编译通过（含 AOT 序列化约束）
  - [x] SubTask 7.2: 代码走查验证：开关关闭时行为与现状一致；开启后 FindDependency 后备链路生效

# Task Dependencies

- Task 2 依赖 Task 1（扫描器使用 DependencyMap 保存）
- Task 4 依赖 Task 1（StructureDependencyProvider 使用 DependencyMap 查询）
- Task 3 与 Task 1/2/4/5 可并行（设置项独立）
- Task 5 依赖 Task 2（命令调用扫描器）
- Task 6 依赖 Task 3/5 的 UI 键确定后填写（可与 3/5 同步进行）
- Task 7 依赖全部任务完成
