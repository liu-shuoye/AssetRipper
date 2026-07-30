# Skip UPM 包程序集并改由 manifest.json 引用 Spec

## Why
AssetRipper 当前会把 Unity 官方/合作伙伴的 UPM 包程序集（如 `Unity.Mathematics`、`Unity.Burst`、`Unity.RenderPipelines.Core.Runtime` 等）当作第三方 DLL 一起导出到 `Assets/Plugins/`，而这些本应通过 `Packages/manifest.json` 以 Package 形式引用。

根因：
- [ScriptExporter.GetExportType](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ScriptExporter.cs) 的 `ReferenceAssemblyDictionary` 白名单只覆盖传统 `UnityEngine.*`、`Mono.*`，不含 UPM 包程序集。
- [PackageManifest.CreateDefault](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/PackageManifest.cs) 只写 30 个 `com.unity.modules.*`，不还原游戏实际依赖的 UPM 包。
- 这些程序集落到 `Save` 分支被写进 `Assets/Plugins/`。

直接 Skip 会导致预制体 MonoBehaviour 引用丢失，因为 [ScriptExporter.CreateExportPointer](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ScriptExporter.cs) 的 `Skip` 分支用 `ReferenceAssemblyDictionary[name]` 取 GUID，而字典里没有这些 UPM 程序集的真实 `.asmdef.meta` GUID。Unity 通过 Package Manager 安装包后用的是包自带的真实 GUID，必须保证字典里的值与之完全一致。

## What Changes
- 新增「UPM 程序集 → (UPM 包名, 真实 asmdef GUID)」映射表 `UnityPackageAssemblyMap`，数据通过工具从用户提供的 Unity 安装目录与项目 PackageCache 采集。
- 在 `ScriptExporter.GetExportType` 顶部新增优先判定：命中映射表的程序集走 `Skip`。
- 修正 `ScriptExporter.CreateExportPointer` 的 `Skip` 分支：UPM 程序集使用映射表里的真实 GUID，而非 `ReferenceAssemblyDictionary`（避免 KeyNotFoundException 或 GUID 不一致）。
- 在 `PackageManifestPostExporter` 中扫描实际加载的程序集列表，把命中映射表的包写入 `manifest.json` 的 `dependencies`，版本号从采集到的 `package.json` 取。
- 保留现有 `ReferenceAssemblyDictionary` 逻辑不动，避免影响传统引擎程序集。
- 保留 `Decompile` / `Save` 兜底逻辑：未命中映射表的程序集仍按原行为处理。

**BREAKING**：导出工程结构变化——UPM 包程序集不再出现在 `Assets/Plugins/` 或 `Assets/Scripts/`，改由 `Packages/manifest.json` 引用。已习惯旧导出结构的下游工具需要适配。

## Impact
- Affected specs: 无（新增能力）
- Affected code:
  - [ScriptExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ScriptExporter.cs) — `GetExportType` 与 `CreateExportPointer`
  - [ReferenceAssemblies.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ReferenceAssemblies.cs) — 旁路新增映射表（不改原字典）
  - [PackageManifestPostExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/PackageManifestPostExporter.cs) — 覆写 `CreateManifest`，需要拿到实际程序集列表
  - [PackageManifest.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Project/PackageManifest.cs) — 可能需要补 `scopedRegistries` 字段（暂不实现，留 TODO）
  - 新增 `UnityPackageAssemblyMap.cs` 与采集工具脚本

## ADDED Requirements

### Requirement: UPM 程序集映射表
系统 SHALL 提供一份「程序集名 → (UPM 包名, 真实 asmdef GUID, 包版本)」映射表，数据来源于用户指定的 Unity 编辑器内置包目录与项目 PackageCache 目录。

#### Scenario: 采集成功
- **WHEN** 运行采集工具扫描 `D:\Program Files\UnityHubEddie\2022.3.62f2c1\Editor\Data\Resources\PackageManager\BuiltInPackages` 与 `C:\Unity\Orisries\Library\PackageCache`
- **THEN** 工具递归查找所有 `.asmdef` + `.asmdef.meta` + `package.json`
- **AND** 输出 C# 静态字典源码文件，每条记录含：程序集名、UPM 包名（来自 package.json 的 name 字段）、真实 GUID（来自 .asmdef.meta 的 guid 字段）、包版本（来自 package.json 的 version 字段）

#### Scenario: 包名解析
- **GIVEN** 一个 .asmdef 文件位于包目录 `com.unity.mathematics@1.0.1/` 内
- **WHEN** 采集时
- **THEN** UPM 包名取该目录下 `package.json` 的 `name` 字段（而非目录名），保证与 `manifest.json` dependencies key 一致

#### Scenario: 重复程序集名处理
- **GIVEN** 同名 .asmdef 出现在多个包目录中（例如 BuiltInPackages 与 PackageCache 各一份）
- **WHEN** 采集时
- **THEN** 优先采用 PackageCache 版本（项目实际依赖），并记录冲突警告

### Requirement: Skip UPM 程序集导出
系统 SHALL 在 `ScriptExporter.GetExportType` 中优先判定 UPM 程序集并返回 `Skip`，使其不进入 `Assets/Plugins/` 或 `Assets/Scripts/`。

#### Scenario: 命中映射表的程序集
- **GIVEN** 加载的程序集名为 `Unity.Mathematics`
- **WHEN** 调用 `GetExportType("Unity.Mathematics")`
- **THEN** 返回 `AssemblyExportType.Skip`

#### Scenario: 未命中映射表的程序集
- **GIVEN** 加载的程序集名为 `MyCompany.Custom.dll`
- **WHEN** 调用 `GetExportType("MyCompany.Custom")`
- **THEN** 走原有判定逻辑（ReferenceAssemblyDictionary / Decompile / Save）

### Requirement: 真实 GUID 用于预制体引用
系统 SHALL 在 `ScriptExporter.CreateExportPointer` 的 `Skip` 分支中，对 UPM 程序集使用映射表里的真实 `.asmdef.meta` GUID，保证预制体引用与 Unity Package Manager 安装后的资产 GUID 一致。

#### Scenario: UPM 程序集的 MonoScript 引用
- **GIVEN** 预制体上的 MonoBehaviour 引用了 `Unity.Mathematics.dll` 中的 `float4` 类型的脚本（如有）
- **WHEN** AssetRipper 导出该预制体
- **THEN** YAML 中 `guid` 字段值等于映射表里 `Unity.Mathematics` 对应的真实 asmdef GUID
- **AND** Unity 通过 manifest.json 安装 `com.unity.mathematics` 后，该引用能正确解析到包内脚本

#### Scenario: fileID 计算不受影响
- **GIVEN** 同一脚本
- **WHEN** 计算 fileID
- **THEN** 仍使用 [ScriptHashing.CalculateScriptFileID](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ScriptHashing.cs)（MD4 哈希 namespace+类名），与原 Unity 算法一致

### Requirement: manifest.json 还原 UPM 依赖
系统 SHALL 在导出工程的 `Packages/manifest.json` 中补充实际命中的 UPM 包依赖，使 Unity 打开工程时能自动还原这些包。

#### Scenario: 自动写入依赖
- **GIVEN** 游戏数据加载了 `Unity.Mathematics`、`Unity.Burst`、`Unity.RenderPipelines.Core.Runtime` 三个程序集
- **WHEN** 执行后置导出
- **THEN** `manifest.json` 的 `dependencies` 中含 `com.unity.mathematics`、`com.unity.burst`、`com.unity.render-pipelines.core` 三条
- **AND** 版本号取自采集到的 `package.json` 的 `version` 字段

#### Scenario: 保留默认模块依赖
- **GIVEN** 任意导出
- **WHEN** 写 manifest.json
- **THEN** 仍包含 `PackageManifest.CreateDefault` 写入的 `com.unity.modules.*` 默认依赖
- **AND** 不覆盖用户已存在的依赖条目（用 `TryAdd` 语义）

### Requirement: 不影响非 UPM 程序集
系统 SHALL 保持现有 `Decompile` / `Save` 兜底逻辑不变，未命中 UPM 映射表的程序集仍按原行为导出。

#### Scenario: 第三方 DLL 仍导出到 Plugins
- **GIVEN** 游戏包含 `MyCompany.Custom.dll` 第三方程序集，不在 UPM 映射表中
- **WHEN** Hybrid 模式导出
- **THEN** 该 DLL 仍被保存到 `Assets/Plugins/MyCompany.Custom.dll` 并生成 `.meta`

## MODIFIED Requirements

### Requirement: ScriptExporter.GetExportType
原逻辑顺序：
1. `ReferenceAssemblyDictionary` 命中 → `Skip`
2. `AssemblyManager` 未设置 → `Decompile`
3. `Decompiled` 模式 → `Decompile`
4. `Hybrid` 模式：`IsPredefinedAssembly` 命中 → `Decompile`；否则 → `Save`
5. 其他 → `Save`

修改后逻辑顺序：
1. **`UnityPackageAssemblyMap` 命中 → `Skip`**（新增，最高优先级）
2. `ReferenceAssemblyDictionary` 命中 → `Skip`
3. 其余同原逻辑 2-5

### Requirement: ScriptExporter.CreateExportPointer
`Skip` 分支原逻辑：
```csharp
AssemblyExportType.Skip => new(ScriptHashing.CalculateScriptFileID(script), ReferenceAssemblyDictionary[script.GetAssemblyNameFixed()], AssetType.Meta),
```

修改后逻辑：
```csharp
AssemblyExportType.Skip => new(
    ScriptHashing.CalculateScriptFileID(script),
    ResolveSkipGuid(script.GetAssemblyNameFixed()),
    AssetType.Meta
),
```
其中 `ResolveSkipGuid` 优先查 `UnityPackageAssemblyMap`，未命中再查 `ReferenceAssemblyDictionary`。

### Requirement: PackageManifestPostExporter.CreateManifest
原逻辑：仅返回 `PackageManifest.CreateDefault(version)`。

修改后逻辑：
1. 调用 `PackageManifest.CreateDefault(version)` 获得基础依赖
2. 遍历实际加载的程序集列表（需从 `GameData` 或 `IAssemblyManager` 获取）
3. 对每个命中 `UnityPackageAssemblyMap` 的程序集，将对应包名与版本 `TryAdd` 到 `Dependencies`
4. 返回 manifest

注：当前 `CreateManifest` 签名只接收 `UnityVersion`，需要扩展为能访问程序集列表。优先方案：在 `DoPostExport` 中从 `gameData` 取程序集列表后传入，调整 `CreateManifest` 签名为 `CreateManifest(UnityVersion, IEnumerable<string> assemblyNames)` 或类似。

## REMOVED Requirements
无（不删除任何现有能力，仅新增旁路）。
