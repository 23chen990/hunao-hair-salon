# 《相位涟漪》抖音小游戏适配包

这是一个低成本的原生 Canvas 适配层，复用 `prototypes/phase-ripple-skinned/src/` 的关卡、规则、教学与渲染，不依赖浏览器 DOM，也没有接入广告或付费。

## 本地构建

```bash
npm test
npm run build
```

构建目录为 `dist/`。其中已包含抖音小游戏要求的 `game.js`、`game.json`、`project.config.json`、CommonJS 脚本与本地图片资源。

## 发布闸门（未完成前禁止提审）

1. 用真实小游戏 AppID 替换 `dist/project.config.json` 中的 `REPLACE_WITH_DOUYIN_APPID`。
2. 使用最新版抖音开发者工具导入 `dist/`，完成模拟器与真机预览。
3. 完整测试侧边栏复访：主界面显示“侧边栏礼”入口、自动跳转侧边栏、从侧边栏返回、每日领取一次落点提示。代码已按官方字段 `launch_from=homepage`、`location=sidebar_card` 判断，但必须用真实 AppID/测试设备验证。
4. 在开放平台补齐名称、类目、年龄分级、隐私政策、截图、软著/授权与备案信息。
5. 审核前保持广告关闭。本包没有广告 SDK，也不承诺变现。

`dist/` 目前是“开发者工具可导入测试包”，不是已通过审核的发布包。
