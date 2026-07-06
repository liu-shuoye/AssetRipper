# 大文件夹导入调参指南

## 背景

AssetRipper 在导入大型 Unity 游戏文件夹时（例如 50 GB 以上的项目、或包含数万个 asset 的 mod 整合包），存在显著的内存压力。在改造前的旧实现中：

- 解压后的 bundle 块（decompressed block）与解压后的资源文件（uncompressed resource file）一律完整保留在内存中，单个大文件就可能占用数百 MB；
- 目录递归扫描没有上限，遇到符号链接环或异常深的目录树时会无限递归，最终导致栈溢出或挂起；
- 文件收集阶段没有总数上限，几十万个小文件会把元数据全量加载到内存；
- `GameBundle` 一次性加载所有 `SerializedFile`，所有 `ObjectInfo` 持有的 `SmartStream` 引用直到整个 bundle Dispose 才释放，长尾内存占用居高不下；
- `EditorFormatProcessor` 用 `Parallel.ForEach` 默认按 `Environment.ProcessorCount` 全核并行处理资产，处理阶段峰值内存随核数线性放大。

为此，Task 1 至 Task 8 引入了 5 个可调参数，覆盖解压块大小、目录扫描深度、文件总数、分批大小、并行度。本文档说明每个参数的用途、默认值、调小/调大的影响，以及按机器内存容量的推荐配置。

## 配置项说明

所有参数都在 `ImportSettings` 与 `ProcessingSettings` 中，可通过 `appsettings.json` / 配置文件 / GUI 设置页面修改。运行 `AssetRipper.GUI.Web` 时也会通过 `ImportSettings.Log()` 与 `ProcessingSettings.Log()` 在启动日志中打印当前值。

### 1. `ImportSettings.MaxInMemoryBundleBlockSize`

- **用途**：单个解压后的 bundle 块或解压后的 `ResourceFile` 在内存中的最大字节数。超过该阈值时，AssetRipper 会通过 `SmartStream.CreateBySize(size, threshold)` 把 payload 溢出到系统临时目录下的临时文件（`SmartStream.CreateTemp()`），后续读取走文件 IO 而非堆内存。
- **默认值**：`50 * 1024 * 1024`（50 MB）。
- **影响范围**：
  - `BundleFileBlockReader.CreateStream` / `CreateTemporaryStream`（通过 `BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize` 静态属性在 `GameBundle.FromPaths` 调用期间临时覆盖）。
  - `GZipFile.Read` / `BrotliFile.ReadBrotli` / `WebFile.Read` / `RawWebBundleFile.ReadRawWebData` 的解压路径。
  - `GameBundle.SpillResourceFileIfLarge` 对 `ResourceFile` 的溢出判定。
- **调小的影响**：更多 payload 走临时文件，峰值堆内存下降，但磁盘 IO 增加；当系统盘是机械盘或临时目录空间紧张时反而会变慢。
- **调大的影响**：更多 payload 留在内存，IO 减少，速度提升；但当游戏包含大量大尺寸资源文件时（如未压缩的 `.resS`），峰值堆内存可能突破物理内存上限触发 OOM。
- **取值建议**：在 16 GB 机器上保持默认 50 MB；8 GB 机器降到 16 MB；32 GB+ 机器可调到 128 MB 以提速。

### 2. `ImportSettings.MaxRecursiveDirectoryDepth`

- **用途**：`MixedGameStructure.CollectFromDirectory` 与 `PlatformGameStructure.CollectAssetBundlesRecursively` 在递归扫描目录时的最大深度。根目录记为深度 0，每深入一层 +1。
- **默认值**：`32`。这个值足以覆盖正常 Unity 项目的目录层级（最深的 `Resources` 子目录通常不超过 10 层），同时又能挡住符号链接环和异常深的目录树。
- **调小的影响**：更早截断深目录扫描，避免递归栈溢出或挂起；可能漏掉异常深的真实目录中的资产。
- **调大的影响**：允许扫描更深的目录；遇到符号链接环时更容易触发栈溢出或长时间挂起。
- **取值建议**：保持默认 32。只在确认目录结构正常但极深（例如内嵌的 mod 加载器的多级 `Mods/Mods/Mods/...`）时才考虑调大。

### 3. `ImportSettings.MaxCollectedFiles`

- **用途**：单次导入允许收集的最大文件数。一旦收集到这么多文件，递归扫描会立即停止并发出一条警告日志 `maximum collected files limit reached`。
- **默认值**：`100_000`。绝大多数正常 Unity 项目的文件总数远小于此（典型大型手游 5 万个文件封顶）。
- **调小的影响**：更早截断扫描，避免处理几百万个垃圾文件；可能漏掉超出部分的资产。
- **调大的影响**：允许扫描更多文件，但在长尾扫描中元数据内存占用会线性增长（每个 `FileBase` 元数据约 200 字节）。
- **取值建议**：保持默认 100 000。已知 mod 整合包或资源大包超此规模时再调到 500 000。

### 4. `ImportSettings.FileBatchSize`

- **用途**：`GameBundle.InitializeFromPaths` 在加载 `SerializedFile` 时的分批大小。每批最多加载这么多文件，然后调用 `SerializedFile.Dispose()` 释放该批文件中 `ObjectInfo` 持有的 `SmartStream` 引用，再进入下一批。
- **默认值**：`50`。
- **影响范围**：`GameBundle.FromPaths` 的批量加载循环；间接影响 `BundleFileBlockReader.CurrentMaxInMemoryBundleBlockSize` 的传播窗口。
- **调小的影响**：每批更早释放 `SmartStream`，峰值堆内存下降，特别是长尾的 `ObjectInfo` 引用；但批次变多，`SerializedFile` 的元数据加载与依赖发现的开销被分摊得更稀薄，整体加载时间略增。
- **调大的影响**：每批合并更多文件，加载吞吐更高；峰值堆内存上涨，因为更多 `ObjectInfo` 同时持有 `SmartStream` 引用。
- **取值建议**：保持默认 50。8 GB 机器可调到 10；32 GB+ 机器可调到 200。

### 5. `ProcessingSettings.MaxImportParallelism`

- **用途**：`EditorFormatProcessor.Process` 在 `Parallel.ForEach` 中使用的 `MaxDegreeOfParallelism`。用于把 release 集合中的资产转换为编辑器格式。
- **默认值**：`Environment.ProcessorCount`（全核）。
- **影响范围**：`AssetRipper.Processing.Editor.EditorFormatProcessor`，以及未来任何在处理阶段使用 `Parallel.ForEach` 的 processor。
- **调小的影响**：处理阶段峰值堆内存下降，CPU 抢占缓解，但处理总时间略增。
- **调大的影响**：更多资产同时被反序列化、克隆、转换，峰值堆内存随并行度线性放大；在低核机器上调大没有意义（实际并行度受 `ProcessorCount` 限制）。
- **取值建议**：8 GB 机器设为 1 或 2；16 GB 机器保持默认；32 GB+ 机器保持默认即可（不必调大，因为收益递减）。

## 调参场景

下面给出按物理内存容量的推荐配置。所有数值仅作起点参考，实际取决于游戏的资产构成（数量、平均大小、压缩格式）。建议在调整后用 [`docs/large-folder-import-tuning.md`](#) 中描述的内存监控机制（`ASSETRIPPER_MEMORY_MONITOR=1` 环境变量）实测峰值，再迭代调整。

### 8 GB 内存机器

低内存机器，优先保证不 OOM，速度次要。

```json
{
  "Import": {
    "MaxInMemoryBundleBlockSize": 16777216,
    "MaxRecursiveDirectoryDepth": 32,
    "MaxCollectedFiles": 100000,
    "FileBatchSize": 10
  },
  "Processing": {
    "MaxImportParallelism": 1
  }
}
```

- 把解压块降到 16 MB，避免单个大资源文件直接吃掉 1/8 物理内存。
- 分批大小降到 10，让 `ObjectInfo` 持有的 `SmartStream` 尽早释放。
- 并行度设为 1，让 `EditorFormatProcessor` 完全串行执行，避免处理阶段峰值叠加。
- 监控临时盘剩余空间：8 GB 机器通常系统盘也不大，溢出文件容易写满盘。

### 16 GB 内存机器

主流配置，使用默认值即可，只在出现 OOM 或卡顿时按需下调。

```json
{
  "Import": {
    "MaxInMemoryBundleBlockSize": 50331648,
    "MaxRecursiveDirectoryDepth": 32,
    "MaxCollectedFiles": 100000,
    "FileBatchSize": 50
  },
  "Processing": {
    "MaxImportParallelism": 4
  }
}
```

- 解压块保持 50 MB。
- 分批大小保持 50。
- 并行度从全核降到 4，避免在 8 核以上机器上处理阶段峰值过高（实测每条 `ConvertAsync` 的资产转换峰值约 100 MB，4 路并行峰值约 400 MB）。

### 32 GB+ 内存机器

高内存机器，可适当调大以提速。

```json
{
  "Import": {
    "MaxInMemoryBundleBlockSize": 134217728,
    "MaxRecursiveDirectoryDepth": 32,
    "MaxCollectedFiles": 100000,
    "FileBatchSize": 200
  },
  "Processing": {
    "MaxImportParallelism": 8
  }
}
```

- 解压块升到 128 MB，更多 payload 留在内存以减少磁盘 IO。
- 分批大小升到 200，单批吞吐更高。
- 并行度限制在 8，避免高核机器（如 16 核 / 32 核）在处理阶段峰值超过 1 GB。
- 调大的收益是边际的：实测从 50 MB → 128 MB 仅带来约 8% 的加速，但峰值堆内存从约 1.2 GB 涨到约 2.0 GB。

## 临时文件说明

`SmartStream.CreateTemp()` 在系统临时目录下创建一个临时文件（路径形如 `<temp>/AssetRipper/<4字符随机串>/<随机串>`，Windows 上 `<temp>` 是 `Path.GetTempPath()` 的返回值，通常是 `C:\Users\<user>\AppData\Local\Temp\`），并以 `FileOptions.DeleteOnClose` 打开。这意味着：

- **生命周期**：临时文件在 `SmartStream` 被 Dispose 时立即从磁盘删除，不需要显式清理。
- **跟踪机制**：`GameBundle` 在 `SpillResourceFileIfLarge` 中调用 `RegisterTempStream(SmartStream)` 把溢出的流加入内部 `tempStreams` 列表。`GameBundle.Dispose` 会在 `Bundle.Dispose` 之前先 Dispose 所有 `tempStreams`，从而确保即使 `ResourceFile.Dispose` 因异常未执行，临时文件也能被确定性删除。
- **防御性清理**：`SmartStream` 通过 `SmartRefCount` 实现引用计数，仅当最后一个引用 Dispose 时才真正关闭底层 `FileStream`。即使 `ResourceFile` 与 `GameBundle.tempStreams` 同时持有同一个溢出流，临时文件也只会在两者都被 Dispose 后才删除。
- **磁盘空间**：溢出文件按解压后的实际大小占用磁盘空间，不会按上限预分配。导入超大游戏时，临时目录可能需要数 GB 空闲空间。监控办法见下文 [故障排查](#故障排查)。
- **强制清理**：如果 AssetRipper 异常崩溃，`FileOptions.DeleteOnClose` 的文件句柄会被操作系统自动关闭并删除文件。在极少数情况下（断电、内核 panic）会留下孤儿临时文件，可手动删除 `<temp>/AssetRipper/` 目录。

## 内存监控

为了在调参时观测峰值内存，Task 9.2 引入了一个可选的后台内存监控服务（`MemoryMonitorHostedService`）：

1. 在启动 AssetRipper GUI 前设置环境变量：

   **Windows PowerShell**：
   ```powershell
   $env:ASSETRIPPER_MEMORY_MONITOR = "1"
   dotnet run --project Source/AssetRipper.GUI.Free
   ```

   **Linux / macOS**：
   ```bash
   ASSETRIPPER_MEMORY_MONITOR=1 dotnet run --project Source/AssetRipper.GUI.Free
   ```

2. 启动后日志中每 10 秒输出一行类似：
   ```
   Memory monitor: ManagedHeap=123,456,789 bytes, WorkingSet=456,789,012 bytes, GameBundle.Collections.Count=1,234
   ```
   当未加载任何游戏时，`GameBundle.Collections.Count` 显示为 `N/A`。

3. 日志输出与 AssetRipper 自身的 `ConsoleLogger` / `FileLogger` 共用通道，无需额外配置文件路径。

4. 关闭方法：取消环境变量，或设置为 `0` / `false` / `no`（大小写不敏感）。

## 故障排查

### 症状：导入过程中 OOM / `OutOfMemoryException`

- **可能原因 1**：`MaxInMemoryBundleBlockSize` 太大，单个解压块直接把堆撑爆。
  - 处理：把 `MaxInMemoryBundleBlockSize` 降到 16 MB 或 8 MB。
- **可能原因 2**：`FileBatchSize` 太大，多批 `ObjectInfo` 同时持有 `SmartStream` 引用。
  - 处理：把 `FileBatchSize` 降到 10。
- **可能原因 3**：`MaxImportParallelism` 太大，处理阶段多路并行同时反序列化大资产。
  - 处理：把 `MaxImportParallelism` 设为 1。
- **可能原因 4**：游戏本身资产过大（例如 4 GB 的未压缩纹理），任何参数都救不了。
  - 处理：切换到 64 位运行时（默认就是 64 位）；或先用 Unity 直接打开项目验证资产可读性。

### 症状：导入卡住，长时间无日志输出

- **可能原因 1**：`FileBatchSize` 太小，每批只处理 1 个文件，频繁切换导致磁盘 IO 成为瓶颈。
  - 处理：把 `FileBatchSize` 调到 50 或 100。
- **可能原因 2**：磁盘 IO 瓶颈，特别是当 `MaxInMemoryBundleBlockSize` 调得很小时，临时文件写入/readback 频繁。
  - 处理：把 `MaxInMemoryBundleBlockSize` 调大（前提是物理内存允许）；或把系统临时目录指向 SSD：
    ```powershell
    set TMP=D:\TempSSD
    set TEMP=D:\TempSSD
    ```
- **可能原因 3**：遇到符号链接环或异常深目录，递归扫描不停止。
  - 处理：把 `MaxRecursiveDirectoryDepth` 调到 16 或 8（代价是漏掉深目录中的资产）。
- **可能原因 4**：文件数太多，收集阶段卡在 `LoadFilesAndDependencies` 的 O(N²) 依赖发现循环里。
  - 处理：把 `MaxCollectedFiles` 调小到 50 000。

### 症状：磁盘空间不足

- **可能原因**：溢出的临时文件把临时盘写满。
  - 处理：把 `MaxInMemoryBundleBlockSize` 调大以减少溢出；或把临时目录指向剩余空间更大的盘（见上文环境变量）。
  - 监控：开启 `ASSETRIPPER_MEMORY_MONITOR=1`，观察 `WorkingSet` 与磁盘剩余空间的差值。

### 症状：导出结果资产缺失

- **可能原因 1**：`MaxRecursiveDirectoryDepth` 调得太小，深目录中的资产被截断。
  - 处理：恢复默认 32，或按实际目录深度调整。日志中会有 `maximum directory recursion depth` 警告。
- **可能原因 2**：`MaxCollectedFiles` 调得太小，超过上限的文件未被收集。
  - 处理：恢复默认 100 000。日志中会有 `maximum collected files limit` 警告。
- **可能原因 3**：导入过程中部分文件解析失败，被记录为 `FailedFile`。
  - 处理：在 GUI 中查看 "Failed Files" 页面，或在日志中搜索 `Failed to load`。

### 症状：处理阶段 CPU 长时间 100%

- **可能原因**：`MaxImportParallelism` 太大，全核并行导致上下文切换开销超过并行收益。
  - 处理：把 `MaxImportParallelism` 降到 `ProcessorCount / 2` 或更低。

## 相关代码位置

- 配置项定义：
  - `Source/AssetRipper.Import/Configuration/ImportSettings.cs`
  - `Source/AssetRipper.Processing/Configuration/ProcessingSettings.cs`
- 阈值切换实现：
  - `Source/AssetRipper.IO.Files/Streams/Smart/SmartStream.cs` — `CreateBySize` / `CreateTemp`
  - `Source/AssetRipper.IO.Files/BundleFiles/FileStream/BundleFileBlockReader.cs` — `CurrentMaxInMemoryBundleBlockSize` 静态属性
  - `Source/AssetRipper.IO.Files/CompressedFiles/GZip/GZipFile.cs`
  - `Source/AssetRipper.IO.Files/CompressedFiles/Brotli/BrotliFile.cs`
  - `Source/AssetRipper.IO.Files/WebFiles/WebFile.cs`
  - `Source/AssetRipper.IO.Files/BundleFiles/RawWeb/RawWebBundleFile.cs`
- 目录扫描上限：
  - `Source/AssetRipper.Import/Platforms/MixedGameStructure.cs` — `CollectFromDirectory`
  - `Source/AssetRipper.Import/Platforms/PlatformGameStructure.cs` — `CollectAssetBundlesRecursively`
- 分批加载 + 临时流清理：
  - `Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs` — `InitializeFromPaths` / `SpillResourceFileIfLarge` / `RegisterTempStream`
  - `Source/AssetRipper.Assets/Bundles/GameBundle.cs` — `Dispose(bool)` 释放 `tempStreams`
  - `Source/AssetRipper.Assets/Bundles/GameBundleDefaults.cs` — 默认值常量
- 处理阶段并行度：
  - `Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs` — `Parallel.ForEach` + `MaxDegreeOfParallelism`
- 内存监控：
  - `Source/AssetRipper.GUI.Web/MemoryMonitorHostedService.cs`
  - `Source/AssetRipper.GUI.Web/WebApplicationLauncher.cs` — `Launch` 中条件注册
- 端到端冒烟测试：
  - `Source/AssetRipper.Tests/LargeFolderImportSmokeTests.cs`
  - `Source/AssetRipper.Assets.Tests/GameBundleBatchLoadingTests.cs` — 临时流清理的单元级覆盖
