# 本地验收清单

## 包与完整性

- [ ] 当前目录包含 `START_HERE.md`、`playable/`、`media/`、`evidence/`、`manifest.json` 与 `SHA256SUMS`。
- [ ] `shasum -a 256 -c SHA256SUMS` 的全部条目为 `OK`。
- [ ] `manifest.json` 可被 JSON 解析，且列出的路径与实际包内容一致。
- [ ] 包内没有 `node_modules`、缓存、隐藏文件或绝对路径。

## 本地试玩

- [ ] 运行 `./playable/start_local_server.sh` 无需安装依赖。
- [ ] 服务只监听 `127.0.0.1:4188`，不会自动打开浏览器。
- [ ] 手动打开 `http://127.0.0.1:4188/` 后出现带皮肤圆盘、棋子和实时涟漪。
- [ ] 圆盘只接受一次点击，第二次点击不会改变本局输入。
- [ ] 成功后轻触进入下一 seed。
- [ ] 越界或同步超时后轻触重试当前 seed。
- [ ] 回到终端按 `Control-C` 能关闭服务。

## 7 条真实玩法素材

- [ ] `01-sync-4.mp4`：seed 17，4 颗，同步成功。
- [ ] `02-near-miss-4.mp4`：seed 17，4 颗，固定步惜败。
- [ ] `03-early-click-4.mp4`：seed 17，同一点过早点击失败。
- [ ] `04-later-success-4.mp4`：seed 17，同一点稍晚点击成功。
- [ ] `06-sync-3.mp4`：seed 223，3 颗，同步成功。
- [ ] `07-sync-5.mp4`：seed 293，5 颗，同步成功。
- [ ] `05-sync-6.mp4`：seed 167，6 颗，同步成功。
- [ ] 7 条素材均能播放，画面竖屏、静音且没有登录信息、账号昵称或投放链接。
- [ ] `phase-ripple-contact-cover.png` 可正常打开。

## 证据与边界

- [ ] `capture-run.json` 的 7 个 `actualOutcome` 与 `expectedOutcome` 一致。
- [ ] `media-report.json` 记录原始连续 WebM 的源相对路径和 SHA-256。
- [ ] `verification-report.json` 明确记录本机没有 `ffprobe`，没有伪称其通过。
- [ ] `ASSET_PROVENANCE.md` 记录生成式位图的来源、提示词摘要和处理方式。
- [ ] `FUTURE_RELEASE_ORDER.md` 的唯一顺序为 `01→02→03→04→06→07→05`。
- [ ] 已确认当前不得发布、不得使用私人号、不得投放或付款。
- [ ] 已确认本包不能证明真人可读性、留存、市场需求或收益。

## 判定

- [ ] 全部通过：本地包可交给用户本人留存，等待唯一用户门槛。
- [ ] 任一失败：停止，不发布；记录失败项并重新生成或核验本地包。
