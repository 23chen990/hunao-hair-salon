# RED / GREEN 实际记录

> 日期：2026-08-10（Asia/Shanghai）  
> 所有命令均在 `prototypes/stacked-aperture/` 执行。

## 核心规则

1. 先创建生成/BFS、几何诱饵、状态/回放、吸附/布局、资格、局长与随机采样测试，尚无 `src/` 实现。
2. RED：`npm test`，退出码 1；17 tests、0 pass、17 fail。每项均为明确断言，例如 `model implementation is missing`、`solver implementation is missing`、`eligibility implementation is missing`，不是拼写或测试加载错误。
3. PRNG 单独 RED：`node --test --test-reporter=spec tests/prng.test.mjs`，退出码 1；1/1 因 `PRNG implementation is missing` 失败。
4. 添加最小实现后 GREEN：`npm test`，18 tests、18 pass、0 fail。

## 浏览器表现层

1. 先创建完整 `tools/browser-smoke.mjs`，尚无 `index.html` / Canvas app。
2. RED：`npm run browser:smoke`，退出码 1；明确失败为 `browser entry implementation is missing`。
3. 添加 HTML、CSS、Canvas app、PointerEvent 输入、构建脚本并成功构建 9 个（最终 11 个）模块。
4. 子任务 GREEN 未取得：实现后的浏览器命令在创建 Chromium 时被系统 Mach port 权限拦截；当次沙箱外授权被安全策略拒绝，因此子任务没有记录假 GREEN。
5. 主任务回收时重新按权限规则获准在沙箱外执行同一命令；390×844 鼠标流程和 430×932 触控 PointerEvent 流程全部断言通过、控制台零错误，真实 GREEN 写入 `browser-smoke.json` 与浏览器截图。

## 模型证据帧

1. RED：`tests/evidence-frame.test.mjs`，1/1 因 evidence frame 实现缺失失败。
2. GREEN：真实 seed + BFS 近解 + 真实放珠状态实现后 1/1 通过。
3. 字体回退 RED：`tests/evidence-labels.test.mjs`，1/1 因跨后端安全标签缺失失败；ASCII 标签实现后 GREEN。
4. 目视发现大弧形伪影；第一处像素采样点没有命中伪影，修正到 `(280,100)` 后得到有效 RED：实际像素 `201,211,225`，超出暗背景阈值。
5. 将证据专用字体权重改为该后端支持的 normal/bold 并重录后，像素回归 GREEN。浏览器 Canvas 的圆孔遮罩实现未因此降级。

## 奖励入口上限

1. RED：eligibility 套件原 4 项通过，新测试因 `registerRewardEntry` 缺失失败。
2. GREEN：增加每会话最多两个提示/诊断资格入口后，eligibility 5/5 通过；全量审计确认尝试 3 次只发出 2 次。

## 最终 GREEN

```text
npm test
tests 23
pass 23
fail 0
duration_ms 368.509041

npm run build
Built 11 throwaway modules and direct-open app.bundle.js into dist/.

npm run audit
exit 0
allMachineGatesPassed true

npm run evidence
exit 0
PNG 390 x 844 RGBA

npm run browser:smoke
exit 0
390x844 mouse drag PASS
430x932 touch pointer drag + tap PASS
console errors 0
```

八项机器硬门槛现已全绿；这只是当时的技术结论。2026-08-11 的真实人玩反馈随后确认持续游玩只是为了等待难度出现，核心循环本身没有形成挑战，最终产品结论更新为 `KILL`。

## 2026-08-11｜Safari `file://` 空白修复

1. 用户实际截图显示 CSS 背景已加载、玩法未启动；检查确认 `index.html` 使用 `<script type="module" src="./dist/app.js">`。
2. RED：新增 `tests/file-open.test.mjs` 后单测退出 1，明确失败为 `file:// entry must not depend on module loading`。
3. GREEN：构建脚本按依赖顺序生成无 ESM 语法的 IIFE `dist/app.bundle.js`，入口改为经典 defer script；契约单测 1/1、bundle `node --check` 均通过。
4. 全量复跑：23 tests、23 pass、0 fail；500/500 seed、10,000 随机、资格与无广告审计保持通过。等待用户在 Safari 强制刷新后做真实显示与交互确认。
