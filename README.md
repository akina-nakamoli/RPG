# 乡村编年史 · Village Chronicles

《乡村编年史》是一款使用 Unity 和 C# 开发的 2D 俯视角像素风单机 RPG。玩家从村庄出发，在森林中探索、战斗和护送 NPC，通过拾取、交易和技能加点成长。完成靶场挑战后，可以招募射手并在战士和射手之间切换。

## 玩法与技术实现

- **实时战斗**：用 Animator 动画事件触发近战判定，同一次攻击对同一敌人只结算一次。箭矢沿每帧的移动路径检测碰撞。敌人使用待机、追击、攻击三态有限状态机。
- **森林探索与任务**：用固定种子生成可复现的森林迷宫，通过 BFS 选择距离入口较远的边界出口。护送 NPC 在直线受到阻挡时，在 Tilemap 道路网格上搜索路线。任务系统记录采集、击杀和护送进度。
- **物品与成长**：用 ScriptableObject 配置物品，支持背包、商店、经验升级和 15 个技能节点。界面使用 uGUI、TextMesh Pro 和 Prefab。
- **本地与云存档**：每个账号有 3 个槽位，支持手动保存、自动保存和死亡读档。本地 JSON 通过临时文件替换并保留备份。云存档先写本地，再由 Node.js HTTP 服务同步至 MongoDB，使用版本号检测写入冲突，用请求 ID 识别重复上传。

## 本地运行

1. 在 Unity Hub 中添加这个仓库的根目录，使用 **Unity 2022.3.62f2c1** 打开，等待资源导入完成。
2. 只体验单机玩法时，将 [DatabaseConfig.json](Assets/Resources/DatabaseConfig.json) 中的 `enabled` 设为 `false`，从 [LoginScene](Assets/Scenes/LoginScene.unity) 进入 Play Mode，注册本地账号并选择存档槽位。
3. 只想快速查看村庄场景时，也可以直接运行 [SampleScene](Assets/Scenes/SampleScene.unity)。直接进场景没有登录会话，适合调试玩法。

使用 **WASD 或方向键**移动，按 **E** 交互。登录并进入存档后可按 **F5** 手动保存。其他按键可在游戏设置中查看。

要在本机体验账号服务和云存档，先启动 MongoDB，再使用 Node.js 20 或以上版本，在 `Server` 目录执行 `npm install` 和 `npm start`。将 `DatabaseConfig.json` 中的 `enabled` 设为 `true`，并将 `apiUrl` 设为本地接口 `http://127.0.0.1:5080/api`。注册邀请码、环境变量和部署说明见 [后端文档](Server/README.md)。

## 代码导航

| 目录 | 内容 |
| --- | --- |
| [Assets/Scripts](Assets/Scripts) | 角色、战斗、任务、森林生成、UI 和存档逻辑 |
| [Assets/Scenes](Assets/Scenes) | 登录、村庄和森林场景 |
| [Assets/Editor](Assets/Editor) | 森林与 UI 安装工具、树木布置工具 |
| [Server](Server) | 账号与云存档接口、后端集成测试 |
| [Tools/Tests](Tools/Tests) | 任务规则、本地存储和后端启停脚本检查 |
