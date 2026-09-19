# 洗发区第二轮美术打磨

本轮按产品负责人提出的“仍然有点丑”反馈，集中打磨现有洗发区；保留 Unity、工位布局、角色造型、服务流程和 HUD。

## 画面变化

- 原先的平板坐垫改为带弧度的厚坐垫，收拢底座层次，加厚扶手，让洗发台的轮廓更完整。
- 洗发盆保留真实内凹、排水口和颈部凹槽，平滑盆沿，减少刺眼的白色边缘。
- 木材增加低对比纹理；坐垫、陶瓷与金属采用不同高光。模型增加自身缝隙的遮蔽层次，接地仍使用原有实时光照与 ContactShadow，没有新增或覆盖 PNG。
- 墙面改为连续浅色墙面和低对比材质变化，减弱地砖缝；调整现有洗护瓶的高矮和体量、植物叶片宽度。
- 暖光与环境光重新平衡。比较截图使用上一版相同的镜头、844×390 画布和 DPR 1。

## 体验入口

- [洗发区近景](http://127.0.0.1:8910/WebGLDemo/?washCraft=detail)
- [正常试玩](http://127.0.0.1:8910/WebGLDemo/)
- [三维资产实验室](http://127.0.0.1:8910/WebGLAssetLab/?assetId=furniture-wash-station-crafted&view=inspect)

近景参数用于查看画面。正常试玩中点击开始营业、选择等候顾客，再点击洗发台并使用底部服务按钮。链接由本机服务提供。

## 范围与限制

本轮改善家具和环境的轮廓、质感、明暗。角色及相邻理发区仍有占位造型；整店尚未达到参考图的美术完成度。模型状态仍为 review，技术检查通过不代表产品视觉批准。

开发录屏脚本改为等待人物实际到位和顾客实际离场，避免固定秒数等待在低帧率录屏时提前判定失败。自动服务证据通过开发参数驱动原有洗剪吹动作，用于检查服务状态和遮挡，不等同于整条链路均为人工点击。未在真实手机上测量性能。

## 最终检查与证据

- Node 资产管线 17/17；最终 Unity 全量 EditMode XML 544/544，0 failed。
- Manifest 和资源检查通过；最终 Demo 与资产实验室重新构建。较早的四场景 WebGL 构建与完整浏览器回归也通过。
- 最终构建在 Chromium 完成总览、近景、两张工位完整服务和模型实验室五项检查，无控制台错误或资源请求失败。
- 两张工位到位误差分别为 0.138 和 0.000（允许不超过 0.15）；均确认付款生成且顾客进入 Leaving 状态。

[同角度前后对比](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/WashCraft/evidence/polish-before-after.png) · [第一张工位录屏](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/WashCraft/evidence/wash-service-station-0.webm) · [第二张工位录屏](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/WashCraft/evidence/wash-service-station-4.webm)

[结构化画面检查报告](/Users/kker/Documents/ChatGPT/game2/unity-hair-salon/Builds/WashCraft/evidence/polish-visual-qa-report.json)

## 版本恢复记录

产品负责人澄清要恢复的是前后对比图红框中的“改进前”首版三维洗发区，而不是占位原版。正式 Demo 默认已恢复该首版模型与镜头；`washCraft=original` 仅作为占位原版对照入口。当前默认截图见 `detail-844x390.png`。
