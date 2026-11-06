# ACL 依赖溯源（第一步：锁版本）

## ACL 主库

- 上游仓库：https://github.com/nfrechette/acl
- Tag：`v2.0.0`
- Commit：`441226a7efeaf0bfa2fda12f71733253417ecb7c`
- 源码包 URL：https://codeload.github.com/nfrechette/acl/tar.gz/refs/tags/v2.0.0
- 源码包 SHA-256：`C5B44C063EFBEE6FB5753D0070E37215162D5D47F047FA033024750D9DBB6CDC`
- 许可证：MIT（`External/acl/LICENSE`）
- 本地路径：`E:/Project/Rider/AssetRipper/External/acl`
- 说明：源码逐字节来自上述 tag 压缩包，未做任何修改；仅补充了 `external/rtm` 子模块。

版本判定依据：

- `includes/acl/core/compressed_tracks_version.h` 中 `v02_00_00 = 7`，与动画 blob 头部的 `version=7` 一致。
- `includes/acl/decompression/impl/decompression_version_selector.h:56-58` 明确支持 `compressed_tracks_version16::v02_00_00`。

## RTM 子模块

ACL tag 的 gitlink 指向固定 commit，压缩包不含子模块内容，按 gitlink 单独取回：

- 上游仓库：https://github.com/nfrechette/rtm
- Commit：`dda7c7452a37c859d1805a15d104e01173df6d08`
- 源码包 URL：https://codeload.github.com/nfrechette/rtm/tar.gz/dda7c7452a37c859d1805a15d104e01173df6d08
- 源码包 SHA-256：`EA78A141D69D9755FAC9F24CB51FBE60D26574C9BA65760B9B885C67878077B6`
- 许可证：MIT（`External/acl/external/rtm/LICENSE`）
- 本地路径：`E:/Project/Rider/AssetRipper/External/acl/external/rtm`
- 说明：源码未修改。

其余子模块（`sjson-cpp`、`catch2`、`benchmark`）只被 ACL 的压缩器/测试使用，解压路径不需要，因此保持 tag 压缩包中的空目录状态，未下载。

## 验证记录（持久）

Unity 会周期性清理 `e:/Project/Unity/Nikki4/Temp`，原临时验证工程已被清理。持久记录改放在 AssetRipper 的 build 目录：

- 缺失 DLL 日志：`build/acl-test/missing_dll.log`
- 多 ACL clip 缺失 DLL（警告去重）：`build/acl-test/missing_dll_multi.log`
- 32 位 DLL 日志：`build/acl-test/bad_bitness.log`
- 版本不匹配日志：`build/acl-test/bad_version.log`
- 正常 DLL 导出日志：`build/acl-test/valid_dll.log`
- 正常导出 YAML：`build/acl-test/valid_idle03.anim`
- 缺失 DLL 时的空壳 YAML：`build/acl-test/missing_idle03.anim`
- 曲线统计脚本：`build/acl-test/count.py`

ACL 解码统计与逐位对照结论见 `External/acl-native/README.md` 与本次任务汇报。

## 原生 DLL（AssetRipper 接线用）

- 路径：`E:/Project/Rider/AssetRipper/External/acl-native`
- 接口头：`External/acl-native/include/acl_native.h`
- C ABI：`aclv_probe` / `aclv_open` / `aclv_close` / `aclv_get_sub_track_types` / `aclv_decompress_track`
- 构建脚本：`External/acl-native/build.ps1`
- 构建系统：CMake + Visual Studio 18 2026（MSVC 19.51.36257.0），x64 Release
- DLL：`External/acl-native/bin/acl-native.dll`
- DLL SHA-256：`D9059EFF53FDCDC226E3CE4CCE8A1C2431CAD3CC31300B37419A4D3517711422`
- AssetRipper 引用方式：`AssetRipper.Import.csproj` 以 `None/CopyToOutputDirectory` 把 DLL 复制到输出目录
- 平台：当前只构建 Windows x64；其他平台需要按同一 CMakeLists 重新编译
