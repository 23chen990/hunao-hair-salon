# 《胡闹理发店》Multiplayer-ready / Cross-platform-ready 审计报告

日期：2026-08-16  
范围：`unity-hair-salon` 当前单人版本；不实现联网、房间、账号或平台 SDK。

## 结论

当前核心顾客/工位模型不需要为双人重写。顾客的洗发阶段、后台等待、毛巾、剪发进度、耐心、满意度和服务进度都属于 `CustomerModel`；工位只保存占用顾客 ID 和工位状态。现有测试已经覆盖半成品洗发、毛巾和未完成剪发跨工位仍保留。

本轮只修复了两个会真正阻塞未来双人的底层问题：

1. 把“选中了哪个顾客、聚焦哪个工位、选了什么工具、当前本地动作”等私人状态放入每玩家独立的 `PlayerContext`，不再把 `Selected` 写在共享顾客上。
2. 保证 `CustomerId` 在整个运行会话内唯一，即使顾客已经离场也不能复用，避免延迟命令误命中新顾客。

没有引入 Transport、Relay、WebSocket、平台 SDK、账号、房间或完整 Command 框架。

## 1. 原有单玩家假设

- `SalonGameModel.SelectedCustomer` 与 `SalonGameModel.ViewState` 原来是模型级唯一状态。
- `CustomerModel.Selected` 原来把某个玩家的 UI 选中状态写进共享顾客。
- `SalonDemo` 内的 `_selectedServiceAction`、`_activeServiceView`、`HaircutInteraction.SelectedTool` 和触摸处理都只描述当前本地玩家。它们不是静态全局变量，但未来若一个进程支持本地双人，需要拆成每玩家控制器。
- 所有服务规则入口（例如 `Assign`、`BeginWashAction`、`BeginActiveOperation`、`StartManualBlow`）目前没有 `playerId`，因此尚不能表达操作所有者或处理两个同时命令。

## 2. 增加 Player2 时仍会产生问题的地方

- 顾客主动操作只有“正在操作”的业务状态，没有 `interactionOwnerPlayerId`；两个网络命令的抢占、拒绝和释放策略尚未实现。
- `BeginActiveOperation` 对已经处于 ActiveOperation 的顾客会直接返回成功，未来必须由 Host 根据玩家 ID 判定是否是同一操作人。
- 工位占用已经由统一模型判定，能拒绝第二位顾客；但 `Assign(customer, int station)` 的参数目前同时被当作列表索引和工位 ID，未来网络命令应改为先按 `StationId` 查找对象。
- 顾客生成、订单选择和客流判断仍由 `SalonDemo` 使用 `UnityEngine.Random` 驱动。未来 Host 需要拥有 session seed 和随机结果；表现层随机（音高、碎发方向）可以继续留在客户端。
- 输入与规则之间已有方法边界，但还没有可序列化的 `PlayerAction` / `GameCommand` 信封。

## 3. Customer State 是否独立于 Station

是。以下状态都在 `CustomerModel`：

- `WashStage`、`BackgroundTask`
- `TowelWrapped`
- `HairStage`、`HaircutService`
- `ActiveServiceAction`、服务计时
- `Patience`、`Satisfaction`、情绪、事故和返工状态

`WorkstationModel` 只保存 `Id`、类型、`CurrentCustomerId`、工位状态、服务点和可用工具。转移顾客时只释放旧工位并占用新工位，不重建顾客服务状态。

现有回归覆盖：`PartialWashStateTravelsWithCustomerAcrossStations`、`WrappedTowelStateSurvivesTransfersInBothDirections`、`IncompleteHaircutCanMoveToWashStationWithoutLosingProgress`。

## 4. 危险的全局 currentCustomer / currentStation

没有发现静态 `currentCustomer`、`currentStation`、`currentTool` 或 `GameManager` 单例。

仍保留 `SalonGameModel.SelectedCustomer` 和 `ViewState` 作为单人 UI 的兼容属性，但它们现在只映射到 `LocalPlayerId = 1` 的 `PlayerContext`，不再是共享世界事实。第二个玩家可同时拥有另一份选择和聚焦状态。

## 5. UI 与核心逻辑耦合程度

核心规则大部分是可测试的普通 C# 模型，`SalonDemo` 通过公开规则方法修改它，这是一个可用的边界。

耦合仍属中高：`SalonDemo` 有约 2,149 行，同时负责触摸/鼠标输入、UI、相机、顾客视图、生成节奏、随机订单、服务按钮、反馈、截图和若干调试流程。UI 按钮没有直接散改耐心或满意度，而是调用 `SalonGameModel` 规则方法；因此不需要现在拆掉整个 Demo，但真正接入 Host/Client 前应把这些调用收口到 Action Gateway。

## 6. 本轮修改

- 新增 `PlayerContext`，包含 `PlayerId`、`SelectedCustomerId`、`FocusedStationId`、`SelectedTool`、`ActiveAction` 和本地 `ViewState`。
- `SalonGameModel` 新增按 `playerId` 获取上下文、选择顾客和清理焦点的 API。
- 原有无 `playerId` API 继续代表 Player 1，现有单人 UI 无需改调用方式。
- 顾客离场、愤怒离开或关店时会清理所有玩家对该顾客的焦点，不会只清 Player 1。
- 删除共享顾客上的 `Selected` 字段；选中缩放改为比较本地 `SelectedCustomer`。
- 新增会话级已发放顾客 ID 集合，禁止活跃重复和离场后复用。
- 新增 Multiplayer readiness 回归测试。

## 7. PlayerId / PlayerContext

已新增。当前 `PlayerId` 使用正整数，Player 1 是本地兼容玩家；没有为了类型包装而增加额外序列化复杂度。等真正定义网络协议时，可再决定是否升级为强类型 ID 或带会话前缀的字符串/整数。

## 8. 稳定实体 ID

- Customer：已有 `CustomerModel.Id`，本轮增加会话级唯一性保证。
- Station：已有固定 `WorkstationModel.Id`，当前 0–5；但仍与列表索引耦合。
- MoneyDrop：已有单调递增的 `PaymentDropModel.Id`，清理当日掉落时不会重置计数器。
- Player：本轮新增 `PlayerContext.PlayerId`。
- ServiceTask：目前没有独立实体；现有服务动作都附着于 Customer，在没有需要单独重连/回放的并行任务前，不建议现在增加 ID。

## 9. Action / Command 层是否值得现在引入

概念上值得，但不值得在本轮全面迁移。当前服务动作和订单仍快速迭代，过早把所有方法固化成网络 DTO 会增加双份维护。

建议在 Phase 2 的 LAN 原型开始前引入一个很薄的入口：`PlayerAction`（只含 ID、动作类型和必要参数）→ `HostGameRules.TryApply` → `ActionResult/WorldEvents`。第一批只覆盖抓取、放置、开始/结束服务、使用工具和捡钱。届时 `OfflineTransport` 也走同一个入口，再实现 `LanTransport`；本轮不创建未被使用的空接口。

## 10. 微信/抖音/TapTap 小游戏的 WebGL 适配点

- `SalonDemo` 直接读取 `Input.touchCount` / `Input.GetTouch`；应迁移到 Input Adapter 或 Unity Input System action，不让服务规则依赖触摸 API。
- 截图与验收报告使用 `System.IO`；小游戏构建需把这类调试/验收能力排除，或改为平台存储适配器。
- 未来 LAN 的原生设备发现和 socket 不能进入 Game Core；小游戏走平台 Adapter + WebSocket/在线 Transport。
- 资源、材质、阴影、粒子、碎发对象和 UI overdraw 需要在目标 WebGL/小游戏真机上重新设预算；本轮没有为了假设性能提前降画质。
- 平台登录、邀请、分享、支付、广告、云存档和排行榜均应通过平台服务接口接入，不能进入 `SalonGameModel`。

## 11. 当前平台耦合

- 没有发现原生 Socket、Android Java、iOS API、Steamworks、微信、抖音或 TapTap SDK 调用。
- 没有正式存档实现，也没有 `PlayerPrefs` 业务存档，因此现在引入 `ISaveStorage` 没有实际消费者。
- `System.IO` 只出现在运行验收报告/截图路径，不在顾客、工位、服务、时间或支付规则中。
- `UnityEngine.Random` 的客流/订单随机是未来权威化重点；反馈音高和碎发随机属于本地表现，不需要同步。

## 12. 本轮刻意不改的部分

- 不迁移顾客服务链、耐心、满意度、工位占用、营业时钟或支付模型；这些已有大量测试且方向正确。
- 不把全部 `int` ID 包成值对象。
- 不拆分 2,149 行的 `SalonDemo`，避免影响当前单人开发速度。
- 不新增未使用的 `IGameTransport`、平台服务和存档接口。
- 不实现交互锁、网络快照、回滚、预测、重连、房间、Relay、WebSocket 或 SDK。
- 不同步本地相机、UI 展开状态、震动、音效和碎发粒子。

## 13. 当前单人版本验证

- Unity EditMode：165/165 通过，0 失败，未发现 C# 编译警告。
- macOS Development Player：构建成功，输出 `Builds/HairSalonDemo.app`。
- 兼容入口 `SelectCustomer(customer)`、`ClearFocus()`、`SelectedCustomer`、`ViewState` 均继续代表 Player 1，现有单人 UI 调用未被迫迁移。

自动测试和实际 Player 构建均表明单人行为保持正常；本轮没有代替真人完成一次完整手动营业日，因此不把“体感完全无回归”当作额外已验证事实。

## 14. 真正加入 Player2 时仍需的模块

建议按顺序增加：

1. `PlayerAction` / `ActionResult` 协议和统一 Action Gateway。
2. Host authoritative session runner；世界时钟、客流随机、订单随机和金币都只由 Host 推进。
3. Customer/Station 的短暂 Interaction Ownership，带动作结束、取消、超时和断线释放。
4. 基于稳定 ID 的实体注册表，消除 Station ID 与列表索引的混用。
5. `IGameTransport` + `OfflineTransport`，随后才是 `LanTransport`。
6. 世界快照、增量事件、加入时同步和短暂重连。
7. 每玩家输入控制器、本地 UI/相机，以及 Player 2 角色表现。
8. LAN 房间创建/发现/加入；Host 退出直接结束，无 Host Migration。
9. LAN 验证成功后再做 Relay/房间码与各平台 Adapter。

这条顺序保持“先把单人核心做对，再验证双人是否更好玩”，同时避免未来为了 Player2 重写 Customer、Station 和 Service。
