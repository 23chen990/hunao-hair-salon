# 《相位涟漪》本地测试包

这是独立工作室品牌号建立之前的最终本地测试包。它只用于本机试玩、观看 7 条真实玩法素材和核对证据；现在不得发布，也不得使用私人账号。

## 5 分钟开始

### 1. 启动本地试玩（约 1 分钟）

需要系统已安装 Python 3；不需要安装项目依赖，也不会自动打开浏览器或联网。

在“终端”中进入解压后的 `package/` 目录，然后运行：

```bash
./playable/start_local_server.sh
```

看到 `Serving HTTP` 后，手动在浏览器打开：

```text
http://127.0.0.1:4188/
```

关闭方法：回到终端按 `Control-C`。服务只绑定本机 `127.0.0.1`。

如果脚本不能执行，可使用同等命令：

```bash
python3 -m http.server 4188 --bind 127.0.0.1 --directory playable
```

### 2. 试玩（约 1 分钟）

- 棋子向红色边界移动时，只能在圆盘内点击一次。
- 涟漪按距离先后让棋子折返；目标是让它们几乎同时回到中心。
- 成功后轻触进入下一 seed；越界或同步超时后轻触重试当前 seed。
- 这是已验证的带皮肤浏览器切片，没有新增玩法、登录、广告或联网功能。

### 3. 观看真实素材（约 2 分钟）

先看 `media/phase-ripple-contact-cover.png`，再严格按以下顺序打开 7 条静音 MP4：

1. `media/01-sync-4.mp4`
2. `media/02-near-miss-4.mp4`
3. `media/03-early-click-4.mp4`
4. `media/04-later-success-4.mp4`
5. `media/06-sync-3.mp4`
6. `media/07-sync-5.mp4`
7. `media/05-sync-6.mp4`

每条均为真实 Canvas 输入和规则结果：390×844、25 fps、6.40 秒、H.264/MP4、无音轨。

### 4. 核对完整性（约 1 分钟）

在 `package/` 目录运行：

```bash
shasum -a 256 -c SHA256SUMS
```

所有条目都应显示 `OK`。随后阅读：

- `LOCAL_TEST_CHECKLIST.md`：逐项本地验收。
- `evidence/LOCAL_VERIFICATION.md`：本次独立复验命令与结果。
- `evidence/capture-run.json`：真实点击、触发顺序和结果。
- `evidence/media-report.json`：媒体规格与 SHA-256。
- `evidence/verification-report.json`：源验证包的最终验收与工具限制。
- `FUTURE_RELEASE_ORDER.md`：仅在未来用户门槛完成后执行的唯一顺序。
- `BOUNDARIES.md`：不得越过的边界。

## 当前唯一下一步

用户日后亲自建立一个独立、长期使用的工作室品牌号。完成前停止在本包的本地试玩和核验阶段：不登录、不建号、不发布、不发消息、不投放、不付款、不提交审核，私人账号永不用于本项目。
