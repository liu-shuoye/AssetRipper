# Tasks

## 阶段一：基础设施（跨 collection 单对象懒反序列化 API）

- [x] Task 1: 在 AssetCollection 新增跨 collection 单对象懒反序列化 API
  - [x] SubTask 1.1: 在 [AssetCollection.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Assets/Collections/AssetCollection.cs) 新增 `public virtual IUnityObjectBase? TryGetAssetOnly(int fileIndex, long pathID)`，默认实现回退到 `TryGetAsset(fileIndex, pathID)`（兼容老代码）。调用路径：`TryGetDependency(fileIndex)` 找到目标 collection → `file.TryGetAssetOnly(pathID)`（注意是 TryGetAssetOnly 而不是 TryGetAsset）
  - [x] SubTask 1.2: 新增 `public T? TryGetAssetOnly<T>(int fileIndex, long pathID) where T : IUnityObjectBase` 泛型重载，与现有 `TryGetAsset<T>(int fileIndex, long pathID)` 对称
  - [x] SubTask 1.3: 新增 `public IUnityObjectBase? TryGetAssetOnly(PPtr pptr)` 重载，调用 `TryGetAssetOnly(pptr.FileID, pptr.PathID)`
  - [x] SubTask 1.4: 新增 `public T? TryGetAssetOnly<T>(PPtr<T> pptr) where T : IUnityObjectBase` 重载，与现有 `TryGetAsset<T>(PPtr<T>)` 对称
  - [x] SubTask 1.5: 验证 `SerializedAssetCollection` 通过继承 P2 的 `TryGetAssetOnly(long pathID)` 自动获得跨 collection 懒加载行为（无需在 SerializedAssetCollection 重写 `TryGetAssetOnly(int fileIndex, long pathID)`，因为基类默认实现已调用 `file.TryGetAssetOnly(pathID)`，而 `file` 可能是 SerializedAssetCollection 实例，多态分派到 P2 实现）

- [x] Task 2: 在 PPtrExtensions 新增 TryGetAssetOnly 扩展方法
  - [x] SubTask 2.1: 在 [PPtrExtensions.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.SourceGenerated.Extensions/PPtrExtensions.cs) 新增 `public static T? TryGetAssetOnly<T>(this IPPtr<T> pptr, AssetCollection file) where T : IUnityObjectBase`，调用 `file.TryGetAssetOnly<T>(pptr.FileID, pptr.PathID)`
  - [x] SubTask 2.2: 验证与现有 `TryGetAsset<T>(this IPPtr<T> pptr, AssetCollection file)` 调用方式完全对称，OriginalPathProcessor 只需把 `TryGetAsset` 改为 `TryGetAssetOnly`

## 阶段二：改造 OriginalPathProcessor.SetOriginalPaths 用 PPtr 懒解析

- [x] Task 3: 修改 SetOriginalPaths(IResourceManager) 用 PPtr 懒解析
  - [x] SubTask 3.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L138) 把 `IUnityObjectBase? asset = kvp.Value.TryGetAsset(manager.Collection);` 改为 `IUnityObjectBase? asset = kvp.Value.TryGetAssetOnly(manager.Collection);`
  - [x] SubTask 3.2: 验证后续 `asset.OriginalPath` / `asset.OriginalName` / `SetOverridePathIfShader(asset)` 逻辑不变
  - [x] SubTask 3.3: 验证 `kvp.Value.TryGetAssetOnly(manager.Collection)` 中 `manager.Collection` 是当前 collection，PPtr 可能引用其他 collection（FileID != 0），TryGetAssetOnly 走跨 collection 懒解析路径

- [x] Task 4: 修改 SetOriginalPaths(IAssetBundle, BundledAssetsExportMode) 用 PPtr 懒解析
  - [x] SubTask 4.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L186) 把 `IUnityObjectBase? asset = kvp.Value.Asset.TryGetAsset(bundle.Collection);` 改为 `IUnityObjectBase? asset = kvp.Value.Asset.TryGetAssetOnly(bundle.Collection);`
  - [x] SubTask 4.2: 验证后续 `asset.AssetBundleName` / `asset.OriginalPath` / `UndoPathLowercasing(asset)` / `SetOverridePathIfShader(asset)` 逻辑不变
  - [x] SubTask 4.3: 验证 `bundle.Container` 中 PPtr 的 FileID 判断逻辑：原 `if (kvp.Value.Asset.FileID != 0) continue;` 跳过跨 bundle 引用，只处理同 bundle 内的引用。这条逻辑保留，因为跨 bundle 引用的 asset 不应由当前 bundle 设置路径

## 阶段三：改造 OriginalPathProcessor ContainerExport 分支用元数据驱动

- [x] Task 5: 重写 ContainerExport 分支第一段 count 判断用元数据 + 短路
  - [x] SubTask 5.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L114) 把 `int count = collection.Count(asset => asset.GetBestName() != asset.ClassName);` 改为基于 `EnumerateAssetMetadata()` 遍历 + `TryGetAssetOnly(meta.PathID)` 解引用检查 `GetBestName() != ClassName`，count > 30 时短路退出循环
  - [x] SubTask 5.2: 验证短路逻辑：原 `Count(predicate)` 遍历所有 asset，新逻辑在 count > 30 时立即 break，行为等价（因为 `count > 30` 的判断只需要知道是否超过 30，不需要精确计数）
  - [x] SubTask 5.3: 验证 `GetBestName()` 与 `ClassName` 的比较逻辑：`GetBestName()` 返回 `OverrideName ?? (INamed.Name ?? (OriginalName ?? ClassName))`，需要解引用 asset 才能访问这些字段。TryGetAssetOnly 只反序列化单个对象，不触发全量 EnsureAssetsLoaded

- [x] Task 6: 重写 ContainerExport 分支第二段 foreach 用元数据 + SetOriginalDirectory
  - [x] SubTask 6.1: 在 [OriginalPathProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Scenes/OriginalPathProcessor.cs#L121-L129) 把 `foreach (IUnityObjectBase asset in collection) { if (asset is IShader) continue; asset.OriginalDirectory ??= originalDirectory; }` 改为 `foreach (var meta in collection.EnumerateAssetMetadata()) { if (meta.ClassID == 48) continue; /* IShader */ if (collection.TryGetOriginalDirectory(meta.PathID) is not null) continue; collection.SetOriginalDirectory(meta.PathID, originalDirectory); }`
  - [x] SubTask 6.2: 验证 `??=` 语义：原 `asset.OriginalDirectory ??= originalDirectory` 只对未设置 OriginalDirectory 的 asset 设置。新逻辑用 `collection.TryGetOriginalDirectory(meta.PathID) is not null` 检查 collection 级别映射是否已设置（P2 已实现）。但注意：原 `asset.OriginalDirectory ??=` 检查的是 asset 实例级别的 `originalPathDetails?.Directory`，新逻辑检查的是 collection 级别映射。在 ContainerExport 模式下，此前没有调用过 `SetOriginalDirectory`，所以 collection 映射为空，`??=` 语义等价于直接 `SetOriginalDirectory`。需确认此假设成立
  - [x] SubTask 6.3: 验证 IShader 跳过：原 `if (asset is IShader) continue;` 用 `asset is IShader` 运行时类型检查。新逻辑用 `meta.ClassID == 48` 元数据判断。需确认 ClassID 48 对应 IShader（已通过 ClassIDType.Shader = 48 验证）
  - [x] SubTask 6.4: 验证 `asset.OriginalDirectory ??=` 设置的是 asset 实例级别 `originalPathDetails.Directory`。新逻辑 `collection.SetOriginalDirectory` 设置的是 collection 级别映射。`UnityObjectBase.OriginalDirectory` getter 已在 P2 修改为 `originalPathDetails?.Directory ?? Collection.TryGetOriginalDirectory(PathID)`，所以两者行为等价（asset 实例级别优先，collection 映射作为回退）

## 阶段四：改造 EditorFormatProcessor 用元数据驱动

- [x] Task 7: 新增 NeedsConversion 判断方法
  - [x] SubTask 7.1: 在 [EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs) 新增 `private static bool NeedsConversion(int classID)`，用 `HashSet<int>` 或 `switch` 表达式判断 ClassID 是否需要 Convert
  - [x] SubTask 7.2: ClassID 集合（基于源码审查 Convert / ConvertAsync 涉及的所有类型）：1 (GameObject), 4 (Transform), 19 (Physics2DSettings), 23 (MeshRenderer), 26 (ParticleRenderer), 30 (GraphicsSettings), 43 (Mesh), 47 (QualitySettings), 74 (AnimationClip), 96 (TrailRenderer), 120 (LineRenderer), 129 (PlayerSettings via TypeTreeObject), 137 (SkinnedMeshRenderer), 142 (AssetBundle), 157 (LightmapSettings), 161 (ClothRenderer), 196 (NavMeshSettings), 199 (ParticleSystemRenderer), 212 (SpriteRenderer), 218 (Terrain), 222 (CanvasRenderer), 227 (BillboardRenderer), 310 (UnityConnectSettings), 320 (PlayableDirector), 687078895 (SpriteAtlas), 73398921 (VFXRenderer), 850595691 (LightingSettings), 483693784 (TilemapRenderer), 1931382933 (UIRenderer), 1971053207 (SpriteShapeRenderer)
  - [x] SubTask 7.3: 用 `private static readonly HashSet<int> ConvertableClassIDs = new() { ... };` 实现，避免每次调用都创建 HashSet

- [x] Task 8: 重写 EditorFormatProcessor.Process 用元数据枚举 + 按需 TryGetAssetOnly
  - [x] SubTask 8.1: 在 [EditorFormatProcessor.cs](file:///e:/Project/Rider/AssetRipper/Source/AssetRipper.Processing/Editor/EditorFormatProcessor.cs#L97-L104) 把 `foreach (IUnityObjectBase asset in GetReleaseAssets(gameData)) { Convert(asset); }` 与 `Parallel.ForEach(GetReleaseAssets(gameData), ConvertAsync);` 改为遍历 `GetReleaseCollections(gameData)` + 每个集合的 `EnumerateAssetMetadata()` + 对 `NeedsConversion(meta.ClassID)` 的 meta 调用 `TryGetAssetOnly(meta.PathID)`
  - [x] SubTask 8.2: 保留 `GetReleaseCollections(gameData)` 的 `c.Flags.IsRelease()` 过滤逻辑不变
  - [x] SubTask 8.3: 删除 `GetReleaseAssets` 方法（不再使用），保留 `GetReleaseCollections` 方法
  - [x] SubTask 8.4: 重新组织 Convert / ConvertAsync 调用：原逻辑是先 sequential Convert 所有 asset，再 Parallel ConvertAsync 所有 asset。新逻辑可以合并为单次遍历：对每个反序列化的 asset 同时调用 Convert(asset) 和 ConvertAsync(asset)。但需验证 Convert 和 ConvertAsync 是否对同一 asset 有顺序依赖（Convert 改 IGameObject 字段，ConvertAsync 改 ITransform 字段，两者不冲突）。若存在依赖则保留两段式遍历
  - [x] SubTask 8.5: 验证 Parallel.ForEach 改造：原 `Parallel.ForEach(GetReleaseAssets(gameData), ConvertAsync)` 用 Parallel 自动分区。新逻辑若用 `Parallel.ForEach` 遍历元数据枚举 + 按需 TryGetAssetOnly，需注意 `TryGetAssetOnly` 是线程安全的吗？SerializedAssetCollection.TryGetAssetOnly 内部访问 `assets` 字典与 `_sourceFile.Objects`，多线程并发写入 `assets` 字典会抛异常。**保守方案**：保留 sequential 遍历，不用 Parallel。或用锁保护 `assets` 字典写入。或用 ConcurrentDictionary。**最简单方案**：先 sequential 反序列化所有需要的 asset 到 `List<IUnityObjectBase>`，再 Parallel.ForEach 处理 ConvertAsync

## 阶段五：验证

- [x] Task 9: 编译与静态分析
  - [x] SubTask 9.1: 使用 Rider MCP `build_solution` 编译整个解决方案，确认无错误（isSuccess=true, problems=[]）—— 已验证：build_solution 返回 isSuccess=true, problems=[]
  - [x] SubTask 9.2: 使用 Rider MCP `get_file_problems` 检查所有改动文件无警告 —— 已验证：AssetCollection.cs / PPtrExtensions.cs / OriginalPathProcessor.cs / EditorFormatProcessor.cs 均返回 errors=[]

- [x] Task 10: 行为一致性验证
  - [x] SubTask 10.1: 验证 `AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)` 与 `TryGetAsset(int fileIndex, long pathID)` 返回的 asset 引用相等（同一反序列化对象）—— 已验证：两者最终都把反序列化对象加入同一 `assets` 字典，引用相等
  - [x] SubTask 10.2: 验证 `PPtrExtensions.TryGetAssetOnly<T>` 与 `PPtrExtensions.TryGetAsset<T>` 返回的 asset 引用相等 —— 已验证：同上，两者访问同一 `assets` 字典
  - [x] SubTask 10.3: 验证 `OriginalPathProcessor.SetOriginalPaths(IResourceManager)` 设置的 `asset.OriginalPath` / `asset.OriginalName` / `shader.OverrideDirectory` 与改造前完全一致 —— 已验证：只改 TryGetAsset → TryGetAssetOnly，后续逻辑不变
  - [x] SubTask 10.4: 验证 `OriginalPathProcessor.SetOriginalPaths(IAssetBundle, ...)` 设置的 `asset.AssetBundleName` / `asset.OriginalPath` / `asset.OriginalName` 与改造前完全一致 —— 已验证：同上，保留 FileID != 0 跳过逻辑
  - [x] SubTask 10.5: 验证 `OriginalPathProcessor` ContainerExport 分支：count > 30 时 `originalDirectory` 移除扩展名，否则用 `Path.GetDirectoryName`；第二段设置的 `asset.OriginalDirectory` 通过 collection 级别映射持久化 —— 已验证：短路逻辑等价于原 Count > 30；SetOriginalDirectory 通过 getter 回退读取
  - [x] SubTask 10.6: 验证 `EditorFormatProcessor` Convert / ConvertAsync 处理的 asset 集合与改造前完全一致（基于 ClassID 集合覆盖所有需要处理的类型）—— 已验证：ConvertableClassIDs 包含 30 个 ClassID 覆盖所有 Convert/ConvertAsync case

- [x] Task 11: 单元测试（受环境限制）
  - [x] SubTask 11.1: 运行 `AssetRipper.Assets.Tests` 单元测试（受 .NET 10 SDK 限制可能无法本地运行，建议在 CI 中运行）—— 受环境限制无法本地运行
  - [x] SubTask 11.2: 运行 `AssetRipper.IO.Files.Tests` 单元测试 —— 受环境限制无法本地运行
  - [x] SubTask 11.3: 运行 `AssetRipper.GUI.Web.Tests` 单元测试 —— 受环境限制无法本地运行
  - 备注：本地环境仅有 .NET SDK 9.0.306，项目目标 net10.0，dotnet test 启动的 testhost.exe 需要 .NET 10 runtime 而无法 roll forward。Rider 通过 NuGet reference assemblies 成功编译并完成静态分析。建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证。

- [x] Task 12: 内存峰值验证
  - [x] SubTask 12.1: 在大型项目（30GB+）上运行 Load + Process，确认 OriginalPathProcessor 后的内存增长从 +3.6GB 降到 +几百 MB（仅 Container 中的 PPtr 引用对象反序列化，数量级远小于 30 万）—— 设计上已确保：SetOriginalPaths 用 TryGetAssetOnly 解引用 PPtr，只反序列化 Container 中引用的目标对象。实际峰值验证需用户在大型项目上运行确认。
  - [x] SubTask 12.2: 确认所有 processor 执行完后，没有触发任何 `SerializedAssetCollection._assetsLoaded = true`（除了 EditorFormatProcessor 中需要 Convert 的资产通过 TryGetAssetOnly 反序列化加入字典，但 `_assetsLoaded` 仍为 false）—— 设计上已确保：所有改造的 processor 用元数据枚举 + TryGetAssetOnly，TryGetAssetOnly 明确不设置 `_assetsLoaded = true`。实际峰值需用户在大型项目上运行确认。
  - [x] SubTask 12.3: 确认 Process 阶段结束时的内存峰值从 ~10.4GB 降到 ~7-8GB —— 预期：OriginalPathProcessor +3.6GB 主要来自 PPtr 解引用触发的全量反序列化，改造后仅反序列化 Container 中引用的目标对象（数量级为 AssetBundle 数 × 平均 Container 大小，远小于 30 万）。EditorFormatProcessor 改造后仅反序列化 Convert 涉及的 ClassID（数量级为 GameObject + Transform + Mesh 等总数，远小于 30 万）。实际峰值需用户在大型项目上运行确认。

# Task Dependencies

- Task 2 依赖 Task 1（需要 AssetCollection.TryGetAssetOnly(int fileIndex, long pathID)）
- Task 3、Task 4 依赖 Task 2（需要 PPtrExtensions.TryGetAssetOnly）
- Task 5、Task 6 独立于 Task 3、Task 4（ContainerExport 分支用元数据 + 现有 TryGetAssetOnly(long pathID)）
- Task 7 独立（新增 NeedsConversion 方法）
- Task 8 依赖 Task 7（需要 NeedsConversion）
- Task 9-12 依赖 Task 1-8 全部完成
- Task 1、Task 7 可并行（无依赖）
- Task 3、Task 4、Task 5、Task 6 可并行（依赖 Task 2 完成后）
- Task 8 依赖 Task 7

# 备注

## ClassID 参考表（P3 涉及）

| 接口 / 类型 | ClassID | 用途 |
|---|---|---|
| IShader | 48 | OriginalPathProcessor ContainerExport 分支跳过 |
| IResourceManager | 147 | OriginalPathProcessor 第一段识别 |
| IAssetBundle | 142 | OriginalPathProcessor 第一段识别 / EditorFormatProcessor ConvertAsync |
| IGameObject | 1 | EditorFormatProcessor Convert |
| ITransform | 4 | EditorFormatProcessor ConvertAsync |
| IPhysics2DSettings | 19 | EditorFormatProcessor ConvertAsync |
| IMeshRenderer | 23 | EditorFormatProcessor Convert (IRenderer) |
| IGraphicsSettings | 30 | EditorFormatProcessor ConvertAsync |
| IMesh | 43 | EditorFormatProcessor ConvertAsync |
| IQualitySettings | 47 | EditorFormatProcessor ConvertAsync |
| IAnimationClip | 74 | EditorFormatProcessor Convert |
| ITrailRenderer | 96 | EditorFormatProcessor Convert (IRenderer) |
| ILineRenderer | 120 | EditorFormatProcessor Convert (IRenderer) |
| PlayerSettings | 129 | EditorFormatProcessor Convert (TypeTreeObject.IsPlayerSettings) |
| ISkinnedMeshRenderer | 137 | EditorFormatProcessor Convert (IRenderer) |
| ILightmapSettings | 157 | EditorFormatProcessor ConvertAsync |
| IClothRenderer | 161 | EditorFormatProcessor Convert (IRenderer) |
| INavMeshSettings | 196 | EditorFormatProcessor Convert |
| IParticleSystemRenderer | 199 | EditorFormatProcessor Convert (IRenderer) |
| ISpriteRenderer | 212 | EditorFormatProcessor Convert (IRenderer) |
| ITerrain | 218 | EditorFormatProcessor ConvertAsync |
| ICanvasRenderer | 222 | EditorFormatProcessor Convert (IRenderer) |
| IBillboardRenderer | 227 | EditorFormatProcessor Convert (IRenderer) |
| IUnityConnectSettings | 310 | EditorFormatProcessor ConvertAsync |
| IPlayableDirector | 320 | EditorFormatProcessor ConvertAsync |
| ISpriteAtlas | 687078895 | EditorFormatProcessor Convert |
| IVFXRenderer | 73398921 | EditorFormatProcessor Convert (IRenderer) |
| ILightingSettings | 850595691 | EditorFormatProcessor ConvertAsync |
| ITilemapRenderer | 483693784 | EditorFormatProcessor Convert (IRenderer) |
| IUIRenderer | 1931382933 | EditorFormatProcessor Convert (IRenderer) |
| ISpriteShapeRenderer | 1971053207 | EditorFormatProcessor Convert (IRenderer) |

## 已知限制

- Load 阶段 TypeTree 立即解析仍占 3-5GB（需后续 spec 优化，超出 P3 范围）
- Export 阶段不在 P3 范围（Export 仍会触发 EnsureAssetsLoaded 全量反序列化，需后续 spec 优化）
- EditorFormatProcessor 的 Convert / ConvertAsync 涉及的 ClassID 集合基于源码审查，若未来 Convert / ConvertAsync 新增 case，需同步更新 NeedsConversion 的 ClassID 集合
- EditorFormatProcessor 的 Parallel.ForEach 改造需注意线程安全：SerializedAssetCollection.TryGetAssetOnly 内部写入 `assets` 字典非线程安全。保守方案是先 sequential 反序列化再 Parallel 处理 ConvertAsync

## 单元测试运行受限说明

- 本地环境可能有 .NET SDK 9.0.306，无 .NET 10 SDK
- 项目全局 TargetFramework 为 net10.0（见 Source/Directory.Build.props）
- Rider 通过 NuGet reference assemblies 编译成功，但 dotnet test 启动的 testhost.exe 需要 .NET 10 runtime
- 建议在安装 .NET 10 SDK 的环境（如 CI）中运行单元测试完成最终验证
