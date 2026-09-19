# STATE_OWNERSHIP.md — 顾客状态所有权定义

> 生成时间：2026-09-18（Asia/Shanghai）
> 目的：当前多个对象同时描述「顾客现在怎么样」，本文件为每一种业务事实指定唯一权威拥有者。
> 本轮只做定义与最小必要修正，**不重写状态机**；需要大规模重构的部分记入 `TECH_DEBT_NEXT.md`。

---

## 0. 问题陈述

当前至少 7 个位置同时描述同一个顾客：

```text
CustomerModel.State            （生命周期阶段）
CustomerModel.Step             （订单第几步）
CustomerModel.WashStage        （洗头阶段，是物理状态的投影）
CustomerModel.AttentionState   （是否被关注/正在操作）
WorkstationModel.State         （工位阶段）
ServiceProgress                （领域里程碑完成状态）
CustomerPhysicalState          （头发/泡沫/毛巾物理事实）
CustomerServiceState           （上面几项 + 订单 + 指标的聚合）
Stage3WashServiceAdapter.Session（每个顾客的会话容器）
SalonCustomerView / 各种 View  （位置、是否到达、进度条、动画）
```

`CURRENT_ARCHITECTURE.md` 已经指出这是核心风险。本文件把它拆成可裁决的条款。

---

## 1. 逐项裁决

### 1.1 `CustomerModel.State`

- **负责**：顾客在理发店中的**生命周期阶段**（宏观流程位置）。
- **权威范围**：`Entering / Waiting / MovingToStation / Serving / Finished / Leaving / Exited` 七个值。
- **唯一写入者**：`SalonGameModel`（`ChangeState`、`ConfirmStationArrival`、`TryFinalizeCompletedOrder`、`ProcessPatienceLeave`、`ForceCloseRemainingCustomers`）。
- **不负责**：服务进度、物理状态、订单完成度、玩家是否正在操作。
- **已知违规**：`ForceCloseRemainingCustomers`（L1496）直接赋值 `customer.State = CustomerState.Exited` 而不走 `ChangeState`，绕过了队列清理与事件通知。→ 记入技术债，**本轮不改**（改动会影响 Day 结算测试）。

### 1.2 `CustomerModel.Step`

- **负责**：订单需求列表 `Needs` 的**当前索引**，即「顾客正在做订单里的第几项服务」。
- **权威范围**：`0 .. Needs.Count`。
- **唯一写入者**：`SalonGameModel.CompleteCurrentStep`（`Step++`）、`UpdateProcessingService`、`ResolveDyeCleanup`。
- **不负责**：单项服务的内部进度（那是 `ServiceProgress`），也不负责物理完成度。
- **与其它状态的关系**：`CurrentNeed => Needs[Step]`。`IsComplete => Step >= Needs.Count` 是**兼容层语义**，与领域层的 `OrderRequirementsCompleted` 并列存在，二者不同步时以领域层为准（见 §2.3）。

### 1.3 `CustomerModel.WashStage`

- **负责**：**无**。它是 `CustomerPhysicalState` 的**只读投影**，用于旧 UI 与旧代码兼容。
- **唯一写入者**：`Stage3WashServiceAdapter.Project`（L513-533）由物理状态推导。
- **已知违规**：
  1. `SalonGameModel.ApplyShampoo`（L1147-1157）直接写 `ShampooApplied` / `WashStage`，**绕过领域层**；
  2. `SalonGameModel.UpdateServiceStage`（L1983-2009）读 `WashStage` 并据其推进后台逻辑 —— 把投影当成了输入；
  3. `PrepareCurrentStep`（L1971-1972）直接写 `WashStage = Dry`。
- **本轮处理**：不删除该字段（大量 UI 与测试依赖），但在 `STATE_OWNERSHIP` 中明确它是投影；新增代码一律从物理状态推导，不再新增对它的直接写入。

### 1.4 `WorkstationModel.State`

- **负责**：**工位**被占用的阶段，不是顾客状态。
- **权威范围**：`Available / Reserved / CustomerEnRoute / AwaitingService / AwaitingTransfer / InService / Rework / Completed`。
- **唯一写入者**：`SalonGameModel`（`Assign`、`SetWorkstationState`、`ReleaseWorkstation`、`ConfirmStationArrival`）。
- **不负责**：顾客做到哪一步、服务是否完成。
- **风险点**：`SetWorkstationState` 只在 `CurrentCustomerId == customer.Id` 时生效；若工位被释放后仍有延迟调用，状态会被静默丢弃。这是「看起来状态没更新」的常见来源。

### 1.5 `ServiceProgress`

- **负责**：**领域层服务里程碑的完成事实**（`WetHairApplied / Shampooed / RinseClean / CleanTowelApplied / Untoweled / ScissorsCompleted / HairDry / DyeCompleted / PermCompleted`）。
- **权威范围**：每个 milestone 的 `EverCompleted` 与 `SatisfiedNow`。
- **唯一写入者**：`ActionResultApplier`（经 `ActionResolver` 产出的 `MilestoneEvent`），以及 `Stage3WashServiceAdapter.CompleteExternalServiceMilestone`（染/烫外部里程碑）。
- **不负责**：订单要求哪些里程碑（那是 `OrderDefinition`），也不负责顾客生命周期。
- **结论**：这是**权威真相源**。`CustomerModel.Step` / `WashStage` 都是它的投影或兼容层。

### 1.6 `CustomerPhysicalState`

- **负责**：顾客头发的**物理事实**：`Wetness / FoamAmount / ShampooState / IsTowelWrapped / TowelContamination / TowelCondition / HairLengthDeviation / HaircutProgress / PhysicalStateRevision`。
- **唯一写入者**：`ActionResultApplier`（按 `PhysicalStateDelta` 原子更新）。
- **不负责**：任何时间、耐心、满意度、生命周期。
- **结论**：这是**权威真相源**，且带 `PhysicalStateRevision` 版本号用于令牌校验，是本仓库设计最干净的一处。

### 1.7 `CustomerServiceState`

- **负责**：把 `CustomerPhysicalState` + `OrderDefinition` + `ServiceProgress` + `CustomerMetricsState` 聚合成一次服务的**完整领域快照**，并持有 `IsMoving` 与出口就绪结果。
- **权威范围**：仅服务领域内部。
- **结论**：它是**聚合容器**，不是新的真相源；它内部的每个成员仍由各自的拥有者写入。
- **风险点**：`CustomerMetricsState`（满意度/耐心）与 `CustomerModel.Satisfaction / Patience` 双向同步，靠 `SynchronizeExternalMetrics` 在每次动作前对齐。这是当前最脆弱的一处同步边界 —— 模型在 Tick 里持续扣耐心，领域层只在动作开始时同步一次，二者存在时间窗偏差。

### 1.8 `Stage3WashServiceAdapter`：状态拥有者还是桥接层？

**裁决：桥接层（Bridge / Synchronization Boundary），不是业务真相源。**

理由：

1. 它不定义任何规则 —— 规则在 `ActionResolver`，原子写入在 `ActionResultApplier`。
2. 它只持有**会话容器**（`Session`：`CustomerServiceState` + `InteractionContext` + `History` + 执行控制器 + 少量流程开关）。
3. 它唯一的「写」动作是 `Project(customer)`：把领域快照单向投影回 `CustomerModel`。

因此它的定位应该是：

```text
允许：持有会话生命周期（Register / ResetForNextDay）
允许：创建 ActionToken、编排 Resolve → Apply → Project
允许：保存纯流程开关（UsesContinuousServiceExecution、PreserveActiveActionOnFocus）
禁止：自行判定服务是否完成、自行修改物理状态、自行推进订单步数
```

**当前越界之处（记入技术债，本轮不改）**：

- `ProcessFoamBurstRecovery`（L374-415）直接在适配器里创建 `FunnyDisasterEvent` / `RecoveryRequirement` 并写 `CustomerModel`，属于业务决策，理应上移到 `SalonGameModel`。
- `CreateOrder`（L578-625）根据 `customer.Needs` 生成 `OrderDefinition`，等于让适配器参与了「订单真相」的构建；而 `CustomerModel.Needs` 才是订单的兼容层真相。

### 1.9 View（`SalonCustomerView` / `CustomerUIRootView` / `*View`）

**允许保存**：

- 游戏对象位置、朝向、缩放、动画状态；
- 是否到达移动目标点（`IsAtMovementDestination`）；
- 上一次渲染用的缓存值，用于避免每帧刷新；
- 纯表现性插值与颜色。

**禁止成为业务事实来源**：

- 禁止由 View 决定服务时长（当前违规：`SalonDemo.Mobile.cs` L428 由 View 侧计算 `_mobileWorkDuration`）；
- 禁止由 View 决定玩家是否忙碌（当前违规：`_mobileWorkingView` 非空即锁死输入，与模型 `PlayerBusy` 并存）；
- 禁止由 View 决定是否生成/消费付款；
- 禁止 View 反向写 `CustomerModel` 的业务字段。

---

## 2. Single Owner Rule（单一拥有者规则）

### 规则表述

> **每一种业务事实，只能有一个 authoritative owner。**
> 其它对象只能：① 投影（只读派生）、② 缓存、③ View、④ Compatibility layer。
> 兼容层必须显式标注，并且不得反向写入权威源。

### 2.1 所有权表

| 业务事实 | 权威拥有者 | 投影 / 兼容层 | 备注 |
|---|---|---|---|
| 顾客生命周期阶段 | `CustomerModel.State` | View 位置与动画 | 仅 `SalonGameModel` 可写 |
| 订单需求列表 | `CustomerModel.Needs` | `OrderDefinition` | `OrderDefinition` 由 `Needs` 派生 |
| 订单当前第几步 | `CustomerModel.Step` | `CurrentNeed` / `NextNeed` | 与领域 milestone 双轨，见 §2.3 |
| 服务里程碑完成 | **`ServiceProgress`** | `CustomerModel.Step`、`WashStage` | 领域层优先 |
| 头发物理事实 | **`CustomerPhysicalState`** | `HairWet` / `ShampooApplied` / `TowelWrapped` / `WashStage` | 物理层优先 |
| 工位占用阶段 | `WorkstationModel.State` | 工位高亮、选中板 | 仅 `SalonGameModel` 可写 |
| 顾客耐心 | `CustomerModel.Patience`（模型 Tick 拥有） | `CustomerMetricsState.Patience` | 双向同步，见 §2.4 |
| 顾客满意度 | `CustomerModel.Satisfaction` | `CustomerMetricsState.Satisfaction` | 双向同步，见 §2.4 |
| 玩家是否忙碌 | **`SalonGameModel.PlayerBusy`** | `_mobileWorkingView`、按钮可交互性 | View 不得反客为主 |
| 后台任务进度 | `CustomerModel.BackgroundTask` | 进度条颜色与文案 | 单一模型字段 |
| 付款是否存在 | `SalonPaymentModel.Drops` | `CoinPileView` | View 只是表现 |
| 事故与补救 | `CustomerModel.AccidentSeverity` / `RecoveryRequirements` | 气泡表情、Toast | 适配器不得自行创建 |

### 2.2 允许的写入方向

```text
输入层（移动按钮 / 桌面工具栏）
        │  只允许调用模型方法
        ▼
SalonGameModel                 ← 生命周期 / 工位 / 耐心 / 步数 的权威
        │
        ▼
Stage3WashServiceAdapter       ← 桥接：建 token、编排、投影
        │
        ▼
ActionResolver（纯函数）  →  ActionResultApplier（原子写）
        │
        ▼
CustomerPhysicalState / ServiceProgress   ← 物理与里程碑的权威
        │
        ▼
Adapter.Project(customer)      ← 单向投影回 CustomerModel
        │
        ▼
View（只读渲染）
```

**禁止的反向写入**：View → Model 业务字段；Adapter → 自行判定业务结果；投影字段 → 被当作输入使用。

### 2.3 双轨进度：本轮的临时处置

`CustomerModel.Step`（兼容层）与 `ServiceProgress`（领域层）目前都能表达「做到哪一步」。本轮**不合并**，但约定：

- **完成判定以领域层为准**：`TryFinalizeCompletedOrder` 已经通过 `ExitReadiness` 查询领域层，这个方向保持不动。
- **新增逻辑一律查领域层**：本轮新增的洗头后台等待，用 `CustomerPhysicalState` + `BackgroundTask` 判定，不再给 `WashStage` 增加新的语义分支。
- **兼容层只在旧路径读取**。

### 2.4 耐心/满意度的双向同步：已知时间窗

`CustomerServiceState.Metrics` 与 `CustomerModel.Patience/Satisfaction` 在每次领域动作开始时通过 `SynchronizeExternalMetrics` 对齐；之后模型 Tick 会继续独立扣减，直到下一次动作才再次对齐。

**影响**：领域动作内部看到的耐心可能比模型略旧；动作施加的满意度变化会在 `Project` 时回写模型。

**本轮处置**：接受这个行为（它已被 595 条测试锁定），并在新增测试中不依赖「动作内部的耐心值与模型完全相等」。记入 `TECH_DEBT_NEXT.md`。

---

## 3. 本轮通过小修改就能解决的重复状态

| # | 问题 | 本轮是否改 |
|---|---|---|
| 1 | 泡沫等待的背景任务没有任何启动方，导致 `UpdateServiceStage` 的 `Foamy` 分支是死代码 | **改**：在洗发动作完成时统一启动 `BackgroundTask`，激活既有分支（Phase 2） |
| 2 | `WashStage.FoamWait / ReadyToRinse / Foamy` 三个名字同一个值 | **不改枚举**（会影响大量测试）；新增代码改用 `BackgroundTask.Elapsed` 判定，并在文档中标注 |
| 3 | 移动剪发时长由 View 计算 | **改**：改为由模型/配置提供，View 只读取（Phase 2） |
| 4 | 自动吹发在桌面 UI 有购买门槛、移动没有 | **改**：统一以模型 `AutoBlowAvailable` 为准（Phase 2） |

## 4. 明确进入技术债、本轮不动

1. `CustomerModel` 与 `CustomerServiceState` 的耐心/满意度双向同步（§2.4）。
2. `Stage3WashServiceAdapter` 越界创建事故与订单（§1.8）。
3. `ApplyShampoo` / `PrepareCurrentStep` 直接写投影字段 `WashStage`（§1.3）。
4. `ForceCloseRemainingCustomers` 绕过 `ChangeState` 直接赋值终态（§1.1）。
5. `CustomerModel.Step` 与 `ServiceProgress` 的双轨进度（§2.3）。
6. `SalonTool` 与 `ServiceTool` 两套枚举并存、映射分散在多处。

以上详见 `TECH_DEBT_NEXT.md`。
