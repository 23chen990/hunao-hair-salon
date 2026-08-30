# 《胡闹理发店》当前状态

> 更新时间：2026-08-29（Asia/Shanghai）

## 当前可运行成果

- Unity 正式 Demo：`unity-hair-salon/Assets/Scenes/HairSalonDemo.unity`。
- 独立资产实验室：`unity-hair-salon/Assets/Scenes/AssetTestLab.unity`。
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

- 资产接入：以现有 `contact-shadow.png` 做只读检查，确认 128×64 PNG、透明通道、可见边界和命名；为避免重复接入定稿资源，没有修改 Manifest。
- 视觉 QA：正式 Demo 和固定资产实验室均在真实 Chromium 中启动并生成 844×390 截图；控制台无错误、核心服务流程通过。因尚无产品负责人批准的视觉基准，主观画面项不宣称通过。
- Bug 修复：实际发现调试碰撞体使用不透明材质、会遮挡正式场景；先补失败回归测试，再改为透明诊断材质，并以同视口前后截图和浏览器流程验证。

## 最近一次完整门禁结果

- Node 资产管线：5/5 通过，包含命名、格式、定稿覆盖保护、成功接入和失败自动回滚。
- Unity EditMode：482/482 通过；Manifest、重复 ID、必要锚点、方向、占地、资源和场景启动检查通过。
- WebGL：正式 Demo 与资产实验室均构建成功。
- Chromium：844×390 正式 Demo 核心流程通过；调试模式可见且无控制台错误；资产实验室后墙向与右墙向截图均通过非空检查。
- 结构化报告与截图：`unity-hair-salon/Builds/PipelineEvidence/`。

## 当前主要技术债

1. 主场景仍由一个大型运行时脚本集中创建，世界布局没有独立场景数据文件。
2. 通用 Manifest 尚未成为所有正式家具的唯一数据源；剪发工位存在兼容层。
3. 除剪发工位外，正式移动碰撞尚未系统性接入每件家具的 Manifest collision。
4. 部分 UI 资源的 Unity 导入类型仍为 Default Texture，而不是 Sprite，运行时依靠自建 Sprite 或特殊加载逻辑。
5. 角色源图存在非透明背景版本，处理后的方向帧透明区差异较大；当前不影响已批准 Demo，但替换动画时需先在实验室核对脚底稳定性。
