# 首日真实触控复现与回归记录

日期：2026-09-28

## 旧包基线

使用本机已启动的 WebGL 包 `http://127.0.0.1:8910/WebGLDemo/`，视口为 844×390，输入方式为 Chromium CDP 真实触控事件。证据独立保存于 [FirstSessionFullDay](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/)。

基线可以完成首位剪发和第二位洗发的接待链，但第三位洗发顾客无法被安排到工位，最终以 `completed=1/3` 进入结算。无浏览器错误。关键截图为 [第二位接待就绪](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/15-second-customer-greet-ready.png)、[第二位安排洗发](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/17-second-customer-wash-assign-ready.png) 和 [失败结算](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/05-result.png)。完整状态、动作和结果见 [states.json](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/states.json)、[actions.json](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/actions.json) 与 [report.json](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/report.json)。

故障帧持续报告 `targetCustomer=2`、`targetAction=Assign`、`action=前往洗发工位`、`available=false`。脚本把遥测中的洗发工位 id=4 当作“空闲”候选，但该旧包只报告 `occupied`，没有报告 `WorkstationModel.IsUsable`；当前移动开局 id=4 是锁定工位。角色停在 `(-3.676, 3.485)`，随后脚本反复点击禁用按钮，直到营业时间结束。这是脚本导航/工位选择错误，不足以证明产品流程不能完成。

## 脚本回归修复

`tools/check-mobile-salon.py` 现在优先使用生产遥测的 `stations[].usable`；兼容旧包时只选择开局明确开放的 id=0、1，过滤锁定的 id=2、3、4。接待、工位分配和等待顾客的候选筛选共用同一判定，避免某个分支重新进入锁定锚点。

新增 [tests/test_mobile_driver.py](/Users/kker/Documents/ChatGPT/game2/tests/test_mobile_driver.py)。修复前回归测试报 `MobileDriver` 缺少工位可用性判定；修复后通过：

```text
python3 -m unittest tests.test_mobile_driver -v
Ran 2 tests ... OK
```

主代理完成新 Unity 构建后，需在同一证据目录继续执行首日、次日、刷新存档和错误恢复的完整真实触控复核；旧包浏览器复跑已按协作约定暂停。

## 新包首轮复现与第二处脚本错误

主代理报告的 `715/715` 测试通过构建首次复核仍使用真实 Chromium 触控，命令额外指定了 `--days 2` 和独立录屏目录 `Builds/FirstSessionFullDay/video-2days/`。首日进入结算时为 `completed=2/3`，浏览器错误为空，因此没有继续伪称次日通过；该轮在首日断言处停止，未产生有效的两日验收结论。

状态证据显示，洗发顾客 O002 已从 Wash 进入 Dry，仍占用洗发椅并标记 `needsTransfer=true`；剪发顾客 O005 已在可用剪发椅就绪。旧策略把所有 `service_ready` 顾客都当成可直接执行对象，优先选中了 O002，于是持续对 `targetAction=Guide`、`action=等待空闲工位` 的禁用目标尝试触控，玩家停在 `(-8.39, 2.92)`。这是脚本选择顺序错误，不是通过放宽目标或注入状态掩盖的产品结果。

脚本现已在 [check-mobile-salon.py](/Users/kker/Documents/ChatGPT/game2/tools/check-mobile-salon.py) 中过滤 `needsTransfer`，先导航至并完成已占用工位上的就绪服务；只有兼容工位实际空闲且 `usable=true` 时，才从顾客当前占用的旧工位触控 Guide，再由下一轮导航至新工位触控 Assign。无兼容空位时只等待，不触控禁用 Guide 或强制取消接待。新增 Python 回归覆盖就绪服务选择、转移工位可用性和旧工位 Guide 锚点。

当前回归命令及结果：

```text
python3 -m unittest discover -s tests -p 'test_*.py' -v
Ran 8 tests ... OK

python3 -m py_compile tools/check-mobile-salon.py
OK
```

主代理重建最终包并明确 READY 后，才可重新启动独立证据目录中的两日真实触控录屏；在此之前不对正在替换的 WebGL 包做浏览器验收。

## 最终包两日闭环结果

最终包 READY 后使用以下命令执行了完整首日、次日、两次闭店刷新和录屏，目标断言保持为当天遥测中的 `target`：

```text
python3 tools/check-mobile-salon.py \
  --url http://127.0.0.1:8910/WebGLDemo/ \
  --evidence-dir unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2 \
  --record-video-dir unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/video \
  --days 2
```

结果为通过：首日 `completed=5/3`、结算余额 `1100`；次日 `completed=6/4`、结算余额 `2500`。首日结算后刷新进入 Day 2，次日结算后刷新仍保留 Day 2 和余额；报告中的 `satisfactionPersisted=true`、`errors=[]`、`admissionWithinCapacity=true`、`backgroundWhileOtherService=true`。报告与 41 MB 录屏见 [final-2days-r2](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/)、[report.json](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/report.json) 和 [录屏](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/video/page@8c479c6c9b64a9138b6a6a89dbf72ef4.webm)。浏览器只记录到 Chromium 在滚动期间取消不可取消 touchstart 的 warning，未记录错误。

### 满意度事实

最终状态中的满意度变化与排队状态一致：Day1 开始为 90；首、二位正常完成顾客离店时分别降至 88、86。第三位完成时仍有 5 位顾客等待，满意度降至 69；第四位完成时仍有 4 位等待，降至 52；第五位完成时仍有 5 位等待，降至 35。随后一位洗发顾客在 ClosingGrace 因 `patience=0`、`serviceResult=Failed` 离店，满意度降至 23。Result/Day2 遥测当前显示 0 并在刷新后保留。这次两日目标通过证明了操作闭环，不证明排队节奏舒适。
