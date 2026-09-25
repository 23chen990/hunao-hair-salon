# P1-01 正式入口首个阻断点复现记录

## A. 一句话结果

在 `BASE_SHA=2dd15d728ca440e3ce08e646d652153082392f80` 的正式 WebGL 入口中，未复现首个阻断；新玩家可以从开始营业完成首位顾客的剪发订单并获得合法收入，因此本批没有实施猜测性修改。

## B. 版本真实性

- 仓库：`23chen990/hunao-hair-salon`
- 工作区：`/Users/kker/Documents/ChatGPT/game2`
- 分支：`phase-0-2-gameplay-gate`
- BASE_SHA / 当前源码：`2dd15d728ca440e3ce08e646d652153082392f80`
- 远端 HEAD：同上
- PR：[#1](https://github.com/23chen990/hunao-hair-salon/pull/1)，Open，非 Draft
- Unity：6000.5.8f1
- 基线构建日志：`unity-hair-salon/Builds/P1-01-BaseWebGLBuild.log`
- 基线 WebGL 输出：`unity-hair-salon/Builds/WebGLDemo/`
- 浏览器入口：`http://127.0.0.1:8910/WebGLDemo/`
- 本批无运行源码修改，因此没有 TESTED_SOURCE_SHA；没有推送或改动 PR 状态。

## C. 修改前复现

验证条件是隔离的新浏览器页、生产默认初始化、正式 `HairSalonDemo` WebGL 入口，未使用 `BrowserCoreFlowSmoke`、`simple2d`、`washCraft` 或自动开店参数。

实际操作：点击“开始营业” → 用正式摇杆接近 1 号顾客 → 点击“接待 1 号” → 移动到中央剪发工位 → 点击“安排剪发” → 等顾客入座 → 按住剪发按钮至绿色区间后松开。

结果：每一步都可继续；剪发完成后 HUD 余额从 0 变为 120，顾客进入完成状态并保留离场流程。证据截图：

- [开店前](../../../unity-hair-salon/Builds/PipelineEvidence/p1-01-base-opening-844x390.png)
- [首位订单完成](../../../unity-hair-salon/Builds/PipelineEvidence/p1-01-base-first-order-844x390.png)

因此没有“实际失败但未修”的首阻断可作为最小修改目标，也没有添加回归夹具或改动产品规则。

## D. 根因与最小修改

未确认根因；没有生产代码修改。源码中的移动目标、接待保留、工位安排和剪发生产调用链在本次正式入口验证中均能完成，不能依据旧构建或猜测改写它们。

## E. 修改后玩家流程

无修改版本的基线流程达到：开始营业、接待、安排剪发、剪发、合法收入、顾客完成/离场准备。第二位顾客已在画面中自然出现并显示需求，但本批因首阻断未复现而停止，没有宣称第二位订单完成。

## F. 测试与证据

- Node 资产管线测试：PASS，17/17，`npm run test:assets`。
- Unity 针对移动运行测试：PASS，13/13，`unity-hair-salon/unity-hair-salon/Builds/P1-01-EditMode.xml`。
- Unity 全量 EditMode：PASS，673/673，`unity-hair-salon/Builds/P1-01-AllEditMode.xml`。
- Manifest/资源校验：PASS，`unity-hair-salon/Builds/P1-01-ValidatePipeline.log`，报告 17 个资产通过。
- WebGL 构建：PASS，`P1-01-BaseWebGLBuild.log` 报告 `Builds/WebGLDemo` 成功。
- 正式浏览器启动与人工输入：PASS，目标横屏 844×390；无页面错误，截图见上。
- 录像：NOT_RUN；本次保留连续可核查操作步骤与前/后截图，未录制视频文件。

## G. 体验判断

1. 本次没有出现玩家被阻断或无法理解下一步的步骤。
2. 接待后的目标提示、工位安排和剪发按钮都能随着玩家操作推进。
3. 移动、服务和调度操作均保留。
4. 没有通过降低压力、自动服务、改库存或改经济来掩盖问题。
5. 第二位顾客的完整洗发/补货流程、双顾客压力和后续日结体验本批未测量。

## H. 剩余问题与停止位置

没有复现到可授权修复的首阻断；继续猜测性修改会违反“无法复现不得猜改”的停止条件。本批在首位订单完成、第二位顾客出现后停止，未进入日结迁移或扩系统任务。

## I. 用户试玩方式

在仓库根目录运行：

```bash
python3 tools/serve-salon.py --port 8910
```

打开 `http://127.0.0.1:8910/WebGLDemo/`，点击“开始营业”，按摇杆移动到顾客、接待、前往中央剪发工位，点击安排后等待入座，再按住剪发按钮到绿色区间松开。重点复查接待后目标提示、工位安排和首位订单收入是否持续可达。

本批结束，等待独立审查，未自动合并，未进入下一阶段。
