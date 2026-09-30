# 顾客跟随、气泡与工位交互修复

日期：2026-10-01（Asia/Shanghai）。本轮按产品负责人反馈修复，Luna 参与交互复查和手持吹发模型兼容修复，主线程统一集成正式场景。

## 实际行为

- 接待后的顾客沿主控走过的路线跟随，保持间距；慢走时不会反复冲刺，停下后逐渐靠近并停稳。取消再接待时重新计算路线，不再沿旧路径折返。
- 顾客从原座位平滑起身，绕开家具加入跟随，不再跳到碰撞体附近的随机空点。
- 选中顾客的需求气泡位于顶部任务栏与触控区之间，并显示在角色、家具前方；选中的需求使用完整尺寸与较高显示层级。
- 未购买自动吹发架时使用手持吹风机，需要按住操作；提前松手会停止并允许继续。购买自动架后才允许后台自动吹发。原有双人模式的另一位主控也遵守同一规则，并保留暂停进度。
- 洗发工位从左侧、前侧、右侧的可达范围都可安排顾客及开始洗发，距离依据工位 Manifest 占地判断。

## 复现与验证

- 跟随修复前 5 项行为测试全部失败：贴身追赶、低速冲刺、重选回旧路、起身跳位、家具边卡住；修复后连同转弯与换主控用例共 7 项通过。
- 修复前真实触摸记录显示停步后顾客与主控间距仅约 0.48；修复后的三次转弯停步间距约 1.06，后续采样位移为 0。取消并重新接待通过。
- 首日实际未拥有自动架时，按住进入手持吹发，提前松手停止，再次按住并完成；整个过程没有触发自动吹发。
- 洗发工位三侧均实测到“安排洗发”和“洗发”可用按钮，保留各侧截图。
- 两日真实 Chromium 触摸营业流程完成 7/3、5/4 单；无人收银弃单均为 0；闭店刷新后金币及洗发水库存保持，浏览器错误为 0。
- Unity 全量 EditMode：830/830；Node 资产管线：17/17；触摸驱动单测：13/13；字体与 Manifest 资源校验通过。
- 最终完整项目检查退出码为 0：Demo、Asset Lab、Candidate、Reference 四种 WebGL 构建成功；真实 Chromium 中四个场景均加载成功，正式 Demo 与候选服务流程通过，场景错误为空。

## 证据入口

- 专项触摸报告：`artifacts/current-bug-fix/browser/report.json`
- 跟随修复前截图与状态：`artifacts/current-bug-fix/browser-before/`
- 跟随修复前测试：`artifacts/current-bug-fix/escort-before.xml`
- 双人操作修复前测试：`artifacts/current-bug-fix/coop-before.xml`、`coop-model-before.xml`
- 修复后截图：`artifacts/current-bug-fix/browser/02-follow-turn-2.png`、`manual-dry-ready.png`、`wash-Wash-right.png`
- 最终全量测试：`unity-hair-salon/Builds/PipelineEditMode.xml`
- 本轮问题与画面报告：`artifacts/current-bug-fix/visual-qa-report.json`
- 完整场景报告：`unity-hair-salon/Builds/PipelineEvidence/browser-check-all.json`（本轮副本：`artifacts/current-bug-fix/pipeline-browser.json`）

## 体验步骤

1. 启动 `python3 tools/serve-salon.py --port 8910`，打开本地 `WebGLDemo/`。资产实验室使用同服务下的 `WebGLAssetLab/`。
2. 新档开始营业，靠近顾客接待，走直线、转弯、中途停下，再取消并重新接待；观察顾客的间距、停步和需求气泡。
3. 解锁手持吹风机后，自动架尚未建造时操作应为“按住吹发”；先短按松开，再按住到满格变绿后松手。
4. 解锁洗发台后，分别从左、前、右侧接近，检查安排和服务按钮。家具或墙体阻挡的位置仍遵守碰撞规则。

## 范围与限制

- 实机画面与触控证据为 Chromium 的 844×390 横屏环境；未将此结果描述为真实 iOS/Android 设备验收。
- 本轮未改角色、家具布局或美术资产。完整流水线发现独立候选资产场景误用了新档解锁模式，导致隐藏洗发位查找失败；已将该技术场景恢复为原有设备齐备的验收模式。
- 已有旧存档若已购买自动架，会继续保留设备；检查未解锁时的行为应使用未购买自动架的存档。

## 主要实现位置

- `unity-hair-salon/Assets/Scripts/SalonCustomerEscort.cs`：跟随路径、缓动与原座位离开。
- `SalonDemo.CoreFlow.cs`：跟随状态的建立、结束与清理。
- `OrderDemandBubbleView.cs`、`SalonDemo.cs`：安全区域、场景前方投影和选中显示。
- `SalonDemo.Mobile.cs`、`SalonDemo.Coop.cs`、`SalonMobileNavigation.cs`：设备分支、暂停恢复与工位占地距离。
- `SalonGameModel.cs`、`SalonGameModel.CoopExtensions.cs`：自动架门槛和玩家专属手持操作归属。
