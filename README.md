# 胡闹理发店

Unity 横屏动作经营 Demo。当前玩法是接待与带客、洗剪吹服务、收银台结账、补货和按顺序投币建设。正式工程位于 `unity-hair-salon/`，固定 Unity 版本为 `6000.5.8f1`；唯一正式可玩场景是 `Assets/Scenes/HairSalonDemo.unity`。

## 先读这些

1. [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md)：最新产品决定位于顶部，优先于历史要求。
2. [STATUS.md](STATUS.md)：最近完成的工作、验证结果与已知限制。
3. [首日节奏纠偏交付](docs/development/pacing-rebalance/DELIVERY.md)：本轮客流、工位负荷、学习与成长规则。
4. [顾客交互修复](docs/development/interaction-fixes/DELIVERY.md)与[核心流程交付](docs/development/core-rework/DELIVERY.md)：本轮之前已经接入的功能。
5. [AGENTS.md](AGENTS.md)：接续开发与验收约束。

`TECH_HANDOFF.md`、Unity 子目录 README 和旧阶段报告包含历史状态。阅读旧文档时先核对上述最新入口；旧价格、按天解锁、开局全工位、服务结束直接到账等记录已经被后续决定替代。

## 运行与检查

使用 Unity `6000.5.8f1` 打开 `unity-hair-salon/`，打开正式场景后播放。新局选择“单人”和空存档；接待顾客、带到工位完成服务，再到收银台收款。起步只有一把理发椅，赚到的钱依次用于建设。手持吹风机需要按住操作，建好自动吹风架后才支持后台吹发。

在已安装指定 Unity 和 WebGL 模块的 macOS 环境中，从仓库根目录执行完整检查与构建：

```sh
zsh tools/check-project.sh
python3 -m unittest discover -s tests -p 'test_*.py'
python3 tools/check-pacing-rebalance.py --days 3 --evidence-dir artifacts/pacing-rebalance/new-browser-run
```

浏览器检查需要 Python 的 Playwright、Pillow 和 Chromium。`check-project.sh` 包含资产测试、字体检查、全量 Unity EditMode、XML 结果核验、资源校验、四种 WebGL 构建和真实浏览器检查。Unity 测试命令不能同时使用 `-runTests` 与 `-quit`。

构建后启动本地试玩：

```sh
python3 tools/serve-salon.py --bind 127.0.0.1 --port 8877
```

浏览器访问 `http://127.0.0.1:8877/WebGLDemo/`；独立资产实验室位于 `/WebGLAssetLab/`。WebGL 需要 HTTP 服务，不能直接打开 `index.html`。GitHub 仓库提供源代码与资源，本地试玩地址仅在运行服务的电脑上有效。

## 代码入口

| 文件或目录 | 职责 |
| --- | --- |
| `Assets/Scripts/SalonDemo.cs` 及其 partial 文件 | 正式场景、交互、视图、客流和系统集成 |
| `Assets/Scripts/SalonPacingDirector.cs` | 根据工作负荷和服务经验安排上客及订单 |
| `Assets/Scripts/SalonDemo.CoreFlow.cs` | 收银、库存、教学与成长记录 |
| `Assets/Scripts/SalonGameModel.cs` | 顾客、工位和服务状态 |
| `Assets/Scripts/BusinessDaySystem.cs` | 营业、收尾与结算 |
| `Assets/Scripts/SalonProgressSave.cs` | 存档、迁移、学习经验与扩建缓冲 |
| `Assets/Scripts/SalonUnlockRoute.cs` | 已批准的七项投币建设路线 |
| `Assets/Tests/` | Unity 自动回归测试 |
| `Assets/Resources/AssetPipeline/asset-manifest.json` | 资产 ID、占地、碰撞、方向、阴影和交互锚点 |
| `tools/` 和 `.agents/skills/` | 构建、验收、资产接入与复现工具 |

表中的 `Assets/` 均相对于 `unity-hair-salon/`。正式世界主要由运行时代码创建，只看 `.unity` 文件无法判断完整表现。

当前节奏目标是有事可做且能处理得过来：先掌握服务，小批接客，处理收银；有补货或可负担建设时留经营窗口，没有任务时及时接下一位。金币建设与已掌握流程及实际瓶颈关联，购买后有适应期。技术自动验收不能替代真人对忙碌度、乐趣及后期价格的试玩判断。

源码、场景、资源、测试、工具、文档及适量验收证据入库。Unity 缓存、可重新生成的构建、日志、逐帧遥测和原始录屏保留在本机。新 PNG 从 `assets/inbox/` 经过管线校验后再接入；不能手工覆盖 approved 资产。下一步先做证据充分的规划，不自动扩展未批准的玩法、多人或商业化系统。
