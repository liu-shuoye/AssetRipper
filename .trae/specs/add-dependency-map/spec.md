# 依赖关系文件生成与加载（Dependency Map）Spec

## Why

用户只打开游戏的一个子文件夹时，文件依赖的其他文件（如 `sharedassets0.assets`、其他 bundle）不在打开的文件夹内，`StructureDependencyProvider.FindDependency` 查找失败，导致依赖缺失、资产不完整。需要预先扫描完整游戏文件夹生成「依赖名 → 绝对路径」的依赖关系文件，加载时据此把文件夹外的依赖文件也加载进来。

## What Changes

- 新增 **DependencyMap** 模型：依赖关系文件的加载/保存/查询（JSON 格式，AOT 友好源生成序列化）
- 新增 **DependencyMapScanner**：扫描一个文件夹，解析所有可识别文件，生成依赖关系文件
- **ImportSettings** 新增设置：`LoadDependencyMap`（是否加载依赖关系文件）与 `DependencyMapPath`（文件路径）
- **StructureDependencyProvider.FindDependency** 增加后备查找：现有结构查找失败时查询 DependencyMap，找到则加载文件夹外的文件；`GameBundle.FromPaths.cs:90` 的现有递归依赖加载机制（`LoadDependencies` → `FindDependency`）自然获得"加载文件夹外依赖"的能力，无需修改 `GameBundle.FromPaths.cs`
- **Web GUI** 新增命令 `/Commands/GenerateDependencyMap`（扫描文件夹生成依赖关系文件）及命令页表单入口
- 设置页新增复选框（启用加载）与路径输入框（依赖关系文件路径，带"选择文件"按钮）

## Impact

- Affected code:
  - `Source/AssetRipper.Import/Structure/DependencyMap.cs`（新增）
  - `Source/AssetRipper.Import/Structure/DependencyMapScanner.cs`（新增）
  - `Source/AssetRipper.Import/Configuration/ImportSettings.cs`（新增 2 个设置项）
  - `Source/AssetRipper.Import/Structure/GameInitializer.cs`（构造函数增加 DependencyMap 参数）
  - `Source/AssetRipper.Import/Structure/GameInitializer.StructureDependencyProvider.cs`（后备查找）
  - `Source/AssetRipper.Import/Structure/GameStructure.cs`（按设置加载 DependencyMap 并传递）
  - `Source/AssetRipper.GUI.Web/Pages/Commands.cs`（新增 GenerateDependencyMap 命令）
  - `Source/AssetRipper.GUI.Web/Pages/CommandsPage.cs`（扫描表单）
  - `Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs`（设置 UI）+ `SettingsPage.g.cs`（重新生成）
  - `Source/AssetRipper.GUI.Web/WebApplicationLauncher.cs`（路由映射）
  - `Localizations/en_US.json`、`Localizations/zh_Hans.json`（本地化字符串）

## ADDED Requirements

### Requirement: 依赖关系文件模型（DependencyMap）
系统 SHALL 提供依赖关系文件的加载、查询与保存能力。

- 文件格式为 JSON：`{"Version":1,"Entries":{"<名称>":"<绝对路径>", ...}}`
- 名称键为小写规范化字符串；加载后可通过 `TryResolve(name)` 查询对应的绝对文件路径
- 使用 `JsonSourceGenerationOptions` 源生成序列化（AOT 兼容，参照 `LastExportSettingsContext` 模式）

#### Scenario: 加载并查询
- **WHEN** 调用 `DependencyMap.Load(path)` 且文件存在且格式合法
- **THEN** 返回非空实例，且 `TryResolve("sharedassets0.assets")` 返回扫描时记录的绝对路径
- **WHEN** 文件不存在或格式非法
- **THEN** `Load` 返回 null 并记录警告日志，不抛出异常

### Requirement: 扫描文件夹生成依赖关系文件
系统 SHALL 提供扫描指定文件夹并生成依赖关系文件的能力（`DependencyMapScanner`）。

- 递归枚举文件夹内所有文件，逐个用 `SchemeReader.LoadFile` 加载（失败文件跳过并记录警告）
- 对每个成功加载的文件，通过 `ReadContentsRecursively` + `FetchSerializedFiles` 展开内容，记录以下名称键到该文件绝对路径的映射：
  1. 文件名（小写，含扩展名），如 `sharedassets0.assets`
  2. 文件名不含扩展名（小写，用于 bundle，模仿 `AddAssetBundle` 命名），如 `data`
  3. 文件内部每个 SerializedFile 的 `NameFixed`（如 `cab-xxxx`），支持 bundle 内文件依赖名
  4. 相对扫描根目录的路径（正斜杠、小写），如 `gamedata/sharedassets0.assets`
- 每个文件处理完后立即 `Dispose`，控制内存峰值
- 输出路径参数可选，默认为 `<扫描文件夹>/AssetRipper.DependencyMap.json`
- 扫描完成后输出统计日志（成功/失败文件数、映射条目数）

#### Scenario: 扫描典型游戏文件夹
- **WHEN** 对包含 `Game_Data`（globalgamemanagers、sharedassets*.assets、data.unity3d）的文件夹执行扫描
- **THEN** 生成的依赖关系文件包含 `globalgamemanagers`、`sharedassets0.assets`、`data`（bundle 名）及 bundle 内 SerializedFile 的 `cab-*` 名称到对应绝对路径的映射

#### Scenario: 遇到无法识别的文件
- **WHEN** 文件夹中存在非资产文件（如 README.txt）
- **THEN** 跳过该文件并记录警告，扫描继续，不中断

### Requirement: 设置开关
系统 SHALL 在导入设置中提供"是否加载依赖关系文件"开关及其文件路径配置。

- `ImportSettings.LoadDependencyMap`（bool，默认 `false`）
- `ImportSettings.DependencyMapPath`（string?，默认 `null`）
- 设置页（导入分区）显示复选框"加载依赖关系文件（Load Dependency Map）"与路径输入框；路径框带"选择文件"浏览按钮
- 设置持久化遵循现有 `SaveSettingsToDisk` 机制

#### Scenario: 默认关闭
- **WHEN** 用户未修改设置
- **THEN** `LoadDependencyMap` 为 false，加载行为与现状完全一致

### Requirement: 加载文件夹外的依赖文件
当启用"加载依赖关系文件"且依赖关系文件存在时，系统 SHALL 在 `GameBundle.FromPaths.cs:90`（`LoadFilesAndDependencies` → `LoadDependencies`）加载文件时，将文件依赖的其他文件也加载，即使文件不在打开的文件夹内。

- `GameStructure` 构造时按设置加载 `DependencyMap` 并传递给 `GameInitializer` → `StructureDependencyProvider`
- `StructureDependencyProvider.FindDependency` 查找顺序：
  1. 现有结构查找（`PlatformStructure`/`MixedStructure.RequestDependency`）——保持现状优先级
  2. 失败后备：`DependencyMap.TryResolve(identifier.PathName)` → 命中则 `SchemeReader.LoadFile` 加载该绝对路径文件
- 通过依赖关系文件加载的文件进入 `files` 列表后，其自身依赖由现有 for 循环递归处理（传递闭包）
- **BREAKING**：无（开关默认关闭）

#### Scenario: 加载子文件夹时补全外部依赖
- **GIVEN** 已扫描完整游戏文件夹生成依赖关系文件，并开启"加载依赖关系文件"
- **WHEN** 用户只打开游戏的某个子文件夹（不含 `sharedassets0.assets`），其中文件依赖 `sharedassets0.assets`
- **THEN** `FindDependency` 通过依赖关系文件定位到绝对路径并加载该文件，依赖解析成功
- **AND** 该文件自身依赖的其他文件夹外文件也被递归加载

#### Scenario: 依赖关系文件路径指向的文件已被移动
- **WHEN** `TryResolve` 命中但磁盘上该路径不存在
- **THEN** `SchemeReader.LoadFile` 抛出的异常被捕获（现有 FailedFile 机制或记录警告），加载流程不中断

### Requirement: GUI 扫描命令
系统 SHALL 在 Web GUI 命令页提供"扫描依赖关系"入口。

- 命令页（未加载文件时）显示表单：扫描文件夹路径输入 + 提交按钮
- 后端路由 `POST /Commands/GenerateDependencyMap`，处理 `Path`（扫描文件夹）与可选 `OutputPath`（输出文件路径）表单字段
- 扫描在后台执行并输出进度/结果日志，完成后重定向回命令页

#### Scenario: 从命令页发起扫描
- **WHEN** 用户在命令页输入文件夹路径并提交
- **THEN** 生成 `AssetRipper.DependencyMap.json`（或用户指定的输出路径），日志显示扫描统计

## MODIFIED Requirements

（无——本变更全部为新增能力，现有行为在开关默认关闭时保持不变）
