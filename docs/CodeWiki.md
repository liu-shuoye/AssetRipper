# AssetRipper Code Wiki

> 本文档基于对仓库源码的静态分析生成，用于帮助开发者快速理解 AssetRipper 的整体架构、模块职责、关键类与函数、依赖关系以及构建运行方式。
>
> 适用版本：`1.3.14`（`Source/Directory.Build.props` 中的 `VersionPrefix`），目标框架 `.NET 10.0`。

---

## 目录

1. [项目概述](#1-项目概述)
2. [总体架构](#2-总体架构)
3. [目录结构与项目清单](#3-目录结构与项目清单)
4. [核心处理流程](#4-核心处理流程)
5. [分层模块详解与关键类](#5-分层模块详解与关键类)
   - 5.1 二进制解析层（AssetRipper.IO.Files）
   - 5.2 资产模型层（AssetRipper.Assets）
   - 5.3 数学类型库（AssetRipper.Numerics）
   - 5.4 YAML 序列化层（AssetRipper.Yaml）
   - 5.5 导入层（AssetRipper.Import）
   - 5.6 序列化逻辑（AssetRipper.SerializationLogic）
   - 5.7 处理层（AssetRipper.Processing）
   - 5.8 导出抽象层（AssetRipper.Export）
   - 5.9 Unity 工程导出（AssetRipper.Export.UnityProjects）
   - 5.10 专项导出模块（纹理/模型/音频/着色器/主内容）
   - 5.11 配置层（AssetRipper.Configuration）
   - 5.12 Web GUI（AssetRipper.GUI.Web / GUI.Free）
   - 5.13 源码生成器（SourceGenerator 系列）
   - 5.14 程序集反编译/重编译工具链（AssemblyDumper 系列）
   - 5.15 工具项目（AssetRipper.Tools.\*）
   - 5.16 兼容库（UnityEngine / Smolv / SpirV）
6. [项目间依赖关系](#6-项目间依赖关系)
7. [配置系统](#7-配置系统)
8. [构建与运行](#8-构建与运行)
9. [关键设计要点](#9-关键设计要点)

---

## 1. 项目概述

**AssetRipper** 是一个用于**分析 Unity 游戏文件**的强大开源工具（GPL v3.0），主要能力包括：

- 找出游戏中意外包含的依赖资源；
- 将游戏资源**转换回 Unity 引擎原生格式**（导出为完整可打开的 Unity 工程）；
- 识别构建中无法内联/裁剪的代码；
- 找出会导致游戏出问题的损坏资源引用。

它支持 Unity **3.5.0 到 6000.5.X** 的版本范围（不同版本支持质量有差异），提供 **Web 版 GUI** 和命令行入口，另有付费 Premium 版（源码中通过 `ExportHandler` 子类扩展）。

**核心技术栈**：C#（C# 14 / latest）、.NET 10、ASP.NET Core Minimal API（Web UI）、Avalonia（历史桌面 UI，当前已迁移为 Web UI）、AsmResolver（.NET 程序集操作）、Cpp2IL（IL2CPP 还原）、ILSpy/ICSharpCode.Decompiler（反编译）。

---

## 2. 总体架构

AssetRipper 采用**经典的分层管线架构**，数据自底向上流动：

```
┌─────────────────────────────────────────────────────────────┐
│  入口层  GUI.Free / GUI.Web / Tools.* / DocExtraction.Console │
├─────────────────────────────────────────────────────────────┤
│  导出层  Export.UnityProjects / Export.PrimaryContent        │
│         Export.Modules.{Textures,Models,Audio,Shaders}       │
├─────────────────────────────────────────────────────────────┤
│  处理层  Processing（资产后处理、脚本修复、场景/预制体构建）    │
├─────────────────────────────────────────────────────────────┤
│  导入层  Import（平台识别、文件收集、资产工厂、程序集管理）     │
├─────────────────────────────────────────────────────────────┤
│  资产模型层  Assets（资产集合、对象模型、Bundle 层次）          │
├─────────────────────────────────────────────────────────────┤
│  基础层  IO.Files（文件解析）| Numerics | Yaml | Configuration│
│          SerializationLogic | SourceGenerated（生成的资产类）  │
└─────────────────────────────────────────────────────────────┘
```

**核心数据流**（关键入口 [ExportHandler.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs)）：

```
原始游戏文件/文件夹
   │  GameStructure.Load()       —— 平台检测、文件收集（不反序列化）
   ▼
GameBundle（文件集合，含 SerializedAssetCollection 的元数据）
   │  GameData.FromGameStructure()
   ▼
GameData（记录 ProjectVersion / AssemblyManager / PlatformStructure）
   │  Process()                  —— 依次执行各 IAssetProcessor（此时才触发反序列化）
   ▼
处理后的 GameData
   │  Export()                   —— ProjectExporter 收集 IExportCollection 并逐个导出
   ▼
Unity 工程（Assets / ProjectSettings / Packages 等）+ 后处理文件
```

**两大关键设计**：

1. **懒加载（Lazy Loading）**：导入阶段只解析文件元数据（文件头、对象表、类型表），资产对象**在首次访问时才按需反序列化**，显著降低加载阶段内存峰值。见 [SerializedAssetCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs)。
2. **导出器栈（ObjectHandlerStack）**：每种资产类型对应一个导出器栈，栈顶优先尝试，失败则回退到栈内下一个导出器，支持通过 `OverrideExporter` 覆盖/扩展导出行为（Premium 版即依赖此机制）。

---

## 3. 目录结构与项目清单

仓库根目录主要项：

| 路径 | 说明 |
|---|---|
| `AssetRipper.slnx` | 解决方案文件（XML 格式） |
| `Source/` | 全部源码项目 |
| `docs/` | DocFX 文档站点（articles + docfx.json） |
| `Localizations/` | 界面多语言文件（en_US.json 等，经 Weblate 维护） |
| `UnityShaderRecompilationTools/` | 着色器重编译工具链 |
| `Media/` | 媒体资源 |
| `out/`、`0Bins/` | 构建产物输出目录 |
| `.github/workflows/` | CI（构建、测试、发布、DocFX） |

`Source/` 下的项目按职责分组：

### 基础层
| 项目 | 职责 |
|---|---|
| `AssetRipper.IO.Files` | Unity 文件格式二进制解析（Bundle / SerializedFile / WebFile / 资源文件 / 压缩流 / SmartStream） |
| `AssetRipper.Assets` | 资产对象模型：`IUnityObjectBase`、`AssetCollection`、`Bundle`/`GameBundle`、PPtr 引用、克隆/遍历 |
| `AssetRipper.Numerics` | Unity 数学类型（Vector2/3/4、Matrix4x4、Color 等，供生成类使用） |
| `AssetRipper.Yaml` | YAML 写出器（导出 `.meta` / `.asset` 文本文件用） |
| `AssetRipper.Configuration` | 通用配置数据容器（DataSet / DataInstance / Singleton 与 List 存储） |
| `AssetRipper.SerializationLogic` | 判断资产属于场景/预制体等序列化类型的静态逻辑 |

### 管线层
| 项目 | 职责 |
|---|---|
| `AssetRipper.Import` | 导入：平台检测、文件收集、`GameAssetFactory` 资产创建、Mono/IL2CPP 程序集管理、依赖映射 |
| `AssetRipper.Processing` | 处理：脚本修复处理器、场景/预制体/精灵/主资产等后处理 |
| `AssetRipper.Export` | 导出抽象：`ObjectHandlerStack`、`IAssetExporter` 契约、导出配置 |
| `AssetRipper.Export.UnityProjects` | 导出为 Unity 工程：`ProjectExporter`、各类型导出集合、`.meta`/YAML、脚本 DLL |
| `AssetRipper.Export.PrimaryContent` | 主内容导出（音频/纹理/模型/脚本的"直接提取"模式，不生成工程结构） |
| `AssetRipper.Export.Modules.Textures` | 纹理解码/编码（BCn、ASTC、Crunch、DirectXTex 互操作、BMP 写出） |
| `AssetRipper.Export.Modules.Models` | GLB 模型导出（GlbMeshBuilder / GlbLevelBuilder 等） |
| `AssetRipper.Export.Modules.Audio` | 音频解码/转换（Fmod5Sharp、NAudio、NVorbis） |
| `AssetRipper.Export.Modules.Shaders` | 着色器导出与重编译（UltraShaderConverter、SpirV、DxShaderProgramRestorer） |

### 界面与宿主
| 项目 | 职责 |
|---|---|
| `AssetRipper.GUI.Free` | 免费版入口（`Program.cs` 直接委托 `WebApplicationLauncher.Launch`） |
| `AssetRipper.GUI.Web` | Web GUI：ASP.NET Core Minimal API + 手写 HTML 页面 + Vue 前端脚本 |
| `AssetRipper.Web` | Web 基础库（静态内容、Json 序列化上下文、HttpClient 封装等） |
| `AssetRipper.GUI.Licensing` / `GUI.Localizations` | 许可信息与本地化资源 |

### 代码生成与工具链
| 项目 | 职责 |
|---|---|
| `AssetRipper.IO.Files.SourceGenerator` | 为 IO.Files 生成代码（如按平台/版本分支的读取逻辑） |
| `AssetRipper.GUI.SourceGenerator` | 为 GUI 生成代码 |
| `AssetRipper.GUI.Localizations.SourceGenerator` | 由 `Localizations/en_US.json` 生成本地化强类型代码 |
| `AssetRipper.GUI.Licensing.SourceGenerator` | 生成第三方许可信息代码 |
| `AssetRipper.Processing.SourceGenerator` | 为 Processing 生成代码 |
| `AssetRipper.SourceGenerated.Extensions.SourceGenerator` | 为生成的资产类生成扩展代码 |
| `AssetRipper.AssemblyDumper` | 从 Unity 安装导出程序集信息（生成 `AssetRipper.SourceGenerated.dll`） |
| `AssetRipper.AssemblyDumper.Downloader` | 下载 Unity 版本数据 |
| `AssetRipper.AssemblyDumper.NativeEnumExtractor` | 提取原生枚举 |
| `AssetRipper.AssemblyDumper.Recompiler` | 将反编译结果重编译为源码工程 |
| `AssetRipper.AssemblyDumper.NuGetFixer` | 修正重编译工程对 Unity 程序集的 NuGet 引用 |
| `AssetRipper.AssemblyDumper.Utils` | 共用工具 |
| `AssetRipper.DocExtraction` / `ConsoleApp` | 从反编译程序集提取 XML 文档 |
| `AssetRipper.Tools.*` | 各独立命令行工具（见 [5.15](#515-工具项目assetrippertools)） |

### 兼容/第三方库
| 项目 | 职责 |
|---|---|
| `UnityEngine` | 最小 Unity 引擎类型占位（仅用于单元测试） |
| `Smolv` | SPIR-V 到 Vulkan GLSL 转换（C# 移植） |
| `SpirV` | SPIR-V 二进制处理 |

---

## 4. 核心处理流程

完整流程由 [ExportHandler.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs#L29-L192) 编排，包含四个阶段：

### 4.1 Load（加载）
1. `ZipExtractor.Process(paths)` 先解压 zip；
2. `PlatformChecker.CheckPlatform()` 识别平台（Windows/Android/iOS/Mac/Linux/WebGL/PS4/Switch/WiiU/WebPlayer/WindowsPhone 等），生成 `PlatformGameStructure` / `MixedGameStructure`，各平台结构类在 `Source/AssetRipper.Import/Platforms/` 下（如 [WindowsGameStructure.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Platforms/WindowsGameStructure.cs)）；
3. 按脚本后端（Mono / IL2CPP / Unknown）创建 `IAssemblyManager`；
4. `GameBundle.FromPaths()` 用 `GameAssetFactory` 构建 Bundle 层次，**只解析文件元数据，不反序列化对象**；
5. 得到 `GameData`（含 `GameBundle`、`ProjectVersion`、`AssemblyManager`、`PlatformStructure`）。

### 4.2 Process（处理）
依次调用 `GetProcessors()` 返回的 `IAssetProcessor` 列表（见 [ExportHandler.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs#L62-L102)），分两组：

- **程序集处理器**：`AttributePolyfillGenerator`、`MonoExplicitPropertyRepairProcessor`、`ObfuscationRepairProcessor`、`ForwardingAssemblyGenerator`、`MethodStubbingProcessor`（可选）、`NullRefReturnProcessor`、`UnmanagedConstraintRecoveryProcessor`、`NullableRemovalProcessor`（可选）、`SafeAssemblyPublicizingProcessor`（可选）、`RemoveAssemblyKeyFileAttributeProcessor`、`InternalsVisibileToPublicKeyRemover`；
- **资产处理器**：`SceneDefinitionProcessor`、`OriginalPathProcessor`、`MainAssetProcessor`、`AnimatorControllerProcessor`、`AudioMixerProcessor`、`EditorFormatProcessor`、`LightingDataProcessor`、`PrefabProcessor`、`SpriteProcessor`、`ScriptableObjectProcessor`。

处理阶段会触发资产反序列化（`EnsureAssetsLoaded`）。

### 4.3 Export（导出为 Unity 工程）
1. 创建 `ProjectExporter`；
2. `projectExporter.Export()`：为每个资产创建 `IExportCollection`（同一集合可包含主资产 + 关联子资产），支持**资产去重**与**确定性 GUID**（见 [ProjectExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L93-L140)）；
3. `ProjectAssetContainer` 负责跨集合的 PPtr/meta 引用解析；
4. 遍历集合调用 `collection.Export()` 写出资源文件 + `.meta`；
5. **后处理**（`GetPostExporters()`）：`ProjectVersionPostExporter`、`PackageManifestPostExporter`、`StreamingAssetsPostExporter`、`DllPostExporter`、`PathIdMapExporter`。

### 4.4 主内容导出（可选模式）
`PrimaryContentExporter` 不走工程结构，直接按类型提取音频/纹理/模型/脚本二进制与 JSON/YAML。

---

## 5. 分层模块详解与关键类

### 5.1 二进制解析层（AssetRipper.IO.Files）

职责：把 Unity 的二进制文件格式解析为内存中的结构化文件对象。

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `FileBase` | [FileBase.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/FileBase.cs) | 所有文件类型的基类，定义 `Read(SmartStream)` / `Write(Stream)` / `ReadContents` / `ToByteArray` |
| `FileContainer` | [FileContainer.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/FileContainer.cs) | 容器型文件（Bundle/WebFile）基类，把解析出的 `SerializedFile`、`ResourceFile`、嵌套容器、压缩文件、失败文件分类保存 |
| `SmartStream` | [SmartStream.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/Streams/Smart/SmartStream.cs) | 流封装抽象（文件流/多部分流/临时流/内存流）；`CreatePartial` 创建分段流，`CreateTemp` 把大流落盘为临时文件 |
| `SerializedFile` | [SerializedFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/SerializedFile.cs) | Unity 序列化文件（.assets/.sharedAssets 等）的解析结果：`Generation`（格式版本）、`Version`（Unity 版本）、`Platform`、`Flags`、`EndianType`、依赖列表/对象表/类型表；支持读写 |
| `SerializedFileHeader` | [SerializedFileHeader.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/SerializedFiles/Parser/SerializedFileHeader.cs) | 文件头：metadataSize / fileSize / dataOffset / endianess / version，`IsSerializedFileHeader` 校验合法性 |
| `SerializedFileMetadata` | `SerializedFiles/Parser/SerializedFileMetadata.cs` | 元数据：Unity 版本签名、target platform、externals、objects、types、type tree 开关 |
| `WebFile` | [WebFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/WebFiles/WebFile.cs) | UnityWebData1.0 容器解析 |
| `FileStreamBundleFile` | [FileStreamBundleFile.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.IO.Files/BundleFiles/FileStream/FileStreamBundleFile.cs) | FileStream 类 Bundle 文件：三步读取 header → metadata → data，块解析为 `ResourceFile`，失败记为 `FailedFile` |
| `UnityVersion` | `IO.Files/UnityVersion.cs`（位于 IO.Files） | Unity 版本结构，`TryParse` 解析 "2020.3.32f1" 形式，支持版本比较与大小判断 |
| `EndianType` / `EndianSpanReader` / `EndianReader` | `IO/Endian/` | 大小端读写基础工具 |

### 5.2 资产模型层（AssetRipper.Assets）

职责：定义"资产对象"这一统一模型，以及资产的容器层次（Bundle → AssetCollection → 资产）。

**核心接口与类型**：

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `IUnityObjectBase` | [IUnityObjectBase.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/IUnityObjectBase.cs) | 所有资产对象的接口：`AssetInfo`、`ClassID`、`ClassName`、`Collection`、`PathID`、`OriginalPath/Name/Extension` 与 `Override*` 路径覆盖；提供 `GetBestDirectory()` / `GetBestName()` / `GetBestExtension()` 决定导出路径与文件名（优先级见代码注释） |
| `UnityObjectBase` | [UnityObjectBase.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/UnityObjectBase.cs) | 默认实现基类 |
| `AssetCollection` | [AssetCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) | 一组资产：`Dependencies` 依赖列表（0 号索引为自身，保证文件索引对应）、`CreatePPtr`/`ForceCreatePPtr` 创建资产指针、`TryGetAsset(fileIndex, pathID)` 按依赖索引解析引用；**懒加载核心**：`EnumerateAssetMetadata()`（不触发反序列化）、`TryGetAssetOnly()`（单对象按需反序列化）、`EnsureAssetsLoaded()`、`UnloadAssets()` |
| `AssetCollection.AssetMetadata` | 同上 | 轻量元数据视图（仅 PathID + ClassID），用于不反序列化即识别资产类型 |
| `SerializedAssetCollection` | [SerializedAssetCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Collections/SerializedAssetCollection.cs) | 从 `SerializedFile` 构建的集合，采用按需反序列化：保存 `_sourceFile` + `_factory` 引用，首次访问才填充 assets 字典 |
| `ProcessedAssetCollection` | `Collections/ProcessedAssetCollection.cs` | 处理阶段新增资产的集合（如预计算数据） |
| `SceneDefinition` | `Collections/SceneDefinition.cs` | 场景定义（场景资产集合与场景路径的映射） |
| `Bundle` | [Bundle.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Bundles/Bundle.cs) | 资源包：`AddCollection`、`ResolveCollection`、`ResolveExternalResource`、`InitializeAllDependencyLists` |
| `GameBundle` | [GameBundle.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Assets/Bundles/GameBundle.cs) | 整个游戏的根 Bundle：`FromPaths` 批量加载、`GetMaxUnityVersion`、`AddNewProcessedCollection`、`HasAnyAssetCollections` |
| `SerializedBundle` / `ProcessedBundle` / `VirtualBundle` | `Bundles/` | 序列化 Bundle / 处理结果 Bundle / 虚拟（合并）Bundle |
| `PPtr<T>` | `Assets/Metadata/` | 资产引用（fileIndex + pathID），`TryGetAsset(PPtr)` 解析引用 |
| `AssetInfo` | `Assets/Metadata/` | 资产位置信息（Collection + PathID） |
| `ClassIDType` | `Assets/` | Unity 类 ID 枚举（1=GameObject, 28=Texture2D, 48=Shader, 114=MonoBehaviour…） |
| `AssetFactoryBase` | `Assets/IO/` | 资产创建抽象（`ReadAsset(AssetInfo, data, type)`），导入层实现之 |
| `IDependencyProvider` / `IResourceProvider` | `Bundles/` | 依赖解析与外部资源提供接口（用于跨文件夹/外部资源解析） |
| 克隆/遍历 | `Cloning/`、`Traversal/` | 资产深拷贝（`IDeepCloneable`）、对象图遍历（`ContentHashWalker` 等） |

### 5.3 数学类型库（AssetRipper.Numerics）

提供 Unity 数学类型的只读包装与扩展：`Vector2/3/4`、`Vector2Int/3Int`、`Quaternion`、`Matrix4x4`、`Color`、`Color32`、`Rect`、`AABB` 等。这些类型被生成的资产类（SourceGenerated）复用，避免直接依赖 `System.Numerics` 的语义差异。

### 5.4 YAML 序列化层（AssetRipper.Yaml）

导出 Unity 文本资产（`.asset`、`.meta`、`ProjectSettings.asset`）时使用的 YAML 写出器：

| 关键类型 | 说明 |
|---|---|
| `YamlWriter` | 顶层写出器，管理缩进与文档分隔（`---`） |
| `YamlMappingNode` / `YamlSequenceNode` / `YamlScalarNode` | 映射/序列/标量节点 |
| `YamlTag` | Unity 标签（如 `!u!114`） |

### 5.5 导入层（AssetRipper.Import）

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `GameStructure` | [GameStructure.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Structure/GameStructure.cs) | 游戏结构：`Load()` 静态入口（先解压 zip → 平台检测 → 程序集管理 → 构建 GameBundle）；持有 `PlatformStructure`/`MixedStructure`、`AssemblyManager` |
| `GameData` | [GameData.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/GameData.cs) | 跨管线传递的数据记录：`GameBundle` + `ProjectVersion` + `AssemblyManager` + `PlatformStructure`；`FromGameStructure()` 创建；`EnumerateAssetsByClassID` 按类型枚举（只反序列化指定 ClassID，避免全量加载） |
| `GameAssetFactory` | [GameAssetFactory.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/AssetCreation/GameAssetFactory.cs) | 资产创建工厂：`ReadAsset` 分发——MonoBehaviour 走结构解析（`SerializableTreeType`），其他走 `AssetFactory.CreateSerialized`（基于生成的强类型类）；支持游戏专属解析器（`IGameAssetProvider`，如 Nikki4）与 TypeTree 回退（`TypeTreeObject`） |
| `PlatformGameStructure` 及子类 | `Platforms/*.cs` | 各平台文件收集：Windows/Android/iOS/Mac/Linux/WebGL/PS4/Switch/WiiU/WebPlayer/WindowsPhone，外加 `MixedGameStructure` 与 `PlatformChecker` |
| `IAssemblyManager` / `MonoManager` / `IL2CppManager` / `Il2CppDumpManager` / `BaseManager` | `Structure/Assembly/Managers/` | 脚本程序集加载：Mono 直接读 DLL；IL2CPP 通过 Cpp2IL 还原或外部 dump 目录加载；`OnRequestAssembly` 按需解析脚本 |
| `GameInitializer` | `Structure/GameInitializer.cs` | 版本变更（`VersionChanger`）、引擎资源注入（`EngineResourceInjector`）、自定义资源提供 |
| `DependencyMap` / `DependencyMapScanner` | `Structure/DependencyMap.cs` | 跨文件夹依赖映射：打开子文件夹时解析不在范围内的依赖文件 |
| `ZipExtractor` | `Structure/ZipExtractor.cs` | 输入 zip 解压 |
| `Logger` / `ILogger` / `FileLogger` / `ConsoleLogger` | `Logging/` | 日志系统（含内存诊断 `LogMemoryDiagnostics`、状态变更 `SendStatusChange`） |
| `ScriptingBackend` | `Structure/Assembly/ScriptingBackend.cs` | Mono / IL2CPP / Unknown 枚举 |

### 5.6 序列化逻辑（AssetRipper.SerializationLogic）

静态判定资产应被序列化为场景（scene）还是普通资产：`IsScene` / `IsScriptableObject` / `IsSerializable` 等判断逻辑，供 `SceneDefinitionProcessor` 与导出集合归类使用。

### 5.7 处理层（AssetRipper.Processing）

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `IAssetProcessor` | [IAssetProcessor.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/IAssetProcessor.cs) | 处理器契约：`Process(GameData)` |
| `MainAssetProcessor` | [MainAssetProcessor.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/MainAssetProcessor.cs) | 为字体、地形等"主资产"设置 `MainAsset` 与依赖纹理/材质引用 |
| `SceneDefinitionProcessor` | `Scenes/` | 识别场景文件并生成 `SceneDefinition` |
| `PrefabProcessor` | `Prefabs/` | 构建预制体层次（m_RootGameObject、m_IsPrefabAsset 等） |
| `SpriteProcessor` | `Textures/` | 精灵图集/打包数据后处理 |
| `AnimatorControllerProcessor` / `AudioMixerProcessor` | `AnimatorControllers/`、`AudioMixers/` | 动画控制器 / 音频混音器后处理 |
| `LightingDataProcessor` | [LightingDataProcessor.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Processing/LightingDataProcessor.cs) | 光照数据后处理（依赖静态网格分离结果） |
| `ScriptableObjectProcessor` | `ScriptableObject/` | ScriptableObject 修正 |
| 程序集处理器 | `Assemblies/` | 反编译后脚本的修复/去混淆/桩方法生成（见 4.2 列表） |
| `OriginalPathProcessor` | `Scenes/` | 设置资产原始路径（支持按 Bundle 名分组模式） |

### 5.8 导出抽象层（AssetRipper.Export）

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `ObjectHandlerStack<T>` | [ObjectHandlerStack.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export/ObjectHandlerStack.cs) | 导出器栈：`OverrideHandler(type, handler, allowInheritance)` 压栈，`GetHandlerStack(type)` 返回自栈顶向下的处理器序列 |
| `IAssetExporter` | [IAssetExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/IAssetExporter.cs) | 导出器契约：`TryCreateCollection` / `Export` / `ToExportType` |
| `IExportCollection` / `IExportContainer` | `Export.UnityProjects/` | 导出集合契约：`Assets`、`Name`、`Exportable`、`Export(container, path, fs)`；容器提供跨集合的引用解析 |
| `FullConfiguration` | [FullConfiguration.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export/Configuration/FullConfiguration.cs) | 完整配置：`CoreConfiguration` + `ProcessingSettings` + `ExportSettings` + 引擎资源数据，支持从默认路径读写 |

### 5.9 Unity 工程导出（AssetRipper.Export.UnityProjects）

| 关键类型 | 文件 | 说明 |
|---|---|---|
| `ExportHandler` | [ExportHandler.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportHandler.cs) | 管线总编排：`Load` / `Process` / `Export` / `LoadAndProcess` / `LoadProcessAndExport`；**Premium 版通过继承此类扩展**（`GameFileLoader.Premium` 即判断类型是否为子类） |
| `ProjectExporter` | [ProjectExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs) | 导出主循环：`CreateCollections`（为每个资产创建集合，去重）、`ProjectAssetContainer` 组装、逐个 `collection.Export` 并触发进度事件；`OverrideExporter` 注册/覆盖导出器 |
| `ProjectExporter.Overrides` | `ProjectExporter.Overrides.cs` | 内置默认导出器的注册（纹理/网格/音频/动画/场景/预制体/脚本等） |
| `ExportCollection` | [ExportCollection.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ExportCollection.cs) | 通用导出集合实现：创建目录、生成唯一文件名、调用 `AssetExporter.Export`、生成 `.meta` |
| `AssetExportCollection` / `BinaryAssetExporter` | `AssetExportCollection.cs` / `BinaryAssetExporter.cs` | 二进制资产导出 |
| `ProjectAssetContainer` | `ProjectAssetContainer.cs` | 工程级容器：维护 `(guid, fileID)` 映射、跨集合 PPtr 解析、跳过集合与重定向映射 |
| `Meta` / `MetaPtr` | `Meta.cs` / `MetaPtr.cs` | `.meta` 文件生成与 GUID/fileID 引用 |
| `ExportIdHandler` | `ExportIdHandler.cs` | 资产导出 ID（fileID）分配 |
| `YamlWalker` | `YamlWalker.cs` | 把资产对象图写出为 Unity YAML |
| `ContentHashWalker` | `ContentHashWalker.cs` | 内容哈希（XxHash），用于资产去重指纹 |
| `DeterministicGuidCalculator` | `DeterministicGuidCalculator.cs` | 确定性 GUID 计算（跨批次稳定） |
| `DummyAssetExporter` / `FailExportCollection` / `SkipExportCollection` / `RedirectExportCollection` | 根目录 | 特殊导出集合：哑导出（占位）、失败记录、跳过、重定向 |
| `IPostExporter` 及其实现 | `IPostExporter.cs`、`Project/`、`Scripts/`、`PathIdMapping/` | 导出后处理：工程版本、包清单、StreamingAssets、脚本 DLL、PathID 映射 |
| 分类导出器 | `Textures/`、`Shaders/`、`Audio/`、`AudioMixers/`、`AnimatorControllers/`、`UserAssets/`、`EngineAssets/`、`Terrains/`、`RawAssets/`、`DeletedAssets/`、`Miscellaneous/`、`Scripts/` | 各类资产的导出集合与导出器 |

### 5.10 专项导出模块

#### 纹理（Export.Modules.Textures）
| 类型 | 说明 |
|---|---|
| `TextureConverter` | 核心纹理转换器：`ConvertTexture` 把 Unity 纹理格式（BCn/ASTC/ETC/DXT 等）转为可写格式 |
| `DirectBitmap<T>` / `DirectBitmap` | 直接内存位图封装 |
| `BmpWriter` | BMP 写出 |
| `CrunchHandler` | Crunch 压缩纹理解码 |
| `SpriteConverter` / `TerrainHeatmap` | 精灵转换 / 地形热力图 |

#### 模型（Export.Modules.Models）
| 类型 | 说明 |
|---|---|
| `GlbMeshBuilder` / `GlbSubMeshBuilder` / `GlbLevelBuilder` / `GlbTerrainBuilder` / `GlbWriter` | 把 Unity 网格导出为 GLB（glTF Binary）：网格构建、LOD 层级、地形、坐标转换 |
| `GlbCoordinateConversion` | 坐标系转换（左手 → 右手） |

#### 音频（Export.Modules.Audio）
| 类型 | 说明 |
|---|---|
| `AudioClipDecoder` | 音频解码（经 Fmod5Sharp / NAudio / NVorbis） |
| `AudioConverter` | 音频格式转换 |

#### 着色器（Export.Modules.Shaders）
| 类型 | 说明 |
|---|---|
| `DXShaderProgramRestorer` | DX 着色器还原 |
| `UltraShaderConverter/` | UltraShaderConverter 着色器转换器（解析字节码并生成可读 HLSL/GLSL） |
| `ShaderBlob/`、`ConstantBuffers/`、`Exporters/`、`Handlers/` | 着色器 blob 解析、常量缓冲、各平台导出器 |
| `Smolv` / `SpirV` | SPIR-V 转换与处理 |

#### 主内容导出（Export.PrimaryContent）
| 类型 | 说明 |
|---|---|
| `PrimaryContentExporter` | 主内容导出器（不生成工程结构，直接提取资源） |
| `IContentExtractor` / `BinaryAssetContentExtractor` / `JsonContentExtractor` | 二进制 / JSON 内容提取器 |
| `ExportCollectionBase` / `SingleExportCollection` / `MultipleExportCollection` | 主内容导出集合基类与单/多资产变体 |

### 5.11 配置层（AssetRipper.Configuration）

通用配置数据容器，采用"存储（Storage）→ 数据集（DataSet）→ 实例（DataInstance）→ 条目（DataEntry）"四级结构：

| 类型 | 说明 |
|---|---|
| `DataStorage` / `SingletonDataStorage` / `ListDataStorage` | 存储：单例键值存储 / 列表存储 |
| `DataSet` / `JsonDataSet` / `ParsableDataSet` / `StringDataSet` | 数据集抽象与序列化方式 |
| `DataInstance` / `JsonDataInstance` / `ParsableDataInstance` / `StringDataInstance` | 数据实例 |
| `DataEntry` / `DataSerializer` / `JsonDataSerializer` | 条目与序列化器 |

`CoreConfiguration`（[CoreConfiguration.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Configuration/CoreConfiguration.cs)）提供：`ImportSettings`、导出路径（`ExportRootPath`/`ProjectRootPath`/`AssetsPath`/`ProjectSettingsPath`/`AuxiliaryFilesPath`）、`SetProjectSettings(version)`。`FullConfiguration` 在其上增加 `ProcessingSettings`、`ExportSettings`，并支持 `LoadFromDefaultPath` / `SaveToDefaultPath`。

### 5.12 Web GUI（AssetRipper.GUI.Web / GUI.Free）

- **入口**：[WebApplicationLauncher.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/WebApplicationLauncher.cs#L40-L69) —— `Launch(args)` 解析命令行参数（端口/headless/日志路径/本地 Web 文件覆盖），然后启动 ASP.NET Core Minimal API。免费桌面版 [Program.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Free/Program.cs) 直接委托给 `WebApplicationLauncher.Launch(args)`。
- **运行时宿主**：`GameFileLoader`（[GameFileLoader.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/GameFileLoader.cs)）静态持有 `GameData` / `GameBundle` / `AssemblyManager` / `Settings` / `ExportHandler`；提供 `LoadAndProcess`、`ExportUnityProject`、`ExportPrimaryContent`、`Reset`（释放 + 双轮 GC）。
- **页面**：`Pages/` 下以静态类 + `DefaultPage`/`VuePage` 基类组织，路由注册见 [WebApplicationLauncher.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/WebApplicationLauncher.cs#L156-L281)：
  - 常规页：`/`（首页）、`/Commands`、`/Privacy`、`/Licenses`、`/PremiumFeatures`、`/ConfigurationFiles`、`/Settings/Edit`；
  - 数据页：`AssetAPI`（资产视图/图片/音频/模型/字体/视频/JSON/YAML/文本/二进制）、`BundleAPI`、`CollectionAPI`、`FailedFileAPI`、`ResourceAPI`、`SearchAPI`、`SceneAPI`；
  - 命令接口（POST）：`/Export/UnityProject`、`/Export/PrimaryContent`、`/LoadFile`、`/LoadFolder`、`/Reset`、`/Commands/GenerateDependencyMap`；
  - 对话框与 IO 探测：`/Dialogs/SaveFile|OpenFolder|OpenFile(s)`、`/IO/File/Exists` 等。
- **命令模式**：`ICommand`（[ICommand.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/Pages/ICommand.cs)）+ `Commands` 注册表，`Commands.HandleCommand<T>()` 统一处理表单提交。
- **静态内容**：`StaticContentLoader` 支持运行时注入本地 css/js 覆盖，`OnlineDependencies` 加载在线依赖。
- **Swagger/OpenAPI**：自动生成 API 文档，附 Swagger UI。

### 5.13 源码生成器（SourceGenerator 系列）

| 项目 | 生成内容 |
|---|---|
| `AssetRipper.IO.Files.SourceGenerator` | 为 IO.Files 生成与 Unity 版本/平台分支相关的读取代码 |
| `AssetRipper.GUI.SourceGenerator` | 为 GUI 页面/路由生成样板代码 |
| `AssetRipper.GUI.Localizations.SourceGenerator` | 由 `Localizations/en_US.json` 生成强类型本地化键（`Localization.ExportDirectoryDeleteUserConfirmation` 等） |
| `AssetRipper.GUI.Licensing.SourceGenerator` | 生成第三方许可信息清单（供 `/Licenses` 页展示） |
| `AssetRipper.Processing.SourceGenerator` | 为 Processing 生成处理器相关代码 |
| `AssetRipper.SourceGenerated.Extensions.SourceGenerator` | 为生成的资产类生成扩展代码（如 `ReadRelease`/`WriteRelease` 变体） |

### 5.14 程序集反编译/重编译工具链（AssemblyDumper 系列）

这是 AssetRipper 的"模型生成器"：把 Unity 引擎的托管程序集转换成项目内的**强类型资产类**（`AssetRipper.SourceGenerated`）。

完整流程（`Source/generate.bat`）：

1. **Downloader**：下载 Unity 版本数据；
2. **AssemblyDumper**（`AssetRipper.AssemblyDumper.exe`）：读取 Unity 安装程序集，生成 `AssetRipper.SourceGenerated.dll`（包含 `ClassID_NN` 资产类、子类结构等）；
3. **Recompiler**：把生成的 DLL 反编译为 C# 源码工程并重编译（`AssetRipper.AssemblyDumper.Recompiler.exe`）；
4. **NuGetFixer**：修正重编译工程对 Unity 程序集引用的 NuGet 包；
5. **NativeEnumExtractor / Utils**：原生枚举提取与工具。

生成的类位于 `AssetRipper.SourceGenerated` 命名空间（如 `AssetRipper.SourceGenerated.Classes.ClassID_28` 对应 Texture2D，`ClassID_48` 对应 Shader，`ClassID_114` 对应 MonoBehaviour），`Subclasses/*` 为内嵌结构体。这些类在编译期由 `SourceGenerated.Extensions` 等提供扩展方法。

### 5.15 工具项目（AssetRipper.Tools.\*）

| 工具 | 职责 |
|---|---|
| `AssetRipper.Tools.CabMapGenerator` | 生成 CAB 映射 |
| `AssetRipper.Tools.DependenceGrapher` | 生成依赖关系图 |
| `AssetRipper.Tools.FileExtractor` | 文件提取 |
| `AssetRipper.Tools.JsonSerializer` | JSON 序列化测试/工具 |
| `AssetRipper.Tools.MonoBehaviourTester` | MonoBehaviour 解析测试 |
| `AssetRipper.Tools.RawTextureExtractor` | 原始纹理提取 |
| `AssetRipper.Tools.SystemTester` | 系统自检（引用 GameData 等核心类型） |
| `AssetRipper.Tools.TypeTreeExtractor` | 类型树提取 |
| `AssetRipper.Tools.UnityPackageGuidCollector` | Unity 包 GUID 收集 |

### 5.16 兼容库（UnityEngine / Smolv / SpirV）

- `UnityEngine`：仅用于单元测试的最小引擎类型占位（`GameObject`、`MonoBehaviour`、`Vector3` 等），README 注明 "Ignore this. It's just for unit testing."；
- `Smolv`：SPIR-V → Vulkan GLSL（Smol-v）转换的 C# 移植；
- `SpirV`：SPIR-V 二进制解析/处理。

---

## 6. 项目间依赖关系

依赖方向自底向上（`csproj` 的 ProjectReference 链）：

```
[基础]  IO.Files  ←  Assets ←  Import ←  Processing
                ↘     ↙ Numerics / Yaml / Configuration（被多个上层引用）
                        ↙
    SerializationLogic（被 Import / Assets 引用）
    SourceGenerated（AssetRipper.SourceGenerated.dll，被 Import/Export 引用）

[导入]  Import
        ├── 引用 IO.Files、Assets、SerializationLogic、Configuration
        └── 程序集工具链（AsmResolver、Cpp2IL、ICSharpCode.Decompiler 等 NuGet）

[处理]  Processing → Import、Assets

[导出]  Export（抽象）→ Assets、Configuration
        Export.UnityProjects → Export、Import、Processing、Assets
        Export.PrimaryContent → Export、Export.UnityProjects
        Export.Modules.{Textures,Models,Audio,Shaders} → Assets、Export.UnityProjects
        （GUI.Web 引用 Export.Modules.Shaders / PrimaryContent / UnityProjects）

[界面]  GUI.Web → Export.UnityProjects、Export.PrimaryContent、Export.Modules.Shaders、
                  GUI.Licensing、GUI.Localizations、Web
        GUI.Free → GUI.Web（免费版入口复用 Web GUI）

[工具]  Tools.* → 核心库子集（按需）
```

关键依赖观察：

1. `AssetRipper.GUI.Free` **只引用 `AssetRipper.GUI.Web`**——桌面版实际上以 Web 服务方式运行并自动打开浏览器；
2. `ProjectExporter`（UnityProjects 层）**不引用 `AssetRipper.Export`**，通过 `CoreConfiguration.SingletonData` 读取 `ProcessingSettings`（见 [ProjectExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/ProjectExporter.cs#L93-L106)）；
3. 所有项目共享 `Source/Directory.Build.props` 的公共属性（net10.0、Nullable、IsAotCompatible、版本号 1.3.14）；
4. 外部 NuGet 依赖集中在各 csproj（AsmResolver、Cpp2IL、ICSharpCode.Decompiler、SharpGLTF、Fmod5Sharp、NAudio、NVorbis、SharpCompress、Lz4、DirectXTexNet 等，许可证见 `Source/Licenses/`）。

---

## 7. 配置系统

### 配置层级

```
CoreConfiguration（导入设置 + 导出路径 + 版本）
  └── FullConfiguration（+ ProcessingSettings + ExportSettings + 引擎资源数据）
```

### 配置持久化
- `SerializedSettings`（Import / Processing / Export 三段 JSON）存于默认路径；
- `FullConfiguration.LoadFromDefaultPath()` / `SaveToDefaultPath()`；
- 是否自动保存由 `ExportSettings.SaveSettingsToDisk` 控制（`MaybeSaveToDefaultPath`）；
- 语言选择：`ExportSettings.LanguageCode`，经 `Localization.LoadLanguage(code)` 加载 `Localizations/*.json`。

### 常用设置
- **导入**（`ImportSettings`）：`ScriptContentLevel`（Level0 禁用脚本 / Level1 桩方法）、`DefaultVersion`、`TargetVersion`、`GameType`、`IgnoreStreamingAssets`、`Il2CppDumpPath`、`LoadDependencyMap`/`DependencyMapPath`；
- **处理**（`ProcessingSettings`）：`RemoveNullableAttributes`、`PublicizeAssemblies`、`BundledAssetsExportMode`、`EnableAssetDeduplication`、`EnableDeterministicGuids`；
- **导出**（`ExportSettings`）：`SaveSettingsToDisk`、`LanguageCode` 等。

---

## 8. 构建与运行

### 环境要求（见 [docs/articles/Requirements.md](file:///d:/Project/AssetRipper/docs/articles/Requirements.md)）
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)；
- IDE：Visual Studio 2026 / VS Code + C# 扩展 / JetBrains Rider（需支持 C# 14）；
- 运行目标游戏的 Unity 编辑器版本建议 ≥ 游戏版本（仅运行二进制导出时所需）。

### 构建
```bash
dotnet build -c Debug d:\Project\AssetRipper
```
（`Source/generate.bat` 提供一键构建 + 重新生成 SourceGenerated 程序集工具链的脚本。）

### 运行（Web GUI）
```bash
dotnet run --project Source/AssetRipper.GUI.Web
```
启动后在浏览器访问 `http://127.0.0.1:<port>`。命令行参数（`Arguments`）：`--port`（默认 0=随机端口）、`--headless`、`--log` / `--logPath`、`--localWebFile <file>`（覆盖静态资源）。

### 运行（免费桌面入口）
```bash
dotnet run --project Source/AssetRipper.GUI.Free
```
实际仍以 Web 服务方式启动并自动打开默认浏览器。

### 测试
```bash
dotnet test -c Debug
```
测试项目：`AssetRipper.Tests`、`AssetRipper.IO.Files.Tests`、`AssetRipper.Assets.Tests`、`AssetRipper.Yaml.Tests`、`AssetRipper.Numerics.Tests`、`AssetRipper.SerializationLogic.Tests`、`AssetRipper.GUI.Web.Tests`、`AssetRipper.Processing.Tests`、`AssetRipper.Indexing.Tests`、`AssetRipper.AssemblyDumper.Tests` 等。

### 文档站点
仓库内 `docs/` 为 DocFX 项目，CI 中由 `.github/workflows/docfx_build.yml` 构建发布到 GitHub Pages。

---

## 9. 关键设计要点

1. **懒加载资产**：`SerializedAssetCollection` 只保存数据源引用，对象按需反序列化；`AssetCollection.AssetMetadata` 允许以 (PathID, ClassID) 轻量元数据驱动处理流程（如 `OriginalPathProcessor`、`EnumerateAssetsByClassID`），避免全量加载触发 OOM。
2. **导出器栈**：`ObjectHandlerStack<IAssetExporter>` + `OverrideExporter` 使导出行为可覆盖、可扩展，是 Premium 版功能扩展的扩展点。
3. **导出集合（IExportCollection）**：把"主资产 + 关联子资产"打包为一个可整体导出的单元，配合 `ProjectAssetContainer` 完成跨集合的 `(guid, fileID)` 引用解析。
4. **资产去重**：`ContentHashWalker`（XxHash64）计算内容指纹，按 (ClassID, hash) 分组去重，并生成 `redirectMap` 保证引用不丢失；Shader 按名称去重。
5. **确定性 GUID**：可选按资产稳定标识计算 GUID，保证跨批次导出结果可复现。
6. **双 GC 重置**：`GameFileLoader.Reset` 在释放 Bundle 后执行两轮 `GC.Collect()`（第一轮触发终结器，第二轮回收终结器释放的对象），确保重新加载前内存被回收。
7. **代码生成驱动**：`AssetRipper.SourceGenerated` 的全部资产类型类由 AssemblyDumper 工具链从 Unity 程序集自动生成，配合多个 SourceGenerator 在编译期注入扩展，保证对海量 Unity 版本/类型的覆盖率。
8. **内存诊断**：管线各阶段通过 `Logger.LogMemoryDiagnostics` 记录内存峰值，用于验证懒加载效果。

---

*本文档由 AI 基于仓库静态分析自动生成，可能存在与最新代码不一致之处；如遇冲突，以源码为准。*
