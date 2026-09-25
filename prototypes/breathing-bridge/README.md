# 呼吸桥（Breathing Bridge）

这是一个 120 分钟、低保真、抛弃式 Canvas/JavaScript 灰盒。现在测试的不是“一张静态障碍图”，而是一个更接近《跳一跳》的连续节奏：负载自动向右走，按住让 10 节点膜桥向前变长，松开泄压并把负载弹起，落到下一块平台后继续。

本目录只回答一个问题：新玩家能否只靠按住/松开感到自己控制膜的形变，并在失败后有意识调整压力时长，把负载送过一串障碍？它不是正式产品代码；没有美术、声音、Meta、商店、SDK、联网、登录、支付、统计或发布流程。`adsEnabled=false`、`networkAllowed=false`、请求预算恒为 `0`。

## 当前结论

- 正式机器审计：`300/300` seed 有求解路径；`300/300` 跨 30/60/120fps；稳健 seed `300/300`；随机基线 `2677/10000 = 26.77%`；稳定仿真 `10000/10000`；全部硬门槛通过。
- 无依赖构建：通过，见 `dist/build-manifest.json`。
- 浏览器烟测：本次重构后若宿主允许本地端口，运行 `npm run smoke` 重新生成两张截图；旧截图属于上一版静态关卡，不能作为本版视觉证据。端口被宿主阻断时必须记录 `ENVIRONMENT_UNVERIFIED`，不能写成玩法 KILL。
- 产品结论：`TECHNICAL_PASS / PRODUCT_UNDECIDED`。没有陌生真人数据，不能写 KEEP、好玩、留存或收益已证明。

## 玩法与变体

每关由 9 段生成，seed 决定可见的动作语法，不只是换坐标：

- `long-reach`：长缺口，连续按住后一次长跳。
- `short-pulse`：短缺口和短脉冲，频繁按住/松开。
- `high-lift`：高墙，要求积压更高压力后释放。
- `late-drop`：前段滚动、后段才出现释放窗口。
- `zigzag-rhythm`：平台高度与缺口节奏交替。

每个 seed 都有 `signature`、`knownStrategy` 和 `nextSeed`；浏览器成功后才解锁“下一关 →”，所以可以连续体验变体，而不是重复同一关。

## 操作

- 按住 Canvas 任意位置：膜节点鼓起、桥头向前增长。
- 松开：泄压；若负载在可跳窗口，触发跳跃，落到下一平台后继续自动前进。
- `↻ 免费重开`：任何结果都从当前 seed 的精确初始状态重开。
- `下一关 →`：只在成功结算后可用，进入 `seed + 1` 的不同动作语法。

## 运行与复现

需要 Node.js 20+；仿真、构建和审计没有第三方依赖。

```bash
cd /Users/kker/Documents/ChatGPT/game2/prototypes/breathing-bridge
node --test tests/*.test.mjs
npm run audit
npm run build
npm run smoke
```

烟测退出码：`0=PASS`、`1=真实浏览器断言失败`、`2=ENVIRONMENT_UNVERIFIED`。脚本只启动临时 `127.0.0.1` 回环服务，不访问外网。若当前 Node 找不到 Playwright，可显式指定本机模块：

```bash
env PLAYWRIGHT_MODULE=/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs npm run smoke
```

## 结构

```text
BRIEF.md                 锁定问题、Keep/Kill 与边界
RED_GREEN.md             真实 TDD RED→GREEN 记录
REPORT.md                正式分子/分母、假设、结论与命令
index.html               Canvas 入口与下一关按钮
styles.css               响应式低保真布局
src/growing-bridge.mjs   新版固定步物理、seed、关卡、求解、回放、审计
src/browser.mjs          指针/触控、横向镜头、关卡切换、暂停恢复
src/sim.mjs              旧版兼容测试夹具；不作为浏览器入口
tests/                   Node 内建测试（含新版行为契约）
scripts/audit.mjs        300 seed / 10,000 仿真正式审计
scripts/build.mjs        零依赖可复现构建
scripts/smoke.mjs        390×844 鼠标与 430×932 原生触控烟测
artifacts/*.json         机器可读审计与烟测结果
dist/                    构建结果与哈希清单
```

## 物理与确定性边界

- 固定步 `1/60s`；渲染累加器单帧最多 5 步，30/60/120fps 只改变采样，不改变物理 tick。
- 10 个膜节点采用目标弹簧、邻接张力和阻尼；按住按压力增长桥头，松开泄压并记录输入。
- 负载有水平/垂直速度钳制、受控巡航、跳跃缓冲；矩形墙用扫掠圆 CCD，掉桥在有界恢复面结束。
- seed 先选动作语法，再生成 9 段平台/缺口/障碍；已知策略用于反生成与审计，束搜索是回退路径。
- 输入日志只存物理 tick 和布尔态；暂停立即释放输入、清空累加器，离开期间物理时间冻结。

这些机器代理证明的是实现稳定性和关卡结构，不证明陌生玩家能读懂因果、觉得好玩或愿意重试。
