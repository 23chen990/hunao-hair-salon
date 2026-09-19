# 洗发区三维制作首版 · 2026-09-07

本轮交付是正式 Unity Demo 里的完整洗发区首版。新模型已经参与实际服务，技术检查通过，视觉仍待产品负责人看图和试玩；没有把整间店标记为完成。

## 看图与体验

- [洗发区实机截图](../unity-hair-salon/Builds/WashCraft/evidence/delivered-demo-844x390.png)
- [营业总览](../unity-hair-salon/Builds/WashCraft/evidence/overview-844x390.png)
- [第一张工位服务截图](../unity-hair-salon/Builds/WashCraft/evidence/station-0-washing-844x390.png)
- [第二张工位服务截图](../unity-hair-salon/Builds/WashCraft/evidence/station-4-washing-844x390.png)
- [第一张工位实机录屏](../unity-hair-salon/Builds/WashCraft/evidence/wash-service-station-0.webm)
- [第二张工位实机录屏](../unity-hair-salon/Builds/WashCraft/evidence/wash-service-station-4.webm)

本机服务正在 `http://127.0.0.1:8910` 运行：

1. 打开 [正式 Demo](http://127.0.0.1:8910/WebGLDemo/)，点击“开始营业”。
2. 点击等候顾客，再点击左后方任意空闲洗发台，即可安排服务；服务方式沿用现有 Demo。
3. [直接看洗发区](http://127.0.0.1:8910/WebGLDemo/?washCraft=detail) 会在开发构建中自动开店并固定近景，适合观察模型。
4. [资产实验室](http://127.0.0.1:8910/WebGLAssetLab/?assetId=furniture-wash-station-crafted&view=inspect) 可以检查三维模型、占地和人物锚点。

若本地服务已停止，在项目根目录运行 `python3 tools/serve-salon.py --port 8910`。这些链接只在当前电脑可用。

## 完成内容

- 使用 Blender 4.5.9 LTS 原创建模并导出 FBX：真正凹进去的陶瓷盆、颈枕、分段软垫、扶手、脚踏、底座和边缘倒角。
- 两张洗发台使用同一份三维资产，沿用原工位位置、碰撞、顾客座位、理发师工作位和服务逻辑。
- 共享地面改为细密、低对比的青绿色砖面；增加木饰面与墙面收边，并替换洗发区原有的两组墙架、瓶罐和一盆植物。
- 在 Unity 中使用真正响应灯光的材质、柔和实时阴影和独立接触阴影；新增家具没有烘焙地面阴影。
- 调整斜向正交取景与左侧低墙剖视，正式 HUD、玩法和既有角色继续使用。
- 新模型先经过 Manifest 和资源校验，再进入正式场景。新 ID 为 `furniture-wash-station-crafted`、`prop-salon-room-crafted`，状态为 `review`；原已批准条目和 PNG 未被覆盖。
- 资产实验室新增真正的三维模型检查，模型不会被误当作 PNG，也不会显示无意义的“100% PIXEL”操作。

## 已修正的问题

首次 Unity 画面中，FBX 的坐标转换让洗发区墙架和植物出现在右侧。专项测试稳定测得瓶罐中心 X 为 +6.05（预期在左侧），先记录失败，再在导出时补偿左右坐标；修正后专项检查通过。

最后复查还发现理发师去第二张洗发台时会被中央剪发台卡住：旧逻辑只直线移动，碰撞后退回，无法绕行。新增根据现有工位碰撞范围生成的短路径，保留碰撞与工位布局；先记录失败测试，再检查两个工作位都实际到达。自动录屏现在会在人物没有到达时直接失败。

另外修正了底座顶入盆腔、地砖缝过亮、空背景出现天空渐变和开发近景的订单气泡挤占顶部 HUD。前后截图均保存在 `Builds/WashCraft/evidence/`；取景也经过调整，不能把这组截图作为逐像素相同机位的比较。

## 检查结果

| 检查 | 结果 |
|---|---|
| Node 资产管线 | 17/17 通过 |
| Unity 全量 EditMode | 544/544 通过；检查 XML，0 failed |
| 新模型与方向专项 | 6/6 通过；保留修正前失败 XML |
| 理发师绕行专项 | 3/3 通过；两张洗发台的实机工作位误差均为 0.000 |
| Manifest、纹理与场景资源 | 通过 |
| WebGL 构建 | Demo、Asset Lab、Candidate、旧 Reference 均成功 |
| 既有浏览器回归 | 完整 `tools/check-project.sh` 通过 |
| 新版双洗发工位 | 分别跑完洗、剪、吹、离店及付款掉落 |
| 实际触屏点击 | 开始营业、选顾客、安排新工位、启动洗发通过 |
| 目标横屏 | Chromium 844×390、DPR 1；交付 URL 再次启动确认 |
| 新版页面错误、资源请求失败 | 0 |

自动服务录屏通过开发参数调用原有服务动作，用于检查状态和画面；真实触屏另行检查至洗发启动；最终交付链接也确认第二张洗发台的顾客安排、理发师到位和洗发启动。没有将自动回归描述为人工点击完成全链路。

## 限制与视觉判断

- 洗发区域、墙地面比之前更完整，但周边剪发、等候和收银区域仍保留占位几何，整店完成度还不统一。
- 顾客仍使用现有简化造型；它与主控角色、参考图在细节和头身比例上仍有差距，本轮没有擅自重做角色。
- 新版仍为 Development Build，正常显示 Unity 的开发构建角标；调试参数没有增加正式 HUD 页面。
- 尚未在真实手机上测量性能；本轮浏览器结果不等同于移动端性能验收。
- 参考图用于布局、美术和氛围判断，没有宣称逐像素还原或产品验收通过。

## 后续维护入口

- 建模源文件：`assets/source/wash-craft/wash-station.blend`、`room-finish.blend`。
- 可重建脚本：`tools/blender/build_wash_craft.py`，使用 Blender 的 `--background --python` 执行。
- 资产规模：洗发台 3,244 三角面；环境 25,454 三角面；两份 FBX 共约 0.5 MB。
- Unity 固定版本保持 `6000.5.8f1`。
- 完整检查：`zsh tools/check-project.sh`。
- 新区实机回归与录屏：`python3 tools/check-wash-craft.py`。
- 结构化结果：[视觉检查报告](../unity-hair-salon/Builds/WashCraft/evidence/visual-qa-report.json)。
