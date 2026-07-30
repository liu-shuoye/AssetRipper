# 修正 UPM 脚本 GUID 引用 Spec

## Why
前一个 spec（`skip-upm-package-assemblies`）用 `.asmdef.meta` 的 GUID 作为 Skip 分支的 guid，但 Unity 预制体中 MonoBehaviour 引用脚本时用的是每个 `.cs.meta` 的 GUID，且 fileID 恒为 `11500000`。两者都不匹配，导致预制体上的 MonoBehaviour 引用变成 Missing。

### 真实 Unity 预制体格式
```
m_Script: {fileID: 11500000, guid: 5f7201a12d95ffc409449d95f23cf332, type: 3}
```
- fileID = `11500000`（ClassID 115 × 100000，MonoScript 固定导出 ID）
- guid = 该脚本 `.cs.meta` 文件中的 guid（每个 .cs 文件独立）

### 当前 AssetRipper 导出（错误）
```
m_Script: {fileID: 1980459831, guid: 78cafab24b9b4f7ed8449d81ba10b912, type: 3}
```
- fileID = `CalculateScriptFileID(namespace, class)`（MD4 哈希，错误）
- guid = `CalculateAssemblyGuid(assemblyName)`（MD5，错误）或 `.asmdef.meta` GUID（也错误）

### 验证数据
- `Text.cs.meta`：guid = `5f7201a12d95ffc409449d95f23cf332`，namespace=`UnityEngine.UI`，class=`Text`
- `Image.cs.meta`：guid = `fe87c0e1cc204ed48ad3b37840f39efc`
- `RawImage.cs.meta`：guid = `1344c3c82d62a2a41a3576d8abb8e3ea`
- AssetRipper 导出的 MainScene.prefab 中对应引用的 guid 全部不匹配

## What Changes
- 修改采集工具，除了采集 `.asmdef` + `.asmdef.meta` + `package.json` 外，**新增扫描所有 `.cs` + `.cs.meta`**，解析每个 .cs 文件中的命名空间和类型声明，建立 `(程序集名, 命名空间, 类名) → .cs.meta GUID` 映射。
- 生成的 `UnityPackageAssemblyMap.generated.cs` 新增第二个字典 `s_scriptGuidMap` 与 `TryGetScriptGuid(assemblyName, namespace, className, out UnityGuid)` 方法。
- 修改 `ScriptExporter.CreateExportPointer`：对 UPM 程序集脚本，fileID 用 `MonoScriptDecompiledFileID`（11500000），guid 用 `TryGetScriptGuid` 返回的真实 .cs.meta GUID；查不到时回退到 `ResolveSkipGuid`（.asmdef GUID）作为兜底。
- 保留现有 `UnityPackageAssemblyMap.TryGetInfo`（程序集级映射，用于 `GetExportType` 判定 Skip 和 manifest.json 写包依赖）。
- 保留现有 `.asmdef` 采集逻辑不变。

## Impact
- Affected specs: `skip-upm-package-assemblies`（修正其 CreateExportPointer 逻辑）
- Affected code:
  - [Program.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Tools.UnityPackageGuidCollector/Program.cs) — 新增 .cs 文件扫描与解析
  - [UnityPackageAssemblyMap.generated.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/UnityPackageAssemblyMap.generated.cs) — 重新生成，含脚本级 GUID 字典
  - [ScriptExporter.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Export.UnityProjects/Scripts/ScriptExporter.cs) — 修改 CreateExportPointer 的 UPM 分支
  - 测试文件更新

## ADDED Requirements

### Requirement: UPM 脚本级 GUID 映射表
系统 SHALL 提供一份 `(程序集名, 命名空间, 类名) → .cs.meta GUID` 映射表，数据来源于 UPM 包目录中的 `.cs` + `.cs.meta` 文件。

#### Scenario: 采集脚本 GUID
- **WHEN** 采集工具扫描 UPM 包目录
- **THEN** 对每个 `.cs` 文件，读取同名 `.cs.meta` 取 `guid` 字段
- **AND** 解析 .cs 文件提取命名空间和所有顶层类型声明（class/struct/interface/enum）
- **AND** 通过所在目录的 `.asmdef` 确定程序集名
- **AND** 输出 `Dictionary<(string assembly, string @namespace, string className), UnityGuid>` 到生成的 .cs 文件

#### Scenario: 一文件多类型
- **GIVEN** 一个 .cs 文件声明了多个类型（如 `ColorBlock.cs` 含 `ColorBlock` 结构体）
- **WHEN** 解析时
- **THEN** 每个类型都映射到同一个 .cs.meta GUID

#### Scenario: 全局命名空间
- **GIVEN** 某个 .cs 文件没有 namespace 声明
- **WHEN** 解析时
- **THEN** 命名空间记为空字符串 `""`

### Requirement: CreateExportPointer 使用真实 .cs.meta GUID
系统 SHALL 在 `CreateExportPointer` 中对 UPM 程序集脚本使用 `fileID=11500000` + 真实 `.cs.meta` GUID。

#### Scenario: 命中脚本映射
- **GIVEN** MonoScript 的 AssemblyName=`UnityEngine.UI`，Namespace=`UnityEngine.UI`，ClassName=`Text`
- **WHEN** 调用 CreateExportPointer
- **THEN** 返回的 MetaPtr 的 fileID = `11500000`
- **AND** guid = `5f7201a12d95ffc409449d95f23cf332`（Text.cs.meta 的真实 GUID）

#### Scenario: 未命中脚本映射的兜底
- **GIVEN** MonoScript 的程序集命中 UPM 映射，但具体脚本未命中 `TryGetScriptGuid`
- **WHEN** 调用 CreateExportPointer
- **THEN** 回退使用 `ResolveSkipGuid`（.asmdef GUID）+ `CalculateScriptFileID`
- **AND** 输出警告日志（便于后续补充映射）

## MODIFIED Requirements

### Requirement: ScriptExporter.CreateExportPointer
修改前（当前 skip-upm-package-assemblies spec 的实现）：
```csharp
AssemblyExportType.Skip => new(
    ScriptHashing.CalculateScriptFileID(script),
    ResolveSkipGuid(script.GetAssemblyNameFixed()),
    AssetType.Meta
),
```

修改后：
```csharp
AssemblyExportType.Skip => CreateUpmScriptPointer(script),
```
新增私有方法 `CreateUpmScriptPointer`：
1. 优先查 `UnityPackageAssemblyMap.TryGetScriptGuid(asm, ns, class, out guid)`
   - 命中：返回 `new(MonoScriptDecompiledFileID, guid, AssetType.Meta)`
2. 未命中：回退到 `new(CalculateScriptFileID(script), ResolveSkipGuid(asm), AssetType.Meta)` + 警告日志

### Requirement: 采集工具输出格式
修改前：只输出 `s_map`（程序集级字典）和 `TryGetInfo`。
修改后：额外输出 `s_scriptGuidMap`（脚本级字典）和 `TryGetScriptGuid`。

## REMOVED Requirements
无。
