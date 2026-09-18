# Round 2.3：基于批准效果图的 Overlay Calibration

> 日期：2026-08-30（Asia/Shanghai）

## 结论

**A. 视觉坐标系已经足够接近批准参考，可以开始逐类资产校准。**

这里的 A 只表示 Camera、房间 framing、人物屏幕权重、地板纹理密度和视觉墙体已经进入同一套坐标系；不表示 wash station 已批准，也不表示可以批量生产资产。

## 唯一视觉真源

- 原图：`Docs/VisualReferences/salon-overview-visual-reference.png`
- 原图尺寸：1672×941。
- SHA-256：`b358fd3f38b6aac7400357623c01af24e8c27e919b44e0cc4b8820e8cc1265af`。
- 开发 Overlay 副本与原图 hash 一致；原图未修改。
- 844×390 Overlay 使用 `cover-center`，与浏览器截图采用完全相同的居中裁切。

## 按要求执行的校准顺序

### Step 1：Room Framing

先固定批准图的房间像素范围，再调整当前房间占屏。旧场景房间横向约占 75%；新场景投影宽度为 91.6%、高度为 91.1%，批准标尺为约 100% / 93.2%。后排真实工位已移出上边界裁切区。

### Step 2：Camera

旧场景是 yaw 45° / pitch 42° 的标准工程等距感。Overlay 显示纵深投影过高，因此新场景改为从批准图测得的压缩视角：正交投影、yaw 42°、pitch 28°。地面投影高度从第一轮 Overlay 的 168.1% 降到 91.1%，接近批准标尺 93.2%。

### Step 3：Player Scale

批准图选取中央站立理发师，源图 bounds 为 x=819、y=397、w=108、h=267。按 `cover-center` 映射到 844×390 后，目标人物屏幕高度约为 34.5%。当前正式主控角色实测 34.8%，旧场景约为五分之一视口高度。

### Step 4：Floor Density

正式截图不再绘制 14×9 大方格。新的视觉地板是一张连续平面，使用批准图青绿色地板色域和 56×36 的细密纹理频率；14×9 工程网格仅在 debug 模式显示。浏览器会自动拒绝 WebGL Shader 洋红回退。

### Step 5：Wall Presentation

`Visual Walls` 与 `Collision Walls` 已分离：视觉墙使用薄墙、低露出和薄顶面；碰撞墙保留较高、较厚的 BoxCollider，但没有 Renderer，不再把碰撞体厚度完整画出来。

## 相对视觉指标

| 指标 | 批准图标尺 | 新场景实测 | 结果 |
|---|---:|---:|---|
| playerScreenHeight / viewportHeight | 34.5% | 34.8% | 通过 |
| roomScreenWidth / viewportWidth | 约 100% | 91.6% | 通过容差，仍略窄 |
| roomScreenHeight / viewportHeight | 93.2% | 91.1% | 通过 |
| stationScreenHeight / playerScreenHeight | 约 1.05 | 1.219 | 坐标系可用，wash 本身继续复核 |
| stationFootprint / visibleFloorArea | 约 4.4% | 4.375% | 通过 |
| visible floor texture columns | 约 50+ | 56 | 通过 |

这些是浏览器运行时从实际 Renderer 和房间投影读取的屏幕指标，不用 Unity Unit 对产品结果作主要说明。

## Furniture 与 Density

- 最终截图使用当前正式主控角色、真实 wash station PNG 和真实杂志架 PNG。
- 简化沙发、桌子和柜台已退出最终 visual baseline；工程占位只允许在 debug context 出现。
- `low-density`：一名正式角色、一个真实 wash station、一个真实普通家具。
- `target-density`：三个同方向真实 wash station、六个真实普通家具和四名正式角色，用于形成接近批准图的沿墙家具区与中部活动区。
- 所有 wash station 都使用真实存在的 `right-wall` 图；没有镜像、旋转、负缩放或第二方向。

## Overlay Calibration Mode

- `?density=target-density&overlay=1&overlayOpacity=0.50`：50% Overlay。
- `overlayOpacity=0..1`：任意透明度。
- `O`：快速开关 Overlay。
- `+ / -`：按 10% 调整透明度。
- `S`：开发用 side-by-side。
- Overlay Canvas 仅存在于独立 `ReferenceVisualScene`，正式 `HairSalonDemo` 不包含该组件或参考图。

## 当前还差什么（按视觉影响排序）

1. 真实家具种类仍只有 wash station 和杂志架，批准图中的剪发镜台、正式沙发、接待台、墙柜和植物尚未逐类进入同一校准流程；这是当前最大视觉差距。
2. wash station 的屏幕高度约为人物的 1.219 倍，高于当前批准标尺约 1.05，继续保持 `NEEDS-REVIEW`，本轮没有写入 wash-station 类别 baseline。
3. 房间横向占屏为 91.6%，比批准图仍窄约 8%；目前处在自动容差内，但后续正式墙柜进入后要复核边缘拥挤度。
4. 参考图的材质层次、墙面装饰和环境阴影更丰富；当前坐标系已对齐，但不是家具美术复刻。

## Visual QA

- resources：通过；无缺失纹理、无洋红 Shader fallback、无浏览器资源失败。
- proportion：通过坐标系阈值；wash 单体继续 NEEDS-REVIEW。
- position：通过；房间 framing 和后排工位不再被错误裁切。
- direction：通过；所有真实 wash 都只使用 `right-wall`。
- shadow：通过本轮范围；两件真实 PNG 继续使用 baked shadow，没有叠加程序阴影。
- occlusion/depth：通过当前静态 Reference；候选服务场景的动态遮挡/服务回归另行通过。
- character size：通过；34.8% 对 34.5% 标尺。
- safe area：通过；844×390 无关键对象越界。
- stability：通过；WebGL settle 后截图，无 console error。

## 自动检查

- Node：15/15 通过。
- Unity EditMode：512/512 通过。
- WebGL：正式 Demo、Asset Lab、候选场景、Reference Scene 均构建成功。
- Chromium：844×390、DPR 1；四个场景均启动，无 console error 和失败资源请求。
- 正式 Demo 核心流程：通过。
- 候选洗→剪→吹→离店支付：普通/debug 两种模式通过。
- Reference 指标门禁、target/low density、Overlay、A/B/C/D 证据：通过。
- `salon-visual-qa`：本轮真实使用并扩展现有 reference 模式。
- `salon-bug-fix`：本轮未调用；GLES Shader 问题在校准实现内由测试直接修正，没有进入正式 Demo Bug 流程。

## 四类证据

- A：`reference-visual-approved-reference-original.png`
- B：`reference-visual-old-round2-2-844x390.png`
- C：`reference-visual-new-calibrated-844x390.png`
- D：`reference-visual-overlay-calibration-844x390.png`

全部位于 `unity-hair-salon/Builds/PipelineEvidence/`。

本轮到此停止，不进入批量资产生产。
