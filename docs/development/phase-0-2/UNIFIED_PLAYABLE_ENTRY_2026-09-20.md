# 正式试玩入口整合记录（2026-09-20）

本轮把先前的 Unity Demo 和移动试玩入口收敛为同一个 `HairSalonDemo` 场景：

- 正式 WebGL 与 macOS 构建都使用 `Assets/Scenes/HairSalonDemo.unity`。
- 默认恢复完整三维沙龙场景；移动营业日、队列、服务和存档逻辑保持不变。
- 移动输入同时支持触摸/鼠标拖动左摇杆、`WASD`/方向键移动，以及右侧按钮/`Space` 交互。
- 原 2D 灰盒仍可用，但仅在 URL 显式加入 `?simple2D=1` 时启用，作为诊断视图，不是第二个试玩包。

## 验证

- Unity EditMode：610/610 通过。
- Node 资产管线：17/17 通过。
- Manifest/资源检查：13 个资产通过。
- 真实 Chromium 横屏触摸回归：通过，完整 Day 1、日切、后台吹发、付款和重载均通过。
- macOS Development Build：从同一场景重新构建并完成无图形启动检查。

本地源码分支和 GitHub 远端分支在交付时使用同一个提交 SHA；浏览器试玩包由该源码重新生成。
