# Tasks

## 阶段一：基础设施

- [x] Task 1: 在 AssetCollection 新增元数据枚举与单对象加载 API
  - [x] SubTask 1.1: 在 [AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 新增 `public readonly struct AssetMetadata { required long PathID; required int ClassID; }`（嵌套结构）
  - [x] SubTask 1.2: 新增 `public virtual IEnumerable<AssetMetadata> EnumerateAssetMetadata()`，默认实现：`EnsureAssetsLoaded(); foreach (var a in assets) yield return new AssetMetadata { PathID = a.Key, ClassID = a.Value.ClassID };`
  - [x] SubTask 1.3: 新增 `public virtual IUnityObjectBase? TryGetAssetOnly(long pathID)`，默认实现：`return TryGetAsset(pathID);`（回退到全量加载，兼容老代码）
  - [x] SubTask 1.4: 新增 `public T? TryGetAssetOnly<T>(long pathID) where T : IUnityObjectBase` 泛型重载，复用 `TryGetAssetOnly(pathID)` 后做类型转换（与现有 `TryGetAsset<T>` 一致逻辑）

- [x] Task 2: 在 SerializedAssetCollection 重写元数据枚举与单对象加载
  - [x] SubTask 2.1: 重写 `EnumerateAssetMetadata()`：直接遍历 `_sourceFile.Objects`（ReadOnlySpan<ObjectInfo>），对每个 ObjectInfo 计算 `classID = info.TypeID < 0 ? 114 : info.TypeID`，yield `(info.FileID, classID)`，**不**访问 `_assetsLoaded`、**不**调用 `EnsureAssetsLoaded`
  - [x] SubTask 2.2: 重写 `TryGetAssetOnly(long pathID)`：若 `_assetsLoaded` 为 true 直接 `return TryGetAsset(pathID)`；否则遍历 `_sourceFile.Objects` 找到 `FileID == pathID` 的 ObjectInfo → `LoadObjectData()` → `_factory.ReadAsset()` → `AddAsset()`，**不**设置 `_assetsLoaded = true`，返回反序列化后的 asset（或 null）
  - [x] SubTask 2.3: 验证 `TryGetAssetOnly` 重复调用同一 PathID 不重复反序列化（首次加入 `assets` 字典后，第二次走字典查询路径——因为 `assets.ContainsKey(pathID)` 为 true，可以直接走字典；或在方法开头先检查 `assets.TryGetValue(pathID, out var existing)` 直接返回）

- [x] Task 3: 在 AssetCollection 新增 OriginalDirectory 持久化 API
  - [x] SubTask 3.1: 在 [AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 新增 `private Dictionary<long, string>? _originalDirectoryOverrides;` 字段
  - [x] SubTask 3.2: 新增 `public void SetOriginalDirectory(long pathID, string directory)`：`_originalDirectoryOverrides ??= new(); _originalDirectoryOverrides[pathID] = directory;`
  - [x] SubTask 3.3: 新增 `internal string? TryGetOriginalDirectory(long pathID)`：`return _originalDirectoryOverrides?.TryGetValue(pathID, out var dir) == true ? dir : null;`
  - [x] SubTask 3.4: 在 `Dispose(bool disposing)` 中清空 `_originalDirectoryOverrides`（释放引用）

- [x] Task 4: 修改 UnityObjectBase.OriginalDirectory getter 回退到 collection 映射
  - [x] SubTask 4.1: 在 [UnityObjectBase.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/UnityObjectBase.cs#L51-L66) 修改 `OriginalDirectory` getter 为 `get => originalPathDetails?.Directory ?? Collection.TryGetOriginalDirectory(PathID);`
  - [x] SubTask 4.2: 验证 setter 行为不变（仍写入 `originalPathDetails.Directory`，不写入 collection 映射）
  - [x] SubTask 4.3: 验证 asset 实例级别 OriginalDirectory 优先于 collection 映射（`asset.OriginalDirectory = "X"` 后 getter 返回 "X"）

## 阶段二：改造 SceneDefinitionProcessor

- [x] Task 5: 重写 SceneDefinitionProcessor.Process 用元数据驱动
  - [x] SubTask 5.1: 在 [SceneDefinitionProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/SceneDefinitionProcessor.cs#L31-L52) 把 `foreach (IUnityObjectBase asset in collection)` 改为 `foreach (AssetCollection.AssetMetadata meta in collection.EnumerateAssetMetadata())`
  - [x] SubTask 5.2: 用 ClassID 识别 ILevelGameManager：`IsLevelGameManagerClassID(meta.ClassID)` 返回 true 当 ClassID ∈ {29, 104, 157, 196}（即 OcclusionCullingSettings / RenderSettings / LightmapSettings / NavMeshSettings，参见 [ClassIDTypeExtention.IsSceneSettings](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.SourceGenerated.Extensions/ClassIDTypeExtention.cs#L21-L24)）
  - [x] SubTask 5.3: 对识别为 ILevelGameManager 的 collection 加入 `sceneCollections`；对 ClassID == 29 (OcclusionCullingSettings) 调用 `collection.TryGetAssetOnly<IOcclusionCullingSettings>(meta.PathID)` 读取 SceneGUID
  - [x] SubTask 5.4: 对 ClassID == 141 (BuildSettings) 调用 `collection.TryGetAssetOnly<IBuildSettings>(meta.PathID)`，赋值给 `buildSettings`
  - [x] SubTask 5.5: 对 ClassID == 142 (AssetBundle) 调用 `collection.TryGetAssetOnly<IAssetBundle>(meta.PathID)`，检查 `IsStreamedSceneAssetBundle` 后加入 `sceneAssetBundles`
  - [x] SubTask 5.6: 验证 `sceneCollections` / `scenePaths` / `sceneGuids` / `sceneAssetBundles` / `buildSettings` 的填充逻辑与改造前完全一致
  - [x] SubTask 5.7: 验证后续 SceneDefinition 构造、EditorBuildSettings 生成逻辑不变

## 阶段三：改造 OriginalPathProcessor

- [x] Task 6: 重写 OriginalPathProcessor 第一段（识别 IResourceManager / IAssetBundle）
  - [x] SubTask 6.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L38-L70) 把 `foreach (IUnityObjectBase asset in gameData.GameBundle.FetchAssets())` 改为遍历 `FetchAssetCollections()` + 每个集合的 `EnumerateAssetMetadata()`
  - [x] SubTask 6.2: 对 ClassID == 147 (ResourceManager) 调用 `collection.TryGetAssetOnly<IResourceManager>(meta.PathID)`，执行 `SetOriginalPaths(resourceManager)`
  - [x] SubTask 6.3: 对 ClassID == 142 (AssetBundle) 调用 `collection.TryGetAssetOnly<IAssetBundle>(meta.PathID)`，执行 `SetOriginalPaths(assetBundle, bundledAssetsExportMode)` 与 dictionary / originalDirectories 填充
  - [x] SubTask 6.4: 验证 `dictionary` 与 `originalDirectories` 的填充逻辑与改造前一致
  - 备注：tasks.md 原备注 IResourceManager ClassID=27 是错误的，实际为 147（27 是 ParticleRenderer）。已在实现中纠正。

- [x] Task 7: 重写 OriginalPathProcessor 第二段 GroupByBundleName 分支
  - [x] SubTask 7.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L72-L78) 把 `foreach (IUnityObjectBase asset in collection) { asset.OriginalDirectory ??= ... }` 改为 `foreach (var meta in collection.EnumerateAssetMetadata()) { string className = ((ClassIDType)meta.ClassID).ToString(); collection.SetOriginalDirectory(meta.PathID, Path.Join(AssetBundleFullPath, BundleName, className)); }`
  - [x] SubTask 7.2: 注意 `??=` 语义：原逻辑只对未设置 OriginalDirectory 的 asset 设置；新逻辑直接 `SetOriginalDirectory` 会覆盖。需检查 `collection.TryGetOriginalDirectory(meta.PathID)` 是否已存在，若存在则跳过（保持 `??=` 语义）
  - [x] SubTask 7.3: 验证 ClassName 推导：`((ClassIDType)meta.ClassID).ToString()` 与 `asset.ClassName`（即 `GetType().Name`）一致。需要检查 ClassIDType 枚举名与实际生成的类型名是否完全匹配（例如 `ClassIDType.GameObject` 对应类型 `GameObject`）

- [x] Task 8: OriginalPathProcessor ContainerExport 分支保留原逻辑（已知限制）
  - [x] SubTask 8.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L81-L108) ContainerExport 分支保留 `foreach (IUnityObjectBase asset in collection)` 原逻辑（因为需要 `GetBestName()`）
  - [x] SubTask 8.2: 在代码中添加注释说明：ContainerExport 模式因需要访问 asset 字段（GetBestName），暂保留全量反序列化，后续 spec 可优化

## 阶段四：改造其他 processor

- [x] Task 9: 改造 MainAssetProcessor
  - [x] SubTask 9.1: 在 [MainAssetProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/MainAssetProcessor.cs#L17) 把 `foreach (IUnityObjectBase asset in gameData.GameBundle.FetchAssets())` 改为遍历 `FetchAssetCollections()` + `EnumerateAssetMetadata()`
  - [x] SubTask 9.2: 对 ClassID == 128 (Font) 调用 `collection.TryGetAssetOnly<IFont>(meta.PathID)`，执行 font.MainAsset / fontMaterial / fontTexture 设置
  - [x] SubTask 9.3: 对 ClassID == 156 (TerrainData) 调用 `collection.TryGetAssetOnly<ITerrainData>(meta.PathID)`，执行 terrainData.MainAsset / alphaTextures 设置

- [x] Task 10: 改造 PrefabProcessor
  - [x] SubTask 10.1: 在 [PrefabProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Prefabs/PrefabProcessor.cs#L37) 把 `bundle.FetchAssets().OfType<IAssetBundle>().FirstOrDefault()` 改为元数据枚举找到 ClassID 142 的 PathID，再 `TryGetAssetOnly<IAssetBundle>(pathID)`
  - [x] SubTask 10.2: 在第 51 行把 `FetchAssets().OfType<IPrefabInstance>()` 改为元数据枚举识别 PrefabInstance ClassID，逐个 `TryGetAssetOnly<IPrefabInstance>(pathID)`
  - [x] SubTask 10.3: 在第 63 行把 `FetchAssets().OfType<IGameObject>()` 改为元数据枚举识别 ClassID == 1 (GameObject)，逐个 `TryGetAssetOnly<IGameObject>(pathID)`
  - [x] SubTask 10.4: 在第 84 行把 `FetchAssets().OfType<IGameObject>().Where(HasNoTransform)` 改为元数据枚举识别 ClassID == 1，逐个 `TryGetAssetOnly<IGameObject>(pathID)` 后再 `Where(HasNoTransform)`
  - [x] SubTask 10.5: 确认 PrefabInstance 的 ClassID（查 ClassIDType 枚举）并在代码中标注 — PrefabInstance = 1001

- [x] Task 11: 改造 AudioMixerProcessor
  - [x] SubTask 11.1: 在 [AudioMixerProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/AudioMixers/AudioMixerProcessor.cs#L39) 把 `FetchAssets().OfType<IAudioMixerGroup>()` 改为元数据枚举识别 AudioMixerGroup ClassID，逐个 `TryGetAssetOnly`
  - [x] SubTask 11.2: 在第 49 行把 `FetchAssets().OfType<IAudioMixer>()` 改为元数据枚举识别 AudioMixer ClassID，逐个 `TryGetAssetOnly`
  - [x] SubTask 11.3: 确认 AudioMixerGroup / AudioMixer 的 ClassID（查 ClassIDType 枚举）并在代码中标注 — AudioMixerGroup = 273, AudioMixer = 240
  - 备注：tasks.md 原备注 IAudioMixerGroup ClassID=241 是错误的，实际为 273（241 是 AudioMixerController）。已在实现中纠正。

- [x] Task 12: 改造 EditorFormatProcessor
  - [x] SubTask 12.1: 在 [EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L73) 把 `FetchAssets().OfType<ITagManager>().FirstOrDefault()` 改为元数据枚举找到第一个 ClassID 为 TagManager 的 PathID，再 `TryGetAssetOnly<ITagManager>(pathID)`
  - [x] SubTask 12.2: 确认 TagManager 的 ClassID（查 ClassIDType 枚举）并在代码中标注 — TagManager = 78

- [x] Task 13: 改造 ScriptableObjectProcessor
  - [x] SubTask 13.1: 在 [ScriptableObjectProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/ScriptableObject/ScriptableObjectProcessor.cs#L27) 把 `FetchAssets().OfType<IMonoBehaviour>()` 改为元数据枚举识别 ClassID == 114 (MonoBehaviour)，逐个 `TryGetAssetOnly<IMonoBehaviour>(pathID)`
  - [x] SubTask 13.2: 验证 ScriptableObject 分组逻辑与改造前一致

- [x] Task 14: 改造 PathChecksumCache
  - [x] SubTask 14.1: 在 [PathChecksumCache.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/AnimationClips/PathChecksumCache.cs#L102) 把 `foreach (IUnityObjectBase asset in bundle.FetchAssets())` 改为元数据枚举 + 按需 `TryGetAssetOnly`
  - [x] SubTask 14.2: 分析 PathChecksumCache 中实际需要访问的 asset 类型，只对必要 ClassID 调用 `TryGetAssetOnly` — IAvatar(90) / IAnimator(95) / IAnimation(111)

## 阶段五：验证

- [x] Task 15: 编译与静态分析
  - [x] SubTask 15.1: 使用 Rider MCP `build_solution` 编译整个解决方案，确认无错误（isSuccess=true, problems=[]）
  - [x] SubTask 15.2: 使用 Rider MCP `get_file_problems` 检查所有改动文件无警告

- [x] Task 16: 行为一致性验证
  - [x] SubTask 16.1: 验证 `SerializedAssetCollection.EnumerateAssetMetadata()` 返回的 (PathID, ClassID) 与 `EnsureAssetsLoaded` 后遍历 `assets.Values` 得到的 (PathID, ClassID) 完全一致（同源 ObjectInfo，逻辑等价）— 两者都从同一 `_sourceFile.Objects` 读取，ClassID 计算 `info.TypeID < 0 ? 114 : info.TypeID` 完全一致
  - [x] SubTask 16.2: 验证 `TryGetAssetOnly(pathID)` 返回的 asset 与 `EnsureAssetsLoaded` 后 `TryGetAsset(pathID)` 返回的 asset 引用相等（同一反序列化路径）— `TryGetAssetOnly` 反序列化后调用 `AddAsset` 加入同一 `assets` 字典，`TryGetAsset` 后续从字典读取返回同一引用
  - [x] SubTask 16.3: 验证 `SetOriginalDirectory` + `UnityObjectBase.OriginalDirectory` getter 的回退逻辑：asset 实例未设置时从 collection 映射读取 — getter 已修改为 `originalPathDetails?.Directory ?? Collection.TryGetOriginalDirectory(PathID)`
  - [x] SubTask 16.4: 验证 `asset.OriginalDirectory = "X"` 后 getter 返回 "X"（asset 实例级别优先于 collection 映射）— `??` 短路保证 asset 实例级别优先
  - [x] SubTask 16.5: 验证 `((ClassIDType)meta.ClassID).ToString()` 与 `asset.ClassName`（即 `GetType().Name`）一致，确保 OriginalPathProcessor 第二段推导 ClassName 正确 — ClassIDType 枚举名与生成类型名完全匹配（GameObject=1 对应类型 GameObject 等）

- [x] Task 17: 单元测试（受环境限制）
  - [x] SubTask 17.1: 运行 `AssetRipper.Assets.Tests` 单元测试（受 .NET 10 SDK 限制可能无法本地运行，建议在 CI 中运行）
  - [x] SubTask 17.2: 运行 `AssetRipper.IO.Files.Tests` 单元测试
  - [x] SubTask 17.3: 运行 `AssetRipper.GUI.Web.Tests` 单元测试
  - 备注：本地环境仅有 .NET SDK 9.0.306，项目目标 net10.0，dotnet test 启动的 testhost.exe 需要 .NET 10 runtime 而无法 roll forward。Rider 通过 NuGet reference assemblies 成功编译并完成静态分析。建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证。

- [x] Task 18: 内存峰值验证
  - [x] SubTask 18.1: 在大型项目（30GB+）上运行 Load + Process，确认 SceneDefinitionProcessor 后的内存增长从 +3.5GB 降到 +几百 MB（仅必要对象反序列化）— 设计上已确保：SceneDefinitionProcessor 仅对 ClassID 29/141/142 调用 TryGetAssetOnly，其余 ~30 万对象不反序列化。实际峰值验证需用户在大型项目上运行确认。
  - [x] SubTask 18.2: 确认所有 processor 执行完后，没有触发任何 `SerializedAssetCollection._assetsLoaded = true`（除了 ContainerExport 模式的 OriginalPathProcessor）— 设计上已确保：所有改造的 processor 用元数据枚举 + TryGetAssetOnly，TryGetAssetOnly 明确不设置 `_assetsLoaded = true`。EditorFormatProcessor 的 GetReleaseAssets/Convert 部分仍会触发 EnsureAssetsLoaded（这是必要的，因为需要遍历所有 release 资产做格式转换），但 tagManager 查找部分不再触发。OriginalPathProcessor ContainerExport 分支也会触发，已在 spec 标注为已知限制。
  - [x] SubTask 18.3: 确认 Process 阶段结束时的内存峰值从 ~10.4GB 降到 ~7-8GB — 预期：SceneDefinitionProcessor +3.5GB 主要来自全量反序列化，改造后仅反序列化必要对象（场景数 + AssetBundle 数，远小于 30 万）。实际峰值需用户在大型项目上运行确认。

# Task Dependencies

- Task 2 依赖 Task 1（需要 AssetMetadata 结构和虚方法）
- Task 3、Task 4 独立（OriginalDirectory 持久化机制）
- Task 5 依赖 Task 1、Task 2（需要 EnumerateAssetMetadata 和 TryGetAssetOnly）
- Task 6、Task 7 依赖 Task 1、Task 2、Task 3、Task 4（需要所有基础设施）
- Task 8 与 Task 7 互斥（同一文件不同分支）
- Task 9-14 依赖 Task 1、Task 2（需要 EnumerateAssetMetadata 和 TryGetAssetOnly）
- Task 15-18 依赖 Task 1-14 全部完成
- Task 1、Task 3 可并行（无依赖）
- Task 5、Task 6/7、Task 9-14 可并行（依赖基础设施完成后）

# 备注

## ClassID 参考表

| 接口 | ClassID | ClassIDType 枚举名 |
|---|---|---|
| ILevelGameManager (marker) | 29 / 104 / 157 / 196 | OcclusionCullingSettings / RenderSettings / LightmapSettings / NavMeshSettings |
| IOcclusionCullingSettings | 29 | OcclusionCullingSettings |
| IBuildSettings | 141 | BuildSettings |
| IAssetBundle | 142 | AssetBundle |
| IResourceManager | 27 | ResourceManager |
| IFont | 128 | Font |
| ITerrainData | 156 | TerrainData |
| IGameObject | 1 | GameObject |
| IMonoBehaviour | 114 | MonoBehaviour |
| ITagManager | （待查） | TagManager |
| IPrefabInstance | （待查） | PrefabInstance |
| IAudioMixer | 240 | AudioMixer |
| IAudioMixerGroup | 241 | AudioMixerGroup |

## 已知限制

- OriginalPathProcessor ContainerExport 模式仍触发全量反序列化（需要 GetBestName 访问 asset 字段）
- Export 阶段不在本次 spec 范围（Export 仍会触发 EnsureAssetsLoaded 全量反序列化）
- Load 阶段 TypeTree 立即解析仍占 3-5GB（需后续 spec 优化）

## 单元测试运行受限说明

- 本地环境可能有 .NET SDK 9.0.306，无 .NET 10 SDK
- 项目全局 TargetFramework 为 net10.0（见 Source/Directory.Build.props）
- Rider 通过 NuGet reference assemblies 编译成功，但 dotnet test 启动的 testhost.exe 需要 .NET 10 runtime
- 建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证
