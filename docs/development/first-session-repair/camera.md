# 首局镜头复现与回归

## 已复现事实

9 月 28 日移动证据的 `17-second-customer-wash-assign-ready.png` 显示按钮为“安排洗发”，底部提示为“已接待 2 号 · 前往洗发工位”，但角色和可用洗发工位没有同时出现在画面里。`04-pressure.png` 也显示目标提示和实际目标不在同一可见区域。

同目录 `states.json` 的对应记录给出玩家位置约为 `(-3.685, 0.05, 3.500)`，可用的第二洗发工位约为 `(-3.05, 0, 4.05)`；这两点的相机 `WorldToScreenPoint` x 均小于 0。`errors.json` 为空，因此本项是镜头取景问题，不是浏览器异常。根目录 `report.json` 的失败只表示该次触控流程未达到日目标，不能作为玩法不可通关的证据。

复现步骤：在 844×390 横屏启动正式 Demo，开始营业并接待第二位洗发顾客；沿提示移动到第二洗发工位附近，在出现“安排洗发”时观察画面。现状会出现角色/工位在视口之外，按钮仍可出现。

## 坐标根因

`WashCraftIntegration.ConfigureCamera` 把总览相机设为 `(-12, 20, -23)`，朝向 `(0, .65, 1)`，正交大小为 `8.4`。`SalonDemo.BuildWorld` 随后保存这组位置和旋转为 `_overviewCameraPosition`、`_overviewCameraRotation`。

当前 `UpdateMobileCameraFollow` 和 `UpdateCoopCameraFollow` 把移动对象的 x/z 中心直接写进相机位置，再加上 `_overviewCameraPosition.z - 1.4`。这相当于按世界轴平移相机；总览相机有 yaw，世界 z 位移也会沿相机 right 轴产生屏幕 x 位移。证据中的玩家/工位因此一起落到屏幕左侧。双人方法还使用同一旧固定轴偏移；当 `halfWidth` 大于房间宽度的一半时，`Mathf.Clamp` 的 min 可能大于 max，需要修正边界分支。

## 回归测试

新增 `Assets/Tests/FirstSessionCameraRegressionTests.cs`，只调用 `SalonDemo.UpdateMobileCameraFollow` / `UpdateCoopCameraFollow`，把方法输出的真实相机目标应用到相机后，用 `WorldToViewportPoint` 检查可见范围。测试覆盖：

- 单人：玩家与第二洗发工位；844×390、960×540。
- 双人：两名角色与第二洗发工位；844×390、960×540。
- 单人极端位置：证据 floor 内四个带边距角点；每个角点检查脚点与 `y+2` 头部。
- 双人极端位置：证据 floor 对角两端；两名角色分别检查脚点与 `y+2` 头部。

测试没有重写相机取景公式，验收依据是屏幕投影。写入后应先由主代理按过滤器运行，当前旧实现预期红；主代理完成基于总览相机实际 right/up 投影和房间边界的最小修复后再运行同一过滤器确认绿。Unity 进程由主代理串行运行。

极端位置测试只要求角色身体在相机视口内，不要求远处全部工位同时入镜；相机应使用自身 `Camera.aspect` 取横屏比例，并在动态取景时保持总览角度与布局。

建议主代理保留 `WashCraftIntegration` 的镜头角度和布局，只把地面目标转换到总览相机平面，按 camera right/up 的投影求出包含玩家和当前目标的相机位置；单人和双人共用相同的投影边界计算，并在房间宽度小于取景宽度时避免反向 Clamp。

## 红绿结果

主代理先在 [FirstSessionCameraRed.xml](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCameraRed.xml) 复现旧相机的 8 条投影失败；P2 转移回归随后在 [FirstSessionCoopRed.xml](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionCoopRed.xml) 的 2 条用例中复现 `StartDry` 误选。修复后，[FirstSessionFinalEditMode.xml](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFinalEditMode.xml) 于 9 月 28 日记录 716/716 通过，其中镜头 8 条、P2 转移 2 条均为绿色；新增的已接待顾客队列提示回归也已通过。
