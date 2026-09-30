# 第二轮满意度归零诊断

## 结论

上一轮 `FirstSessionFullDay/final-2days-r2` 的两日操作闭环通过，但满意度归零有明确的收尾路径：Result 展示完成时，`SalonDemo.CompleteResultPresentation()` 调用 `SalonGameModel.ForceCloseRemainingCustomers()`；该方法把仍在 `Entering`、`Waiting` 或服务中的顾客置为 `Exited`，再触发 `CustomerChanged`。`SalonDemo.HandleCustomerChanged()` 对每次顾客事件调用 `ShopSatisfactionModel.TrySettleCustomer()`，因此尚未接待的排队顾客也被当成失败订单结算。

这不是同一个顾客重复扣分。`ShopSatisfactionModel` 用 `_settledCustomers` 按顾客 ID 做幂等保护；问题在于闭店时仍在店内的不同顾客被逐个写成终态并逐个结算。第一日被夹到 0 后，移动入口在下一日以当前值调用 `BeginDay(_satisfaction.CurrentSatisfaction)`，所以第二日从 0 继续，报告中的“满意度持久化”正好证明了这一点。

## 实测状态证据

来源：

- `unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/report.json`
- `unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/states.json`
- 共 1432 个 Chromium touch 状态帧，脚本报告 `passed: true`、`errors: []`。

报告中的日结结果是：

| 日 | 完成/目标 | 余额 | 满意度 |
| --- | ---: | ---: | ---: |
| Day 1 | 5/3 | 1100 | 0 |
| Day 2 | 6/4 | 2500 | 0 |

Day 1 的满意度状态序列能与代码路径逐段对应：

- 开店为 90；前两位正常完成顾客离场后为 88、86。
- 第三、四、五位完成时分别为 69、52、35。三次都是 `NormalCompletion`，但每次都走了“长等待 + 事故”分支，单次为 `+2`（正常完成）−`4`（长等待）−`15`（`AccidentSeverity != None` 的 DisasterPenalty）=`−17`。
- ClosingGrace 中一位顾客因耐心归零，以 `Failed` 离场，满意度由 35 变为 23。
- Result 收尾时仍有一位 `Serving` 和三位 `Waiting` 顾客；强制清客后满意度变为 0，顾客列表清空。
- Day 2 PreOpen 仍为 0，第二日 Result 继续为 0。

状态帧 716、743、768 分别记录了 35、23、0 的关键跳变；这与 `ForceCloseRemainingCustomers` 在最后一帧逐个触发 `CustomerChanged` 的实现一致。现有证据没有显示 Rating 到满意度文本的映射异常，也没有显示后台时间戳导致的额外扣分。

### 第三到第五单的 −17 来源

旧 `states.json` 直接记录了三位顾客的 `serviceResult=NormalCompletion`、`wrongStationCount=0`、`reactionKind=None`，所以没有证据表明这三单走了错误工位或错误工具。旧遥测没有序列化 `AccidentSeverity`、`TotalWaitSeconds`、`HadServiceDelay`，下面把可观察字段和由模型公式反推的分支分开记录：

| 顾客 | 完成状态帧 | 洗发→冲洗的 action progress | 按 180 秒换算的记录间隔 | 由阈值支持的事故分支 | 商店变化 |
| ---: | ---: | ---: | ---: | --- | ---: |
| 1 号（O002） | 457 | 0.236510→0.321769 | 约 15.35 秒；扣除约 2 秒洗发动作后仍约 13.35 秒 | 超过 Moderate 阈值 12 秒，支持泡沫迟到事故 | −17 |
| 3 号（O003） | 525 | 0.518440→0.569430 | 约 9.18 秒；扣除约 2 秒洗发动作后约 7.18 秒 | 超过 Minor 阈值 7 秒，支持泡沫迟到事故 | −17 |
| 4 号（O004） | 716 | 0.725283→0.824625 | 约 17.88 秒；扣除约 2 秒洗发动作后仍约 15.88 秒 | 超过 Moderate 阈值 12 秒，支持泡沫迟到事故 | −17 |

这些间隔来自脚本动作采样点，不是领域内部精确的泡沫计时；精确的“长等待”字段在旧文件中不可见。要得到 −17，`ShopSatisfactionModel` 的长等待条件必须同时成立，即 `TotalWaitSeconds > 12` 或 `HadServiceDelay`；事故条件则是任意非 `None` 的 `AccidentSeverity`。因此这里能确定的是“正常完成 + 长等待分支 + 事故分支”，具体长等待布尔来源和事故枚举属于旧遥测未记录的内部字段，不能冒充直接观测值。

这也说明闭店不是全部压力来源：90→35 的 −55 已在营业中发生（前两单各 −2，第三至第五单各 −17），耐心归零的真实失败又使 35→23。闭店修正只移除未接待顾客从 23→0 的错误扣分，不能解释此前已经发生的 −67。

### 脚本路径边界

`tools/check-mobile-salon.py` 的 `haircut_checks()` 确实会单独故意验证提前松手、暂停和 overcut 失败；它在 `actions.json` 中留下检查标记，并在进入全天 `play_day()` 前重新加载。这个故障注入没有进入上述 5/3、6/4 日结状态。

全天路径则是有意制造交错服务和队列压力的确定性脚本：直接观测到的完成单均为 `NormalCompletion`，`wrongStationCount` 均为 0，`reactionKind` 均为 `None`；它没有故意把顾客送到错误工位或使用错误工具，但会在处理其他顾客时让部分泡沫等待越过迟到阈值。这个结果说明一种可复现的高压路径，不代表普通玩家必然每次得到相同事故或满意度。

## 闭店边界

本轮产品判断如下：

1. 到点进入 ClosingGrace 后，耐心仍大于 0、尚未接待、未开始服务、没有真实失败或错误债的 `Entering`/`Waiting` 顾客正常打烊离开，保持 `CustomerServiceResult.None`，不扣店铺满意度。
2. 已接待但尚未完成的顾客继续保留闭店后果；当前实现的 `SevereUnhappyCompletion` 及已有长等待/延迟扣分继续有效。
3. 真实耐心归零离场继续是 `Failed`，继续扣满意度。
4. 闭店清理重复调用不能再次结算同一顾客。

这条边界不改变 180 秒营业、15 秒 ClosingGrace、持续接客、目标、奖励或收入入账规则，也不通过调低数值来掩盖问题。

## 最小回归

新增 `unity-hair-salon/Assets/Tests/SecondPassSatisfactionTests.cs`（及 `.meta`），用真实 `SalonGameModel.CustomerChanged` 事件订阅到 `ShopSatisfactionModel.TrySettleCustomer`，覆盖：

- 未接待的 `Waiting` 与 `Entering` 在闭店后结果保持 `None`，满意度保持 90；
- 已接待、未完成且长等待的顾客仍为 `SevereUnhappyCompletion`，满意度按现有长等待规则下降到 86；
- 真实耐心归零顾客仍以 `Failed` 离场，满意度由 90 降到 82；
- 已经带着 `Failed` 结果进入 `Leaving` 的顾客不会被闭店清理改写成 `SevereUnhappyCompletion`，仍按原失败结果结算到 78；
- 对已清理顾客重复调用 `ForceCloseRemainingCustomers` 不重复扣分或计数。

首轮红测已推动生产模型完成边界修正：`ForceCloseRemainingCustomers` 保留 Leaving 顾客已有结果；仍有耐心且未接待的 Entering/Waiting 顾客不生成服务结果；`ShopSatisfactionModel.TrySettleCustomer` 忽略没有服务结果的终态。此前红测中的两类失败因此得到覆盖，真实失败和已接待未完成顾客的惩罚保持不变。

```text
SecondPassSatisfactionTests
```

历史红测记录在 `SecondPassControlsSatisfactionRed.xml`：该批次共 7 个用例，本文件的 5 个满意度用例中 3 个通过、2 个失败。两个失败分别是“未服务闭店仍被写成 `Failed`”和“已有 `Failed + Leaving` 被清理改写为 `SevereUnhappyCompletion`”。这确认本轮修正的是闭店结果语义与错误结果覆盖，保留真实失败的惩罚，不是盲目降低满意度数值。

最终串行 XML 为 `unity-hair-salon/Builds/SecondPassFinalEditMode.xml`，文件存在且 `result="Passed"`：731/731 用例通过、失败 0；其中 `SecondPassSatisfactionTests` 的 5 条全部通过。这个结果只验证模型和闭店边界回归，不等同于真人节奏或满意度认可。

## 限制

两日报告证明脚本输入下的达标和状态持久化，不等于真人满意度认可。旧脚本的压力路径仍留下长等待与泡沫迟到的独立体验问题；本文不把它们全部归咎于闭店，也不把故障注入当作普通玩家必然遭遇。本文只解释满意度归零的可复现状态链和闭店语义，不据此调整营业节奏、耐心参数或奖励数值。
