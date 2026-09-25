# 《胡闹理发店》第二轮单向真实资产生产验证报告

> 验证日期：2026-08-30（Asia/Shanghai）  
> 当前横屏验证基线：844×390，仅用于本轮自动化检查，不代表最终生产目标分辨率。

## 1. 真实资产接入结果

### 普通资产 A

- 原图：`像素风木质杂志架与柔和阴影.png`，1211×1299，RGBA，1,221,440 bytes。
- 稳定 ID：`furniture-magazine-rack-wood`；状态：`candidate`；方向：`free`。
- SHA-256：`00bf361949d088a5859f675c5eeb8925aff6923539d643e5a3f68a3b3245a9cb`。
- 下载目录原图、安全归档、Unity 导入副本三者 hash 完全一致，处理流程没有改图。
- 可见 Alpha 边界为 `[0,0,1195,1299]`，有效 Alpha（>16）边界为 `[128,5,1072,1291]`。仅极低透明度像素触边，有效内容没有触边；未发现纯白背景和全透明像素的 RGB 色污染。
- 原图含柔和自然阴影，因此登记为 `shadowMode=baked`，程序阴影关闭，没有叠加第二层阴影。

### 单向交互工位 B

- 原图：`等距视角复古洗发椅工作站.png`，1254×1254，RGBA，1,139,156 bytes。
- 稳定 ID：`furniture-wash-station-vintage-right-wall`；状态：`candidate`；唯一方向：`right-wall`。
- SHA-256：`d0ffacbb09f847154ff08458ae54621b256742c8fc6fa33b715566401c070cbb`。
- 下载目录原图、安全归档、Unity 导入副本三者 hash 完全一致，处理流程没有改图。
- 可见 Alpha 边界为 `[25,78,1238,1254]`，有效 Alpha（>16）边界为 `[45,80,1216,1240]`。底边仅有极低透明度阴影像素触边，有效内容没有触边；未发现纯白背景和透明边缘色污染。
- 原图含自然阴影，因此登记为 `shadowMode=baked`，程序阴影关闭。
- 没有镜像、水平翻转、负缩放、旋转图片、重新生成或重绘。

## 2. Manifest 与数据化结果

两件资产都已进入 `Assets/Resources/AssetPipeline/asset-manifest.json`，并记录稳定 ID、显示名、分类、资源路径、candidate 状态、真实方向、默认方向、pivot、显示尺寸、footprint、collision、sorting、shadow、来源 hash、原路径、宽高、文件体积和可见边界。

普通资产 A 当前参数：pivot `(0.5,0)`；显示尺寸 `1.8×1.9301`；footprint `1.7×0.75`；collision `1.5×0.65`；sorting order `3`；baked shadow；候选摆位 `(4,0,6.75)`。

工位 B 当前参数：pivot `(0.5,0)`；显示尺寸 `3.35×3.35`；footprint `2.7×3.5`；collision `2.2×3.05`；sorting order `4`；baked shadow；候选摆位 `(9,0,-0.3)`。唯一可用和默认方向都是 `right-wall`。

候选场景的角色类型、世界坐标、需隐藏的旧场景物体、目标逻辑工位和工位编号也已进入 Manifest，不再写死本轮两个资产 ID 或坐标。未来增加同类 candidate 时，运行时代码读取同一结构。

## 3. 资产实验室与候选游戏场景

资产实验室现在读取真实 PNG，不再用程序占位家具冒充导入结果。A、B 均可查看原图、pivot、footprint、collision、sorting baseline、shadow 范围、asset ID、candidate 状态和锚点。

候选游戏场景为 `Assets/Scenes/CandidateAssetValidation.unity`，仅用于 Editor/Development Build。正式 `HairSalonDemo.unity` 没有永久替换为 candidate，也没有合入调试 HUD。

当前画面事实：

- A 放在后墙货架位置，接地、未下沉、未见双重阴影；在 844×390 全景中偏小，是否为最终比例仍需产品负责人确认。
- B 位于右墙区域，没有与烫发工位重叠，接地、未下沉、未见双重阴影；相对人物约为合理的大型洗发设备候选比例，但最终大小和位置仍需产品负责人确认。
- 角色到工位前后时，整张透明 PNG 通过 Alpha 裁切并写入深度，人物不会被透明矩形错误遮住。单张扁平 PNG 不能把椅子扶手、椅背等内部部件拆成独立前后遮挡层，这是当前素材结构的限制。
- collision 已从 Manifest 建立 BoxCollider；debug 截图可查看实体范围。正式场景中所有旧家具尚未全部迁移到同一碰撞数据源，因此本轮只宣称候选资产的配置和实例已接入，不宣称全场碰撞迁移完成。

## 4. 单向交互工位 B 与服务逻辑

工位 B 没有重写 `SalonGameModel` 的洗发、剪发、吹发、离店或支付逻辑。候选场景把新视觉和数据锚点绑定到现有洗发逻辑工位，并调用现有服务接口完成回归。

- `CustomerSeatAnchor`：来自 `customer-seat`。
- `PlayerServiceAnchor`：来自 `stylist-work`。
- `QueueAnchor`：来自 `queue`。
- `ToolAnchor`：来自 `tool`。
- `ServiceVFXAnchor` / 现有 `CustomerUIAnchor`：来自 `service-vfx`。
- sorting、shadow、footprint、collision 和所有锚点均来自 Manifest。
- 浏览器真实流程：顾客进入 → 分配到候选洗发工位 → 淋水 → 洗发 → 冲洗 → 包毛巾 → 去毛巾 → 转剪发 → 完美剪发 → 吹发 → 离店并生成支付。
- 最终运行标记：`customerState=Leaving complete=True payment=1`。

结论：B 可以在不重写核心服务逻辑的情况下替换视觉，但 pivot、比例、摆位、碰撞和锚点仍需要一次人工视觉确认，确认后才能从 candidate 晋级。

## 5. Skills 试运行结果

- `salon-asset-ingest`：**部分通过**。真实附件完成了原图检查、hash、安全复制、命名、Manifest、Unity 资源、预览、实验室构建和横屏截图；重复接入会明确拒绝且不产生重复项。未完全通过之处是：脚本内部导入阶段可自动回滚，但若随后 Unity 构建或浏览器门禁失败，整个 Skill 尚未做成跨阶段事务回滚；生产参数配置也仍需技术人员填写或复核。
- `salon-visual-qa`：**部分通过**。真实 Chromium 能发现资源加载、console、空截图、工位对齐时机、服务中状态、完成状态和请求失败等问题，并已实际帮助发现对齐截图过早及遮挡问题。未完全通过之处是：没有产品批准的视觉基准时，它不能自动判断最终比例、审美匹配、阴影轻重和最终摆位，只能报告当前事实并标记 `baseline-missing`。
- `salon-bug-fix`：**本轮真实调用，通过**。稳定复现并修复了候选服务顺序、毛巾转场、缺少吹发导致不结算、WebGL Quad 附带剥离碰撞组件、截图早于角色到位、透明图深度遮挡，以及新增候选校验后测试夹具未同步等问题；均有专项测试或浏览器证据。

## 6. 本轮测试结果

- Node 资产管线测试：**通过，9/9**。
- Unity 全量 EditMode：**通过，495/495，0 失败**；以 XML 结果为准。
- Manifest 校验：**通过，11 个资产**。
- 资源检查：**通过**；两件真实 PNG 可从 Resources 加载，原图/归档/导入 hash 一致。
- 资产实验室：**通过**；A、B 均完成 preview、anchors、areas 横屏截图，真实 artwork 标记为 true。
- 正式 Demo：**通过**；WebGL 构建和现有核心服务浏览器检查通过，未永久使用 candidate。
- 浏览器：**通过**；844×390 下 Demo、资产实验室和候选场景均启动。
- Console / 网络资源请求：**通过**；最终候选普通模式和 debug 模式均为 0 error、0 failed request。
- 服务回归：**通过**；候选 B 完成洗→剪→吹→离店→支付。
- 重复接入测试：**通过**；A、B 第二次接入均以 candidate 已存在而安全拒绝，Manifest hash 不变，相关文件数量均保持 `4→4`，没有重复项。
- 最终产品比例、摆位、美术协调批准：**未覆盖**；本轮没有批准基准，需产品负责人看截图或可玩版本确认。

## 7. 截图和体验入口

- 打开资产实验室：Unity 打开 `Assets/Scenes/AssetTestLab.unity`；WebGL 构建入口为 `BuildScript.BuildWebGLAssetLab`。
- 查看 A：在资产实验室选择 `furniture-magazine-rack-wood`，或使用资产实验室浏览器检查的 `--asset-id furniture-magazine-rack-wood`。
- 查看 B：选择 `furniture-wash-station-vintage-right-wall`；它只显示 `right-wall`，方向按钮不会制造另一方向。
- 打开真实候选场景：Unity 打开 `Assets/Scenes/CandidateAssetValidation.unity`；WebGL 构建入口为 `BuildScript.BuildWebGLCandidate`。
- 打开正式 Demo：Unity 打开 `Assets/Scenes/HairSalonDemo.unity`；它仍保持正式资产和现有玩法。
- 所有本轮截图：`unity-hair-salon/Builds/PipelineEvidence/`。
- 机器报告：`unity-hair-salon/Builds/PipelineEvidence/browser-check.json` 与 `visual-qa-report.json`。

## 8. 以后还需要多少人工操作

### 普通资产

产品负责人最少提供：一张最终 PNG、用途/类别、是否自带阴影、是否有方向要求，以及希望放入哪个场景区域。稳定 ID、文件检查、hash、复制、Manifest 落盘、Unity 导入、预览、实验室截图、构建和浏览器检查可以自动完成。

仍需人工确认：世界显示大小、pivot 是否准确落地、footprint、collision、最终摆位、sorting，以及截图中的比例和遮挡是否符合产品意图。

### 交互工位

产品负责人最少提供：一张最终单方向 PNG、真实方向、对应服务类型、希望替换的现有逻辑工位，以及在图上大致指出顾客、理发师、排队、工具和服务特效位置。无需提供第二方向。

仍需人工确认：显示比例、pivot、footprint、collision、五个锚点、最终墙边摆位、人物前后遮挡效果和完整服务截图。产品负责人不需要手改 Manifest、Unity 场景或代码，但当前仍需要技术人员把这些确认值写入配置 JSON。

## 9. 关于第二方向

当前没有第二方向素材；本轮没有生成、镜像、翻转、负缩放、旋转或伪造第二方向。Manifest 的 `Visuals + Directions + DefaultOrientation` 数据结构支持未来扩展，但 B 当前只登记真实存在的 `right-wall`。

将来收到真实第二方向 PNG 后，仍经过同一套原图保护、hash/边缘检查、明确方向配置、Manifest 校验、资产实验室、候选场景、视觉 QA、自动测试和浏览器截图流程。第二方向不是本轮阻塞项。

## 10. 第二轮结论

### B：流水线需要再修一轮，然后才能进入小批量生产。

理由：两件真实 PNG 已完整走通“原图 → ingest → Manifest → 实验室 → 候选场景 → visual QA → 自动测试 → 横屏截图”，普通资产可以低人工成本接入，单向交互工位也能复用现有服务逻辑；但跨 Unity 构建与浏览器阶段的失败还不能自动事务回滚，关键场景参数仍需技术人员填写，且无批准基准时视觉 QA 不能自动批准比例和遮挡。因此当前适合继续真实试验，不适合直接宣称可无人值守小批量生产。

**唯一下一步：把 `salon-asset-ingest` 改成带导入收据和跨阶段自动回滚的一次性事务命令，并用下一件真实单向资产做验收。**
