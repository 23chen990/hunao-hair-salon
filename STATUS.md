# 《胡闹理发店》当前状态

> 更新时间：2026-09-23（Asia/Shanghai）

## 2026-09-23 当前工作区复核

- 当前工作区改动已落入最新 WebGL 构建；Node 资产管线 `17/17`、UI 字体检查通过、Unity EditMode `638/638`，Demo/Asset Lab/Candidate/Reference 四个 WebGL 构建成功。
- 当前构建真实触控整局报告：`unity-hair-salon/Builds/MobileEvidenceCurrent/report.json`；首日 `3/3` 单、余额 `540`、满意度 `69`、后台吹发期间处理另一位顾客、自动结算、手动金币收取 `0` 次、错误 `0`。
- 当前构建失败/重试报告：`unity-hair-salon/Builds/MobileEvidenceFailureCurrent/report.json`；失败局 `0/3`，重试回到第 1 天，余额 `0`、满意度 `90`、错误 `0`。
- 一局时长已按《胡闹厨房》的可操作关卡节奏调整：正式移动入口为营业 `180` 秒 + 最多 `15` 秒 ClosingGrace；收尾是独立的离场/结算缓冲。达到目标并完成离场时可能提前结算，暂停不消耗营业时间。旧的 120/150 秒记录保留为历史决策过程；当前仍需真人试玩记录实际首局时长和完成率。
- 调整后的真实触控证据：`Builds/MobileEvidenceSecondCustomerAcceptanceOvercooked180/report.json` 正确接待路径通过，`Builds/MobileEvidenceWrongStationOvercooked180/report.json` 错误工位→纠正路径通过，均为 844×390、浏览器错误 `0`。
- 旧的 `TECH_HANDOFF.md` 仍保留 9 月 18 日历史审计；当前接续入口已补在其 §17，后续以本节、`docs/gameplay-repair-2026-09-20.md` 和最新实玩复现为准。

## 当前：玩法与操作修复（2026-09-20）

- 按产品负责人最新反馈优先修玩法体验，暂不做 UI 视觉调整。
- 移动端剪发按实际按住/松手判定，支持提前松手后补剪、后续多步工具顺序；暂停保留进度，多指点暂停从按下时即生效。
- 接待新客时仍可给在座顾客收尾；已接待目标不再硬锁下一次交互，玩家靠近在座顾客时可以先冲洗、剪发或吹发收尾，再回来安排新客。
- 实玩首日发现接待后跑向工位期间顾客仍按完整排队速度掉耐心；现在接待成功会进入护送宽限，玩家到工位前不会继续扣这位顾客的等待耐心，首日基础掉耐心也调到可教学的节奏。
- 失败订单不计目标；收客同时考虑已完成与未完成订单，避免目标 3 单却接进 8 人再被清场扣分，流失后可补客。
- 按最新反馈移除金币堆玩法：完整订单完成后收入立即结算到余额，不再生成金币堆、靠近收取或在闭店时兜底拾取。
- 补回完整“洗 → 吹 → 付款”订单回归，确认多步骤订单不会在洗发后提前结算；最新门禁：Node 17/17、Unity EditMode 628/628、四个 WebGL 构建和 844×390 Chromium 场景检查通过；移动入口整局触控 3/3 单、余额 540、无浏览器错误。
- 第二位顾客接待回归已覆盖：订单收入即时结算后，安排工位不会再被上一单的待收金币交互打断；真实触控记录在 `Builds/MobileEvidenceNoCoins/`。
- 设备占用现在参与可见操作门禁：下一类兼容工位全部被占时，不再给出可按的“转移顾客”，而是显示“等待空闲工位”；释放工位后才恢复转移，避免把不可执行任务当成最高优先级。
- 最新回归：Node 17/17、字体字符检查通过；Unity EditMode 629/629；Manifest/资源检查、四个 WebGL 构建和 844×390 Chromium 四场景检查通过；新构建真实触控首日 3/3 单、余额 540、满意度 69、后台吹发期间完成另一项服务、无浏览器错误。
- 日结生命周期已修复：达标后停止接待新客，但 `Finished/Leaving` 顾客会保留到真正 `Exited`，最后一位顾客先走出店外再显示结算；新增离店时序回归测试。
- 最新离店修复门禁：Unity EditMode 630/630，真实触控报告位于 `Builds/CustomerExitTimingFinal/report.json`，三位顾客均记录到 `Finished → Leaving` 后才进入结算，无浏览器错误。
- 接待上限与离场显示现在使用两套计数：`Finished/Leaving` 仍保持日结和离场动画，但不再重复占用“未完成订单”名额；新增 `MobileAdmissionDoesNotCountFinishedCustomersStillLeavingAsUnfinishedOrders` 回归。
- HTTP 试玩：`http://127.0.0.1:8910/WebGLDemo/`。下次可在 Finder 双击根目录 `启动试玩.command`，自动打开本地 HTTP 版本。
- 本轮复现、验证与限制见 `docs/gameplay-repair-2026-09-20.md`。历史阶段认可不代表最新版本已通过产品体验验收。

## 试玩入口整合（2026-09-20）

- 正式试玩只保留 `Assets/Scenes/HairSalonDemo.unity` 这一入口；WebGL 与 macOS 构建均由它生成，代码已推送到 GitHub 当前分支。
- 正常试玩默认显示完整三维沙龙场景，并复用同一套移动玩法；`WASD`/方向键、鼠标拖动摇杆和触摸摇杆都能移动，右侧按钮或 `Space` 执行就近操作。
- 2D 灰盒只保留给显式 `?simple2D=1` 的诊断检查，不再作为产品试玩版本；Asset Lab/Candidate/Reference 仍是技术验证场景。

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

## 2026-09-23 顾客接待与离场回归修复（当前有效）

本轮针对产品负责人指出的两个具体问题完成复现、最小修复和真实触控复核：

- 第一个顾客离开时瞬间消失：旧离场路径穿过剪发工位碰撞，`UpdateCustomerViews` 每帧把 `Leaving` 顾客回滚；顾客随后被模型移除，所以画面只看到瞬间消失。现在离场路径先进入低位安全通道，再沿左侧出口离开；`Leaving` 不再被剪发工位移动回滚，路线也有正式工位/出口植物碰撞回归测试，并把离场时长调到足够走完整条路线。
- 第二个顾客无法接待：旧的 `_mobileGuidedCustomer` 可能残留为已经离场的第一位顾客，移动目标搜索会跳过后续等待顾客；同时排队顾客只有抵达目标点后才被视为可接待。现在每次移动交互前清理失效引导对象，等待顾客可以直接成为接待目标，接待和护送动作分开执行，不会把“转移顾客”误当成“接待顾客”。
- 日结提前清场：结果状态原先停止更新视图，导致 `Finished/Leaving` 顾客在离场动画完成前被清掉。现在有可见离场旅程时延迟结果面板，持续更新到顾客进入 `Exited` 后再结算。

专项回归：`ReportedGameplayRegressionTests` 为 3/3，`CustomerExitRouteRegressionTests` 为 2/2；全量 Unity EditMode 为 635/635，Node 资产管线为 17/17，四个 WebGL 构建和 844×390 Chromium 检查均通过。真实触控报告 `unity-hair-salon/Builds/MobileEvidenceReportedFinal3/report.json` 记录首位顾客最大屏幕位移 226.08px、到达出口距离约 0、并在第一位顾客仍为 `Leaving` 时成功接待第二位（目标顾客 id 1，动作 `Greet`）。失败重试报告为 `unity-hair-salon/Builds/MobileEvidenceReportedFailureFinal2/report.json`。

对应截图为 `unity-hair-salon/Builds/MobileEvidenceReportedFinal3/14-first-customer-leaving.png`；全量门禁结果为 `unity-hair-salon/Builds/PipelineEditMode.xml` 与 `unity-hair-salon/Builds/PipelineEvidence/browser-check-all.json`。这部分内容取代本文件中较早的 630/630 和旧移动证据描述；旧报告保留作历史记录，不再作为本轮缺陷已解决的依据。

## 2026-09-23 第二位顾客接待后的指引断档修复（当前有效）

产品负责人再次反馈“第二个顾客还是无法正常接待”后，重新用真实 Chromium 触控复现。此前版本其实把第二位设置成 `guided=1`，但玩家仍在排队区时，目标搜索因距离洗发锚点超过 1.5 而返回空目标，右侧按钮退回灰色“靠近顾客”，排队卡片也仍显示等待状态。这会让玩家无法知道接待已成功、下一步应该去哪。

本轮修复 `SalonDemo.Mobile.cs`：引导顾客始终保留为当前目标；没有到达兼容工位时显示禁用的“前往洗发工位”，目标仍指向第二位；到达空闲洗发锚点后切换为可用的“安排洗发”。没有改变订单、工位布局或 UI 视觉方向，只补齐了接待后的连续指引。

新增 `SecondCustomerReceptionRegressionTests.GuidedWashCustomerExplainsNextStepAndCanBeAssigned`，覆盖首日真实 O002（洗发→吹发）完整首段：第一位离场期间，第二位 `Greet` 可用；触摸后目标仍为第二位、动作 `Assign`、按钮显示“前往洗发工位”；到站后“安排洗发”可用，执行后第二位进入 `MovingToStation` 并分配到洗发工位 0。

最终真实触控证据：`unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptance/report.json`，其中 `secondCustomerReceptionFlow.passed=true`、`greetTouch.targetCustomer=1`、`guidedTarget.action=前往洗发工位`、`assignBeforeTouch.action=安排洗发`、`assignedCustomer.state=MovingToStation`，浏览器错误为空。截图为 `15-second-customer-greet-ready.png`、`16-second-customer-guided-to-wash.png`、`17-second-customer-wash-assign-ready.png`、`18-second-customer-assigned-to-wash.png`。

专项回归为 `SecondCustomerReceptionRegressionTests` 1/1、`ReportedGameplayRegressionTests` 3/3、`CustomerExitRouteRegressionTests` 2/2；本轮完整门禁为 Node 17/17、Unity EditMode 636/636、四个 WebGL 构建和 844×390 Chromium 场景检查通过。

## 2026-09-23 错误工位路径恢复（当前有效）

产品负责人澄清：第二位顾客 O002 的需求是洗发，之前把她带去剪发区时无法继续，是因为移动入口又把流程错误误当成了物理无效。这个收紧与已经批准的 `ACTION_RULE_MATRIX`、4A.5 报告和 Phase 6 测试冲突；“流程上不正确”应当允许执行，再由模型记录后果。

本轮只恢复这条已经决定的自由度，没有重做订单、房间布局或 UI 方向：

- 接待 O002 后，系统默认仍推荐空闲洗发工位；玩家如果走到任意空闲剪发工位，按钮会显示“安排剪发工位”并保持可用。
- 执行错误安排后，`SalonGameModel.Assign` 记录 `WrongStationCount=1`、`ReactionKind=Confused`，满意度按既定规则下降一次；顾客不会被自动撤销或自动送回。
- 顾客到错工位后，移动入口显示“转移顾客”；玩家可以再带到空闲洗发工位，错误次数和已经扣除的满意度保留，困惑反应清除。
- 空闲工位仍受真实占用限制；没有空位时不提供物理上无法执行的安排。这保持“物理无效可禁用、流程错误可执行”的边界。

新增 `SecondCustomerReceptionRegressionTests.GuidedWashCustomerCanBeAssignedToWrongHaircutStationWithFeedback`，并用真实 844×390 Chromium 触控跑通完整错误→纠正路径。证据在 `unity-hair-salon/Builds/MobileEvidenceWrongStation/report.json`：真实动作依次为“接待 2 号”→“安排剪发工位”→“转移顾客”→“安排洗发”；满意度 `70→67`，错误工位次数为 1，最终回到洗发工位。正确引导证据仍在 `unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptanceFinal/report.json`，没有回归。

最终门禁：Node 资产管线 17/17，UI 字体检查 367/629，Unity EditMode 638/638，Demo/Asset Lab/Candidate/Reference 四个 WebGL 构建成功，844×390 Chromium 场景检查无错误；专项 XML 为 `Builds/MobileAdmissionRegressionFinal.xml`（14/14）、`Builds/SecondCustomerReceptionRegressionFinal.xml`（2/2）、`Builds/ReportedGameplayRegressionFinal.xml`（3/3）和 `Builds/CustomerExitRouteRegressionFinal.xml`（2/2）。最新正确/错误路径证据分别为 `Builds/MobileEvidenceSecondCustomerAcceptanceFinal2/` 与 `Builds/MobileEvidenceWrongStationFinal2/`。

旧的 `docs/development/phase-0-2/MAIN_PATH_AUDIT.md` 中关于“移动入口只给兼容工位”的内容是历史审计快照，不能覆盖后续批准的错误工位规则；当前行为以规则矩阵、模型测试和本节证据为准。
