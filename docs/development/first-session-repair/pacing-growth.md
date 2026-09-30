# 首局节奏与首个扩建实证

日期：2026-09-28

本轮只核对现有营业配置、补货模型和已存在的两个施工点，不改变 180 秒营业上限、15 秒收尾、首日 3 单、手机剪过头失败口径，也不新增施工点、容量或货币。

## 当前可复核事实

- `SalonMobileDayConfig` 仍固定 `BusinessDurationSeconds=180`、`ClosingGraceSeconds=15`、首日 `TargetOrdersForDay(1)=3`。
- 首日确定的前三单是 `O001=剪发`、`O002=洗发+吹发`、`O003=洗发+剪发`。移动洗发主动打泡沫为 2 秒，泡沫最佳回访窗口从 4 秒开始；这解释了玩家需要在后台等待期间切换任务，而不是原地连续按键。
- 首个现有扩建是 `expansion-pad-haircut-2`，成本 180，解锁稳定工位 2；它改变的是可用剪发工位与 Zone B 通行边界。补给货架施工点仍是另一个现有点，成本同样为 180。
- 旧 R1 报告中的补给货架路线测量属于另一版首个施工点语境，本轮不把它当作当前理发椅扩建的收益证据。

## 已复现缺陷：隐藏货架施工点仍会扣款

首个理发椅扩建未购买时，`BuildMobileSupplyPurchasePad` 将 `_mobileSupplyPadRoot` 设为不激活（`SalonDemo.Mobile.cs` 中的 `showPad` 条件）。但 `UpdateMobilePurchasePad` 的入口只检查移动模式、补给 pad 模型、锚点、玩家和营业状态，没有检查 `_mobileSupplyPadRoot.activeInHierarchy`。

因此，玩家经过隐藏货架锚点并停留 1 秒时，正式方法仍会按 60 金币/秒计算并提交 60 金币。这个扣款没有可见施工圈或 `BUILD` 标签，直接造成“扩建作用难理解”和资金异常；若玩家沿首个理发椅施工路线经过该位置，错误更容易出现。

`Assets/Tests/FirstSessionPacingTests.cs` 的 `HiddenSupplyPadDoesNotChargeBeforeHaircutExpansionIsUnlocked` 通过反射调用正式 `UpdateMobilePurchasePad`，把隐藏根节点、锁定理发扩建、余额 180 和玩家相同锚点固定下来。红阶段实际观测为 `Paid=60`、余额 `120`；修复后该回归通过，隐藏施工圈不再收费。

同一集成红测还固定了施工圈的三种边界状态：理发扩建完成后调用 `UpdateMobilePurchasePadVisual`，货架施工圈红阶段应显示却仍为隐藏；两个扩建均未开始时红阶段应隐藏却仍显示；货架已有部分投入时红阶段应保持显示却仍隐藏。修复后这三条均通过，且理发扩建完成后货架施工圈可以继续按现有节奏扣款。红测 `Builds/FirstSessionIntegration1.xml` 共 30 条，其中 26 条通过、4 条失败，失败正是上述四条；`Builds/FirstSessionWorkersRed.xml` 也记录了隐藏扣款的 `Paid=60` 失败观测。

## 节奏判断与本轮边界

现有纯模型配置没有足够证据支持降低压力或改短营业时长：等待顾客在接待前按首日 0.85/秒掉耐心，接待成功后交接路径已有不继续掉耐心的回归保护；移动服务本身也保留了“打泡沫后离开、回访冲洗”的后台任务。模型和 EditMode 回归通过只保护规则契约，不代表真人节奏已经获得产品认可。已有 `MobileEvidence/report.json` 的首日失败没有浏览器证据证明由隐藏施工圈扣款导致，因此本轮不作该因果归因。

首个理发椅扩建的确定收益是新增一个可安排的剪发工位，并解除 Zone B 隔离；单人玩家仍只有一个主动操作上下文，现有模型没有证明它必然带来固定的订单吞吐提升。因此本轮只修复隐藏施工点的显示与事务边界，并用最终 WebGL 包做一次独立真实触摸验收。

## 主代理验证

Unity 红→绿证据：

```text
FirstSessionPacingTests.HiddenSupplyPadDoesNotChargeBeforeHaircutExpansionIsUnlocked
FirstSessionPacingTests.CompletedHaircutExpansionRevealsSupplyPadAndKeepsItChargeable
FirstSessionPacingTests.SupplyPadHidesAgainWhenBothExpansionsAreUnstarted
FirstSessionPacingTests.PartialSupplyExpansionKeepsItsPadVisibleBeforeHaircutExpansion
FirstSessionPacingTests.MobileProfileRetainsTheApprovedRoundAndFirstDayTarget
```

红阶段 `Builds/FirstSessionIntegration1.xml` 为 `30` 条总计、`26` 条通过、`4` 条失败；四条失败分别是隐藏不收费、理发扩建后显示、未投入时隐藏和部分投入保持显示。最终 `Builds/FirstSessionFinalEditMode.xml` 为 `716/716` 通过、`result=Passed`，其中 `FirstSessionPacingTests` 为 `5/5` 通过。该结果证明代码回归已收口，不替代真人节奏或浏览器首日体验认可；WebGL、Manifest 等仓库级检查由主代理统一收口。

## 最终包真实 Chromium 扩建验收

使用已经启动的 `http://127.0.0.1:8910/WebGLDemo/?mobileEvidence=1`，运行 `Builds/FirstSessionGrowth/run_growth_acceptance.py`。驱动复用 `tools/check-mobile-salon.py` 的 `MobileDriver`、路径规划和 `station_is_usable`，浏览器为 `channel="chromium"`、headless、844×390 单一上下文；所有动作均为 CDP `Input.dispatchTouchEvent`，没有注入 Unity 状态或存档。

最终报告 `Builds/FirstSessionGrowth/report.json` 为 `passed=true`，共观察 254 帧、发送 12 个真实动作，浏览器错误和警告均为空。可复核的实际序列如下：

- 首单 O001 真实接待、安排和剪发完成，`completed=1`，余额从 0 变为 **120**，顾客为 `NormalCompletion` 且 `hairStage=Complete`。这次包实测首单收入是 120，报告没有把它改写成 180。
- 走到隐藏补货施工点 `(-4.25,.2,1.1)` 停留约 1.1 秒，前后余额均为 **120**、供货架遥测 `padPaid=0`、`padUnlocked=false`；没有发生隐藏扣费。
- 走到理发扩建点 `(3.9,.2,-.35)` 形成真实部分投入：到达时余额为 86，停留后为 14，报告记录从首单余额算出的部分钱包支出为 106。随后经过既有洗发用品来源与货架路线补货，真实完成 O002（洗发+吹发），余额回到 **300**。
- 回到同一理发扩建点完成累计 **180** 金币投入，余额为 **240**，`stations` 中 id `2` 的 `usable=true`。截图 `07-haircut-expansion-complete-rack-visible.png` 同时记录了中文提示“新理发工位已开放 · 可同时安排两位顾客”，以及已出现的补货架施工圈；供货架自身仍保持 `padPaid=0`、`padUnlocked=false`，没有把两个施工点的字段混淆。
- 离开施工点后在同一浏览器上下文刷新，余额仍为 **240**，工位 id `2` 仍为 `usable=true`，供货架字段仍为未投入状态。刷新后的启动画面见 `08-after-refresh-persisted.png`，持久化断言来自刷新后的新遥测。

最终可复核文件为 `Builds/FirstSessionGrowth/report.json`、`states.json`、`actions.json`、`errors.json` 及 01–08 截图。此前一次驱动在 O002 前忘记先走既有货架补货路线，因而停在“携带 3 个用品、货架 0 个”的路径失败；该过程保留为 `report-wash-supply-blocked.json`、`states-wash-supply-blocked.json`、`actions-wash-supply-blocked.json` 和 `failure-wash-supply-blocked.png`，仅作驱动失败记录，不代表最终包验收失败。
