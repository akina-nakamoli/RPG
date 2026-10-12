# 游戏脚本目录

代码按功能模块分类，文件夹名不影响脚本类名。移动现有 Unity 资源时必须保留对应 `.meta` 文件和 GUID。

| 目录 | 用途 |
| --- | --- |
| `Core` | 全局音效、预制体目录、场景切换 |
| `Characters/Player` | 玩家移动、战斗、生命与经验 |
| `Characters/Heroes` | 英雄形象、能力与切换 |
| `Characters/Enemies` | 敌人移动、战斗、生命与生成 |
| `Characters/NPC` | NPC 巡逻与对话数据 |
| `Quests`、`Quests/Escort` | 任务进度与 jame 护送逻辑 |
| `World/Buildings` | 建筑、入口与传送交互 |
| `World/Forest` | 森林地图、迷雾、宝箱与森林状态 |
| `World/Trees` | 树木与受保护区域 |
| `World/Tiles` | 地形边界与高低地入口 |
| `World/Rendering` | 世界、建筑深度排序与网格效果 |
| `MiniGames/TargetRange` | 靶场玩法、计分与英雄招募流程 |
| `Inventory` | 背包、物品类型与掉落 |
| `Shop` | 商店购买与导航 |
| `Stats` | 角色属性 |
| `Skills` | 技能树 |
| `Networking` | 账号与云存档的联网接口 |
| `SaveSystem` | 存档结构、本地账号、存档会话与恢复 |
| `UI/HUD` | 角色血条、伤害数字 |
| `UI/Panels` | 游戏菜单、设置、属性与死亡面板 |
| `UI/Account` | 登录、注册、存档操作界面 |
| `UI/Dialogue` | 对话界面 |
| `UI/Quests` | 任务日志与任务大厅界面 |
| `UI/MiniGames` | 靶场界面与可编辑布局参数 |

## 代码与资源的边界

- UI 显示和按钮绑定放在 `UI`；玩法规则放在相应模块。
- 物品配置放在 `Assets/Data/Items`，不和 C# 脚本混放。
- 预制体统一放在 `Assets/Prefabs`；`Resources` 中运行时加载的资源和目录引用维持原路径。
- 编辑器工具放在 `Assets/Editor`，后端放在项目根目录的 `Server`，独立测试放在 `Tools/Tests`。
- 此次整理不修改脚本名称、类名、命名空间或程序集划分，也不改变后端的手动启动方式。

以后移动资源时，优先在 Unity 的 Project 面板中操作。若在文件系统中移动，必须同时移动对应 `.meta`。移动后检查 Console，并确认硬编码资源路径和测试路径也已更新。
