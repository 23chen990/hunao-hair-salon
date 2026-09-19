# 《胡闹理发店》当前状态

> 更新时间：2026-09-07（Asia/Shanghai）

## 当前最新：正式 Demo 恢复截图中的首版三维洗发区（2026-09-07）

- 产品负责人澄清要恢复的是前后对比图红框中的“改进前”版本，而不是占位原版；正式 Demo 默认现已恢复首版三维洗发台、墙面、货架、植物和镜头。
- 模型回到首版统计：洗发台 3,244 三角面、房间饰面 25,454 三角面。第二轮圆润化、纹理噪声和过细墙块已撤回。
- 默认 Chromium 检查确认加载首版 Crafted Room Finish，画布 844×390、DPR 1，无控制台错误或失败请求；`washCraft=original` 保留为占位原版对照入口。
- 保留服务流程、角色、HUD、碰撞和第二工位寻路修复。

## 此前：洗发区第二轮美术打磨（2026-09-07）

- 洗发台改为鼓面坐垫、包覆扶手与简化底座；陶瓷高光、木材细节、模型缝隙明暗和暖光重新调整。
- 收敛墙地面细节，保留现有布局、角色、玩法和 HUD；模型仍待视觉验收，状态 review。
- 最终 Node 17/17、Unity 544/544、Manifest/资源检查通过。最终 Demo 和资产实验室构建与 Chromium 五项检查通过，包括双工位服务、付款和离场。
- 开发录屏改为等待实际人物状态，修复固定等待秒数在录屏时提前判定失败的问题；游戏规则不变。
- 同角度截图、试玩与录屏见 `docs/wash-craft-polish-2026-09-07.md`。角色及周边占位造型仍限制整店完成度。

## 此前：Blender 洗发区首版（2026-09-07）

- 正式 Demo 已接入原创建模的两张洗发台、共享墙地面及洗发区现有装饰；固定 Unity 6000.5.8f1，现有角色和服务流程保留。
- 本轮用户同意采用 Blender 建模、Unity 光照整合，并再次确认 `salon-overview-operations-reference.png` 为参考。新模型状态为 `review`，技术通过不代表视觉已经批准。
- 原工位碰撞、顾客/理发师/UI 锚点保留；FBX 左右反向有先失败后通过的专项回归证据。
- 最终 Node 17/17、Unity 544/544、新模型专项 6/6、Manifest 与资源检查通过；四个 WebGL 构建以及原有完整 Chromium 回归通过。
- 已修复理发师去第二张洗发台被中央工位阻挡的问题，使用现有碰撞范围计算绕行；3 项路径测试通过，两处实机到达误差均为 0.000。
- 新版两个洗发工位分别通过洗→剪→吹→离店/付款掉落；实际触屏另行验证开始营业、选顾客、安排新工位、启动洗发。
- 本机 Demo：`http://127.0.0.1:8910/WebGLDemo/`；重启服务使用 `python3 tools/serve-salon.py --port 8910`。
- 交付、截图、录屏和限制见 `docs/wash-craft-first-slice-2026-09-07.md`。其余服务区及顾客仍有原有占位造型，整店尚未达到参考图完成度。

以下章节保留此前阶段记录，以本节和产品负责人最新说明为准。

## 当前可运行成果

- Unity 正式 Demo：`unity-hair-salon/Assets/Scenes/HairSalonDemo.unity`。
- 独立资产实验室：`unity-hair-salon/Assets/Scenes/AssetTestLab.unity`。
- 开发专用真实候选场景：`unity-hair-salon/Assets/Scenes/CandidateAssetValidation.unity`，不替换正式 Demo。
- 通用资产 Manifest、inbox 接入工具、统一接触阴影、开发调试覆盖层和 WebGL 自动构建已接入。
- 正式 Demo 的玩法、角色方向、房间布局和正式 UI 没有被重新设计。

## 资产使用审计

### 图片路径

- 角色源图、处理图和方向帧分散在 `Assets/Art/Characters/Hairdresser/`。
- 运行时 UI 图片位于 `Assets/Resources/DemandBubble/` 和 `Assets/Resources/TopHUD/`，通过 `Resources.Load` 字符串路径加载。
- 角色 Prefab 引用了 `Assets/Resources/Characters/` 下资源。
- 新通用资产必须进入 Manifest；inbox 工具只把校验通过的图片放入 `Assets/Resources/Imported/`。

### 尺寸与缩放

- 角色方向帧统一为 384×512，Pixels Per Unit 主要为 180；角色占屏尺寸还受 Prefab 层级缩放影响。
- 多数 UI 位图源尺寸约 1254×1254 或 2172×724，但运行时使用大量独立的 `RectTransform` 目标尺寸，源图比例与显示比例由代码分别维护。
- 世界程序几何通过 `Vector3` 直接定义尺寸；新 Manifest 已为逻辑家具记录 footprint、collision 和 shadow 尺寸，但旧场景尚未全部由 Manifest 生成。

### 锚点与原点

- 多数导入图片使用中心 pivot（0.5, 0.5）；角色落脚和世界交互位置依靠 Prefab 子节点或代码偏移补偿。
- 剪发工位已有顾客、理发师、排队、工具和服务特效锚点，并能统一旋转。
- 洗头、烫发、等待区和收银区仍有部分锚点直接在 `SalonDemo.cs` 中建立；本轮已把逻辑值录入通用 Manifest，但尚未全面替换旧运行逻辑。

### 家具方向

- 正式场景中剪发工位支持后墙向和右墙向，并由同一旋转规则解析。
- 通用 Manifest 只允许 `back-wall`、`right-wall`、`free`；非法方向会阻止接入和构建。
- 资产实验室可以在两种墙向之间切换，用于在接入正式场景前验收。

### 阴影

- 旧角色 Prefab 使用一张独立接触阴影图片。
- 本轮新增程序生成的柔和 `ContactShadow`，阴影不再要求画进家具图片；尺寸、偏移、透明度和柔和度来自 Manifest。
- 当前正式世界家具已开始使用统一组件；没有要求重做现有图片。角色旧阴影暂时保留，避免改变已批准角色表现。

### 深度排序

- 2D 角色与 UI 依赖 `sortingOrder`；3D 程序家具主要依赖相机深度、材质和世界 Z 位置。
- 现有排序值分散在角色构建器、工位配置和 UI 构建代码中。
- Manifest 新增 sorting layer、order、depth offset 和按世界 Z 排序声明；调试模式显示角色位置、目标和深度说明。

### 碰撞

- 剪发工位有结构化 BoxCollider，并在角色移动时执行阻挡检查。
- 其他大量程序家具仍主要是视觉几何，碰撞覆盖不完整或未参与统一移动阻挡。
- Manifest 已要求每个登记资产提供合法 collision 区域；资产实验室会单独显示，正式场景全面消费仍属于后续渐进迁移。

### 人工坐标

- `SalonDemo.cs` 和 `SalonCustomerPath.cs` 中仍存在大量世界位置、路线点、UI 位置和尺寸常量。
- 这既是当前 Demo 快速迭代的主要来源，也是更换家具、移动区域和修复穿插问题时的主要人工成本。
- 本轮没有全面重构；只建立 Manifest、测试实验室和调试证据，为后续逐区迁移提供安全入口。

### 重复配置

- 剪发、洗头、烫发等工位的视觉尺寸、选择底板、碰撞、服务锚点和 UI 锚点在旧代码中分别维护。
- UI 源图片尺寸、运行时显示尺寸和点击区域也分别维护。
- 局部剪发工位 Manifest 与新通用 Manifest 暂时并存；前者仍是正式剪发服务的运行配置，后者是统一接入契约。后续应通过一次受控迁移消除这项双份配置。

## 自动化入口

- `node --test tests/asset-pipeline.test.mjs`：inbox、命名、格式、覆盖保护、预览和 Manifest 更新。
- `tools/check-project.sh`：全量 Unity EditMode 测试、Manifest/资源/场景检查、Demo 与资产实验室 WebGL 构建、真实 Chromium 截图和核心流程。
- 构建日志、测试 XML 和截图位于 `unity-hair-salon/Builds/`。

## 仓库级 Skills

- `.agents/skills/salon-asset-ingest/`：对已确认图片执行格式、透明通道、空白/触边、命名、覆盖保护、Manifest、预览、资产实验室和浏览器截图流程；失败时自动回滚未完成接入。
- `.agents/skills/salon-visual-qa/`：构建指定场景，在真实 Chromium 的 844×390 手机横屏视口中截图，并输出结构化视觉报告；没有批准基准时明确标记 `baseline-missing`。
- `.agents/skills/salon-bug-fix/`：把问题材料、固定复现、分类、回归检查、最小修复、相邻流程和前后截图固化成一条流程。
- 三项 Skill 均已用当前仓库真实任务试运行；当前未创建 Hooks。

## 本轮真实试运行

- 普通资产 A `furniture-magazine-rack-wood` 与单向洗发工位 B `furniture-wash-station-vintage-right-wall` 已用真实附件完成候选接入；原图、归档和 Unity 导入副本 hash 一致。
- B 的 Manifest 只登记 `right-wall`；没有镜像、翻转、负缩放、旋转、生成或重绘第二方向。
- 两件图片都含自然阴影，使用 `baked` 模式并关闭程序阴影。
- 资产实验室显示真实 PNG 与 pivot、footprint、collision、sorting、shadow、锚点诊断；候选场景完成洗发、剪发、吹发、离店和支付回归，正式 Demo 未被 candidate 永久替换。
- `salon-asset-ingest` 与 `salon-visual-qa` 均评为部分通过；`salon-bug-fix` 本轮真实调用并完成专项回归。完整报告见 `docs/round2-single-orientation-real-asset-validation.md`。

## Round 2.1 显示与验收环境修复

- 已确认上一版又小又糊的主因不是原始 PNG：Unity 的 NPOT 默认策略把两张运行时纹理都缩成了 1024×1024，同时启用了 mipmap、普通压缩、Repeat 和不适合固定视角透明家具的默认设置；旧 Asset Lab 又使用固定远相机和人工视觉尺寸。
- 新增仅作用于 Manifest 中 `production-2.5d-rendered` 资源的自动导入策略：保留 NPOT 原尺寸、关闭 mipmap、Clamp、Bilinear、无纹理压缩、Max Size 2048、alpha transparency、sRGB，并为 WebGL 使用 RGBA32/无 Crunch。A 运行时保持 1211×1299，B 保持 1254×1254。
- Asset Lab 现在具有 Inspect、Context、100% Pixel 三种模式；Inspect 依据 bounds 自动构图，Context 使用 Manifest world size、1 Unity Unit 地砖和真实主控理发师作比例参照。
- 世界尺寸不再由 PNG 像素决定，而由 footprint、有效可见边界和 `ScalePolicy` 计算；A 为 2.614×2.804 units，B 为 4.578×4.578 units。
- A/B 同框 WebGL 证据、Editor Game View、最终 Asset Lab 与候选场景截图位于 `unity-hair-salon/Builds/PipelineEvidence/Round2_1/` 和 `unity-hair-salon/Builds/PipelineEvidence/`。
- 候选场景曾稳定复现普通资产被后墙遮成一小条，已通过 `salon-bug-fix` 最小修正候选验证位置并增加回归测试；没有修改正式 Demo 布局。
- `salon-asset-ingest` 已增加不复制/不替换 PNG 的 `--revalidate` 幂等重验入口，两件真实 candidate 均已实际重跑通过；`salon-visual-qa` 已生成 Source、Inspect、Context、Pixel、候选流程和正式 Demo 证据。
- Round 2.1 完整报告见 `docs/round2-1-real-png-display-and-visual-qa.md`。

### Wash station 比例复核

- 产品负责人未批准 B 的 1.00× Context 比例；`furniture-wash-station-vintage-right-wall` 当前状态已改为 `NEEDS-REVIEW`，原始 `DesiredWorldSize=4.578×4.578` 暂时保留为未批准参考值。
- 0.80×、0.85×、0.90× 的单项微调试验已按产品负责人要求停止；这些临时值从未写回 Manifest，也不会作为类别 baseline。
- Round 2.2 已建立独立 `Assets/Scenes/ReferenceVisualScene.unity` 和 `reference-visual-baseline.json`，正式 Context 使用批准效果图、等距相机、1.25-unit 地砖、后/右墙、正式人物、正式家具/剪发工位和统一密度。
- wash station 已放入新 Reference Scene：当前视觉判断为基本合理但画面权重略偏大，继续保持 `NEEDS-REVIEW`；wash-station 类别 baseline 仍为空。
- 完整报告见 `docs/round2-2-reference-visual-scene.md`。

## 最近一次完整门禁结果（Round 2.2）

- Node 资产管线：14/14 通过，新增 Reference Scene 独立构建、浏览器标记和并排证据检查。
- Unity EditMode：512/512 通过；新增相机/地砖/人物/类别关系数据、独立场景结构和正式 Demo 隔离检查。
- WebGL：正式 Demo、资产实验室、候选验证场景和 Reference Scene 均构建成功。
- Chromium：844×390、DPR 1；正式 Demo 核心流程、候选洗→剪→吹→离店支付流程、两件资产 Inspect/Context/Pixel、Reference Scene 普通/wash/debug 均通过；无 console error 或失败资源请求。
- 重复接入：两件 candidate 通过 `--revalidate` 安全 no-op 重验；各只有一个 Manifest 项、一份 Imported 文件、一份原始归档，candidate 状态与 baked shadow 配置不变。
- 结构化报告与截图：`unity-hair-salon/Builds/PipelineEvidence/`。

## Round 2.3 Overlay Calibration（已被产品负责人否决为正式基准）

- 产品负责人拒绝 Round 2.2 作为正式视觉基准后，已停止凭 Unity Unit 微调，改用批准效果图的源像素 bounds 和 844×390 `cover-center` 映射做直接 Overlay 校准。
- Reference Scene 现在支持 0%–100% Overlay、快速开关、side-by-side、`low-density` / `target-density`；全部仅存在于开发场景。
- 正式截图已使用连续细密视觉地板；14×9 技术网格只在 debug 模式显示。视觉墙与不可见碰撞墙已拆分。
- 最终视觉上下文只使用正式主控角色、真实 wash PNG、真实杂志架 PNG；简化沙发、桌椅和柜台不再承担正式 visual baseline。
- 屏幕相对结果：人物高度 34.8%（批准标尺 34.5%）、房间宽 91.6%、房间高 91.1%、wash footprint/可见地面 4.375%、地板横向纹理 56 列。
- wash station 仍为 `NEEDS-REVIEW`；没有保存类别 baseline、没有伪造第二方向。
- 上述工程结果保留为历史技术证据，但产品负责人随后确认其房间轮廓、家具布局、服务区位置、前后台关系和场景密度都不是当前真正游戏布局，因此 Round 2.3 的“A”结论作废。
- 独立 Reference Scene 与 Asset Lab 现在只保留技术检查用途；正式视觉集成和批准回到真正可玩的 `HairSalonDemo`。
- 完整报告见 `docs/round2-3-overlay-calibration.md`。

## 最近一次完整门禁结果（Round 2.3）

- Node 资产管线：15/15 通过。
- Unity EditMode：512/512 通过。
- WebGL：正式 Demo、资产实验室、候选验证场景和 Reference Scene 均构建成功。
- Chromium：844×390、DPR 1；四场景均无 console error 或失败资源请求；Reference Scene 额外通过屏幕指标与洋红 Shader fallback 门禁。
- 正式 Demo 核心流程和候选洗→剪→吹→离店支付回归均通过。

## 当前正式 Demo 洗发区视觉纵切

- 已在 `HairSalonDemo` 同一场景内增加开发专用 `washAreaVisual=before|after|context` 证据状态；不带参数的正式默认 Demo 完全不变。
- After 只替换第一个洗发工位的渲染层，并加入一件真实杂志架作为附近装饰；站点 ID、碰撞、顾客/理发师/UI 锚点和服务状态机未修改。
- 真实洗发台继续保持 `NEEDS-REVIEW`、`right-wall` 单方向、baked shadow；没有镜像、旋转、负缩放、第二方向或 Manifest baseline 写回。
- `salon-visual-qa` 已改为以 `HairSalonDemo` 作为唯一正式 Visual Integration Scene，并生成批准区域裁切、正式 Demo Before、After、Player+wash Context 四类证据。
- 截图时曾稳定复现开店 `DAY 1` 暗场遮住证据，已真实调用 `salon-bug-fix`：新增营业态等待与亮度门禁后重新截图，专项 Unity 3/3 和浏览器核心流程通过。
- 当前视觉结论：真实主控、wash PNG 与杂志架的美术语言开始统一，但大格技术地板、纯色厚墙和邻近占位工位仍明显破坏融合；尚未达到“明显属于批准效果图同一个完整游戏世界”，不得进入批量资产生产。
- 完整报告见 `docs/round2-4-formal-demo-wash-area-visual-slice.md`。

## 最近一次完整门禁结果（正式 Demo 洗发区纵切）

- Node 资产/自动化：16/16 通过。
- Unity EditMode：515/515 通过，XML 明确为 Passed、0 failed。
- Manifest、生产纹理和场景资源校验：通过。
- WebGL：正式 Demo、Asset Lab、Candidate、旧 Reference 技术场景均构建成功；后两者只作回归，不参与本轮视觉批准。
- Chromium：844×390、DPR 1；四场景无 console error 或失败资源请求，正式 Demo 默认与 wash After 核心流程均通过。
- 原图保护：wash 与杂志架的原始附件、归档、Unity Imported 三份 SHA-256 分别完全一致。

## 当前主要技术债

1. 主场景仍由一个大型运行时脚本集中创建，世界布局没有独立场景数据文件。
2. 通用 Manifest 尚未成为所有正式家具的唯一数据源；剪发工位存在兼容层。
3. 除剪发工位外，正式移动碰撞尚未系统性接入每件家具的 Manifest collision。
4. 部分 UI 资源的 Unity 导入类型仍为 Default Texture，而不是 Sprite，运行时依靠自建 Sprite 或特殊加载逻辑。
5. 角色源图存在非透明背景版本，处理后的方向帧透明区差异较大；当前不影响已批准 Demo，但替换动画时需先在实验室核对脚底稳定性。
