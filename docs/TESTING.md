# 验证记录与边界

玩法版本：`coop-combat-20261124-r1`。区分历史运行结果与本次发布检查，不把截图或模拟当作真人完整通关。

| 检查 | 已记录结果 | 证据 |
| --- | --- | --- |
| 最终本地双 WebGL | 建房、邀请、双方拾刀、装备、行走/鼠标攻击、死亡同步、手电、第二人移动、刷新恢复通过 | [结果](test-evidence/Coop-Combat-Browser-Final-Result.txt) |
| 最终公网双 WebGL | 同一分享入口加载两个会话，持刀 NPC 造成伤害；交战、死亡同步和重连通过 | [结果](test-evidence/Coop-Combat-Public-Browser-Result.txt) |
| 完整实时网络回合 | 8078 帧、439.1 秒；三名 NPC、门、A、双人 B、共同出口通过 | [结果](test-evidence/Coop-Combat-Socket-Round.txt) |
| 真实网络救援 | NPC 实际攻击、一人倒地、持续救援，以及两人倒地失败通过 | [结果](test-evidence/Coop-Combat-Socket-Rescue.txt) |
| 公网 WSS | 建房、加入、手电与朝向、断线和认证恢复通过 | [结果](test-evidence/Coop-Combat-Public-Socket.txt) |

以上实测发生于 2026-11-24。完整网络回合由自动客户端发送合法输入，双 WebGL 由自动化浏览器操作；不是两名真人异地试玩。

首次公网浏览器测试曾因工具超时参数位置错误，在下载阶段提前退出。修正后重新运行通过，没有略过该问题。

## 可重复运行的规则检查

- `Server/rooms.test.cjs`：房间隔离、人数、移动、门、双人任务、出口、重连与 289 个区格中心。
- `Server/combat.test.cjs`：持有、拾取、方向、距离、冷却、伤害、死亡、误杀、连续救援、遮挡、NPC 警告/攻击/脱离和共同失败。
- `Server/combat-flow.test.cjs`：完整地图路线与战斗；可用 `COOP_ORDINARY=0`、`1`、`2` 指定普通囚犯位置。

## 美术证据

`*-web.png` 为实际浏览器截图。`*-unity.png` 为 Unity 相机离屏渲染的测试夹具截图，部分状态由夹具设置，不包含 HUD，也不证明玩家从起点走到了该位置。图片未作提亮或模型替换。

## 尚未完成

两名真人异地整局、持续高延迟和丢包、多档低端硬件与移动端测试尚未完成。手部动作、角色服装、第三人称手电实体与尸体贴墙姿态仍有不足。没有宣称“所有情况无 bug”。

详细制作记录见 [原始检查报告](../UnityURP/Coop-Combat-Review.txt)。其中临时网址和当时的本地提交状态属于历史记录，不代表当前托管状态。

## 2026-11-26 发布检查

在整理后的 GitHub 工作副本重新运行 rooms、combat 和 combat-flow 测试，均通过；浏览器测试脚本语法检查通过。修复构建脚本对旧 Builds/Blackout-Web 许可文件的依赖，并移除测试脚本的个人安装路径。这两项是发布可复现性调整，本次没有重新编译玩法二进制；发行包沿用 11 月 24 日验收通过的 WebGL。
