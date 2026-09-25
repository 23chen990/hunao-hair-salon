# TECH_DEBT_NEXT.md — 本轮记录但明确不修的技术债

> 生成时间：2026-09-18（Asia/Shanghai）
> 定位：`STATE_OWNERSHIP.md` 与 `MAIN_PATH_AUDIT.md` 中判定为「需要重构但会扩大本轮范围」的问题，集中登记在此。
> **本轮（Phase 0—2）不处理本文件内的任何一项。**

---

## D1 · 耐心 / 满意度的双向同步时间窗

- **现象**：`CustomerServiceState.Metrics` 与 `CustomerModel.Patience / Satisfaction` 互为副本，只在每次领域动作开始时通过 `SynchronizeExternalMetrics` 对齐。中间模型 Tick 会独立扣减耐心，直到下一次动作才回写。
- **风险**：领域动作内部看到的耐心/满意度可能滞后；长期看任何「按耐心奖惩」的新规则都会出现边界抖动。
- **为什么现在不改**：该行为已被 595 条 EditMode 测试锁定，改动等于重写服务结算链路。
- **建议**：下一步先补一组「同步时序」测试把当前行为固定下来，再考虑把耐心/满意度收敛为单一拥有者（建议模型层独享，领域层只接收只读快照）。

## D2 · `Stage3WashServiceAdapter` 越界承担业务决策

- **现象**：`ProcessFoamBurstRecovery`（L374-415）自行创建 `FunnyDisasterEvent` / `RecoveryRequirement` 并写 `CustomerModel`；`CreateOrder`（L578-625）自行从 `Needs` 构建 `OrderDefinition`。
- **风险**：适配器名义上是桥接层，实际参与业务判定；未来加染发/烫发时会出现「规则写在适配器还是模型」的二义。
- **建议**：把事故/补救的创建上移到 `SalonGameModel`，把订单构建移到独立的 `OrderFactory`，适配器只保留 token、编排与投影。

## D3 · 投影字段被当成输入使用

- **现象**：`CustomerModel.WashStage` 是物理状态的投影，却被 `UpdateServiceStage`（L1983-2009）、`PatienceDrainMultiplier`（L2217）、`ResolveWashToolSelection`（L1054）当作判定输入；同时 `ApplyShampoo`（L1147）与 `PrepareCurrentStep`（L1971）又直接写它。
- **建议**：先把所有判定改为读取 `CustomerPhysicalState`，再逐步废弃 `WashStage` 的写入路径。

## D4 · `ForceCloseRemainingCustomers` 绕过状态机

- **现象**：L1496 直接 `customer.State = CustomerState.Exited`，绕过 `ChangeState` 的队列清理与事件通知。
- **风险**：Day 强制收尾时可能留下悬空的等待队列项或未被通知的 View。
- **建议**：改为复用 `ChangeState` 并显式处理「强制」语义分支，同时补「ClosingGrace / ForceClose 不产生重复统计」的测试。

## D5 · `CustomerModel.Step` 与 `ServiceProgress` 双轨进度

- **现象**：订单进度同时由 `Step`（兼容层）和领域 milestone（权威）表达，靠 `CompleteCurrentStep` 维持一致。
- **建议**：中期把 `Step` 降级为纯只读派生属性；在此之前，所有新增完成判定必须走领域层。

## D6 · `SalonTool` 与 `ServiceTool` 双枚举

- **现象**：两套工具枚举，映射分散在 `Stage3WashServiceAdapter.TryServiceTool` / `TryHaircutTool` 与多个调用点。
- **建议**：建立单一映射表（工具类或静态字典），并让 `SalonTool` 只保留 UI/工位能力语义。

## D7 · 订单与 Day 配置按路径分支

- **现象**：移动走 `SalonMobileDayConfig.PickOrderForSpawn`（Day 分支 if/else），桌面走 `DaySettings.PickOrder`（加权随机）；剪发工具要求也按 `_mobileMode` 分支。
- **建议**：统一为一份「订单池 + 权重 + 工具要求」的数据表，两条路径共用同一抽取器。这一步会触及大量已有测试，应在有端到端基线后再做。

## D8 · `SalonDemo` 体积与职责

- **现象**：3589 行，混合输入、世界构建、相机、HUD、服务编排、Day 回调、浏览器验收分支。
- **建议**：按「输入 → HUD → View → 业务编排」逐步抽取；**每次抽取必须以行为锁定测试先行**，且不以「拆干净」为阶段成功标准。

## D9 · 陈旧仓库副本

- **现象**：存在 `/Users/kker/.codex/worktrees/4903/game2`，最后更新 2026-08-31，缺少本次交接文档。
- **风险**：两份代码继续分叉。
- **建议**：确认无用后删除；在删除前不要在其中做任何改动。

## D10 · 仓库历史中的巨型对象

- **现象**：`.git/objects` 约 4.5 GB，来自历史上被 `git add` 过又撤销的 Unity `Library/Builds/Artifacts` 内容；当前提交本身只有 610 → 653 个文件。
- **建议**：在推送到远端前用 `git filter-repo`（或等价方式）清理历史，并在日常流程中依赖 `.gitignore`。
