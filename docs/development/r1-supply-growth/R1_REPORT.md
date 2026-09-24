# R1：补货、施工与重试闭环报告

> 状态：`FIXED_WITH_EVIDENCE`（代码与 Chromium 证据已完成；真机未执行，见限制）
> 日期：2026-09-24（Asia/Shanghai）
> 仓库：`23chen990/hunao-hair-salon`
> 分支：`phase-0-2-gameplay-gate`
> 审阅基线：`6b7886986978e536635842c91a4d9051549fa67e`
> 最终 HEAD：交付前最后一次提交后的 `git rev-parse HEAD`，交付消息同步记录；本报告随该提交交付。

## 范围

本轮只修复 WashKit 共享库存显示、施工扣费余量和 DayOpening/保存检查点一致性，并测量现有第一个扩建货架的实际路线收益。没有新增第二施工点、容量升级、货币或玩法；PR #1 保持开放，不合并、不 force push。

## 复现、原因、修复与证据

### R1-A：货架显示与共享库存真值不一致

- **复现**：基线回归 `SalonSupplyVisualRegressionTests.SharedRackInventoryIsShownExactlyOnceAcrossOneOrTwoRacks` 在库存 2、未扩建时失败，期望显示 2 件、实际显示 1 件；原循环在每次显示后递减共享剩余数。
- **原因**：同一 `remainingRackItems` 同时作为槽位索引和剩余库存计数，导致库存 0～6 中部分数量被跳过；第二货架还会继续消费同一计数。
- **最小修复**：未扩建时全部显示在原货架；扩建后按共享库存拆成原货架 `ceil(n/2)`、扩建货架 `floor(n/2)`。两处相加始终等于模型的 `WashRackWashKits`，没有复制库存，也没有改容量 6。
- **回归**：库存 0～6、单/双货架，以及取货、卸货、消耗和每日重置路径由模型与显示回归覆盖；聚焦 R1 套件 `26/26` 通过。

### R1-B：施工扣费的帧率相关余量丢失

- **复现**：基线集成计时断言在 30/60/90/120 FPS 和非均匀 `dt` 下无法找到安全余量接口；原场景每次成功扣款后把累计时间清零，整数币舍入的余量被丢弃。
- **原因**：`CalculatePayment` 只返回本次整数扣款，`UpdateMobilePurchasePad` 没有保留 `deltaTime - amount/rate`。
- **最小修复**：新增 `RetainUnspentPaymentTime`，仅在钱包扣款成功且 `ApplyPayment` 成功后扣除已结算金额对应的时间；钱包拒绝或模型拒绝时不推进施工。余额、`Paid`、完成状态仍由现有事务顺序约束，离圈、余额为 0 时清掉未结算时间。
- **回归**：直接调用正式 `UpdateMobilePurchasePad` 集成路径的 30/60/90/120 FPS、非均匀 `dt`、暂停/恢复、离圈、余额不足共 `7/7` 通过；名义 60 金币/秒在约 3 秒完成，`Paid=180` 且余额不为负。

### R1-C：DayOpening 与可持久化检查点混用

- **复现**：基线 `SaveMobileCheckpoint(false)` 在普通营业中保存后会把 `_mobileOpening` 改成营业中状态；存档只有 `SupplyRackExpansionPurchased`，部分投入刷新后没有 `Paid`。
- **原因**：开店快照和安全检查点使用同一个保存入口，且施工进度只有解锁布尔值。
- **最小修复**：
  - `SalonProgressData` 增加兼容字段 `SupplyRackExpansionPaid`，范围校验与解锁成套校验；旧 JSON 缺字段默认 0，旧 `purchased=true` 升级为完整 180 金币，不再次收费。
  - PreOpen 只在新营业开始时捕获 `_mobileOpening`；普通 `SaveMobileCheckpoint(false)` 只写当前检查点，不覆盖 DayOpening。
  - 扣款成功后保存余额、Paid、解锁状态的同一快照；离圈、解锁、日结等安全边界保存，保存失败显示“本次进度无法保存”。
  - 失败日把 `_mobileOpening` 写回并同步 `_mobileProgress`；重试时恢复余额、已购设备和投入金额，并移除回滚掉的扩建货架、锚点、碰撞和显示。
- **回归**：`SalonProgressSaveTests`、`SalonMobileCheckpointRegressionTests`、扩建货架回滚测试及 Chromium 刷新流程通过。真实 Chromium 证据显示部分投入离圈后刷新仍为同一 `Paid/Balance`，完整购买刷新后保持 `Paid=180` 与 `OPEN`。
- **恢复边界**：刷新/重新加载仍是“从保存检查点重新进入当天准备阶段”，不承诺顾客位置、订单和计时器的中途续玩；进程强杀最后一帧也不作绝对不丢失承诺。

### R1-D：第一个扩建点的路线收益

这是测量结论，不是新增设计：

| 同一工作循环 | 购买前 | 购买后 | 变化 |
|---|---:|---:|---:|
| `source → 货架 → 洗发工位` 实测路径 | 17.7993 | 17.0649 | -0.7344 世界单位 |
| 摇杆移动墙钟时间 | 7.2515 秒 | 7.1661 秒 | -0.0855 秒 |
| 卸货次数 | 1 | 1 | 0 |

证据来自 `tools/check-proximity-pad.py` 的真实 Chromium 触控：同一订单策略、同一 844×390 视口、同一共享容量；两次测量都从 `source=12 / carried=0 / rack=0` 的保存检查点开始，使用实际碰撞地图路线和自动拾取/卸货状态。源点到货架的距离变长，扩建入口到洗发工位的回程缩短，合计路径少 `0.7344` 世界单位，卸货次数不变。该选定几何路线的功能收益已测得；单次墙钟差只有 `0.0855` 秒，尚不足以证明普遍的玩家时间收益，因此产品层结论标为 **VALUE_NOT_PROVEN**，不外推到所有顾客路径。

## 验证命令与结果

| 检查 | 命令/证据 | 结果 |
|---|---|---|
| Node 资产管线 | `node --test tests/asset-pipeline.test.mjs` | `17/17` 通过 |
| UI 字体 | `python3 tools/check-ui-font.py` | 367 必需字形、629 打包字形，通过 |
| Manifest/资源 | Unity `BuildScript.ValidatePipeline` | 17 资产检查通过 |
| R1 聚焦 EditMode | `Builds/R1Green5.xml` | `26/26` 通过 |
| 施工集成 EditMode | `Builds/R1RuntimePad.xml` | `7/7` 通过 |
| 全量 EditMode | `Builds/R1FullEditModeFinal.xml` | `670/670` 通过，XML `result=Passed` |
| WebGL | `Builds/WebGLDemoBuild.log`、`WebGLAssetLabBuild.log`、`WebGLCandidateBuild.log`、`WebGLReferenceVisualBuild.log` | 四个构建成功，Demo 约 74 MB |
| 正式移动入口 | `Builds/MobileEvidenceSuccessR1/report.json` | 844×390，真实触控，Day1 `3/3`、余额 `515`、满意度 `69`；刷新到 Day2 准备阶段并保持余额/满意度，错误 `0` |
| 施工/路线入口 | `Builds/ProximityPadEvidence/report.json` | 部分投入刷新、完整购买刷新、卸货和路线对比通过；错误 `0` |
| 失败重试 | `python3 tools/check-mobile-salon.py --failure` | 844×390 真实触控通过；`0/3` 失败后“再试一次”恢复 Day1、余额 `0`、满意度 `90`，错误 `0` |
| 不同横屏比例 | 一次性 Chromium 检查（WebGLDemo） | 960×540 启动、画布 `960×540`、截图非空、浏览器错误 `0` |

Unity 测试命令遵守仓库约束：`-runTests` 未与 `-quit` 同时使用，并检查 XML 的 `result/passed/failed`，没有只看进程退出码。

正式移动回归记录：Day1 完成 `3/3`（目标 `3`），结果余额 `515`、满意度 `69`，随后进入 Day2 准备阶段并在刷新后保持余额和满意度；本轮真实浏览器运行共观察 `439` 个遥测帧，最大等待队列 `2`。失败重试单独运行完成 `0/3`，点击“再试一次”回到 Day1 开店状态，余额和满意度回到开店值。

## 普通中文试玩步骤

1. 在仓库根目录运行 `python3 tools/serve-salon.py --port 8910`，打开 `http://127.0.0.1:8910/WebGLDemo/`；这是本机试玩入口，不是公网链接。
2. 以横屏 844×390 视口进入，点“开始营业”，使用左摇杆移动、右侧按钮接待和安排顾客。
3. 按提示从后场取 WashKit，到洗发架卸货；完成洗发后继续后台吹发和接待下一位顾客。
4. 赚到金币后走到 `BUILD 0/180` 施工圈，停留观察扣费；离开后再回来会从剩余进度继续，刷新会回到 PreOpen 并恢复同一个保存检查点。
5. 购买完成后看到第二个卸货入口和 `OPEN`；完成一轮后可在结果页进入闭店经营。失败局使用“再试一次”，当天营业收入和当天施工投入回滚到开店快照，之前已经完成的扩建保留。

## 证据文件

构建和截图按仓库约定留在本机 `unity-hair-salon/Builds/`，不提交 Git：

- `ProximityPadEvidence/report.json`：施工、部分投入刷新、解锁刷新、真实路线距离/时间/卸货次数。
- `MobileEvidenceSuccessR1/report.json`：844×390 正式移动入口、首日结算、后台吹发交错、第二顾客洗发接待、刷新/跨天。
- `MobileEvidence/report.json`：844×390 失败日与“再试一次”恢复。
- `MobileEvidence/r1-route-before.png`、`r1-route-after.png`：同视角路线证据（由施工脚本生成）。
- `MobileEvidence/r1-alt-960x540.png`：不同宽高比横屏启动截图。
- `R1Evidence/video/r1-mobile-844x390.webm`：真实 Chromium 移动入口短录屏（开店准备→开始营业）。

## 未解决项与限制

- `tools/check-mobile-salon.py --skip-haircut-checks` 是旧的“跳过手势检查”选项，未包含当前补货主路径，曾在 `1/3` 停止；正式默认路径已补货并通过，本轮不把该旧选项标为通过。
- 未执行真实 iOS/Android 手机检查；当前结论是 Chromium 真实触控，不是“真机通过”，因此真机状态为 `ENVIRONMENT_UNVERIFIED`。
- Chromium headless 会报告 `screen.orientation.lock() is not available` 能力提示；验收脚本按既有规则过滤这一环境提示，游戏运行与资源错误仍为 `0`。
- 路线收益只证明选定的 `source → 货架 → 洗发工位` 循环；没有改布局、容量或扩建链，也没有声称所有订单路线都会变短。
- 保存失败只显示提示并保留当前页面，不伪称已保存；没有测试进程强杀最后一帧。
- 旧工程文档中的其他原型状态仍保留在根 `PROJECT_STATUS.md`，理发店当前入口只在文件顶部新增，不覆盖历史内容。
