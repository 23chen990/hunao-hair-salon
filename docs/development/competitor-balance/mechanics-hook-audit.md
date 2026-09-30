# 《胡闹理发店》扩展玩法接入审计

审计范围：现有 Unity 运行时代码、Manifest 与 EditMode 测试。本文只描述接入点和缺口，不批准或实现任何候选玩法。

> 2026-09-30 之后的变化：口碑已对玩家开放（`ReputationSystemEnabled = true`，手机档使用 `SalonMobileDayConfig.CreateReputationConfig()`，每星 ±12.5% 客流），客流改为 `TrafficWaves` 分段并在 `LastAdmissionProgress` 后停止进客。下文关于“声望关闭”的描述是审计当时的状态；行号也可能已移动。当前进度见 [基础问题清单](../basic-issues/README.md)。

## 结论摘要

- 客流已经有两套边界：`BusinessDaySystem.DayConfig` 的通用日流，以及 `SalonMobileDayConfig.ApplyForDay` 的手机日配置。新增外部客流倍率最适合成为 `Evaluate` 的可选参数或 `DayConfig` 的默认值，默认保持 `1`，这样旧调用和旧测试不变（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:65-99,215-247`；`unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs:136-180`）。
- 宣传拉客不会线性转化为订单：达到等候阈值后刷新间隔乘 `2.4`（手机配置实际为 `2.1`），等候容量或并发硬上限又会直接拒绝刷新；宣传若只提高倍率，主要效果可能变成“更快撞上过载”，而非更多完成单（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:225-247`；`unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs:150-159`）。
- 口碑结算链已经存在，但核心 `SalonGameModel.ReputationSystemEnabled` 明确为 `false`；`DayStats` 只按满意结果/未服务/未完成/弃客计数，没有顾客级评价明细或特殊权重（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:255-260`；`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:255-345,361-399`）。
- `Marketing` 只出现在投资枚举中，没有发现消费、配置、UI 或保存字段的使用；闭店后目前是 `Result -> ClosedManagement -> 下一天`，已实现的购买是场景内靠近施工点持续扣金币，不是新菜单（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:10-22,520-538`；`unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs:500-639`）。
- 工位可锁定/解锁，但当前 `SetWorkstationAvailability` 拒绝“已有顾客时锁定”，因此停电/故障若要影响在用工位，必须先定义转移、暂停还是失败语义；自动吹风和泡沫任务目前随 `SalonGameModel.Tick` 无条件推进（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:10-20,350-365,399-411`）。

## 1. 客流：宣传、热度、季节倍率

### 现有配置和运行链

通用 `DayConfig` 配置营业时长、最小/最大刷新间隔、并发上限、等候过载阈值、等候容量、压力阶段强度、Rush 参数，以及声望客流倍率上下限。默认值包括 `MaxConcurrentCustomers=6`、`OverloadWaitingThreshold=3`、`OverloadSlowdownMultiplier=2.4`、声望每星 `5%`、客流倍率钳制 `0.9–1.1`（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:65-99`）。

手机配置按天写入目标单数、营业/收尾时长、并发上限 `6`、等候容量 `4`、阈值 `3`、刷新区间 `5.5–8.5` 秒、阶段强度和 Rush；第 1/2/3+ 天目标单数为 `3/4/5`，订单序列也按天和进度变化（`unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs:50-59,136-180`）。

实际运行由 `SalonDemo.MaintainCustomerFlow` 每帧调用 `CustomerTrafficDirector.Evaluate`，输入营业进度、当前活跃/等候/占用/愤怒快照、随机间隔值和当前声望；只有 `ShouldSpawn` 且 `SalonGameModel.Spawn` 成功才产生顾客（`unity-hair-salon/Assets/Scripts/SalonDemo.cs:1195-1219`；`unity-hair-salon/Assets/Scripts/SalonDemo.cs:1221-1240`）。

### 能否加外部倍率而不影响旧测试

可以，推荐在 `CustomerTrafficDirector.Evaluate` 增加可选参数，例如 `externalTrafficMultiplier=1f`，或在 `DayConfig` 增加默认值为 `1f` 的字段，并把它乘入 `intensity`。旧调用不传参时行为不变；旧测试覆盖压力阶段、Rush、过载和硬上限，默认值为 `1` 可维持原断言（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:215-247`；`unity-hair-salon/Assets/Tests/Phase7BusinessDayTests.cs:90-132`）。

但“按天配置”目前是 `ApplyForDay` 直接覆盖 `DayConfig`，没有外部事件/季节层。因此正式接入需要一个明确的数据来源：日配置字段、当天事件结果，或存档中的持续倍率；不能仅在 UI 里临时改刷新计时器（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:436-445`；`unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs:136-180`）。

### 过载保护如何抵消宣传

`Evaluate` 先把声望倍率乘入强度，再计算基础刷新间隔；若等候过载，间隔再乘 `OverloadSlowdownMultiplier`；等候达到容量则 `ShouldSpawn=false`，达到并发硬上限也不能刷新。手机配置允许阈值过载时继续刷，但容量 `4` 仍是硬门槛（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:225-247`；`unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs:150-159`）。

因此宣传的设计必须选择“增加有效容量/延后过载”或“只增加进入机会”。若只加倍率，过载乘数会吃掉部分收益，容量时完全没有边际收益；测试已经固定“三人等候仍可刷、四人等候停止、过载间隔变长”（`unity-hair-salon/Assets/Tests/MobileDayChallengeTests.cs:220-238`）。

## 2. 口碑/热度

### 已有结算、存档、显示

`ShopReputationModel` 默认初始 `3` 星，范围 `1–5`；每日结算从 `DayStats` 的开心、正常、不满意、非常不满意、闭店未服务、未完成、未开始服务弃客计数计算 raw delta，再钳制每日最多增长 `+0.1`、损失 `-0.2`（配置默认值实际为 `Happy +.04`、`Unhappy -.05`、`VeryUnhappy -.1` 等）（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:28-40,361-399`）。

闭店结果展示 `ReputationBefore -> ReputationAfter`；存档有 `ReputationStars`，恢复时重新钳制到 `1–5`（`unity-hair-salon/Assets/Scripts/SalonDemo.cs:960-1008`；`unity-hair-salon/Assets/Scripts/SalonProgressSave.cs:20-35,100-105`）。

但核心常量 `ReputationSystemEnabled=false`，审计到的运行入口仍需确认产品模式是否实际启用这条链；不能把“代码存在”当成“游戏体验已启用”（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:255-260`）。

主任务复核（2026-09-30）：运行入口已经启用这条链。`SalonDemo.cs:966` 每天打烊都会调用 `FinalizeDayReputation()`，`SalonDemo.cs:1201-1202` 把星级传给客流导演，`SalonDemo.Mobile.cs:792` 会存档星级；上面的常量只被一条测试引用。实际缺口是玩家看不见、幅度只有 ±10%。

### 缺口和特殊顾客权重

没有顾客级声望评价记录：`DayStats.RecordCustomerSnapshot` 只按顾客 ID 去重累计完成结果、错误工位和事故严重度，`ApplyDayResult` 只读取汇总整数；没有评价文本、评价来源、权重或某个特殊顾客字段（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:255-345,377-399`）。

可以挂“评论家/网红权重”，但需要新增 `CustomerModel` 的评价权重或评价类型，并让 `DayStats` 保存顾客结果明细或加权桶；若只把一个顾客计数复制多次，会破坏每日 `±0.1/−0.2` 上限的可解释性。规模取决于是否要显示评价详情：仅权重为中等，含可见评价内容则为大（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:136-250`；`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:377-399`）。

## 3. 宣传投入和闭店经营

### Marketing 当前状态

`ManagementInvestmentType` 包含 `Marketing`，但全仓脚本检索没有找到其他使用点；现有交易记录只在自动吹风购买处写入 `Equipment`，没有 Marketing 产品、价格、效果或存档字段（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:10-22`；`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:1699-1727`；`unity-hair-salon/Assets/Scripts/SalonProgressSave.cs:20-35`）。

### 闭店经营形态

日状态是 `Result`、`ClosedManagement`，再 `PrepareNextDay`；当前已实现购买流程是玩家靠近世界施工点后持续计算金币、钱包扣款、施工点累计支付，完成后开放补给架或第二理发位。它没有新增经营页面，施工点的视觉和位置由 `SalonDemo.Mobile` 创建（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:404-538`；`unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs:500-639`）。

符合既有约束的宣传接入点是“闭店期间新增一个稳定 ID 的场景施工/投放点”，复用 `SalonProximityPurchasePadModel` 的 `CalculatePayment -> TrySpend -> ApplyPayment` 原子流程；完成后写入当天或下一天的倍率，并在离开施工点时保存检查点。不要新增菜单、货币或独立购买页（`unity-hair-salon/Assets/Scripts/SalonProximityPurchasePadModel.cs:15-25,70-115`；`unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs:520-559`）。

## 4. 特殊顾客

### CustomerModel 已有字段

顾客已有订单 `Needs/Step`，耐心 `Patience/MaxPatience`，满意度，状态/情绪，当前工位，等待/服务时间，服务反馈和结果，错误工位/事故，外观由场景视图另行生成。已有标记包括服务是否接触、注意状态、当前服务动作、自动吹风状态、后台任务和泡沫灾难事件（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:136-250`）。

单顾客耐心倍率不能直接配置：耐心扣减使用全局 `ExperienceProfile.PatienceDecayMultiplier`，但 `PatienceDrainMultiplier` 已是按顾客分支的集中入口，因此新增顾客字段并在该入口乘上倍率是较小的模型改动（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:2520-2570`）。小费当前按全局奖励配置、耐心比例、事故和错误工位计算；要做小费倍率需在 `CreatePayment` 或支付模型增加顾客属性（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:2637-2650`）。

满意度权重也不存在。当前满意度是顾客自身数值，结果分类由服务完成条件决定；若要特殊顾客影响口碑，需要同时区分“顾客满意度”“日结评价权重”，避免把权重误用于玩家即时反馈（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:2593-2620`；`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:330-345`）。

### UI 可见性

手机队列卡只显示顾客编号/状态、订单图标和耐心条；`SalonMobileQueueView.QueueCard.Refresh` 没有读取特殊标记、头像类型或紧急图标。世界气泡/情绪视图也初始化现有订单和情绪数据，没有通用特殊标签槽位。因此“急单/评论家/明星同款”至少需要模型字段、队列卡显示规则和世界顾客视图显示规则（`unity-hair-salon/Assets/Scripts/SalonMobileQueueView.cs:70-100,150-239`；`unity-hair-salon/Assets/Scripts/SalonDemo.cs:1140-1165`）。

## 5. 工位机关和设备暂停

工位有 `Locked/Available/Reserved/.../Completed` 状态，`IsUsable` 只判断不是 `Locked`；手机开局通过 `ConfigureWorkstationAvailability` 锁住第二剪发位、第二洗发位和烫发位，购买完成后解锁第二剪发位（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:10-20,109-121,350-365`；`unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs:620-635`）。

临时禁用“空闲工位”可以复用 `SetWorkstationAvailability(id,false)`；但占用工位时会直接返回 `false`，所以停电/故障不能只切换一个 bool 就覆盖在服务顾客。需要新增故障状态、可恢复规则、正在服务中的行为，以及分配/自动分配/操作入口对故障状态的判断（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:355-365,730-748,860-879`）。

自动吹风和洗发泡沫都属于模型 Tick 推进的后台时间：`SalonGameModel.Tick` 每帧调用 `BackgroundTask.Tick` 和服务/处理更新；没有设备暂停接口。停电若要求“吹风/洗发不可用但计时冻结”，需要把设备暂停状态传入这些更新路径；若只禁止新操作，改动较小但不符合“后台任务暂停”的完整语义（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:399-411`；`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:1824-1895`）。

## 6. 场景机关、湿滑和临时障碍

当前玩家移动路线 `SalonPlayerRoute.Build` 只接收已解析的剪发工位布局，把工位碰撞矩形作为障碍节点做可见性寻路；没有通用区域修正速度、湿滑系数、临时障碍注册表或动态碰撞体系统（`unity-hair-salon/Assets/Scripts/SalonPlayerRoute.cs:1-73`）。

场景代码会给顾客/货币等对象直接添加 Unity `BoxCollider`，资产候选接入也会读取 Manifest 的碰撞配置，但这不是可复用的“临时障碍物系统”。因此湿滑区域需要移动控制器增加区域采样/速度系数，漏水/货架/熊孩子需要运行时对象生命周期、碰撞、排序和路线重建（`unity-hair-salon/Assets/Scripts/SalonDemo.cs:1140-1155`；`unity-hair-salon/Assets/Scripts/AssetPipeline/CandidateAssetValidation.cs:73-90,169-193`）。

新增世界物件必须走 `Assets/Resources/AssetPipeline/asset-manifest.json` 的 `AssetDefinition` 合同：稳定 `Id`、类型/资源路径/状态、方向和默认朝向、`Pivot`、`DesiredWorldSize`、`ScalePolicy`、`ImportProfile`、`Footprint`、`Collision`、`Shadow`、交互锚点/必需锚点、`Sorting`；候选资产还要 `CandidateValidation` 和来源/哈希字段。Manifest 测试覆盖重复 ID、非法方向、占地/锚点、资源、pivot、候选场景验证和世界缩放（`unity-hair-salon/Assets/Scripts/AssetPipeline/AssetManifest.cs:30-130`；`unity-hair-salon/Assets/Tests/AssetPipelineManifestTests.cs:20-160`）。

## 7. 事件框架、DayStats 和存档

没有发现通用“当天事件/随机事件/预告/剧情气泡”框架。现有 `FunnyDisasterEvent` 是顾客服务链里的泡沫爆发事件，挂在单个 `CustomerModel.ActiveDisasterEvents`，并由洗发适配器处理；它不是当天事件调度器，也没有事件选择、持续时间或预告层（`unity-hair-salon/Assets/Scripts/SalonGameModel.cs:199-230`；`unity-hair-salon/Assets/Scripts/ServiceArchitecture/Adapters/Stage3WashServiceAdapter.cs:390-455`）。

`DayStats` 已有每日收入、订单结果、未服务/未完成、错误工位、事故和声望前后值；没有事件 ID、是否触发、处理结果、事件损失或恢复奖励字段。新增机关应在 `DayStats` 增加可序列化的事件统计或明确计数方法，并在闭店前记录；不要把事件结果偷偷塞入顾客满意度而丢失可追踪性（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:255-355`）。

向后兼容可沿用 `SalonProgressSave`：新增字段先给安全默认值；旧 JSON 缺字段时由 `JsonUtility` 得到默认值，再用原始 JSON 是否包含字段来做一次性归一化；保持 `CurrentSchemaVersion` 和未来版本拒绝策略，不把未知未来存档降级覆盖（`unity-hair-salon/Assets/Scripts/SalonProgressSave.cs:10-35,50-120,270-360`）。持续事件效果应保存稳定 ID/剩余天数或倍率，而不是保存 Unity 场景对象引用。

## 8. 测试覆盖和新增测试位置

现有覆盖可分为：

- 客流/日状态/Rush/过载/硬上限/DayStats/闭店：`Phase7BusinessDayTests`，尤其是刷新决策和 `DayStats`（`unity-hair-salon/Assets/Tests/Phase7BusinessDayTests.cs:90-170`）。
- 手机目标单数、逐日订单、并发/等候容量、过载和闭店重试：`MobileDayChallengeTests`（`unity-hair-salon/Assets/Tests/MobileDayChallengeTests.cs:40-58,80-110,140-290`）。
- 顾客耐心、服务链、付款、工位占用和状态转移：`SalonGameModelTests`（`unity-hair-salon/Assets/Tests/SalonGameModelTests.cs:20-160`）。
- 存档字段、旧 JSON 缺字段、旧购买标记补全付费额、未来 schema 和无效数据：`SalonProgressSaveTests`（`unity-hair-salon/Assets/Tests/SalonProgressSaveTests.cs:20-180`）。
- 施工点空间扣费、购买恢复和支付原子性：`SalonProximityPurchasePadModelTests`、`SalonPurchasePadRuntimeIntegrationTests`、`SalonR1CheckpointRollbackIntegrationTests`（测试文件位于 `unity-hair-salon/Assets/Tests/`）。
- Manifest 字段和资源校验：`AssetPipelineManifestTests`（`unity-hair-salon/Assets/Tests/AssetPipelineManifestTests.cs:20-160`）。

建议新增测试：

1. 客流倍率、倍率默认值、宣传与过载/容量叠加：扩展 `Phase7BusinessDayTests` 或新增 `TrafficModifierTests`。
2. 特殊顾客耐心/小费/口碑权重：扩展 `SalonGameModelTests` 和 `ShopSatisfactionTests`，另测 `DayStats` 的加权结算。
3. 工位故障暂停/恢复、在用工位的拒绝或迁移策略：扩展 `StationCapabilityFlowTests`、`MobileServiceRulesTests`。
4. 事件记录与重开/下一天重置：扩展 `Phase7BusinessDayTests`、`SalonMobileSettlementTests`。
5. 新世界物件：先在 `AssetPipelineManifestTests` 增加 Manifest 校验，再在对应运行时集成测试验证碰撞、锚点、排序和主场景接入。

## 候选玩法汇总

| 候选玩法 | 可复用的现有系统 | 必须新增的东西 | 新世界资产/Manifest | 是否触碰主场景 `SalonDemo*.cs` | 规模 | 主要技术风险 |
|---|---|---|---|---|---|---|
| 口碑/热度影响客流 | `ShopReputationModel`、`DayStats`、`CustomerTrafficDirector`、`ReputationStars` 存档 | 顾客评价明细/特殊权重、启用开关、客流外部倍率来源和显示口径 | 否；若做网红角色则是 | 是，接入日结算、存档和刷新 | 中 | 现有核心常量关闭；每日 ±0.1/−0.2 上限会压平强反馈；倍率会被过载保护抵消 |
| 玩家宣传投入 | `ManagementInvestmentType.Marketing`、钱包、`SalonProximityPurchasePadModel` | Marketing 产品/成本、持续天数或一次性倍率、交易记录、存档字段 | 是，宣传施工点必须入 Manifest | 是 | 中 | 只能扣现有金币；容量/过载可能使投入变成更快触顶 |
| 团购活动 | `DayConfig` 外部倍率思路、支付模型、DayStats | 客流倍率与单价/奖励规则、活动期间标记和结算统计 | 可否；若只做施工点则是 | 是 | 中 | “客多单价低”会同时改客流和支付，需避免与声望倍率耦合 |
| 停电/跳闸 | 工位锁定、服务入口、后台任务模型 | 可恢复故障状态、受影响设备集合、暂停/恢复 Tick 语义、电闸交互 | 是，电闸/故障提示需 Manifest | 是 | 大 | 已占用工位不能直接锁定；后台任务目前无暂停接口 |
| 热水不足 | 洗发服务链、补给模型、DayStats 事故统计 | 资源库存/消耗、缺水状态、补水交互和失败反馈 | 通常是，补水点/提示物件 | 是 | 大 | 既有洗发流程已有时序和泡沫后台任务，资源不足会影响服务链状态一致性 |
| 湿滑/漏水 | 顾客/玩家碰撞、现有路线障碍矩形 | 区域速度/转向修正、动态碰撞和清理状态 | 是，湿滑视觉/碰撞区需 Manifest | 是 | 大 | 没有区域移动修正或通用动态障碍注册表 |
| 流浪猫/熊孩子/打翻货架 | `SalonPlayerRoute` 的障碍思路、顾客/玩家碰撞 | 动态实体生命周期、碰撞、避让/阻挡规则、事件结果记录 | 是 | 是 | 大 | 现有寻路只接收静态剪发工位布局，动态障碍需重做输入边界 |
| 到货搬运 | 现有补给取放、空间锚点和施工点 | 货物状态、搬运占用、交付点、失败/超时规则 | 是，货物和交付点需 Manifest | 是 | 中 | 要与现有补给库存和移动状态并存，避免重复资源模型 |
| 限时急单 | 顾客耐心、订单序列、队列卡和日配置 | 顾客类型/时间窗、单顾客耐心倍率、UI 标记、奖励/失败规则 | 否；若有专属外观则是 | 是 | 中 | 现有耐心扣减集中但无类型字段；队列卡没有特殊标记槽位 |
| 挑剔评论家/网红顾客 | `CustomerModel` 满意度、`DayStats`、声望结算 | 顾客类型、评价权重/明细、队列/世界标记、可能的特殊结算 | 可否；外观需要 | 是 | 大 | 需要同时保持即时满意度与加权声誉的两套语义 |
| 只剪不洗老顾客 | `Needs`、订单目录、确定性日订单和服务链 | 顾客标签/订单模板、显示标签 | 否 | 小幅接入生成和 UI | 小 | 若复用现有 `Needs` 最小；仍需防止订单目录/教程假设 |
| 带娃家庭 | 顾客列表、队列、耐心和场景视图 | 多实体/陪同关系、阻挡和 UI 规则 | 通常是，外观需要 | 是 | 大 | `CustomerModel` 目前一顾客一订单，陪同者不是独立模型 |
| 明星同款潮流订单 | 订单目录、工具序列、支付和 UI 订单图标 | 新订单定义、顾客标签、奖励/评价规则、可能的视觉 | 可否 | 是 | 中 | 当前订单 picker 只允许 O001–O005（`unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs:108-155`） |
| 日历/季节/天气/竞争对手 | `MobileDayNumber`、按天配置、顶部日期/星期显示 | 日历事件配置、倍率/订单/价格效果、存档持续状态、预告显示 | 通常否；天气/竞争店视觉则是 | 是 | 大 | 当前日期显示是装饰性计算，不是事件调度器；效果会横跨客流、支付和订单 |

## 最小推荐切入顺序（仅供批准后的设计评审）

1. 先做不依赖新世界资产的“按天外部客流倍率”纯模型实验，并明确倍率与过载/容量的关系；默认 `1` 保持旧行为。
2. 再做空间施工点式宣传，复用现有支付原子性和存档兼容方式，避免新增经营页面。
3. 特殊顾客先选择“只剪不洗老顾客”这类订单模板变化；评论家/网红涉及声誉权重和 UI/存档，属于更大范围。
4. 机关优先验证“空闲工位临时锁定”；停电暂停后台任务、湿滑和动态障碍都需要先补通用状态/移动基础设施。
