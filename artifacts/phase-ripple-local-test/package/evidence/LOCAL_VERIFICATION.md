# 2026-08-10 独立本地复验

为遵守“不得修改现有源目录”，所有会写构建、截图或报告的命令均在系统临时目录下的原型副本运行。本包复制的是该次构建确认一致的产物和已验收媒体。

## 命令与结果

| 命令 | 结果 |
|---|---|
| `npm test` | 通过：10/10；资产契约、20 个浏览 seed、真实一次点击、重试/下一关、双视口布局、位图渲染 |
| `npm run build` | 通过：11 个 TypeScript 模块构建到 `dist/`；仅出现 Node 原生类型剥离的实验性警告 |
| `npm run validate` | 通过：200/200 seed；0 无解、0 非连续、0 过易、0 确定性失败、0 重置失败；最大离散乱点成功率 12.4058%，均值 8.3055% |
| `node --test validation/tests/*.test.mjs` | 通过：3/3；真实场景清单、坐标映射、连续逐关录制计划 |
| `python3 -m unittest validation/tests/media_tools_test.py` | 通过：2/2；FourCC 解码与 6.4 秒分段边界 |
| `python3 validation/verify.py` | 通过：7/7；每条 390×844、25 fps、6.40 秒、H.264、静音，实际结果与预期一致；封面存在 |
| `node tools/browser-verify.mjs`（使用项目指定 Node 绝对入口） | 通过：390×844 参考点击成功并进入 seed 29；430×932 坏点击 `timeout` 且重试事件存在；两视口控制台错误均为 0 |
| `diff -qr <源 dist> <临时重建 dist>` | 通过：无差异 |
| `./playable/start_local_server.sh` | 通过：Python 3 静态服务只绑定 `127.0.0.1:4188`，未自动打开浏览器 |
| `curl -fsS http://127.0.0.1:4188/` | 通过：HTTP 200 返回包内 `playable/index.html` |
| `Control-C` | 通过：本地服务正常退出 |

## 工具与限制

- `ffprobe` 在源验证机上不可用，没有声称其通过。
- Playwright 自带 FFmpeg 可核对 raw WebM 的 VP8、390×844、25 fps 和无音频，但其定制构建不能解析最终 MP4。
- 最终 MP4 由 OpenCV 的 FFmpeg 后端核对编码/尺寸/帧率/时长，由系统 `file` 核对 MP4 容器，由 `afinfo` 的无可读音轨结果核对静音。
- 首次在受限沙箱启动无头 Chromium 因 macOS Mach 端口权限被拒；获准在本机沙箱外以无头模式重跑后通过。没有打开可见浏览器，也没有联网。
- 首次在受限沙箱绑定回环端口被拒；获准仅绑定本机回环地址后，首页读取与 `Control-C` 关闭均通过。
- 这些结果不替代陌生真人可读性、第二局意愿、留存、市场或收益验证。
