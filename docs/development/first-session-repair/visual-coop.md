# 首局画面与双人横屏验收记录

本记录对应 `HairSalonDemo` 的浏览器 WebGL 包。验收只使用 Playwright 对可见 canvas 发送真实鼠标点击和摇杆拖动；没有写入 PlayerPrefs、注入 Unity 状态或启动 Unity。640×360 是窄横屏浏览器模拟，不能替代真实 iOS/Android 设备验收。

## 证据位置

- 基线：`unity-hair-salon/Builds/FirstSessionVisual/before/`
- 正常双人入口基线：`unity-hair-salon/Builds/FirstSessionVisual/after-normal-coop/`
- 失败入口保留：`unity-hair-salon/Builds/FirstSessionVisual/after-failed-mobileEvidence-entry/`
- 最终正常入口报告：`unity-hair-salon/Builds/FirstSessionVisual/final/`（单人/双人 × 844×390、960×540、640×360，共 6 runs、39 张图）
- 最终 direct telemetry 报告：`unity-hair-salon/Builds/FirstSessionVisual/final-telemetry2/`（双人三尺寸、9 张图；每轮已确认 `state=Business`、`coop=true`）
- 误用入口失败证据：`unity-hair-salon/Builds/FirstSessionVisual/final-telemetry/`（仍为 `state=Starting`，不可用于镜头验收）
- P2 服务探测失败证据：`unity-hair-salon/Builds/FirstSessionVisual/p2-probe/`；该轮误点 P1 交互按钮，最终 `completed=0`、首客仍 `Waiting`，不宣称 P2 服务通过。P2 实际服务由主代理另行复测。
- 新一轮完整报告：运行 `python3 tools/check-first-session-visual.py --label after` 后写入 `unity-hair-salon/Builds/FirstSessionVisual/after/`
- 检查脚本：[tools/check-first-session-visual.py](/Users/kker/Documents/ChatGPT/game2/tools/check-first-session-visual.py)

脚本覆盖 844×390、960×540、640×360 三个 viewport，分别进入单人和双人模式，保存模式/进度/准备页/营业静止画面，并用左右虚拟摇杆拖动角色。每张截图都会校验 canvas 尺寸、非空和 SHA-256；canvas 出现后等待 12 秒再采集。双人正常入口使用 `?multiplayerEvidence=1`，只有出现 `[MULTIPLAYER_READY]` 与 `[MULTIPLAYER_FLOW_PASS]` 才接受为双人。direct telemetry 入口点击准备页后还必须从只读 `MOBILE_STATE` 看到 `state=Business` 且 `coop=true`，否则整轮记为失败。

最终包引用 `unity-hair-salon/Builds/FirstSessionFinalWebGLDemo.log`，不是历史的 `Builds/BuildWebGLDemo.log`。所有截图均由 Playwright 鼠标点击/拖动产生；窄横屏是浏览器尺寸模拟，不能替代真实 iOS/Android 触控验收。

## 当前基线结果

正常双人入口三种尺寸均确认两套摇杆、两名角色和独立移动日志。844×390 的初始营业画面中两名角色在店内下方区域；移动 P1 向右上、P2 向左下后，两名角色仍在同一画面内，P2 靠近右上隔断但没有出屏。960×540 的可读性最好，640×360 仍能看清角色和主要家具，但空间与文字更紧。

已复现的画面问题：

1. 底部“靠近顾客”交互按钮覆盖顾客、气泡和长椅的屏幕区域；640×360 的覆盖最明显，按钮与顾客阅读区同时出现。建议主代理在现有 HUD 安全区内缩小或上移交互按钮，确保顾客脚底、气泡和目标家具仍可见，再由同一脚本复测；不要改变交互规则。
2. `final/960x540-single-business-p1-moved.png` 中 P1 到后墙角时上半身落在顶部任务条下方；头脚仍在画面内不等于角色无遮挡。
3. 窄横屏顶部任务条和左侧订单条的间距接近安全边界。当前截图没有硬裁切，但 640×360 应继续作为回归尺寸检查字体与第二行文字。
4. 双人分开后 P2 靠近右上隔断，角色仍可见；建议保留同一镜头边界约束，检查其头脚屏幕坐标与家具重叠，不能只看世界坐标。
5. 之前带 `mobileEvidence=1` 的运行会绕过正常模式/存档卡，导致文件名看似双人但实际 `coop=false`、只有一套摇杆。该失败证据保存在 `after-failed-mobileEvidence-entry/`，不得当作双人通过。

## 九项视觉 checklist

报告中的每项必须填写 `pass`、`issue`、`not-applicable` 或 `baseline-missing`：

| 类别 | 当前判定依据 |
|---|---|
| resources | 浏览器 console、pageerror 和 requestfailed；当前包无错误、无丢失资源 |
| proportion | 三尺寸营业截图；重点看角色相对家具和右侧空区是否改变 |
| position | 静止与拖动前后截图；重点看开局角色是否贴边、目标是否留在可见区 |
| direction | 与 `Docs/VisualReferences/salon-overview-visual-reference.png` 对照；当前背墙/右墙等距方向一致 |
| shadow | 角色脚底、家具接地和阴影偏移需人工看截图；脚本不以像素差代替视觉判断 |
| occlusion/depth | 两人分开截图；分开后角色应在屏内，交互按钮不应覆盖顾客/气泡关键区域 |
| character size | 看头顶留白、脚底接触和与椅子/柜台关系；不能只看 telemetry 点 |
| safe area | 844×390、960×540、640×360 全部检查 HUD、摇杆、交互按钮和底部提示 |
| stability | canvas 出现后等待至少 12 秒，截图必须非空；记录 console/request 结果 |

## 主代理集成建议

主代理只需把交互按钮安全区、顶部任务条遮挡、队列条第二行文字和任何摄像机变化纳入自己的生产文件修复；本 worker 不修改 `SalonDemo*.cs`、场景、Manifest 或已批准美术。最终正常入口 6 runs 和 direct telemetry 3 runs 已覆盖画面范围；`MOBILE_STATE` 记录了三尺寸的 `playerScreen/playerHeadScreen/playerTwoScreen/playerTwoHeadScreen`、镜头位置/尺寸、工位 `usable`、按钮/摇杆中心。P2 首单服务不在本 worker 的通过结论中，等待主代理的独立实服务证据。

## 验收边界

本记录是浏览器横屏视觉回归证据，不能宣称真实手机触控、GPU 性能或设备刘海安全区已经验收。角色可见性、家具遮挡、气泡/按钮覆盖和接触阴影应分别记录，不能把“角色仍在画面内”当作“家具无遮挡”。


## 主代理补充：P2 真实服务已通过

最终独立证据位于 [FirstSessionCoopService](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopService/)。主代理使用 844×390 Chromium CDP touch，实际操作右侧摇杆和按钮完成 P2 接待、安排入座、剪发；完成数 1、余额 120。只读日志记录到 P2 正在服务顾客 0，P1 全程未执行服务，浏览器错误为空。该结果覆盖实际首单服务，不代表双人整日协作已验收。

[完成截图](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopService/05-p2-completed.png) · [操作报告](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopService/report.json)。root 首次驱动使用过小的导航到达半径，未找到长椅外的可达点；该失败记录保存在 route-radius-failed/，改为现有有效到达半径后通过，游戏和存档未被注入或修改。
