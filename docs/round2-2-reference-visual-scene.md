# Round 2.2：正式视觉基准场景

> 日期：2026-08-30（Asia/Shanghai）

## 结论

已建立独立的 `ReferenceVisualScene.unity`。它不挂载 `SalonDemo`，不包含服务状态机，也不替换正式 Demo；Asset Lab 继续只用于透明边缘、pivot、碰撞、锚点等工程检查，正式比例与融合判断改在 Reference Scene 中完成。

本轮以仓库中明确标注为“当前业务总览参考”的 `Docs/VisualReferences/salon-overview-visual-reference.png` 为主要目标。`salon-overview-operations-reference.png` 在仓库说明中被标注为早期参考，因此没有覆盖当前版本。

## Reference Scene 锁定数据

- Camera：正交投影，yaw 45°、pitch 42°、orthographic size 6.50，目标横屏 844×390、DPR 1。
- Ground：14×9 块真实店铺青绿色地砖，每格 1.25 world units，不再使用 Asset Lab 的技术网格作为审批背景。
- Walls：一段后墙、一段右墙，高 3.0 units，含下墙木饰面和顶部浅色收边。
- Player：当前正式 `Characters/Hairdresser` Prefab，reference world height 2.844 units。
- Standard furniture：当前正式等待沙发构型，world size 5.2×2.175×1.6，footprint 5.2×1.6；高度约为人物的 0.765，约占 4.16×1.28 格地砖。
- Service station：当前正式剪发工位配置 `cut-station-classic-poc`，world size 2.45×2.36×2.65，footprint 2.45×2.65；高度约为人物的 0.83，约占 1.96×2.12 格地砖。
- Wash station：保持 `NEEDS-REVIEW`；类别 baseline 的 world size、footprint、人物相对高度和地砖占用全部仍为 0，未把当前未批准尺寸写成类别基准。
- Shadow：统一方向 (0.18, -0.12)、softness 0.82、opacity 0.20；真实 wash PNG 保持自身 baked shadow，不叠加程序阴影。
- Density：最小基准场景目标 footprint coverage 0.20、每 100 格约 7 个场景对象；用同一个 approved 剪发工位重复形成密度上下文，不引入新资产设计。

数据文件：`Assets/Resources/AssetPipeline/reference-visual-baseline.json`。类别记录同时保存 world size、footprint、人物相对高度、地砖占用和 interaction clearance，不再只记录一项独立 world size。

## 与旧 Context 的区别

旧 Asset Lab Context 是固定技术网格，只能确认资源是否加载、是否糊、pivot/footprint/collision 是否可视化。它没有正式墙面、正式家具密度、批准构图或统一类别比例，因此不能单独批准最终尺寸与场景融合。

新 Reference Scene 同时提供：正式横屏相机、等距地砖、后墙/右墙、正式人物、正式普通家具、正式交互工位、接待区、统一阴影和目标密度。wash station 放入的是同一个世界位置，而不是展台式单品预览。

## 与目标效果图的差距

- 当前程序家具仍比效果图中的定稿 Low Poly 家具简化，材质细节和轮廓丰富度不足。
- Reference Scene 是最小审批截面，场景对象数量和角色数量仍低于完整效果图；但地面占用密度已用固定指标锁定，不再是空白技术网格。
- 墙面层次、货架/植物/瓶罐装饰和接待区细节比效果图少。
- 统一接触阴影方向已经建立，但阴影层次和效果图的环境光遮蔽仍有差距。
- 正式 HUD 不放进 Reference Scene，避免把审批环境变成另一套可玩场景；side-by-side 对照只比较世界画面语言。

这些差距被保留为已知事实，没有宣称像素级复刻或视觉已获产品批准。

## wash station 新结果

放入 Reference Scene 后，wash station 不再像旧技术 Context 那样是单独展示模型；它与人物、两张正式剪发椅、沙发、地砖和墙面同框后，整体尺度进入可判断范围。

当前观察：它不是明显失控的大模型，也不偏小；但底座面积和画面权重仍略强于旁边的正式剪发工位，暂时判断为“基本合理、略偏大，继续 NEEDS-REVIEW”。本轮没有缩放它，也没有保存 wash-station 类别 baseline。

## 自动检查

- Node 资产管线：14/14 通过。
- Unity EditMode：512/512 通过。
- Manifest、纹理生产导入策略、资源和四个场景校验：通过。
- WebGL：正式 Demo、Asset Lab、候选服务场景、Reference Scene 全部构建通过。
- Chromium：844×390、DPR 1；四个场景全部启动，无 console error、无失败资源请求。
- 正式 Demo 核心流程：通过。
- 候选洗→剪→吹→离店支付回归：普通模式和 debug 模式均通过。
- `salon-visual-qa`：已真实运行 reference 模式，输出总览、并排、wash、debug 和旧/新 Context 对比证据。
- `salon-bug-fix`：本轮未调用；没有发现可稳定复现且需要改代码的 Bug。

## 截图

- `reference-visual-overview-844x390.png`：Reference Scene 总览。
- `reference-visual-side-by-side-approved-vs-runtime.png`：当前批准效果图与 Reference Scene 并排。
- `reference-visual-wash-station-844x390.png`：wash station 放入 Reference Scene。
- `reference-visual-debug-844x390.png`：人物、地砖、类别 footprint 与状态调试层。
- `old-context-vs-reference-visual-context.png`：旧工程 Context 与新正式 Context。

全部位于 `unity-hair-salon/Builds/PipelineEvidence/`。

## 唯一下一步

请产品负责人先确认这张 Reference Scene 可以作为视觉审批环境；确认后即可用它审批下一批资产，不再回到 Asset Lab 纯网格做最终比例判断。
