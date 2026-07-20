# Checklist

## 方案 A：ObjectInfo 懒加载

- [x] `ObjectInfo` 新增 `DataStream`、`DataAbsoluteOffset`、`DataSize` 字段
- [x] `ObjectInfo.Read` 不再调用 `reader.ReadBytes(byteSize)`，改为记录偏移并创建 SmartStream 引用
- [x] `ObjectInfo.LoadObjectData()` 方法实现正确：从 DataStream 按偏移读取，无 DataStream 时回退到 ObjectData
- [x] `ObjectInfo.ReleaseDataStream()` 方法实现正确：调用 `DataStream.FreeReference()` 并置空
- [x] `ObjectInfo.Equals` / `GetHashCode` 不再比较全量字节，改用偏移+长度（双方都有 DataStream 时；否则回退到 LoadObjectData 字节比较，保证测试兼容）
- [x] `SerializedAssetCollection.ReadData` 使用 `LoadObjectData()` 替代 `ObjectData`
- [x] `ReadData` 在 `factory.ReadAsset` 后调用 `ReleaseDataStream()`
- [x] `SerializedFile` 实现 `IDisposable`（实际：FileBase 已实现，重写 Dispose(bool)），在 `Dispose` 中释放所有 ObjectInfo 的 DataStream
- [x] `SerializedFile.Dispose` 幂等（disposedValue 守卫；FreeReference 在 IsNull 时安全跳过）

## 方案 C：显式 Dispose 链

- [x] `AssetCollection` 实现 `IDisposable`
- [x] `AssetCollection.Dispose(bool)` 清空 `assets`、`dependencies`、`Scene`
- [x] `AssetCollection.Dispose()` 幂等
- [x] `SerializedAssetCollection` 重写 `Dispose` 清空 `DependencyIdentifiers`
- [x] `Bundle.Dispose(bool)` 先调用 collections 的 Dispose，再释放 resources/bundles
- [x] `Bundle.Dispose(bool)` 在末尾 `Clear()` 三个列表
- [x] `Bundle.Dispose` 重复调用幂等
- [x] `GameFileLoader.Reset` 调用 `GameBundle.Dispose()`
- [x] `GameFileLoader.Reset` 执行 `GC.WaitForPendingFinalizers()` + 二次 `GC.Collect()`
- [x] `GameFileLoader.Reset` 在 Dispose 抛异常时仍能完成重置（try-catch 保护）

## 兼容性

- [x] `ObjectInfo.ObjectData` 属性 getter/setter 对外契约不变
- [x] `SerializedFile.Write` 仍能正常写入（不依赖 DataStream，从 ObjectData 写入）
- [~] 现有单元测试无需修改即可通过（无法本地运行测试，但代码改动保持 API 兼容；已通过代码审查确认）
- [x] `AssetCollection.Assets` 只读字典接口不变

## 验证

- [x] 解决方案编译无错误（Rider build_solution：isSuccess=true, problems=[]）
- [x] 改动文件无警告（Rider get_file_problems：7 个文件均 errors=[]）
- [~] `AssetRipper.Assets.Tests` 全部通过（受环境限制：本地无 .NET 10 SDK，建议在 CI 中重新运行）
- [~] `AssetRipper.IO.Files.Tests` 全部通过（同上）
- [~] `AssetRipper.GUI.Web.Tests` 全部通过（同上）
- [~] 加载小型测试项目，验证导出结果与改造前一致（依赖测试运行，受环境限制）
- [x] 重复 Reset 不抛异常（disposedValue 守卫 + try-catch 双重保护，代码审查通过）

## 环境说明

本地环境仅有 .NET SDK 9.0.306，项目目标 net10.0，dotnet test 启动的 testhost.exe 需要 .NET 10 runtime 而无法 roll forward。Rider 通过 NuGet reference assemblies 成功编译并完成静态分析。建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证。
