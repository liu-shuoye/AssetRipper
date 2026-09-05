# 启动 GUI.Free 测试验证改动（Rider MCP 指引）

> 适用范围：在 TRAE 中通过 `mcp_rider`（`execute_tool`）驱动 Rider 构建/启动 AssetRipper GUI，并对改动做 headless 导出验证。
> 本文档流程基于真实执行验证（AssetRipper 解决方案、net10.0、Windows 11）。

---

## 1. 目的

源码改动（尤其 `AssetRipper.Import`、`AssetRipper.SourceGenerated.Extensions` 等库项目）能否真正生效，
需要用构建产物 **GUI.Free** 实测验证：

1. 编译通过；
2. headless 启动服务并加载真实游戏文件；
3. 走与用户一致的导出链路，检查导出产物是否符合预期。

以近期 **Mesh 顶点流压缩（Nikki4 5 流 → 标准 4 流）** 改动为例：改动后导出 `.asset` 中所有
`m_Channels[].stream` 必须落在 0-3，Unity 导入不再抛 `Vertex stream out of range`。

---

## 2. 前置条件

| 项 | 要求 |
|----|------|
| Rider | 已打开 `D:/Project/AssetRipper` 解决方案，MCP 插件连通 |
| mcp_rider | `server_name = mcp_rider`，`tool_name = execute_tool` |
| rootFolder | 每次调用传 `D:/Project/AssetRipper`，避免项目歧义 |
| .NET | 本机 .NET 10.0.301 SDK（解决方案目标 net10.0） |
| 测试数据 | 真实游戏文件，如 `D:\UserData\閃耀暖暖_4.1.2328503\assets\art\fx\scenes\mesh.nn4bld` |

启动前确认 Rider 连通性：

```
command: get_solution_projects
rootFolder: D:/Project/AssetRipper
```

能列出 60+ 模块（含 `AssetRipper.GUI.Free`）即连通。

---

## 3. 构建

### 3.1 Rider 构建（异步，需轮询）

```
command: build_solution_start
rootFolder: D:/Project/AssetRipper
```

```
command: build_solution_state
rootFolder: D:/Project/AssetRipper
```

构建类命令是异步的：`build_solution_start` 触发后必须用 `build_solution_state` 轮询到成功，
再进入下一步。也可先在 Rider 外部快速验证单个库项目：

```powershell
dotnet build d:\Project\AssetRipper\Source\AssetRipper.Import\AssetRipper.Import.csproj -c Debug
```

### 3.2 构建产物路径

| 产物 | 路径 |
|------|------|
| GUI.Free 可执行 | `d:\Project\AssetRipper\Source\0Bins\AssetRipper.GUI.Free\Debug\AssetRipper.GUI.Free.exe` |

> GUI.Free 是整体导出链路的宿主（引用了 Import / Export / SourceGenerated.Extensions 等），
> 改了这些库必须重建 GUI.Free，只 build 单库不足以让旧 exe 生效。

---

## 4. 启动 GUI.Free

### 4.1 方式一：Rider 运行配置（可调试）

获取运行配置名（已实测存在 `AssetRipper.GUI.Free`）：

```
command: get_run_configurations
rootFolder: D:/Project/AssetRipper
```

启动（无头验证不依赖 GUI，建议配合 4.2 方式；需要调试时可用本方式 + 附加调试器）：

```
command: execute_run_configuration --configName AssetRipper.GUI.Free
rootFolder: D:/Project/AssetRipper
```

> Rider 对参数名校验严格：若 `configName` 参数名报错，Rider 会返回缺失/正确的参数名，按提示补齐。

调试可选：启动后可用 `attach_to_process` 附加，或 `xdebug_start_debugger_session` +
`xdebug_set_breakpoint` 打断点查 `NormalizeStreams` 内部状态。

### 4.2 方式二：命令行 headless（测试首选）

不弹 GUI 窗口、便于脚本化断言：

```powershell
# 以指定端口启动 headless 服务（端口自选，避免与已有实例冲突）
Start-Process -FilePath "d:\Project\AssetRipper\Source\0Bins\AssetRipper.GUI.Free\Debug\AssetRipper.GUI.Free.exe" `
  -ArgumentList "--port","19200","--headless" -WindowStyle Hidden -PassThru
```

健康检查：

```powershell
(Invoke-WebRequest -Uri "http://localhost:19200/" -UseBasicParsing -TimeoutSec 5).StatusCode  # 期望 200
```

> headless 模式下约 6 秒后服务就绪；若端口被占用换用其他端口并同步修改以下所有 URL。

---

## 5. headless API 验证流程（对应库改动的导出链路）

> 表单一律用 `application/x-www-form-urlencoded`（`Invoke-WebRequest -Method Post -Body`）。

### 5.1 设置游戏类型并加载文件

```powershell
# 1) 游戏类型（Nikki4 专属解析入口，决定 Mesh_Nikki4 等解析类是否生效）
$body = @{ GameType = "Nikki4" }
Invoke-WebRequest -Uri "http://localhost:19200/Settings/Update" -Method Post -Body $body -UseBasicParsing | Select-Object StatusCode

# 2) 加载真实 bundle 文件
$body = @{ Path = "D:\UserData\閃耀暖暖_4.1.2328503\assets\art\fx\scenes\mesh.nn4bld" }
Invoke-WebRequest -Uri "http://localhost:19200/LoadFile" -Method Post -Body $body -UseBasicParsing -TimeoutSec 300 | Select-Object StatusCode
```

> `LoadFile` 会同步完成解析与 Processing，大文件请给足超时（300s 以上）。

### 5.2 导出 Unity 工程（产出 `.asset`）

```powershell
# 输出到空目录；CreateSubfolder=false 时产物直接在 Path 下
$out = "D:\Project\AssetRipper\tmp\export_test"
$body = @{ Path = $out; CreateSubfolder = "false" }
Invoke-WebRequest -Uri "http://localhost:19200/Export/UnityProject" -Method Post -Body $body -UseBasicParsing -TimeoutSec 590 | Select-Object StatusCode
```

导出结果为完整 Unity 工程：

```
<out>\ExportedProject\Assets\build\art\fx\scenes\mesh\fx_924_candy_01_06_geo.asset
```

### 5.3 校验导出产物（mesh 顶点流修复的检查点）

针对"5 流压缩"改动的验收：`.asset` 内 `m_Channels` 的 `stream` 不允许出现 >= 4。

```powershell
# 校验目标资产：stream 值全部 <= 3 才算通过
$f = "<out>\ExportedProject\Assets\build\art\fx\scenes\mesh\fx_924_candy_01_06_geo.asset"
Select-String -Path $f -Pattern "m_VertexCount:|stream:|m_DataSize:" | Select-Object -First 20

# 批量全量扫描：找出所有仍带越界流的资产（期望无输出）
Get-ChildItem "<out>\ExportedProject\Assets" -Recurse -Filter "*.asset" | ForEach-Object {
  $bad = Select-String -Path $_.FullName -Pattern "^\s+- stream: ([0-9]+)$" |
         ForEach-Object { ($_.Line -replace "[^0-9]","") } |
         Where-Object { [int]$_ -ge 4 }
  if ($bad) { "BAD: $($_.FullName)" }
}
```

自查一致性的快速验算（数据规模与流布局自洽）：

```
m_VertexCount × (各流 stride 之和) ≈ m_DataSize（16 字节对齐）
```

### 5.4 （可选）Unity 导入终验

用干净工程 + 批处理模式确认 NativeFormatImporter 不再崩溃（不依赖已打开的编辑器）：

```powershell
# 1) 搭建最小工程：Assets/TestMesh 放目标 .asset，ProjectSettings/ProjectVersion.txt 写 m_EditorVersion: 6000.3.21f1
# 2) 批处理导入（退出码 0 = 成功）
& "D:\Program Files\UnityHubEddie\6000.3.21f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "D:\...\unity_verify" -quit -logFile "import.log"
# 3) 断言日志：
#    Start importing Assets/TestMesh/xxx.asset ... (NativeFormatImporter)   # 出现过
#    无 "Vertex stream out of range"、无 crash 堆栈、退出码 0
```

---

## 6. 常见问题

| 现象 | 原因 | 处理 |
|------|------|------|
| `Unable to determine the target project` | 未传 `rootFolder` 且有多个项目 | 调用参数补充 `rootFolder: D:/Project/AssetRipper` |
| `/Collections/View` 等返回 404 | 这些端点要求 `Path` 查询参数（URL 编码 JSON），缺参即 404 | 验证优先走 5.1-5.3 的 LoadFile + Export 流程，不依赖该页面 |
| `Settings/Update` 在加载后才调用 | 设置仅允许加载前修改，调用了也不生效 | 先 `POST /Settings/Update` 再 `POST /LoadFile`（顺序不可反） |
| 导出后旧 exe 行为不变 | 只 build 了库项目，GUI.Free 未重建 | 重建 GUI.Free（见第 3 节） |
| headless 端口被占 | 服务实例残留 | 换端口重试；或 `Get-Process AssetRipper.GUI.Free | Stop-Process -Force` 后重启 |

---

## 7. 与真实链路的关系

| 本验证环节 | 对应线上链路 |
|------------|--------------|
| `Settings/Update GameType=Nikki4` | 用户导出时选择的游戏类型 |
| `Mesh_Nikki4.ReadRelease` + `VertexData.NormalizeStreams` | 解析/压缩改动生效点 |
| `Export/UnityProject` 产出的 `.asset` | 用户导入 Unity 的实际文件 |
| 5.3 扫描 `stream >= 4` | 复现 "Vertex stream out of range" 的根因检查 |