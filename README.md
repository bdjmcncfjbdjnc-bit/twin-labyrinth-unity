# 双生迷城 — Unity 简模版

房间为 4×4 / 6×6 格，比原版本各缩小一格。所有墙壁、人物、武器、灯具、天气颗粒均由几何体组成，使用纯色材质，不导入图像贴图。文字使用系统字体。

## 打开游戏

1. 若从 GitHub 获取，先下载并解压 `TwinLabyrinthUnity.zip`。安装 Unity 2022.3 LTS（内置渲染管线），在 Unity Hub 中添加包含 Assets / Packages / ProjectSettings 的工程文件夹。
2. 首次导入会生成 `Assets/TwinLabyrinth/Scenes/Main.unity`。也可以通过菜单 **Twin Labyrinth → Prepare scene** 生成。
3. 打开 Main 场景，点击 Play。可选单人 AI、本地双人分屏或远程双人。
4. 菜单 **Twin Labyrinth → Build Windows / Build WebGL** 输出到 Builds 文件夹。需在 Hub 安装对应平台模块。

当前交付为源工程，制作环境没有 Unity Editor，尚未进行 Unity 导入、画面、音频播放和平台构建验证；不包含已编译的 EXE/WebGL。独立 C# 核心已编译并测试。

## 操作

- A：WASD 移动，鼠标或 Q/E 转向，R/F 抬头低头，鼠标左键/J/空格挥刀。
- 本地 B：上下方向键前后移动，左右方向键转向，逗号/句号抬头低头，Enter 挥刀。
- M 地图，Esc/P 暂停菜单，N 音乐开关，T 重新开始。远程服务器不会随菜单暂停。
- 雷雨躲入房间；浓雾注意紫色腕环；血月 A 靠近挥刀为 B 解毒；沙暴房内两条命会触发爆炸。完成五种天气后，两人一起到 24 号房逃离。

## 音效与美术

包含程序合成的脚步、湿地脚步、挥刀、命中、护甲破裂、雷声、爆炸、倒计时、解毒、胜负提示、呼吸、心跳，以及五种天气环境声和背景音乐。此处是完整事件覆盖的合成音效，不是实录音效。环境包含几何灯具、门牌、家具、出口、坍塌碎块，人物带四肢步行动画。音量可在菜单调节。

## 联机服务

公网服务已部署并验证：https://twin-labyrinth-server-production.up.railway.app

客户端已预填此地址。该网页是服务说明页，游戏在 Unity 客户端中运行。验证日期：2026-09-28；已通过 HTTPS 建房、双人加入、共享状态、地图同步和错误令牌拒绝测试。Railway 当前账户为试用额度，控制台显示剩余 26 天或 5 美元；服务续期由账户额度决定。

仓库根目录是独立服务器；解压的 Unity 工程中服务器在 Server 文件夹。在服务器目录执行 `node server.js`，默认端口 8787。客户端输入服务器 HTTP(S) 地址，创建房间后另一人输入四位码加入。原生客户端无需 CORS；WebGL 客户端部署到其他域名后，在服务器 `ALLOWED_ORIGINS` 中配置完整来源，多个来源用逗号分隔。正式客户端请使用 HTTPS。

服务使用内存房间，单实例运行。重启会丢失房间，暂不支持跨实例共享状态。掉线超过 4 秒由 AI 接管，10 分钟无人在线清理房间。房间令牌仅驻留客户端内存，重启客户端后无法恢复原席位。

仓库根目录 Dockerfile 可直接用于 Railway。公网服务只提供联机接口；浏览器可玩的版本需要先用 Unity 构建 WebGL，再部署 Builds/WebGL 内容。

## 验证

`node tests.js`（仓库根目录）或 `node Server/tests.js`（解压后的 Unity 工程） 验证服务器规则与房间接口。
`Tests/CoreTests.cs` 与 `LabyrinthDomain.cs` 可一起编译为控制台程序，验证 300 张地图及关键规则。

已通过：22,204 项 C# 核心断言、17 组 Node 测试、Unity 世界数据协议测试、公网双人接口测试。未验证：Unity Editor 导入及播放、3D 视觉效果、实际音频输出、Windows/WebGL 构建。

工程不包含密码、令牌或任何第三方素材。Unity 元数据由首次导入生成。
