# Tasks

* [ ] Task 1: 定义 GameType 枚举并接入 ImportSettings
  * [ ] 在 `AssetRipper.Import` 中新增 `GameType` 枚举(`Generic`、`Nikki4`),放置于 `Configuration` 命名空间(与 `ScriptContentLevel` 等同级)

  * [ ] 在 [ImportSettings.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Configuration/ImportSettings.cs) 添加 `GameType` 属性(默认 `Generic`),并在 `Log()` 中输出

* [ ] Task 2: 重构游戏专属资产解析架构
  * [ ] 新增 `AssetCreation/IGameAssetProvider.cs` 接口:`IUnityObjectBase? TryCreateAsset(AssetInfo assetInfo, UnityVersion version)`

  * [ ] 新增 `AssetCreation/GameAssetProviderRegistry.cs` 静态注册表:`GameType → IGameAssetProvider` 字典 + `GetProvider(GameType)` 查询(未注册返回 null)

  * [ ] 新增 `AssetCreation/Nikki4/Nikki4GameAssetProvider.cs`,将 [GameAssetFactory.cs:227](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/AssetCreation/GameAssetFactory.cs#L227) 中 Nikki4 的 12 个 case 迁移为 `TryCreateAsset` 实现

  * [ ] 重构 `GameAssetFactory`:构造函数新增 `GameType` 参数并持有对应 provider;`CreateAsset`/`ReadNormalObject`/`TryReadNormalObject` 去静态化后先查询 provider,未命中走原有默认逻辑(含版本回退与 TPK 回退)

  * [ ] 保留 `CreateEngineAsset` 静态方法与现有错误处理/调试保存逻辑不动

* [ ] Task 3: 更新调用方
  * [ ] [GameStructure.cs:80](file:///d:/Project/AssetRipper/Source/AssetRipper.Import/Structure/GameStructure.cs#L80):`InitializeGameCollection` 使用 `configuration.ImportSettings.GameType` 构造工厂(需将 configuration 传入该方法或调整取值路径)

  * [ ] [Program.cs:88](file:///d:/Project/AssetRipper/Source/AssetRipper.Tools.DependenceGrapher/Program.cs#L88):`DependenceGrapher` 以 `GameType.Generic` 构造工厂

* [ ] Task 4: GUI 设置页面支持
  * [ ] 新增 `GameTypeDropDownSetting.cs`(GUI.Web/Pages/Settings/DropDown),显示名与描述走 Localization

  * [ ] 在 [SettingsPage.cs](file:///d:/Project/AssetRipper/Source/AssetRipper.GUI.Web/Pages/Settings/SettingsPage.cs) 导入区域添加 `WriteDropDownForGameType(writer)`

  * [ ] 运行 `AssetRipper.GUI.SourceGenerator` 控制台程序重新生成 `SettingsPage.g.cs`(或按相同格式手工补齐 `SetProperty` 分支与 `WriteDropDownForGameType` 方法)

  * [ ] 在 `Localizations/en_US.json` 与 `Localizations/zh_Hans.json` 添加 `GameType` 标题/描述/选项文案

* [ ] Task 5: 构建验证
  * [ ] 构建 `AssetRipper.Import`、`AssetRipper.GUI.Web`、`AssetRipper.Tools.DependenceGrapher` 确认无编译错误

  * [ ] 全解决方案构建通过

# Task Dependencies

* Task 2 依赖 Task 1(枚举)

* Task 3 依赖 Task 2(工厂签名)

* Task 4 依赖 Task 1(枚举属性名),与 Task 2/3 可并行

* Task 5 依赖全部任务完成

