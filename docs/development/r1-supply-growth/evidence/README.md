# R1 验收证据包

本证据包对应的运行时代码被测版本是工作分支 `phase-0-2-gameplay-gate` 的
`2dcb19a110235d7e2bb5cab3fef7fe882868c160`。本轮仅修改浏览器证据采样脚本和
其回归测试；文档和测试文件属于随后补交的审查版本，不把最终提交 SHA 当作
运行时构建来源。

## 文件索引

- `proximity-pad-report-r1-acceptance.json`：正式 `WebGLDemo`，Chromium 真实触控，844×390，施工、刷新、路线和失败/重试；`errors=[]`。失败数据位于 `purchasedFailureRetry.failed*`，重试数据位于同对象的 `retry*`。
- `unity-editmode-r1-acceptance.xml`：当前工作树全量 Unity EditMode 原始 XML，`670/670`，`result=Passed`，Unity 6000.5.8f1。
- `visual-qa-report-844x390.json`：正式 `HairSalonDemo` 视觉/启动检查，844×390、DPR 1、核心流程通过、资源/控制台错误 0；比例、位置、方向、阴影等仍是产品人工确认项。
- `startup-smoke-960x540.png`：正式 WebGLDemo 的 960×540 启动冒烟截图，canvas 960×540，图像方差 13731.09；这张图不代表完整经营流程。

## 采样回归

命令：`python3 -m unittest tests/test_check_proximity_pad.py`

结果：`1` 个测试通过。测试先在旧脚本上因缺少 `capture_failure_snapshot` 失败，加入最小快照函数后通过；快照在驱动状态被更新为重试状态后仍保留失败时的 `completed/target/balance/paid/unlocked/supply`。

## 运行命令

- `python3 tools/check-proximity-pad.py`（正式入口，真实 Chromium 触控，844×390）
- `python3 tools/browser-check.py --mode demo`（正式 Demo 启动、核心流程和 A/B/C/D 视觉证据）
- 960×540 启动冒烟：正式 `WebGLDemo`，Chromium，DPR 1；仅检查 canvas、截图非空和浏览器错误。
- Unity EditMode：`-runTests -testPlatform EditMode -testResults .../R1AcceptanceFullEditMode.xml`，未与 `-quit` 同用；以 XML `result/passed/failed` 判定。
- Manifest/资源：`BuildScript.ValidatePipeline`，17 项通过。
- WebGL：`BuildScript.BuildWebGLDemo`，`Builds/WebGLDemo` 构建成功，日志记录 77,673,723 bytes。

浏览器 headless 会报告 `screen.orientation.lock() is not available` 能力提示；既有脚本按环境限制过滤它，游戏控制台/资源错误仍为 0。证据来自 Chromium，不是真机 iOS/Android。
