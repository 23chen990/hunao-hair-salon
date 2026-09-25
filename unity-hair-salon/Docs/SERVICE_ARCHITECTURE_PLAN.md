# 顾客服务流程受控重构 — 架构计划

> 范围：仅重构顾客服务状态、动作判定、错误结果、补救、UI 读取与服务结算。  
> 不改：店铺布局、排队 FIFO、世界时钟、金币拾取、寻路、商店、烫染、联机、发布。

---

## 1. 审计结论：旧逻辑混乱的真实根因

### 1.1 单一概念被多处同时占用

旧系统用 `CustomerModel.Step` + `WashStage` + `HaircutServiceModel.CurrentStepIndex` + UI 按钮 `interactable` 共同回答本应拆开的六个问题：

| 问题 | 旧答案来源（混用） |
|------|-------------------|
| 顾客想要什么 | `Needs` / `Step` |
| 顾客现在物理上怎样 | `HairWet` / `ShampooApplied` / `TowelWrapped` **且** `WashStage` |
| 下一步该做什么 | `WashStage` 枚举顺序 **且** 教程高亮 **且** 按钮灰显 |
| 工具能不能点 | `BuildToolBar` 里按“当前步骤 / 毛巾 / 泡沫”禁用 |
| 这次操作算什么 | 各按钮分支分别 `CompleteCurrentStep` / `ApplyExtraService` / 直接 return |
| 最终算不算完成 | `IsComplete` + `HasBlockingPhysicalState` + Overcut→`Failed` |

根因不是“少写几个 if”，而是 **门禁（能不能做）与判定（做得对不对）绑在同一条链路上**。为避免状态错误，系统选择禁用按钮，于是“胡闹感”被设计抹掉。

### 1.2 已确认的具体问题点

| 问题 | 代码位置（现状） | 表现 |
|------|------------------|------|
| 流程错误 → 按钮禁用 | `SalonDemo.BuildToolBar`：戴毛巾时剪刀等 `interactable=false`；包毛巾仅 `Rinsed` 可点 | 玩家无法犯错 |
| 错误操作被当正确或被直接吞掉 | `BeginWashAction` / `ResolveWashToolSelection` 大量 `return false` / `ExtraService` 且不改物理态 | 干发上洗发水等无法留下结块 |
| 物理态双轨 | `HairWet`+`ShampooApplied`+`TowelWrapped` 与 `WashStage` 并行 | UI 与逻辑偶发不一致 |
| 工具选择三份拷贝 | `HaircutInteraction` + `PlayerContext` + `SalonDemo._selectedToolIndex` | 换顾客/换工位串台风险 |
| 剪太短 = 未完成 | `ApplyHaircutResult` Overcut → `ServiceResult=Failed`，UI 文案“未完成，不结算” | 日终口径错误 |
| 教程虽未硬锁，但 UI 仍在锁 | `ShampooTutorial` 只高亮；真正锁工具的是工具栏 | 教学与营业同一套禁用逻辑 |
| 长期步骤文案 | `_focusLabel` / Toast 长期提示“下一步 / 毛巾尚未拆除” | 系统替玩家给答案 |
| `PrepareCurrentStep` 重置洗头阶段 | 切步骤时 `WashStage=Dry` | 物理态可能被订单步骤冲掉（风险点） |

### 1.3 重复数据源清单

1. 选中工具：`HaircutInteraction` / `PlayerContext` / `_selectedToolIndex`
2. 湿润：`HairWet` ↔ `WashStage`
3. 泡沫：`ShampooApplied` ↔ `WashStage`
4. 毛巾：`TowelWrapped` ↔ `WashStage.Toweled`
5. 订单进度：`Step` ↔ UI `SelectedServiceStep` 快照 ↔ 剪发 `HaircutServiceModel.CurrentStepIndex`
6. 完成语义：`CustomerServiceResult` / `HaircutServiceRating` / `HaircutServiceState` / `WorkstationState.Completed`

---

## 2. 目标架构：六层职责与原子提交边界

```text
ActionRequest
+ OrderDefinition
+ PhysicalStateSnapshot
+ ServiceProgressSnapshot
+ InteractionContextSnapshot
        │
        ▼（纯计算，不修改 CustomerModel / CustomerPhysicalState）
ActionResolver.Resolve(...)
        │
        ▼
ActionResult（完整状态差量、判定、事件与日志数据）
        │
        ▼（单一原子提交边界）
ApplyActionResult(...)
  ├─ 提交 CustomerPhysicalState
  ├─ 通过 CustomerMetrics.ApplyDelta 提交 Satisfaction / Patience
  ├─ 提交 Incident 与 HistoricalEvent
  ├─ Revision++
  └─ 重新计算 ServiceProgress（MilestoneKind + EverCompleted + SatisfiedNow / NeedsRedo）
        │
        ├─ SettlementGate / ServiceExitReadiness
        ├─ 成功后尽力追加非权威 ActionHistory
        └─ UI / Tutorial / Toast（只读）
```

**硬规则：**

- 工具可不可用 → **物理 + 工位能力**
- 操作对不对 → **ActionResolver**
- `ActionResolver` 必须是无副作用的规则计算器；不得先修改物理状态再判断。
- `ApplyActionResult` 是一次动作业务后果的唯一写入口；提交失败时不得留下半套状态或半条历史。
- `ActionHistory` 不属于业务原子事务：业务状态成功提交后再追加；日志失败只报错，不回滚业务状态。
- `currentStep` / `WashStage` 若暂时保留 → **只能由新状态派生，禁止多处写入**

---

## 3. 新旧对照

| 维度 | 旧 | 新 |
|------|----|----|
| 订单目标 | `Needs` + `Step` 兼做门禁 | `OrderDefinition`（RequiredServices / Milestones / CutTools） |
| 物理状态 | bool + `WashStage` 双轨 | `CustomerPhysicalState`（不复制工位真相；毛巾拆为包裹/污染/损坏维度） |
| 进度 | `Step++` 由各按钮触发 | `ServiceProgress` 同时记录 `EverCompleted` 与 `SatisfiedNow` / `NeedsRedo` |
| 交互 | 三处 selectedTool | 单一 `InteractionContext` + Version/ActionToken + 按原因清理 |
| 动作判定 | 分散在 Demo/Model | 快照输入 → 纯 `ActionResolver` → `ActionResult` → 原子 Apply |
| 错误 | 禁用 / Toast / 瞬间扣分恢复 | 持续状态 + 补救路径 + 耗时 |
| 剪太短 | `Failed` / 未完成 | `Completed + SatisfactionBand.Unhappy + IncidentSeverity.Major + Waived` |
| 教程 | 与禁用耦合 | 仅高亮/手指；错误仍走 Resolver |
| UI | 长期步骤提示 + 灰按钮 | 固定工具栏 + 短暂 Toast |

---

## 4. 新增类型（建议文件）

| 文件 | 职责 |
|------|------|
| `CustomerPhysicalState.cs` | 物理状态结构与同步辅助 |
| `OrderDefinition.cs` / `ServiceProgress.cs` | 订单目标与里程碑判定 |
| `ActionRequest.cs` / `ActionResult.cs` | 动作请求、不可变快照引用与统一动作结果 |
| `ActionResolver.cs` | 无副作用动作规则计算 |
| `ActionResultApplier.cs` | `ApplyActionResult` 原子提交与提交后进度重算 |
| `SettlementGate.cs` | RequiredMilestonesSatisfied + NoBlockingPhysicalState |
| `ServiceOutcomeModel.cs` | OrderStatus / SatisfactionBand / IncidentSeverity / PaymentOutcome / LeaveReason |
| `ServiceDebugOverlay.cs` | 开发模式状态面板 + ActionHistory |
| `ServiceInvariants.cs` | 开发模式断言 |
| `Docs/ACTION_RULE_MATRIX.md` | 规则矩阵（已同步） |
| `Docs/SERVICE_ARCHITECTURE_PLAN.md` | 本文件 |

`SalonGameModel` / `SalonDemo` / `PlayerContext` / `BusinessDaySystem` 做受控接入，不推倒重写场景与寻路。

---

## 5. CustomerPhysicalState 字段（第一版）

| 字段 | 类型 / 范围 | 说明 |
|------|-------------|------|
| `Wetness` | 0～1 | 湿润程度 |
| `FoamAmount` | 0～1 | 泡沫量；冲净后为 0 |
| `ShampooState` | None / Normal / ClumpedOnDryHair | 洗发水状态 |
| `IsTowelWrapped` | bool | 是否包着毛巾 |
| `TowelContamination` | None / Foam / Shampoo | 污染维度，可与损坏并存 |
| `TowelCondition` | Intact / Damaged | 完好维度，可与污染并存 |
| `TowelWetness` | 0～1（可选） | 毛巾湿润程度 |
| `HaircutProgressByTool` | `Dictionary<SalonTool,float>` 或定长结构 | 按剪刀、分齿剪、推子分别记录，不设单一总进度 |
| `HairLengthDeviation` | 连续偏差值或枚举 | **唯一可写的发长真相** |
| `OvercutSeverity` | None / Minor / Major（派生） | 只由 `HairLengthDeviation` 派生，禁止直接写入 |
| `PhysicalStateRevision` | 单调递增整数 | 阶段 2 强制并发版本；每次成功业务提交后 `Revision++` |
| `IsInUninterruptibleAction` | bool | 极短不可打断动画 |

工位只以 `CustomerModel.Station` 为唯一可写真相；`CustomerPhysicalState` **不再保存** `CurrentStation`。所有 Resolver 输入通过 `ActionRequest.StationId` 与快照读取工位，提交时重新核验。

`Wetness` 是唯一可写的干湿物理真相。吹风直接降低 `Wetness`；UI 吹发进度从 `Wetness` 与当前 Action 进度派生；`HairDry` 由 `Wetness <= HairDryThreshold` 判定。不得再保存可写 `BlowDryProgress`。

**兼容策略（迁移期）：**

- 只有 `ApplyActionResult` 可写 `PhysicalState`
- 派生写回旧字段：`HairWet = Wetness > 0.5`，`ShampooApplied = FoamAmount > 0.05 || ShampooState != None`，`TowelWrapped = IsTowelWrapped`
- `WashStage` 由物理态 **只读派生**，禁止业务按钮直接赋值（开发模式不一致则报警）

---

## 6. ServiceProgress 与订单目标判定

### OrderDefinition（从 `Needs` + 剪发配置生成）

- `RequiredServices`: Wash / Cut / Dry …
- `RequiredMilestones`: 如 WetHair, Shampoo, Rinse, CleanTowel, Untowel, Scissors, ThinningShears, BlowDry
- `RequiredCutTools`: 剪刀 / 分齿剪等
- `MilestoneDependencies`: 例如 CleanTowel 依赖 Rinse；正常剪发里程碑不要求“从未犯错”，但要求未包毛巾且对应工具 Perfect/Over

### ServiceProgress 的历史与当前满足状态

每个里程碑至少拆为：

- `EverCompleted`：历史上是否曾正确达到，用于教程、历史与结果分析，不因返工清除。
- `SatisfiedNow`：当前物理状态是否仍满足，用于下一步建议与结算。
- `NeedsRedo`：可由 `EverCompleted && !SatisfiedNow` 派生，或显式保存返工原因。
- `MilestoneKind`（或等价 `SatisfactionMode`）：决定结算读取历史还是当前状态。

| MilestoneKind | 结算依据 | 第一版里程碑 |
|---------------|----------|--------------|
| `HistoricalEvent` | `EverCompleted` | `WetHairApplied`、`Shampooed`、`CleanTowelApplied`、`CutToolCompleted(tool)` |
| `RevalidatableState` | `SatisfiedNow` | `RinseClean`、`HairDry`、`Untoweled` |
| `ExitConstraint` | `ServiceExitReadiness` | `NoFoam`、`NoClumpedShampoo`、`NoTowel`、`NotMoving`、`NoActiveAction` |

提交动作后，必须基于**提交后的权威物理状态 + 本次已提交业务事件**重新计算 `SatisfiedNow`，不得依赖 ActionHistory 作为业务输入。示例：

- RinseClean 曾满足后重新上洗发水：`RinseClean.SatisfiedNow=false`，`NeedsRedo=true`；历史洗发事件不删除。
- HairDry 曾满足后重新打湿：`HairDry.EverCompleted=true` 可保留，`HairDry.SatisfiedNow=false`。
- Untowel 曾完成后重新包毛巾：`Untowel.EverCompleted=true`，`Untowel.SatisfiedNow=false`。

`WetHairApplied` 是历史事件：吹干后不会失效。`CleanTowelApplied` 也是历史事件：拆毛巾后不会失效。避免正确流程因最终头发已干或毛巾已拆而无法结算。

### 里程碑完成条件（示例）

| Milestone | 完成条件（正确路径） | 错误操作能否完成 |
|-----------|----------------------|------------------|
| WetHairApplied (HistoricalEvent) | 曾用花洒正确达到湿发阈值 | 吹干后仍看 EverCompleted |
| Shampooed (HistoricalEvent) | 湿发下正确揉洗达到要求 | 后续冲洗不使历史失效 |
| RinseClean (RevalidatableState) | `FoamAmount==0 && ShampooState==None` 且最后一次洗发后正确冲洗 | 重新上洗发水后 SatisfiedNow=false |
| CleanTowelApplied (HistoricalEvent) | 冲洗后曾包上未污染完整毛巾 | 拆毛巾后仍看 EverCompleted；仅跨工位后续服务时必需 |
| Untoweled (RevalidatableState) | `!IsTowelWrapped`（订单要求时） | 重新包毛巾后 SatisfiedNow=false |
| CutToolCompleted(X) (HistoricalEvent) | 正确工具 + Perfect 或 Over；Over 同时记 `IncidentSeverity.Major` | 错误工具 / UNDER → 否 |
| HairDry (RevalidatableState) | `Wetness <= HairDryThreshold` | 重新打湿后 SatisfiedNow=false |

`nextSuggestedMilestone` 仅供教程软引导，**绝不**用于 `Button.interactable`。

`CustomerModel.Step` 迁移期由当前有序里程碑视图派生，不能只数 `EverCompleted`；发生 NeedsRedo 时必须反映当前未满足状态，但不删除历史。

---

## 7. ActionResolver 核心流程

```text
ActionRequest + OrderDefinition + PhysicalStateSnapshot
+ ServiceProgressSnapshot + InteractionContextSnapshot
  1. Snapshot 与 ActionResult 必须携带 ExpectedRevision
  2. Resolver 校验物理可行性与 ActionToken
     - 无目标 / 已离店已结算 / 工位无此工具 / 工位占用冲突 / 不可打断动画
     - 拆毛巾但 IsTowelWrapped=false
     - CustomerId / StationId / ContextVersion 与 BeginHold token 不一致
     → executed=false, 直接返回 Invalid（允许 UI 弱化该按钮）
  3. 仅在快照上计算 PhysicalStateChanges（不修改真实对象）
  4. 对照 OrderDefinition + ServiceProgressSnapshot 判定
     - orderEffect: None / Progress / ExtraService
     - mistakeSeverity: None / Minor / Recoverable / Major
     - executionQuality: N/A / Under / Perfect / Over
  5. 生成带 source/reason 的 metrics delta、事故、recoveryTags、elapsedTimeForLog
  6. 返回含 ExpectedRevision 的完整 ActionResult + uiEvent
  7. ApplyActionResult 提交前验证 ExpectedRevision == PhysicalStateRevision
     - 不一致返回 StaleState；业务状态与历史均不提交
     - 提交物理差量、数值与事故
     - 通过 CustomerMetrics.ApplyDelta 提交数值 delta
     - 根据提交后状态重算 MilestoneKind / EverCompleted / SatisfiedNow / NeedsRedo
     - 运行 SettlementGate 与开发不变量
     - 成功后 PhysicalStateRevision++
     - 最后尽力追加一条非权威 ActionHistory；失败仅记录错误
```

**禁止：** 花洒/洗发水/剪刀按钮各自 `CompleteCurrentStep`。  
**禁止：** Resolver 内直接引用并修改 CustomerModel、CustomerPhysicalState 或 ActionHistory。  
**允许：** UI 提交 `ActionRequest`；业务层负责 Resolve + Apply。

`ActionResult.timeCost`（如保留该命名）仅用于日志/统计，**不得再次推进世界时间**。Hold 已由世界时钟自然经过；QuickAction 的短动画也不得额外扣一遍营业时间。

`InteractionContextVersion` 只防止旧交互写入错误目标；`PhysicalStateRevision` 只防止旧物理快照覆盖新状态。两者都必须验证，不能互相替代。

满意度与耐心仍存放在 `CustomerModel`，但禁止模块直接赋值或加减。Action、WaitingTick、WrongStation 等统一提交：

```text
CustomerMetrics.ApplyDelta(customer, satisfactionDelta, patienceDelta, source, reason)
```

所有变化必须带 `source/reason`，便于审计与测试。

QuickAction（包/拆毛巾）：

- 单击立即 Resolve + Apply
- **不得**写入 `selectedTool`
- 执行后必须 `ClearInteractionContext(QuickAction)`

---

## 8. InteractionContext 清理契约

统一方法：`ClearInteractionContext(ClearReason reason)`

| 事件 | 清理内容 |
|------|----------|
| 切换顾客 | selectedTool / hold / hint |
| 顾客开始移动 | 同上；**保留物理与进度** |
| 到达新工位 | 同上 |
| 退出聚焦 / 空白总览 | 全部交互 |
| 动作完成且可继续同工具（UNDER、未冲净、未吹干） | 结束 hold，**保留 selectedTool** |
| 动作完成且工具失效 / 订单完全结算 | 清 hold + tool |
| 工具不属于新工位 | tool |
| QuickAction | tool + hold（强制空） |

`PlayerContext` 收敛为 InteractionContext 的宿主；`HaircutInteraction` 仅作 hold 计时器，选中工具以 Context 为准。

### ActionToken / InteractionContextVersion

- InteractionContext 每次切换顾客、移动、换工位、退出聚焦、QuickAction 或工具失效时递增 `Version`。
- `BeginHold` 生成 `ActionToken { CustomerId, StationId, ContextVersion, ActionId, StartedAt }`。
- `CompleteHold`、协程与动画回调必须重新核验 token；不匹配则禁止向新目标提交。
- 被移动/切换中断时，先按实际 elapsed 结算旧 token，再递增 Version 并清交互：
  - 花洒、冲洗、揉洗、吹发：提交已经发生的连续部分进度。
  - 剪发：按实际持续时间结算 UNDER / PERFECT / OVER；不可无损取消。

---

## 9. SettlementGate / ServiceExitReadiness 与结算口径

订单只在以下条件**同时**满足时允许结算：

```text
All HistoricalEvent milestones: EverCompleted
&& All RevalidatableState milestones: SatisfiedNow
&& ServiceExitReadiness
```

`ServiceExitReadiness` 至少检查：`NoFoam`、`NoClumpedShampoo`、`NoTowel`、`NotMoving`、`NoActiveAction`，以及 `Wetness <= ExitWetnessThreshold`。泡沫、结块、仍包毛巾、湿度高于离店阈值、正在移动或操作中会阻止结算。发型偏差、剪太短、已记录事故只影响结果维度，**不**使订单变成未完成。

| 概念 | 取值 | 说明 |
|------|------|------|
| OrderStatus | InProgress / Completed / Unfinished / Abandoned | 订单目标是否做完 |
| SatisfactionBand | Happy / Normal / Unhappy | 满意度展示维度 |
| IncidentSeverity | None / Minor / Major | 事故维度，独立于满意度 |
| PaymentOutcome | FullPayment / WithTip / Discounted / Waived | 第一版支付结果 |
| LeaveReason | ServiceFinished / LeftBeforeService / DayEnded / AngrilyLeft | 为何离店 |

**剪太短（Over）：**

```text
OrderStatus = Completed
SatisfactionBand = Unhappy
IncidentSeverity = Major
PaymentOutcome = Waived
LeaveReason = ServiceFinished
```

- 计入完成服务人数  
- 计入不满意  
- **不**计入未完成  

第一版所有 Major 事故统一 `PaymentOutcome=Waived`，暂不实现 `Compensation`，避免扩大金币、负收入与日终账务范围。

日终不变量：

```text
开心 + 一般 + 不满意 = 完成服务人数
今日接待 = 完成服务 + 未完成 + 提前离店
```

迁移：保留 `CustomerServiceResult` 枚举时，用映射层填充；Overcut 不再写 `Failed`。

---

## 10. UI / Tutorial / Toast 约束

### 工具栏

- 洗头工位：花洒 / 洗发水 / 毛巾槽 **固定位置**（仅物理不可能时禁用，如动画中）
- 洗头工位第三槽根据状态切换：未包毛巾显示“包毛巾”；已包毛巾显示“拆毛巾”。泡沫毛巾可当场拆除，不能要求先去剪发区。
- 剪发工位：拆毛巾 / 剪刀 / 分齿剪 / 推子 / 手动吹发 **固定位**；仅“无毛巾”时拆毛巾可 Invalid/弱化
- **禁止**因“下一步不该用剪刀”而灰掉剪刀

### 正常营业文案

底部只保留：工位名、固定工具栏、当前选中工具、必要长按进度。  
去掉长期“下一步请使用…”类提示。

需求气泡：只显示服务类别（洗头 / 剪发 / 烫染或组合），不展示内部工具顺序。

### Toast

`ShowToast(message, 1.0～1.5s)`；禁止长期钉在 `_focusLabel` 上的世界状态说明（如“顾客正在前往工位”可极短或改用非阻塞指示）。

### 教程

仅第一位洗头顾客软引导；完成后 `WashingTutorialCompleted=true`。  
错误工具仍可执行并由 Resolver 处理。

---

## 11. 洗头床姿态与视觉（本轮）

- 洗头床：顾客横向躺下，头朝盆/墙，身体沿床；作用点在头部，HitBox 可含上半身与床附近。
- 物理态必须有对应视觉（可占位）：Dry / Wet / ClumpedShampoo / Foamy / Rinsing / CleanTowel / FoamyTowel / DamagedTowel / FoamyAndDamagedTowel / Overcut。
- **禁止**逻辑有毛巾、画面无毛巾。

---

## 12. 调试与不变量（开发模式）

面板字段：CustomerId, Station, Wetness, Foam, ShampooState, IsTowelWrapped, TowelContamination, TowelCondition, RequiredMilestones, EverCompleted, SatisfiedNow, NeedsRedo, SelectedTool, ContextVersion, ActionToken, ActiveAction, LastActionResult, Satisfaction, Patience, OrderStatus, SatisfactionBand, IncidentSeverity, SettlementGate。

ActionHistory 是非权威诊断记录，不参与里程碑或结算计算。业务状态先完整提交；成功后再尝试追加。日志写入失败只 `LogError`，不得回滚已提交业务状态。

ActionHistory 行格式：

```text
Customer A | HaircutStation | Scissors
Pre: Towel=Wrapped/Clean/Intact
Result: Executed / NoProgress / Recoverable
Post: Towel=Wrapped/Clean/Damaged
Satisfaction: -5
```

不变量（违反则 LogWarning/Assert）：

1. `selectedToolCustomerId == focusedCustomerId`
2. `selectedToolStationId == focusedStationId`
3. UI 不直接改 PhysicalState
4. QuickAction 后 selectedTool 为空
5. 移动后 activeHoldAction 为空
6. 换工位后旧工位工具不可继续执行
7. `IncidentSeverity.Major` 不能自动推出 `OrderStatus.Unfinished`
8. 完成人数 = 开心+一般+不满意
9. 派生 `WashStage`/`Step` 与新状态不一致时报警
10. Resolver 计算期间真实状态对象不得发生变化
11. ApplyActionResult 必须验证 ExpectedRevision；成功后 Revision 恰好 +1
12. 未匹配到矩阵规则必须报告 `UnhandledActionRule`，禁止静默回退旧逻辑
13. 过期 ActionToken 不得提交到当前顾客或工位
14. `OvercutSeverity` 必须等于 `HairLengthDeviation` 的派生结果
15. Satisfaction/Patience 不得绕过 CustomerMetrics.ApplyDelta
16. ActionHistory 失败不得回滚已成功业务提交

---

## 13. 实施顺序（编码阶段，待确认后执行）

| 阶段 | 内容 | 完成标准 |
|------|------|----------|
| 1 | 审计 + 本文档 + ACTION_RULE_MATRIX | 文档就绪（本轮） |
| 2 | 新状态类型 + 纯 ActionResolver + ActionResultApplier + 双版本校验 + 历史/不变量骨架 | 单元测试验证 Resolve 无副作用、Apply 原子性与 StaleState |
| 3 | 迁移花洒/洗发水/包拆毛巾/移动清交互 | 进入条件：完全冲洗明确写成 FoamAmount=0、ShampooState=None、RinseClean 满足；CleanTowelApplied 仅在洗头后需跨工位服务时列为必需历史事件 |
| 4 | 迁移剪发/吹发 UNDER/PERFECT/OVER | Case 4–5, 8–12 |
| 5 | 结算拆分、Toast、Tutorial 软引导、日终、调试面板 | Case 15–16；旧双写删除 |

每阶段结束后跑现有 EditMode 测试，并补 `ServiceArchitectureTests`。

---

## 14. 明确不改清单

- 店铺布局、Low-Poly 方向、光照主色  
- FIFO 排队、世界实时时钟、金币落地拾取  
- 寻路（除非状态保留相关 bug）  
- 一屏到底与工位聚焦结构  
- 商店、烫染新玩法、多人联机、平台发布  

---

## 15. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 大量旧测试依赖 `WashStage`/`Failed` Overcut | 派生兼容层 + 分批改断言 |
| Demo 与 Model 耦合深 | Resolver 先接管写入；Demo 只改工具栏门禁与文案 |
| 视觉资源不足 | 占位色/形，状态枚举先对齐 |
| 一次改太大 | 严格按阶段 2→5，禁止同时维护两套长期写入源 |
| 组合规则漏判后落回旧逻辑 | Resolver 无匹配时报告 `UnhandledActionRule` 并拒绝静默旧路径 |
| ActionHistory 写入失败 | 仅影响诊断完整性；业务提交不回滚，记录日志错误 |

---

## 16. 本轮交付物

- [x] 审计根因（本文 §1）  
- [x] 新旧对照与六层职责（§2–§3）  
- [x] 字段 / Resolver / 结算 / UI 计划（§5–§11）  
- [x] `ACTION_RULE_MATRIX.md`  
- [ ] 编码与测试（**待你确认后再做**）

---

## 17. 本次反馈修订索引

| # | 反馈 | 修订位置 |
|---|------|----------|
| 1 | Resolver 纯计算、Apply 原子提交 | §2 数据流、§3、§4、§7 |
| 2 | 毛巾拆为包裹/污染/损坏维度 | §5、§6、§10、§11；规则矩阵 D6–D9、H |
| 3 | EverCompleted / SatisfiedNow / NeedsRedo | §6；规则矩阵 G、H |
| 4 | SettlementGate / ServiceExitReadiness | §4、§7、§9；规则矩阵 G |
| 5 | 洗头工位同槽包/拆毛巾 | §10；规则矩阵 B、D6、H-W4 |
| 6 | 按动作结果决定是否保留工具 | §8；规则矩阵 D16 |
| 7 | ActionToken / InteractionContextVersion | §7、§8、§12；规则矩阵 A、D16 |
| 8 | 中断时提交部分进度/剪发质量 | §8；规则矩阵 D15、H-W13/W14 |
| 9 | Station 唯一可写真相 | §5；规则矩阵 D15 |
| 10 | 剪发按工具记录；发长唯一真相 | §5、§12；规则矩阵 D10/D11 |
| 11 | SatisfactionBand + IncidentSeverity | §3、§4、§9；规则矩阵 D10、G |
| 12 | 第一版 Major 统一 Waived | §9；规则矩阵 D10、G |
| 13 | timeCost 不重复推进世界时间 | §7；规则矩阵开头执行契约 |
| 14 | 扩充组合矩阵 + UnhandledActionRule | §12、§15；规则矩阵 H、J |
