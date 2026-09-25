# 阶段 1D 验收包：顾客服务流程与工具行为判定

> 依据：`SERVICE_ARCHITECTURE_PLAN.md`、`ACTION_RULE_MATRIX.md` 修订版。  
> 本文只确认设计、迁移边界与测试计划；不授权阶段 2 编码。

---

## 1. 14 项反馈逐项对照

| # | 原问题 | 修改后的设计 | 文档与章节 | 最终规则 |
|---|--------|--------------|------------|----------|
| 1 | Resolver 先修改 PhysicalState 再判断，可能留下半套状态 | 输入全部改为不可变请求/快照；Resolver 纯计算；`ApplyActionResult` 统一原子提交并重算进度 | 架构 §2、§3、§4、§7；矩阵开头执行契约 | `ActionResolver` 零业务写入；一次 ActionResult 要么完整提交，要么不提交 |
| 2 | `TowelState=None/Clean/Foamy/Damaged` 无法表达污染与损坏并存 | 拆成 `IsTowelWrapped`、`TowelContamination`、`TowelCondition`，可选 `TowelWetness` | 架构 §5、§6、§10、§11；矩阵 D6–D9、H | 包裹、污染、损坏是正交维度 |
| 3 | 永久 CompletedMilestones 无法因返工失效 | 每个里程碑区分 `EverCompleted`、`SatisfiedNow`、`NeedsRedo` | 架构 §6；矩阵 G、H | 历史保留，结算只看当前满足；返工使 `SatisfiedNow=false` |
| 4 | 缺少独立结算门，事故和阻挡状态混在“完成/失败”里 | 新增 `SettlementGate / ServiceExitReadiness` | 架构 §4、§7、§9；矩阵 G | 必须同时满足里程碑、无阻挡物理态、未移动、未操作 |
| 5 | 提前包泡沫毛巾后可能必须去剪发区才能拆 | 洗头工位第三槽固定，依据 `IsTowelWrapped` 在包/拆之间切换 | 架构 §10；矩阵 B、D6、H-W4 | 泡沫毛巾在洗头区即可立即拆除，不允许死锁 |
| 6 | 动作结束无条件清工具，妨碍继续修正 | 按结果和上下文决定是否保留工具 | 架构 §8；矩阵 D16 | UNDER、未冲净、未吹干保留工具；切人、移动、换站、退出、QuickAction、工具失效必清 |
| 7 | 旧协程/动画回调可能把 A 的动作写给 B | 增加 `ActionToken` 与 `InteractionContextVersion` | 架构 §7、§8、§12；矩阵 A、D16 | Begin 绑定顾客、工位、版本、动作；Complete 全量复核 |
| 8 | 移动或切换可无损取消已经发生的操作 | 中断前按实际 elapsed 解析并提交，再递增版本和清理交互 | 架构 §8；矩阵 D15、H-W13/W14 | 连续动作保留部分进度；剪发按实际时长判 UNDER/PERFECT/OVER |
| 9 | PhysicalState 再保存 CurrentStation 会形成第二工位真相 | 删除该字段设计，唯一工位真相继续为 `CustomerModel.Station` | 架构 §5；矩阵 D15 | PhysicalState 不可写工位副本 |
| 10 | 单一 HaircutProgress 不支持多工具；发长与 Overcut 双写 | 改为 `HaircutProgressByTool`；`HairLengthDeviation` 可写，`OvercutSeverity` 派生 | 架构 §5、§12；矩阵 D10/D11 | 每种剪发工具独立进度；过剪严重度不可直接写 |
| 11 | ServiceOutcome 把情绪与事故揉成一个枚举 | 拆为 `SatisfactionBand` 与 `IncidentSeverity` | 架构 §3、§4、§9；矩阵 D10、G | 满意度档位与事故严重度独立组合 |
| 12 | Compensation 扩大金币与负收入范围 | 第一版 Major 事故统一 `PaymentOutcome.Waived` | 架构 §9；矩阵 D10、G | 本轮不实现 Compensation |
| 13 | `timeCost` 可能再次扣世界时间 | 明确其仅用于日志/统计 | 架构 §7；矩阵开头执行契约 | Hold 时间已由世界时钟经过，Apply 不再推进时间 |
| 14 | 洗头/毛巾组合不全，未知组合可能偷偷落回旧逻辑 | 增加 W1–W14；未知组合显式 `UnhandledActionRule` | 架构 §12、§15；矩阵 H、J | 未匹配规则不得默认成功、推进订单或调用旧逻辑 |

---

## 2. 唯一可信状态源清单

“唯一来源”指只有一个可持久化、可写的存储位置。Snapshot、ActionResult、UI ViewModel 和旧兼容字段均不得成为第二个可写来源。

| 数据 | 唯一持有者 | 允许写入者/入口 | 只能读取或派生 |
|------|------------|-----------------|----------------|
| 顾客所在工位 | `CustomerModel.Station` | 顾客移动/分配领域入口 | PhysicalSnapshot、Resolver、UI、路径表现 |
| 湿润程度 | `CustomerPhysicalState.Wetness` | `ApplyActionResult` | 旧 `HairWet`、视觉、ServiceProgress |
| 泡沫量 | `CustomerPhysicalState.FoamAmount` | `ApplyActionResult` | 旧 `ShampooApplied` 部分派生、视觉、ServiceProgress |
| 洗发水状态 | `CustomerPhysicalState.ShampooState` | `ApplyActionResult` | 旧 WashStage、视觉、ExitReadiness |
| 毛巾是否包裹 | `CustomerPhysicalState.IsTowelWrapped` | `ApplyActionResult` | 旧 `TowelWrapped`、按钮标签、视觉 |
| 毛巾污染状态 | `CustomerPhysicalState.TowelContamination` | `ApplyActionResult` | 视觉、Resolver Snapshot、历史 |
| 毛巾损坏状态 | `CustomerPhysicalState.TowelCondition` | `ApplyActionResult` | 视觉、事故与历史 |
| 各剪发工具进度 | `CustomerPhysicalState.HaircutProgressByTool` | `ApplyActionResult` | Haircut UI、ServiceProgress |
| 头发长度偏差 | `CustomerPhysicalState.HairLengthDeviation` | `ApplyActionResult` | `OvercutSeverity`、视觉、结算结果 |
| 订单里程碑 | `ServiceProgress` | `ApplyActionResult` 提交 HistoricalEvent，并在提交后统一重算 RevalidatableState | `Step`、建议步骤、UI、SettlementGate |
| 当前选中工具 | `InteractionContext.SelectedTool` | InteractionContext 的选择/清理 API | HaircutInteraction、SalonDemo UI |
| 顾客满意度 | `CustomerModel.Satisfaction` | 仅 `CustomerMetrics.ApplyDelta`；Action/WaitingTick/WrongStation 只提交带 source/reason 的 delta | UI、SatisfactionBand |
| 顾客耐心 | `CustomerModel.Patience` | 仅 `CustomerMetrics.ApplyDelta`；Action/WaitingTick/WrongStation 只提交带 source/reason 的 delta | UI、情绪表现 |
| 订单状态 | `CustomerModel.OrderStatus`（目标结构） | 结算/日终领域入口，依据 Progress 与 ExitReadiness | UI、日终统计、旧结果映射 |
| 最终情绪档位 | `CustomerModel.SatisfactionBand`（目标结构） | 结算领域入口 | UI、日终统计 |
| 事故严重度 | `CustomerModel.IncidentSeverity`（目标结构） | `ApplyActionResult` 只允许单调升级；结算只读 | UI、支付与统计 |
| 支付结果 | `CustomerModel.PaymentOutcome`（目标结构） | 结算领域入口 | 金币生成、UI、日终统计 |

约束：

1. ActionResult 只携带差量，不是持久状态源。
2. Snapshot 是不可变副本，不可回写。
3. UI、Tutorial、Toast 只能读取。
4. 兼容字段只能从新真相派生，禁止反向写入。
5. `OvercutSeverity` 是 `HairLengthDeviation` 的函数，不在表中另列可写入口。

---

## 3. 旧字段迁移清单

| 旧字段/状态 | 迁移定位 | 停止直接写入 | 删除旧引用 | 最终状态 |
|-------------|----------|--------------|--------------|----------|
| `CustomerModel.Step` | 迁移期由 ServiceProgress 当前有序视图派生 | 阶段 3 先停止洗头推进；阶段 4 停止全部工具直接 `Step++` | 阶段 5 替换业务判断；如 UI 兼容需要可保留只读属性 | 只读派生，最终不作为真相 |
| `WashStage` | 由 Wetness/Foam/Shampoo/Towel/ActiveAction 派生表现阶段 | 阶段 3 | 阶段 5 删除业务分支写入；可保留只读兼容枚举至旧测试迁完 | 仅迁移期兼容，最终废弃可写字段 |
| `HairWet` | 由 `Wetness` 阈值派生 | 阶段 3 | 阶段 5 删除业务引用 | 仅迁移期兼容，最终废弃 |
| `ShampooApplied` | 由 FoamAmount/ShampooState 派生 | 阶段 3 | 阶段 5 删除业务引用 | 仅迁移期兼容，最终废弃 |
| `TowelWrapped` | 由 `IsTowelWrapped` 派生 | 阶段 3 | 阶段 5 删除业务引用 | 仅迁移期兼容，最终废弃 |
| `HaircutServiceModel.CurrentStepIndex` | 映射到 `HaircutProgressByTool` + CutTool milestones | 阶段 4 | 阶段 5 | 仅迁移期兼容，最终废弃 |
| `HaircutInteraction._selectedTool` / `SelectedTool` | 选择真相迁到 InteractionContext；HaircutInteraction 只保留 hold 计时 | 阶段 4 | 阶段 4 完成接入后删除选中工具存储 | 最终废弃其工具状态 |
| `PlayerContext.SelectedTool` 及 owner/station 快照 | PlayerContext 演进为 InteractionContext 或仅代理它 | 阶段 3 建立单向代理；阶段 4 禁止独立写 | 阶段 5 删除重复 owner/step/washstage 快照 | 保留为唯一 InteractionContext 时可保留；不得与新对象双写 |
| `SalonDemo._selectedToolIndex` | 由 InteractionContext.SelectedTool 派生按钮高亮 | 阶段 3（洗头）/阶段 4（剪发） | 阶段 4 | 最终废弃 |
| `SalonDemo._selectedServiceAction` | 由 InteractionContext.ActiveAction 派生 | 阶段 3 | 阶段 4/5 清理剩余 UI 引用 | 最终废弃 |
| `CustomerServiceResult` | 由 OrderStatus + SatisfactionBand + IncidentSeverity 映射 | 阶段 5 | 阶段 5 迁移 UI/日终/支付后删除业务引用 | 仅迁移期兼容，最终废弃 |
| `CustomerModel.HairStage` | 由 HairLengthDeviation 与 Cut milestones 派生视觉阶段 | 阶段 4 | 阶段 5 删除业务判断，视觉可改只读属性 | 只读派生 |
| `HaircutServiceRating/State` | 由里程碑历史、NeedsRedo、IncidentSeverity 派生 | 阶段 4 | 阶段 5 | 仅迁移期兼容，最终废弃 |

阶段 2 只建立基础模型与纯规则测试，不接正式场景，因此上述旧字段在阶段 2 仍按旧路径运行；不得提前建立双向同步。

---

## 4. 新数据流确认

最终数据流固定为：

```text
ActionRequest
+ OrderDefinition
+ PhysicalStateSnapshot
+ ServiceProgressSnapshot
+ InteractionContextSnapshot
→ ActionResolver 纯计算 ActionResult
→ ApplyActionResult 原子提交
→ 重新计算 ServiceProgress 与 ServiceExitReadiness
→ UI / Tutorial / Toast 只读渲染
```

确认：

- `ActionResolver` 不持有真实 CustomerModel 引用，不直接修改物理状态、满意度、耐心、事故、历史或里程碑。
- `ActionResult` 包含完整差量和判定，不自行提交。
- `ApplyActionResult` 强制同时验证 ContextVersion 与 ExpectedRevision；版本冲突返回 StaleState。
- 业务物理/数值/事故/里程碑属于原子提交；成功后 PhysicalStateRevision++。
- ActionHistory 是非权威诊断记录，在业务提交成功后尽力追加；日志失败只报错，不回滚业务状态。
- 当前两份文档已是该结构，无需再次修订。

---

## 5. 毛巾状态组合确认

毛巾不使用 Clean/Foamy/Damaged 单一互斥枚举，最少使用：

```text
IsTowelWrapped: bool
TowelContamination: None | Foam | Shampoo
TowelCondition: Intact | Damaged
TowelWetness: 0..1（可选）
```

要求的组合可以表示为：

```text
IsTowelWrapped = true
TowelContamination = Foam
TowelCondition = Damaged
```

拆毛巾后，顾客头上的 FoamAmount/ShampooState 不会被毛巾字段重置；毛巾污染与损坏作为 ActionHistory 归档。

---

## 6. 里程碑种类与可失效机制

| 维度 | 含义 | 是否可回退 |
|------|------|------------|
| `EverCompleted` | 历史上曾正确达成 | 否 |
| `SatisfiedNow` | 提交后当前物理态仍满足 | 是 |
| `NeedsRedo` | `EverCompleted && !SatisfiedNow`，可附失效原因 | 随补救消除 |

| MilestoneKind | 结算读取 | 第一版成员 |
|---------------|----------|------------|
| HistoricalEvent | EverCompleted | WetHairApplied、Shampooed、CleanTowelApplied、CutToolCompleted |
| RevalidatableState | SatisfiedNow | RinseClean、HairDry、Untoweled |
| ExitConstraint | ServiceExitReadiness | NoFoam、NoClumpedShampoo、NoTowel、NotMoving、NoActiveAction |

三条强制规则：

1. 冲洗后重新上洗发水：`RinseClean.SatisfiedNow=false`，`NeedsRedo=true`；再次完全冲净后恢复。
2. 吹干后重新打湿：`HairDry.SatisfiedNow=false`，`NeedsRedo=true`；`WetHairApplied.EverCompleted` 不失效。
3. 拆毛巾后重新包毛巾：`Untoweled.SatisfiedNow=false`，`NeedsRedo=true`；`CleanTowelApplied.EverCompleted` 不失效。

结算要求所有 HistoricalEvent 的 EverCompleted、所有 RevalidatableState 的 SatisfiedNow，并通过 ServiceExitReadiness。`CleanTowelApplied` 仅在洗头后还有跨工位服务时才是必需 HistoricalEvent。

---

## 7. ServiceExitReadiness / SettlementGate

结算门：

```text
All HistoricalEvent milestones: EverCompleted
&& All RevalidatableState milestones: SatisfiedNow
&& ServiceExitReadiness
```

| 状态/错误 | 阻止结算 | 处理 |
|-----------|----------|------|
| 仍有泡沫 | 是 | 冲净后重新判断 |
| 干发洗发水结块 | 是 | 打湿并重新揉洗/冲洗 |
| 仍包着毛巾 | 是 | 拆毛巾 |
| 正在移动 | 是 | 到站后重新判断 |
| 正在执行动作 | 是 | 动作提交完成后重新判断 |
| Wetness 高于离店阈值 | 是 | 继续吹干至阈值以下 |
| 剪得太短 | 否 | Completed + Unhappy + Major + Waived |
| 发型轻微偏差 | 否 | 影响满意度/IncidentSeverity/支付档位，不改未完成口径 |
| 已发生重大事故 | 否 | 若必需里程碑当前满足，订单仍可 Completed；第一版支付 Waived |

“不阻止结算”不代表没有后果，只表示其不把已完成订单改成 Unfinished。

---

## 8. 洗头工位毛巾按钮规则

洗头工位第三槽位置固定：

| 状态 | 槽位动作 |
|------|----------|
| `IsTowelWrapped=false` | 包毛巾 |
| `IsTowelWrapped=true` | 拆毛巾 |

提前包泡沫毛巾后，槽位立即切换为拆毛巾。玩家可在洗头区执行：

```text
拆泡沫毛巾 → 冲洗残留泡沫 → 重新包干净毛巾
```

不得要求先移动到剪发区，不得因错误流程造成补救死锁。

---

## 9. 操作中断规则

### 连续洗头/吹发动作

花洒打湿、冲洗、揉洗、吹发都按实际 elapsed 生成部分差量：

- Wetness 按已执行时间增加。
- FoamAmount 在揉洗时增加、冲洗时减少。
- 吹风按已执行时间直接降低 Wetness；不保存可写 BlowDryProgress。
- 未达到阈值时不完成对应 milestone，但保留已发生物理进度。
- 工具仍属于当前顾客/工位且未发生切换时，UNDER/未冲净/未吹干可继续使用。

### 剪发动作

剪刀、分齿剪、推子在移动或切换顾客前，先依据旧 token 的真实 Hold 时长结算：

- `< PerfectMin` → UNDER
- `PerfectMin..PerfectMax` → PERFECT
- `> PerfectMax` → OVER

结算写入对应 `HaircutProgressByTool` 与 `HairLengthDeviation`，然后才允许移动/切换清理 Context。

### 防止无损取消事故

中断顺序必须是：

```text
冻结旧 ActionToken 的 elapsed
→ ResolveInterruptedAction
→ ApplyActionResult
→ ContextVersion++
→ 清理交互
→ 执行移动或焦点切换
```

因此达到 OVER 阈值的动作会先落地事故，玩家不能通过移动顾客、点击 B 顾客或退出聚焦来撤销。

---

## 10. ActionToken / ContextVersion

BeginAction 记录：

```text
ActionId
CustomerId
StationId
ContextVersion
ActionType / Tool
StartedAtWorldTime
PhysicalStateRevision（阶段 2 强制）
```

CompleteAction / InterruptAction 重新验证：

- token 的 CustomerId 等于原操作目标。
- token 的 StationId 仍对应原操作发生工位。
- token 的 ContextVersion 与提交上下文一致。
- ActionId 仍是当前活动动作且尚未提交。
- 顾客未离店/未结算。
- `PhysicalStateRevision` 为阶段 2 强制字段：Snapshot 记录 ExpectedRevision，ActionResult 原样携带，Apply 前必须匹配；不匹配返回 StaleState。
- 每次成功业务提交后 `PhysicalStateRevision++`。
- ContextVersion 管交互归属，PhysicalStateRevision 管快照新旧，两者不可替代。

安全规则：

1. 旧协程或动画回调找不到活动 ActionId：幂等丢弃，不写历史、不弹业务 Toast。
2. ContextVersion 不匹配：视为 stale callback，开发模式记录诊断后丢弃。
3. A 的 token 永远携带 A.CustomerId；即使 UI 已聚焦 B，也只能结算 A 的中断动作，不能把差量应用给 B。
4. 每个 ActionId 最多成功 Apply 一次，重复回调必须幂等。

---

## 11. 剪发状态唯一来源

确认：

- 不存在单一可写 `HaircutProgress`。
- 使用 `HaircutProgressByTool[Scissors|ThinningShears|Clippers]` 分别记录。
- `HairLengthDeviation` 是唯一可写的发长/发型偏差真相。
- `OvercutSeverity = Derive(HairLengthDeviation)`，只能派生，不能直接赋值。
- HairStage、Overcut 视觉、事故判断、支付结果读取上述真相，不得反向写回。

---

## 12. 结算维度确认

最终拆分：

| 维度 | 作用 |
|------|------|
| `OrderStatus` | InProgress / Completed / Unfinished / Abandoned |
| `SatisfactionBand` | Happy / Normal / Unhappy |
| `IncidentSeverity` | None / Minor / Major |
| `PaymentOutcome` | FullPayment / WithTip / Discounted / Waived |
| `LeaveReason` | ServiceFinished / LeftBeforeService / DayEnded / AngrilyLeft |

第一版重大事故统一：

```text
OrderStatus = Completed（若订单目标满足且通过 ExitReadiness）
SatisfactionBand = Unhappy
IncidentSeverity = Major
PaymentOutcome = Waived
LeaveReason = ServiceFinished
```

本轮不实现 `Compensation`。

---

## 13. timeCost 确认

- Hold 动作经过的真实时间已由世界时钟消耗。
- `ActionResult.timeCost` 或 `elapsedTimeForLog` 只用于 ActionHistory、分析和日终统计。
- `ApplyActionResult` 不调用世界时钟推进，不再次扣营业时间。
- QuickAction 的短动画可以占用不可打断表现时段，但不能把同一时段重复从营业时间扣除。

---

## 14. ACTION_RULE_MATRIX 尚未明确的组合

以下是当前矩阵仍缺少确定结果或精确阈值的组合。它们在规则补齐前一律返回 `UnhandledActionRule`，不得静默走旧逻辑：

### 洗头/毛巾

1. 部分湿润（0 < Wetness < 正确阈值）时第一次使用洗发水。
2. 已涂普通洗发水但尚未形成足量泡沫时再次挤洗发水。
3. 正常 Shampoo 状态、低泡沫时改用花洒，是否视为有效冲洗或过早冲洗。
4. 无泡沫但仍 `ShampooState=Normal` 残留时包毛巾。
5. 湿发但从未完成 Shampoo 时包毛巾的精确污染、严重度与满意度。
6. 完全干发、无洗头订单时包毛巾的精确 orderEffect。
7. ClumpedOnDryHair 状态直接包毛巾时污染类型及拆除后结块状态。
8. 已损坏但未污染毛巾被花洒冲水。
9. 已损坏毛巾上再次使用剪刀/分齿剪/推子时的升级阈值。
10. 已包毛巾期间连续重复吹风，何时从松动升级为 Damaged/Major。
11. TowelWetness 达到各阈值后的视觉与事故影响。
12. 毛巾已包裹时自动吹风（若设备入口可达）。
13. 正在包/拆毛巾短动画中再次点击毛巾槽。
14. 顾客移动中点击洗头工具或 QuickAction。

### 剪发

15. 正确剪发里程碑已满足后重复使用同一正确工具的具体偏差增量及 Major 阈值。
16. 错误剪发工具 UNDER/PERFECT/OVER 与“工具错误”两个维度的组合结果。
17. 同一错误工具重复次数如何映射 Minor→Major。
18. 泡沫状态剪发时，各工具对 HaircutProgress 与 HairLengthDeviation 的具体倍率。
19. ClumpedOnDryHair 状态下剪发的物理和事故结果。
20. 湿发但无泡沫时剪发是否有质量倍率/额外事故。
21. 已 Overcut 后继续使用任一剪发工具。
22. 不需要剪发的顾客分别使用剪刀、分齿剪、推子的差异。
23. 多工具订单中先完成后置工具、再做前置工具的里程碑规则。
24. 多工具分别产生 UNDER 后，累计发长偏差如何影响后续 PERFECT 窗口。

### 吹发

25. ClumpedOnDryHair 状态吹发。
26. 部分泡沫与不同 FoamAmount 下吹发的进度/飞溅倍率。
27. 湿度不足但尚未完全干时重复吹发的 ExtraService 边界。
28. BlowDry 已满足后持续吹发，Minor→Major 的具体过热阈值。
29. 不需要吹发的顾客使用手动吹风。
30. 自动吹风与手动吹发同时请求或切换。
31. 自动吹风运行中移动顾客时如何结算 elapsed 与设备状态。

### 并发、生命周期与提交冲突

32. 两名玩家同时对同一顾客发起不同工具动作的仲裁。
33. 同一帧 QuickAction 与 Hold Complete 同时到达的提交顺序。
34. Resolve 后、Apply 前顾客被移动造成 Station/Revision 变化。
35. Resolve 后、Apply 前订单被另一动作结算。
36. 营业结束发生在活动 Hold 中间时，是先结算中断动作还是先 ForceClose。
37. 顾客耐心归零与动作完成同帧发生时的确定顺序。
38. 场景卸载、对象销毁或应用暂停导致 Hold 中断。
39. ActionHistory 写入失败：业务状态保持已提交，仅记录诊断错误；不回滚。

开发模式统一行为：

```text
executed = false
diagnostic = UnhandledActionRule
不修改 PhysicalState
不推进 ServiceProgress
不改变 Satisfaction / Patience / Incident
不调用旧 BeginWashAction / ApplyHaircutResult
```

这些缺口需在进入对应正式迁移阶段前补齐；阶段 2 只验证“未知规则不会静默成功”。

---

## 15. 阶段 2 EditMode 测试计划（不编码）

阶段 2 仅建立：

`CustomerPhysicalState`（含强制 PhysicalStateRevision）、`ServiceProgress`（含 MilestoneKind）、`InteractionContext`、`ActionRequest`、`ActionResult`、`ActionResolver`、`ApplyActionResult`、`ActionToken/ContextVersion`、`ServiceExitReadiness`、`ActionHistory`、`ServiceInvariants`。

不改按钮、不接场景、不迁移正式工具入口。

### A. Resolver 纯函数与原子提交

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `Resolve_DoesNotMutateInputSnapshots` | 干发快照 + Shower request | 返回 Wetness 差量；所有输入对象值不变 |
| `Resolve_DoesNotAppendActionHistory` | 任意合法 request + 空历史 | Resolver 后历史仍空 |
| `Apply_CommitsBusinessFieldsAtomically` | 含物理、metrics delta、事故、里程碑事件的 ActionResult | 业务字段一起更新，Revision++；随后尽力追加历史 |
| `Apply_RejectedToken_CommitsNothing` | 过期 token + 合法 ActionResult | 状态、数值、里程碑、历史全部不变 |
| `Apply_StaleExpectedRevision_CommitsNothing` | ExpectedRevision 落后当前 Revision | 返回 StaleState；业务与历史不变 |
| `Apply_Success_IncrementsRevisionExactlyOnce` | ExpectedRevision 匹配 | 成功提交后 Revision 恰好 +1 |
| `Apply_RecalculatesProgressAfterPhysicalCommit` | Rinse 已满足 + 添加洗发水结果 | 先提交泡沫，再得到 Rinse SatisfiedNow=false |
| `Apply_DoesNotAdvanceWorldTime` | elapsedTimeForLog=2s | 世界剩余时间不由 Apply 改变 |
| `Apply_DuplicateActionId_IsIdempotent` | 同一 ActionResult Apply 两次 | 第二次丢弃，状态/历史不重复 |
| `ActionHistoryFailure_DoesNotRollbackBusinessState` | 注入日志写入失败 | 业务状态与 Revision 保持成功，仅记录错误 |

### B. CustomerPhysicalState 与毛巾组合

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `TowelState_CanBeWrappedFoamyAndDamaged` | Wrapped=true, Foam, Damaged | 三维组合可同时存在并正确快照 |
| `RemoveFoamyDamagedTowel_PreservesHairFoam` | 毛巾 Foam+Damaged，头发 FoamAmount=.6 | 毛巾解除；头发 FoamAmount 仍 .6 |
| `PhysicalState_DoesNotOwnStation` | 构造 PhysicalState | 类型中无可写 CurrentStation |
| `HaircutProgress_IsTrackedPerTool` | 剪刀 .6、分齿剪 .2 | 两个进度独立 |
| `OvercutSeverity_IsDerivedFromHairLengthDeviation` | 不同 deviation 边界值 | 得到对应 severity，且无独立 setter |

### C. ServiceProgress 可失效

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `Rinse_ReapplyingShampoo_MarksNeedsRedo` | Rinse Ever/Satisfied=true；重新上洗发水 | Ever=true, Satisfied=false, NeedsRedo=true |
| `Rinse_Rerinsing_RestoresSatisfiedNow` | 上述 NeedsRedo + 冲净结果 | Ever=true, Satisfied=true, NeedsRedo=false |
| `WetHairApplied_DoesNotInvalidateAfterDrying` | WetHairApplied 历史完成 + Wetness 降到干燥 | EverCompleted 仍为 true |
| `CleanTowelApplied_DoesNotInvalidateAfterUntowel` | CleanTowelApplied 历史完成 + 拆毛巾 | EverCompleted 仍为 true |
| `HairDry_Rewetting_MarksNeedsRedo` | HairDry 已满足 + Shower | HairDry 当前失效 |
| `Untoweled_Rewrapping_MarksNeedsRedo` | Untoweled 已满足 + Wrap | Untoweled 当前失效，CleanTowelApplied 历史保留 |
| `Progress_DoesNotDoubleCompleteMilestone` | 同一正确动作重复提交 | EverCompleted 不重复计数 |

### D. InteractionContext 与 Token

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `BeginAction_CapturesCustomerStationAndVersion` | A、站点 0、Version 3 | token 精确记录目标与版本 |
| `SwitchCustomer_IncrementsVersionAndClearsTool` | A 选剪刀后聚焦 B | Version++，工具/hold/hint 清空 |
| `QuickAction_IncrementsVersionAndClearsTool` | 已选择花洒后拆毛巾 | QuickAction 后 selectedTool=null |
| `Under_Result_PreservesValidSelectedTool` | A、剪刀、UNDER、仍在原站 | hold 结束，剪刀保留 |
| `PartialRinse_Result_PreservesValidSelectedTool` | 花洒、未冲净 | 花洒保留 |
| `MoveCustomer_InvalidatesOldToken` | Begin 后移动 | 旧 Complete 不可向新站提交 |
| `StaleCallback_CannotWriteCustomerB` | A token；当前聚焦 B | B 状态完全不变 |
| `DuplicateCallback_IsSafelyDiscarded` | 已提交 ActionId 再回调 | 无第二次写入 |

### E. 中断结算

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `InterruptShower_PreservesPartialWetness` | Shower elapsed 40% 后移动 | Wetness 增加 40% 对应量，Wet milestone 未必满足 |
| `InterruptRinse_PreservesPartialFoamReduction` | Foam=.8，冲洗 50% | Foam 降至规则值，不自动清零 |
| `InterruptShampoo_PreservesPartialFoamIncrease` | 湿发揉洗 30% | Foam 增加部分，Shampoo 未达标 |
| `InterruptBlow_PreservesPartialWetnessReduction` | 吹发 45% | Wetness 部分降低，HairDry 未满足 |
| `InterruptHaircut_BelowWindow_ResolvesUnder` | 剪刀 elapsed < min | UNDER，按工具进度变化，事故不被取消 |
| `InterruptHaircut_InWindow_ResolvesPerfect` | elapsed 在窗口内 | PERFECT，正确里程碑按规则满足 |
| `InterruptHaircut_AboveWindow_ResolvesOver` | elapsed > max | OVER，Incident Major，发长偏差落地 |
| `MoveCannotCancelPendingOvercut` | 已越过 max 后请求移动 | 先 Apply OVER，再移动；事故保留 |

### F. ExitReadiness 与结算维度

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `ExitReadiness_FoamBlocksSettlement` | 必需里程碑历史完成，FoamAmount>0 | false |
| `ExitReadiness_ClumpedShampooBlocksSettlement` | ClumpedOnDryHair | false |
| `ExitReadiness_WrappedTowelBlocksSettlement` | IsTowelWrapped=true | false |
| `ExitReadiness_MovingBlocksSettlement` | IsMoving=true | false |
| `ExitReadiness_ActiveActionBlocksSettlement` | ActiveAction!=None | false |
| `ExitReadiness_WetHairAboveExitThresholdBlocksSettlement` | Wetness 高于离店阈值 | false |
| `ExitReadiness_OvercutDoesNotBlockSettlement` | 当前里程碑满足、Major/Overcut | true |
| `ExitReadiness_HairDeviationDoesNotBlockSettlement` | 轻微偏差、无阻挡态 | true |
| `MajorCompletedOutcome_IsUnhappyWaivedFinished` | 必需目标完成 + Major | Completed/Unhappy/Major/Waived/ServiceFinished |

### G. History、不变量与未知规则

| 测试名 | 输入 | 预期 |
|--------|------|------|
| `ActionHistory_RecordsPreResultPostAndDeltas` | 合法动作提交 | 一条记录含 token、前后快照、满意度与里程碑变化 |
| `Metrics_AllChangesRequireSourceAndReason` | Action/Waiting/WrongStation delta | 缺 source/reason 拒绝；有效 delta 统一应用 |
| `Invariant_SelectedToolCustomerMatchesFocus` | A 工具、B 焦点 | 开发模式告警/断言 |
| `Invariant_SelectedToolStationMatchesFocus` | 工具归站 0、焦点站 1 | 开发模式告警/断言 |
| `Invariant_QuickActionLeavesNoSelectedTool` | QuickAction 后仍有工具（故障注入） | 告警/断言 |
| `Invariant_MajorDoesNotImplyUnfinished` | Major + RequiredSatisfied | 允许 Completed；错误映射被检测 |
| `Invariant_CompletedCountMatchesBands` | 构造不一致日终数据 | 告警/断言 |
| `UnhandledRule_ReturnsDiagnosticAndNoChanges` | 矩阵 §14 任一未定义组合 | `UnhandledActionRule`；所有业务状态不变 |
| `Resolver_CannotFallbackToLegacyPath` | 注入 legacy 调用探针 | 探针未被调用 |

---

## 验收等待点

阶段 1 文档交付至此停止。只有收到明确回复：

> 阶段1验收通过，可以进入阶段2。

才允许创建阶段 2 业务类或测试；此前不得修改按钮逻辑、正式场景或旧业务写入路径。

阶段 3 额外进入门槛：

1. 正常完全冲洗必须产生 `FoamAmount=0`、`ShampooState=None`、`RinseClean.SatisfiedNow=true`。
2. `CleanTowelApplied` 仅在洗头后存在跨工位后续服务时作为必需 HistoricalEvent；洗头为最终服务时不强制包上再拆下。
