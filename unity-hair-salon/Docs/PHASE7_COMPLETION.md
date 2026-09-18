# 第七阶段验收修正完成报告

## 本次 23 项完成报告

1. Day 状态机现为 `PRE_OPEN → BUSINESS → CLOSING_GRACE → RESULT → CLOSED_MANAGEMENT`；准备下一天后进入新 Day 的 `PRE_OPEN`，不会自动营业。
2. `RESULT` 只展示已冻结的当日经营结果；玩家进入 `CLOSED_MANAGEMENT` 后消费或跳过，再进入下一 Day 的 `PRE_OPEN`。
3. `PRE_OPEN` 的“开始营业”按钮是唯一正式开店入口，调用 `StartBusiness()` 后才进入 `BUSINESS`。
4. Business Timer 在 `PRE_OPEN` 完全冻结，只在 `BUSINESS` 内递减；收尾阶段使用独立的 Closing Grace Timer。
5. Customer Spawn 只在 `CanSpawnCustomers == true` 时发生，因此必须处于未暂停的 `BUSINESS` 且营业时间尚未归零。
6. `OrderIncome` 只在完整订单结束后形成，数值是折扣、免单等规则处理后的最终服务费，不包含小费；各 Service Step 不单独结算。
7. 打折只改变最终订单服务费。例如原价 100、八折、小费 10，记录为 `OrderIncome=80`、`TipIncome=10`，单个钱袋 `FinalPayment=90`。
8. 免单记录 `OrderIncome=0`。当前普通规则下小费也可为 0；未来即使允许免单后给小费，也仍只进入 `TipIncome`。
9. `CompensationExpense` 只记录实际支付成功的营业赔偿，同时从钱包扣款；它不与免单混为一谈。
10. `TipIncome` 在完整订单结束时独立计算并写入同一 Payment Drop 的 `TipIncomeComponent`，永远不并入 `OrderIncomeComponent`。
11. `OperatingNetIncome = OrderIncome + TipIncome + OperatingRewardIncome - CompensationExpense - OtherOperatingExpense`。
12. 钱包与 DayStats 都只在钱袋拾取动画完成后入账一次；Result 是只读账单，不会再次修改钱包，重复回调也由 Payment ID 防重。
13. 未拾取钱袋在进入 Result 时从场景和当天 Payment 列表中清理，其订单与小费分量均不进入当天实际结算。
14. `UncollectedIncome` 已从正式 DayStats 和 Result 删除：未拾取钱袋不是收入，只是尚未兑现的场景对象。
15. `ManagementTransaction` 独立记录 `Equipment / Expansion / Upgrade / Marketing` 等经营投资，完全不进入已结算的营业支出或 `OperatingNetIncome`。
16. 购买设备成功时，钱包立即扣除商品价格并追加一笔 Equipment 类型的经营投资；Day Result 不回写、不变化。
17. 自动吹风支架在闭店经营中购买，下一天 `PRE_OPEN` 会明确显示已安装，并在该营业日开始后可用。
18. Reputation 初始为 3.0 星，只按顾客最终结果统一计算；Happy、Unhappy、Very Unhappy、未服务关店、未完成关店等分别贡献变化，并受单日增减上限及 1～5 星边界保护。
19. Reputation 由 `BusinessDayController` 持有，不随 DayStats 重建而清零；上一日结果直接成为下一日显示与客流输入。
20. Traffic 使用口碑相对 3 星的轻量倍率，当前范围固定为 0.9～1.1；低口碑仍有最低客流保护，高口碑只带来轻微额外压力。
21. `PRE_OPEN` 当前展示 Day 编号、开店准备状态、基础设备或自动吹风支架安装信息、当前口碑 HUD，以及“开始营业”按钮。
22. 专项测试覆盖完整订单/多步骤、折扣、免单、赔偿、订单与小费拆分、未拾取清理、钱包防重复、经营投资、买/不买跨日、Reputation 持久化与客流边界、PRE_OPEN 冻结与 UI 文案；全量 EditMode 结果见下方证据。
23. 本轮自动化未发现回归 Bug；第六阶段并发服务、FIFO、洗剪吹状态、物理阻塞和拾取链路仍由全量回归测试覆盖。阶段八功能未提前开发。

## 数据口径

一个完整订单只产生一个钱袋：

```text
FinalPayment = OrderIncomeComponent + TipIncomeComponent
钱包入账       += FinalPayment
DayStats       += OrderIncomeComponent（订单）
DayStats       += TipIncomeComponent（小费）
```

只有完成手动拾取的钱袋执行上述入账。钱袋生成、订单中的单个服务步骤、Result 展示、闭店经营购买都不会把订单收入或小费重复计入当天结果。

## 验收证据

- 全量 EditMode 测试：`195/195` 通过。
- `Phase7AcceptanceCorrectionTests`：数据口径、生命周期、投资和口碑专项测试。
- `Phase7CorrectionRuntimeViewTests`：PRE_OPEN、Result、闭店经营页面与未拾取钱袋清理测试。
- 运行态双路径报告：`Builds/Phase7Evidence/two-day-buy.json` 与 `Builds/Phase7Evidence/two-day-skip.json`。
- 可视证据：`Builds/Phase7Evidence/` 内 PRE_OPEN、营业 HUD 与跨日闭店经营截图。
