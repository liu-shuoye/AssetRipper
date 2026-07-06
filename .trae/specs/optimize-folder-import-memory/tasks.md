# Tasks

## Phase 1: 配置项与基础设施(低风险,先做)

- [x] Task 1: 在 `ImportSettings` 中新增 4 个内存/并发/扫描配置项
  - [x] SubTask 1.1: 在 `Source/AssetRipper.Import/Configuration/ImportSettings.cs` 新增 `MaxInMemoryBundleBlockSize`(int,默认 50×1024×1024)
  - [x] SubTask 1.2: 新增 `MaxRecursiveDirectoryDepth`(int,默认 32)
  - [x] SubTask 1.3: 新增 `MaxCollectedFiles`(int,默认 100000)
  - [x] SubTask 1.4: 新增 `FileBatchSize`(int,默认 50)
  - [x] SubTask 1.5: 新增 `MaxImportParallelism`(int,默认 `Environment.ProcessorCount`,可放 `ProcessingSettings` 中更合适)
  - [x] SubTask 1.6: 在 `Settings.LogConfigurationValues()` 输出中包含新增配置项,便于调试

- [x] Task 2: 提取统一的"按阈值选择内存/临时文件"工厂方法
  - [x] SubTask 2.1: 在 `Source/AssetRipper.IO.Files/Streams/Smart/SmartStream.cs` 新增静态方法 `CreateBySize(int size, int threshold)`,内部逻辑: `size <= threshold` → `CreateMemory()`,否则 `CreateTemp()`
  - [x] SubTask 2.2: 把 `BundleFileBlockReader.CreateStream`(第 170-178 行)与 `CreateTemporaryStream`(第 180-192 行)改为调用该方法,阈值通过参数注入(暂时硬编码旧值,Task 5 再接到配置)
  - [x] SubTask 2.3: 新增单元测试 `SmartStreamSizeThresholdTests`,覆盖 0 / 阈值-1 / 阈值 / 阈值+1 / int.MaxValue 等边界

## Phase 2: 压缩/容器文件解压改造(中风险)

- [ ] Task 3: 改造 GZip / Brotli / WebFile / RawWebFile 解压路径,接入阈值切换
  - [ ] SubTask 3.1: 修改 `Source/AssetRipper.IO.Files/CompressedFiles/GZip/GZipFile.cs:12-33`,把 `gzipStream.CopyTo(memoryStream)` 改为分块写入 `SmartStream.CreateBySize(estimatedSize, threshold)`,默认 estimate 用 GZip 头部 4 字节解压大小,缺失时按 4× 倍率估算
  - [ ] SubTask 3.2: 修改 `Source/AssetRipper.IO.Files/CompressedFiles/Brotli/BrotliFile.cs:71-77`,移除 `memoryStream.ToArray()` 二次分配,改为返回 `MemoryStream.GetBuffer()` 截断或写入 `SmartStream.CreateBySize`
  - [ ] SubTask 3.3: 修改 `Source/AssetRipper.IO.Files/WebFiles/WebFile.cs:25-49`,把 `new byte[entry.Size]` 改为 `SmartStream.CreateBySize(entry.Size, threshold)`,用 `stream.ReadExactly` 写入
  - [ ] SubTask 3.4: 修改 `Source/AssetRipper.IO.Files/BundleFiles/RawWeb/RawWebBundleFile.cs:78-88`,同 3.3 改造
  - [ ] SubTask 3.5: 在以上 4 处中,threshold 参数从 `ImportSettings.MaxInMemoryBundleBlockSize` 取值;若调用方未传,保留旧的全内存行为作为兼容默认
  - [ ] SubTask 3.6: 新增单元测试 `CompressedFileThresholdTests`:小文件走内存、大文件走临时文件,断言 `ResourceFile.Stream` 类型与文件存在性

## Phase 3: 递归目录扫描上限(低风险)

- [x] Task 4: 为目录递归扫描加深度与文件数上限
  - [x] SubTask 4.1: 修改 `Source/AssetRipper.Import/Platforms/MixedGameStructure.cs:50-66` 的 `CollectFromDirectory`,新增 `currentDepth` 参数,超过 `MaxRecursiveDirectoryDepth` 时 break 并输出 `Logger.Warning(string.Join(", ", skippedDirs))`
  - [x] SubTask 4.2: 修改 `Source/AssetRipper.Import/Platforms/PlatformGameStructure.cs:280-287` 的 `CollectAssetBundlesRecursively`,同 4.1 加深度参数
  - [x] SubTask 4.3: 在两处都加 `files.Count >= MaxCollectedFiles` 早退判断,到达上限后输出 Warning
  - [x] SubTask 4.4: 新增单元测试 `DirectoryRecursionLimitTests`,用 mock 文件系统构造深度 100、文件数 200000 的虚拟目录,断言扫描被正确截断且无异常

## Phase 4: BundleFileBlockReader 接入配置(低风险)

- [x] Task 5: 把 `BundleFileBlockReader` 的两个 `private const` 改为从配置读取
  - [x] SubTask 5.1: 修改 `Source/AssetRipper.IO.Files/BundleFiles/FileStream/BundleFileBlockReader.cs:202,209`,把 `MaxMemoryStreamLength` 与 `MaxPreAllocatedMemoryStreamLength` 改为构造函数注入或方法参数(注意该类有实例字段 `m_stream`,可加 readonly field)
  - [x] SubTask 5.2: 在 `FileStreamBundleFile.Read` 中传入 `ImportSettings.MaxInMemoryBundleBlockSize`,链路: `GameBundle.FromPaths` → `SchemeReader.LoadFile` → `FileBase.Read` → `BundleFile.Read` → `BlockReader`
  - [x] SubTask 5.3: 验证 `Source/AssetRipper.Tools.SystemTester` 等命令行工具仍可工作(它们可能不走 GUI 配置链路,需要提供默认值)

## Phase 5: ObjectInfo 延迟读取(P0,高风险,需充分测试)

- [x] Task 6: 把 `ObjectInfo.ObjectData` 改为延迟读取
  - [x] SubTask 6.1: 修改 `Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs:39-126` 的 `Read` 方法,第 73 行 `ObjectData = reader.ReadBytes(byteSize)` 改为保存 `(stream, byteOffset, byteSize)`,新增内部字段 `private SmartStream? owningStream;` `private long byteOffset;` `private int byteSize;`
  - [x] SubTask 6.2: 把 `ObjectData` 属性改为 getter,内部按需调用 `SerializedReader.ReadAssetDataAt(owningStream, byteOffset, byteSize)` 并缓存(避免多次读取)
  - [x] SubTask 6.3: 新增方法 `SerializedReader.ReadAssetDataAt(SmartStream stream, long offset, int size)`,内部定位流位置 + `ReadExactly`
  - [x] SubTask 6.4: 修改 `Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs:74-86` 的 `ReadData`,反序列化后调用 `objectInfo.ReleaseObjectData()` 释放 byte[](新方法,把缓存清掉)
  - [x] SubTask 6.5: 修改 `Source/AssetRipper.IO.Files/SerializedFiles/SerializedFile.cs`,在 `Dispose` 时释放 `owningStream` 引用(避免泄露)
  - [x] SubTask 6.6: 单元测试 `ObjectInfoLazyReadTests`:断言 (a) 读取 metadata 后 `ObjectData` 未实际占用 byte[]; (b) 访问 `ObjectData` 时能拿到正确字节; (c) `ReleaseObjectData` 后再次访问 `ObjectData` 抛 `ObjectDisposedException` 或重新读取
  - [x] SubTask 6.7: 兼容性测试:跑现有 `Source/AssetRipper.IO.Files.Tests/` 与 `Source/AssetRipper.Tests/` 全套,确认无回归

## Phase 6: GameBundle 分批加载(P0,高风险)

- [x] Task 7: 改造 `GameBundle.LoadFilesAndDependencies` 为分批加载
  - [x] SubTask 7.1: 在 `Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs:67-128` 中,把第一段 `foreach (string path in paths)` 改为按 `FileBatchSize` 分批;每批加载完成后立即调用第二段(依赖解析)与 `SerializedAssetCollection.ReadData`
  - [x] SubTask 7.2: 每批处理完后,遍历该批 `FileBase`,对其中 `SerializedFile` 调用 `Dispose()` 或新方法 `ReleaseRawData()`,只保留 `SerializedAssetCollection`(已加入 `GameBundle.Collections`)
  - [x] SubTask 7.3: 对 `ResourceFile`(非序列化资源,如纹理 raw data)改造: 若 `ResourceFile.Stream` 是 `MemoryStream` 且长度 > `MaxInMemoryBundleBlockSize`,转储到 `SmartStream.CreateTemp()` 并替换 `Stream` 引用
  - [x] SubTask 7.4: 修改 `GameBundle.Dispose`(若不存在则新增),清理所有由 `SmartStream.CreateTemp()` 创建的临时文件;在 `GameFileLoader.Reset()` 中调用 `GameBundle.Dispose()`
  - [x] SubTask 7.5: 单元测试 `GameBundleBatchLoadingTests`: 加载 N×FileBatchSize+1 个文件,断言 (a) 内存峰值 ≈ 一批的体积; (b) 最终 `GameBundle.Collections` 包含全部资产; (c) 临时文件在 `Dispose` 后被删除
  - [x] SubTask 7.6: 集成测试:加载现有测试 fixtures(如 `Source/AssetRipper.Tests/TestGameFiles` 等),对比改造前后导出结果一致

## Phase 7: 处理阶段并发上限(低风险)

- [x] Task 8: 给 `EditorFormatProcessor` 加 `MaxDegreeOfParallelism`
  - [x] SubTask 8.1: 修改 `Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs:83-84`,把 `Parallel.ForEach(GetReleaseAssets(gameData), ConvertAsync)` 改为 `Parallel.ForEach(GetReleaseAssets(gameData), new ParallelOptions { MaxDegreeOfParallelism = settings.MaxImportParallelism }, ConvertAsync)`
  - [x] SubTask 8.2: 把 `settings` 通过 `ProcessingContext` 或构造函数注入(查看现有依赖注入路径)
  - [x] SubTask 8.3: 单元测试 `EditorFormatProcessorParallelismTests`:用 mock `MaxImportParallelism = 1`,断言 `ConvertAsync` 完全串行执行

## Phase 8: 端到端验证(必做)

- [ ] Task 9: 大文件夹冒烟测试与基准对比
  - [ ] SubTask 9.1: 选一个真实大型游戏样本(若仓库无现成的,使用 `Source/AssetRipper.Tests` 中的最大 fixture),改造前后分别运行导入,记录峰值内存(用 `dotnet-counters` 或 `Process.WorkingSet64`)
  - [ ] SubTask 9.2: 在 `Source/AssetRipper.GUI.Web` 启动时新增可选日志,每 10 秒输出当前 `GameBundle.Collections.Count` 与 `GC.GetTotalMemory(false)`,便于排查
  - [ ] SubTask 9.3: 文档:在 `docs/` 或 README 中补充"大文件夹导入调参指南",说明如何根据机器内存调整 `MaxInMemoryBundleBlockSize` / `FileBatchSize` / `MaxImportParallelism`

# Task Dependencies
- Task 1 → Task 2, 4, 5, 7, 8(配置项是其它任务的前置)
- Task 2 → Task 3, 5(工厂方法被多处复用)
- Task 5 → Task 3(GZip/Brotli/WebFile 复用同一阈值常量)
- Task 6 → Task 7(ObjectInfo 延迟读取是分批加载释放内存的前提,否则 `SerializedFile.Dispose` 也没用)
- Task 3, 6, 7 → Task 9(端到端验证需要所有改造就位)
- Task 4, 8 与其它任务**可并行**

# 并行机会
- Task 4(目录递归上限)与 Task 8(并发上限)相互独立,可与 Phase 2/5/6 并行开发
- Task 1 是所有任务的前置,需先完成
- Task 6 与 Task 7 有强依赖,不能并行
