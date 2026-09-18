# LoadFilesAndDependencies 并行化优化

## Context（背景）

`LoadFilesAndDependencies`（[GameBundle.FromPaths.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs#L79-L144)）是加载大型项目（可达数十万文件）时的性能瓶颈：

- **阶段一（主路径循环，L83-122）**：对每个 path 串行执行 `SchemeReader.LoadFile`（打开流 + 依次探测 8 种 scheme 读文件头）→ `ReadContentsRecursively`（展开 FileContainer 内部文件，每个内部文件再走 scheme 探测）。纯串行磁盘 IO + 解压/解析，CPU 与 IO 等待无法重叠。
- **阶段二（依赖循环，L125-141）**：串行 for + 动态增长，对每个 SerializedFile/Container 的依赖做去重加载。**本阶段保持串行，不做并行**（依赖数量远少于主文件，且涉及动态发现 + 去重的顺序语义）。
- `FileContainer.ReadContents`（[FileContainer.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/FileContainer.cs#L111-L123)）对大 bundle（如 .unity3d 内含数千文件）逐个串行解析内部 ResourceFile，是另一热点。

**目标**：将阶段一主循环与 FileContainer 内部展开并行化，在不改变 `files` 添加顺序（后续 `InitializeFromPaths` 按 `RemoveLastItem` 顺序处理）与失败语义（单文件失败 → FailedFile）的前提下显著缩短加载时间。

## 线程安全依据（已核实）

- 每个 path 的加载完全独立，无共享可变状态。
- `RandomAccessStream`（[RandomAccessStream.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/Streams/RandomAccessStream.cs)）用 `RandomAccess.Read(SafeFileHandle, ...)` 按偏移读取，同一文件的多个 partial 流可并行。
- `SchemeReader.ReadFile(ResourceFile)` 对每个实例的独立 `SmartStream` 调 `CreateReference()`，RefCounter 互不共享，可并行。
- `AddFile` 修改 FileContainer 内部 List，**非线程安全** → 并行解析后必须顺序合并。

## 改动 1：阶段一主循环并行化（GameBundle.FromPaths.cs）

`LoadFilesAndDependencies` 改造：`paths` 转数组 → `Parallel.For` 并行加载，结果按索引写回结果数组（天然保序）→ 顺序收集，保持原有类型分发与 `serializedFileNames` 去重逻辑不变。

```csharp
string[] pathArray = paths.ToArray();
FileBase[] results = new FileBase[pathArray.Length];
int completed = 0;
Parallel.For(0, pathArray.Length, i =>
{
    string path = pathArray[i];
    FileBase? file;
    try
    {
        file = SchemeReader.LoadFile(path, fileSystem);
        file.ReadContentsRecursively();
    }
    catch (Exception ex)
    {
        // 单个文件解析失败不中断整体加载，与现状一致
        file = new FailedFile() { Name = fileSystem.Path.GetFileName(path), FilePath = path, StackTrace = ex.ToString() };
    }
    // 解包压缩层：纯属性导航，无共享状态，放并行体内结果等价
    while (file is CompressedFile compressedFile)
    {
        file = compressedFile.UncompressedFile;
    }
    results[i] = file; // 不同索引写入互不冲突，线程安全
    int n = Interlocked.Increment(ref completed);
    if (n % 100000 == 0)
    {
        Logger.Info(LogCategory.Import, $"{n} 正在加载文件：'{path}'");
    }
});

// 顺序收集：保持原 files 添加次序与 serializedFileNames 去重语义
List<FileBase> files = new(pathArray.Length); // 阶段一每个 path 恰好产出 1 个 FileBase，预分配精确
HashSet<string> serializedFileNames = new();
foreach (FileBase file in results)
{
    // 原 L105-121 的类型分发原样搬入（ResourceFile/FailedFile/SerializedFile/FileContainer）
}
// 阶段二（L125-141）保持串行，不动
```

要点：
- 进度日志 `files.Count % 100000` 改为 `Interlocked.Increment` 的完成计数，每个边界恰好命中一次。
- 并行度用 `Parallel.For` 默认（CPU 核心数），不引入新配置。
- 建议把「try/catch + 解包循环」抽成私有静态方法 `LoadFileSafely(string path, FileSystem fileSystem)`，并行体只调用它，保持可读性。

## 改动 2：FileContainer.ReadContents 并行化（FileContainer.cs）

`ReadContents` 并行解析内部 ResourceFiles，小数组（< 8 个）走串行避免并行调度开销：

```csharp
public override void ReadContents()
{
    if (m_resourceFiles is not { Count: > 0 })
    {
        return;
    }
    ResourceFile[] resourceFiles = m_resourceFiles.ToArray();
    m_resourceFiles.Clear();
    FileBase[] parsed = new FileBase[resourceFiles.Length];
    if (resourceFiles.Length >= 8)
    {
        Parallel.For(0, resourceFiles.Length, i =>
        {
            parsed[i] = SchemeReader.ReadFile(resourceFiles[i]);
        });
    }
    else
    {
        for (int i = 0; i < resourceFiles.Length; i++)
        {
            parsed[i] = SchemeReader.ReadFile(resourceFiles[i]);
        }
    }
    // AddFile 修改内部 List（非线程安全），解析完成后顺序合并
    foreach (FileBase file in parsed)
    {
        AddFile(file);
    }
}
```

要点：
- 嵌套 bundle（解析结果又是 FileContainer）在顺序合并阶段经 `AddFile` 进入 `m_fileLists`，`ReadContentsRecursively` 的递归展开（L125-132）在调用线程上执行，不受影响。
- 异常由 `Parallel.For` 聚合抛出，与现状语义一致（整个容器视为失败）。

## 改动文件清单

仅 2 个文件，无新文件、无新配置：
- `Source/AssetRipper.Assets/Bundles/GameBundle.FromPaths.cs`：`LoadFilesAndDependencies` 改造 + 新增私有静态辅助 `LoadFileSafely`。
- `Source/AssetRipper.IO.Files/FileContainer.cs`：`ReadContents` 并行化 + 阈值常量（`const int ParallelThreshold = 8`）。

## 风险与权衡

- **内存峰值**：并行数 × 单文件解压缓冲（Brotli/GZip 可达数 MB），峰值约为 P 核倍；阈值门控缓解小 bundle 场景。
- **线程超订**：外层 Parallel.For 工作项内可能触发内层 FileContainer 并行；默认 `MaxDegreeOfParallelism = -1` 由 ThreadPool 工作窃取控制，不会无限超订，可接受。
- **顺序语义**：结果数组 + 顺序收集完整保留 `files` 次序，`InitializeFromPaths` 处理顺序不变。

## 验证方式

1. **构建**：走 Rider MCP（`build_solution_start` → `build_solution_state --sessionId <id>` 轮询，确认 `buildIsSuccess`）。
2. **功能回归**：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`，POST `/Settings/Update`（ImportSettings.GameType=Nikki4）、`/LoadFile`（Path=大型项目），GET `/Collections/View` 核对资源总数与改造前一致。
3. **顺序一致性**：`/Assets/Json|Yaml` 抽样比对，确认资源集合与改造前一致。
4. **边界用例**：单文件、空路径、gzip/brotli 压缩 bundle、嵌套 bundle（bundle 内套 bundle）。
5. **耗时与内存**：观察日志 `加载文件和依赖项前/后` 的时间差与 `LogMemoryDiagnostics` 的内存差值，确认加载时间下降、内存峰值未超预期。
