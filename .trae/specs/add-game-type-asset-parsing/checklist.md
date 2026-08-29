# Checklist

- [x] `GameType` 枚举定义于 AssetRipper.Import,包含 `Generic` 与 `Nikki4`,默认值为 `Generic`
- [x] `ImportSettings.GameType` 属性可序列化到磁盘并在 `Log()` 中输出
- [x] `IGameAssetProvider` 接口与 `GameAssetProviderRegistry` 注册表存在,按 `GameType` 查询提供者,未注册返回 null
- [x] Nikki4 的 12 种资产特殊创建逻辑已从 `GameAssetFactory.CreateAsset` 迁移至 `Nikki4GameAssetProvider.TryCreateAsset`(注:原代码实际为 11 个 case,已全部迁移,行为与原逻辑一致)
- [x] `GameAssetFactory` 构造函数接受 `GameType`;`CreateAsset` 先查询 provider,未命中走原有默认逻辑(`AssetFactory.CreateSerialized` + TPK 回退)
- [x] 原有行为不回归:版本回退重试、中文版 Texture2D 额外 24 字节、MonoBehaviour 解析、错误日志与调试数据保存逻辑保持不变
- [x] `GameType` 为 `Generic` 时,不使用任何 `*_Nikki4` 类解析资产(修复原硬编码对所有游戏生效的问题)
- [x] `GameStructure` 从配置读取 `GameType` 并传入工厂;`DependenceGrapher` 编译通过
- [x] 设置页面显示 GameType 下拉框,保存后可通过 POST `/Settings/Update` 更新,且仅未加载文件时可修改
- [x] `en_US.json` 与 `zh_Hans.json` 含 GameType 相关文案
- [x] 全解决方案构建通过,无编译错误(0 错误;CS8603 警告已通过修正 `GetDescription` 签名为 `string?` 消除,其余警告均为仓库既有)
