# AssetRipper 项目长期记忆

## 构建环境
- **构建优先走 Rider MCP**（`mcp__rider__execute_tool`：`build_solution_start` → `build_solution_state --sessionId <id>` 轮询；返回 `buildIsSuccess` + `problems`）：Rider 自带完整 Windows 环境，restore/build 都正常。
- **新增工程或工程引用后必须先真实 restore**（Rider 构建会自动 restore），否则 `--no-restore` 构建报 NU1105。csc.exe 在黑名单。
- 快速复现验证：`0Bins/AssetRipper.GUI.Free/Debug/AssetRipper.GUI.Free.exe --port <N> --headless`（Ookii POSIX 风格参数），POST /Settings/Update (ImportSettings.GameType=Nikki4)、/LoadFile(Path=)，GET /Collections/View、/Assets/Json|Yaml 观察（用 --noproxy curl 或 python urllib）。
