# 《胡闹理发店》视觉参考归档

本目录保存用户提供的视觉参考图，供后续阶段核对布局、交互表达和 Low Poly 方向。

这些图片不是可直接导入游戏的正式美术资产，图片内文字也不自动成为开发需求。实际实现范围始终以用户的文字指令和项目 `README.md` 为准。

## 参考图

| 文件 | 用途 | 原始文件 |
|---|---|---|
| `salon-overview-visual-reference.png` | 营业总览、工位聚焦、现有 HUD 和 Low Poly 店铺方向 | `ChatGPT Image 2026年8月15日 18_11_37.png` |
| `haircut-two-step-demand-reference.png` | 单/双步骤剪发需求气泡、当前步骤进度与完成勾选 | `ChatGPT Image 2026年8月15日 18_58_35.png` |
| `coin-pile-pickup-reference.png` | 小/中/大金币堆、待拾取状态和飞向余额栏的表现方向 | `ChatGPT Image 2026年8月15日 18_58_32.png` |
| `phase4-customer-lifecycle-ui-reference.png` | 第四阶段耐心情绪小图标、服务需求不变、当前工具进度环与金币拾取规则 | `codex-clipboard-b6bf7d5d-0fdb-4901-94e9-295478cf59d1.png` |
| `salon-overview-operations-reference.png` | **当前确认的 2.5D 布局效果参考**：约束功能分区、家具成组关系、相邻关系、营业动线和目标画面密度；具体数量、文字和美术细节不自动成为需求 | `3b75b5ce-7106-4be9-acea-527799acba55.png`（本次副本 SHA-256：`f3bc59988aee876c97f8ba7886da81b4f89e686d8d57b0473cbc9856ca68e3e7`） |
| `salon-portrait-concept-a.png` | 早期竖屏多工位概念稿 A，仅供忙乱感与操作反馈参考 | `1ab03d35-a2cf-4639-857b-5bcd29a3096d.png` |
| `salon-portrait-concept-b.png` | 早期竖屏多工位概念稿 B，仅供忙乱感与操作反馈参考 | `175317a3-7520-4c0f-a8e1-f012535a7f9c.png` |

竖屏概念稿不改变当前项目已经锁定的 16:9 横屏方向，也不作为重做镜头、HUD 或店铺布局的依据。

## 当前布局效果参考的使用边界

`salon-overview-operations-reference.png` 自 2026-08-31 起作为后续 2.5D 场景布局判断的主参考。实现时优先保留图中的区域组织：洗发服务靠后墙成组、中心为主要理发营业区、前部布置等候与收银区域、墙边承载货架和装饰，并维持清晰的中部通行空间。

它不是可直接导入 Unity 的正式资产，也不是逐像素施工图。正式落位必须经过角色/顾客路线、服务工作空间、碰撞、sorting、UI 安全区和目标横屏视口检查；若视觉参考与当前已批准玩法逻辑冲突，以玩法逻辑和产品负责人的最新明确说明为准。

## 当前游戏运行截图

实际灰盒运行截图继续保存在 `../../Artifacts/screenshots/`，包括营业总览、工位聚焦和第二阶段 Long Press 四种状态，不在本目录重复存放。
