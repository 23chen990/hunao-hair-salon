# PHASE0_BASELINE.md — 当前基线与验收快照

> 生成时间：2026-09-18（Asia/Shanghai）
> 目的：在改动任何业务代码前，冻结并验证当前基线，作为本轮所有改动的对照。
> 本轮原则：不推倒重做、不新增玩法、先保护现有成果。

---

## 1. 仓库与 HEAD

| 项目 | 值 |
|---|---|
| 仓库路径 | `/Users/kker/Documents/ChatGPT/game2` |
| Unity 工程 | `unity-hair-salon/` |
| 分支 | `main` |
| HEAD commit | `a71e7279b5e21909a35ac4186b755c3d123d5ffa` |
| HEAD 标题 | `chore: checkpoint hair salon pipeline baseline` |
| HEAD 时间 | 2026-08-30 11:07:24 +0800 |
| 可读取提交历史 | 仅 1 个 commit（历史不可用于还原改动意图） |
| Unity 版本 | `6000.5.8f1`（`/Applications/Unity/Hub/Editor/6000.5.8f1`） |

> 注意：磁盘上另有一份陈旧副本 `/Users/kker/.codex/worktrees/4903/game2`（最后更新 8-31，不含 TECH_HANDOFF/CORE_FILE_MAP/CURRENT_ARCHITECTURE）。
> 本轮认定 `/Users/kker/Documents/ChatGPT/game2` 为唯一活跃工作区。陈副本未被改动，但后续应确认是否可以删除，避免两份代码再次分叉。

## 2. 工作区状态（改动前）

| 类型 | 数量 |
|---|---|
| 已跟踪文件的未提交修改 | 43 |
| 未跟踪文件/目录 | 124（含 `docs/`、`artifacts/`、`assets/source/`、新资产 inbox 与预览） |
| `git status --porcelain` 总条目 | 167 |
| diffstat | 44 files changed, 4483 insertions(+), 371 deletions(-) |

未提交修改的主要范围：

1. 资产管线：`.agents/skills/salon-asset-ingest`、`salon-visual-qa`、`tools/asset-pipeline.mjs`、`tests/asset-pipeline.test.mjs`
2. 构建与验收：`tools/check-project.sh`、`tools/browser-check.py`、`Assets/Editor/BuildScript.cs`
3. 资产 Manifest 与视觉实验室：`asset-manifest.json`、`AssetTestLab.cs`、`HairSalonDemo.unity`、`AssetManifest*.cs`
4. 主玩法模型：`SalonDemo.cs`、`SalonGameModel.cs`、`BusinessDaySystem.cs`、`SalonPaymentModel.cs`、`ShopSatisfactionModel.cs`、`Stage3WashServiceAdapter.cs`、`MetricsAndSettlement.cs`
5. 视图：`CustomerEmotionView.cs`、`CustomerUIRootView.cs`、`OrderDemandBubbleView.cs`、`DemandBubbleArtwork.cs`
6. 自动测试：13 个测试文件的新增/修改

**本轮承诺：以上未提交工作全部保留，不做任何删除或覆盖。**

## 3. 基线验收结果（全部在执行改动前运行）

| # | 检查 | 命令 | Before 结果 |
|---|---|---|---|
| 1 | Node 资产管线测试 | `node --test tests/asset-pipeline.test.mjs` | **17 / 17 通过，0 失败** |
| 2 | Unity EditMode | Unity `-runTests -testPlatform EditMode` | **595 / 595 passed，0 failed** |
| 3 | Manifest / 资源校验 | `BuildScript.ValidatePipeline` | **通过** — `Startup, resources and manifest checks passed for 13 assets` |
| 4 | WebGL Demo 构建 | `BuildScript.BuildWebGLDemo` | **成功** — `Build Finished, Result: Success` |
| 5 | Chromium Demo smoke | `tools/browser-check.py --mode demo` | **通过** — coreFlowPassed=true，debugModeVisible=true，errors=[] |
| 6 | Chromium 候选场景 | `tools/browser-check.py --mode candidate` | **通过** — 两轮（普通 + debug）均产生 `CANDIDATE_ALIGNMENT_READY` / `CANDIDATE_SERVICE_ACTIVE` / `CANDIDATE_FLOW_PASS` |

结果文件：

- `unity-hair-salon/Builds/PHASE0_EditMode.xml`（595 testcases, result=Passed）
- `unity-hair-salon/Builds/PHASE0_Validate.log`
- `unity-hair-salon/Builds/PHASE0_WebGLDemo.log`
- `unity-hair-salon/Builds/PipelineEvidence/browser-check-demo.json`、`browser-check-candidate.json`

### 3.1 与 TECH_HANDOFF.md 记录值的差异（重要）

`TECH_HANDOFF.md` §1.3 记录了两项失败：Chromium 调试模式未通过、候选场景缺少 `CANDIDATE_ALIGNMENT_READY`。

**本次基线运行时这两项均为通过。** 判断：

- 原因最可能是交接文档生成（09-18 19:19）之后工作区又发生过改动，或这两项属于不稳定/flaky 的视觉验收；
- 无论原因为何，**当前基线比文档记录的更好**，本轮以实际运行为准；
- 风险提示：这两项属于「视觉实验室 + 候选资产」链路，不属于正式移动玩法链路，本轮不扩大修复范围，但会在最终报告中再次运行以确认没有回归。

### 3.2 环境注意（非项目问题）

资产管线测试依赖 Python `PIL`。系统 `/usr/bin/python3` 具备 Pillow 11.3.0；若 `PATH` 前置了不含 Pillow 的 Python，测试会以 `ModuleNotFoundError: No module named 'PIL'` 失败 3 项（测试 4 / 5 / 17）。

**这是环境 PATH 问题，不是项目回归。** 运行验收时须保证 `python3` 解析到带 Pillow 的解释器。

## 4. 当前普通 Demo 是否可玩

**是，可玩。** 基线运行路径：

```text
HairSalonDemo.unity
  → Bootstrap / SalonDemo.Start
      → ConfigureMobileGame()          // 普通 URL 进入移动正式路径
      → SalonMobileDayConfig.CreateForDay(day)
      → SalonGameModel / BusinessDayController / CustomerTrafficDirector / ShopSatisfactionModel
      → BuildWorld() + BuildHud() + BuildMobileHud()
  → 开店界面 → 营业 120 秒
  → 摇杆移动 → 靠近顾客 → 接待 → 安排工位 → 洗发/剪发/吹发
  → 完成 → 工位掉落金币 → 玩家跑过去收取
  → ClosingGrace 15 秒 → Result → ClosedManagement
```

浏览器实测标记（`?browserSmoke=1`）：`StartedBusiness`、`SpawnedCustomer`、`AssignedStation`、`CompletedService`、`CustomerFinished`、`PaymentCreated` 均已产生。

## 5. 当前已知问题（不在本轮范围内）

| # | 问题 | 影响正式移动 Demo？ |
|---|---|---|
| 1 | `SalonDemo.cs` 3589 行，混合输入/世界构建/HUD/服务编排/验收分支 | 不阻断可玩性，但阻碍主路径统一 |
| 2 | 移动路径与桌面路径的操作语义存在分叉（见 `MAIN_PATH_AUDIT.md`） | **影响**，是本轮 Phase 1/2 的处理对象 |
| 3 | 顾客状态在 `CustomerModel` / `CustomerServiceState` / `ServiceProgress` / `WorkstationModel` / View 之间重复保存 | **影响**，本轮只定义所有权，不做大规模重构 |
| 4 | 泡沫等待后台（`SalonGameModel.UpdateServiceStage` 的 `WashStage.Foamy` 分支）目前**没有任何代码路径启动对应的 `BackgroundTask`**，属于事实上的死分支 | 影响 Wash 节奏设计，本轮复用并激活它 |
| 5 | `AutoBlowPurchased` 用单个 bool 表示设备状态 | 不阻断；本轮保留兼容，不再复制该模式 |
| 6 | 订单 O001–O005、Day 目标、订单池在 `SalonMobileDayConfig` 中以 if/else 写死 | 不阻断；本轮只做最小必要调整 |
| 7 | 存在一份陈旧仓库副本 `.codex/worktrees/4903/game2` | 不阻断；有再次分叉风险 |

## 6. 当前主要运行路径

```text
正式移动玩法（产品主路径）
  SalonDemo.Mobile.cs
    FindMobileTarget() → ExecuteMobileTarget()
      Greet / Guide  → SalonGameModel.SelectCustomer
      Assign         → SalonGameModel.Assign
      Wash           → SalonGameModel.BeginServiceExecution(Wash)       [3 秒，玩家被锁]
      Cut            → SalonGameModel.BeginHaircutAction + 本地计时     [玩家被锁]
      StartDry       → SalonGameModel.StartAutoBlow                     [后台]
      FinishDry      → SalonGameModel.FinishAutoBlow
      Payment        → SalonPaymentModel.BeginCollection / CompleteCollection

桌面 / 开发路径
  SalonDemo.cs BuildToolBar()
      洗头工位 → BeginServiceExecution(Wash)
      理发工位 → 剪刀/牙剪/推子/吹风机/染发刷 + 手动吹发/自动吹发
  另有 washCraft=detail / washCraft=service URL 走细分洗头步骤

纯领域服务层（两条路径都应最终汇聚到这里）
  Stage3WashServiceAdapter
    → InteractionContext / ActionToken
    → ActionResolver.Resolve(...)
    → ActionResultApplier.Apply(...)
    → CustomerPhysicalState / ServiceProgress
    → Project() 回写 CustomerModel
```

## 7. 本轮将保护的行为基线

以下行为在本轮**不允许被破坏**，任何改动若导致其中一条失败即视为回归：

1. **顾客生命周期**：`Entering → Waiting → MovingToStation → Serving → Finished → Leaving → Exited` 完整可达。
2. **多顾客并行**：一个顾客处于主动服务时，其它顾客与后台任务在同一 `Tick` 中继续推进。
3. **后台吹发**：`StartAutoBlow` 后玩家离开，任务继续；`FinishAutoBlow` 在正常窗口返回 `Good`，超时返回 `Minor`/`Moderate`，安全停机后仍可收尾。
4. **剪发规则**：`BeginHaircutAction` / `CompleteHaircutAction` 的 perfect / undercut / overcut / wrong tool / repair 语义不变；`ServiceExecutionType.Haircut` 仍然被拒绝作为后台计时。
5. **付款**：一个完成订单只生成一个 `PaymentDrop`，`DayStats.RecordPaymentCollected` 按 `drop.Id` 去重，只入账一次。
6. **Day 边界**：`ClosingGrace` 与 `ForceCloseRemainingCustomers` 不产生重复付款、重复统计；`ResetForNextDay` 后世界可重新开始。
7. **存档**：`SalonProgressSave` schema 不变，现有存档仍可读取（本轮不触碰存档结构）。
8. **自动测试**：现有 595 条 EditMode 测试与 17 条 Node 测试在改动后仍须全部通过（除非有明确说明的、已批准的行为变更）。
9. **浏览器验收**：`--mode demo` 与 `--mode candidate` 在改动后仍须通过。
10. **ActionResolver / ActionResultApplier**：只作为被调用方，不因本轮目标设计变化而重写。
