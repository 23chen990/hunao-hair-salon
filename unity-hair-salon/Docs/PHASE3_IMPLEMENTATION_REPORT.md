# 阶段3实现报告

## 1. 范围与结果

阶段3仅迁移了正式游戏中的花洒、洗发水、包毛巾、拆毛巾和顾客移动交互。

- 剪发：未迁移。
- 吹发：未迁移。
- 结算：未迁移，仍调用既有结算链。
- 教程：未迁移，仍是既有只读通知。
- UI：仅取消旧洗头预判写入、增加洗头床毛巾槽的包/拆切换，并读取领域物理快照绘制湿度和泡沫。
- 旧代码：未删除。
- 阶段4：未开始。

## 2. 修改文件清单

修改：

- `Assets/Scripts/SalonGameModel.cs`
  - 正式洗头Hold、毛巾QuickAction、毛巾移除和移动开始接入阶段3适配器。
  - 提供只读领域Snapshot和History查询。
  - 保留旧剪发、吹发、结算、教程和旧兼容API。
- `Assets/Scripts/SalonDemo.cs`
  - 正式花洒/洗发水按钮不再调用会写旧状态的 `ResolveWashToolSelection`。
  - 洗头床毛巾槽按当前物理状态切换“包毛巾/拆毛巾”。
  - 洗头视觉读取 `CustomerPhysicalStateSnapshot`。
  - 增加仅命令行启用的阶段3录屏入口。
- `Assets/Scripts/ServiceArchitecture/Core/ActionResultApplier.cs`
  - 增加聚合工位更新和接入期外部非动作指标同步。
- `Assets/Tests/ShampooInteractionRedesignTests.cs`
  - 将旧“干发拒绝洗发水”断言更新为阶段3要求的“真实执行并结块、可补救”。

新增：

- `Assets/Scripts/ServiceArchitecture/Adapters/Stage3WashServiceAdapter.cs`
- `Assets/Scripts/Stage3WashRuntimeAcceptance.cs`
- `Assets/Tests/ServiceArchitecture/Phase3WashIntegrationTests.cs`
- `Tools/encode_png_sequence.swift`
- `Docs/PHASE3_IMPLEMENTATION_REPORT.md`
- 上述Assets文件对应的Unity `.meta` 文件
- `artifacts/phase3-recordings/A/normal-wash.mp4`
- `artifacts/phase3-recordings/B/dry-shampoo-recovery.mp4`
- `artifacts/phase3-recordings/C/foamy-towel-recovery.mp4`
- `artifacts/phase3-recordings/D/foamy-transfer.mp4`
- 每条录屏对应的 `report.json` 和原始PNG帧。

构建产物：

- `Builds/HairSalonDemo.app` 已用阶段3代码重新构建。

未修改Scene、Prefab或美术资源。

## 3. 新旧调用链对比

### 旧花洒/洗发水链

```text
工具栏点击
→ ResolveWashToolSelection
→ 可能立即ApplyShampoo / ApplyExtraService并写旧字段
→ BeginWashAction再次写WashStage/HairWet
→ TickActiveServiceAction完成后再次写HairWet/ShampooApplied/WashStage
```

旧链的问题是“选择工具”和“动作完成”都可能写业务状态，无法保证原子提交。

### 新花洒/洗发水链

```text
工具栏点击
→ 只记录PlayerContext选择，不改业务状态
→ BeginWashAction
→ Stage3WashServiceAdapter.BeginTimedAction
→ InteractionContext.BeginAction生成ActionToken
→ Hold累计真实Elapsed
→ Tick完成或Cancel中断
→ ActionResolver.Resolve
→ ActionResult
→ ActionResultApplier.Apply
→ PhysicalStateRevision++
→ 单向更新旧显示投影
→ CustomerChanged刷新现有UI
```

中断花洒/洗发水会按真实Elapsed提交部分湿度、泡沫增加或泡沫减少。

### 旧毛巾链

```text
按钮
→ PerformQuickAction / CompleteTowelWrap
→ 直接写TowelWrapped和WashStage
```

### 新毛巾链

```text
包/拆毛巾按钮
→ Stage3WashServiceAdapter.ExecuteQuickAction
→ 新ActionToken
→ ActionResolver.Resolve
→ ActionResultApplier.Apply
→ 原子提交IsTowelWrapped/TowelContamination/TowelCondition
→ 单向显示投影
```

泡沫包毛巾会真实形成 `TowelContamination.Foam`；在洗头床即可拆除，拆除后头发泡沫保留。

### 新移动链

```text
Assign
→ 旧工位释放、设置新CustomerModel.Station
→ Stage3WashServiceAdapter.MovementStarted
→ ContextVersion递增并丢弃旧Token
→ CustomerServiceState更新动作验证工位并标记IsMoving
→ CustomerPhysicalState不保存工位、不重建、不清空
→ 正常进入旧移动/寻路链
→ 到站时StationArrived再次递增ContextVersion并清除IsMoving
```

因此泡沫、洗发水形态、毛巾污染、物理Revision和History会跨工位保留。

## 4. 录屏测试

录屏由重新构建的正式Mac Player运行；驱动调用正式 `SalonGameModel` 入口，不直接修改领域状态。
每条流程都生成MP4、原始帧和最终领域状态JSON。

### A. 正常洗头

- 文件：`artifacts/phase3-recordings/A/normal-wash.mp4`
- 结果：通过。
- 最终：Wetness=1、Foam=0、Shampoo=None、干净毛巾已包、Revision=4、History=4、洗头步骤推进。

### B. 干发上洗发水 → 结块 → 补救

- 文件：`artifacts/phase3-recordings/B/dry-shampoo-recovery.mp4`
- 结果：通过。
- 路径：干发洗发水形成ClumpedOnDryHair → 花洒打湿 → 重新揉洗形成Normal泡沫。
- 最终：Wetness=1、Foam=1、Shampoo=Normal、Revision=3、History=3。

### C. 泡沫包毛巾 → 拆毛巾 → 补救

- 文件：`artifacts/phase3-recordings/C/foamy-towel-recovery.mp4`
- 结果：通过。
- 路径：泡沫包毛巾形成Foam污染 → 洗头床拆毛巾且泡沫保留 → 花洒冲净。
- 最终：Foam=0、Shampoo=None、毛巾已拆、历史污染=Foam、Revision=5、History=5。

### D. 泡沫顾客移动到剪发区

- 文件：`artifacts/phase3-recordings/D/foamy-transfer.mp4`
- 结果：通过。
- 最终：Station=1、Foam=1、Shampoo=Normal、Revision=2、History=2，移动没有清理物理状态。

四个运行时报告：

- `artifacts/phase3-recordings/A/report.json`
- `artifacts/phase3-recordings/B/report.json`
- `artifacts/phase3-recordings/C/report.json`
- `artifacts/phase3-recordings/D/report.json`

## 5. 测试结果

阶段3新增测试命令：

```bash
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/kker/Documents/ChatGPT/game2/unity-hair-salon" \
  -runTests -testPlatform EditMode \
  -testFilter "Phase3WashIntegrationTests" \
  -testResults "/tmp/phase3-final.xml"
```

结果：8通过，0失败，0跳过。

完整回归：

```bash
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics \
  -projectPath "/Users/kker/Documents/ChatGPT/game2/unity-hair-salon" \
  -runTests -testPlatform EditMode \
  -testResults "/tmp/phase3-all-final.xml"
```

结果：324通过，0失败，0跳过。C#编译警告和IDE诊断均为0。

正式Mac Player构建成功，四条录屏进程均以退出码0结束。

## 6. 无旧逻辑双写确认

正式UI调用链中：

- `ResolveWashToolSelection`不再由花洒/洗发水按钮调用。
- `BeginWashAction`只创建领域Token和旧UI所需的临时Hold状态，不写物理结果。
- `TickActiveServiceAction`对花洒/洗发水/拆毛巾只调用Resolver和Applier，不再直接写
  `HairWet`、`ShampooApplied`、`TowelWrapped`或完成态 `WashStage`。
- `PerformQuickAction`对包/拆毛巾只通过Resolver和Applier写物理状态。
- `CustomerPhysicalState`是洗头/毛巾物理状态唯一权威。
- `HairWet`、`ShampooApplied`、`TowelWrapped`和 `WashStage`保留给旧UI、教程和旧测试，
  但正式新链中只由 `Stage3WashServiceAdapter.Project` 从提交后的Snapshot单向更新。
- 移动只更新工位与ContextVersion，不复制或重建物理状态。

旧 `ResolveWashToolSelection`、`ApplyShampoo`、`CompleteTowelWrap` 等兼容代码按要求保留，
但正式阶段3按钮链已不可达这些旧写入路径。

剪发、吹发、结算和教程仍走原链，没有接入Resolver，也没有写阶段3ActionHistory。

阶段3到此停止，等待明确批准后才进入阶段4。
