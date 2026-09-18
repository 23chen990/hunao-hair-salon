# 包来源与路径规范化

本包于 2026-08-10 从仓库内的既有带皮肤切片和验证包组装，不新增玩法，不重新编码媒体。

## 来源

- 可玩构建：`prototypes/phase-ripple-skinned/`。`dist/` 已在隔离的临时副本中重新构建，并与源目录现有 `dist/` 逐文件一致。
- 可玩位图：`prototypes/phase-ripple-skinned/assets/tabletop-board.png`、`piece-outbound.png`、`piece-returned.png`。
- 7 条 MP4：`prototypes/phase-ripple-skinned/validation/videos/`。
- 联系人封面：`prototypes/phase-ripple-skinned/validation/cover/phase-ripple-contact-cover.png`。
- 证据：`prototypes/phase-ripple-skinned/validation/evidence/`。
- 生成式位图来源：同目录的 `ASSET_PROVENANCE.md`。

## 未复制的连续原始录屏

为控制包大小，没有复制连续 raw WebM。其仓库相对路径与 SHA-256 为：

```text
prototypes/phase-ripple-skinned/validation/raw/phase-ripple-continuous.webm
6a0eb8a5d2beb84e8fb00fd29db8c12468d515fdae19b7be2f84d6725f82e9ba
```

源文件规格：390×844、25 fps、95.4 秒、VP8、无音频流。需要追溯时应在原仓库中按上述相对路径和 SHA-256 核对；本包不依赖 raw WebM 才能试玩或查看 7 条成品。

## 路径规范化

`capture-run.json`、`media-report.json` 和 `verification-report.json` 是源证据的包内副本。为避免泄露本机用户名，包内副本只将本机绝对路径规范化为仓库相对路径或包内相对路径；玩法结果、seed、点击、时间、媒体规格和 SHA-256 未改变。

`verification-report.json` 中 Playwright FFmpeg 的本机绝对路径也被替换为不含用户名的可用性描述。源报告明确记录 `ffprobe` 不可用；本包不声称 `ffprobe` 通过。
