# 阶段4A实现报告

## 1. 范围与结果

阶段4A只迁移了剪发领域：

- 拆毛巾；
- 剪刀、分齿剪、推子；
- UNDER / PERFECT / OVER；
- 毛巾未拆直接剪；
- 错误剪发工具及后续补救；
- 泡沫顾客移动到剪发区后继续剪发。

正式剪发动作现在统一经过：

```text
InteractionContext
→ ActionResolver
→ ActionResult
→ ActionResultApplier.Apply
→ CustomerPhysicalState
```

未迁移吹发，未接入结算、教程或商店，未修改日终、金币、满意度展示及整体UI结构。

## 2. 修改文件清单

修改：

- `Assets/Scripts/SalonGameModel.cs`
  - 增加正式 `BeginHaircutAction` / `CompleteHaircutAction` 入口；
  - 旧 `ApplyHaircutResult` 仅保留兼容调用，并转发到新领域链；
  - 剪发门禁和拆毛巾门禁读取领域物理快照；
  - 旧 `HaircutServiceModel`、`HairStage`、情绪和既有流程只做提交后的兼容投影。
- `Assets/Scripts/SalonDemo.cs`
  - 剪发工具不再因旧毛巾/泡沫字段而禁用；
  - 不再由UI预判错误工具；
  - `HaircutInteraction` 只负责按住计时与表现；
  - 松手和中断都以真实Elapsed提交领域动作；
  - 剪发区提示读取 `CustomerPhysicalStateSnapshot`；
  - 接入阶段4A命令行录屏驱动。
- `Assets/Scripts/ServiceArchitecture/Core/Actions.cs`
  - 增加纯值 `HaircutActionParameters`，把各工具时间窗和罚值带入请求。
- `Assets/Scripts/ServiceArchitecture/Core/ActionResultApplier.cs`
  - 支持动作开始前同步订单定义，不触碰物理状态或既有进度。
- `Assets/Scripts/ServiceArchitecture/Rules/ActionResolver.cs`
  - 增加剪发规则：毛巾损坏、错误工具、泡沫影响、UNDER/PERFECT/OVER、工具进度和长度偏差。
- `Assets/Scripts/ServiceArchitecture/Adapters/Stage3WashServiceAdapter.cs`
  - 扩展为阶段3洗头与阶段4A剪发共用运行时会话；
  - 同步剪发订单工具；
  - 创建/完成剪发Token并调用Resolver/Applier。
- `Assets/Tests/ServiceArchitecture/Phase3WashIntegrationTests.cs`
  - 更新阶段边界断言：剪发在阶段4A后会写领域History。
- `Assets/Tests/StationCapabilityFlowTests.cs`
  - 更新旧“毛巾/泡沫禁用剪发”断言为阶段4A真实执行规则。

新增：

- `Assets/Scripts/Stage4AHaircutRuntimeAcceptance.cs`
- `Assets/Tests/ServiceArchitecture/Phase4AHaircutIntegrationTests.cs`
- 上述Assets文件对应的Unity `.meta` 文件
- `Docs/PHASE4A_IMPLEMENTATION_REPORT.md`
- `artifacts/phase4a-recordings/A/normal-untowel-haircut.mp4`
- `artifacts/phase4a-recordings/B/towel-on-haircut.mp4`
- `artifacts/phase4a-recordings/C/under-perfect-over.mp4`
- `artifacts/phase4a-recordings/D/wrong-tool-recovery.mp4`
- `artifacts/phase4a-recordings/E/foamy-transfer-haircut.mp4`
- 每条录屏对应的 `report.json` 和原始PNG帧。

构建产物：

- `Builds/HairSalonDemo.app` 已用阶段4A代码重新构建。

未修改Scene、Prefab、美术资源、支付模型、日终模型、商店或教程代码。

## 3. 新旧剪发调用链对比

### 旧剪发链

```text
剪发区工具栏
→ UI读取TowelWrapped / ShampooApplied并禁用工具
→ UI读取HaircutService.CurrentRequiredTool预判错误工具
→ HaircutInteraction自行判定UNDER/PERFECT/OVER
→ SalonGameModel.ApplyHaircutResult
→ 直接写HaircutServiceModel / HairStage / Satisfaction / Accident / Step
```

旧链的问题：

- UI提前决定业务结果；
- 旧 `TowelWrapped` / `ShampooApplied` 是剪发门禁；
- 错误工具不会留下按工具区分的物理进度；
- 发长偏差和剪发进度没有进入统一物理Revision；
- 中断可以丢弃已经发生的剪发时间。

### 新剪发链

```text
剪发区选择剪刀/分齿剪/推子
→ HaircutInteraction只计时
→ SalonGameModel.BeginHaircutAction
→ InteractionContext.BeginAction生成ActionToken
→ 松手或中断提交真实Elapsed
→ Stage3WashServiceAdapter.CompleteHaircutAction
→ ActionResolver.Resolve
→ ActionResult
→ ActionResultApplier.Apply
→ CustomerPhysicalState原子提交
→ PhysicalStateRevision恰好+1
→ ActionHistory追加
→ 单向兼容投影旧HaircutServiceModel/HairStage/现有UI
```

规则结果：

- 毛巾未拆：动作真实执行，毛巾变为 `Damaged`，剪发目标不推进，可拆除后补救；
- 错误工具：错误工具自身进度和不可完全消除的长度偏差保留，正确工具仍可补救；
- UNDER：按真实Elapsed提交部分工具进度，保留同工具继续；
- PERFECT：当前正确工具进度达到1并完成对应里程碑；
- OVER：工具进度达到1并形成负 `HairLengthDeviation` / 正 `OvercutSeverity`；
- 泡沫未清：不禁用剪发，正确工具仍推进，同时保留泡沫并产生可恢复错误与满意度损失。

拆毛巾仍沿用阶段3已迁移链：

```text
拆毛巾QuickAction
→ InteractionContext
→ ActionResolver
→ ActionResult
→ ActionResultApplier
→ CustomerPhysicalState.IsTowelWrapped=false
```

阶段4A将剪发区拆毛巾入口的可用性判断也改为读取领域快照，不再把旧 `TowelWrapped` 当作唯一状态。

## 4. 测试结果

阶段4A新增测试：

```bash
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/kker/Documents/ChatGPT/game2/unity-hair-salon" \
  -runTests -testPlatform EditMode \
  -testFilter "Phase4AHaircutIntegrationTests"
```

结果：8通过，0失败，0跳过。

覆盖：

1. 正常拆毛巾并剪发；
2. 毛巾未拆直接剪；
3. UNDER；
4. PERFECT；
5. OVER；
6. 错误工具后用正确工具补救；
7. 泡沫顾客跨工位继续剪发；
8. 旧 `WashStage` / `TowelWrapped` / `ShampooApplied` 不覆盖领域物理真相。

完整EditMode回归：

- 332通过；
- 0失败；
- 0跳过；
- C#编译警告、IDE诊断、录屏运行异常均为0。

正式Mac Player构建成功。

## 5. 录屏验收

录屏由重新构建的正式Mac Player运行。驱动只调用正式 `SalonGameModel` 动作入口，不直接修改领域状态。

### A. 正常拆毛巾 + 剪发

- 文件：`artifacts/phase4a-recordings/A/normal-untowel-haircut.mp4`
- 结果：通过。
- 最终：毛巾已拆、ScissorsProgress=1、Revision=6、History=6。

### B. 毛巾未拆直接剪

- 文件：`artifacts/phase4a-recordings/B/towel-on-haircut.mp4`
- 结果：通过。
- 最终：毛巾仍包裹且 `TowelCondition=Damaged`，ScissorsProgress=0，满意度下降。

### C. UNDER / PERFECT / OVER

- 文件：`artifacts/phase4a-recordings/C/under-perfect-over.mp4`
- 结果：通过。
- 流程：第一位顾客UNDER后PERFECT补剪；第二位顾客OVER。
- OVER最终：HairLengthDeviation=-0.25、OvercutSeverity=0.25。

### D. 错误剪发工具补救

- 文件：`artifacts/phase4a-recordings/D/wrong-tool-recovery.mp4`
- 结果：通过。
- 流程：错误推子留下ClippersProgress=0.3448和长度偏差，再用正确剪刀完成；
- 最终：ScissorsProgress=1、History=2，错误影响没有被补救抹除。

### E. 泡沫顾客搬去剪发区继续操作

- 文件：`artifacts/phase4a-recordings/E/foamy-transfer-haircut.mp4`
- 结果：通过。
- 最终：FoamAmount=1保持、ScissorsProgress=1、Revision=3、History=3，满意度下降。

五个场景的 `report.json` 均为 `passed=true`，Player进程均以退出码0结束。

## 6. 边界确认

- 正式剪发入口不读取旧 `WashStage` 作为剪发依据；
- 正式剪发与拆毛巾入口读取 `CustomerPhysicalStateSnapshot`，不把旧 `TowelWrapped` 当唯一状态；
- UI不预判错误剪发工具；
- UI不直接写剪发物理结果；
- 吹发仍走旧链；
- 结算、金币、日终、满意度展示、教程和商店未迁移、未修改；
- 阶段4B和阶段5未开始。
