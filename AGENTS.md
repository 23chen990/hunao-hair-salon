# 《胡闹理发店》协作规则

1. 产品负责人通过可玩版本、截图和普通中文说明验收，不审查代码 diff，也不逐步验收底层实现。
2. 不自行新增未批准的页面、玩法、货币、事故或升级系统。
3. 不改变已批准的角色、房间布局和 UI 视觉方向。
4. 世界场景资产不能长期依赖散落的人工坐标；可交互家具必须定义占地、碰撞和人物锚点。
5. 主场景在同一轮中只能由一个集成任务修改。
6. 新资产必须先通过配置校验，再接入正式场景。
7. 所有任务完成前必须运行相关自动测试、构建和实际启动检查。
8. 最终交付用普通中文说明完成内容、测试结果、体验步骤和限制。
9. 当前核心玩法已获得阶段性认可，不重新验证、推翻或重做；保持现有技术栈和当前 Demo 可运行。
10. 当前工程重点是资产接入、比例/方向/阴影统一、场景还原、穿模定位和 Bug 修复效率，不扩展未经批准的多人或商业化系统。

## 当前项目入口

- 正式 Unity 项目：`unity-hair-salon/`，固定使用仓库 `ProjectSettings/ProjectVersion.txt` 声明的 Unity 版本。
- 正式 Demo 场景：`Assets/Scenes/HairSalonDemo.unity`。
- 独立资产测试场景：`Assets/Scenes/AssetTestLab.unity`，不得把其调试 UI 合入正式 HUD。
- 通用资产 Manifest：`Assets/Resources/AssetPipeline/asset-manifest.json`。
- 新图片只能先放到根目录 `assets/inbox/`，通过 `tools/asset-pipeline.mjs` 后再进入 Unity；禁止手工覆盖状态为 `approved` 的资产。

## 实施约束

1. `PROJECT_CONTEXT.md` 的产品背景以产品负责人最新版本为准；旧原型和历史规划不能自动覆盖当前方向。
2. 修改 `SalonDemo.cs` 视为修改主场景；同一轮只能有一个任务负责集成，其他任务不得并行触碰。
3. 新世界资产必须具有稳定 ID、方向、pivot、footprint、collision、shadow、interaction anchors 和 sorting 配置，并先通过 Manifest 校验。
4. 不把接触阴影永久烘焙进家具图；优先使用统一 `ContactShadow` 和 Manifest 参数。
5. 调试功能只能在 Editor、Development Build 或明确调试参数下启用，不得污染发行 UI。
6. Unity 命令行测试不能同时使用 `-runTests` 与 `-quit`；必须检查生成的 XML，不能只看进程退出码。
7. 完成前至少运行：Node 资产管线测试、Unity 全量 EditMode 测试、Manifest/资源检查、WebGL 构建和真实 Chromium 检查。

## 验收产物

- 普通中文完成说明。
- 可运行 Demo 与资产实验室。
- 目标横屏浏览器截图。
- 自动测试/构建结果和已知限制；不要求产品负责人阅读代码 diff。

## Skills 与自动化原则

1. 项目中的重复工作应逐步固化为仓库级 Skills，存放在 `.agents/skills/`。
2. 当前优先维护 `salon-asset-ingest`、`salon-visual-qa`、`salon-bug-fix`。
3. 每个 Skill 只负责一个明确工作，不创建万能 Skill。
4. Skill 必须明确触发条件、禁止触发条件、输入、输出、必须检查和完成标准。
5. 已批准的玩法、美术、朝向和布局不能被 Skill 自行重新设计。
6. 图像生成负责视觉创作；裁边、命名、校验、Manifest、截图和构建等确定性工作必须优先使用脚本。
7. 新 Skill 应先通过真实任务验证，再考虑自动调用。
8. Hooks 只在对应 Skill 已稳定后启用；当前不得创建自动 Hooks。
9. 不安装来源不明、会执行任意命令或上传项目资源的第三方 Skill。
10. 产品负责人只验收最终图片、截图、录屏和可玩结果，不负责逐步执行 Skill 内部流程。

## Repo Skills 自然语言路由

- 当请求包含“接入已确认 PNG”“从 assets/inbox 导入”“更新资产 Manifest/预览/资产测试场景”时，使用 `$salon-asset-ingest`。未确认的视觉创作或素材重做不得触发它。
- 当请求包含“启动 Demo 检查画面”“和批准截图对比”“检查比例、方向、阴影、遮挡或安全区域”时，使用 `$salon-visual-qa`。没有批准基准时只能报告当前事实，不宣称视觉一致。
- 当请求包含“根据截图、录屏、文字或调试状态复现并修 Bug”时，使用 `$salon-bug-fix`。功能需求、视觉重设计和无复现依据的猜测修复不得触发它。
- 同一请求若先接入资产再验收画面，先用 `$salon-asset-ingest`，成功后再用 `$salon-visual-qa`；不要把两项职责合并为新万能 Skill。
