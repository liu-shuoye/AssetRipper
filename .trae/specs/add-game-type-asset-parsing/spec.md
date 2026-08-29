# 游戏类型枚举与游戏专属资产解析架构 Spec

## Why
当前 `GameAssetFactory.CreateAsset`([GameAssetFactory.cs:227](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/AssetCreation/GameAssetFactory.cs#L227))中硬编码了对 Nikki4(无限暖暖)游戏的 12 种资产类型的特殊解析类,且**无条件对所有游戏生效**。这导致其他游戏的同类资产也被 Nikki4 类解析,属于隐性缺陷;同时缺少"游戏类型"概念,无法按游戏选择解析方式,新增游戏特殊处理需要修改工厂主逻辑,不易扩展。

## What Changes
- 新增 `GameType` 枚举(`Generic`/`Nikki4`),添加到 `ImportSettings` 中持久化,并在设置页面提供下拉选择。
- **BREAKING**:`GameAssetFactory` 构造函数新增 `GameType` 参数;`CreateAsset`/`ReadNormalObject`/`TryReadNormalObject` 由静态方法改为实例方法,以携带游戏专属提供者。
- 新增 `IGameAssetProvider` 接口与 `GameAssetProviderRegistry` 注册表,Nikki4 的特殊 switch 逻辑迁移至 `Nikki4GameAssetProvider`。
- `GameStructure` 创建工厂时从配置传入 `GameType`;`DependenceGrapher` 工具同步更新调用。
- GUI:新增 `GameTypeDropDownSetting`、设置页面下拉框、重新生成 `SettingsPage.g.cs`、补充本地化文案。

## Impact
- Affected specs: 无
- Affected code:
  - [ImportSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Configuration/ImportSettings.cs) — 新增 `GameType` 属性
  - [GameAssetFactory.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/AssetCreation/GameAssetFactory.cs) — 重构解析分发
  - `AssetCreation/IGameAssetProvider.cs`、`AssetCreation/GameAssetProviderRegistry.cs`(新增)
  - `AssetCreation/Nikki4/Nikki4GameAssetProvider.cs`(新增)
  - [GameStructure.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Structure/GameStructure.cs#L80) — 传入 GameType
  - [Program.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Tools.DependenceGrapher/Program.cs#L88) — 更新构造调用
  - `SettingsPage.cs` / `SettingsPage.g.cs` / `GameTypeDropDownSetting.cs`(GUI.Web)
  - `Localizations/en_US.json`、`Localizations/zh_Hans.json`

## ADDED Requirements

### Requirement: GameType 枚举
系统 SHALL 提供 `GameType` 枚举,包含 `Generic`(默认,通用 Unity 解析)与 `Nikki4` 两个值,定义于 `AssetRipper.Import` 项目中。

#### Scenario: 默认值
- **WHEN** 用户未修改设置加载任意游戏
- **THEN** `GameType` 为 `Generic`,所有资产走通用解析逻辑,不使用 Nikki4 特殊类

### Requirement: 游戏专属资产提供者注册表
系统 SHALL 提供 `IGameAssetProvider` 接口(`TryCreateAsset(AssetInfo, UnityVersion)` 返回 `IUnityObjectBase?`,返回 null 表示回退默认解析)与静态注册表 `GameAssetProviderRegistry`,按 `GameType` 查找提供者。新增游戏支持只需:添加枚举值、实现接口、在注册表注册一行。

#### Scenario: Nikki4 特殊资产
- **WHEN** `GameType` 为 `Nikki4` 且资产为 AnimationClip/Material/Shader/SkinnedMeshRenderer/Mesh/AnimatorController/ParticleSystem/ParticleSystemRenderer/TrailRenderer/SpriteRenderer/VisualEffect 之一
- **THEN** 使用对应 `*_Nikki4` 类创建实例

#### Scenario: 未注册类型回退
- **WHEN** 提供者返回 null 或 `GameType` 无注册提供者(如 `Generic`)
- **THEN** 走原有 `AssetFactory.CreateSerialized` + TPK 类型树回退逻辑

### Requirement: 设置页面游戏类型选择
设置页面 SHALL 在"导入"区域提供 `GameType` 下拉框;由于资产解析发生在加载阶段,该设置仅在未加载文件时可修改(沿用现有页面行为)。设置 SHALL 随 `ImportSettings` 序列化到磁盘。

#### Scenario: 选择游戏类型
- **WHEN** 用户在设置页选择 `Nikki4` 并保存,然后加载游戏文件
- **THEN** `GameStructure` 以 `GameType.Nikki4` 构造 `GameAssetFactory`,Nikki4 特殊解析生效

## MODIFIED Requirements

### Requirement: GameAssetFactory 资产创建
工厂 SHALL 依据构造时传入的 `GameType` 先查询注册表中的提供者;提供者未命中时使用通用逻辑。原有版本回退(Patch 版本 +1 重试)、TPK 类型树回退、中文版 Texture2D 额外 24 字节处理等行为保持不变。

## REMOVED Requirements
无
