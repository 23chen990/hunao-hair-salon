# R1 验收收口审查

本轮只完成 R1 验收收口，不进入 R2，不新增玩法、货币、页面、成长参数或主场景布局。产品负责人已明确刷新、开店和失败重试规则，本报告按该口径判定，不再把同一刷新问题标为 `NEEDS_PRODUCT_DECISION`。

## 1. 实际版本和差异范围

本轮开始核对结果：工作树干净，当前分支为 `phase-0-2-gameplay-gate`，本地 HEAD 和 `origin/phase-0-2-gameplay-gate` 都是 `6df532c4b1bc800442983c751c19dc355417692e`；`origin/HEAD` 为默认分支 `a71e7279b5e21909a35ac4186b755c3d123d5ffa`，没有把默认分支当作本轮目标。

验收材料交付后的最终本地 HEAD 与远端目标分支为 `195f04b72e6e1bcf070789684b7b7bd5e1bbf5e5`。

WebGL 被测运行时来自 Unity 6000.5.8f1、`unity-hair-salon/Builds/WebGLDemo`，构建时运行源码为 `2dcb19a110235d7e2bb5cab3fef7fe882868c160`，构建记录为 77,673,723 bytes。本轮确认运行代码已经符合批准规则，因此没有修改 `SalonDemo`、存档格式、钱包、施工价格、容量、主场景或游戏流程；新增内容只包括回归夹具、正式入口证据脚本、证据 JSON/XML 和本报告。

## 2. 本轮采用的批准规则

- 同一次营业中的普通保存只更新最近检查点，不覆盖这次营业的 `DayOpening`。
- 刷新或重新打开加载最近一次完整、有效的保存，进入当天 `PreOpen`；不判定成功、不推进营业日、不补发结算奖励。
- 再次点击开始营业时，以此刻准备状态建立新的 `DayOpening`；准备阶段已经完成的合法施工和补货状态纳入新的营业尝试。
- 失败重试恢复本次开始营业时的 `DayOpening`，余额、`Paid`、`Unlocked`、库存和场景对象随之恢复。
- 失败处理结束后再刷新，加载失败处理后的有效保存，不回到失败前的旧检查点。
- 失败前刷新允许已保存进展进入下一次尝试；不实现反退出、反刷档或顾客现场续玩。

## 3. 运行调用链核查

`SalonDemo.Mobile.cs:475-491` 的 `RestoreMobileGame` 先加载并校验最近有效存档，准备当天后回到 `PreOpen`，再把载入状态捕获为当前 `_mobileOpening`；不会推进营业日或发放结算奖励。`SalonDemo.Mobile.cs:573-596` 的 `SaveMobileCheckpoint` 捕获当前状态并保存，不写 `_mobileOpening`，所以普通营业中检查点不会覆盖本次开店基准。

`SalonDemo.Mobile.cs:602-616` 在非恢复进入 `PreOpen` 时捕获新的 `_mobileOpening`，因此刷新后再次点击开始营业会以实际准备状态建立新基准。`SalonDemo.Mobile.cs:618-653` 在失败结果把 `_mobileOpening` 写回存档并清理本局临时状态；`SalonDemo.Mobile.cs:691-719` 的重试恢复余额、扩建付款、扩建对象、同一营业日和准备阶段，再保存失败处理后的有效状态。

存档的 `SalonProgressData.TryValidate/Clone` 和仓储的 primary/backup 读取保证了完整有效存档边界。营业中的 WashKit（后场/携带/共享货架）属于当天现场临时状态，进入 `PreOpen` 会按批准的准备流程重置为 12/0/0；本轮证据记录的是开店和失败回滚后的 12/0/0，不宣称未保存的顾客现场或临时携带状态跨刷新续玩。

结论：运行代码无需修改。当前实现与批准的刷新、重新开店、失败重试和失败后刷新规则一致。

## 4. A：不刷新时的本局施工回滚

证据类型：正式 WebGL 移动入口，Chromium 真实触控，目标横屏 844×390。首次合法赚取余额、部分投入并保存后刷新只是准备条件；A 分支本身从该有效 `PreOpen` 开始，点击开始营业后没有刷新，发生实际追加投入，随后自然失败并点击真实重试。

状态来自 `evidence/r1-rollback-report.json`：

| 阶段 | phase | 余额 | Paid | Unlocked | 库存（source/carried/rack） |
|---|---|---:|---:|---|---|
| 初始有效保存 | Business | 162 | 78 | false | 12 / 0 / 0 |
| 本次开店 `openingA` | Business | 162 | 78 | false | 12 / 0 / 0 |
| 实际追加投入 `changedA` | Business | 110 | 130 | false | 12 / 0 / 0 |
| 自然失败前 `failed*` | Result | 74 | 166 | false | 12 / 0 / 0 |
| 点击重试后 `retry*` | PreOpen | 162 | 78 | false | 12 / 0 / 0 |

`changedA` 的余额和 `Paid` 都发生了实际变化；失败前采样保留在点击重试之前，重试后回到 `openingA`。第二货架在两组状态中均为未解锁，交互状态与 `Unlocked=false` 一致。该路径通过正式入口证明了部分投入边界；完整购买边界由第 5 节的正式 B 路径和 Unity 模型/存档夹具共同覆盖。

从 Pad 走向安全位置的短暂离圈过程中，正式入口又提交了少量合法施工，所以 `failed*` 比 `changedA` 多 36 Paid、少 36 余额；这也是失败前采样的实际状态，不是重试覆盖造成的差异。

## 5. B：刷新后新基准用于后续失败回滚

证据类型：正式 WebGL 移动入口，Chromium 真实触控，844×390。

流程是：部分投入并离圈保存 → 刷新到 `PreOpen` → 再次点击开始营业 → 在新尝试中继续投入至完整解锁 → 自然失败 → 在失败屏采样 `failed*` → 点击重试。

| 阶段 | phase | 余额 | Paid | Unlocked | 库存（source/carried/rack） |
|---|---|---:|---:|---|---|
| 刷新前有效保存 `savedBeforeRefresh` | Business | 70 | 170 | false | 12 / 0 / 0 |
| 刷新恢复 `refresh` | PreOpen | 70 | 170 | false | 12 / 0 / 0 |
| 刷新后新 `openingB` | Business | 70 | 170 | false | 12 / 0 / 0 |
| 新尝试完成购买 `changedB` | Business | 60 | 180 | true | 12 / 0 / 0 |
| 自然失败前 `failed*` | Result | 60 | 180 | true | 12 / 0 / 0 |
| 点击重试后 `retry*` | PreOpen | 70 | 170 | false | 12 / 0 / 0 |

重试目标是刷新后重新开店时的 `openingB`（70/170/false），不是最初的旧状态，也不是失败前的 60/180/true。`changedB` 的完整购买是实际触控产生的状态变化；失败后的第二货架回到未解锁状态，和 `openingB` 一致。

## 6. D：失败处理后再次刷新

同一正式入口报告的 D 段在重试完成后立即刷新：`afterFailureRefresh` 为 `PreOpen`、余额 70、`Paid=170`、`Unlocked=false`、库存 12/0/0，与 B 的 `retry*` 完全一致。它证明刷新读取的是失败处理后的有效保存，而不是失败前的旧检查点。

## 7. C：开店前已有扩建的原有回归

原有 `tools/check-proximity-pad.py` 正式入口证据继续保留：已购买扩建在开店前进入 `DayOpening`，自然失败后 `failed*` 与 `retry*` 均为余额 60、`Paid=180`、`Unlocked=true`、库存 12/0/0，第二货架保持 `OPEN`。对应 JSON 为 `evidence/proximity-pad-report-r1-acceptance.json`。

补充夹具 `SalonR1CheckpointRollbackIntegrationTests` 使用真实钱包、施工 Pad、补货模型和 `ISalonProgressRepository` 的 Save→Load，覆盖完整购买失败回滚、刷新检查点成为新基准、开店前已购扩建保留三条边界。该 XML 属于模型/存档集成证据，不冒充正式入口实测：`evidence/unity-r1-checkpoint-integration.xml`，3/3 通过。

## 8. 测试、构建和启动结果

- `python3 -m unittest tests/test_check_proximity_pad.py`：1/1 通过，失败快照和重试快照分开保存。
- `python3 tools/check-r1-rollback.py`：正式 WebGL 移动入口 A/B/D 通过，`passed=true`、`errors=[]`。
- `python3 tools/check-proximity-pad.py`：原有施工、补货、完整购买刷新和开店前已购扩建失败重试通过，`errors=[]`。
- Unity 定向命令：`/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /Users/kker/Documents/ChatGPT/game2/unity-hair-salon -runTests -testPlatform EditMode -testFilter SalonR1CheckpointRollbackIntegrationTests -testResults /Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/R1R1IntegrationTests.xml -logFile /Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/R1R1IntegrationTests.log`：3/3 通过，XML `result=Passed`、`failed=0`。
- Unity 全量 EditMode 基线：`evidence/unity-editmode-r1-acceptance.xml`，在本轮新增夹具前的运行代码/工作树 HEAD `6df532c` 上为 670/670，`result=Passed`、`failed=0`；本轮新增夹具另以定向 XML 3/3 验证。命令未与 `-quit` 同用，按 XML 判定。
- `npm run test:assets`：17/17 通过；`BuildScript.ValidatePipeline`：17 项 Manifest/资源检查通过。
- `python3 tools/check-ui-font.py`：367 个必需 CJK 字形、629 个打包字形。
- `BuildScript.BuildWebGLDemo`：成功，`Builds/WebGLDemo` 77,673,723 bytes；构建标识为运行时源码 `2dcb19a` + Unity 6000.5.8f1。
- `python3 tools/browser-check.py --mode demo`：正式 `HairSalonDemo`，844×390、DPR 1，启动和核心流程通过，console/failed requests 为 0。
- 960×540 启动冒烟：canvas 960×540、截图非空；只代表启动检查，不代表完整经营流程。

关键证据可直接访问：[A/B/D 正式入口 JSON](evidence/r1-rollback-report.json)、[原有施工/已购扩建 JSON](evidence/proximity-pad-report-r1-acceptance.json)、[Unity R1 定向 XML](evidence/unity-r1-checkpoint-integration.xml)、[全量 EditMode XML](evidence/unity-editmode-r1-acceptance.xml)、[844×390 视觉 JSON](evidence/visual-qa-report-844x390.json)、[960×540 截图](evidence/startup-smoke-960x540.png)。

## 9. 剩余范围和技术结论

本轮没有相关 `BLOCKED` 或 `NEEDS_PRODUCT_DECISION`。以下项目仍是 `NOT_RUN`，不改变本轮 R1 规则结论：真实 iOS/Android 设备验证；顾客位置、订单计时器和服务动作的中途续玩；失败结果存在可见离场动画时的延迟结果边界。960×540 只做启动冒烟。临时 WashKit 不属于跨刷新现场续玩存档字段，证据只保证准备阶段和失败重试后的 12/0/0 基准。

成长价值仍保留 `VALUE_NOT_PROVEN`；本轮没有重新测量成长价值，也没有调整路线、价格、容量、客流、服务时长或主场景布局。

**技术结论：PASS（R1 验收范围）**。批准的刷新、新营业尝试、部分/完整施工变化、失败回滚、开店前扩建保留和失败后刷新规则均有对应正式入口或模型/存档证据；运行代码无需修改。PR #1 保持未合并，完成后停止，不进入 R2。
