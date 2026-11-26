# 双人联机近战测试版

入口：https://forum-monitoring-purse-inputs.trycloudflare.com/?v=coop-combat-20261124-r1

使用电脑浏览器与键鼠。首次需加载约 150 MB 的 Unity WebGL 数据。一人点击「创建双人房间」，把六位房间码或创建后的浏览器地址发给朋友；另一人输入房间码加入，也可直接打开邀请地址。两人连接后，各自点击「进入监区」。同一电脑测试时使用两个独立浏览器会话。

WASD 移动，鼠标观察，Shift 跑步，F 开关门／操作配电，L 开关手电，Esc 打开自己的菜单。浏览器失去鼠标锁定后，点击画面重新控制视角。游戏没有内置语音，建议同时使用语音通话。

本版目标：找到配电 A 并接通 → 两人一起靠近配电 B 并操作 → 两人共同到达出口。门、配电、出口结果由服务器同步；朋友的位置、朝向、行走动作和手电也会同步。两人都在线才能继续探索，菜单不会暂停朋友。

本版恢复三名囚犯（一名普通、两名隐藏持刀），支持拾刀、自卫、受击倒地、持续救援和共同失败；狱警、随机协议及旧任务链暂不启用。起点桌上 F 领取刀，1 手电、2 刀、鼠标左键攻击；靠近倒地朋友按住 F 两秒救援。误杀计入结算，不开放队友伤害。每人开局持有手电，取消电池耗尽和随机熄灭，保留走跑、精力与喘息。增加近身补光，并限制同时参与照明的走廊灯，避免手电被渲染管线灯光数量上限挤掉。

## 保存与重连

刷新当前标签页会凭会话令牌返回原来的角色位置。断线后自动重试；两人都离线后，房间保留两分钟。请不要把邀请地址中的房间码当作账户认证：本版没有账户、长期存档、语音或公开房间列表。房间满员会拒绝第三人；离开后可在原标签页重新加入尚未过期的房间，原角色席位暂不开放给其他人。

## 本地启动与构建

需 Node.js，先设置环境变量 `TWIN_COOP_BUILD=Coop-Combat-Web`，再运行 `node Server/coop-server.cjs`，默认仅监听 `127.0.0.1:8792`。`TWIN_COOP_PORT` 可覆盖端口。静态文件由 `TWIN_COOP_BUILD` 指定，当前为 `Builds/Coop-Combat-Web`（未设置则兼容旧目录）；WebSocket 路径为 `/coop`，健康检查为 `/health`。远程试玩需要将此端口通过 HTTPS/WSS 代理发布。本次提供的是临时 Cloudflare 隧道，依赖本机服务和隧道进程持续运行，不属于持续托管服务。

Unity 6000.3.23f1：执行 `CoopBuilder.Build`（目标 WebGL），成功后运行 `node customize-combat.cjs`。后者仅对刚生成的网页执行一次。独立场景为 `Assets/ArtReview/Coop/PrisonOnline.unity`，原 `Assets/ArtReview/HauntCoop/PrisonCoop.unity` 和旧单人构建保留。地图碰撞与交互数据由构建器导出到 `Server/map.json`。

服务器依赖 `vendor/package` 中的 ws 8.23.0，来自官方 npm 包，采用 MIT 许可，许可证随包保留。既有人物、环境、音频和字体的来源记录仍在项目内，网页提供原有资产声明入口。

## 验证与边界

- `node Server/rooms.test.cjs`：房间容量与隔离、非法移动、门状态与防夹、供电前置、两人共同出口、会话恢复、地图区格检查。
- `node Server/flow.test.cjs`：通过地图可行走路径，模拟两名玩家完成 A、B 和共同出口。7365 个有效移动帧，模拟时间 368.3 秒；这是服务端完整流程测试，不能等同于两名真人完整试玩。
- `node Server/socket.test.cjs`：实际 WebSocket 建房、加入、状态同步、断线与恢复。可设置 `COOP_WS_URL` 验证公网 WSS。
- `node Tools/coop-browser.test.cjs`：在两个独立 Chromium 会话实际加载 Unity WebGL，验证创建、邀请地址、双方移动、手电开关同步、刷新恢复及运行时异常。此工具目前使用本机的 Playwright 和 Chrome 路径。

当前没有宣称完成异地双人整局人工验收。网络位置具有平滑处理和基础服务端校验，尚未实现高延迟预测与完整反作弊；服务器重启会丢失房间。联机版保留现有美术资产，本轮未新增人物或重制监狱。

完整近战功能、最新实测和美术边界见 [迭代检查报告](../Coop-Combat-Review.txt)。新增测试：`combat.test.cjs`、`combat-flow.test.cjs`、`socket-round.test.cjs`、`socket-rescue.test.cjs` 和 `Tools/coop-combat-browser.test.cjs`。
