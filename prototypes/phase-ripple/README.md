# 《相位涟漪》两天抛弃式灰盒

这不是正式工程。它只验证一次点击产生的空间涟漪，能否通过不同触发时刻让 3–6 颗径向外逃的棋子折返并在 0.8 秒窗口内同步归心。

## 直接运行

仓库已包含浏览器可用的 `dist/`，不需要安装任何 npm 依赖：

```bash
cd /Users/kker/Documents/ChatGPT/game2/prototypes/phase-ripple
python3 -m http.server 4177 --bind 127.0.0.1
```

- 玩法：<http://127.0.0.1:4177/>
- 解区热图：<http://127.0.0.1:4177/debug.html>

玩法页在红色边界内接受一次点击。成功后轻触进入下一 seed；碰红边或超时后轻触立即重开当前 seed。切回后台再恢复时会重开当前局，避免旧涟漪残留。

## 验证命令

使用 Node.js 24 的原生 TypeScript 类型擦除，因此无需 `npm install`：

```bash
npm test
npm run validate
npm run build
```

- `npm test`：规则、确定性、单次点击、三种结果、重置、事件日志、布局、渲染、20 seed 与 200 seed 审计。
- `npm run validate`：写出 `reports/validation-report.json`，记录 24 × 40 空间点、50ms 时刻采样和完整指标。
- `npm run build`：将 `src/*.ts` 擦除类型并生成原生浏览器 ESM 到 `dist/`。

## 结构

```text
src/rules.ts       固定步长纯规则模拟与解析评估
src/generator.ts   由参考点击、共同归心时刻反向生成关卡
src/solver.ts      (x,y,t) 穷举、连通分量、热图与 200 seed 审计
src/session.ts     局生命周期、即时重试、下一关与后台恢复
src/render.ts      Canvas 表现层，不参与规则判定
src/events.ts      仅 localStorage 的本地事件日志
src/app.ts         浏览器输入和固定步长会话驱动
src/debug.ts       解区热图调试页
```

当前自动门槛通过只支持“技术 KEEP”：它证明局面可解、解区连续、乱点率受控和状态可复现，不证明陌生真人看懂或觉得好玩。真人可读性与重复游玩意愿仍需按决策记录另行测试。
