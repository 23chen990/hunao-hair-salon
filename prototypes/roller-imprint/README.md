# 滚筒成章（P2）抛弃式 Canvas 灰盒

这是一个 `ROLL / THROWAWAY=yes / TIMEBOX=120min` 的本地实验，不是正式产品。它只验证：玩家能否看懂“拖动距离决定转过多少离散面，接触面的凸纹会落到 8×12 色带上”，并在错印后调整下一趟。

当前结论：**技术通过，等待陌生真人，产品未决。主任务已独立复跑测试、审计、构建和双视口浏览器交互。**

## 运行

无需安装依赖，不包含网络请求、广告 SDK 或外部资产。

```bash
cd /Users/kker/Documents/ChatGPT/game2/prototypes/roller-imprint
npm test
npm run audit
npm run build
python3 -m http.server 4177 -d dist --bind 127.0.0.1
```

然后打开 `http://127.0.0.1:4177/`。入口源文件是 `index.html`，构建入口是 `dist/index.html`。

浏览器自动烟测：

```bash
npm run smoke
```

烟测脚本只使用回环临时端口和本机已有的 `playwright-core` / Chromium，不下载内容。成功后会产生：

- `artifacts/browser-smoke.json`
- `artifacts/smoke-390x844-mouse.png`
- `artifacts/smoke-430x932-touch.png`

主任务已在允许本地回环端口的环境独立执行成功：390×844 鼠标与 430×932 触控均完成完整交互链，控制台零错误、外部请求为 0。原始 JSON 与两张截图均保存在 `artifacts/`。

## 操作

- `− / ＋`：选择 8 个离散起始接触面之一。
- 拖左圆柄向右：正向滚压；拖右圆柄向左：反向滚压。
- 每跨过一个带格，接触面离散转一面并把该面的凸纹落印。
- 当前图中，深色为正确落印，红色为错印，虚线为仍缺的目标格。
- `撤销整趟`：免费恢复整趟前的完整快照。
- `免费重开`：同 seed 从零开始，无广告分支。
- `下一 seed`：载入下一程序生成并经严格最短求解器验收的目标。
- `接触弧（本地）`、`模拟冻结`：只展示本地资格/暂停结构；默认广告关闭，绝不发请求。

## 证据与结构

- `BRIEF.md`：唯一问题、抛弃边界和判定规则。
- `RED_GREEN.md`：六轮真实 RED/GREEN 命令与原始失败原因。
- `REPORT.md`：最终数字、完成物、风险与下一步。
- `artifacts/machine-audit.json`：500 seed / 10,000 随机序列的机器可读原始审计。
- `src/model.mjs`：8 面、8×12 色带、落印、撤销、重开、序列化和事件回放的纯状态真源。
- `src/solver.mjs`：seed 生成、合法动作枚举、严格最短 set-cover 求解、扰动与随机抽样。
- `src/iaa.mjs`：本地 IAA 资格结构代理、无广告回归和冻结；没有 SDK。
- `src/app.mjs`：Canvas 表现与统一 PointerEvent 映射。
- `tools/audit.mjs`、`tools/build.mjs`、`tools/browser-smoke.mjs`：审计、构建和双视口烟测。

页面在 `window.__ROLLER_AUDIT__` 暴露只读灰盒证据接口：`snapshot()`、`events()`、`regions()`、`controls()` 和 `smokePlan()`。事件回放读取的仍是纯模型，不从 Canvas 反推状态。

## 明确不证明什么

机器结果不回答陌生玩家是否 30 秒看懂、是否满足、是否主动再开一局。局长、提示率、撤销资格、冷却和广告库存都是开发机/行为假设代理，不是低端真机、观看率、填充率、eCPM、ARPDAU、收入、留存或买量结论。若进入下一阶段，必须抛弃并重写这份 spike，不能直接升格为正式工程。
