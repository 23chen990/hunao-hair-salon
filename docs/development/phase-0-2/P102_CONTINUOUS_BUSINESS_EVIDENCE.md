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

## P1-02 有限收尾

- 收尾运行源码：`f26c6630b98ee8f2ce3fa51d734a0c8c142b3635`
- 保存失败专项：第一次 Save 失败，点击“重试保存”第二次成功；结算历史、余额和施工投入只保留一份。
- Unity EditMode：676/676，通过；XML：`Builds/P102-finish-editmode.xml`。
- 当前 Demo 真实触摸低表现路线：`0/3 → Result → ClosedManagement → Day 2 PreOpen`，`maxWaiting=4`，`errors=[]`；报告：`Builds/P102-LowScriptCurrentBuild/report.json`。
- 更新脚本命令：`python3 tools/check-mobile-salon.py --failure --skip-haircut-checks --evidence-dir Builds/P102-LowScriptCurrentBuild`，结果 PASS。`--failure` 现代表低表现续行路线，已不再点击旧 Retry。
- Chromium 的 `Ignored attempt to cancel a touchstart event with cancelable=false` 作为 warning 单独记录，不吞掉其他错误；本次报告 warnings 为空。
- 目标 C 的非零收入、部分施工、日结边界刷新恢复：NOT_RUN/BLOCKED。当前环境没有可审阅的人工画面操作录制，不能用隐藏状态脚本替代正式玩家输入。

本次正式 Demo 构建的完整入口哈希：

| 文件 | SHA-256 |
| --- | --- |
| `Builds/WebGLDemo/index.html` | `e5222a6532d3e999a21c6ded7d72344062c9fc4335f8d9e16872db4e5c88a8a0` |
| `Builds/WebGLDemo/Build/WebGLDemo.loader.js` | `a0d2b837932a423665c30714ce0ddc21daa1ac18302180ad40e2dcadae70a7f2` |
| `Builds/WebGLDemo/Build/WebGLDemo.framework.js` | `28171c8d35e7530226c80e2c7573bab969efde660d34f7768fe3210546d70527` |
| `Builds/WebGLDemo/Build/WebGLDemo.wasm` | `12d3ed833cdea93248fcfed6dbf659bad3cf6157cd859439ce56d89257614bb2` |
| `Builds/WebGLDemo/Build/WebGLDemo.data` | `69e8a00c026d51a4ce7b09d68ad08be1fb1786950a9169ac0edcbc159cf40db6` |
