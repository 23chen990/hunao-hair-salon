# 玩法与操作修复 · 2026-09-20

本轮按最新反馈优先修操作和整局流程，不做 UI 排版、美术或房间布局调整。现有进度条的颜色与操作文字只用于正确反馈按住、松手和暂停状态。

## 2026-09-23 参考《胡闹厨房》调整活动时长

《胡闹厨房》普通关卡的可操作倒计时通常落在约 3–4 分钟范围；它把高压并行任务集中在关卡计时内，结算/过场是独立缓冲。本作当前是单人移动操作、任务密度较低，因此取该范围下沿：正式移动营业段从 120 秒调整为 180 秒，原有最多 15 秒 ClosingGrace 保持为离场/结算缓冲。

本次只改 `SalonMobileDayConfig.BusinessDurationSeconds` 和开店说明文案，没有调整目标订单、客流间隔、顾客耐心、服务时长或错误处理规则。调整后真实 Chromium 触控仍通过 O002 正确接待路径与错误剪发工位后纠正洗发路径；证据分别为 `Builds/MobileEvidenceSecondCustomerAcceptanceOvercooked180/report.json` 和 `Builds/MobileEvidenceWrongStationOvercooked180/report.json`。这是竞品类比后的当前取值，仍需真人试玩确认首局实际节奏。

## 后续实玩修正 · 2026-09-22

真实手动走完首日开局后发现，接待第一位顾客再跑去工位的过程中，后续顾客仍按完整排队速度掉耐心；正常手速下第二位顾客在完成第一次工位安排前就接近流失。这与交接文档要求的“首日教学、交错服务、目标可完成”不一致，也让压力变成了移动耗时，而不是《胡闹厨房》式的任务优先级选择。

修正为：玩家接待顾客后，该顾客进入护送宽限，直到被安排到工位前不再继续扣等待耐心；移动入口的基础耐心速度从 `1.15` 调整到 `0.85`，只缓和首日教学节奏，不改变后续服务、后台吹发和失败规则。新增 `MobileHandoffGivesAPlayerTimeToEscortTheSelectedCustomer` 回归测试，确认护送中的顾客不会在跑向工位时自行流失。

继续检查后又发现，已接待顾客原本会硬锁下一次交互：即使玩家已经走到需要冲洗或收尾的在座顾客旁边，系统仍强迫玩家先把新客安排到工位。这会直接抹掉多任务选择。现在“安排新客”只作为候选任务保留；玩家靠近已准备好的在座服务时，可以先完成该服务，接待对象和护送宽限都不会丢失。新增 `ReadyServiceWinsOverAGuidedAssignmentWhenItIsCloser` 回归，锁定这一行为。

设备占用复核还发现一个误导操作：顾客完成当前步骤、需要转移到下一类设备时，即使全部兼容工位都在使用中，近身按钮仍显示可执行的“转移顾客”。现在转移前先检查真实占用；无空位时按钮显示“等待空闲工位”且不可执行，释放任一兼容工位后才恢复“转移顾客”。新增 `TransferTargetIsDisabledWhenAllCompatibleStationsAreOccupied` 回归，避免不可完成任务挤掉当前可做的服务。

随后复现到达标瞬间的生命周期问题：最后一位顾客完成服务后，日结判断把 `Finished/Leaving` 排除在活跃顾客外，结果面板会在离店动画开始前清掉顾客。现在移动版日结计数会保留所有尚未进入 `Exited` 的顾客；达标后停止接待新客，但会等最后顾客走出店外再进入结算。新增 `FinishedCustomerRemainsActiveUntilItExitsTheShop` 回归。

## 与《胡闹厨房》和交接构想的对照

这次实玩确认，当前 Demo 已经有交接文档要求的“移动到顾客 → 安排工位 → 前台服务 → 后台吹发时切换顾客 → 回来收尾 → 达标结算”骨架；最终触控回归也确实记录到了后台吹发期间处理另一位顾客。但它和《胡闹厨房》的差距仍然很明确：

- 《胡闹厨房》的压力来自同时处理可见的订单步骤、食材/设备占用和交付优先级；当前 Demo 的压力主要来自跑图、排队耐心和一个前台长按动作，空间任务还没有形成同等密度。
- 交接构想要求首日先教会移动、接待、工位安排和一次后台切换，再逐步增加复杂度；原来的完整耐心扣除会在玩家学会路线前先制造流失，接待硬锁又会阻止玩家中途收尾，本轮已修复护送宽限、首日耐心节奏和接待目标锁定。
- 当前仍是单理发师单前台操作，后台吹发是唯一稳定的并行任务；没有擅自加入食材、多人、额外货币或新页面，因为这些超出已批准范围。

因此本轮只修改了能由实玩直接证明的阻塞点；“操作对象还不够丰富”和“任务优先级层次少”是下一轮体验问题，不能靠继续加快客流掩盖。

## 已落实的行为

| 场景 | 原来的问题 | 现在的行为 |
| --- | --- | --- |
| 剪发 | 移动端点击后自动按完美时长完成，玩家无法控制结果 | 按住操作，进度变绿后松手；过早松手可补剪，过久会剪坏 |
| 后续剪发订单 | 移动端始终使用单步剪刀，洗发后的剪发也可能丢失工具顺序 | 第一单保持单步，后续使用现有剪刀、打薄剪、电推剪规则，按每步实际操作完成 |
| 正在接待新客，椅子被占 | 接待状态把“安排新客”硬锁为下一次交互 | 可以保留接待对象；靠近座位时先冲洗、剪发或吹发收尾，再回来安排新客 |
| 泡沫等待与附近可用工位 | 更近但暂时不能操作的顾客可能抢走交互 | 优先选可操作的近身目标 |
| 剪发途中暂停或切出应用 | 输入重置被当作提前松手，恢复后扣满意度 | 保留剪发进度，恢复后重新按住继续 |
| 剪坏订单 | 顾客结束服务仍算完成订单 | 失败订单不计目标，也不给收入 |
| 完成订单后的收入 | 订单完成后还需要额外处理掉落物，容易打断下一位顾客的接待 | 订单完成时立即把收入结算到余额，不生成金币堆，也不再需要额外收钱步骤 |
| 当天接单量 | 目标 3 单却接进 8 人，达标后仍因积压清场被大幅扣分 | 已完成和未完成订单合计够当天目标就暂停收客；失败/流失后补客，全部完成即可结算 |
| 首日中段客流 | 只来洗发开头的订单，洗发区容易成为唯一瓶颈 | 在原有订单中混入剪发开头的订单 |
| 双击 WebGL 的 index.html | file:// 无法加载 Unity 数据 | 提供 HTTP 试玩入口和根目录的「启动试玩.command」 |

## 回归依据

使用真实移动端控制器及服务模型，先复现了接待锁住工位、剪坏计数、暂停误松手、达标后仍继续收客四个失败，再修复到通过。专项包括提前松手重试、分两步剪发、暂停/后台恢复、接待时收尾、泡沫等待不抢操作和营业结算。

全量 Unity EditMode：628/628；Node 资产管线：17/17；Manifest/资源检查及字体字符检查通过。Demo、资产实验室、候选场景和参考场景四个 WebGL 构建通过；正式 Demo 已重新构建。

真实浏览器进一步复现了三处模型测试没有覆盖的问题：多指同时松开时暂停的点击回调晚于剪发松手、已完成目标却因接单过量在清场时把满意度降到 5，以及收入结算步骤会打断后续接待。前两项分别改为按下暂停即暂停、按每日剩余目标限制接单；后一项改为订单完成后立即结算收入。

最终 Chromium 844×390 实测：首日 3/3 单、余额 540、满意度 69；完整触控回归确认订单收入自动结算，过程中不出现金币堆或“收取”步骤。确认吹发运行时处理另一位顾客、接单量不超过目标、结算后刷新、进入第二天与中途刷新恢复。全程无控制台错误。失败局 0/3 单，免费重试回到第 1 天、资金 0、满意度 90。

提前松手补剪、多指暂停恢复、剪坏不计目标的真实触控验证另行完成，终版整局复用手势证据。第一遍有限接单流程虽已通关，但脚本先剪完再启动吹发，没有覆盖交错服务，因此该轮未记为通过；调整操作顺序为先启动吹发再剪另一位后，相同构建通过了完整检查。

证据位置：

- 整局与存档报告：`unity-hair-salon/Builds/MobileEvidenceFinal3/report.json`。
- 失败重试报告：`unity-hair-salon/Builds/GameplayFinalFailure/report.json`。
- 手势回归：`unity-hair-salon/Builds/GameplayRepairVerified/` 中的 `11-early-release-retry.png`、`12-resumed-haircut.png`、`13-overcut-no-goal-credit.png` 及状态/操作记录。
- 最终结算截图：`unity-hair-salon/Builds/GameplayDeliveryEvidence/05-result.png`；后台服务截图：同目录 `03-background.png`。
- 自动测试 XML：`unity-hair-salon/Builds/PipelineEditMode.xml`；资源及构建记录：`PipelineValidate.log`、`WebGLDemoBuild.log`；场景浏览器记录：`PipelineEvidence/browser-check-all.json`。

- 金币拾取专项：`unity-hair-salon/Builds/MobileEvidenceFinal3/actions.json` 与 `report.json`。
- 多步骤订单专项：`MobileGameplayRuntimeTests.WashThenDryOrderKeepsTheWholeOrderAliveUntilDryFinishes` 锁定洗发完成后仍需转移到吹发，最终只生成一笔付款；最新全量门禁 XML 为 `unity-hair-salon/Builds/PipelineEditMode.xml`（628/628）。
- 最新移动触控复核：`unity-hair-salon/Builds/MobileExperienceAuditDeepFixFinal/report.json`，真实 844×390 Chromium 触控通过，3/3 单、余额 540、满意度 69、金币堆收取 0 次、后台吹发期间完成另一项服务、无浏览器错误。
- 接待并行回归：`MobileGameplayRuntimeTests.GuidingAnotherCustomerDoesNotBlockFinishingAnOccupiedStation` 与 `ReadyServiceWinsOverAGuidedAssignmentWhenItIsCloser`，确认新客已接待时仍能释放占用工位，并按玩家实际靠近的位置选择可执行服务。
- 本次修复后的全量 Unity EditMode XML 为 628/628；四个 WebGL 构建和正式 Demo 浏览器核心流程均通过。
- 设备占用门禁加入后，全量 Unity EditMode 为 629/629；四个 WebGL 构建和 844×390 四场景 Chromium 检查通过。新构建的真实触控整局仍为 3/3 单、余额 540、满意度 69，并确认后台吹发期间完成另一项服务；证据在 `Builds/MobileEquipmentPriorityFinal/`。
- Node 资产测试为 17/17。设备占用提示曾引入字库没有的“都”字，字体门禁准确阻止了它；改用现有字库可覆盖的等义提示后复检通过。Unity、Manifest/资源、四个 WebGL 构建和场景浏览器门禁均通过。
- 本轮新增离店时序修复后，日结不会再截断最后顾客的离店动画；闭店宽限仍按原有规则处理未完成订单。
- 离店时序最终门禁：Unity EditMode 630/630，真实 844×390 触控 3/3 单、余额 540、满意度 69、无浏览器错误；生命周期记录中三位顾客均走到 `Finished → Leaving`，结算随后才出现。证据在 `Builds/CustomerExitTimingFinal/`。

模型层的历史压力模拟没有人物行走，只用于验证客流规则；不能用它声称游戏已经好玩。压力测试单独保持未达标，以验证整段客流曲线；实际达标与支付结算另按真实目标验证。

## 试玩方法

1. 打开 [本机新版 Demo](http://127.0.0.1:8910/WebGLDemo/)，或在 Finder 双击仓库根目录的「启动试玩.command」。保留启动后出现的终端窗口。[资产实验室](http://127.0.0.1:8910/WebGLAssetLab/) 仍可运行。
2. 摇杆、WASD 或方向键移动，靠近顾客接待，再走到空闲且匹配的工位安排。
3. 剪发按住右侧操作按钮或空格，进度变绿后松开。过早松开可重新按住补剪。
4. 洗发打好泡沫、吹发启动后可以照顾其他顾客，回来冲洗或收尾。
5. 顾客完整完成服务后，收入立即结算到余额，随后可以直接继续处理下一位顾客。

## 限制

这是对已复现问题的修复，不代表已经完整符合产品负责人的体验预期。真实浏览器自动操作也不能替代产品试玩。本轮不改 UI 视觉；仍使用现有布局、角色、订单与设备系统，没有接入正式广告。

服务的计时窗口和满意度惩罚仍沿用现有规则；本次实测存在泡沫收尾较晚的扣分，满意度为 69，并非无失误流程。后续节奏是否合适仍以实际试玩反馈为准。

本轮交付与实测针对 WebGL 浏览器版本，已有 macOS 应用包没有重新生成。请使用上面的 HTTP 入口体验这次改动。

## 2026-09-23 顾客接待与离场回归修复

产品负责人随后指出两个仍未解决的实际阻塞，本轮按 `$salon-bug-fix` 重新复现，而不是沿用旧的模型测试结论。

### 第一个顾客离场瞬间消失

旧路径从右侧工位斜穿中央区域，`SalonDemo.UpdateCustomerViews` 的剪发工位碰撞回滚会把 `Leaving` 顾客每帧拉回原处；模型在 3.4 秒后进入 `Exited`，因此看起来像瞬间消失。修复为：离场先进入低位安全通道，再经过左侧出口；`Leaving` 不再触发剪发工位移动回滚；离场时长调整为 4.1 秒以覆盖右墙工位的实际路径。路径回归检查明确避开两张正式剪发工位碰撞和出口植物碰撞。

### 第二个顾客无法接待

旧的 `_mobileGuidedCustomer` 会在第一位顾客离场后残留，目标搜索因此跳过后续等待顾客；队列顾客还必须先抵达目标点才会被认为可接待。修复为：交互开始时清理失效引导对象；等待顾客可直接作为接待目标；`Greet` 和 `Guide` 使用独立分支，接待第二位不会被旧护送状态吞掉。

### 结果状态提前清场

如果最后顾客刚完成服务就进入 `Result`，旧逻辑立即停止更新顾客视图并清场。现在有 `Finished/Leaving` 旅程时暂缓结果结算，持续推进离场，顾客进入 `Exited` 后才显示结果面板。

### 回归与真实证据

- `ReportedGameplayRegressionTests`：3/3，通过“离场状态不提前删除”“最后一位离场后才结算”“第一位离场期间可接待第二位”。
- `CustomerExitRouteRegressionTests`：2/2，通过两类正式剪发工位路线的时长和家具碰撞检查。
- 最终整体验收：Node 17/17；Unity EditMode 635/635；Demo、Asset Lab、Candidate、Reference 四个 WebGL 构建成功；844×390 Chromium 检查通过且无浏览器错误。
- 正常真实触控：`unity-hair-salon/Builds/MobileEvidenceReportedFinal3/report.json`，首日 3/3 单、余额 540、满意度 69；第一位顾客最大屏幕位移 226.08px、到达出口距离约 0；第一位为 `Leaving` 时第二位执行 `Greet`。
- 离场截图：`unity-hair-salon/Builds/MobileEvidenceReportedFinal3/14-first-customer-leaving.png`。
- 失败重试：`unity-hair-salon/Builds/MobileEvidenceReportedFailureFinal2/report.json`。

### 试玩步骤

打开 `http://127.0.0.1:8910/WebGLDemo/`，完成第一位顾客的服务；在第一位出现离场状态后，立即靠近第二位等待顾客并点击接待。第一位应沿低位通道连续走到左侧出口，第二位应进入护送；最后一位真正离店后才出现日结面板。

限制仍是 844×390 横屏 WebGL 证据，满意度本次为 69；没有重新生成 macOS Standalone 包，也没有改变当前批准的房间布局、角色和 UI 方向。

## 2026-09-23 第二位顾客接待后的指引断档修复

再次实玩发现，上一版虽能记录第二位的 `Greet`，但接待后玩家仍会看到灰色“靠近顾客”按钮，无法判断下一步。根因是引导顾客距离工位超过 1.5 时被目标筛选丢弃；`guided=1` 仍在模型里，但交互呈现退回通用接待提示。

现在引导顾客在移动途中始终保留为当前目标：未到站时显示“前往洗发工位”，到达兼容空闲工位后显示“安排洗发”。本轮没有改变 O002 订单、工位占用或房间布局。

真实 Chromium 触控已完整走通首日 O002 首段：第一位顾客仍在 `Leaving` 时，第二位先显示“接待 2 号”；点击后显示“前往洗发工位”；走到主洗发锚点后显示“安排洗发”；点击后第二位进入 `MovingToStation`、分配到工位 0。证据目录：`unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptance/`。

专项结果：`SecondCustomerReceptionRegressionTests` 1/1，`ReportedGameplayRegressionTests` 3/3，`CustomerExitRouteRegressionTests` 2/2；最终整体验收 Unity EditMode 636/636、Node 17/17、四个 WebGL 构建及 844×390 Chromium 检查通过。

## 2026-09-23 错误工位路径恢复

后续实玩确认第二位顾客 O002 是洗发顾客。之前的实现虽然修好了“接待后持续指引”，却把目标工位筛选限制为兼容工位，玩家带她去剪发区时无法点击安排。这不是新的产品规则，而是移动入口覆盖不足；此前已经批准的规则是：物理上无法执行的动作才禁用，流程上做错允许执行并留下后果。

已恢复为：引导中的顾客优先显示兼容工位作为推荐，但只要附近有任意空闲工位，就可以实际安排。O002 在剪发工位会显示“安排剪发工位”，点击后进入剪发工位移动；模型记录一次 `WrongStationCount`、困惑反应和满意度损失。顾客到达后显示“转移顾客”，玩家可再带到洗发工位；纠正清除当前困惑反应，但保留错误次数和已经发生的满意度损失。占用中的工位仍不可用，避免把物理不可能伪装成可执行。

真实触控复核结果：

- 正确路径报告：`unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptanceFinal2/report.json`，O002 的 `Wash` 需求仍可完成接待和洗发安排。
- 错误路径报告：`unity-hair-salon/Builds/MobileEvidenceWrongStationFinal2/report.json`，真实动作是“接待 2 号”→“安排剪发工位”→“转移顾客”→“安排洗发”；错误工位次数为 1，满意度从 70 降到 67，最终回到洗发工位，无浏览器错误。
- `MobileAdmissionRegression` 14/14、`SecondCustomerReceptionRegressionTests` 2/2、`ReportedGameplayRegressionTests` 3/3、`CustomerExitRouteRegressionTests` 2/2；Unity 全量 EditMode 638/638，Node 17/17，UI 字体和四个 WebGL/844×390 Chromium 门禁通过。

试玩时可以按两条路径验证：接待 O002 后直接去洗发区，或故意去剪发区点击“安排剪发工位”，观察困惑与扣分，再点击“转移顾客”纠正。这里没有重新设计玩法或场景，只把已决定的犯错空间重新接回实际移动入口。
