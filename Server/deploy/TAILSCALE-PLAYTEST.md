# 给朋友试玩：本机后端 + Tailscale 私有 HTTPS

> 本文保留为旧的私有网络方案。当前已选择“朋友不用安装 Tailscale”的公网 Funnel 方案，请使用 [PUBLIC-PLAYTEST.md](PUBLIC-PLAYTEST.md)。公网入口是否已启用以 `tailscale funnel status` 为准，不能只根据域名判断。

## 当前接入

- 主机：`100.110.213.25`，设备域名：`akina.tail0c9e66.ts.net`。
- Unity API：`https://akina.tail0c9e66.ts.net/api`。
- 健康检查：`https://akina.tail0c9e66.ts.net/health`。
- Tailscale Serve 将私有 HTTPS 443 转发到 `http://127.0.0.1:5080`。
- Node.js 和 MongoDB 仍只监听 `127.0.0.1`。没有启用公网 Funnel，没有开放 5080/27017。
- Unity 配置关闭了本地后端自动启动：朋友的电脑只作为客户端，不需要安装 Node.js 或 MongoDB。

已在主机上验证正常证书校验下 HTTPS 健康检查返回 200，未提供登录令牌的存档请求返回 401。
这不代表已验证朋友电脑的连通性、Unity 打包后的登录或存档流程。

## 邀请朋友前必须完成：访问规则

`tailscale-game-only.policy.example.json` 是待应用的规则模板，创建文件不会修改 Tailscale 网页设置。
它只允许已加入本网络的成员连接这台主机的 TCP 443，不授予远程桌面、文件共享、数据库或其他设备的连接权限。

1. 在 Tailscale 管理网页打开 Access controls，先查看并备份现有策略。
2. 这个模板适用于专门用于游戏试玩的新网络；如果已有其他服务或规则，先审核并合并，不要直接覆盖。
3. 移除现有“任意来源访问任意目的地任意端口”的放行规则。只追加限制规则不能抵消已有的宽泛放行规则。
4. 通过网页验证、保存策略，确认生效后再邀请朋友。
5. 邀请朋友时使用普通 Member 权限，不授予 Owner/Admin，不把自己的账号或密码给朋友共用。

此模板针对加入同一网络的成员，不用于跨网络的设备分享；改用设备分享时需要另行审核策略。

参考：[Grants 语法](https://tailscale.com/docs/reference/syntax/grants)、[策略示例](https://tailscale.com/docs/reference/examples/grants)。

## 游戏测试与打包

1. 保持主机 Tailscale 在线、MongoDB 和 Node 后端运行，不让主机休眠。
2. Unity 如果正在运行游戏，先停止再重新运行，从 LoginScene 测试自己的游戏账号登录。
3. 配置文件位于 `Assets/Resources/DatabaseConfig.json`，属于打包资源。必须重新构建游戏；之前打包的 exe 不会自动得到这个新地址。
4. 把完整构建输出发给朋友，不要只发 exe，也不要把 Server、数据库、日志或开发用文件一起发出去。
5. 朋友各自安装并登录 Tailscale，接受邀请后先测试 `/health` 地址，再用游戏内注册创建自己的游戏账号。
6. 用专门的测试存档验证保存、退出、重新登录、恢复，以及单槽删除。先备份重要进度，不拿重要存档测试删除。

Tailscale 账号用于连接网络，游戏账号用于游戏内登录、注册和存档，两者不是同一个账号系统。
当前只有主机连接测试完成，跨设备测试和实际游戏测试仍待完成。

## 主机重新启动后

Tailscale Serve 使用了 `--bg`，会在 Tailscale 恢复连接后继续提供入口，但它不会启动 Node.js 或 MongoDB。
确认 MongoDB 服务运行；如果后端尚未启动，在 PowerShell 中执行：

```powershell
& 'D:\Unity\Study\My project RPG\Server\StartServer.ps1'
```

保持该终端运行，Ctrl+C 可停止这次启动的后端。如果后端已正常运行，不要再次启动同端口的进程。
本次没有修改系统开机任务、休眠设置或防火墙，没有重启或替换已有的后端进程。

## 查看和停止私有入口

```powershell
& 'C:\Program Files\Tailscale\tailscale.exe' serve status
& 'C:\Program Files\Tailscale\tailscale.exe' serve --https=443 off
```

停止入口不会删除账号或存档，也不会停止 Node 后端。不要用 `serve reset` 清理无关服务。
如果继续使用本篇私有方案，不要启用 Funnel；主动切换到公网试玩方案时，按 PUBLIC-PLAYTEST.md 的授权和安全检查操作。
参考：[Serve 官方说明](https://tailscale.com/docs/reference/tailscale-cli/serve)。

## 改回仅本机开发

把 `Assets/Resources/DatabaseConfig.json` 恢复为以下内容，并停止上述私有入口：

```json
{
  "enabled": true,
  "apiUrl": "http://127.0.0.1:5080/api"
}
```

重新运行或重新构建后生效。后端仍需双击 Server/StartServer.cmd 手动开启。
改回本机地址不会删除云端或本地的游戏进度，但不能用于朋友远程连接。
