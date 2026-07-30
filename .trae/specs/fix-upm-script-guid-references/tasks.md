# Tasks

- [ ] Task 1: 修改采集工具，新增 .cs + .cs.meta 扫描与解析
  - [ ] SubTask 1.1: 在 `Program.cs` 中新增 `ScanScriptFiles` 方法：对每个已采集的程序集，扫描其 .asmdef 所在目录树下的所有 `.cs` + `.cs.meta`
  - [ ] SubTask 1.2: 用正则解析 .cs 文件提取命名空间（`namespace\s+([\w.]+)`）和顶层类型声明（class/struct/interface/enum）
  - [ ] SubTask 1.3: 构建 `List<ScriptEntry>`，每条含 AssemblyName、Namespace、ClassName、Guid（来自 .cs.meta）
  - [ ] SubTask 1.4: 输出格式新增 `s_scriptGuidMap` 字典与 `TryGetScriptGuid(string assemblyName, string @namespace, string className, out UnityGuid guid)` 方法

- [ ] Task 2: 重新运行采集工具生成更新后的映射表
  - [ ] SubTask 2.1: 在用户提供的两个路径上运行采集工具
  - [ ] SubTask 2.2: 确认生成的 `UnityPackageAssemblyMap.generated.cs` 含 `s_scriptGuidMap` 且条目合理（如 `("UnityEngine.UI", "UnityEngine.UI", "Text")` → `5f7201a12d95ffc409449d95f23cf332`）

- [ ] Task 3: 修改 `ScriptExporter.CreateExportPointer` 使用真实 .cs.meta GUID
  - [ ] SubTask 3.1: 新增私有方法 `CreateUpmScriptPointer(IMonoScript script)`：优先查 `TryGetScriptGuid`，命中则返回 `(MonoScriptDecompiledFileID, guid, AssetType.Meta)`；未命中回退到 `(CalculateScriptFileID, ResolveSkipGuid, AssetType.Meta)` 并输出警告
  - [ ] SubTask 3.2: 将 Skip 分支改为调用 `CreateUpmScriptPointer`
  - [ ] SubTask 3.3: 确认 `MonoScriptDecompiledFileID` 常量值确实是 `11500000`

- [x] Task 4: 单元测试
  - [ ] SubTask 4.1: 测试 `TryGetScriptGuid("UnityEngine.UI", "UnityEngine.UI", "Text")` 返回 true 且 guid = `5f7201a12d95ffc409449d95f23cf332`
  - [ ] SubTask 4.2: 测试 `TryGetScriptGuid("UnityEngine.UI", "UnityEngine.UI", "Image")` 返回 true 且 guid = `fe87c0e1cc204ed48ad3b37840f39efc`
  - [ ] SubTask 4.3: 测试 `TryGetScriptGuid("Unknown.Asm", "Unknown.Ns", "UnknownClass")` 返回 false

- [x] Task 5: 端到端验证
  - [ ] SubTask 5.1: 重新导出游戏，检查 MainScene.prefab 中 Text/Image/RawImage 的 m_Script 引用 guid 与 .cs.meta 一致（需用户手动验证）
  - [ ] SubTask 5.2: 用 Unity 打开导出工程，确认预制体上的 MonoBehaviour 引用未变成 Missing（需用户手动验证）

# Task Dependencies
- Task 2 依赖 Task 1
- Task 3 依赖 Task 2（需要新生成的 s_scriptGuidMap）
- Task 4 依赖 Task 2、Task 3
- Task 5 依赖 Task 1-4 全部完成
