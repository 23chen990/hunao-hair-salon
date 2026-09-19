# Phase 2 Gameplay Gate 最终复跑验收

> 复跑日期：2026-09-19（Asia/Shanghai）  
> 范围：Phase 2 最后一轮验收收尾；不进入 Phase 3。  
> 原则：只保护正式 Mobile Wash 的 readiness，修正文档事实，不调玩法参数，不扩展系统。

## A. Git 状态与可追溯性

开始修改前实际核对结果：

| 项目 | 结果 |
| --- | --- |
| branch | `phase-0-2-gameplay-gate` |
| 开始时 HEAD | `7d9a3a1935ae3e05897c9731bed0a3a544429255`，与交接预期一致 |
| 开始时工作区 | clean |
| remote | `origin/phase-0-2-gameplay-gate` 当时与本地同 SHA |
| PR | [#1](https://github.com/23chen990/hunao-hair-salon/pull/1)，`OPEN`，base=`main`，merge state=`CLEAN` |
| 基线范围 | `5dde4ea..HEAD` 的 5 个历史提交保持可追溯，没有 squash、merge 或 force push |

本轮按小提交完成：

1. `d4dde1d` `fix(phase-2): enforce foam readiness in formal wash rinse`
2. `2ee974a` `docs(phase-2): correct final acceptance claims`
3. `ebf167d` `test(phase-2): align mobile browser driver with 2d camera`
4. 本文件：`docs(phase-2): add final acceptance rerun`（本文件提交后以交付时 `git rev-parse HEAD` 为仓库最终 SHA）

`ebf167d49d6fe4c10548c46a70019127887eefc5` 是本验收报告提交前、功能/文档/QA 改动均已完成的最终代码 SHA。报告提交本身只增加本验收记录，不再改变玩法代码。交付时再次核对 branch、remote 和工作区，并将最终 HEAD 在回复中明确列出。

## B. 本轮实际改动（逐文件）

改动保持在 5 个文件，且没有新增玩法系统：

| 文件 | 改动 | 影响边界 |
| --- | --- | --- |
| `unity-hair-salon/Assets/Scripts/SalonGameModel.cs` | 在 `FinishWashRinse` 中增加 `!IsWashFoamReadyToRinse(customer)` guard | 只保护正式冲洗 API；没有改 `BeginWashAction`、状态机或参数 |
| `unity-hair-salon/Assets/Tests/Phase2BusySalonFlowTests.cs` | 新增 `WashCannotFinishRinseBeforeFoamIsReady` | 覆盖未 ready 拒绝、ready 后成功 |
| `docs/development/phase-0-2/PHASE2_PLAYABLE_REPORT.md` | 修正 OverdueRinseDuration、EditMode 数量及“路径完全统一”的措辞 | 文档事实修正 |
| `docs/development/phase-0-2/PRODUCT_OWNER_SUMMARY.md` | 区分 Desktop/QA 快捷入口与正式 Mobile 序列，标明迟到额外时间待试玩 | 文档事实修正 |
| `tools/check-mobile-salon.py` | 当 2D 正式场景的 telemetry 没有水平 forward 投影时，使用运行时的 world +Z 作为 QA 输入基底 | 仅修浏览器驱动；不改变游戏移动速度、碰撞或玩法参数 |

本轮没有改动 `BusinessDurationSeconds=120`、`ClosingGrace=15`、`MaxConcurrentCustomers=6`、`WaitingCapacity=4`、Day 1 target=3、Dry 12—20s 窗口、Director 强度、spawn interval 或 player move speed。

## C. Readiness model guard

正式 Mobile 的模型路径现在明确为：

```text
BeginWashFoamHold
  → Shampoo 主动阶段结束
  → BackgroundTask 正在运行
  → FoamOptimalStart 到达
  → FinishWashRinse
```

本轮新增测试 `WashCannotFinishRinseBeforeFoamIsReady`，逐项验证：

- `BeginWashFoamHold` 成功；
- Shampoo 阶段结束后 `BackgroundTaskState.Running`；
- 尚未到 `FoamOptimalStart` 时 `IsWashFoamReadyToRinse == false`；
- 此时 `FinishWashRinse == false`、`CurrentNeed` 仍为 `Wash`、`Step` 不推进；
- 到达 ready window 后 `FinishWashRinse == true`，订单才进入下一步 `Cut`。

TDD 证据也保留在本机 XML：guard 加入前的 focused run 为 1 failed（未 ready 时错误地返回 true）；加入 guard 后同一命令为 1 passed。完整复跑为 609/609 passed。

## D. 文档陈述修正

旧文档曾把 Moderate 迟到描述为“冲洗从约 2.0s 延长到 2.75s，玩家会明显感觉多花处理时间”。重新对照真实代码后，准确事实是：

- 领域层仍使用 `ServiceConfig.OverdueRinseDuration`，默认输入为 `2.75s`；
- 正式 Mobile `FinishWashRinse` 当前是即时收尾 API，玩家不会被该数值实际锁住 2.75 秒；
- 因而“迟到造成可感知的额外操作时间”尚未实现/尚未验证，已在两个面向产品的文档中标为 **【需要试玩验证】**；
- 本轮没有为了迎合旧文档擅自新增 timed-rinse 系统。

同时撤销“移动与桌面两条路径完全统一”的表述：它们共享领域裁定，但输入序列仍有意不同，详见下一节。

## E. 完整测试与构建矩阵

### 自动测试与资源检查

| 项目 | 执行方式 | 结果 |
| --- | --- | --- |
| Unity EditMode | `/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath unity-hair-salon -runTests -testPlatform EditMode -testResults unity-hair-salon/Builds/Phase2FinalAcceptanceEditMode.xml -logFile unity-hair-salon/Builds/Phase2FinalAcceptanceEditMode.log`（不带 `-quit`） | **609 total / 609 passed / 0 failed / 0 skipped / 0 inconclusive**；Unity `6000.5.8f1` |
| Phase 2 忙碌日模拟 | `Phase2BusyDaySimulationTests` focused XML | 4/4 passed；保留并发结构证据，不当作好玩证明 |
| Node 资产管线 | `node --test tests/asset-pipeline.test.mjs` | **17 passed / 0 failed** |
| Python/Pillow 环境 | 通过 `/usr/bin/python3` 执行；实际 `sys.executable` 为 `/Library/Developer/CommandLineTools/usr/bin/python3`，Python 3.9.6，Pillow 11.3.0 | 环境可用 |
| Manifest / 资源校验 | Unity `BuildScript.ValidatePipeline` | 13 assets passed；状态为 approved 9、candidate 1、NEEDS-REVIEW 1、review 2 |

### WebGL 与真实 Chromium

| 场景/路径 | 结果与证据 |
| --- | --- |
| 正式 WebGL Demo | `BuildScript.BuildWebGLDemo` 成功，`Builds/WebGLDemo` 约 77.6 MB；日志含 `[Pipeline] WebGL build succeeded` |
| AssetLab / Candidate / Reference WebGL | 三套构建均成功：`Builds/WebGLAssetLab`、`Builds/WebGLCandidate`、`Builds/WebGLReferenceVisual` 均生成 `index.html` |
| 正式 Demo 视觉 QA | `zsh .agents/skills/salon-visual-qa/scripts/run.sh demo` 通过；844×390、DPR 1、console errors=[]；正式 HUD、开店卡、active service、Wash A/B/C/D 证据均生成 |
| Candidate Chromium | `python3 tools/browser-check.py --mode candidate` 通过；普通与 debug 两次均完成 alignment → active Shampoo → completion/payment，errors=[]、failedRequests=[] |
| Reference Chromium | `python3 tools/browser-check.py --mode reference` 通过；批准图/旧场景/运行时/overlay/low-density/debug 证据齐全，屏幕指标校准成功，errors=[]、failedRequests=[] |
| 正式 Mobile 全路径 | `python3 tools/check-mobile-salon.py --evidence-dir unity-hair-salon/Builds/Phase2FinalMobileEvidence-final` 通过；真实 Chromium touch，844×390，47 actions，`backgroundWhileOtherService=true`，`maxWaiting=4`，Day 1 完成 4/3，进入 Day 2，满意度持久化，errors=[] |
| 正式 Mobile 失败/重试路径 | `python3 tools/check-mobile-salon.py --failure --evidence-dir unity-hair-salon/Builds/Phase2FinalMobileEvidence-failure` 通过；`failureRetry=true`，ClosingGrace/Result/reset 路径可达，errors=[] |

正式 Mobile 自动路径实际观察到的标签包括：`洗发`、`泡沫中`、`冲洗`、`剪发`、`启动吹发`、`吹发收尾`、`收钱`、`转移顾客`。这证明交互链可运行，但不等同于真人手感通过。

### 已知的仓库级检查限制

`zsh tools/check-project.sh` 的 Node 17/17 先通过，随后在 UI 字体审计处停止：`UI font missing glyphs: 然走`。缺字来自现有 `SalonDemo.Mobile.cs` 提示文案（`然后可以先走`），不是本轮改动；本轮没有改字体、隐藏检查或把它误报成通过。它是一个需要单独处理的既有质量问题，不阻塞本轮 Unity、Manifest、WebGL 或 Chromium 结果。

## F. 回归核对

- 本轮没有删除任何旧测试，也没有修改任何旧测试；只新增 1 条 readiness test。Phase 2 更早的历史提交曾有意修改 2 条旧 Wash 语义测试，这不是本轮偷偷改测试。
- 完整 EditMode 609/609 全绿；没有出现原本通过项在本轮变失败的情况。
- `SalonProgressSave`、付款模型和 Day reset 的生产代码没有被本轮修改；自动测试覆盖单次付款、防重复付款、跨天清理泡沫等待，正式 Mobile 浏览器也通过收钱、Result、Day 2 与满意度持久化路径。
- 普通 Demo、Candidate、Reference 均完成启动/浏览器检查；QA 驱动修复只解决 2D camera telemetry 的输入基底，不绕过正式 Mobile 流程。
- 没有删除或清理 baseline 资产，没有加入新玩法、货币、页面、设备系统或参数调优。

## G. Phase 1 残余语义分叉（本轮明确不修）

1. **Desktop Wash shortcut**：Desktop / QA 普通入口仍可能直接走 `BeginServiceExecution(Wash)`；正式 Mobile 是 `BeginWashFoamHold → BackgroundTask → FinishWashRinse`。
2. **Mobile Cut stable Perfect**：移动点按式使用 `HaircutHoldDurationFor = PerfectMin + 0.05`，因此稳定落在 Perfect；Desktop hold/release 仍可产生其它结果。
3. **Desktop Auto Blow capability gate**：Desktop UI 仍以 `HasAutoBlowStand` 控制 Auto Blow 可见性；Mobile 直接使用模型的 `AutoBlowAvailable`/`StartAutoBlow` 能力。
4. **order/day path divergence**：Desktop 默认订单/快捷入口与 Mobile day profile 的订单升级、营业节奏不是同一输入路径。
5. **state compatibility layers**：`CustomerModel.State`、`Step`、别名与兼容字段仍存在；本轮没有重写 Customer 状态机或做通用命令框架。

这些是“正式产品路径 + Desktop/Debug 工具入口”的已知边界，不是本轮需要为了表面统一而扩大的重构范围。

## H. 仓库 baseline 卫生审计（只读）

相对 `main` 的 PR 当前为 663 个文件，顶层分布为：`unity-hair-salon` 265、`prototypes` 236、`artifacts` 59、`docs` 37、`platforms` 30、`assets` 11、`.agents` 5、`tools` 8、`output` 2，其余为项目说明/配置文件。

- tracked tree 共 1,227 个文件、约 161,575,276 bytes；
- `git ls-files` 中没有 `Library/`、`Temp/`、`Builds/`、`Logs/`、`UserSettings/`、`Artifacts/`；`.gitignore` 已保护这些未来生成目录；
- 没有发现超过 GitHub 100 MB 限制的 tracked 单文件；较大的三个文件为：
  - `assets/source/wash-craft/room-finish.blend1`（10,005,491 bytes）；
  - `prototypes/phase-ripple-skinned/validation/raw/phase-ripple-continuous.webm`（9,042,378 bytes）；
  - `assets/source/wash-craft/room-finish.blend`（8,063,729 bytes）。
- `artifacts/`、`platforms/` 和 `prototypes/` 下有可由构建重新生成的 dist/media/validation 产物，但同时带有 manifest、checksum、release checklist 或验证证据；本轮只列出、不删除，避免破坏历史验收调用关系。

## I. Gameplay Gate 与产品负责人 Day 1 试玩步骤

### Gate 结论

**【需要试玩验证】**

无头模拟证明系统能制造并发与 pending actions，真实 Chromium 自动化证明按钮、移动、后台任务、付款和日切链可运行；但两者都不能证明真人觉得跑动舒服、Dry 窗口合适、6 人峰值不崩，或 Moderate 迟到的额外惩罚足够有存在感。因此本轮不宣称 Gameplay Gate 通过，也不进入 Phase 3。

### 只需这样试玩 Day 1

1. 用本地静态服务器打开 `unity-hair-salon/Builds/WebGLDemo/index.html`，横屏 844×390 左右；点击开店。
2. 用摇杆移动，靠近顾客后点“引导/安排”，观察队列和工位占用。
3. 遇到 Wash：点“洗发”，看到主动段结束并出现“泡沫中”后立刻走开；去处理另一位顾客。
4. 泡沫进入“冲洗”窗口后再回来点“冲洗”，确认订单推进，而不是必须一直站在洗头床旁。
5. 剪发时留意自己被占住期间，队列耐心、泡沫等待和吹风后台是否继续变化。
6. 启动吹发后走开，等到“吹发收尾”再回来完成；做完跑去收钱。
7. 打完 Day 1，观察 `ClosingGrace → Result`，再确认是否能自然进入下一天。

请只记录三件事：有没有真正出现“这个还没完，那个又好了”的优先级冲突；有没有在 Dry 区空等；峰值顾客数是否让你觉得是在经营而不是纯排队。当前没有新增机制，试玩目标是验证现有洗、剪、吹节奏。

## J. 最终判断

- 技术上：代码、自动测试、资源检查、WebGL 构建、正式 Mobile 浏览器路径、Candidate/Reference QA 均已准备好；PR #1 可继续 review。
- 产品上：唯一尚未关闭的是真人手感与 Gameplay Gate，尤其是 Wash 离开/回来是否真有决策感、Dry 窗口是否舒服、Moderate 迟到是否需要可感知的额外操作时间。
- 阻塞项：没有本轮代码回归 blocker；另有既有 UI 字体审计缺字 `然走`，应作为独立质量债处理。
- 下一步：先让产品负责人按上面的 Day 1 步骤试玩并反馈，再决定是否只调数字，或是否需要更大设计变更；在此之前不进入 Phase 3。
