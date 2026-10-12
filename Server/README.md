# RPG MongoDB backend

Database: `rpg_game`; collections: `accounts`, `saves`, `sessions`.

Start MongoDB, then run `npm install` and `npm start` in this directory using Node.js 20+.
Or after installation run `node server.mjs`. Keep the API running while playing Unity.
On this workstation you can also run `powershell -ExecutionPolicy Bypass -File Server/StartServer.ps1` from the project root.
Check `http://127.0.0.1:5080/health`.

The backend uses `RPG_MONGO_URI` (default `mongodb://127.0.0.1:27017`) and `RPG_API_PORT` (default 5080).
Driver connection/index APIs follow the [official MongoDB Node.js documentation](https://www.mongodb.com/docs/drivers/node/current/connect/mongoclient/).
Set authenticated connection strings through the server environment, never Unity assets or chat.
The service binds to loopback only; an HTTPS proxy provides public access. For friend playtests without client-side Tailscale,
see `deploy/PUBLIC-PLAYTEST.md` (host-side Funnel). For a dedicated server, see `deploy/DEPLOYMENT.md` and Caddy templates.
Unity authentication and uploads now use asynchronous requests. Saves are local-first with pending-upload recovery,
optimistic cloud revisions and idempotency IDs. Cloud conflicts require an explicit KEEP LOCAL / USE CLOUD choice.
This is cloud saving for a single-player game, not server-authoritative multiplayer or anti-cheat.

`Assets/Resources/DatabaseConfig.json` enables database-backed registration/login/three save slots.
Set `enabled` to `false` to explicitly return to the existing local-account login/register mode. There is no silent fallback.
Existing local JSON accounts/saves are untouched and are not automatically imported into the database.
Passwordless save-group switching has been removed. Files created during that mode remain on disk;
if an account's cloud copy replaces a different local cache, the cache is archived as `.offline-backup`
when `offline-profile.json` is present. Offline-only progress is not automatically uploaded or merged.
Database accounts need registration separately. Passwords use salted scrypt hashes; sessions expire after one day
and can be revoked on logout. Only token hashes are stored in the sessions collection; backend restarts retain sessions.

Save updates are atomic per slot and preserve the previous data in `previousData`.
After updating this server, stop the old process with Ctrl+C and restart `StartServer.ps1`, then log in again in Unity.
The old server cannot process the new revision/idempotency protocol. Existing saves without revisions are treated as revision zero.
Gameplay saves include scene/position, health/level/skills, inventory/gold, quest progress and terrain/NPC state.
Audio/display settings remain local PlayerPrefs. World tree/enemy persistence is not part of the existing save model.

Cloud loads include the last upload receipt, so a lost acknowledgement does not create a false conflict on re-login.
Failed slot initialization clears the partial client session instead of treating unread slots as empty slots.
Expired sessions stop automatic upload attempts until sign-out and re-login; local saving continues.
Conflict resolution validates the cloud copy and uses unique backup names to retain earlier conflict backups.

Integration regression tests: `npm test` starts a separate backend and a fresh, randomly named test database,
then removes only that database. It covers invitations, closed registration, old-account login, throttling and authenticated deletion,
plus the existing save recovery suite. MongoDB must be running; it never uses the production database for fixtures.
For the standalone `node smoke-test.mjs`, set `RPG_TEST_API` and `RPG_TEST_INVITATION` explicitly in the test environment.
The tests create and remove only their own temporary account, saves and sessions. They verify upload receipts,
concurrent retries, revision conflicts, authorization, logout and expired-session rejection.
These tests do not replace Unity Play Mode checks or production HTTPS deployment verification.

## 手动开启 / 关闭后端

在 Server 文件夹双击 `StartServer.cmd` 开启，看到 `RPG API ready` 即成功；保持该窗口开启。
双击 `StopServer.cmd` 关闭本项目后端，也可以在启动窗口按 Ctrl+C。
这两个入口会保留错误信息，不会像直接双击 ps1 一样出错后立即消失。
StartServer 检查已有进程、端口及项目启动锁，重复点击不重复启动，也不替换占用端口的其他程序。
StopServer 同时核对 Node 可执行文件、完整 server.mjs 路径、命令行和进程创建时间，
只停止本项目 API，不使用“关闭全部 node.exe”。它不关闭 MongoDB/Tailscale，不删除账号、存档或邀请码。
为避免误停其他同名程序，无法确定目录的相对路径命令（如 node ./server.mjs）不会被停止脚本接管；这类旧窗口请自行 Ctrl+C 结束。
旧的游戏内自动启动逻辑和相关配置字段已移除，即使旧配置保留自动启动字段，游戏也不会使用它。
打开登录场景不会启动或探测后端；点击登录/注册才请求 API。后端未开启时提示手动启动并重试，保留本地进度。
MongoDB 仍由现有 Windows 服务管理；没有创建新的开机任务、自动重启或后台常驻启动器。
Node.js、Server 依赖和运行中的 MongoDB 必须预先存在；不会自动安装或提权。
联网试玩保留数据库模式并使用已验证的远程 HTTPS 地址。
只发送 Unity 完整构建输出，不发送 Server 或数据库；详见 `deploy/PUBLIC-PLAYTEST.md`。

## 试玩注册邀请码

新账号注册必须提交 `invitationCode`；已有账号登录不需要邀请码，账号、密码和存档格式不变。
邀请码的 SHA-256 摘要只从主机 `Server/.local/playtest.json` 的 `registrationInviteHash` 读取。
也可以通过主机环境变量 `RPG_INVITE_SHA256` 覆盖；设为空字符串可关闭注册但保留登录。
缺少配置时注册默认关闭，配置格式错误时后端拒绝启动。邀请码不会写进 Unity、PlayerPrefs 或存档。
主机的 `Server/.local/INVITE-CODE.txt` 是本次随机生成的邀请码，单独私发给朋友；该目录已加入忽略列表。
注册输入框是 `LoginScene/LoginScene/LoginCanvas/AuthPanel/InvitationCodeInput`，只在联网注册模式显示。
接口保留每用户名 10 次/分钟、每来源 240 次请求/分钟，并增加每来源 40 次认证/分钟及最多 4 个并发认证。
默认不信任 X-Forwarded-For；本机反向代理的请求共享来源额度，适合少量朋友试玩，不是大规模公网运营方案。
`RPG_MONGO_DATABASE` 可指定隔离测试库，默认仍为 `rpg_game`；不要对正式部署随意修改此变量。
