# 导入文件夹内存溢出优化 Spec

## Why
当用户通过"Open Folder"导入一个大型游戏文件夹(数 GB 量级的 bundle / StreamingAssets)时,AssetRipper 会以全量同步加载的方式把所有 `FileBase`、`ObjectInfo.ObjectData`(byte[])、解压后的 GZip/Brotli/WebFile/RawWeb 数据、反序列化后的资产对象**同时**加载进内存,且常驻于 `GameFileLoader.GameData` 静态字段直到用户手动 Reset。该模型没有任何"内存上限""批量加载""流式读取"旋钮,在大型游戏上直接导致 OOM 崩溃。

经只读分析定位,内存压力来自 8 处关键代码点(见下文 Impact 节),其中 P0 级别 3 处,P1 级别 3 处,P2 级别 2 处。配置层面 (`ImportSettings` / `ProcessingSettings`) 缺少任何可调节的内存/并发上限参数,唯一的硬编码阈值是 `BundleFileBlockReader.MaxMemoryStreamLength = 50MB`(private const,且只对 bundle 内部单 block 生效)。

## What Changes
- **新增 `ImportSettings.MaxInMemoryBundleBlockSize` 配置项**(默认 50MB),把 `BundleFileBlockReader` 现有的两个 `private const` 内存阈值(50MB / 30MB)提升为可配置项,允许用户在小内存机器上下调。
- **新增 `ImportSettings.MaxStreamingAssetsDepth` / `MaxRecursiveDirectoryDepth` 配置项**(默认 32,与 `Path.GetDirectoryName` 限制一致),为 `MixedGameStructure.CollectFromDirectory` 与 `PlatformGameStructure.CollectAssetBundlesRecursively` 的递归扫描加深度上限,避免目录环路或异常深路径。
- **改造 GZip / Brotli / WebFile / RawWebFile 的解压路径**:在 `GZipFile.Read`、`BrotliFile.ReadBrotli`、`WebFile.Read`、`RawWebBundleFile.ReadRawWebData` 中加入与 `BundleFileBlockReader` 相同的阈值判断逻辑,当解压后体积超过 `MaxInMemoryBundleBlockSize` 时,改走 `SmartStream.CreateTemp()`(临时文件),避免一次性把整文件读到内存。
- **修正 `BrotliFile.ReadBrotli` 中的 `memoryStream.ToArray()` 二次分配**:解压完成后直接返回底层 buffer(MemoryStream 的 `GetBuffer()` 或 `TryGetBuffer`)而不再 `ToArray()` 复制一份。
- **新增 `ImportSettings.MaxImportParallelism` 配置项**(默认 `Environment.ProcessorCount`),传给 `EditorFormatProcessor.Process` 中的 `Parallel.ForEach`,通过 `ParallelOptions.MaxDegreeOfParallelism` 限制处理阶段瞬时峰值。
- **在 `MixedGameStructure.CollectFromDirectory` 中加入文件计数上限**(`ImportSettings.MaxCollectedFiles`,默认 100000),到达上限后发出警告并停止递归,避免扫描到上百万文件的目录导致列表本身占用过多内存。
- **`ObjectInfo.ObjectData` 改为延迟读取**(P0 改造,见 ADDED Requirements):把 `ObjectInfo.Read` 中的 `reader.ReadBytes(byteSize)` 改为只保存 `(offset, size)` 与所在 `SmartStream` 的引用,真正反序列化资产时才从流中读取该 byte[](通过 `SerializedReader.ReadAssetDataAt` 新方法),并在 `SerializedAssetCollection.ReadData` 反序列化后立即释放该 byte[]。这是降低**瞬时**峰值的关键。
- **`GameBundle.LoadFilesAndDependencies` 改为分批加载**(P0 改造,见 ADDED Requirements):新增 `ImportSettings.FileBatchSize`(默认 50),每加载一批就立即把该批 `SerializedFile` 反序列化成 `SerializedAssetCollection` 并加入 `GameBundle`,随后把 `SerializedFile.Objects`(`ObjectInfo[]`)和 `ObjectInfo.ObjectData`(byte[])释放,只保留 `SerializedAssetCollection` 中的强类型对象与必要的 `DependencyIdentifiers`,再加载下一批。

## Impact
- **Affected specs**:
  - 无(项目当前不存在其他 spec)
- **Affected code**(关键文件,带链接):
  - 配置层
    - [Source/AssetRipper.Import/Configuration/ImportSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Configuration/ImportSettings.cs) — 新增 4 个配置项
    - [Source/AssetRipper.Processing/Configuration/ProcessingSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/Configuration/ProcessingSettings.cs) — `MaxImportParallelism` 可选放此处
  - 块解压阈值化(改造为可配置 + 复用到其它路径)
    - [Source/AssetRipper.IO.Files/BundleFiles/FileStream/BundleFileBlockReader.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/BundleFiles/FileStream/BundleFileBlockReader.cs#L170-L209) — 把 `MaxMemoryStreamLength` / `MaxPreAllocatedMemoryStreamLength` 改为从配置读取
    - [Source/AssetRipper.IO.Files/Streams/Smart/SmartStream.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/Streams/Smart/SmartStream.cs) — 提取统一的"按阈值选择内存/临时文件"工厂方法
  - 压缩/容器文件全量解压改造
    - [Source/AssetRipper.IO.Files/CompressedFiles/GZip/GZipFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/CompressedFiles/GZip/GZipFile.cs#L12-L33)
    - [Source/AssetRipper.IO.Files/CompressedFiles/Brotli/BrotliFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/CompressedFiles/Brotli/BrotliFile.cs#L11-L77)
    - [Source/AssetRipper.IO.Files/WebFiles/WebFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/WebFiles/WebFile.cs#L25-L49)
    - [Source/AssetRipper.IO.Files/BundleFiles/RawWeb/RawWebBundleFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/BundleFiles/RawWeb/RawWebBundleFile.cs#L78-L88)
  - 目录递归上限
    - [Source/AssetRipper.Import/Platforms/MixedGameStructure.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Platforms/MixedGameStructure.cs#L50-L66)
    - [Source/AssetRipper.Import/Platforms/PlatformGameStructure.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Platforms/PlatformGameStructure.cs#L280-L287)
  - **延迟读取 ObjectInfo**(P0 大改)
    - [Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/ObjectInfo.cs#L39-L126) — 第 73 行 `ReadBytes` 改为只存 offset+size
    - [Source/AssetRipper.IO.Files/SerializedFiles/Parser/SerializedFileMetadata.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/SerializedFileMetadata.cs#L47-L119) — 持有 stream 引用
    - [Source/AssetRipper.IO.Files/SerializedFiles/IO/SerializedReader.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/IO/SerializedReader.cs#L52-L63) — `ReadAssetDataAt(offset, size)` 新方法
    - [Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs#L74-L86) — 反序列化后释放 byte[]
  - **批量加载 FileBase**(P0 大改)
    - [Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs#L21-L128) — `LoadFilesAndDependencies` 改为分批
  - 并发上限
    - [Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L83-L84) — `ParallelOptions`
  - 单元测试
    - [Source/AssetRipper.IO.Files.Tests/](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files.Tests/) — 新增阈值切换 / 临时文件降级 / ObjectInfo 延迟读取测试
    - [Source/AssetRipper.Tests/](file:///d:/Project/AssetRipper/Source/AssetRipper.Tests/) — 端到端大文件夹冒烟测试

## ADDED Requirements

### Requirement: 可调节内存阈值配置
系统 SHALL 暴露 `ImportSettings.MaxInMemoryBundleBlockSize`(int,单位字节,默认 50×1024×1024)配置项,用于决定单个解压块/解压文件何时应从内存切换到临时文件。所有解压路径(GZip / Brotli / WebFile / RawWeb / Bundle block)SHALL 在解压后体积超过该阈值时改走 `SmartStream.CreateTemp()` 临时文件流。

#### Scenario: 小文件仍走内存
- **WHEN** 用户加载一个解压后体积 5MB 的 GZip 文件
- **AND** `MaxInMemoryBundleBlockSize` 为默认 50MB
- **THEN** 该文件被解压到 `MemoryStream` 常驻内存(行为与现状一致)

#### Scenario: 大文件降级到临时文件
- **WHEN** 用户加载一个解压后体积 200MB 的 Brotli 文件
- **THEN** 系统使用 `SmartStream.CreateTemp()` 写入临时文件
- **AND** 内存中只持有该临时文件的 `FileStream` 引用(几 KB)
- **AND** `ResourceFile.Stream` 在被访问时从临时文件按需读取

### Requirement: 递归目录扫描深度与文件数上限
系统 SHALL 在 `ImportSettings` 中提供 `MaxRecursiveDirectoryDepth`(int,默认 32)与 `MaxCollectedFiles`(int,默认 100000)配置项。`MixedGameStructure.CollectFromDirectory` 与 `PlatformGameStructure.CollectAssetBundlesRecursively` SHALL 在递归深度达到 `MaxRecursiveDirectoryDepth` 或已收集文件数达到 `MaxCollectedFiles` 时停止递归并输出 `Warning` 日志。

#### Scenario: 正常深度目录正常扫描
- **WHEN** 用户导入一个深度为 5 的游戏目录
- **THEN** 所有子目录都被扫描,文件全部加入 `Files` 列表

#### Scenario: 异常深度目录被截断
- **WHEN** 用户导入一个深度为 100 的目录(可能是符号链接环路)
- **AND** `MaxRecursiveDirectoryDepth = 32`
- **THEN** 在第 32 层停止递归
- **AND** 输出一条 `Warning` 级别日志,包含已跳过的子目录路径

### Requirement: 处理阶段并发度上限配置
系统 SHALL 在 `ImportSettings` 中提供 `MaxImportParallelism`(int,默认 `Environment.ProcessorCount`)配置项,`EditorFormatProcessor.Process` 中的 `Parallel.ForEach` SHALL 通过 `ParallelOptions { MaxDegreeOfParallelism = MaxImportParallelism }` 限制瞬时并发度。

#### Scenario: 小内存机器调低并发度
- **WHEN** 用户把 `MaxImportParallelism` 设为 1
- **THEN** `EditorFormatProcessor` 完全串行处理资产
- **AND** 处理阶段的瞬时内存峰值较默认值下降

### Requirement: ObjectInfo 二进制数据延迟读取
系统 SHALL 修改 `ObjectInfo.Read`,不再立即调用 `reader.ReadBytes(byteSize)`,改为只保存 `(byteOffset, byteSize)` 与所属 `SmartStream` 的弱引用。系统 SHALL 在 `SerializedAssetCollection.ReadData` 中通过新方法 `SerializedReader.ReadAssetDataAt(stream, offset, size)` 在反序列化时按需读取该 byte[],并在反序列化完成后立即让该 byte[] 可被 GC 回收。

#### Scenario: 大型 SerializedFile 峰值内存下降
- **WHEN** 用户加载一个含 10000 个对象、总 ObjectData 体积 1GB 的 SerializedFile
- **THEN** 加载完成时该 SerializedFile 的 `m_objects` 数组只持有 offset+size(每个对象约 16 字节),共约 160KB
- **AND** 单个资产反序列化时,临时分配的 byte[] 大小 = 该对象自己的 byteSize(而非全部对象之和)
- **AND** 反序列化完成、`IUnityObjectBase` 加入 collection 后,该 byte[] 立即可被 GC 回收

#### Scenario: 兼容现有 API
- **WHEN** 外部代码访问 `ObjectInfo.ObjectData` 属性
- **THEN** 该属性 SHALL 内部调用 `ReadAssetDataAt` 返回 byte[](透明延迟读取,API 不变)

### Requirement: GameBundle 分批加载
系统 SHALL 修改 `GameBundle.LoadFilesAndDependencies`,按 `ImportSettings.FileBatchSize`(int,默认 50)分批加载文件。每加载完一批 SHALL 立即把该批 `SerializedFile` 反序列化成 `SerializedAssetCollection` 加入 `GameBundle`,然后释放该批 `SerializedFile.Objects`(`ObjectInfo[]`)与 `ObjectInfo.ObjectData`(byte[])。`FileBase` 中的 `ResourceFile`(纯二进制资源,如纹理 raw data、音频流)SHALL 通过 `SmartStream.CreateTemp()` 转移到临时文件后从内存释放。

#### Scenario: 默认批量大小正常工作
- **WHEN** 用户加载 200 个 SerializedFile
- **AND** `FileBatchSize = 50`
- **THEN** 任意时刻内存中同时存在的 `ObjectInfo.ObjectData` byte[] 总量 ≈ 50 个文件的对象体积(而非 200 个)

#### Scenario: 临时文件被清理
- **WHEN** `GameBundle.Dispose()` 被调用
- **THEN** 所有由 `SmartStream.CreateTemp()` 创建的临时文件 SHALL 被删除
- **AND** 临时文件目录不应残留文件

## MODIFIED Requirements

### Requirement: BundleFileBlockReader 内存阈值
**修改前**:`MaxMemoryStreamLength = 50 * 1024 * 1024` 与 `MaxPreAllocatedMemoryStreamLength = 30 * 1024 * 1024` 是 `private const int`,不可配置,只对 bundle 内部单个 block/entry 生效。

**修改后**:这两个常量改为从 `ImportSettings.MaxInMemoryBundleBlockSize` 派生(`MaxMemoryStreamLength = MaxInMemoryBundleBlockSize`,`MaxPreAllocatedMemoryStreamLength = MaxInMemoryBundleBlockSize * 6 / 10`),且同一阈值复用到 GZip / Brotli / WebFile / RawWebFile 路径,统一行为。

## REMOVED Requirements
无。本变更不删除任何现有功能,所有改造对 API 调用方透明(`ObjectInfo.ObjectData` 仍可访问,只是改为延迟读取)。
