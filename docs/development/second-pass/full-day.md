# 第二轮两日真实触控对比记录

日期：2026-09-28

本轮只比较 root 明确提供的新版 Demo 与前轮最终包，不修改 Unity 生产文件、场景或公共触控工具。浏览器证据限定在 [SecondPassFullDay](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/SecondPassFullDay/)。前轮原始证据仍保留在 [final-2days-r2](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/FirstSessionFullDay/final-2days-r2/)。

## Before 基线

Before 来源为前轮通过的 `final-2days-r2/states.json`、`actions.json` 与 `report.json`。正式营业段从最后一次开局检查后的 state index 120（Day 1）和 780（Day 2）开始，避免把独立剪发手势检查中的失败样本混入营业统计。完整机器可读记录见 [before-metrics.json](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/SecondPassFullDay/before-metrics.json)。

| 天数 | 日结完成/目标 | 营业结束前 satisfaction | Result satisfaction | 最高等待 | 最高未接待等待 | 耐心流失 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Day 1 | 5/3 | 35 | 0 | 4 | 4 | 1 |
| Day 2 | 6/4 | 0 | 0 | 4 | 4 | 2 |

当前 Before 遥测没有 `score` 或 `shopScore` 字段，因此“score”列使用可用的 `satisfaction` 作为比较代理；第二轮若新版遥测提供 score，将同时记录原始字段。Day 1 的 1 名流失顾客在 ClosingGrace 耐心归零；Day 2 的 2 名流失顾客在营业中耐心归零。两日最高同时 Leaving 数均为 1。该基线只陈述触控结果，不将完成目标写成节奏舒适或好玩结论。

## After 验证

等待 root 明确 `SECOND PASS READY` 后，使用新包执行完整两日真实 Chromium/CDP touch：首日营业、正常打烊、次日营业、两次刷新存档和独立录屏。运行命令及 After 报告、状态、截图和视频将在 READY 后补充到本节；失败轮单独留存，不覆盖 Before 或其他证据。

```text
python3 tools/check-mobile-salon.py \
  --url http://127.0.0.1:8910/WebGLDemo/ \
  --evidence-dir unity-hair-salon/Builds/SecondPassFullDay/<after-run> \
  --record-video-dir unity-hair-salon/Builds/SecondPassFullDay/<after-run>/video \
  --days 2
```
