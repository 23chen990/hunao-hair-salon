# R1 验收收口审查

审查目标是核实 R1 现有实现和证据，只处理已确认的证据采样问题；本轮没有进入 R2，也没有新增玩法、货币、页面或运行时经济规则。

## 1. 审查版本与差异范围

| 项目 | 实际值 |
|---|---|
| 工作分支 | `phase-0-2-gameplay-gate` |
| 本地 HEAD | `2dcb19a110235d7e2bb5cab3fef7fe882868c160` |
| `origin/phase-0-2-gameplay-gate` | `2dcb19a110235d7e2bb5cab3fef7fe882868c160` |
| `origin/HEAD` | `a71e7279b5e21909a35ac4186b755c3d123d5ffa`（默认分支，不是本轮工作分支） |
| R1 前基线 | `6b7886986978e536635842c91a4d9051549fa67e0` |
| 被测运行时构建来源 | `2dcb19a...`；Unity 6000.5.8f1；`Builds/WebGLDemo` |

基线到交付 HEAD 的 R1 差异包括 `SalonDemo.cs`、`SalonDemo.Mobile.cs`、`SalonProgressSave.cs`、`SalonProximityPurchasePadModel.cs`、R1 相关测试、`tools/check-mobile-salon.py`、`tools/check-proximity-pad.py` 以及 R1 报告。`SalonGameModel.cs`、`BusinessDaySystem.cs`、`SalonSupplyModel.cs` 没有被 R1 提交修改，本轮按调用链审查了它们的实际行为。

本轮工作树只新增两项证据修正：

- [tools/check-proximity-pad.py](../../../tools/check-proximity-pad.py) 增加失败屏快照，并把重试值改为独立的 `retry*` 字段。
- [tests/test_check_proximity_pad.py](../../../tests/test_check_proximity_pad.py) 锁定快照必须在状态被重试覆盖后仍保留失败前的值。

原有 [R1_REPORT.md](R1_REPORT.md) 保留历史结论；本文件记录本次收口勘误和未决规则。

## 2. 结论分类

### CONFIRMED

- 施工模型按 180 金币、60 金币/秒计时，钱包成功后才增加 `Paid`；余额不足、离圈、暂停、重复解锁和超过剩余价格的边界由模型/集成测试覆盖。
- 补货模型是后场 12、携带上限 3、共享货架容量 6；取货、卸货和洗发消耗各只改变共享模型一次，第二货架只是同一库存的另一个卸货入口。
- 原 `check-proximity-pad.py` 在点击“再试一次”后才读取 `failedCompleted`；旧报告中的 `failedCompleted` 和 `failedBalance` 不能证明失败瞬间状态。这是已确认的证据采样缺陷，本轮已修正。
- 修正后的正式 Chromium 流程在失败屏记录 `0/3、target=3、balance=60、Paid=180、Unlocked=true`，点击重试后另记录 `retryCompleted=0、retryTarget=3、retryBalance=60、retryPaid=180、retryUnlocked=true`，两组数据没有互相冒充。
- 已完成购买的货架在失败重试后仍保留；正式流程也重新确认第二货架对应的 `OPEN` 状态和共享库存回到 12/0/0 的新日开店值。

### NOT_REPRODUCED

- 本轮没有复现重复扣费、`Paid` 超过 180、钱包拒绝后增加 `Paid`、暂停或离圈继续累计施工时间、洗发重复消耗 WashKit、第二货架复制共享库存、重复加载留下第二组对象等问题。
- 在本轮被测构建中没有发现浏览器控制台或失败资源请求；headless Chromium 的 `screen.orientation.lock() is not available` 是已知环境能力提示，按既有检查规则过滤。

### NOT_RUN

- 未在真实 iOS/Android 设备上运行。
- 没有用正式浏览器流程完整执行以下三个“同一次营业尝试内失败再重试”组合：开店未投入→部分投入→失败；开店未投入→完成购买→失败；开店已有部分投入→继续投入→失败。现有流程覆盖了它们的存档/模型边界，但不能把模型夹具当作正式入口全流程。
- 没有实现顾客位置、订单计时器或服务动作的中途续玩验证。

### BLOCKED

- 是否把“营业中安全检查点”视为刷新后新的 DayOpening，尚未有唯一明确的产品规则，阻塞这三种同局刷新后失败路径的最终判定。

### NEEDS_PRODUCT_DECISION

现有文档同时存在两种口径：`docs/mobile-demo-delivery-2026-09-16.md` 写“中途退出则从当天开店前重新开始”，而 R1 流程文档和现有实现允许离圈后保存施工检查点，并在刷新后从该检查点进入 `PreOpen`。需要负责人只决定一条规则：刷新后是否开启新的营业尝试并以已保存检查点作为新 DayOpening。代码目前保持 R1 交付时的行为，没有擅自改动。

## 3. 关键调用链与文件行号

- 启动加载：`SalonDemo.Mobile.cs:96-120` 创建 `SalonSupplyModel` 和 `SalonProximityPurchasePadModel`，`SalonProgressSave.cs:232-327` 读取、校验并兼容旧存档。
- 补货：`SalonDemo.Mobile.cs:357-392` 只调用 `TryPickUpWashKit`、`TryDeliverWashKit`；洗发入口 `SalonDemo.Mobile.cs:1029-1044` 在正式开始洗发后调用一次 `TryConsumeWashKit`。模型本身位于 `SalonSupplyModel.cs:20-63`。
- 施工：`SalonDemo.Mobile.cs:394-473` → `SalonProximityPurchasePadModel.CalculatePayment` → `SalonGameModel.Payments.TrySpend` → `ApplyPayment`；成功交易才保留计时余量、更新 `SupplyRackExpansionPaid` 并在完整付款时构建货架。
- 开店快照与安全检查点：`SalonDemo.Mobile.cs:573-596` 捕获当前可持久化状态；`598-656` 在 `PreOpen` 捕获 `_mobileOpening`，失败结果把 `_mobileOpening` 写回存档；普通保存不再覆盖 `_mobileOpening`。
- 刷新与重试：`SalonDemo.Mobile.cs:475-499` 的 `RestoreMobileGame` 把载入检查点捕获为当前 `_mobileOpening`；`691-719` 的 `RetryMobileDay` 恢复余额、扩建付款、货架对象和 DayNumber，再回到 `PreOpen`。
- 营业状态：`BusinessDaySystem.cs:437-498` 创建 `PreOpen/Business/Result`，`529-533` 负责普通重试；该控制器没有定义刷新是否开启新营业尝试的产品语义。
- 证据采样：`tools/check-proximity-pad.py:127-141` 在重试输入前复制失败快照，`260-284` 分别写入 `failed*` 与 `retry*`。

## 4. 已确认的失败回滚规则

失败结果触发时，运行时把 `_mobileOpening` 写回当前存档；因此在一次已经确定的营业尝试中，营业收入和施工投入应回到该次 `_mobileOpening`，而开店前已经拥有的扩建继续保留。`SupplyRackExpansionPaid` 与 `SupplyRackExpansionPurchased` 在 `SalonProgressSave.TryValidate` 中成套校验；缺少该字段的旧存档按旧布尔值兼容为 0 或完整 180。

本轮没有把“刷新后载入的检查点”强行解释为同一次尝试的旧 DayOpening。当前实现会在 `RestoreMobileGame` 结束时以载入检查点重新捕获 `_mobileOpening`，这正是需要产品决定的规则边界。

## 5. 路径 A/B 状态对照

| 路径 | 实际证据 | 余额 | Paid / Unlocked | 共享库存 | 第二货架场景状态 | 判定 |
|---|---|---:|---|---|---|---|
| A：开店→营业中投入→不刷新→失败→重试 | 本轮未完整执行正式入口 | 未形成可复核值 | 未形成可复核值 | 未形成可复核值 | 未形成可复核值 | NOT_RUN |
| B：部分投入→离圈保存→刷新→PreOpen | `partialReloadPaid=144` 与离圈保存 `paidAtExit=144`；`partialReloadBalance=96` | 96 | 144 / false | 新日临时库存 12/0/0 | 未解锁、无扩建货架 | CONFIRMED（刷新检查点） |
| B：刷新后重新营业并走向洗发区 | `routeStartPaid=165`；该值比 144 多出的 21 是重新营业后移动经过施工点产生的新投入，不能当作刷新加载值 | 以正式状态为准 | 165 / false | 起始仍为 12/0/0 | 未解锁 | CONFIRMED（路径继续） |
| B：完成购买→刷新→PreOpen | `reloadPaid=180`、`reloadUnlocked=true`、开店余额 60 | 60 | 180 / true | 12/0/0 | 第二货架已构建，锚点和碰撞随对象恢复 | CONFIRMED |
| B：已购买开店→失败→重试 | `failed*` 与 `retry*` 分开记录，均为 `0/3、60、180、true、12/0/0` | 60 | 180 / true | 12/0/0 | `OPEN` 保留 | CONFIRMED |

这组证据证明了安全检查点和已购买资产保留，但没有证明“同一次营业尝试在刷新前后的失败回滚基准必须相同”。该差异留给 NEEDS_PRODUCT_DECISION。

## 6. 本轮修改与最小性

已确认问题只在检查器采样时序：旧代码在重试后读取失败字段。修复只增加一个无副作用的 `capture_failure_snapshot`，在 `Result` 屏、发送“再试一次”之前复制失败字段；重试完成后另外写 `retryCompleted`、`retryTarget`、`retryBalance`、`retryPaid`、`retryUnlocked` 和 `retrySupply`。没有改 `SalonDemo`、存档格式、施工价格、扣费速度、补货容量或正式场景。

## 7. 实际执行命令、结果与限制

- `npm run test:assets`：17/17 通过。
- `python3 tools/check-ui-font.py`：367 个必需字形、629 个打包字形，通过。
- `python3 -m unittest tests/test_check_proximity_pad.py`：1/1 通过；先在旧脚本上失败，再在最小修复后通过。
- Unity 全量 EditMode：`R1AcceptanceFullEditMode.xml`，670/670，XML `result=Passed`、`failed=0`。其中 R1 相关夹具：`SalonProgressSaveTests 10`、`SalonMobileCheckpointRegressionTests 2`、`SalonProximityPurchasePadModelTests 12`、`SalonPurchasePadRuntimeIntegrationTests 7`、`SalonSupplyModelTests 5`、`SalonSupplyVisualRegressionTests 2`。
- `BuildScript.ValidatePipeline`：17 项 Manifest/资源检查通过。
- `BuildScript.BuildWebGLDemo`：构建成功，日志记录 `77,673,723 bytes`；运行时来源为 `2dcb19a...`。
- `python3 tools/browser-check.py --mode demo`：正式 `HairSalonDemo`，844×390、DPR 1，启动、核心服务流程和 A/B/C/D 视觉证据通过，console/failed requests 为 0。比例、位置、方向、阴影和安全区仍按视觉 QA 规则保留人工确认项。
- `python3 tools/check-proximity-pad.py`：正式 WebGL 移动入口，844×390，真实 Chromium 触控，补货、部分投入刷新、完整购买刷新、已购失败重试和路线对照通过，`errors=[]`。
- 960×540 启动冒烟：正式 `WebGLDemo` canvas 为 960×540，截图非空，console 仅有可过滤的 orientation 能力提示；这项只代表启动冒烟，不代表完整经营流程。

本轮没有把 Chromium 模拟触控写成 iOS/Android 真机通过，也没有把 960×540 启动写成完整流程通过。

## 8. 证据索引

- [证据包说明](evidence/README.md)
- [施工/刷新/失败重试 JSON](evidence/proximity-pad-report-r1-acceptance.json)
- [Unity 全量 EditMode XML](evidence/unity-editmode-r1-acceptance.xml)
- [正式 Demo 844×390 视觉 QA JSON](evidence/visual-qa-report-844x390.json)
- [960×540 启动冒烟截图](evidence/startup-smoke-960x540.png)

较大的录屏和完整 Builds 目录仍按仓库规则留在本机，未强行加入 Git；本轮必要 JSON、XML 和启动截图已放入可远端访问的证据目录。

## 9. 技术结论

**BLOCKED**。已确认的失败快照证据问题已最小修正，相关测试、Unity 全量 EditMode、Manifest 校验、WebGL 构建和正式 Chromium 检查均通过。R1 收口仍被“刷新是否开启新营业尝试、检查点是否成为新的 DayOpening”这一唯一产品规则冲突阻塞；没有据此修改运行时行为。

## 10. 合并与 R2

PR #1 保持未合并。是否允许合并以及何时进入 R2 由产品负责人决定；本轮不会自动合并，也不会继续 R2 开发。第二货架的普遍玩家时间价值仍保留 `VALUE_NOT_PROVEN`，本轮没有扩大成长系统或调整路线参数。
