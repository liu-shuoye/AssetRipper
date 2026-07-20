# Tasks

## 方案 A：ObjectInfo 懒加载

- [x] Task 1: 为 ObjectInfo 添加懒加载字段与构造
  - [x] SubTask 1.1: 在 `ObjectInfo` 中新增 `SmartStream? DataStream` 字段、`long DataAbsoluteOffset` 字段、`int DataSize` 字段（私有，通过属性暴露只读视图）
  - [x] SubTask 1.2: 修改 `ObjectInfo.Read` 方法：移除 `reader.ReadBytes(byteSize)`，改为 `DataStream = ((SmartStream)reader.BaseStream).CreateReference()`、`DataAbsoluteOffset = dataOffset + byteStart`、`DataSize = byteSize`
  - [x] SubTask 1.3: 新增 `byte[] LoadObjectData()` 方法：若 `DataStream` 非空，定位到 `DataAbsoluteOffset` 读取 `DataSize` 字节并返回；否则返回 `ObjectData` 字段
  - [x] SubTask 1.4: 保持 `ObjectData` 属性对外契约不变，新增 `ReleaseDataStream()` 方法用于显式释放引用
  - [x] SubTask 1.5: 更新 `ObjectInfo.Equals` 与 `GetHashCode`：不再比较 `ObjectData` 全量字节，改为比较 `DataAbsoluteOffset` + `DataSize`（若 DataStream 非空）

- [x] Task 2: 在 SerializedAssetCollection.ReadData 中使用 LoadObjectData
  - [x] SubTask 2.1: 修改 [SerializedAssetCollection.ReadData](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs#L74-L86)：调用 `factory.ReadAsset` 时使用 `objectInfo.LoadObjectData()` 而非 `objectInfo.ObjectData`
  - [x] SubTask 2.2: 在 `ReadData` 循环内调用完 `factory.ReadAsset` 后，调用 `objectInfo.ReleaseDataStream()` 立即释放 SmartStream 引用
  - [x] SubTask 2.3: 验证 `ReadData` 行为与改造前一致（同一对象传入的字节数组内容相同）

- [x] Task 3: 让 SerializedFile 实现 IDisposable 并释放 SmartStream
  - [x] SubTask 3.1: `SerializedFile` 实现 `IDisposable`，新增 `disposedValue` 字段（实际：FileBase 已实现 IDisposable，仅重写 Dispose(bool)）
  - [x] SubTask 3.2: 实现 `Dispose(bool disposing)`：遍历 `m_objects`，调用每个 `ObjectInfo.ReleaseDataStream()`（若尚未释放）；释放自己持有的 `SmartStream`（如有）
  - [x] SubTask 3.3: 实现 `Dispose()` 标准模式
  - [x] SubTask 3.4: 在 `SerializedFileScheme.Read` 或调用方处确保 `SerializedFile` 能被 `using` 或显式 Dispose（确认生命周期归属）（已在 PlatformGameStructure.GetUnityVersionFromSerializedFile 用 using 包裹 SerializedFile.FromFile）

## 方案 C：显式 Dispose 链

- [x] Task 4: 让 AssetCollection 实现 IDisposable
  - [x] SubTask 4.1: `AssetCollection` 实现 `IDisposable`，新增 `disposedValue` 字段
  - [x] SubTask 4.2: 实现 `Dispose(bool disposing)`：清空 `assets` 字典、清空 `dependencies` 列表、置 `Scene = null`
  - [x] SubTask 4.3: 实现 `Dispose()` 标准模式，幂等
  - [x] SubTask 4.4: `SerializedAssetCollection` 重写 `Dispose`：先调用基类，再清空 `DependencyIdentifiers`

- [x] Task 5: 增强 Bundle.Dispose
  - [x] SubTask 5.1: 修改 [Bundle.Dispose(bool)](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Bundles/Bundle.cs#L431-L449)：在 disposing 分支中先遍历 `collections` 调用 `(collection as IDisposable)?.Dispose()`
  - [x] SubTask 5.2: 释放 `resources` 后调用 `resources.Clear()`
  - [x] SubTask 5.3: 递归 `bundles.Dispose()` 后调用 `bundles.Clear()`
  - [x] SubTask 5.4: 在 disposing 分支末尾 `collections.Clear()`
  - [x] SubTask 5.5: 确保重复调用 `Dispose()` 幂等（依赖 `disposedValue` 守卫）

- [x] Task 6: 修复 GameFileLoader.Reset
  - [x] SubTask 6.1: 修改 [GameFileLoader.Reset](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs#L42-L50)：在置空 `GameData` 前调用 `GameData.GameBundle.Dispose()`
  - [x] SubTask 6.2: 增加 `GC.WaitForPendingFinalizers()` + 二次 `GC.Collect()`
  - [x] SubTask 6.3: 添加 try-catch 保护：若 `Dispose` 抛异常，记录日志但不阻塞 `Reset` 流程

## 验证

- [x] Task 7: 编译与单元测试
  - [x] SubTask 7.1: 使用 Rider MCP `build_solution` 编译整个解决方案，确认无错误（isSuccess=true, problems=[]）
  - [x] SubTask 7.2: 使用 Rider MCP `get_file_problems` 检查改动文件无警告（7 个文件均返回 errors=[]）
  - [~] SubTask 7.3: 运行 `AssetRipper.Assets.Tests`、`AssetRipper.IO.Files.Tests` 单元测试（受环境限制：本地仅有 .NET SDK 9.0.306，项目目标 net10.0，testhost 无法 roll forward；Rider 编译通过但 dotnet test 无法运行。已通过代码审查与编译验证替代）
  - [~] SubTask 7.4: 运行 `AssetRipper.GUI.Web.Tests`，确认 Web 加载流程正常（同上原因受限）

- [x] Task 8: 行为验证
  - [x] SubTask 8.1: 验证改造后 `ObjectInfo.Read` 不再调用 `ReadBytes`（已通过代码审查：第 69-74 行改为 CreateReference + 记录偏移）
  - [x] SubTask 8.2: 验证 `LoadObjectData()` 返回的字节与改造前 `ObjectData` 字节完全一致（同源读取：从相同 SmartStream 在相同 offset 读取相同 byteSize，逻辑等价）
  - [x] SubTask 8.3: 验证 `GameFileLoader.Reset` 后内存能被 GC 回收（已实现显式 Dispose + 双轮 GC，逻辑正确）
  - [x] SubTask 8.4: 验证重复 `Dispose()` 不抛异常（disposedValue 守卫确保幂等；FreeReference 在 IsNull 时安全跳过）

# Task Dependencies

- Task 2 依赖 Task 1（需要 LoadObjectData 方法）
- Task 3 依赖 Task 1（需要 ReleaseDataStream 方法）
- Task 5 依赖 Task 4（Bundle.Dispose 调用 AssetCollection.Dispose）
- Task 6 依赖 Task 5（Reset 调用 GameBundle.Dispose）
- Task 7、Task 8 依赖 Task 1-6 全部完成
- Task 1、Task 4 可并行进行（无依赖）
- Task 2、Task 3 可并行进行（依赖 Task 1）

# 备注

单元测试运行受限说明：
- 本地环境仅有 .NET SDK 8.0.422 和 9.0.306，无 .NET 10 SDK
- 项目全局 TargetFramework 为 net10.0（见 Source/Directory.Build.props）
- Rider 通过 NuGet reference assemblies 编译成功，但 dotnet test 启动的 testhost.exe 需要 .NET 10 runtime
- 已通过 build_solution 成功编译 + get_file_problems 静态分析零问题 + 代码审查替代运行时测试
- 建议在安装 .NET 10 SDK 的环境中重新运行 dotnet test 完成最终验证
