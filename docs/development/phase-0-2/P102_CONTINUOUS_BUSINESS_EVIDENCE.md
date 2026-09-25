# P1-02 持续按天经营证据摘要

## 版本

- 仓库：`23chen990/hunao-hair-salon`
- 工作区：`/Users/kker/Documents/ChatGPT/game2`
- 分支：`phase-0-2-gameplay-gate`
- BASE_SHA：`3761099cee84e4217bedc9ea9ee8e0bace95df66`
- TESTED_SOURCE_SHA：`f3cd320a80eac599f92ffa795b9187c430d96f42`
- Unity：`6000.5.8f1`
- PR：`#1`，Open，非 Draft

## 玩家可见路线

### 路线 A：持续经营

使用当前 `Builds/WebGLDemo` 的 Chromium 844×390 真实触摸输入：

`PreOpen → Business 3/3 → Business 5/3 → ClosingGrace → Result → ClosedManagement → Day 2 PreOpen`

达到旧目标后仍保持 Business；自然营业结束前继续接待。记录和截图位于本机临时证据目录 `Builds/P102-MobileEvidence/`，关键状态同时由生产入口回归测试覆盖。

### 路线 B：低表现积累

使用真实开始营业、等待营业结束、进入闭店经营、准备下一天和刷新输入：

`0/3 Result → ClosedManagement → Day 2 PreOpen → 刷新后 Day 2 PreOpen`

未达标没有显示 Retry，日期只增加一次。余额为 0 的正式输入记录位于本机临时证据目录 `Builds/P102-MobileLowEvidence/`；余额与部分施工款保留由 `SalonMobileCheckpointRegressionTests` 的生产入口夹具覆盖。

## 自动验证

- Node 资产测试：17/17，通过。
- Unity 全量 EditMode：675/675，通过；XML：`Builds/P102-all-editmode.xml`。
- UI 字体：364 required CJK glyphs，629 packaged glyphs，通过。
- Manifest/资源检查：通过。
- WebGL：Demo、Asset Lab、Candidate、Reference Visual 构建通过。
- Chromium 844×390：四场景启动、截图、无资源请求错误，通过。

## 构建入口哈希

以下文件来自 TESTED_SOURCE_SHA 对应的 WebGL 构建：

| 入口 | SHA-256 |
| --- | --- |
| `Builds/WebGLDemo/index.html` | `e5222a6532d3e999a21c6ded7d72344062c9fc4335f8d9e16872db4e5c88a8a0` |
| `Builds/WebGLAssetLab/index.html` | `d3742c8a80b59e1f759ed9ecf349cfa40c46714c3cd65944ed84bc966e418132` |
| `Builds/WebGLCandidate/index.html` | `fc6001a169be64c2fca1d0080171d80adda3608fede5932b07ea98792bd97031` |
| `Builds/WebGLReferenceVisual/index.html` | `a1d36c4b0039850c46b6b301a125b458887473470ce04c6844cce62ae955bc73` |

旧版 `check-mobile-salon.py` 仍含“接客不得超过 target”的历史断言，因此在新规则路线 A 的 5/3 状态处会报旧断言；这不是生产入口运行错误。其余真实输入状态已记录在上述路线摘要和本轮测试中。
