# acl-native

Nikki4 的 `AnimationClip.m_ACLClip` 使用 ACL v2.0.0 压缩格式。本目录提供最小原生解压器，供 AssetRipper 导出空壳动画曲线。

## 构建

前置条件：

- Windows x64
- Visual Studio 2022/2026 的 C++ 工具链
- CMake
- ACL v2.0.0 源码在 `../acl`，且 `../acl/external/rtm` 已取回

执行：

```powershell
powershell -ExecutionPolicy Bypass -File External/acl-native/build.ps1
```

产物：

```text
External/acl-native/bin/acl-native.dll
```

`AssetRipper.Import.csproj` 会把该 DLL 复制到 AssetRipper 输出目录，DLL 必须与宿主可执行文件同目录。

## DLL 缺失或加载失败时

导出不会中断：

- 标准 Streamed / Dense / Constant 三段照常导出；
- ACL 曲线重建跳过，只打印一条 `Warning`；
- 原本就是空壳的 ACL clip 会保持空曲线，不会崩溃或写错数据。

位数/版本不匹配同样只警告并跳过，不会静默产出错误曲线：

- 32 位 DLL / 架构不匹配：`BadImageFormatException`
- 缺少 `aclv_*` 导出（旧版本 DLL）：`EntryPointNotFoundException`
- 文件不存在：`DllNotFoundException`

警告示例：

```text
ACL 动画曲线重建已跳过：未找到 acl-native.dll。Unable to load DLL 'acl-native' ... 
请运行 External/acl-native/build.ps1，并确保 acl-native.dll 与 AssetRipper 可执行文件同目录。
```

## C ABI

头文件：`include/acl_native.h`

- `aclv_probe`：只校验头部/hash
- `aclv_open` / `aclv_close`：建立/释放解压上下文
- `aclv_get_sub_track_types`：返回每 track 的 rotation/translation/scale 子轨道类型（0=default，1=constant，2=animated）
- `aclv_decompress_track`：只解一条 track，避免整段大拷贝

调用方需保证 `aclv_open` 到 `aclv_close` 期间传入的 blob 指针有效；DLL 不复制 blob。

当前只构建 Windows x64。其他平台需要用 `CMakeLists.txt` 重新编译。
