# 给朋友试玩：不用安装 Tailscale

## 朋友需要什么

只需要完整的 Windows 游戏构建文件夹，以及你单独私发的试玩邀请码。
不用安装 Tailscale、Node.js 或 MongoDB，不用登录你的 Tailscale/Google 账号。
游戏仍使用原来的注册、登录和每账号三个存档。邀请码仅用于首次创建账号。

游戏 API：`https://akina.tail0c9e66.ts.net/api`。
检查地址：`https://akina.tail0c9e66.ts.net/health`。
这两个地址与之前相同，但 Serve 是私有入口，Funnel 才能供未安装 Tailscale 的朋友访问。

## 主机启动与首次公网授权

1. 这台电脑保持开机、联网、不休眠，Tailscale 已登录且在线。
2. 确认 MongoDB 服务运行；双击 `Server/StartServer.cmd` 手动开启后端，保持窗口开启。停止时双击 `Server/StopServer.cmd`。
3. 主机需要保留 `Server/.local/playtest.json`，其中是邀请码摘要。不要删除或打包发出去。
4. 运行 `Server/deploy/StartPublicPlaytest.ps1`。它先检查更新后的本机 API 和邀请码配置，再只发布游戏 API。
5. 如果首次提示 Funnel 未启用，在命令输出的 Tailscale 授权链接里亲自确认。它会修改网络策略的 Funnel 权限。
   这只是允许发布的权限；还需 CLI 成功输出 `Available on the internet` 才表示入口实际公开。
6. 保留现有仅游戏 HTTPS 443 的私有网络访问规则，不改回任意设备任意端口。不要开启 MongoDB、远程桌面、文件目录的公网发布。
7. 用没有 Tailscale 的朋友电脑，或手机关闭 Wi-Fi 后使用移动网络，打开 `/health`，应返回 `ok: true`、`protocol: 2`、`registrationReady: true`。
   主机自己访问成功不代表公网访问成功。DNS 首次更新可能需要几分钟，官方说明最多约 10 分钟。

首次网页授权可能默认允许网络成员使用 Funnel。若不需要该范围，在管理页保留其他规则，
将新增 `nodeAttrs` 的 `funnel` 权限限定为你的主机所属账号或专用设备标签，不授予朋友管理员权限。
不要直接覆盖整个网络策略。朋友无需加入这个私有网络。

Funnel 的公开访问不受之前的“仅网络成员访问 443”规则限制；游戏接口由 HTTPS、游戏登录、邀请码和限流保护。
公网任何人都可能请求接口，邀请码不能当作完整的公网运营安全方案。只给少量朋友试玩，避免使用其他网站的相同密码。
不配置路由器端口映射，不开放 5080 或 27017；Node 和 MongoDB 保持 `127.0.0.1` 监听。

## 邀请码与打包

- 在主机打开 `Server/.local/INVITE-CODE.txt`，把这一行邀请码单独私发给朋友。不要发送服务器配置、数据库、日志或自己的登录凭据。
- Unity 的 LoginScene 切换到 CREATE ACCOUNT 后出现 Playtest invitation code 输入框。已有账号直接 LOG IN，不用填写。
- 固定输入对象：`LoginScene → LoginScene → LoginCanvas → AuthPanel → InvitationCodeInput`。
  编辑模式下默认隐藏，可在 Hierarchy 选中调整；运行时仅注册模式显示，并临时为第四个输入框扩展面板。
- 重新构建游戏后发送完整构建文件夹（exe、对应 Data 文件夹、UnityPlayer.dll 等），不要只发 exe。
  旧构建没有邀请码输入框，不能用来创建新账号。Server 不需要也不应包含在构建文件夹中。
- API 地址已在 `Assets/Resources/DatabaseConfig.json` 设置好。游戏内自动启动代码已移除，主机和朋友的客户端都不会启动后端。
- 先用新的测试账号验证注册、保存、退出、重新登录和恢复。重要进度先备份，不拿正式存档测试删除。

## 关闭公网入口 / 改回私有

```powershell
& 'C:\Program Files\Tailscale\tailscale.exe' funnel status
& 'C:\Program Files\Tailscale\tailscale.exe' funnel --https=443 off
```

关闭入口不删除账号或存档，也不停止 Node/MongoDB。不要使用 reset 清理无关服务。
改回仅 Tailscale 网络访问时：

```powershell
& 'C:\Program Files\Tailscale\tailscale.exe' serve --bg --yes --https=443 http://127.0.0.1:5080
```

不要用上述 serve 命令启动公网试玩；同一个端口最后执行的是 serve，就会重新变为私有。
`--bg` 让 Funnel 随 Tailscale 恢复入口，但不负责开机启动 Node 或 MongoDB。
重启电脑后先确认 MongoDB 服务运行，再双击 StartServer.cmd；Node API 只由你手动开启，不会随游戏自动启动。
主机关机、休眠、断网或手动关闭后端时，朋友不能联网登录或同步存档。

## 已完成和待验证

代码编译、独立数据库回归测试、本机更新后端检查已通过；未修改正式账号或存档。
公网权限、实际 Funnel 状态和朋友设备的游戏流程须分别验证。文档和配置文件本身不表示已成功公开。

参考：[Funnel 官方说明](https://tailscale.com/docs/features/tailscale-funnel)、[CLI 操作](https://tailscale.com/docs/reference/tailscale-cli/funnel)。
