# 首局反馈组件修复记录

日期：2026-09-28

## 复现依据

使用 `artifacts/p107/mobile/` 下的 844×390 横屏截图检查首局移动控件。`02-touch-controls.png`、`04-pressure.png` 和 `17-second-customer-wash-assign-ready.png` 中，右侧动作按钮的文字在目标视口约 13px，底部提示约 9px；洗发中、前往工位等状态只有深色按钮和短字样，动作对象不够醒目。队列卡片只显示号码、服务图标和百分比，玩家需要再结合场景猜测顾客是进场还是等待。

## 本次最小改动

- `SalonMobileControls`：右下动作面板从 320×184 调整为 360×200（参考分辨率），动作字从 34 调整为 48，最小自适应字号提高；底部提示面板扩大到 760×76，提示字调整为 32，并为动作字保留深色阴影。不可操作时仍保留传入的状态文案，文字使用奶油色并清空触控捕获。
- `SalonMobileControls`：摇杆旋钮从 112 调整为 124，并在摇杆上方增加“移动”标签，帮助首局识别左侧触控区域。标签和动作字均不接收射线，不改变触控事件路由。
- `SalonMobileQueueView`：队列卡片调整为 336×94，号码改为两行状态提示（`1号\n待接`、`1号\n已接` 或 `1号\n进场`），号码框增高到 80×70 并调整字号，耐心值字号提高到 34，进度条增粗，服务图标略放大。`Waiting + HasServiceEngaged` 明确显示“已接”，避免玩家重复接待；仍只显示 `Entering`/`Waiting` 顾客，未改变队列来源和服务模型。

## 回归测试

红测阶段由主代理在 `Builds/FirstSessionWorkersRed.xml` 确认：新断言先捕获旧动作面板宽度 320 小于 340、旧队列卡片宽度 310 小于 320；原有 `SalonMobileControlsTests` 行为测试通过。

主代理随后在 `Builds/FirstSessionIntegration1.xml` 复跑并确认本次控件相关用例全绿：

- `FirstSessionFeedbackTests`：1/1 通过。
- `SalonMobileControlsTests`：12/12 通过。

第二轮状态回归先在 `Builds/FirstSessionAcceptedQueueRed.xml` 捕获旧实现错误：2 条中 1 条失败，已接待顾客实际显示“待接”。本轮修正后需重新执行：

```text
FirstSessionFeedbackTests|SalonMobileControlsTests
```

并在新构建的 844×390 截图确认队列第二行实际可见。

主代理最终在 `Builds/FirstSessionFinalEditMode.xml` 完成全量 EditMode：716/716 通过，其中 `FirstSessionFeedbackTests` 2/2、`SalonMobileControlsTests` 12/12。第二轮修正后的新版横屏截图仍需单独留存；现有 `FirstSessionVisual/after/844x390-single-business-static.png` 早于第二轮代码。

生产代码修改后应由主代理继续执行：

```text
FirstSessionFeedbackTests|SalonMobileControlsTests
```

本 worker 未启动 Unity、未运行构建或浏览器验收；844×390 实机截图仍需主代理/视觉 QA 在新构建上确认。

## 给主代理的集成建议

控件已直接放大现有 `SetInteraction`/`SetHint` 的显示，不需要改主场景。主场景的状态源仍应传明确的中文短句：洗发用品不足时提示“用品不足 · 去洗发区补货”；洗发泡沫达到 `ReadyToRinse` 时提示“回来冲洗收尾”，`Rinsed` 不再列为待冲洗；自动吹发进入最佳回收窗口时提示“回来收尾”。收尾阶段应继续让按钮/底部提示说明“收尾中”，结算结果由现有结果面板显示。

这些文案建议只涉及 `SalonDemo.Mobile.cs` 的现有状态提示，未在本 worker 中修改主场景集成文件。
