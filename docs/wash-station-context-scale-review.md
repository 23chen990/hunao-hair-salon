# Wash Station Context 比例复核

> 日期：2026-08-30  
> 资产：`furniture-wash-station-vintage-right-wall`  
> 当前状态：`NEEDS-REVIEW`

产品负责人未批准原 1.00× Context 比例。原始 PNG、pivot、baked shadow、`right-wall` orientation、人物位置、地砖和服务逻辑均未修改。

Manifest 中原始 `DesiredWorldSize=4.57801×4.57801` 暂时保留，尚未批准；以下三个数值只通过开发查询参数应用于 Asset Lab Context，没有写回 Manifest：

| 方案 | 相对原尺寸 | 临时 World Size |
|---|---:|---:|
| A | 0.80× | 3.662×3.662 units |
| B | 0.85× | 3.891×3.891 units |
| C | 0.90× | 4.120×4.120 units |

并排对比图：

[asset-lab-furniture-wash-station-vintage-right-wall-context-scale-comparison-080-085-090.png](../unity-hair-salon/Builds/PipelineEvidence/asset-lab-furniture-wash-station-vintage-right-wall-context-scale-comparison-080-085-090.png)

机器证据：

- 三个面板均为独立 Chromium 844×390、DPR 1 截图；
- 相机、人物、地砖与 asset pivot 保持一致；
- 运行时纹理均为原始 1254×1254；
- 状态日志均为 `NEEDS-REVIEW`；
- orientation 均为 `right-wall`；
- 浏览器 console 为 0 错误；
- Node 13/13、Unity EditMode 509/509 通过；
- 候选洗→剪→吹→离店→支付回归通过，证明服务逻辑未被修改。

产品负责人确认 A、B 或 C 前，不建立 wash-station 类别 reference baseline。确认后只保存所选 world size 与类别 baseline，不修改 PNG 或服务状态机。
