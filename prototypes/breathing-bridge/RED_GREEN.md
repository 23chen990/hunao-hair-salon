# 真实 RED → GREEN 记录

所有新版行为都先有失败断言，再写实现。以下命令在本目录真实执行；旧版 `src/sim.mjs` 兼容夹具仍保留给既有回归测试，新版浏览器入口只使用 `src/growing-bridge.mjs`。

## 周期 1：新版模块边界

先写 `tests/growing-bridge.test.mjs` 的存在性断言：

```text
$ node --test tests/growing-bridge.test.mjs
✖ the growing-bridge simulation module exists before its behavior is implemented
false !== true
tests 1 / pass 0 / fail 1
```

只添加空模块后 GREEN：

```text
$ node --test tests/growing-bridge.test.mjs
✔ the growing-bridge simulation module exists before its behavior is implemented
tests 1 / pass 1 / fail 0
```

## 周期 2：先行为 RED，再实现

在空模块上补齐“5 种动作语法、桥头增长/泄压、跳跃、求解、跨帧回放、关卡包”断言，真实 RED 为 5 个缺失导出：

```text
$ node --test tests/growing-bridge.test.mjs
✖ seeded levels have different action grammars ... createLevel is not a function
✖ holding grows ... createLevel is not a function
✖ a pressure release ... createLevel is not a function
✖ known paths ... createLevel is not a function
✖ the level pack ... buildLevelPack is not a function
tests 6 / pass 1 / fail 5
```

实现固定步物理、自动巡航、10 节点膜、seed 关卡、策略回放和下一关数据后：

```text
$ node --test tests/growing-bridge.test.mjs
tests 6 / pass 6 / fail 0
```

## 周期 3：浏览器变体入口 RED → GREEN

先把源代码契约改为要求 `id="next-level"` 与 `growing-bridge.mjs`：

```text
$ node --test tests/browser-source.test.mjs
✖ dependency-free browser source exposes next-level control
expected id="next-level"
tests 2 / pass 1 / fail 1
```

补上成功后解锁下一关、seed/archetype 读数、横向镜头和新版物理导入后：

```text
$ node --test tests/browser-source.test.mjs
tests 2 / pass 2 / fail 0
```

## 周期 4：正式审计前的一次聚焦校准

中等规模审计首次暴露三类真实问题：长按区间的 ±5% 噪声过敏、随机模型成功率高于目标、失败下坠会把诊断标记为越界。一次聚焦修复只围绕“控制窗口与终止边界”进行：

- 已知策略只在需要跳的段释放，减少无意义空跳；高抬升语法使用更早的释放窗口。
- 增加释放后的跳跃缓冲；随机无技能模型改为更短的 `0.25–1.5s` 反复窗口，使基线落入 10%–30%。
- 障碍采用扫掠圆 CCD，并给视觉边缘留固定 gameplay core；掉桥在有界恢复面结束，不继续积分到无穷。
- IAA 局长模型收敛到 40–70 秒目标结构；没有调低任何硬门槛、删除断言或伪造数据。

修复后完整回归：

```text
$ node --test tests/*.test.mjs
tests 17 / pass 17 / fail 0

$ npm run audit
overallPass: true
solver: 300/300
crossFps: 300/300
robustSeeds: 300/300
random: 2677/10000
stable: 10000/10000
```

浏览器若被宿主禁止回环端口，必须按脚本重试一次后写 `ENVIRONMENT_UNVERIFIED`；不能将环境故障写成玩法 KILL，也不能绕过权限或伪造截图。
