# 首局体验修复验收记录

日期：2026-09-28。当前状态：本轮修复、构建与浏览器验证完成；剩余体验问题列在下方。

[打开正式 Demo](http://127.0.0.1:8910/WebGLDemo/) · [打开资产实验室](http://127.0.0.1:8910/WebGLAssetLab/)

从正式入口选择单人或本地双人、选择一份进度，再开始营业。后续重新启动可双击项目中的“启动试玩.command”。

## 玩家能看到的变化

- 镜头按现有斜角视角跟随，角色走到边角时保持在画面内；双人分开行动时调整取景。
- 第二位玩家可以把洗完头的顾客转移到吹发工位，也能纠正错误工位安排。
- 未显示的货架施工点不再扣钱；理发工位扩建完成后，下一处货架施工点会正常出现，已有投入仍可继续。
- 施工点显示中文用途、当前投入和所需金币，完成后说明实际获得的能力。
- 当前任务会提示待接待、待冲洗和吹发收尾，并显示手持用品和货架库存；正在主动服务时仍更新其他任务提醒。
- 操作按钮、底部指引和队列信息提高可读性。日结明确区分目标达成与未达成，并说明保留收入和施工进度。

## 本轮没有靠改难度掩盖问题

营业仍为 180 秒，收尾最多 15 秒，首日目标仍为 3 单。保留当前持续营业规则：达成目标后可以继续接待；未达标也结算收入与投入，之后可继续下一天。核心玩法、家具布局、角色和美术方向保持原有批准范围。

之前一次触控记录的 1/3 并非游戏无法通关的充分证据。本轮复现发现，旧自动试玩会把未解锁的洗发工位选作目的地。现在检查工具读取实际可用工位，继续使用真实触控，不修改游戏状态。

## 验收方式

1. 从正常入口选择单人或双人，再选择一份进度并开始营业。
2. 用摇杆靠近顾客，接待后带到工位；观察人物、目标与按钮是否一致。
3. 洗发缺货时去后场取用品，再到洗发架放下；打好泡沫后离开，看到待冲洗提示再回来。
4. 洗发结束后转移顾客到理发工位吹发；双人模式由第二位玩家也能完成这一步。
5. 收入足够后走入理发工位施工圈，观察扣款与新工位开放；之后才看到补货架施工圈。
6. 等本日结算、继续下一天，退出后重新打开同一份进度检查保存结果。

## 验证记录

- Node 资产管线：17/17 通过。
- Python 工具回归：最终 8/8 通过。
- 最终 Unity 全量检查：716/716 通过，失败为 0。
- Manifest 与资源校验：18 项资产通过；377 个所需中文字形均已包含。
- 最终 WebGL Demo 与资产实验室：构建成功；候选资产与参考技术场景本轮也已成功构建、启动。
- 正式 Demo 核心服务与资源浏览器检查：通过，无控制台错误或资源加载失败。
- 两天真实触控营业：首日 5/3 单、次日 6/4 单；余额 1100 → 2500，刷新后经营进度和满意度保存通过，浏览器错误 0。剪发提前松手、暂停继续、剪过头不计目标，以及后台服务期间处理其他顾客均已覆盖。
- 三种横屏尺寸（844×390、960×540、640×360）的正常入口：单人/双人共 6 组，39 张截图，无浏览器错误或缺失资源。
- 真实收入扩建：完成剪发和洗吹两单，合计收入 420；花费 180 开放新理发工位，余额 240，下一处补货架施工圈出现。刷新后金币和开放工位保留，隐藏货架圈没有扣款。
- 双人实际服务：第二位玩家通过右侧摇杆和按钮独立完成接待、安排入座、剪发，完成 1 单、到账 120；第一位玩家全程未执行服务。浏览器错误 0。
- [第二位玩家完成服务截图](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopService/05-p2-completed.png) · [第二位玩家操作报告](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopService/report.json)。
- [扩建完成截图](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionGrowth/07-haircut-expansion-complete-rack-visible.png) · [扩建保存报告](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionGrowth/report.json)。
- [首日日结截图](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/05-result.png) · [次日日结截图](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/11-day2-result.png) · [完整试玩录屏](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/video/page@8c479c6c9b64a9138b6a6a89dbf72ef4.webm) · [两日触控报告](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/report.json)。
- 检查记录：[验证汇总](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionValidation/validation.json)。

## 仍需产品体验判断的问题

- 两天虽然都达标，但仍分别流失 1 位、2 位顾客，满意度均降到 0。真实输入验证了流程和保存，不代表当前经营压力、回访节奏已经舒服；本轮保留原有数值。
- 双人窄横屏的底部操作按钮仍会覆盖部分等候区；单人走到后墙角时，上半身可能与顶部任务条重叠。人物未出屏与“完全无遮挡”是不同结论，下一轮应继续处理 HUD 对当前操作区域的遮挡。
- 当前有占位顾客与周边资产，整店视觉完成度尚未获得新的产品批准。

## 边界

- 本轮使用真实 Chromium 浏览器输入及手机横屏尺寸模拟，尚未在实体 iOS/Android 手机上验收。
- 两天完整营业为单人触控验证；双人验证覆盖正常入口、独立移动与第二位玩家完成首单，尚未宣称双人整日协作已经通过真人验收。
- 刷新恢复到保存的经营进度/准备阶段，不承诺恢复顾客位置和正在进行的服务现场。
- 美术工作集中于镜头、可见性和现有文字表现；未用占位素材替换已批准资产，也未宣称整店美术已获批准。

内部复现和分工记录见同目录 camera.md、feedback.md、full-day.md、pacing-growth.md、visual-coop.md。
