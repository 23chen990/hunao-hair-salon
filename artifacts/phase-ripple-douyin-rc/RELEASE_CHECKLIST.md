# 《相位涟漪》抖音小游戏上架清单

> 当前包的定位：开发者工具可导入测试包。没有真实 AppID、真机验证、主体资质和平台审核结果前，不得称为已上架或审核包。

## A. 代码包（已完成）

- [x] 原生 Canvas 入口，不依赖浏览器 DOM/BOM。
- [x] 根目录包含 `game.js`、`game.json`、`project.config.json`。
- [x] TypeScript 核心模块转换为平台支持的 CommonJS 相对模块。
- [x] 图片、触摸、本地存储、前后台恢复和竖屏适配已接入 `tt` API。
- [x] 侧边栏 `tt.onShow` 在 `game.js` 启动期同步监听。
- [x] `tt.checkScene`、`tt.navigateToScene`、侧边栏返回字段及每日落点提示奖励已接入。
- [x] 未接广告、支付、登录、联网服务或用户输入。
- [x] 自动测试、生成包启动烟测、200-seed 规则审计和 20MB 包体门槛通过。

## B. 你本人需要先准备

- [ ] 在抖音开放平台完成开发者注册/入驻，并创建“小游戏”应用。
- [ ] 确认最终游戏名称。平台名称、软著名称和权利人/授权链应一致，暂名不要直接当正式名提交。
- [ ] 获取真实 AppID，替换包内 `project.config.json` 的 `REPLACE_WITH_DOUYIN_APPID`。
- [ ] 准备软件著作权登记证书或符合平台要求的完整授权材料。
- [ ] 在后台补齐联系人、类目、隐私政策、年龄分级、适龄提示、图标和至少 3 张真实游戏截图。
- [ ] 按后台当期要求完成小游戏作品备案、实名防沉迷及其他备案/出版合规项目；不同备案或证件不能互相替代。

## C. 开发者工具与真机验证

- [ ] 安装并登录最新版抖音开发者工具，选择“小游戏”，导入 `package/` 目录。
- [ ] 模拟器从冷启动走完：教学 1 → 教学 2 → 正式关 → 成功/失败 → 重试/提示。
- [ ] 在 390×844 与至少一台异形屏真机上检查胶囊按钮、安全区、触摸坐标、中文字体和图片清晰度。
- [ ] 用场景值 `021036` 模拟侧边栏进入，确认回调包含 `launch_from=homepage`、`location=sidebar_card`。
- [ ] 真机完整走通：点击“侧边栏礼” → 跳转首页侧边栏 → 从侧边栏回到游戏 → “领取提示” → 当日不可重复领取。
- [ ] 连续前后台切换、锁屏/解锁和重进 10 分钟，无黑屏、时间冻结或旧局残留。

## D. 上传与提审

- [ ] 先上传测试版本，不直接提审；记录版本号和更新说明。
- [ ] 用测试设备扫码验证，确认上传包与本地包一致。
- [ ] 提交版本审核并按平台反馈修正；审核通过后仍需在后台手动点击发布。
- [ ] 首版保持广告关闭。是否具备广告商业化主体资格，以后台实时校验结果为准，不能把“成功上架”等同于“已经能变现”。

## 官方入口

- 开发与上传：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/develop>
- 原生小游戏运行时：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/develop/guide/dev-guide/bytedance-mini-game>
- 必接能力：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/essential-skills>
- 侧边栏技术指南：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/develop/guide/open-ability/Introduction-for-tech>
- 版本提审：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/examineguide>
- 发布小游戏：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/release/>
- 作品备案：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/minigame/game-filing-application-works>
- 资质规范：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/operation1/norms/credential-norms-for-mini-game>
