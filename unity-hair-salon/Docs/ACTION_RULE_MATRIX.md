# ACTION_RULE_MATRIX — 动作规则矩阵（第一版）

> 原则：**物理上不可能 → 可禁止（Invalid）；流程上不正确 → 允许执行 → 由 Resolver 判定后果。**  
> 工具可用性由工位能力 + 物理条件决定；对错由本矩阵决定。

图例：

| 字段 | 含义 |
|------|------|
| executed | 是否真实发生 |
| orderEffect | None / Progress / ExtraService |
| mistakeSeverity | None / Minor / Recoverable / Major |
| executionQuality | NotApplicable / Under / Perfect / Over |
| 持续状态 | 必须留下的物理态（禁止“弹字后恢复”） |
| 补救 | 玩家可达路径（需消耗营业时间） |

规则执行契约：

```text
ActionRequest
+ OrderDefinition
+ PhysicalStateSnapshot
+ ServiceProgressSnapshot
+ InteractionContextSnapshot
→ ActionResolver（纯计算）
→ ActionResult（携带 ExpectedRevision）
→ Apply 前验证 ExpectedRevision == PhysicalStateRevision
→ 不一致返回 StaleState；一致则原子提交并 Revision++
→ 重新计算 MilestoneKind / EverCompleted / SatisfiedNow / NeedsRedo
→ SettlementGate
→ 成功后尽力追加非权威 ActionHistory
```

Resolver 不得直接修改真实状态。`ActionResult.timeCost` / `elapsedTimeForLog` 只用于日志和统计，不推进世界时间；Hold 消耗的时间已经被世界时钟计算。

`InteractionContextVersion` 防止旧交互跨顾客/工位提交；`PhysicalStateRevision` 防止旧物理快照覆盖新状态。两者均为阶段 2 强制校验，不能互相替代。

---

## A. 物理门禁（唯一允许禁用 / Invalid 的情况）

| 条件 | 结果 |
|------|------|
| 当前工位没有该工具 | Invalid |
| 工位被其他顾客占用（移动目标） | Invalid（移动） |
| 顾客处于不可打断极短动画 | Invalid / 忽略输入 |
| 拆毛巾但 `IsTowelWrapped == false` | Invalid 或弱化按钮 |
| 顾客已离店或订单已完全结算 | Invalid |
| 无有效操作目标 | Invalid |
| CompleteHold 的 ActionToken 与 CustomerId / StationId / ContextVersion 不一致 | Invalid；过期回调不得提交 |

**禁止**因“当前推荐步骤不是 X”而禁用 X。

---

## B. 洗头工位工具可用性

工位固定工具：花洒、洗发水、毛巾槽。第三槽位置固定，显示行为随状态切换：

- `IsTowelWrapped=false`：包毛巾
- `IsTowelWrapped=true`：拆毛巾

因此泡沫毛巾可在洗头工位直接拆除，不能强迫玩家先移到剪发区。

| 工具 | 物理可执行条件 | 流程错误时 |
|------|----------------|------------|
| 花洒 | 在洗头工位、可操作、无不可打断动画 | **仍可执行** |
| 洗发水 | 同上 | **仍可执行** |
| 毛巾槽（包/拆 QuickAction） | 同上；单击立即执行，不进 selectedTool | **仍可执行** |

---

## C. 剪发工位工具可用性

工位固定工具：拆毛巾、剪刀、分齿剪、推子、手动吹发。

| 工具 | 物理可执行条件 | 流程错误时 |
|------|----------------|------------|
| 拆毛巾 | `IsTowelWrapped` | 无毛巾 → Invalid |
| 剪刀 / 分齿剪 / 推子 | 在剪发工位即可 | **戴毛巾也允许** |
| 手动吹发 | 在剪发工位即可 | **戴毛巾 / 有泡沫也允许** |

---

## D. 规则明细

### 1. 干发时使用花洒

| 项 | 值 |
|----|----|
| 动作 | 长按花洒 |
| 物理 | `Wetness` 增加至达标 |
| orderEffect | Progress |
| mistakeSeverity | None |
| executionQuality | N/A |
| 里程碑 | 完成 `WetHairMilestone` |
| Toast | 可选极短“头发湿了” |

---

### 2. 干发时直接使用洗发水

| 项 | 值 |
|----|----|
| 动作 | 可执行（点选 + 长按揉洗或点选即涂抹，与现交互对齐） |
| 物理 | `ShampooState = ClumpedOnDryHair`；`Wetness` 仍不足；结块视觉 |
| orderEffect | None |
| mistakeSeverity | Recoverable |
| 满意度 | 轻微下降（不可因补救完全恢复） |
| 里程碑 | **不**完成正常 `ShampooMilestone` |
| 补救 | 花洒打湿 → 再次揉洗 → `ShampooState=Normal` 且起泡 |
| 禁止 | 仅 Toast“操作错误”后恢复干净 |

---

### 3. 已湿发时正确使用洗发水

| 项 | 值 |
|----|----|
| 动作 | 长按揉洗 |
| 物理 | `FoamAmount` 渐增；`ShampooState=Normal` |
| orderEffect | Progress（达标后） |
| mistakeSeverity | None |
| 里程碑 | `ShampooMilestone` |

---

### 4. 已有足够泡沫后再次使用洗发水

| 项 | 值 |
|----|----|
| 物理 | `FoamAmount` 可继续增加；浪费时间 |
| orderEffect | ExtraService |
| mistakeSeverity | Minor |
| 里程碑 | **不**重复完成洗发目标 |
| 禁止 | 把主流程切回“正在洗发”订单步骤 |

---

### 5. 有泡沫时使用花洒

| 项 | 值 |
|----|----|
| 动作 | 长按冲洗 |
| 物理 | `FoamAmount` 渐减；视觉泡沫减少；完全冲洗时明确提交 `FoamAmount=0`、`ShampooState=None` |
| orderEffect | Progress（冲净后） |
| mistakeSeverity | None |
| 里程碑 | `RinseClean.SatisfiedNow=true` |

---

### 6. 泡沫未冲净就包毛巾

| 项 | 值 |
|----|----|
| 动作 | QuickAction 单击，**必须允许** |
| 物理 | `IsTowelWrapped=true`；`TowelContamination=Foam`；`TowelCondition=Intact`；`FoamAmount` **不得自动清零** |
| orderEffect | None |
| mistakeSeverity | Recoverable |
| 满意度 | 下降 |
| 补救 | 拆毛巾 → 冲洗剩余泡沫 → 重新包干净毛巾（全程耗时） |
| 死锁防护 | 洗头工位同一毛巾槽立即切换为“拆毛巾”；剪发工位也提供拆毛巾 |
| 禁止 | “现在不能包毛巾”并拒绝执行 |

---

### 7. 正确冲洗后包毛巾

| 项 | 值 |
|----|----|
| 物理 | `IsTowelWrapped=true`；`TowelContamination=None`；`TowelCondition=Intact` |
| orderEffect | Progress |
| mistakeSeverity | None |
| 里程碑 | `CleanTowelApplied.EverCompleted=true`；仅当洗头后仍需跨工位服务时属于 RequiredMilestones |
| 后续 | 可自由移动到其他工位（不自动拆/清状态） |

---

### 8. 包着毛巾使用剪刀 / 推子 / 分齿剪

| 项 | 值 |
|----|----|
| 动作 | **必须允许**（不可灰掉） |
| 物理 | `IsTowelWrapped=true`；`TowelCondition=Damaged`；保留原 `TowelContamination`；不推进正常剪发里程碑 |
| 剪刀 | 剪到毛巾 |
| 推子 | 损坏毛巾 |
| 分齿剪 | 毛巾被扯坏 |
| orderEffect | None |
| mistakeSeverity | 短操作 Recoverable；长按过久 Major |
| 满意度 | 下降 |
| 补救 | 拆除损坏毛巾 → 继续服务；错误历史与满意度损失保留 |
| 禁止 | Toast“请先拆毛巾”后状态不变 |

---

### 9. 正确拆毛巾

| 项 | 值 |
|----|----|
| 动作 | QuickAction 单击；不经 `selectedTool`；不进 Hold |
| 物理 | `IsTowelWrapped=false`；毛巾视觉消失；污染/损坏信息可归档到 ActionHistory 后重置为默认 |
| 若 `TowelContamination=Foam` | 拆除后泡沫/洗发残留仍在 |
| 清理交互 | `selectedTool=None`，`activeHoldAction=None` |
| orderEffect | Progress（若订单需要拆毛巾里程碑）或 None |
| mistakeSeverity | None |

---

### 10. 使用正确剪发工具（点选 + 长按）

保留 UNDER / PERFECT / OVER：

| 质量 | orderEffect | 里程碑 | 其他 |
|------|--------------|--------|------|
| UNDER | None | 不完成；可继续剪 | 满意度轻微下降或不变；OrderStatus 仍 InProgress |
| PERFECT | Progress | 完成当前剪发工具目标 | 正常推进 |
| OVER | Progress | **视为完成**当前剪发目标 | `mistakeSeverity=Major`；`executionQuality=Over`；写 `HairLengthDeviation`，`OvercutSeverity` 由其派生；**不是未完成** |

OVER 结算口径：

```text
OrderStatus = Completed（若其余订单目标也完成）
SatisfactionBand = Unhappy
IncidentSeverity = Major
PaymentOutcome = Waived
LeaveReason = ServiceFinished
```

计入：完成服务 + 不满意；**不**计入未完成。

---

### 11. 使用错误剪发工具

例：需要分齿剪却用剪刀。

| 项 | 值 |
|----|----|
| 动作 | 可执行 |
| 物理 | 对所用工具的 `HaircutProgressByTool[tool]` 增加；`HairLengthDeviation` 累积 |
| orderEffect | ExtraService 或 None |
| mistakeSeverity | Minor / Recoverable；重复过多可升 Major |
| 里程碑 | **不**完成正确工具目标 |
| 补救 | 之后仍可用正确工具；偏差不能完全清零 |

---

### 12. 毛巾已拆但头发仍有泡沫时剪发

| 项 | 值 |
|----|----|
| 动作 | 可执行；**不**因泡沫禁用剪发工具 |
| 结果 | 正确工具仍可能推进部分剪发目标 **且** 产生“泡沫影响剪发”错误 |
| mistakeSeverity | Recoverable（或按严重度） |
| 视觉 | 泡沫与碎发混合反馈 |
| 满意度 | 下降 |

---

### 13. 吹发

| 场景 | orderEffect | mistakeSeverity | 物理 / 备注 |
|------|--------------|-----------------|-------------|
| 湿发且洗头目标已完成、无泡沫无毛巾 | Progress | None | 吹风直接降低 `Wetness`；达到阈值后 `HairDry.SatisfiedNow=true` |
| 头上仍有泡沫 | None | Recoverable | 泡沫飞溅；不完成正常吹干 |
| 头上有毛巾 | None | Minor / Recoverable | 吹的是毛巾；浪费时间；可能松动/损坏毛巾 |
| 头发已干继续吹 | ExtraService | Minor | — |
| 长按过久 | — | Major 可能 | 过热 / 烫到顾客 |

`Wetness` 是唯一可写干湿真相，不保存可写 `BlowDryProgress`。UI 吹发进度从 Wetness 或当前 Action 进度派生。重新打湿后 `HairDry.SatisfiedNow=false`、`NeedsRedo=true`；`WetHairApplied` 作为 HistoricalEvent 不失效。

---

### 14. 不需要洗头的顾客被洗头

| 项 | 值 |
|----|----|
| 动作 | 真实发生；物理态改变 |
| orderEffect | ExtraService |
| mistakeSeverity | Minor（可按次数升级） |
| 里程碑 | **不**推进订单洗头目标（订单本无该目标） |
| 时间 | 消耗真实营业时间 |

---

### 15. 顾客移动

| 项 | 值 |
|----|----|
| 条件 | 非不可打断极短动画即可移动到可用位置 |
| 改变 | 仅 `CustomerModel.Station`（及移动状态机）；PhysicalState 不保存第二份工位字段 |
| **不得**自动 | 清泡沫/湿度/毛巾、完成包拆毛巾、修复错误、重置服务目标、纠正下一工位、清满意度损失 |
| 错误工位 | 允许到达；一次性轻微降满意度；晾着则耐心继续下降；**不**自动送回 |
| 开始移动时清交互 | `selectedTool` / `activeHoldAction` / 操作提示；物理与进度完整保留 |

移动或切换中断连续动作时，先用旧 ActionToken 和实际 elapsed 结算，再递增 InteractionContextVersion：

- 花洒、冲洗、揉洗、吹发：保留已发生的部分 Wetness / FoamAmount；吹发通过降低 Wetness 留下部分效果。
- 剪发：按实际持续时间结算 UNDER / PERFECT / OVER；不可借移动无损取消。

---

### 16. 切换顾客 / 工位聚焦

| 事件 | 交互 | 顾客 A/B 业务状态 |
|------|------|-------------------|
| A 选剪刀后点 B | B 不继承工具与提示 | A 物理与进度保留 |
| QuickAction | 清空 selectedTool | 按规则改物理态 |

`BeginHold` 必须保存 `ActionToken { CustomerId, StationId, ContextVersion, ActionId, StartedAt }`；`CompleteHold`、协程和动画回调提交前必须重新验证。切换顾客、移动、换工位、退出聚焦、QuickAction、工具不属于工位时递增 Version。

动作结束后是否保留工具：

| 结果 | selectedTool |
|------|--------------|
| UNDER、未冲净、未吹干，且工具仍属于当前工位 | 保留，便于继续 |
| QuickAction、切换顾客、移动、换工位、退出聚焦、工具失效 | 清空 |
| 订单完全结算 | 清空 |

---

## E. 耐心 vs 满意度（保持分离）

| | Patience | Satisfaction |
|--|----------|--------------|
| 含义 | 还能等多久 | 整次服务做得怎样 |
| 下降场景 | 无人等待、错工位晾着、工序间无人、忙别的顾客 | 等待历史、错工位、错工具、重复服务、返工、UNDER/OVER、泡沫超时、吹风过久、剪太短、补救是否及时、性格 |
| 服务中 | 正式主动操作可暂停或明显减缓 | 开始服务 **不清零** 已发生的等待满意度损失 |
| 补救 | — | 避免进一步恶化，**不能**完全抹除已损失 |

建议中性基准：Satisfaction = 70。

Satisfaction 与 Patience 仍存放于 CustomerModel，但 Action、WaitingTick、WrongStation 等不得直接赋值或加减，只能提交带来源的 delta：

```text
CustomerMetrics.ApplyDelta(customer, satisfactionDelta, patienceDelta, source, reason)
```

---

## F. QuickAction 契约（包毛巾 / 拆毛巾）

```text
点击一次 → 立即 Resolve → Apply → 短动画 → 更新毛巾三个维度
```

禁止：

- 写入 `selectedTool`
- 要求再长按顾客
- 继承上一工具
- 显示“已选择花洒”
- 进入普通 HoldInteraction

---

## G. 里程碑种类、重做与结算门

每个里程碑区分：

- `EverCompleted`：历史上完成过，永不因返工删除。
- `SatisfiedNow`：当前仍满足。
- `NeedsRedo = EverCompleted && !SatisfiedNow`（或保存明确返工原因）。

| MilestoneKind | 最终判定 | 第一版成员 |
|---------------|----------|------------|
| HistoricalEvent | 要求 `EverCompleted` | WetHairApplied、Shampooed、CleanTowelApplied、CutToolCompleted |
| RevalidatableState | 要求 `SatisfiedNow` | RinseClean、HairDry、Untoweled |
| ExitConstraint | 由 ServiceExitReadiness 检查 | NoFoam、NoClumpedShampoo、NoTowel、NotMoving、NoActiveAction |

WetHairApplied 在吹干后不失效；CleanTowelApplied 在拆毛巾后不失效。CleanTowelApplied 只在洗头后还有跨工位服务时作为必需 HistoricalEvent；洗头为最终服务时不强制包上再拆下。

| 后续动作 | 历史 | 当前满足变化 |
|----------|------|--------------|
| RinseClean 满足后重新上洗发水 | 洗发历史事件保留 | `RinseClean.SatisfiedNow=false` |
| HairDry 满足后重新打湿 | 历史事件不受影响 | `HairDry.SatisfiedNow=false` |
| Untowel 完成后重新包毛巾 | `Untowel.EverCompleted=true` | `Untowel.SatisfiedNow=false` |

SettlementGate / ServiceExitReadiness：

```text
All HistoricalEvent milestones: EverCompleted
&& All RevalidatableState milestones: SatisfiedNow
&& ServiceExitReadiness
```

ServiceExitReadiness 阻挡：泡沫未清、干发洗发水结块、仍包毛巾、Wetness 高于离店阈值、正在移动、正在操作。  
不阻挡结算但影响结果：发型偏差、剪太短、历史事故。

第一版 Major 事故统一 `PaymentOutcome=Waived`，不实现 Compensation。

---

## H. 洗头与毛巾组合规则补充

| 编号 | 前置状态 + 动作 | 物理结果 | 订单 / 错误 / 补救 |
|------|-----------------|----------|--------------------|
| W1 | 已湿且无泡沫，再用花洒 | Wetness 保持或增加到 1 | ExtraService + Minor；不重复推进 WetHair |
| W2 | 已冲净，再用花洒 | Wetness 保持 1 | ExtraService；Rinse 不重复完成 |
| W3 | HairDry 满足后重新用花洒 | Wetness 增加 | HairDry SatisfiedNow=false / NeedsRedo；WetHairApplied 历史不失效 |
| W4 | 已包毛巾，再点同一毛巾槽 | 立即拆毛巾 | QuickAction；清 selectedTool；不得叠第二条毛巾 |
| W5 | 戴干净毛巾冲水 | 毛巾可变湿；TowelWetness 增加；头发作用受毛巾阻挡或仅少量变湿 | ExtraService / Minor；不完成正常 Wet/Rinse |
| W6 | 戴污染毛巾冲水 | TowelWetness 增加；污染不自动消失；底下 FoamAmount 按规则仅少量或不变 | Recoverable；应先拆毛巾再冲洗 |
| W7 | 戴毛巾上洗发水 | TowelContamination=Shampoo 或 Foam；保持 TowelCondition | Recoverable；不完成 Shampoo；拆毛巾后残留仍在 |
| W8 | 泡沫毛巾被剪/推/扯 | `Contamination=Foam` **且** `Condition=Damaged` 同时存在 | Recoverable/Major；拆除后底下泡沫仍在 |
| W9 | 干发洗发水结块后用花洒 | Wetness 增加；结块软化但不直接完成 Shampoo | 仍需再次揉洗；原满意度损失保留 |
| W10 | 结块软化后再次揉洗 | ShampooState→Normal；FoamAmount 渐增 | 达标后 Shampooed.EverCompleted=true；之后仍需 RinseClean |
| W11 | RinseClean 满足后重新上洗发水 | FoamAmount 增加；ShampooState=Normal | RinseClean SatisfiedNow=false；NeedsRedo |
| W12 | Untowel 完成后重新包毛巾 | IsTowelWrapped=true，污染/损坏按当前状态判定 | Untowel SatisfiedNow=false；NeedsRedo |
| W13 | 已有足量泡沫继续花洒但提前松手 | FoamAmount 按 elapsed 部分减少 | Rinse 未满足；保留花洒工具以继续 |
| W14 | 未吹干提前松手 | Wetness 按 elapsed 部分降低 | HairDry 未满足；保留吹风工具以继续 |

上述组合全部通过快照 Resolver 计算，不能让 UI 预先改状态。

阶段 3 进入门槛：

1. 正常完全冲洗结果必须明确为 `FoamAmount=0`、`ShampooState=None`、`RinseClean.SatisfiedNow=true`。
2. `CleanTowelApplied` 只在洗头后仍有跨工位服务时加入 Required HistoricalEvents；洗头为最终服务时不要求包毛巾再拆除。

---

## I. 与测试用例映射

| Case | 覆盖规则 |
|------|----------|
| 1 标准洗头 | 1,3,5,7,9,15 |
| 2 干发洗发水 | 2 |
| 3 泡沫包毛巾 | 6,9 |
| 4 戴毛巾剪刀 | 8,9 |
| 5 戴毛巾吹风 | 13 |
| 6 泡沫搬去剪发 | 12,15 |
| 7 无洗头需求却洗 | 14 |
| 8 错误剪发工具 | 11 |
| 9 重复同一剪发工具 | 4 同类 ExtraService + 升级 |
| 10 UNDER | 10 |
| 11 PERFECT | 10 |
| 12 OVER | 10 + 结算口径 |
| 13 移动清交互 | 15 |
| 14 切换顾客 | 16 |
| 15 教程软引导 | 教程约束（见架构计划） |
| 16 日终统计 | OVER≠未完成；开心+一般+不满意=完成 |

---

## J. 未覆盖规则策略与编码检查清单

当 ActionRequest 的工位、动作、物理状态组合未匹配本矩阵时：

```text
executed = false
diagnostic = UnhandledActionRule
开发模式 Debug.LogError / Assert
```

不得静默调用旧 `BeginWashAction` / `ApplyHaircutResult`，也不得把未知组合默认当 ExtraService。

- [ ] 无“因步骤不对而灰按钮”（物理门禁除外）
- [ ] 所有洗头/毛巾/剪发/吹风入口经纯 ActionResolver + 原子 Apply
- [ ] Resolver 计算过程中真实状态不变
- [ ] Snapshot/ActionResult 强制携带 ExpectedRevision；冲突返回 StaleState
- [ ] 每次成功业务提交 PhysicalStateRevision 恰好 +1
- [ ] 错误留下毛巾污染/损坏、ShampooState、HairLengthDeviation 等持续态
- [ ] 每条 Recoverable 有可达补救
- [ ] 移动不清物理态
- [ ] QuickAction 后 selectedTool 为空
- [ ] 可继续的 UNDER / 未冲净 / 未吹干保留工具
- [ ] 旧 ActionToken 不得跨顾客/工位提交
- [ ] 中断动作按实际 elapsed 提交部分进度或剪发质量
- [ ] OVER → Completed + Unhappy + Major + Waived，非 Unfinished
- [ ] SettlementGate 同时检查里程碑当前满足与阻挡物理态
- [ ] 湿度高于离店阈值时 ServiceExitReadiness=false
- [ ] Satisfaction/Patience 只经 CustomerMetrics.ApplyDelta
- [ ] UnhandledActionRule 在开发模式显式报告
- [ ] ActionHistory 非权威；日志失败不回滚业务状态
