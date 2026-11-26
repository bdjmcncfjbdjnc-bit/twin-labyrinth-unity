# 运行当前 Unity 3D 联机版

仓库根目录仍保留旧版 2.5D 项目。**当前 3D 版工作目录是 `UnityURP`**；根目录的 `npm start` 不会启动当前版本。

## 使用发行包

安装 Node.js 22 或更新版本，解压 Releases 中的 WebGL 服务包。在解压目录运行 `start-demo.cmd`（Windows），或：

```sh
TWIN_COOP_BUILD=Coop-Combat-Web node Server/coop-server.cjs
```

浏览器打开 `http://127.0.0.1:8792`。两名玩家可用两个独立浏览器会话测试；创建房间后分享邀请地址。

服务默认只监听本机回环地址。异地联机需使用反向代理或隧道把同一个端口发布为 HTTPS，并支持 `/coop` WebSocket 升级。不要把 `127.0.0.1` 邀请地址发给异地朋友。房间只存在内存中，服务器重启会清空。

## 从源码构建

1. 安装 Unity 6000.3.23f1 和 WebGL Build Support。
2. 用 Unity Hub 打开 `UnityURP`，等待包解析和素材导入。
3. 使用 Unity 命令行执行：

```text
<Unity executable> -batchmode -quit -projectPath <absolute UnityURP path> -buildTarget WebGL -executeMethod CoopBuilder.Build -logFile <build log path>
```

4. 构建成功后，在 `UnityURP` 内运行 `node customize-combat.cjs`，只对新生成的 HTML 运行一次。
5. 在同一目录启动服务：

```powershell
$env:TWIN_COOP_BUILD='Coop-Combat-Web'
node Server/coop-server.cjs
```

产物位于 `Builds/Coop-Combat-Web`。构建会更新独立联机场景并重新导出 `Server/map.json`，发布时应使用同一构建对应的地图。许可文件来自工程内 `ThirdPartyNotices.txt`，不需要旧构建目录。

首次运行无需执行 `Stage-Art.ps1` 或重新下载资产；这些是制作阶段的辅助脚本。已导入的公开授权素材随工程保留。

## 测试

服务端测试无须下载 Unity 或 Playwright：

```sh
cd UnityURP
npm test
```

实际浏览器测试需要已构建并启动的游戏：

```sh
npm install
npx playwright install chromium
npm run test:browser
```

`COOP_BROWSER_URL` 可指定测试网址，默认 `http://127.0.0.1:8792`；`CHROME_PATH` 可指定已有 Chrome。截图和运行日志写入 `Logs`，不提交到源码目录。

## 部署边界

这是开发测试服务，尚未完成正式公网运营的完整安全和负载验证。没有账户、持久存档、内置语音或高延迟预测。发行包不是原生游戏 EXE，必须通过 Node 服务加载 WebGL，不能双击 HTML 直接运行。
