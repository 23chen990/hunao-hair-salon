# 阶段4A.5实现报告：自由度边界、错误反馈与离店规则

## 1. 阶段结论

阶段4A.5已完成，并停止在阶段4A.5边界。

本次没有迁移吹发，没有修改教程架构，也没有进入阶段5。核心结果是：

- 物理上成立但未被订购的洗头会真实执行，并由Resolver标记为`ExtraService`；
- 错误工位仍可进入，产生一次轻度满意度事件和`?`困惑反馈；
- 错误工位持续等待只持续消耗`Patience`，不再每帧同时扣`Satisfaction`；
- 订单目标完成与物理离店准备彻底分开；
- 订单完成但仍有湿发、泡沫或毛巾时，顾客不会离店，也不会被自动修复；
- 头顶订单区域会显示独立的Cleanup / Exit Blocker提示；
- 明显不匹配的不可逆剪发使用集中配置的`ResistanceWindow`。

## 2. 修改文件清单

领域与适配层：

- `Assets/Scripts/ServiceArchitecture/Core/ServiceTypes.cs`
- `Assets/Scripts/ServiceArchitecture/Core/MetricsAndSettlement.cs`
- `Assets/Scripts/ServiceArchitecture/Core/ActionResultApplier.cs`
- `Assets/Scripts/ServiceArchitecture/Rules/ActionResolver.cs`
- `Assets/Scripts/ServiceArchitecture/Adapters/Stage3WashServiceAdapter.cs`

营业模型与UI：

- `Assets/Scripts/SalonGameModel.cs`
- `Assets/Scripts/SalonDemo.cs`
- `Assets/Scripts/HaircutInteraction.cs`
- `Assets/Scripts/OrderDemandBubbleView.cs`
- `Assets/Scripts/CustomerEmotionView.cs`

测试与验收：

- `Assets/Tests/ServiceArchitecture/Phase4A5FreedomAndExitTests.cs`（新增）
- `Assets/Tests/Phase6FinalAcceptanceTests.cs`
- `Assets/Tests/Round2SpecialFixTests.cs`
- `Assets/Tests/StationCapabilityFlowTests.cs`
- `Assets/Scripts/Stage4A5FreedomRuntimeAcceptance.cs`（新增）

产物与文档：

- `Artifacts/phase4a5-recordings/A/wrong-wash.mp4`
- `Artifacts/phase4a5-recordings/B/completed-but-wet.mp4`
- `Artifacts/phase4a5-recordings/C/normal-customer.mp4`
- `Docs/PHASE4A5_IMPLEMENTATION_REPORT.md`

## 3. 三类操作的判断方式

### 3.1 物理无效

继续由现有入口和`InteractionContext`约束：

- 无顾客或无有效工位；
- 工位类型不支持工具；
- 已有互斥`ActiveAction`；
- Token、Customer、Station、ContextVersion或PhysicalRevision不一致。

这类操作不会进入有效物理提交，也不会产生伪造的服务后果。

### 3.2 物理成立但服务错误

订单不再作为普通动作的按钮禁用条件。未要求洗头的顾客可以：

```text
花洒动作
→ ActionResolver
→ Wetness真实增加
→ OrderEffect.ExtraService
→ CustomerMetricsDelta(Satisfaction)
→ ActionHistory
→ Protest反馈
```

剪发错误工具仍沿用阶段4A的可恢复错误链。错误留下的湿度、泡沫、毛巾和剪发偏差不会因换工位而消失。

### 3.3 重大不可逆错误

无剪发需求时开始使用剪发工具：

- 开始长按立即显示`Resistance`；
- `ServiceRuleConfig.ResistanceWindowSeconds`默认0.5秒；
- 在窗口内松手，Resolver返回`ExecutionStatus.Resisted`；
- `ActionResultApplier`记录历史并结束Action，但不提交物理状态、不增加PhysicalRevision；
- 超过窗口继续执行，Resolver产生真实剪发物理变化、`OrderEffect.ExtraService`、`MistakeSeverity.Major`和Incident。

窗口只用于“重大、不可逆、与订单明显不匹配”的剪发，不用于普通错误。

## 4. WrongStation实现

错误工位移动仍通过正式`Assign`完成。首次和后续进入错误工位会：

- 保留真实Station移动；
- 增加已有`WrongStationCount`，作为Misrouted语义记录；
- 只产生一次轻度满意度事件；
- 设置`CustomerReactionKind.Confused`，头顶显示`?`；
- 不推进订单里程碑。

顾客停留在错误工位时，既有`PatienceDrainMultiplier`继续消耗耐心。等待期间不再每帧同步扣满意度；顾客表情仍按耐心阈值从正常升级为不耐烦、愤怒。

## 5. ExtraService实现

`ActionResolver`现在读取`OrderDefinition`判断花洒/洗发水是否属于订单：

- 需要洗头：保持原有Progress语义；
- 不需要洗头：动作仍执行，结果为`OrderEffect.ExtraService`；
- 物理Delta照常提交；
- 不生成无关订单里程碑；
- 通过`CustomerMetricsDelta`产生事件型满意度损失；
- 记录Incident和ActionHistory；
- 兼容投影设置`ExtraServiceCount`、`Dissatisfied`和`Protest`反应。

## 6. CustomerResistance

已实现。

领域语义为`ExecutionStatus.Resisted`，配置集中在`ServiceRuleConfig`。短按取消不会产生不可逆物理结果；持续超过窗口则视为玩家明确坚持，产生Major Incident。UI只复用现有头顶反馈槽，不新增弹窗或确认对话框。

## 7. ServiceExitReadiness变化

`ServiceExitReadiness`仍是唯一离店物理规则，不建立第二套状态机。新增只读派生信息：

- `OrderRequirementsCompleted`
- `PhysicalExitReady`
- `PrimaryBlockReason`

`TryFinalizeCompletedOrder`现在同时要求：

```text
旧兼容订单步骤已完成
+
领域物理离店条件已满足
```

阶段4B尚未迁移，因此已完成的旧Dry需求只临时兼容`DryEnough`，不能绕过泡沫、洗发残留或毛巾阻挡。

## 8. ExitBlockReason

新增`ExitBlockReason`是既有`ExitConstraint`的UI派生，不保存第二份物理真相。

当前优先级：

1. `FoamRemaining`
2. `ShampooResidue`
3. `TowelWrapped`
4. `WetHair`
5. `Moving`
6. `ActiveAction`
7. `RequirementsIncomplete`

因此订单全部完成且湿度超标时：

```text
OrderRequirementsCompleted = true
ExitReady = false
ExitBlockReason = WetHair
```

## 9. UI最小反馈

- 订单气泡仍展示顾客要完成的目标；
- 订单完成但不能离店时，气泡增加独立Cleanup条；
- 湿发提示同时包含可读的`✓ WET HAIR`和中文语义；
- 泡沫、毛巾、洗发残留使用同一最小提示槽；
- 错误工位显示`?`；
- 未要求服务和抗拒显示`!`；
- 正常服务不显示错误反馈；
- 正式文案将“当前步骤/下一步”弱化为“订单目标/顾客仍需要”；
- 洗头按钮不再因为顾客没有洗头需求而禁用；
- 吹发按钮仍视为阶段4B未迁移能力，本阶段没有改变吹发业务。

## 10. 新增测试

`Phase4A5FreedomAndExitTests`共10项：

1. 错误工位允许进入并记录困惑反应；
2. 未要求花洒真实改变Wetness并记录ExtraService；
3. 错误工位等待只扣Patience且不清空订单；
4. 订单完成但湿发时不能离店；
5. WetHair / FoamRemaining / TowelWrapped原因映射；
6. 换工位后湿度保持；
7. 无顾客剪发仍是PhysicalInvalid；
8. 需求不匹配不会挡在Resolver之前；
9. 不可逆错误的ResistanceWindow；
10. 订单气泡展示Cleanup原因。

结果：10通过，0失败。

旧测试中“洗头+剪发后即使湿发也离店”的断言已按阶段4A.5规则更新。等待测试也改为验证Patience与Satisfaction不在每帧同时下降。

## 11. 完整EditMode结果

执行环境：Unity 6000.5.8f1，EditMode。

最终结果：

- 342通过；
- 0失败；
- 0跳过；
- IDE诊断0；
- 正式Mac Player构建成功。

## 12. 三个录屏结果

### A. 错误洗头

- 文件：`Artifacts/phase4a5-recordings/A/wrong-wash.mp4`
- 结果：通过；
- WrongStationCount=1；
- ExtraServiceCount=1；
- 头顶先显示困惑，花洒后显示`!`；
- Wetness=1，ActionHistory=1。

### B. 订单完成但不能离店

- 文件：`Artifacts/phase4a5-recordings/B/completed-but-wet.mp4`
- 结果：通过；
- 剪刀和分齿剪领域里程碑均完成；
- `OrderRequirementsCompleted=true`；
- `ExitReady=false`；
- `ExitBlockReason=WetHair`；
- 顾客保持Serving且没有生成付款；
- 订单区域显示`✓ WET HAIR / 湿发待收尾`。

### C. 正常顾客

- 文件：`Artifacts/phase4a5-recordings/C/normal-customer.mp4`
- 结果：通过；
- WrongStationCount=0；
- ExtraServiceCount=0；
- 无错误反应；
- `ExitReady=true`。

三个场景的`report.json`均为`passed=true`，正式Player均以退出码0结束。

## 13. 边界确认

- 未迁移吹发；
- 未修改教程架构；
- 未重做UI；
- 未自动清理Wetness、Foam或Towel；
- 未修改金币、结算价格、小费、商店或日终；
- 未进入阶段4B或阶段5。
