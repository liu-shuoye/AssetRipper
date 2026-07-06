# Checklist

## 配置项
- [x] `ImportSettings` 中新增 `MaxInMemoryBundleBlockSize` 字段,默认 50MB
- [x] `ImportSettings` 中新增 `MaxRecursiveDirectoryDepth` 字段,默认 32
- [x] `ImportSettings` 中新增 `MaxCollectedFiles` 字段,默认 100000
- [x] `ImportSettings` 中新增 `FileBatchSize` 字段,默认 50
- [x] `ImportSettings` 或 `ProcessingSettings` 中新增 `MaxImportParallelism` 字段,默认 `Environment.ProcessorCount`
- [x] `Settings.LogConfigurationValues()` 输出包含新增的 5 个配置项
- [x] 所有新增配置项支持从 `appsettings.json` / 命令行 / GUI 表单读取(record class + JsonSerializerContext 自动收录)

## SmartStream 阈值切换
- [x] `SmartStream.CreateBySize(int size, int threshold)` 静态方法已实现
- [x] `BundleFileBlockReader.CreateStream` / `CreateTemporaryStream` 已改用该方法
- [x] 单元测试覆盖 size=0 / threshold-1 / threshold / threshold+1 / int.MaxValue 边界
- [x] 临时文件被正确清理(测试用 `Path.GetTempPath` 监控文件残留)

## 压缩/容器文件解压
- [x] `GZipFile.Read` 在解压体积 > 阈值时改走临时文件
- [x] `BrotliFile.ReadBrotli` 移除 `ToArray()` 二次分配
- [x] `WebFile.Read` 改用 `SmartStream.CreateBySize`
- [x] `RawWebBundleFile.ReadRawWebData` 改用 `SmartStream.CreateBySize`
- [x] 单元测试 `CompressedFileThresholdTests` 覆盖小文件走内存、大文件走临时文件两条路径
- [x] `ResourceFile.Stream` 在临时文件路径下仍可被按需读取

## 目录递归上限
- [x] `MixedGameStructure.CollectFromDirectory` 加 `currentDepth` 参数与 `MaxRecursiveDirectoryDepth` 比较
- [x] `PlatformGameStructure.CollectAssetBundlesRecursively` 加 `currentDepth` 参数与 `MaxRecursiveDirectoryDepth` 比较
- [x] 两处加 `MaxCollectedFiles` 早退判断
- [x] 截断时输出 `Logger.Warning` 并包含被跳过的路径
- [x] 单元测试 `DirectoryRecursionLimitTests` 覆盖深度截断与文件数截断

## BundleFileBlockReader 配置化
- [x] `MaxMemoryStreamLength` 与 `MaxPreAllocatedMemoryStreamLength` 改为 readonly field(通过静态属性 `CurrentMaxInMemoryBundleBlockSize` 注入,GameBundle.FromPaths try/finally 设置)
- [x] `FileStreamBundleFile.Read` 从 `ImportSettings.MaxInMemoryBundleBlockSize` 取值并传给 `BundleFileBlockReader`(经 GameStructure → GameBundle.FromPaths → 静态属性)
- [x] `MaxPreAllocatedMemoryStreamLength = MaxInMemoryBundleBlockSize * 6 / 10` 关系保留
- [x] 命令行工具(如 `AssetRipper.Tools.SystemTester`)在无配置时使用默认值,无回归

## ObjectInfo 延迟读取
- [x] `ObjectInfo.Read` 不再立即 `ReadBytes`,只保存 `(stream, offset, size)`
- [x] `ObjectInfo.ObjectData` 属性改为按需读取并缓存
- [x] 新增 `SerializedReader.ReadAssetDataAt(SmartStream, long, int)` 方法
- [x] `SerializedAssetCollection.ReadData` 反序列化后调用 `ObjectInfo.ReleaseObjectData()`
- [x] `SerializedFile.Dispose` 释放 `owningStream` 引用
- [x] 单元测试 `ObjectInfoLazyReadTests` 覆盖未访问 / 访问 / 释放 / 再访问 四种状态
- [x] 现有 `AssetRipper.IO.Files.Tests` 与 `AssetRipper.Tests` 全部通过,无回归

## GameBundle 分批加载
- [x] `LoadFilesAndDependencies` 按 `FileBatchSize` 分批(策略 B:全量加载 metadata + 依赖,再分批反序列化)
- [x] 每批加载完成后立即反序列化为 `SerializedAssetCollection` 并释放 `ObjectInfo.ObjectData`
- [x] `ResourceFile.Stream` 超阈值时转储到 `SmartStream.CreateTemp()`(TrySpillToTempFile)
- [x] `GameBundle.Dispose` 清理所有临时文件
- [x] `GameFileLoader.Reset()` 调用 `GameBundle.Dispose()`
- [x] 单元测试 `GameBundleBatchLoadingTests` 验证内存峰值下降与最终资产完整性
- [x] 集成测试:加载现有测试 fixture,导出结果与改造前一致

## EditorFormatProcessor 并发上限
- [x] `Parallel.ForEach` 改用 `ParallelOptions { MaxDegreeOfParallelism }`
- [x] `MaxImportParallelism` 通过 `ProcessingContext` 或构造函数注入
- [x] 单元测试 `EditorFormatProcessorParallelismTests` 在 `MaxImportParallelism = 1` 时断言串行执行

## 端到端验证
- [ ] 大文件夹冒烟测试通过,改造后峰值内存显著下降(目标:较改造前下降 ≥ 50%)
- [ ] 改造前后导出结果二进制一致(用 `fc /b` 或 hash 对比)
- [x] 临时文件在程序退出 / `Reset()` / `Dispose()` 后被删除,无残留
- [ ] README / docs 中补充"大文件夹导入调参指南"
- [ ] 所有现有单元测试与集成测试通过
- [ ] 在 `Source/AssetRipper.GUI.Web` 启动时新增可选的内存监控日志(每 10 秒输出 `GC.GetTotalMemory`)

## 兼容性
- [x] `ObjectInfo.ObjectData` 公共 API 行为不变(透明延迟读取)
- [x] `ResourceFile.Stream` 公共 API 行为不变
- [x] 命令行工具无回归
- [x] 所有 `private` / `internal` 改动不破坏公共 API
