# R1 验收证据包

本证据包分开记录运行时代码、正式 WebGL 入口和模型/存档夹具。被测正式
WebGL 运行时来自 Unity 6000.5.8f1、`Builds/WebGLDemo`，构建时运行源码为
`2dcb19a110235d7e2bb5cab3fef7fe882868c160`；本轮工作区起始 HEAD 和远端分支均为
`6df532c4b1bc800442983c751c19dc355417692e`。本轮未修改运行时代码。

## 文件索引

- `r1-rollback-report.json`：正式 `HairSalonDemo` WebGL 移动入口，Chromium 真实触控、844×390。记录 A 不刷新部分投入失败重试、B 刷新后新 DayOpening 再完成购买失败重试、D 失败处理后刷新；包含 initialSave、opening、changed、failed*、retry*、phase 和库存。
- `proximity-pad-report-r1-acceptance.json`：原有正式施工/补货/路线和开店前已购扩建失败重试证据；`failed*` 在重试输入前采样，`retry*` 单独记录。
- `unity-r1-checkpoint-integration.xml`：`SalonR1CheckpointRollbackIntegrationTests` 三条模型/存档边界夹具，使用真实钱包、施工 Pad、补货模型和 `ISalonProgressRepository` Save→Load；不是正式入口实测。
- `unity-editmode-r1-acceptance.xml`：运行代码未变的 6df 基线全量 Unity EditMode 原始 XML，670/670，`result=Passed`；本轮新增夹具另见定向 XML。
- `visual-qa-report-844x390.json`：正式 Demo 844×390 启动与视觉检查，资源/控制台错误 0；比例、位置、方向、阴影仍以人工视觉验收为准。
- `startup-smoke-960x540.png`：正式 WebGLDemo 960×540 启动冒烟截图，不代表完整经营流程。

## 本轮命令

- `python3 tools/check-r1-rollback.py`：A/B/D 正式入口流程，`passed=true`、`errors=[]`。
- `python3 tools/check-proximity-pad.py`：保留原有正式入口的开店前已购扩建失败重试和补货路线证据。
- `python3 -m unittest tests/test_check_proximity_pad.py`：1/1 通过。
- Unity 定向夹具：`-runTests -testPlatform EditMode -testFilter SalonR1CheckpointRollbackIntegrationTests -testResults .../R1R1IntegrationTests.xml`，3/3 通过。
- Unity 全量 EditMode、Manifest 资源检查、WebGL 构建和 `browser-check.py --mode demo` 的结果记录在主报告；Unity 测试命令没有与 `-quit` 同用，按 XML 判定。

headless Chromium 可能输出 `screen.orientation.lock() is not available` 能力提示；现有检查器按环境限制过滤它，正式游戏控制台和资源错误仍为 0。本证据来自 Chromium，不等同于 iOS/Android 真机验证。
