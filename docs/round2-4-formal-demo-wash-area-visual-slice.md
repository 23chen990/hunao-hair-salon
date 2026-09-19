# Round 2.4：正式 Demo 洗发区视觉纵切

## Visual Gap Audit

### Room Framing

- 事实：844×390 正式 Demo 总览中，店铺主体约占 64% 屏宽，左右有大面积灰色外部留白；批准效果图中的营业空间接近满屏。
- 事实：当前玩家实际可见房间仍是原有 `Fixed Salon Map`，本轮没有重建或移动房间。
- 判断：主要差距是正式 Demo 的总览 framing 与资产完成度，不应通过独立假店铺反推主场景布局。

### Camera / Projection

- 事实：正式 Demo 使用正交相机，overview size 8.35，位置 `(0,18,-19)` 并朝向房间；本轮没有改变其默认 projection、旋转或 zoom。
- 判断：批准效果图包含非严格 3D 的绘制关系，不能据其像素边缘强制重建相机。当前相机不是本轮首要错误。
- 风险：永久修改相机会同时改变房间可见范围、点击射线的视觉预期、世界 UI 与移动反馈，因此必须另开受控任务，不能为这块纵切顺手修改。

### Player Scale

- 事实：总览证据中的正式主控可见高度约为视口 14%–15%；批准图中站立人物通常约为 25%–30%，视觉权重明显更高。
- 事实：本轮同镜头洗发区 After 中，主控约为视口 23%，wash 约为 29%，wash/player 屏幕高度约 1.26。
- 判断：总览 framing 是人物看起来过小的重要原因；局部纵切里人物与真实 wash 已接近可判断的相对权重。

### Furniture Scale

- 事实：只比较了真实 wash PNG、真实杂志架和正式主控，没有用简化沙发、桌椅或柜台建立产品 baseline。
- 事实：wash 在局部 After 不再明显像人物两倍以上的展示模型，但仍保持 `NEEDS-REVIEW`；本轮视觉适配值没有写回 Manifest 或类别 baseline。
- 判断：真实 wash 与人物之间已基本可读，最终批准仍需放在完成度更高的正式墙地板旁复核。

### Scene Density

- 批准图洗发区包含连续工位、顾客、服务车、墙架、植物和相邻营业区，空白被活动关系组织起来。
- 正式 Demo 洗发区已有两个服务点和墙架位置，但大量物体仍是程序占位几何；这首先是资产尚未替换，不足以证明既有服务布局必须推翻。
- 本轮只放入一套真实 wash 和一个真实杂志架，没有复制资产填密度。

### Wall / Floor

- 正式 Demo 仍使用 1-unit 大格程序地砖，横向约 21 格，棋盘感明显；批准图地面更连续、细密，视觉密度约为当前的两倍量级。
- 当前墙是纯色、厚实体程序块；批准图墙体主要是细腻背景、装饰承载和空间边界。
- 仓库没有可直接采用的正式墙面/地板 PNG。本轮按约束保留当前最接近正式的墙地板，没有生成、重绘或擅自设计替代品。

### Art Consistency

- 正式主控、真实 wash 和真实杂志架都采用柔和、带体积和自然阴影的 Low Poly 渲染，三者开始属于同一视觉语言。
- 当前墙、地板、邻近洗发床、中央椅和部分背景仍是扁平程序几何，和三件真实资产存在明显完成度断层。
- 结论：真实资产组内部开始统一，但整个洗发区域尚未达到明确的“同一个完整游戏世界”。

## 实施边界

- 唯一正式集成场景：`Assets/Scenes/HairSalonDemo.unity`。
- `washAreaVisual=before|after|context` 仅在 Editor/Development Build 生效；正式默认状态未永久替换 candidate。
- After 仅关闭第一个 wash 的四个旧 renderer；旧 colliders 全部保留，`CustomerSeatAnchor`、`PlayerServiceAnchor`、`CustomerUIAnchor` 全部保留。
- 没有修改服务状态机、站点 ID、原始 PNG、方向、阴影模式或第二方向。

## A / B / C / D

- A：`Builds/PipelineEvidence/wash-area-approved-reference-crop-844x390.png`
- B：`Builds/PipelineEvidence/wash-area-demo-before-844x390.png`
- C：`Builds/PipelineEvidence/wash-area-demo-after-844x390.png`
- D：`Builds/PipelineEvidence/wash-area-player-context-844x390.png`

## 自动检查与截图时序 Bug

- 第一次 B/C/D 稳定被 `DAY 1` 开店暗场覆盖，无法用于验收。
- 失败门禁先被加入；修复后每个页面都等待 `[BROWSER_CORE_FLOW_PASS]`，并对最终截图执行亮度下限检查。
- `salon-bug-fix` 专项 Unity：3/3 通过；修复后浏览器无 console error、无失败资源请求，默认和 After 核心流程均通过。

## 完整门禁

- Node：16/16 通过。
- Unity EditMode：515/515 通过，XML 为 Passed、0 failed。
- Manifest / 生产纹理 / 资源检查：通过。
- WebGL：Demo、Asset Lab、Candidate 与旧 Reference 技术场景均构建成功。
- Chromium：844×390、DPR 1；无 console error、无失败资源请求，Demo 默认与 wash After 核心流程通过。
- 原始 wash、原始杂志架与各自归档、Unity Imported 副本 SHA-256 完全一致。

## 当前结论

这块真实资产纵切比 Before 明显接近批准图的柔和 Low Poly 方向，但答案仍不是明确的“是”。阻塞视觉融合的首要因素已经从 wash 单体比例转移到正式地板、墙面和相邻占位资产的完成度。当前不得开始批量资产生产。

## UI 实现判断（本轮未实施）

批准图的 UI 不是简单加图标，而是固定的视觉层级：顶部日历/天气、金币、满意度、设置；左侧经营入口；底部工具托盘；世界内需求气泡。实现时应保留现有业务数据和按钮行为，只建立统一的 9-slice 木质/奶油色面板、正式图标图集、固定横屏安全区锚点、统一描边/阴影与状态驱动显示。该工作不应与洗发区资产替换混在同一轮。
