# 阶段2实现报告

## 1. 结果摘要

阶段2已建立独立的顾客服务纯领域层。新代码位于 `HairSalon.ServiceArchitecture` namespace，仅在
EditMode测试中运行；没有接入场景、UI、MonoBehaviour、旧顾客状态或世界时钟。

- Unity版本：`6000.5.8f1`
- C#编译语言版本：`9.0`（由Unity测试编译日志确认）
- Runtime程序集：`HairSalon.Runtime`
- Editor测试程序集：`HairSalon.Tests`
- 阶段2测试：94 / 94通过
- 完整EditMode回归：316 / 316通过
- C#编译警告：0

## 2. 新增和修改文件

新增：

- `Assets/Scripts/ServiceArchitecture/Core/ServiceTypes.cs`
- `Assets/Scripts/ServiceArchitecture/Core/CustomerPhysicalState.cs`
- `Assets/Scripts/ServiceArchitecture/Core/ServiceProgress.cs`
- `Assets/Scripts/ServiceArchitecture/Core/InteractionContext.cs`
- `Assets/Scripts/ServiceArchitecture/Core/MetricsAndSettlement.cs`
- `Assets/Scripts/ServiceArchitecture/Core/Actions.cs`
- `Assets/Scripts/ServiceArchitecture/Core/ActionResultApplier.cs`
- `Assets/Scripts/ServiceArchitecture/Rules/ActionResolver.cs`
- `Assets/Scripts/ServiceArchitecture/Diagnostics/ActionHistory.cs`
- `Assets/Tests/ServiceArchitecture/Phase2ServiceArchitectureTests.cs`
- 上述Assets目录和文件对应的Unity `.meta` 文件
- `Docs/PHASE2_IMPLEMENTATION_REPORT.md`

未修改任何既有 `.cs`、测试、场景、Prefab或美术资源。

## 3. 核心类型职责

- `CustomerPhysicalState`：物理状态唯一可写真相；保存Wetness、FoamAmount、ShampooState、正交毛巾状态、
  按工具剪发进度、HairLengthDeviation和强制Revision，不保存工位。
- `CustomerPhysicalStateSnapshot`：Resolver使用的不可变物理快照，数组采用防御性复制。
- `HaircutProgressByTool`：固定三槽保存Scissors、ThinningShears、Clippers进度。
- `OrderDefinition` / `ServiceMilestoneDefinition`：只描述订单需求、必需里程碑、剪发工具和跨工位毛巾需求。
- `ServiceProgress` / `ServiceMilestoneState`：分开保存EverCompleted、SatisfiedNow并派生NeedsRedo。
- `ServiceProgressSnapshot`：不可变服务进度输入。
- `InteractionContext`：统一管理焦点、选择工具、活动ActionToken和ContextVersion。
- `InteractionContextSnapshot`：不可变交互输入。
- `ActionToken`：绑定ActionId、顾客、工位、ContextVersion、ExpectedPhysicalRevision、动作、工具和开始时间。
- `ActionRequest`：连续动作的纯值请求，包含真实Elapsed和中断标记。
- `ActionResult`：携带执行状态、订单效果、错误严重度、质量、全部Delta、事件、诊断和ExpectedRevision。
- `ActionResolver`：纯计算分派器；不修改输入、不写日志、不推进时间、不访问旧流程。
- `PhysicalStateDelta`：可验证并限制范围的物理差量。
- `ActionResultApplier`：完整验证、计算NextState、检查不变量并原子提交。
- `CustomerMetricsDelta` / `CustomerMetricsDomain`：满意度和耐心的统一、可溯源入口。
- `ServiceExitReadiness`：独立检查订单里程碑和离店物理/运行约束。
- `ActionHistoryEntry` / `IActionHistorySink` / `InMemoryActionHistory`：成功提交后的非权威诊断历史。
- `ServiceInvariants`：提交前集中验证数值范围和洗发状态一致性。
- `HoldQualityEvaluator`：按真实Elapsed确定UNDER / PERFECT / OVER。
- `ServiceOutcomeEvaluator`：验证重大事故不等同于Unfinished的纯结算模型。

## 4. 最终数据流

```text
ActionRequest
+ OrderDefinition
+ CustomerPhysicalStateSnapshot
+ ServiceProgressSnapshot
+ InteractionContextSnapshot
+ CustomerMetricsSnapshot
        ↓
ActionResolver（纯计算）
        ↓
ActionResult（保留ExpectedPhysicalRevision）
        ↓
ActionResultApplier完整验证
        ↓
计算完整NextPhysical / NextMetrics / NextProgress / NextExitReadiness
        ↓
ServiceInvariants
        ↓
一次性提交业务状态并Revision++
        ↓
标记ActionId已提交
        ↓
尽力追加非权威ActionHistory
```

ActionHistory失败会调用 `Debug.LogError`（可注入日志函数用于测试），但不会回滚已提交业务状态。

## 5. PhysicalStateRevision与ContextVersion

- `PhysicalStateRevision`：防止旧物理快照覆盖新状态。Snapshot和ActionToken记录Revision；Apply时必须与当前物理
  Revision一致；成功提交后恰好加一。
- `ContextVersion`：防止旧焦点、旧工具选择、旧移动前交互和旧回调串台。切换顾客或统一清理交互时递增。

两者独立校验，不能互相替代。ActionToken还校验顾客ID、工位ID和活动ActionId，因此A顾客Token不能写B顾客。

## 6. Resolver与Apply边界

Resolver：

- 只读六类纯值输入；
- 计算动作真实发生后的Delta、里程碑事件、指标变化和诊断；
- 相同输入产生相同结果；
- 未知组合明确返回 `UnhandledActionRule` 和空业务变化；
- 不访问CustomerModel、SalonGameModel、场景对象、UI、历史记录或世界时钟。

Apply：

- 检查重复ActionId、顾客/工位、ContextVersion、活动Token、ExpectedPhysicalRevision、离店/结算状态和Result合法性；
- 在写入前计算全部NextState、重算进度和ExitReadiness并验证不变量；
- 验证失败不修改湿度、泡沫、毛巾、指标、事故、里程碑或订单准备状态；
- 成功后Revision只增加一次，重复回调返回AlreadyApplied。

## 7. 三种里程碑

- `HistoricalEvent`：结算读取 `EverCompleted`。WetHairApplied、Shampooed、CleanTowelApplied及三种剪发工具属于此类；
  后续状态变化不会抹除历史事实。
- `RevalidatableState`：结算读取 `SatisfiedNow`。RinseClean、HairDry、Untoweled属于此类；
  `NeedsRedo = EverCompleted && !SatisfiedNow`。
- `ExitConstraint`：不存入普通服务步骤，不参与步骤计数；由ServiceExitReadiness实时检查。

WashThenCut要求CleanTowelApplied历史事件和Untoweled当前状态；WashOnly不要求无意义的包/拆毛巾循环。

## 8. ServiceExitReadiness阻挡条件

- Required HistoricalEvent尚未EverCompleted；
- Required RevalidatableState当前未SatisfiedNow；
- FoamAmount高于离店阈值；
- ShampooState为ClumpedOnDryHair；
- ShampooState存在任何残留；
- 仍包着毛巾；
- Wetness高于离店阈值；
- 正在移动；
- 仍有活动动作。

发型轻微偏差、剪短、重大事故、满意度损失和重复服务历史不会阻止结算。

## 9. 测试命令与结果

阶段2测试：

```bash
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/kker/Documents/ChatGPT/game2/unity-hair-salon" \
  -runTests -testPlatform EditMode \
  -testFilter "Phase2ServiceArchitectureTests" \
  -testResults "/tmp/phase2-green.xml" \
  -logFile "/tmp/phase2-green.log"
```

结果：新增94个测试用例，94通过，0失败，0跳过。

完整回归：

```bash
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/kker/Documents/ChatGPT/game2/unity-hair-salon" \
  -runTests -testPlatform EditMode \
  -testResults "/tmp/all-editmode.xml" \
  -logFile "/tmp/all-editmode.log"
```

结果：316通过，0失败，0跳过。现有EditMode测试全部通过，没有删除或削弱既有测试。

## 10. 已实现规则

- 干发花洒按Elapsed部分增加Wetness并完成WetHairApplied；
- 干发洗发水真实执行，形成ClumpedOnDryHair，不完成Shampooed，满意度轻微下降并给出补救建议；
- 湿发洗发水按Elapsed增加FoamAmount，达阈值完成Shampooed；
- 部分/完全冲洗，完全冲洗原子归一为FoamAmount=0、ShampooState=None；
- 冲净后重新上洗发水使RinseClean进入NeedsRedo；
- 吹风直接降低Wetness，重新打湿使HairDry进入NeedsRedo；
- 包毛巾生成污染，拆毛巾保留头发上的泡沫和洗发水；
- QuickAction不进入SelectedTool；
- 连续动作中断保留真实部分结果；
- HoldQualityEvaluator按真实Elapsed分类UNDER / PERFECT / OVER。

## 11. 当前返回UnhandledActionRule的组合

- `ServiceActionType.Haircut`的完整物理Delta与里程碑规则（阶段2只建立HoldQualityEvaluator）；
- `ServiceActionType.Unknown`；
- 动作与工具不匹配的组合，例如Shower动作配Shampoo工具；
- 阶段2范围外的染发、烫发、商店、教程、多人及其他未登记动作。

不会回退到旧流程，也不会默认成功。

## 12. 正式运行行为与硬边界

没有修改或调用：

- `SalonDemo`、`BuildToolBar`；
- `SalonGameModel.BeginWashAction`；
- `SalonGameModel.ResolveWashToolSelection`；
- `SalonGameModel.ApplyHaircutResult`；
- 场景按钮点击事件；
- 顾客移动、寻路、FIFO、世界时钟、金币、教程、Toast、日终、商店、烫染、多人；
- Scene、Prefab和美术资源。

新旧系统没有双写。旧游戏继续只使用原有CustomerModel字段；新系统只在独立领域状态和EditMode测试中运行。

## 13. 编译警告和已知问题

- C#编译错误：0。
- C#编译警告：0。
- IDE诊断：0。
- Unity批处理日志出现本机License Client握手/令牌刷新诊断，但测试进程获得有效许可并以成功码完成；
  不影响编译与316个EditMode测试结果。
- 阶段2尚无CustomerModel适配器、持久化或正式UI接入，这是硬边界，不是遗漏。
- ActionHistory是进程内诊断接口；持久化策略留待后续阶段决定。

## 14. 阶段3候选接入文件（本阶段未修改）

- `Assets/Scripts/SalonDemo.cs`：正式工具栏、长按开始/结束、QuickAction按钮和反馈适配候选。
- `Assets/Scripts/SalonGameModel.cs`：现有BeginWashAction、ResolveWashToolSelection、物理状态与指标适配候选。
- `Assets/Scripts/PlayerContext.cs`：现有工具选择与交互清理适配候选。
- `Assets/Scripts/SalonServiceChain.cs`：现有WashAction、ActiveServiceAction和阈值配置映射候选。
- `Assets/Scripts/SalonCustomerPath.cs`：仅在阶段3需要把MovementStarted/StationChanged接入ContextVersion时评估。
- `Assets/Scripts/ShampooTutorial.cs`：正式业务稳定后再做只读事件订阅；不得参与业务推进。
- `Assets/Tests/ShampooInteractionRedesignTests.cs`、`Assets/Tests/StationCapabilityFlowTests.cs`：
  阶段3接入回归与适配测试候选。

阶段2到此停止，不接入正式洗头按钮，等待“阶段2验收通过，可以进入阶段3”。
