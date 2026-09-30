# 核心体验返工：成熟游戏与玩家心理参考

研究日期：2026-09-30。产品负责人要求“多参照成熟的游戏，研究玩家心理，不要自己想”。可靠程度写在每条后面；没有可靠来源的具体数值不照搬。

## 解锁节奏

- **《我的完美酒店》（SayGames）首局逐分钟记录**：开局 $50、立即服务第一位客人，没有文字教程；约半分钟后花 $30 开第二间房（约 1.5 位客人的收入），买完立即出现下一间 $50；1:46 出现雇清洁工；约 5:00 厕所与储物间一起开放，此时才开始补厕纸；9:26 雇前台；12 分钟共 5 项设施、2 名员工。来源：[ARPU Brothers 拆解](https://arpubrothers.com/blog/my-perfect-hotel-arcade-idle-deconstruction/)（媒体实测，中强度）。另一篇 [Design Breadcrumbs](https://martinjurasek.substack.com/p/900-seconds-with-my-perfect-hotel) 指出前 15 分钟没有每日任务和硬货币，玩法逐步出现，玩家不被淹没。
- **《Burger Please!》（Supercent）**：开局给 $180，玩家自己花钱建入口 $25、桌子 $5、汉堡机 $50、收银台 $100；约 3 位客人后买第二张桌子 $50。来源：[ARPU Brothers 拆解](https://arpubrothers.com/blog/burger-please-deconstruction-of-the-game/)（中强度）。
- **《猴子超市》（TinyDobbins/Poki）**：开局一种作物、一个货架、一个收银台；靠近即自动干活；赚钱后在场景里解锁新产品，顾客只买店里有的；买齐一家店全部货架和设备后自动开放下一家。第三方攻略转述开发者称每家店约 20–25 分钟（未核实）。可靠价格表未找到，玉米约 200 为单一攻略说法。来源：[Poki](https://poki.com/en/g/monkey-mart)、[第三方攻略](https://monkeymartgame.co.uk/guides/all-marts-explained/)（低/中强度）。
- **反例：Idle Barber Shop Tycoon（Codigames）**按剧情章节开放家具，玩家评价“每天打开一次选件家具”“很快就腻”。来源：Google Play 用户评论（低强度，仅作风险提示）。
- **Homa / Unity LevelUp**：玩法要在几秒内看懂；几分钟内要让玩家看到长期目标；每个新区域更贵、成本非线性增长。来源：[Homa](https://medium.com/ironsource-levelup/arcade-idle-creating-the-new-hybridcasual-genre-10d7f9dac21f)（中强度）。

换算到本作（一天 180 秒）：首个解锁约在第 2 位顾客后；前 10 分钟约每 1–2 分钟一项；补货这类附加负担放在第二天左右、随需要它的设施一起出现；整店约 18–20 分钟建完，接近《猴子超市》一家店。

## 玩家心理

- **目标梯度**：越接近奖励越努力。咖啡店集点卡实验中，顾客离免费咖啡越近购买越频繁。来源：[Kivetz, Urminsky & Zheng 2006](https://business.columbia.edu/faculty/research/goal-gradient-hypothesis-resurrected-purchase-acceleration-illusionary-goal)（高强度）。落地：下一个施工点一直可见并写“还差多少”，第一位顾客结账后接近完成。
- **先送进度**：洗车集点卡实验中，预盖 2 个章的卡完成率 34%，空白卡 19%。来源：[Nunes & Drèze 2006](https://academic.oup.com/jcr/article-abstract/32/4/504/1787425)（高强度）。落地：开局送 100 金币。
- **一次只教一件事、不写教程**：《我的完美酒店》无文字教程、补货到第 5 分钟才出现；《胡闹厨房》开发者强调每关带来新东西。落地：每个施工点只教一个新动作；先用吹风机教“开始—走开—回来收尾”，洗头台只多教“带顾客换工位”。

## 耐心与失败反应

- **《Diner Dash》（Glu 官方帮助页）**：顾客头顶用爱心表示满意度；只剩一颗心时闪烁；快离开时眼睛冒火；爱心掉光就离开。来源：[Glu Help Center](https://glumobile.helpshift.com/hc/id/12-diner-dash/faq/283-why-are-my-customers-flashing/?hc_location=ufi&p=all)（高强度）。另有设计分析指出顾客生气地离开会直接吃掉小费，形成时间压力。
- **《料理妈妈》**：步骤失败时妈妈眼睛喷火并说“别担心，妈妈来帮你修好！”，夸张反差形成搞笑感。来源：[Wikipedia](https://en.wikipedia.org/wiki/Cooking_Mama_(video_game))（中强度）。
- 落地：等待分“平静—不耐烦—冒火”三段，最后气冲冲走出店门；剪过头、泡沫放太久、吹过头都有夸张、短促、能一眼看懂的反应，且不打断玩家继续操作。
