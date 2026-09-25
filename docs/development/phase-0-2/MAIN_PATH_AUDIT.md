# MAIN_PATH_AUDIT.md — 移动 / 桌面 / ServiceArchitecture 主路径差异审计

> 审计时间：2026-09-18（Asia/Shanghai）
> 审计对象：`SalonDemo.cs`、`SalonDemo.Mobile.cs`、`SalonGameModel.cs`、`Stage3WashServiceAdapter.cs`、`ServiceArchitecture/*`
> 结论先行：**本轮只做文档，不重构。** 但审计已经发现一条此前未被记录的关键业务分叉（移动洗头锁死玩家、桌面洗头不锁），以及多处状态同步风险。

标记说明：

- 【输入差异】允许长期存在，属于输入层。
- 【表现差异】允许存在，属于 UI / 视图层。
- 【业务规则差异】必须逐步收敛，本轮 Phase 2 主要处理这一类。
- 【状态同步风险】本轮只定义所有权，大规模修复进入 `TECH_DEBT_NEXT.md`。

---

## 0. 一句话结论

**正式可玩路径已经比交接文档描述的更统一**：桌面洗头工位和移动洗头，调用的都是同一个 `SalonGameModel.BeginServiceExecution(ServiceExecutionType.Wash)`。

真正的分叉不在「用不用 ServiceArchitecture」，而在于：

1. **同一个 API 在两条路径上的前台/后台语义不同**（移动锁死玩家，桌面不锁）；
2. **细粒度洗头步骤（`BeginWashAction` 系列）只存在于 QA / craft 入口，两条正式路径都没有走到**；
3. **移动剪发被固定成「永远完美」，移动吹发绕过了桌面 UI 的设备购买门槛**；
4. **订单内容与剪发工具要求在生成时就已经按路径分支**。

因此本轮 Phase 2 的正确方向不是「把桌面删掉」，而是：

```text
移动输入（靠近 + 按钮）  ─┐
                          ├─→ 统一 SalonGameModel 服务 API ─→ ServiceArchitecture
桌面输入（点击 + 工具栏）─┘        （Resolver / Applier / PhysicalState / Progress）
```

---

## 1. 移动版 Wash 的真实流程

代码位置：`SalonDemo.Mobile.cs` → `FindMobileTarget()`（L359-379）→ `ExecuteMobileTarget()`（L416-434）→ `TickMobileWork()`（L438-463）

```text
靠近洗头工位 1.45m 内且顾客已入座
  → 目标 = MobileAction.Wash，按钮文案「洗发」
  → ExecuteMobileTarget
      _game.SelectCustomer(customer)
      _game.BeginServiceExecution(customer, ServiceExecutionType.Wash)
      _mobileWorkingView = view          ← 设置本地工作锁
      _mobileWorkDuration = ServiceSettings.WashServiceDuration   // 移动配置 = 3f
  → UpdateMobilePlay 每帧：if (_mobileWorkingView != null) { TickMobileWork(dt); return; }
      ▲ 玩家不能移动、不能做其它交互
  → TickMobileWork 累计到 3 秒后 EndMobileWork()
```

模型侧：`SalonGameModel.BeginServiceExecution`（L452）→ `Stage3WashServiceAdapter.StartServiceExecution` → `ServiceExecutionController.Start(Wash, 3f)`。
`SalonGameModel.Tick` → `UpdateServiceExecution`（L615）→ 进度到 1 后 `CompleteServiceExecution` → `ActionResolver.ResolveServiceCompletion`（tool = Shower）→ 一次性写入 `WetHairApplied + Shampooed + RinseClean` → `CompleteCurrentStep(customer, true)`。

**关键事实：移动洗头是一次性 3 秒定时服务，中间没有任何可离开的节奏点，且玩家被完全锁死。**

---

## 2. 桌面版 Wash 的真实流程

代码位置：`SalonDemo.cs` → `BuildToolBar()`（L1296-1302）

```text
顾客在洗头工位（WorkstationType.Wash）
  → AddServiceExecutionButton(customer, ServiceExecutionType.Wash, "🫧", "洗头")
  → 点击 → _game.BeginServiceExecution(customer, ServiceExecutionType.Wash)
  → SalonDemo 不设置任何玩家锁
  → SalonGameModel.Tick 后台推进 ServiceExecution
  → 完成 → CompleteCurrentStep
```

**关键事实 1：桌面正式路径与移动路径调用的是同一个模型 API。**
**关键事实 2：桌面不锁玩家，`BeginServiceExecution` 之后玩家可以走开，洗头在模型 Tick 里继续推进。**

### 2.1 细分洗头步骤在哪里？

`BeginWashAction(WashAction.Shower / Shampoo / Wet / Rinse / WrapTowel)` 与 `PerformQuickAction(WrapTowel / RemoveTowel)` 的调用方只有：

| 调用方 | 性质 |
|---|---|
| `Stage3WashRuntimeAcceptance.cs` | QA 验收脚本 |
| `Stage4A5FreedomRuntimeAcceptance.cs` | QA 验收脚本 |
| `Stage4AHaircutRuntimeAcceptance.cs` | QA 验收脚本 |
| `AssetPipeline/WashCraftEvidence.cs` | 资产工艺证据脚本 |
| `AssetPipeline/CandidateAssetValidation.cs` | 候选资产校验脚本 |
| `SalonDemo.cs`（`washCraft=detail` / `washCraft=service` URL） | 工艺 / 调试入口 |
| 13 个 EditMode 测试文件 | 自动化测试 |

**结论：细分洗头（打湿 → 洗发 → 泡沫等待 → 冲洗 → 包/拆毛巾）是「QA + 工艺」路径，不是任何一条正式可玩路径。**
`TECH_HANDOFF.md` 中「桌面版有更细的洗头步骤」这句话，如果按「正式可玩路径」理解是不准确的。

---

## 3. Cut 两条路径是否一致？

**不一致，且是真实的业务规则差异。**

| 维度 | 移动 | 桌面 |
|---|---|---|
| 入口 | `ExecuteMobileTarget` → `_game.BeginHaircutAction(customer, tool, HaircutSettings)` | `SalonDemo.cs` L1886 → `_game.BeginHaircutAction(customer, selectedTool, HaircutSettings)` |
| 时长来源 | `SalonDemo.Mobile.cs` L428：`_mobileWorkDuration = HaircutSettings.GetPerfectMin(tool) + .05f` | `HaircutInteraction`（真实按住时长，玩家决定何时松手） |
| 结果 | `CompleteHaircutAction(customer, _mobileWorkDuration, false)` → 恒定落在 `[PerfectMin, PerfectMax]` 内 → **恒为 Perfect** | 玩家按住时长决定 `Under` / `Perfect` / `Over` |
| 玩家锁定 | `TickMobileWork` 期间完全不能移动 | 按住期间不能做其它动作 |
| 剪发工具要求 | `SpawnRuntimeCustomer` L1091：`_mobileMode` → `ConfigureHaircutOrder(customer, Scissors)`（永远只要剪刀） | L1092-1095：按 id 奇偶 → `Scissors+ThinningShears` 或 `Clippers` |
| 过剪 / 剪坏 | **移动路径不可能发生 overcut** | 可能发生 overcut、wrong tool、需要 repair |

**【业务规则差异】** 移动剪发被简化成「点一下 = 完美」。这让移动路径失去了剪发操作本身的风险维度，也让 overcut / repair / bad haircut 这些已有规则在正式路径上不可达。

---

## 4. Dry 两条路径是否一致？

**部分一致，但 UI 门槛不同。**

| 维度 | 移动 | 桌面 |
|---|---|---|
| 后台自动吹发 | `MobileAction.StartDry` → `StartAutoBlow`；`FinishDry` → `FinishAutoBlow` | `HandleBlowAction`（L1450）→ 同样 `StartAutoBlow` / `FinishAutoBlow` |
| 手动长按吹发 | **不提供** | `AddServiceAction("≋", "吹风机")` → `StartManualBlow` / `EndManualBlowHold` |
| 自动吹发可见性 | 无条件可用 | `SalonDemo.cs` L1322：`if (_game.HasAutoBlowStand)` 才显示「自动吹风」按钮 |
| 模型层门槛 | `SalonGameModel.AutoBlowAvailable => true`（永远为 true） | 同上，模型层无门槛，只有 UI 门槛 |
| 后台推进 | `SalonGameModel.UpdateServiceStage`（L2010-2036）+ `BackgroundTask` | 完全相同（同一份模型代码） |

**【业务规则差异】** `SalonGameModel.AutoBlowAvailable` 恒为 true，但桌面 UI 用 `HasAutoBlowStand` 额外加了一道门槛、移动 UI 不加。同一个业务能力在两个入口的可用条件不同。

---

## 5. 哪些业务规则只存在桌面入口？

| 规则 | 位置 | 说明 |
|---|---|---|
| 手动长按吹发 | `StartManualBlow` / `TickManualBlow` / `EndManualBlowHold` | 移动路径完全没有入口 |
| 细分洗头步骤 | `BeginWashAction` / `PerformQuickAction(WrapTowel/RemoveTowel)` | 只存在于 `washCraft=*` 与 QA 脚本 |
| 染发 / 烫发 | `BeginProcessingServiceApply` / `ResolveDyeCleanup` | 移动订单池 O001–O005 不含 Dye/Perm，正式路径不可达 |
| 错误工位 / 错误工具 | `ApplyWrongStationTool`、`WrongStationCount` | **历史审计快照**：移动入口当时只给「转移顾客」。2026-09-23 已恢复错误工位可执行并可纠正；错误工具仍未接入移动入口。 |
| 工具栏工具选择 | `PlayerContext.SelectServiceAction` | 移动无工具选择概念 |
| 订单加权抽取 | `DaySettings.PickOrder(Random.value)` | 移动改用 `SalonMobileDayConfig.PickOrderForSpawn` |
| 多工具剪发订单 | `ConfigureHaircutOrder(..., Scissors, ThinningShears)` | 移动恒定为单剪刀 |

> 本轮态度：以上规则**保留**，不删除；但不再为它们扩展新内容（染发/烫发本轮明确不做）。

## 6. 哪些业务规则只存在移动入口？

| 规则 | 位置 | 说明 |
|---|---|---|
| 引导顾客到下一工位 | `_mobileGuidedCustomer` + `MobileAction.Guide` | 桌面无对应交互，靠 `AwaitingTransfer` + 再次 Assign |
| 靠近自动收取金币 | `CollectNearbyMobilePayments()` | 桌面需点击金币堆 |
| 2D 简化表现 | `_simple2DMode = _mobileMode` | 桌面为 3D 视角 |
| 按 Day 分支的固定订单池 | `SalonMobileDayConfig.PickOrderForSpawn` | 桌面用加权随机 |
| 开店 / 重试 / 存档 | `RestoreMobileGame` / `RetryMobileDay` / `SaveMobileCheckpoint` | 桌面无存档路径 |
| 洗头期间锁死玩家 | `_mobileWorkingView` | **桌面没有这层锁**（差异核心） |

## 7. 哪些只是输入 / 表现差异（不需要收敛）？

- 【输入差异】摇杆 + 单按钮 vs 鼠标点击 + 工具栏。
- 【输入差异】靠近判定（`FlatDistance` ≤ 1.45/1.8）vs 直接点选。
- 【表现差异】移动 HUD（目标进度条、队列视图、goal 标签、取消接待按钮）vs 桌面工具栏（工具按钮、focus 标签、教程按钮）。
- 【表现差异】`_simple2DMode` 的正交 2D 相机 vs 桌面 3D 相机。
- 【表现差异】Toast 文案与进度条颜色。
- 【表现差异】移动 `MobileAction.Guide`「转移顾客」与桌面「再次 Assign」是同一业务动作的不同包装。

## 8. 真正的业务语义差异汇总（本轮要收敛的）

| # | 差异 | 严重度 | 本轮处理 |
|---|---|---|---|
| B1 | 同一 `BeginServiceExecution(Wash)`：移动锁死玩家 3 秒，桌面后台推进不锁 | 高 | **Phase 2 收敛**：移动洗头改为「短主动 → 后台等待 → 回来收尾」，两条路径都变成「可离开」 |
| B2 | 移动洗头一次性完成，无任何后台节奏点 | 高 | **Phase 2 收敛**：复用 `BackgroundTask` + `WashStage.Foamy` + `FoamOptimalStart/MinorLate/ModerateLate` |
| B3 | 移动剪发恒为 Perfect，overcut/repair 不可达 | 中 | **Phase 2 部分收敛**：保留「点一下」输入，但时长不再硬编码为 `PerfectMin+.05`，改为可由配置进入 overcut 区间；不新增 UI |
| B4 | 自动吹发：桌面 UI 有购买门槛、移动没有 | 中 | **Phase 2 收敛**：统一以模型 `AutoBlowAvailable` 为准，UI 门槛保留为桌面调试表现，不新增设备 bool |
| B5 | 订单生成与剪发工具要求按路径分支 | 中 | **本轮不收敛**（会影响大量已有测试），记入 `TECH_DEBT_NEXT.md` |
| B6 | 细分洗头只存在于 QA/craft 入口 | 中 | **Phase 2 部分收敛**：把「洗发 → 等待 → 冲洗」的最小子集接入正式移动路径，复用 `BeginWashAction` 的领域动作而非新增状态 |

## 9. 状态同步风险

| # | 风险 | 位置 | 说明 |
|---|---|---|---|
| S1 | `WashStage.FoamWait`、`ReadyToRinse` 与 `Foamy` 是**同一个枚举值** | `SalonServiceChain.cs` L20-27 | `UpdateServiceStage` L1989 的 `WashStage = WashStage.ReadyToRinse` 是一次**空赋值**；「泡沫还在等」与「可以冲洗了」在枚举上无法区分，所有 UI 都必须改用 `BackgroundTask.Elapsed >= FoamOptimalStart` 重新计算 |
| S2 | 泡沫等待分支**没有任何启动方** | 全仓库 | `UpdateServiceStage` L1983-2009 与 `PatienceDrainMultiplier` L2217-2218 都依赖 `WashStage == Foamy && BackgroundTask.State == Running`，但没有任何代码在设置 `Foamy` 的同时 `BackgroundTask.Start(...)`。当前是**死分支** |
| S3 | `ApplyShampoo` 直接写 `CustomerModel.ShampooApplied / WashStage`，绕过领域层 | `SalonGameModel.cs` L1147-1157 | 领域真相是 `CustomerPhysicalState`；这里出现第二个写入者 |
| S4 | 移动工作锁写在 View 层 | `SalonDemo.Mobile.cs` `_mobileWorkingView` | 「玩家正在忙」这件事模型另有 `PlayerBusy` / `AttentionState`；View 侧再存一份，任何一方清理不同步就会出现「模型说在忙、界面说空闲」 |
| S5 | 移动剪发时长由 `SalonDemo` 计算 | `SalonDemo.Mobile.cs` L428 | 服务时长应由模型/配置拥有；当前 View 参与了业务事实 |
| S6 | `_mobileGuidedCustomer` 与 `PlayerContext.SelectedCustomerId` 重复表达「当前接待的顾客」 | `SalonDemo.Mobile.cs` L17 | 同一事实两个持有者 |
| S7 | `CustomerModel.WashStage` 是 `CustomerPhysicalState` 的投影，但多处代码把它当真相读 | `SalonGameModel.cs` L1054、L200-203、L2217 | 投影被反向当成输入使用 |
| S8 | `CustomerModel.ServicePhysicalState` 注释说是「只读显示投影」，但 `ResolveRecoveryIfPossible` 又把它当真相 | `Stage3WashServiceAdapter.cs` L428 | 投影与真相混用 |

---

## 10. 本轮不做的收敛（明确记录）

- 不删除桌面路径、不删除 `washCraft=*` 入口、不删除 QA 验收脚本。
- 不重写 `ActionResolver` / `ActionResultApplier`。
- 不合并 `SalonMobileDayConfig.PickOrderForSpawn` 与 `DaySettings.PickOrder`（影响面过大）。
- 不新建「统一的 Gameplay Command 层」类体系；本轮只让两条路径调用**同一批模型方法**，不做新的抽象层。
- 不为自动吹发复制 `AutoBlowPurchased` 这种 bool 设备模式。
