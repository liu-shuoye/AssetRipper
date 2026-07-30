# Tasks

- [x] Task 1: 编写 GUID 采集工具脚本
  - [ ] SubTask 1.1: 创建 `Source/AssetRipper.Tools.UnityPackageGuidCollector/Program.cs`，接受两个输入目录参数（BuiltInPackages 路径、PackageCache 路径），递归扫描所有 `.asmdef` + `.asmdef.meta` + 同级 `package.json`
  - [ ] SubTask 1.2: 解析每条记录：程序集名（来自 .asmdef 的 name 字段）、UPM 包名（来自 package.json 的 name）、GUID（来自 .asmdef.meta 的 guid）、版本（来自 package.json 的 version）
  - [ ] SubTask 1.3: 处理冲突：同一程序集名出现在多个包目录时，PackageCache 优先，输出警告到 stderr
  - [ ] SubTask 1.4: 输出 C# 源码文件 `UnityPackageAssemblyMap.generated.cs`，含静态字典 `Dictionary<string, UnityPackageAssemblyInfo>`，每条记录含 PackageName、Guid、Version

- [x] Task 2: 运行采集工具生成映射表
  - [ ] SubTask 2.1: 在用户提供的两个路径上运行采集工具：
    - `D:\Program Files\UnityHubEddie\2022.3.62f2c1\Editor\Data\Resources\PackageManager\BuiltInPackages`
    - `C:\Unity\Orisries\Library\PackageCache`
  - [ ] SubTask 2.2: 将生成的 `UnityPackageAssemblyMap.generated.cs` 放入 `Source/AssetRipper.Export.UnityProjects/Scripts/`，加入项目编译

- [x] Task 3: 新增 `UnityPackageAssemblyInfo` 类型与查询接口
  - [ ] SubTask 3.1: 在 `Source/AssetRipper.Export.UnityProjects/Scripts/` 新增 `UnityPackageAssemblyInfo.cs`：`record struct UnityPackageAssemblyInfo(string PackageName, UnityGuid Guid, string Version)`
  - [ ] SubTask 3.2: 在 `UnityPackageAssemblyMap.generated.cs` 中提供 `TryGetInfo(string assemblyName, out UnityPackageAssemblyInfo info)` 静态方法

- [x] Task 4: 修改 `ScriptExporter.GetExportType` 优先判定 UPM 程序集
  - [ ] SubTask 4.1: 在方法顶部新增 `if (UnityPackageAssemblyMap.TryGetInfo(assemblyName, out _)) return AssemblyExportType.Skip;`
  - [ ] SubTask 4.2: 添加单元测试 `ScriptExporterTests.cs`：验证 `Unity.Mathematics` 返回 Skip，`MyCompany.Custom` 走原逻辑

- [x] Task 5: 修改 `ScriptExporter.CreateExportPointer` 的 Skip 分支
  - [ ] SubTask 5.1: 新增私有方法 `ResolveSkipGuid(string assemblyName)`：优先查 `UnityPackageAssemblyMap`，未命中再查 `ReferenceAssemblyDictionary`
  - [ ] SubTask 5.2: 将 Skip 分支改为调用 `ResolveSkipGuid`
  - [ ] SubTask 5.3: 添加单元测试：验证 UPM 程序集的 `CreateExportPointer` 返回的 GUID 与映射表一致

- [x] Task 6: 修改 `PackageManifestPostExporter` 写入 UPM 依赖
  - [ ] SubTask 6.1: 修改 `CreateManifest` 签名为 `protected virtual PackageManifest CreateManifest(UnityVersion version, IEnumerable<string> assemblyNames)`，`DoPostExport` 从 `gameData` 取程序集列表传入
  - [ ] SubTask 6.2: 在 `CreateManifest` 中遍历 `assemblyNames`，命中 `UnityPackageAssemblyMap` 的包 `TryAdd` 到 `Dependencies`
  - [ ] SubTask 6.3: 添加单元测试：验证给定 `[Unity.Mathematics, Unity.Burst]` 时 manifest 含对应包依赖

- [x] Task 7: 端到端验证
  - [ ] SubTask 7.1: 选取含 UPM 包程序集的游戏样本（如 Orisries 项目本身或测试工程），导出（需用户手动验证）
  - [ ] SubTask 7.2: 检查导出工程：`Assets/Plugins/` 不含 `Unity.Mathematics.dll` 等已知 UPM 程序集（需用户手动验证）
  - [ ] SubTask 7.3: 检查 `Packages/manifest.json` 含对应 UPM 包依赖（需用户手动验证）
  - [ ] SubTask 7.4: 用 Unity 打开导出工程，验证预制体上的 MonoBehaviour 引用未变成 Missing（需用户手动验证）

# Task Dependencies
- Task 2 依赖 Task 1
- Task 3 可与 Task 1 并行（独立类型定义）
- Task 4、Task 5 依赖 Task 3（需要 `UnityPackageAssemblyInfo` 类型）
- Task 6 依赖 Task 3
- Task 7 依赖 Task 1-6 全部完成
