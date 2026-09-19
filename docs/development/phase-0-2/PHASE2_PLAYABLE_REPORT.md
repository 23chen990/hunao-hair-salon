# PHASE2_PLAYABLE_REPORT.md

> 本轮范围：Phase 0 冻结基线 → Phase 1 主路径与状态所有权审计 → Phase 2 建立「洗 + 剪 + 吹」基础忙乱 Demo。
> 本轮**没有**实现染发、烫发、顾客性格、情侣、网红、通用 Event 系统、Daily Scenario Generator、Day 1—30、扫地、毛巾、洗衣机、投诉、Purchase Pad、区域扩建、通用设备升级树、节日活动、后端、IAA。

## 0. 交付索引

| 文档 | 内容 |
| --- | --- |
| `PHASE0_BASELINE.md` | 改动前的基线 HEAD、工作区状态、测试结果 |
| `MAIN_PATH_AUDIT.md` | 移动 / 桌面 / ServiceArchitecture 三条路径的差异审计 |
| `STATE_OWNERSHIP.md` | 状态所有权定义与 Single Owner Rule |
| `TECH_DEBT_NEXT.md` | 本轮明确不修的技术债登记（D1—D10） |
| `PHASE2_PLAYABLE_REPORT.md` | 本文件 |
| `PRODUCT_OWNER_SUMMARY.md` | 非技术版总结与待决事项 |

---

## 1. 版本与控制点

| 项 | 值 |
| --- | --- |
| 仓库 | https://github.com/23chen990/hunao-hair-salon （公开） |
| baseline commit | `5dde4ea`（`chore: checkpoint Phase 0-2 gameplay baseline`） |
| final commit | `phase-0-2-gameplay-gate` 分支 HEAD（即 PR head，文档里不写死 SHA 以免修订后失效） |
| baseline → final 提交数 | 5 个，见本文件第 9 节上方的提交清单 |
| 开发分支 | `phase-0-2-gameplay-gate` |
| 建议 review 方式 | GitHub PR diff：`5dde4ea...phase-0-2-gameplay-gate` |

### Baseline 的来源说明（重要）

`a71e727`（旧 `main`）只跟踪了 610 个文件。基线提交时，工作区里还有 **653 个文件长期处于未跟踪状态**（包括
`SalonDemo.Mobile.cs`、`SalonMobileDayConfig.cs`、`BusinessDaySystem.cs`、`tool/bundle`、
AssetTestLab / ReferenceVisual / WashCraft 等），并且仓库**没有任何 `.gitignore`**，
`Library/Builds/Artifacts`（约 4 GB）随时可能被误提交。

因此 `5dde4ea` 做的第一件事是**只加 `.gitignore` 并把已有工作全部纳入版本控制**，不含任何玩法改动。
换句话说：这个 baseline 是「本轮开始前这台机器上真实存在的工作状态」，而不是「被裁剪过的对照版本」。

新增 `.gitignore` 排除了：`Library/`、`Builds/`、`Artifacts/`、`Logs/`、`UserSettings/`、`Temp/`、`obj/`、
`.gradle/`、`.idea/`、`.vs/`、`.vscode/`、`node_modules/`、`__pycache__/` 等生成物。
已确认 `git ls-files` 中不含任何上述目录。

**没有使用 Git LFS**：本机未安装 git-lfs，且 `:https://github.com/23chen990/hunao-hair-salon` 树内含 Album 最大单文件
`assets/source/wash-craft/room-finish.blend1`（10 MB），远低于 GitHub 100 MB 单文件限制，
tracked tree 总计 154 MB。本轮没有删除任何正式资产。

---

## 2. 现在玩家可以怎么玩？

正式产品路径 = **横屏移动：摇杆跑动 + 靠近后一个交互按钮**。桌面点击 / 工具栏保留作调试入口。

从开店开始的完整流程：

```
开店（Starting 1.25s）
        ↓
顾客从门口进场            ← 顾客状态 Entering
        ↓
站在等候区，耐心开始下降  ← Waiting；HUD 左侧队列条显示编号 / 订单图标 / 耐心百分比
        ↓
【玩家】靠近顾客 → 点「引导」按钮把它带到工位
        ↓
顾客走到工位（被占用）    ← MovingToStation → Serving；头顶订单气泡显示剩余步骤
        ↓
按订单执行：洗发 / 剪发 / 吹干（详见第 3—5 节）
        ↓
订单全部完成 → 顾客头上出现钱币掉落
        ↓
【玩家】跑过去收钱        ← PaymentDrop → Balance 增加
        ↓
顾客离场，工位释放
        ↓
120 秒营业结束后进入 15 秒 ClosingGrace，未做完的顾客被 ForceClose（不重复结算）
```

工位布局（来自 `SalonGameModel` 默认配置）：

| 工位 | 类型 | 支持 |
| --- | --- | --- |
| 0 | Wash | Wash |
| 1 | Haircut | Cut / Dry / Dye |
| 2 | Haircut | Cut / Dry / Dye |
| 3 | Perm | Perm |
| 4 | Wash | Wash |

**本轮可用的订单**：`O001`（剪）、`O002`（洗+吹）、`O003`（洗+剪）、`O004`（剪+吹）、`O005`（洗+剪+吹）。
`SalonMobileDayConfig.PickOrderForSpawn` 会按天数与营业进度逐步升级订单复杂度，Day 1 前两位顾客固定为
`O001`、`O002`，作为隐形教学。

---

## 3. Wash 现在是什么节奏？

### 改动前的样子

一次 3 秒定时服务：**玩家站在洗头床前被锁死 3 秒**，一次性完成打湿 + 洗发 + 冲洗。
没有任何「可以走开、稍后回来」的节奏点。

### 现在的样子

```
【玩家】靠近 → 点「洗发」
        ↓  0s
第 1 段：短主动（ShampooDuration = 2s）
          Immediately 结算领域「打湿」动作，然后进入 2 秒主动打泡沫
          → 提示：泡沫已打好 · 可以先去照顾别人，回来冲洗
        ↓  2s
玩家被释放，可以离开
        ↓
后台： foam wait（BackgroundTask 自动启动）
        FoamOptimalStart   = 4s   → 进入可冲洗窗口，按钮变「冲洗」
        FoamMinorLate      = 7s   → 事故升到 Minor
        FoamModerateLate   = 12s  → 事故升到 Moderate
        ↓
【玩家】回来 → 点「冲洗」（FinishWashRinse）
        ↓
清泡沫 → 推进订单步数 → 洗头工位释放
```

### 复用了什么，新增了什么

**没有新增任何移动版专用 Wash 状态。** 三段节奏全部落在既有系统上：

- 第 1 段用 `_washServiceAdapter.BeginTimedAction / CompleteTimedAction`（领域「打湿」「打泡沫」动作）
- 后台用既有 `BackgroundTaskModel`（`Start(idealStart, idealEnd, dangerAt, now)`）
- 事故用既有 `UpdateServiceStage` 的 `FoamOptimalStart / FoamMinorLate / FoamModerateLate` 升级分支
- 迟到惩罚在领域层复用既有 `ServiceConfig.OverdueRinseDuration`（默认输入为 2.75s）；正式移动路径的
  `FinishWashRinse` 是即时收尾 API，不会把玩家实际锁住 2.75s，因此「迟到造成可感知的额外操作时间」目前尚未实现，标记为【需要试玩验证】
- 视觉用既有 `SalonDemo.UpdateCustomerViews` 的泡沫等待进度环（`WashStage.FoamWait` / `ReadyToRinse`
  都是 `WashStage.Foamy` 的枚举别名，因此旧 View 分支天然命中，无需改 UI）

真正被「激活」的其实是一段**早就写好但从没人调用过的死分支**：
`SalonGameModel.UpdateServiceStage` 里的泡沫等待逻辑原本从来不会被触发，因为没有代码启动对应的
`BackgroundTask`。本轮只是把缺失的那一句补上。

`SalonGameModel` 新增 5 个对外 API：`BeginWashFoamHold`、`FinishWashRinse`、`IsWashFoamWaitRunning`、
`IsWashFoamReadyToRinse`、`HaircutHoldDurationFor`。View 只能读，不能自己推导。

---

## 4. Cut 现在是什么节奏？

Cut 是**唯一持续占用玩家**的前台任务，规则**一行没改**。

```
【玩家】靠近 → 点「剪发」
        ↓
BeginHaircutAction  → BeginActiveOperation → PlayerBusy = true
        ↓
1.55 秒（HaircutConfig.PerfectMin + .05）
        ↓
CompleteHaircutAction + EndActiveOperation → PlayerBusy = false
```

被占用期间，**世界的其余部分继续跑**：等候顾客继续掉耐心、洗头泡沫继续计时、吹发后台继续走。
这一点由测试 `HaircutHoldsThePlayerButTheRestOfTheSalonKeepsRunning` 锁定。

本轮对 Cut 的唯一改动是**时长由模型说的算**：
移动路径原本自己算 `HaircutSettings.GetPerfectMin(tool) + .05f`，现在统一读 `_game.HaircutHoldDurationFor(...)`。
这样桌面和移动不可能因为某一边改了计算公式而 diverge。

保留并且没有改动的既有剪发规则：正确剪发、progress、overcut、bad haircut、repair、服务结果判定。

---

## 5. Dry 现在是什么节奏？

Dry 是**后台任务**，完全复用既有 Auto Blow。

```
【玩家】靠近 → 点「开始吹」
        ↓
StartAutoBlow → AutoBlowRunning = true，BackgroundTask 启动
        ↓
玩家立刻可以离开
        ↓
后台计时（移动 profile：ManualBlowGoodStart=12s / ManualBlowGoodEnd=20s / AutoBlowSafetyStopTime=30s）
        ↓  IdealStart
进入理想完成窗口 → 交互按钮变「完成吹发」
        ↓
【玩家】回来收尾 → FinishAutoBlow → BlowResult.Good / Minor / Moderate
```

`AutoBlowPurchased`（`SalonGameModel.HasAutoBlowStand`）继续作为 Demo 的兼容购买状态保留，
**但没有复制这套 bool 架构去创建第二个设备系统**。

---

## 6. 如何产生多任务压力？

压力完全来自**既有 `CustomerTrafficDirector`**，本轮**没有新建第二套导演系统，也没有改任何 Director 参数**。

Director 的四段压力相位（移动 profile）：

| 相位 | 起始进度 | 强度 |
| --- | --- | --- |
| OpeningLight | 0 | 0.62 |
| Normal | 0.15 | 1.00 |
| Busy | 0.46 | 1.25 |
| FinalPeak | 0.76 | 1.45 |
| Rush 叠加 | 0.70 — 0.85 | × 1.35 |

过载保护：当 `Waiting >= 3`、或工位饱和且等候 >= 2、或愤怒顾客 >= 2 时，
`IsOverloaded = true`，生成间隔 × `OverloadSlowdownMultiplier`（2.1）。
硬上限：`MaxConcurrentCustomers = 6`、`WaitingCapacity = 4`。

### 实测：3 分钟基础营业到底出现了什么并发

为了让「忙乱」可以被验收而不是靠嘴说，本轮新增了一个**无头营业日模拟**
（`Assets/Tests/Phase2BusyDaySimulationTests.cs`）：真实 `BusinessDayController` +
`CustomerTrafficDirector` + `SalonGameModel`，由一个「称职玩家」策略驱动跑完整天。

**重要前提**：模拟里的玩家是瞬间移动的（模型层没有玩家位置），所以这是**吞吐上界**。
真实游玩需要跑动，并发只会更高、完成数只会更少。

Day 1 实测并发曲线（`t` = 营业秒数，`active` = 同时在场顾客，`pending` = 此刻真正等着玩家动手的事情数）：

```
t=   4s active=0 pending=0 done=1 phase=OpeningLight | Finished/Cut
t=  12s active=1 pending=1 done=1 phase=OpeningLight | Entering/Wash
t=  16s active=1 pending=0 done=1 phase=OpeningLight | Serving/Wash(foam)   ← 第 1 个后台泡沫
t=  24s active=2 pending=1 done=1 phase=Normal       | Dry(blow) + Entering/Cut
t=  48s active=3 pending=1 done=3 phase=Normal       | Dry(blow) + Moving/Dry + Wash
t=  76s active=4 pending=1 done=7 phase=Busy         | Dry(blow)x2 + Wash(foam) + Entering/Wash
t= 100s active=5 pending=4 done=11 phase=FinalPeak   | Dry(blow) + Cut + Dry + Wash + Entering/Cut
t= 112s active=6 pending=4 done=12 phase=FinalPeak   | Cut x2 + Dry(blow) x2 + Waiting/Wash x2   ← 峰制
t= 120s active=5 pending=4 done=14 phase=FinalPeak   | Cut + Dry(blow) + Wash(foam) + Waiting x2
t= 132s active=2 pending=1 done=17                   | 收尾
[summary] spawned=19 completed=17 abandoned=0 unserved=0 incomplete=0 drops=17 collected=17
```

**结论**：

- 并发自然爬升 **2 → 3 → 4 → 5 → 6**，分别出现在约 t=24s / 48s / 76s / 100s / 112s；
- 峰值同时有 **4 件不同的事**等着玩家；
- **t=100s 那一帧就是本轮想要的优先级选择**：一个正在剪（占住玩家）、一个吹发后台、一个洗头待开始、
  一个新客进场、一个等候——玩家必须决定「先剪完、还是先去停吹風、还是先处理洗头」；
- Day 1 目标 3 单在称职玩家手里稳过（实测 17 单），目标没有过紧；
- 玩家完全不作为时 Director 会把客流压下来（`TheDirectorEasesOffInsteadOfPunishingAnOverloadedPlayer`）。

### 当前最多推荐同时多少顾客？

**推荐峰值 4，硬上限 6。**

理由：超过 4 以后两个洗头工位 + 两个剪发工位已经全部占满，新增顾客只能堆在等候区，
压力从「选择困难」退化成「纯粹排队」。6 是 `MaxConcurrentCustomers` 的天花板，只在 FinalPeak 短暂触及。
是否要把天花板压到 5，需要一次真机手感试玩才能定——见第 10 节的【需要产品决定】。

---

## 7. 事故：本轮只留一个

**泡沫迟到**，复用既有 `ApplyAccidentSeverity` 升级链路，**没有新增任何事故系统**。

事故不是单纯扣分，它有真实的后续成本：

| 迟到程度 | 阈值 | 事故 | 后果 |
| --- | --- | --- | --- |
| 理想窗口 | 4s | 无 | 冲洗 2.0s |
| Minor | 7s | `AccidentSeverity.Minor` | 满意度下降，冲洗仍 2.0s |
| Moderate | 12s | `AccidentSeverity.Moderate` | 满意度进一步下降；领域冲洗动作选择 `OverdueRinseDuration = 2.75s`，但正式移动 API 当前即时收尾，玩家是否能感知额外操作时间【需要试玩验证】 |

既有其他事故（overcut、blow timeout）保持不变，没有动。

---

## 8. 复用了哪些旧系统 / 哪些停止继续扩展

### 直接复用的旧系统

| 旧系统 | 怎么用的 |
| --- | --- |
| `SalonGameModel` | 唯一入口；新增 5 个 API，未重写 |
| `BusinessDaySystem.CustomerTrafficDirector` | 唯一客流导演，参数一行没改 |
| `BusinessDayController` | 营业时间 / ClosingGrace / DayStats |
| `SalonMobileDayConfig` | 移动 profile 全部 pacing 参数 |
| `Stage3WashServiceAdapter` | 洗头与后台等待的全部领域动作（打湿 / 打泡沫 / 冲洗 / 吹干） |
| `ActionResolver` / `ActionResultApplier` | **完全没动**，一行没改 |
| `ServiceProgress` / `CustomerPhysicalState` / `ServiceExecution` | 原样作为真相源 |
| `CustomerTrafficDirector.Evaluate` 的过载保护 | 原样 |
| `BackgroundTaskModel` + `UpdateServiceStage` 的泡沫升级分支 | 从死分支激活 |
| `SalonPaymentModel` PaymentDrop | 服务完成 → 掉钱 → 玩家跑去收 |
| `SalonProgressSave` | 存档路径未改，不破坏现有存档 |
| 已有 WebGL + Chromium 验收流程 | 原样复用 |

### 明确停止继续扩展的旧实现

| 旧实现 | 状态 |
| --- | --- |
| 桌面详细的分步洗头 UI（`SalonDemo.cs` 的 Shower / Shampoo / Rinse 分步按钮） | 保留作调试路径，**不再作为正式产品形态发展** |
| `BeginServiceExecution(Wash)` 的移动调用 | 已被 `BeginWashFoamHold` 取代；桌面仍用它，属【输入差异】 |
| `WashStage.FoamWait` / `ReadyToRinse` 作为独立状态名的假象 | 它们其实是 `Foamy` 的别名；STATE_OWNERSHIP 已澄清，别再当两套状态用 |
| `SalonDemo.cs` 里由 View 手算的剪发按住时长 | 已改为读模型；View 不再自己算业务时长 |
| `CustomerModel.State` / `.Step` / `WorkstationModel.State` 的多头并存 | 不合并，只在 STATE_OWNERSHIP 里定义单一真相源，留到下一阶段 |

### 属于兼容层、下一阶段应该删的

- `SalonMobileDayConfig.CreateServiceConfig()` 里给 Auto Blow 复用的 `ManualBlow*` 字段命名：
  名字说的是「手动」，实际喂给「自动」，是本文件最容易被误读的一处。
- `SalonGameModel.SetFirstDayCompleteForDebug` / `AutoBlowPurchased` 这类 bool 开关：本轮兼容保留，
  但**不要复制这种形态去创建第二个设备**。

---

## 9. 本轮真正改动的文件

commit 清单（`5dde4ea` → HEAD）：

```
41e5365  docs(phase-1): add state ownership definitions and tech debt register
90718bd  feat(phase-2): give the wash a leave-and-return background beat
2bcdad7  test(phase-2): lock the busy salon flow with 9 new EditMode tests
84cbe0d  test(phase-2): measure the real traffic curve with a headless business-day simulation
HEAD     docs(phase-2): add PHASE2_PLAYABLE_REPORT and PRODUCT_OWNER_SUMMARY
```

文件级改动量（`git diff --stat 5dde4ea..HEAD`）：

```
unity-hair-salon/Assets/Scripts/SalonGameModel.cs        +124 / -5
unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs       +53 / -12
unity-hair-salon/Assets/Scripts/SalonDemo.cs              +3 / -2
unity-hair-salon/Assets/Tests/Phase2BusySalonFlowTests.cs           (新增 310)
unity-hair-salon/Assets/Tests/Phase2BusyDaySimulationTests.cs       (新增 419)
unity-hair-salon/Assets/Tests/Phase6FinalAcceptanceTests.cs        (改 1 条)
unity-hair-salon/Assets/Tests/ShampooInteractionRedesignTests.cs   (改 1 条)
.gitignore                                                (新增)
docs/development/phase-0-2/*.md                           (新增 6 份)
```

**没有被改动的头文件**：`ActionResolver.cs`、`ActionResultApplier.cs`、`Stage3WashServiceAdapter.cs`、
`CustomerTrafficDirector.cs`、`SalonPaymentModel.cs`、`SalonProgressSave.cs`、`BusinessDayController`。

### 有意修改的两条既有测试（不是掩盖失败）

这两条原本锁定的是**旧语义**——「洗发完成立刻可以直接冲洗」，而本轮的设计意图正是「洗发后必须经历后台等待」：

| 原测试 | 改为 | 为什么 |
| --- | --- | --- |
| `ShampooInteractionRedesignTests.CompletedRubbingProducesFoamImmediatelyWithoutBackgroundWait` | `CompletedRubbingProducesFoamAndStartsBackgroundWashWait` | 锁定新的「打完泡沫进入后台等待」语义 |
| `Phase6FinalAcceptanceTests.CompletedRubbingIsImmediatelyReadyForAStandardRinse` | `UnattendedFoamEscalatesIntoAnAccidentAndSlowsTheRinse` | 改为锁定「放着不管会升级事故、且冲洗被延长」 |

### 新增测试（13 条）

`Phase2BusySalonFlowTests.cs`（9 条）：

| 测试 | 锁定什么 |
| --- | --- |
| `CustomerLifecycleRunsFromEntryToExit` | 进场 → 等候 → 分配 → 到站 → 服务 → 完成 → 离场 → 移除 |
| `WashKeepsRunningAfterTheActiveBeatAndNeedsThePlayerToReturn` | 洗头第一段结束后不占玩家，泡沫继续走，回来冲洗才推进订单 |
| `WashCannotStartWhileThePlayerIsBusyWithAnotherCustomer` | 一个玩家同时只能持有一个前台操作 |
| `AutoBlowKeepsRunningWhileThePlayerWalksAway` | 吹发后台在玩家离开后继续，且不锁玩家 |
| `HaircutHoldsThePlayerButTheRestOfTheSalonKeepsRunning` | 剪发占住玩家期间，洗头后台与等候耐心继续变化 |
| `FourCustomersCanDemandAttentionAtTheSameTime` | 剪 / 泡沫 / 吹 / 等候四人同时施压；剪发期间洗头进入可冲洗窗口 |
| `ACompletedOrderCreatesExactlyOnePaymentThatCreditsOnce` | 一单只生成一次付款，重复收取只入账一次 |
| `ForceCloseDoesNotDuplicatePaymentsOrDayStatistics` | 强制收尾不重复付款/统计，二次调用也不重复计数 |
| `ResetForNextDayClearsFoamWaitState` | 跨天重置清掉泡沫等待状态 |

`Phase2BusyDaySimulationTests.cs`（4 条）：

| 测试 | 锁定什么 |
| --- | --- |
| `MobileDayOneNaturallyClimbsFromTwoToThreeAndPeaksAtFour` | 并发真的按 2 → 3 → 4 顺序爬升，且 3 人以上能持续一段时间 |
| `ACompetentPlayerStillHasSeveralThingsPendingAtThePeak` | 最忙时至少 3 件不同的事在等玩家 |
| `DayOneTargetIsAchievableWithoutPerfectPlay` | Day 1 目标可达，且每单都能收到钱 |
| `TheDirectorEasesOffInsteadOfPunishingAnOverloadedPlayer` | 玩家不作为时 Director 放慢客流而不是继续堆人 |

前一轮的 13 条测试只依赖 `SalonGameModel` 与 `ServiceArchitecture`，不依赖 View；本轮另加
`WashCannotFinishRinseBeforeFoamIsReady` 锁定模型 readiness guard。它们锁定的是共享领域规则，
不代表移动与桌面拥有完全相同的 gameplay sequence：正式移动是
`BeginWashFoamHold → BackgroundTask → FinishWashRinse`，Desktop / QA 普通入口仍可能走
`BeginServiceExecution(Wash)`，详见本轮最终验收报告的「Phase 1 残余语义分叉」。

---

## 10. 测试结果（Before / After）

| 验收项 | Before (`5dde4ea`) | After |
| --- | --- | --- |
| Node 资产管线测试 | 17 / 17 通过 | 17 / 17 通过 |
| Unity EditMode | 595 / 595 通过 | **609 / 609 通过**（本轮 readiness guard 后复跑） |
| Manifest / 资产校验 | 13 assets 通过 | 13 assets 通过 |
| WebGL Demo 构建 | 成功 | 成功 |
| Chromium Demo smoke | 通过（含调试模式） | 通过（含调试模式） |
| Chromium 候选场景 smoke | 通过 | 通过（未回归） |

前一阶段新增 13 条、修改 2 条（见第 9 节，属有意行为变更）；本轮再新增
`WashCannotFinishRinseBeforeFoamIsReady` 1 条。最终 **609 / 609 通过**，没有任何原本通过的测试变成失败。

### 关于 Phase 0 时见到的 3 个 Node 失败

那 3 个失败在改变环境后消失，根因是**环境问题不是代码问题**：
沙箱的 PATH 前置了一个没有 Pillow 的托管 Python，导致 `tools/asset-pipeline.mjs` 调用的图像处理步骤失败。
用系统 PATH（`/usr/bin/python3` 带 Pillow）运行时 17/17 通过。已在 `PHASE0_BASELINE.md` 记录。

---

## 11. Gate A 是否有证据支持

**有，但不是「感觉」，是可复跑的数字证据。**

| Gate A 要求 | 证据 |
| --- | --- |
| 现有成果没被破坏 | EditMode 609/609、Node 17/17、WebGL 构建 + Demo smoke 全绿；`ActionResolver`/`ActionResultApplier`/`Stage3WashServiceAdapter` 一行未改 |
| 共享规则得到保护 | 移动与桌面共享模型推进与裁定规则，剪发时长由模型给出；但 Wash 的输入序列仍是移动 `BeginWashFoamHold → BackgroundTask → FinishWashRinse`、Desktop/QA 可能走 `BeginServiceExecution(Wash)`，不能宣称两条路径完全统一 |
| 洗剪吹形成优先级压力 | 模拟实测 t=100s 出现「正在剪 + 吹发后台 + 洗头待办 + 新客进场 + 1 人等候」，pending=4 |
| 3 分钟内出现忙乱 | 2 人出现在 ~24s，3 人 ~48s，4 人 ~76s；均在 120 秒营业内自然发生 |
| 下一阶段不用推翻底层 | 本轮没有重写任何有重测试覆盖的核心服务；STATE_OWNERSHIP + TECH_DEBT_NEXT 已标定下一阶段该动哪里 |

### 还没验证的部分（诚实说明）

- **没有真机 / 真手游玩数据**。全部证据来自无头模拟 + 模型层测试。
  模拟假设玩家瞬间移动，所以真实手感只会更忙。
- **付费、存档、多天连续流程**没有重跑长时序测试；本轮没碰 `SalonProgressSave`。
- **视觉**没有做任何新的美术验证，Demo smoke 只验证加载、核心流程和调试模式可见。

---

## 12. 哪些设计假设还没有验证

| 假设 | 为什么还没验证 | 建议怎么验证 |
| --- | --- | --- |
| 「玩家可以离开」真的是一个爽点而不是纯等待 | 需要真人手感 | 真机试玩时记录玩家在泡沫等待期间是否真的去做别的事 |
| 12—20 秒的吹发后台窗口是否太长 | 移动 profile 给得很宽（`ManualBlowGoodStart=12s`） | 试玩时注意玩家有没有在吹发区空转 |
| 峰值 6 人是「差一点忙不过来」还是「崩溃」 | 模拟里玩家是超人 | 真机统计第 1 天的放弃率与平均耐心 |
| Day 1 目标 3 单是否过松 | 称职玩家实测完成 17 单 | 【需要产品决定】要不要上调到 4—5 单 |
| 洗头只有 2 个工位是否成为吞吐瓶颈 | 模拟里出现过工位饱和 | 看 t≈104—120s 附近的排队情况 |

---

## 13. 下一阶段最值得做什么

按性价比排序：

1. **一次真机手感试玩**（最重要，成本最低，信息量最大）——把 Day 1 打三遍，记录「忙不过来」出现的时刻。
2. 根据试玩结果微调 Director / 服务时长参数（只动 `SalonMobileDayConfig` 里的数字，不动架构）。
3. 把「桌面详细洗头 UI」明确降格为调试面板，避免它繼續和正式移动路径长成两套。
4. 处理 `TECH_DEBT_NEXT.md` 的 **D1**（`CustomerModel.State` / `.Step` 双状态并存）与
   **D2**（耐心 / 满意度双向同步时间窗）——这两个是下一阶段做染发 / 烫发之前最值得清的债。
5. 之后才轮到染发、烫发、Event 系统与 Day 1—30 内容。

---

## 14. 【需要产品决定】

| # | 问题 | 我的建议 | 为什么需要你定 |
| --- | --- | --- | --- |
| 1 | Day 1 目标订单数现在是 3，称职玩家实测能完成 17 单。要不要上调？ | Day 1 保持 3（教学），Day 2—3 依次 4 / 5 已经配好 | 目标值直接决定「新手第一天的挫败感」，不是技术能定的 |
| 2 | 峰值并发要不要从 6 压到 5？ | 先别压，等一次真机试玩 | 模拟里的玩家是超人，压不压取决于真人是否真的崩溃 |
| 3 | 移动营业时长现在是 120 秒 + 15 秒收尾。你提到「玩 3 分钟」——要不要延长到 150 秒？ | 建议延长到 150 秒 | 会同时提高总客流和目标难度，属于产品节奏决策 |
| 4 | 桌面详细洗头 UI 是否正式确认为「仅调试用」？ | 确认为调试用 | 一旦确认，我下一轮就可以放心不维护它的业务一致性 |
