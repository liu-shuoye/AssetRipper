# 支持加载外部 IL2Cpp Dump 程序集 Spec

## Why

当前 `GameStructure.InitializeAssemblyManager` 对 IL2Cpp 游戏固定使用 Cpp2IL（`AssetRipper.Cpp2IL.Core`）解析 `GameAssembly` 与 `global-metadata.dat`。对元数据被加密或二进制被加固（壳保护）的游戏，Cpp2IL 解析失败后仅能回退到"未知"脚本后端，导致所有脚本类型信息（MonoBehaviour 字段等）丢失。

**方案论证（对比用户提出的三种方向）：**

| 方案 | 结论 | 原因 |
|------|------|------|
| A. 用 Il2CppDumper 完全替换 Cpp2IL | 不采用 | Il2CppDumper 对加密游戏同样无解（其官方说明：反混淆超出程序范围，需内存 dump 或定制 fork）；原版仅支持 Unity 5.3–2022.2；且会丢失 Cpp2IL 分支的 processing layers、`ScriptContentLevel` 等深度集成，改动大、风险高 |
| B. 支持加载 Il2CppDumper 导出的 DummyDll | **采用** | DummyDll 是标准 .NET 托管程序集，AsmResolver 可直接加载；用户可在游戏外部用任意工具（带解密的 Il2CppDumper fork、Zygisk-Il2CppDumper 内存 dump 等）生成；改动小、风险低，且保留 Cpp2IL 作为默认路径 |
| C. 更好的方案 | 即 B + 回退机制 | 路径无效时记录警告并回退 Cpp2IL；未配置时行为完全不变 |

## What Changes

- `ImportSettings` 新增设置 `Il2CppDumpPath`（string?，默认 `null`）：指向 Il2CppDumper 的输出目录（如 `C:\Unity\output`，自动识别其中的 `DummyDll` 子目录），或直接指向包含 dump 程序集的目录
- 新增 `Il2CppDumpManager`（`ScriptingBackend.IL2Cpp`）：从外部目录加载全部托管程序集，替代 Cpp2IL 解析流程
- `GameStructure.InitializeAssemblyManager` 的 IL2Cpp 分支：配置了有效 dump 路径时创建 `Il2CppDumpManager`，否则维持现有 `IL2CppManager`（Cpp2IL）
- Web GUI 设置页新增"IL2Cpp dump 目录"文本输入框（附带"选择文件夹"按钮），含中英文本地化
- 文档：`docs/articles/CommonIssues.md` 中 IL2Cpp 相关条目补充新的替代方案说明

**Non-goals（明确不做）：**
- 不替换/不移除 Cpp2IL 集成，默认行为不变
- 不在 AssetRipper 内部调用 Il2CppDumper 进程或集成其库
- 不实现任何解密/脱壳逻辑（由用户在外部工具完成）
- dump 模式下忽略 `ScriptContentLevel`（内容由外部工具决定）

## Impact

- Affected specs: 无（新增能力，不修改已有 spec）
- Affected code:
  - `Source/AssetRipper.Import/Configuration/ImportSettings.cs`（新增属性）
  - `Source/AssetRipper.Import/Structure/Assembly/Managers/Il2CppDumpManager.cs`（新文件）
  - `Source/AssetRipper.Import/Structure/GameStructure.cs`（`InitializeAssemblyManager` 分支调整）
  - `Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs`（新增输入框）
  - `Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.g.cs`（由 SourceGenerator 重新生成）
  - `Localizations/en_US.json`、`Localizations/zh_Hans.json`（新增键）
  - `docs/articles/CommonIssues.md`（补充说明）

## ADDED Requirements

### Requirement: 外部 IL2Cpp Dump 程序集加载

当导入设置 `Il2CppDumpPath` 指向有效目录且游戏为 IL2Cpp 后端时，系统 SHALL 通过新的 `Il2CppDumpManager` 加载该目录下的全部托管程序集（`.dll`），跳过加载失败的文件并记录日志，而非调用 Cpp2IL 解析。

#### Scenario: 路径指向 Il2CppDumper 输出根目录（含 DummyDll 子目录）
- **WHEN** 用户将 `Il2CppDumpPath` 设为 `C:\Unity\output`，且该目录下存在 `DummyDll` 子目录（内含 `.dll` 文件）
- **THEN** 系统加载 `C:\Unity\output\DummyDll` 下的全部程序集，日志记录所用的实际目录与程序集数量，不调用 Cpp2IL

#### Scenario: 路径直接指向程序集目录
- **WHEN** 用户将 `Il2CppDumpPath` 设为不含 `DummyDll` 子目录、但本身包含 `.dll` 文件的目录
- **THEN** 系统加载该目录下的全部程序集

#### Scenario: 路径无效时回退 Cpp2IL
- **WHEN** `Il2CppDumpPath` 已配置但目录不存在、或目录（及其 `DummyDll` 子目录）中没有任何 `.dll` 文件
- **THEN** 系统记录警告日志，回退到现有 Cpp2IL 流程（`IL2CppManager`）

#### Scenario: 未配置时行为不变
- **WHEN** `Il2CppDumpPath` 为 `null` 或空白
- **THEN** IL2Cpp 后端行为与现状完全一致（使用 Cpp2IL）

#### Scenario: 加载的 dump 程序集可参与脚本解析
- **WHEN** dump 程序集加载完成
- **THEN** 跨程序集类型引用可解析（参考 `MonoManager` 模式：以 mscorlib 建立 `RuntimeContext` 并注册全部程序集），MonoBehaviour/MonoScript 的类型与字段解析与 Mono 流程一致

#### Scenario: 非 IL2Cpp 后端不受影响
- **WHEN** 游戏为 Mono 后端或脚本导入被禁用
- **THEN** `Il2CppDumpPath` 不产生任何影响

### Requirement: Web GUI 设置项

系统 SHALL 在设置页（导入区块）提供"IL2Cpp dump 目录"文本输入框，附带"选择文件夹"按钮（复用 `browseForFolder` JS），标签支持中英文，提交后随其他设置持久化。

#### Scenario: 修改并保存设置
- **WHEN** 用户在设置页输入（或通过按钮选择）dump 目录并保存
- **THEN** `ImportSettings.Il2CppDumpPath` 更新并按现有机制持久化到磁盘

## MODIFIED Requirements

### Requirement: IL2Cpp 程序集管理器的选择逻辑（GameStructure.InitializeAssemblyManager）

IL2Cpp 后端的 `AssemblyManager` 构造逻辑由"固定创建 `IL2CppManager`"改为：优先根据 `ImportSettings.Il2CppDumpPath` 创建 `Il2CppDumpManager`（路径有效时），否则创建 `IL2CppManager`。`IL2CppManager` 及其余后端（Mono/Unknown）的选择逻辑保持不变；`AssemblyManager.Initialize` 失败时回退 `BaseManager` 的现有兜底逻辑保持不变。
