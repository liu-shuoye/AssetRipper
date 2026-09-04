# Checklist

- [x] `ImportSettings.Il2CppDumpPath`（string?，默认 null）已添加，`Log()` 输出该设置，中文注释说明用途
- [x] `Il2CppDumpManager` 继承 `BaseManager`，`ScriptingBackend` 返回 `IL2Cpp`
- [x] `TryGetAssemblyDirectory`：路径含 `DummyDll` 子目录时返回子目录；不含时若目录本身有 `.dll` 则返回该目录；均无则返回 false（冒烟测试验证了三种分支）
- [x] `Il2CppDumpManager.Initialize`：优先加载 dump 中的 mscorlib，缺失时用系统运行时重建（同 `MonoManager` 模式），并据此创建 `RuntimeContext` 注册全部程序集
- [x] 单个程序集加载失败时记录警告并跳过，不中断整体加载；完成后记录程序集数量
- [x] `GameStructure.InitializeAssemblyManager`：IL2Cpp 分支在 `Il2CppDumpPath` 有效时使用 `Il2CppDumpManager`；路径无效时记录警告并回退 `IL2CppManager`；未配置时行为与现状完全一致
- [x] Mono/Unknown 后端与 `DisableScriptImport` 路径不受影响；`Initialize` 失败回退 `BaseManager` 的现有逻辑未改动
- [x] `en_US.json` 与 `zh_Hans.json` 新增 `il2cpp_dump_path` 键，其余语言可缺省
- [x] 设置页导入区块出现"IL2Cpp dump 目录"输入框（含选择文件夹按钮），保存后 `ImportSettings.Il2CppDumpPath` 正确更新并持久化（经现有 SetProperty/SaveSettingsToDisk 机制）
- [x] `SettingsPage.g.cs` 已包含 `Il2CppDumpPath` 的 `SetProperty` case（string 赋值）
- [x] `docs/articles/CommonIssues.md` 已补充外部 dump 方案说明
- [x] `AssetRipper.Import` 与 `AssetRipper.GUI.Web` 构建零错误
- [x] 场景验证：配置含 `DummyDll` 的输出根目录（`C:\Unity\output`，106 个程序集）后加载 IL2Cpp 游戏，程序集数量 > 0 且全部加载成功（Assembly-CSharp/mscorlib/UnityEngine.CoreModule/System 均在列），不调用 Cpp2IL（`Il2CppDumpManager.Initialize` 不涉及 Cpp2IlApi）
- [x] dump 特性清理覆盖 `Il2CppDummyDll` 命名空间全部特性（Token/FieldOffset/Address），真实 DummyDll 加载后字段与方法残留均为 0
- [x] 接口方法完整性已用真实 DummyDll 验证：13175 项接口方法需求缺失 0；ilspy 反编译确认显式/隐式接口实现均可见
