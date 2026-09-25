# Round 2.1：真实 PNG 显示与视觉 QA 基准

> 后续比例复核：产品负责人已明确不批准 wash station 的原 1.00× Context 比例。该资产现为 `NEEDS-REVIEW`；本报告中关于 B“比例合理/通过”的旧判断已被撤销。最新三方案证据见 `docs/wash-station-context-scale-review.md`。

> 完成日期：2026-08-30（Asia/Shanghai）  
> 当前截图基准：844×390、devicePixelRatio 1。该尺寸只用于本轮稳定复核，不代表最终正式目标尺寸。

## 根因

上一版的“又小又糊”不是一个原因，而是三项问题叠加：

| 环节 | 结论 | 实际证据 |
|---|---|---|
| 原始 PNG | 没有发现导致本轮模糊的源文件问题 | A 为 1211×1299，hash `00bf361949d088a5859f675c5eeb8925aff6923539d643e5a3f68a3b3245a9cb`；B 为 1254×1254，hash `d0ffacbb09f847154ff08458ae54621b256742c8fc6fa33b715566401c070cbb`。原图、归档、Imported 副本 hash 一致。 |
| Texture Import | 有问题，是主要清晰度根因 | 旧设置启用 mipmap、普通压缩、Repeat，`alphaIsTransparency=false`，NPOT 自动缩放；两张图进入 Unity 后都变成 1024×1024。 |
| Asset Lab Camera | 有问题，是“资产像小点”的直接原因 | 旧实验室固定使用远相机和大空场景，不按 visible bounds 构图。 |
| World Scale | 有问题，是候选场景比例异常的原因 | 旧视觉尺寸与 PNG/人工设置耦合，没有以 footprint 和可见边界作为唯一世界尺寸来源。 |
| WebGL | 旧构建继承了错误导入结果，不是浏览器单独把清晰图变糊 | 新构建在 844×390、DPR 1 下读取到 A 1211×1299、B 1254×1254；Canvas 内部分辨率和 CSS 尺寸均为 844×390。 |

## 修了什么

### 1. Production 2.5D Rendered Assets 导入策略

新增只作用于 Manifest 中 `ImportProfile=production-2.5d-rendered` 且位于 `Assets/Resources/Imported/` 的自动策略，不扫描或强改整个项目：

- Texture Type：Default，继续服务当前 MeshRenderer/Quad 管线；
- Alpha Source：From Input；
- Alpha Is Transparency：开启；
- Generate Mip Maps：关闭；
- Filter Mode：Bilinear；在非整数屏幕缩放下比 Point 稳定，同时不再叠加 mipmap、压缩和降采样；
- Aniso Level：1；
- Compression：Uncompressed；
- Max Size：2048；
- NPOT：None，保留原尺寸；
- sRGB：开启；
- Wrap：Clamp；
- WebGL：RGBA32、无 Crunch、Max Size 2048、Mitchell resize；
- 构建前自动校验实际 Unity 纹理尺寸必须等于 Source 尺寸，否则中止。

策略由 AssetPostprocessor 自动应用，也可由批处理重验，不需要产品负责人逐张进入 Inspector 设置。

### 2. Asset Test Lab

资产实验室新增三个明确模式：

- Inspect：按真实 bounds 自动构图，主体目标占画面约 60%，并限制在 45%–70% 范围；PREV/NEXT 后重新计算，不使用逐资产相机坐标。
- Context：使用 Manifest 的真实 world size，同屏显示 1 Unity Unit 地砖和真实主控理发师。
- 100% Pixel：棋盘背景，按 1 texture pixel : 1 screen pixel 显示；源图大于视口时只显示其中一部分，这是像素检查，不是完整构图。

Inspect 的 debug overlay 可显示 pivot、footprint、collision、sorting baseline、shadow 和已有 anchors；开发 UI 不进入正式 HUD。

### 3. 世界尺寸与 Manifest

明确唯一优先级：

- 家具世界尺寸：由 footprint、有效 visible bounds 和 `ScalePolicy` 决定；
- PNG 像素尺寸：只用于清晰度和纹理预算校验，不直接决定家具大小。

当前结果：

- A `furniture-magazine-rack-wood`：`DesiredWorldSize=2.614×2.804` Unity units；
- B `furniture-wash-station-vintage-right-wall`：`DesiredWorldSize=4.578×4.578` Unity units。

Manifest 已统一记录 Source Pixel Size、Visible Bounds、Desired World Size、Pivot、Footprint、Collision、Scale Policy、Import Profile、Sorting、Shadow Mode 和交互锚点。

### 4. 候选场景遮挡缺陷

最终复核时稳定复现了一个真实问题：A 在 Asset Lab 清楚，但候选场景的 floor-pivot depth baseline 太靠近后墙，几乎整张 PNG 被墙遮住。已实际使用 `salon-bug-fix`：

- 只调整 candidate 验证位置，不修改正式 Demo 布局；
- 先增加失败回归测试，再把验证点移到墙体遮挡平面前；
- 修后 A 完整可见，B 的服务流程仍通过；
- 原图、Manifest 世界尺寸和正式玩法均未为掩盖问题而改动。

## A/B 对比

A 和 B 使用相同 Camera、World Scale、Quad Size、844×390 和 WebGL，只切换导入策略。

### 普通资产 A

- 旧设置：[ab-a-legacy-magazine-rack-same-frame-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/Round2_1/ab-a-legacy-magazine-rack-same-frame-844x390.png)
- 新设置：[ab-b-optimized-magazine-rack-same-frame-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/Round2_1/ab-b-optimized-magazine-rack-same-frame-844x390.png)
- 同框整图边缘均值 3.546 → 4.214，Laplacian 方差 210.94 → 277.01；书本边缘、木架切面和细线更容易辨认。

### 单向交互工位 B

- 旧设置：[ab-a-legacy-wash-station-same-frame-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/Round2_1/ab-a-legacy-wash-station-same-frame-844x390.png)
- 新设置：[ab-b-optimized-wash-station-same-frame-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/Round2_1/ab-b-optimized-wash-station-same-frame-844x390.png)
- 同框整图边缘均值 3.051 → 3.772，Laplacian 方差 177.98 → 278.31；椅背分面、底座边线和水槽配件更清楚。

新版没有增加锐化滤镜，也没有修改 PNG。改善来自保留源分辨率并移除不适合这类资产的降采样、mipmap 和压缩组合。

## Asset Lab 新用法

### Unity Editor

1. 使用项目声明的 Unity 6000.5.8f1 打开 `unity-hair-salon`。
2. 打开 `Assets/Scenes/AssetTestLab.unity` 并 Play。
3. 用 PREV/NEXT 切换资产；用 Inspect、Context、100% Pixel 切换查看模式。
4. Inspect 中打开 diagnostics 可看 pivot、footprint、collision、sorting、shadow 和 anchors。

### 自动浏览器证据

- 重建并检查资产实验室：`.agents/skills/salon-visual-qa/scripts/run.sh asset-lab`
- A 的 stable ID：`furniture-magazine-rack-wood`
- B 的 stable ID：`furniture-wash-station-vintage-right-wall`
- 构建入口：`unity-hair-salon/Builds/WebGLAssetLab/index.html`
- 所有截图：`unity-hair-salon/Builds/PipelineEvidence/`

## 真实场景结果

- A：Context 中约为人物高度的 1.2 倍、占约 1.7×0.75 个地砖 footprint，落地正常；候选场景墙体遮挡问题修后完整可见。
- B：Context 中约为人物高度的 1.6–1.8 倍，占约 3.2×2.6 个地砖 footprint，符合含底台、躺椅和水槽的一体工位体量；右墙方向没有镜像、旋转、负缩放或伪造第二方向。
- 两件 PNG 自带自然阴影，因此 `shadowMode=baked`，程序阴影关闭；未发现双重阴影。
- 候选 B 继续绑定既有洗发工位逻辑和已有状态机；顾客对齐、理发师到位、Shampoo 开始、洗→剪→吹、完成、离店和支付全部通过。
- Candidate 仍是开发验证场景，没有永久替换正式主场景资产。

体验入口：

- 候选场景：`Assets/Scenes/CandidateAssetValidation.unity`，或运行 `.agents/skills/salon-visual-qa/scripts/run.sh candidate`；
- 正式 Demo：`Assets/Scenes/HairSalonDemo.unity`，或运行 `.agents/skills/salon-visual-qa/scripts/run.sh demo`；
- 候选浏览器截图：[candidate-overview-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/candidate-overview-844x390.png)、[candidate-alignment-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/candidate-alignment-844x390.png)；
- 人物比例截图：[asset-lab-furniture-magazine-rack-wood-context-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/asset-lab-furniture-magazine-rack-wood-context-844x390.png)、[asset-lab-furniture-wash-station-vintage-right-wall-context-844x390.png](../unity-hair-salon/Builds/PipelineEvidence/asset-lab-furniture-wash-station-vintage-right-wall-context-844x390.png)。

## 清晰度结果

- Source：通过。A/B 的原始尺寸与 hash 已记录，透明源图未修改。
- Unity Editor Game View：通过。有效截图位于 `Builds/PipelineEvidence/Round2_1/editor-gameview-*-844x390.png`；一次 `-nographics` 灰图被自动判空并废弃，没有当作通过证据。
- WebGL：通过。运行时尺寸等于 Source，Canvas/CSS 均为 844×390、DPR 1，console 和资源请求无错误；A/B 截图肉眼与清晰度指标均优于旧设置。

## 自动化结果

以后同类 2.5D PNG 接入时，产品负责人不再需要手工设置 Mipmap、Filter、Compression、Max Size、NPOT、Alpha 或 WebGL Format，也不需要逐资产写 Asset Lab Camera。`salon-asset-ingest` 会自动应用策略、检查是否降采样、读取 visible bounds、计算初始 world size、校验 Manifest、构建实验室并触发浏览器证据。

本轮 Skill 结果：

- `salon-asset-ingest`：通过。A、B 均真实执行 `--revalidate`；该模式不会复制或替换 PNG。
- `salon-visual-qa`：通过。已生成 Source、Inspect、Debug、Context、Pixel、Candidate、Demo 三层证据；没有产品批准基准的主观项目继续标记 `baseline-missing`，不会伪称已获美术批准。
- `salon-bug-fix`：本轮真实调用。修复候选场景后墙遮挡普通资产的问题，并增加稳定回归测试。

重复接入结果：

- A、B 各只有一个稳定 ID、一个 Manifest 项、一个 Imported PNG 和一个原始安全归档；
- 重验后仍是 candidate；
- 没有第二个同名 Prefab、重复候选对象、重复 shadow 或垃圾副本；
- approved 覆盖保护测试继续通过。

## 测试结果

| 项目 | 结果 |
|---|---|
| Node 资产管线 | 通过，12/12 |
| Unity 全量 EditMode | 通过，507/507；XML `Passed`，失败 0 |
| Manifest / 资源 /生产纹理策略校验 | 通过 |
| Asset Lab Inspect / Context / Pixel | 通过，两件真实附件均覆盖 |
| Unity Editor Game View | 通过 |
| WebGL Asset Lab build | 通过 |
| WebGL Candidate build | 通过 |
| WebGL 正式 Demo build | 通过 |
| Chromium 844×390 / DPR 1 | 通过 |
| 浏览器 console / 失败资源请求 | 通过，0 错误 |
| 候选服务回归 | 通过，洗→剪→吹→离店→支付 |
| 正式 Demo 核心流程 | 通过 |
| A/B 同条件对照 | 通过，A、B 两件资产均覆盖 |
| 重复接入 / 幂等重验 | 通过，A、B 均覆盖 |
| 第二方向 | 未覆盖；当前没有真实素材，本轮没有生成或伪造，也不构成阻塞 |

结构化机器结果：

- `unity-hair-salon/Builds/PipelineEditMode.xml`
- `unity-hair-salon/Builds/PipelineEvidence/browser-check-all.json`
- `unity-hair-salon/Builds/PipelineEvidence/visual-qa-report-asset-lab.json`
- `unity-hair-salon/Builds/PipelineEvidence/visual-qa-report-candidate.json`
- `unity-hair-salon/Builds/PipelineEvidence/visual-qa-report-demo.json`

## 已知限制

- 844×390 只是当前固定验证基准，不代表最终发布分辨率。
- 机器可以确认资源、尺寸、构图、运行流程和错误日志；风格融合、最终比例与锚点舒适度仍需要产品负责人看截图或可玩版本确认。
- 本轮没有开始批量接入，也没有改造旧场景全部家具的碰撞和排序体系。
- 当前只有 B 的真实 `right-wall` PNG；数据结构可追加真实 visual variant，但本轮没有生成、镜像、旋转或伪造第二方向。

## Round 2.1 结论

**A. 显示与验收环境已经合格，可以继续真实资产流水线验证。**

理由：原始像素已在 Unity 与 WebGL 中保持，Inspect/Context/Pixel 三种用途已分离，世界尺寸有单一数据来源，A/B 证据显示清晰度确实提升，候选场景遮挡缺陷已被 QA 发现并回归修复，正式 Demo 与核心流程未被破坏。此结论只表示可以继续单件/小批候选验证，不表示现在开始批量资产导入或两件 candidate 已自动获得美术批准。
