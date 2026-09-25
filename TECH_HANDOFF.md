# 《胡闹理发店》技术交接资料

> 审计时间：2026-09-18（Asia/Shanghai）  
> 审计口径：以当前工作区代码为准，而不是只以历史设计文档为准。本文是在当前工作区已经存在未提交修改的前提下整理的；本次任务没有修改业务代码、场景或资源，只新增本交接文档，并运行了既有自动检查。  
> 结论中的“无法确认”表示当前代码、配置和可观察运行结果不足以证明该功能，不代表设计文档中的意图。

> **续接提示（2026-09-23）**：上面的审计是历史记录，不能覆盖当前工作区。9 月 20–22 日的玩法修复已经改变了移动主路径、收入结算、设备占用提示和离店时序；请先阅读本文 §17 和根目录 `STATUS.md`，再决定下一步。旧文中“默认 2D”“完成订单生成金币堆并收取”“调试/候选浏览器门禁失败”等描述均已过期。

## 0. 一页结论

当前项目已经是一个可以运行的 Unity 理发店 Demo，主路径可以完成：开店、顾客进入、排队、玩家接待/分配工位、洗头/剪发/吹发的主要流程、顾客完成、付款掉落与收取、营业结束、结算、进入下一天管理界面。

但需要特别注意：

- 当前普通启动路径是手机式简化 2D 玩法，不是设计文档中所有服务都完整展开的桌面版流程。
- 洗头在普通移动路径中被压缩成一次定时服务；更细的喷水、洗发、冲洗、毛巾等步骤主要存在于桌面/开发/服务架构路径。
- 染发、烫发的代码和部分 UI/领域规则存在，但当前普通移动订单池没有使用它们。
- “收银”不是独立的顾客状态或柜台阶段；完成订单后在工位附近生成金币，玩家走近自动/手动收取。
- 没有独立的 `CustomerStateMachine`、`CustomerSpawner`、通用事件系统或通用升级/扩建系统。
- 当前最大结构问题是 `SalonDemo` 过大、移动路径和桌面路径语义分叉、顾客状态在多个对象中重复保存。

## 1. 项目基本信息

### 1.1 引擎、语言和平台

| 项目 | 当前真实值 |
|---|---|
| 引擎 | Unity |
| Unity 版本 | `6000.5.8f1`，revision `5cb7df797b7d` |
| 主要语言 | C# |
| 辅助工具语言 | Node.js/JavaScript（资产管线测试），Python（构建检查、Chromium 检查） |
| Unity 包 | `com.unity.ugui`、Unity Animation、ScreenCapture、Unity Test Framework `1.4.5` |
| 正式运行平台 | 当前验收目标为横屏 WebGL；也存在 macOS Standalone 开发构建入口 |
| 浏览器验收尺寸 | `844 x 390`，DPR 1，移动 UA，Playwright/Chromium |
| 当前 Git branch | `main` |
| 当前 HEAD commit | `a71e7279b5e21909a35ac4186b755c3d123d5ffa` |
| 当前 HEAD 提交标题 | `chore: checkpoint hair salon pipeline baseline` |

Unity 版本来源是 `unity-hair-salon/ProjectSettings/ProjectVersion.txt`，包来源是 `unity-hair-salon/Packages/manifest.json`。

### 1.2 启动和运行

编辑器运行：

1. 使用 Unity `6000.5.8f1` 打开 `unity-hair-salon/`。
2. 打开 `Assets/Scenes/HairSalonDemo.unity`。
3. 点击 Play。
4. 普通模式启动时，`SalonDemo.Start` 会动态建立相机、灯光、地图、工位、玩家、顾客视图和 HUD；场景本身不是一个完整静态摆好的场景。
5. 普通移动路径会进入开店/营业界面，使用触摸摇杆或键盘移动，靠近目标后使用交互按钮。

命令行可用入口主要在 `unity-hair-salon/Assets/Editor/BuildScript.cs`：

- `BuildScript.BuildMac`：开发版 macOS Standalone，输出到 `unity-hair-salon/Builds/HairSalonDemo.app`。
- `BuildScript.BuildWebGLDemo`：正式 Demo WebGL，输出到 `unity-hair-salon/Builds/WebGLDemo`。
- `BuildScript.BuildWebGLAssetLab`：资产测试场景 WebGL。
- `BuildScript.BuildWebGLCandidate`：候选资产场景 WebGL。
- `BuildScript.BuildWebGLReferenceVisual`：参考视觉场景 WebGL。
- `BuildScript.ValidatePipeline`：Manifest、资源、场景和贴图策略检查。

一键检查脚本是根目录 `tools/check-project.sh`。它顺序执行 Node 资产测试、字体检查、Unity EditMode 测试、XML 结果校验、Manifest 校验、四个 WebGL 构建和 Chromium 检查。

### 1.3 当前 Build / Play 状态

本次实际检查结果：

| 检查 | 结果 | 说明 |
|---|---|---|
| Node 资产管线测试 | 通过 | `17/17` 通过，`0` 失败 |
| UI 字体检查 | 通过 | 检查到 `366` 个必需 CJK 字形，打包字体 `629` 个 |
| Unity EditMode | 通过 | XML：`595/595` passed，`0` failed |
| Manifest/资源校验 | 通过 | 日志为 `Startup, resources and manifest checks passed for 13 assets` |
| WebGL Demo 构建 | 通过 | `Build Finished, Result: Success` |
| WebGL AssetLab 构建 | 通过 | 成功 |
| WebGL Candidate 构建 | 通过 | 成功 |
| WebGL ReferenceVisual 构建 | 通过 | 成功 |
| Chromium 普通 Demo 核心流程 | 通过 | 运行标记包含 StartedBusiness、SpawnedCustomer、AssignedStation、CompletedService、CustomerFinished、PaymentCreated |
| Chromium Demo 调试模式 | 未通过 | `Demo 调试模式核心流程未通过` |
| Chromium 全量检查 | 未通过 | 候选场景未产生 `CANDIDATE_ALIGNMENT_READY` |

因此准确说法是：项目能够编译并完成 Unity/WebGL 构建，普通 Demo 的浏览器核心营业流程可运行；完整浏览器验收目前不能判定为全通过，调试模式和候选资产验收仍有失败。

## 2. 当前目录结构

以下只列核心目录和重要文件。

```text
game2/
├── AGENTS.md                         协作和验收规则
├── PROJECT_CONTEXT.md                产品背景；当前需与真实代码交叉验证
├── STATUS.md                         当前状态说明，可能包含历史/工作区信息
├── package.json                      Node 资产管线命令
├── tests/                            Node 资产管线测试
├── tools/
│   ├── check-project.sh              一键检查入口
│   ├── asset-pipeline.mjs            资产导入/Manifest 管线
│   ├── browser-check.py              WebGL + Chromium 截图/运行标记检查
│   ├── check-ui-font.py              UI 字体检查
│   ├── serve-salon.py                本地 WebGL 服务辅助脚本
│   └── check-*.py                    其他专项检查
├── assets/
│   ├── inbox/                        新图像进入位置
│   ├── previews/                     资产预览
│   ├── archive/                      归档/来源资产
│   └── source/                       工作区中存在的来源资源
├── .agents/skills/                   仓库级资产接入、视觉 QA、Bug 修复 Skill
└── unity-hair-salon/
    ├── Assets/
    │   ├── Editor/                   Unity 编辑器构建与管线检查
    │   ├── Resources/
    │   │   ├── AssetPipeline/        asset-manifest.json 等资产配置
    │   │   ├── CutStations/          剪发工位 JSON 数据
    │   │   └── Imported/Models/...   运行时资源和导入资产
    │   ├── Scenes/
    │   │   ├── HairSalonDemo.unity   正式 Demo 场景
    │   │   └── AssetTestLab.unity    独立资产实验室场景
    │   ├── Scripts/                  游戏运行时代码
    │   ├── Scripts/ServiceArchitecture/
    │   │   ├── Core/                 纯服务规则、状态、动作结果
    │   │   ├── Adapters/             服务架构与 CustomerModel 的桥接
    │   │   └── Presentation/         服务相关显示/接口辅助
    │   └── Tests/                    Unity EditMode 测试
    ├── Packages/manifest.json        Unity 包清单
    ├── ProjectSettings/              Unity 版本、场景、质量设置
    ├── Builds/                       本地构建、日志和验收截图；不应当作为源码依赖
    ├── Library/                      Unity 本地缓存
    └── Logs/UserSettings/            Unity 本地运行数据
```

核心目录职责：

- `Assets/Scripts`：玩法运行时的主要实现。`SalonDemo.cs` 负责集成和表现，`SalonGameModel.cs` 负责主营业模型，`BusinessDaySystem.cs` 负责天/营业阶段，`SalonPaymentModel.cs` 负责金币和付款。
- `Assets/Scripts/ServiceArchitecture`：较新的纯规则服务层。它不是整个游戏的唯一状态源，而是通过 `Stage3WashServiceAdapter` 投影回 `CustomerModel`。
- `Assets/Scripts/AssetPipeline`：资产 Manifest、工位/家具验证、接入和开发验收，不是玩家玩法系统。
- `Assets/Tests`：Unity EditMode 测试；当前测试覆盖度较高，但测试通过不等于所有浏览器模式视觉验收通过。
- `Assets/Resources/AssetPipeline/asset-manifest.json`：资产 ID、状态、方向、pivot、footprint、collision、shadow、anchors、sorting 等运行/校验数据。
- `Assets/Scenes/HairSalonDemo.unity`：正式入口场景。大量运行时对象由 `SalonDemo.BuildWorld` 和 `BuildHud` 创建。
- `Assets/Scenes/AssetTestLab.unity`：独立资产实验室；其诊断 UI 不应合入正式 HUD。

## 3. 当前游戏完整运行流程

下面按用户要求的完整流程说明；“顾客跟随”“收银”等名称是产品流程语言，代码中的真实状态可能不同。

### 3.1 启动游戏

`Bootstrap` 在没有找到 `SalonDemo` 时创建入口对象；随后 `SalonDemo.Start`：

1. 识别普通模式、浏览器 Smoke 模式和开发/验收参数。
2. 普通模式应用 `SalonMobileDayConfig`，启用移动简化路径和 2D 运行表现。
3. 创建 `SalonGameModel`、`BusinessDayController`、`ShopSatisfactionModel`、`CustomerTrafficDirector`。
4. 构建世界、玩家、工位、顾客视图、HUD。
5. 读取 `SalonProgressSave` 的 PlayerPrefs/JSON 状态。
6. 准备第 1 天或已保存的天数，进入开店界面。

已实现，但场景视觉和玩法对象主要是运行时生成，接手时不能只看 `.unity` 场景文件判断完整游戏。

### 3.2 进入营业

点击开店/开始营业后，`BusinessDayController.StartBusiness` 进入 `BusinessDayState.Business`，开始倒计时。`SalonDemo.Update` 每帧更新 Day、顾客模型、交通 Director、HUD 和玩家移动。

已实现。正式移动配置营业时长是 `180` 秒，另有最多 `15` 秒独立收尾；核心模型默认配置也为 `180` 秒，但默认收尾仍是 `30` 秒，最终移动入口以 `SalonMobileDayConfig` 为准。

### 3.3 顾客生成

`SalonDemo.MaintainCustomerFlow` 根据冷却时间和 `CustomerTrafficDirector.Evaluate` 判断是否生成。订单 ID 由 `SalonMobileDayConfig.PickOrderForSpawn` 或 `DayConfig.PickOrderForSpawn` 选择，再由 `SalonOrderCatalog.Get` 转换成服务列表，最后调用 `SalonGameModel.Spawn`。

已实现。当前有最大并发、等待容量、营业时间和 Director 压力控制。顾客对象的实际类型是 `CustomerModel`，不是名为 `Customer` 的 MonoBehaviour。

### 3.4 顾客等待

`SalonGameModel` 持有等待队列和顾客列表。新顾客从 `Entering` 进入 `Waiting`，视图沿 `SalonCustomerPath` 移动到固定等待位置；等待时耐心下降。

已实现。等待位和容量规则存在，但路径和位置有一部分写死在代码中。

### 3.5 玩家与顾客互动

普通移动路径中，玩家用摇杆/键盘移动；靠近顾客后，`SalonDemo.Mobile` 提供 Greet/Assign/Guide 等交互。桌面路径还有点击顾客、点击工位、工具栏按钮和服务操作。

已实现。交互入口存在两套：移动近距离交互和桌面点击/工具栏交互；二者并非完全同一套操作语义。

### 3.6 进入工位

玩家选择或引导顾客后，`SalonGameModel.Assign` 检查工位是否空闲，设置 `WorkstationModel.CurrentCustomerId`、工位状态和顾客的 `MovingToStation` 状态。经过移动时间后，`ConfirmStationArrival` 把顾客置为 `Serving`，工位置为 `AwaitingService`。

已实现。工位兼容性存在：洗头工位、剪发工位、烫发工位各自接受不同服务；错误工位目前会被记录并造成满意度影响，而不是一个强类型的路由失败。

### 3.7 执行服务

普通移动路径：

- Wash：通过 `BeginServiceExecution(ServiceType.Wash)` 做一次约 3 秒定时服务。
- Cut：通过剪发操作和定时/长按模拟完成。
- Dry：使用手动或自动吹发；移动路径主要使用自动吹发后台计时。

桌面/开发路径额外提供：喷水、洗发、冲洗、包毛巾、拆毛巾、剪发工具、手动吹发、自动吹发、染发、清理染料、烫发外部里程碑等。

因此“服务执行”整体部分完成：基础主循环可玩，但所有服务尚未在普通路径保持相同深度。

### 3.8 服务完成

`CustomerModel.Step`、`ServiceProgress` 和适配器中的 `OrderDefinition`/milestone 一起判断当前步骤是否完成。完成当前步骤后：

- 如果订单还有下一步且同一工位可继续，则继续 `Serving`。
- 如果下一步需要其他工位，则进入等待转移的逻辑，等待玩家再次分配。
- 所有需求满足且出口条件通过，则进入 `TryFinalizeCompletedOrder`。

已实现，但存在多份进度状态，长期维护有同步风险。

### 3.9 收银

代码没有 `Cashier` 顾客状态，也没有收银台业务类。完成订单时，`SalonPaymentModel.CreateOrderPayment` 创建 `PaymentDropModel`；`SalonDemo` 创建金币堆视图。玩家靠近金币堆后执行 `BeginCollection`，动画完成后 `CompleteCollection` 才增加余额。移动路径中还有附近自动收取逻辑。

因此按产品流程名称是“部分完成”：付款和收取存在，但独立收银阶段不存在。

### 3.10 顾客离开

完成订单后顾客短暂处于 `Finished`，随后进入 `Leaving`，沿固定离店路径移动，最终 `Exited` 并从活动顾客集合移除。耐心耗尽时会提前愤怒离开，通常没有正常付款。

已实现。顾客离开和结算回调由 `SalonDemo.HandleCustomerChanged` 等逻辑监听。

### 3.11 营业结束

营业倒计时归零后，`BusinessDayController` 进入 `ClosingGrace`。系统等待活动顾客处理完，超过 closing grace 后由 `ForceCloseRemainingCustomers` 强制收尾，然后进入 `Result`。

已实现。强制结束是一个直接状态收尾路径，可能绕过正常服务/离场过程，是后续状态机整理时的重点。

### 3.12 结算

`DayStats` 记录生成、完成、满意度、放弃、未服务、收入、支出和错误；`ShopSatisfactionModel` 在顾客终态结算全店满意度；`ShopReputationModel` 计算星级，但总开关 `SalonGameModel.ReputationSystemEnabled` 当前为关闭状态。移动版会判断当天目标订单数并记录完成天数/最佳订单数。

已实现，但声誉、全店满意度、顾客满意度三套指标并不完全等价。

### 3.13 下一天

结算完成后移动版进入 `ClosedManagement`/商店管理面板。可以购买自动吹发设备；点击下一天会清理当天付款掉落、重置/保存必要状态并调用 `BusinessDayController.PrepareDay`。

已实现。下一天的订单目标和订单池由 `SalonMobileDayConfig` 按第 1 天、第 2 天、3 天以上分支写死。

## 4. 当前已经实现的玩法系统

状态标签含义：

- 【已完成】：当前主路径或可验证的正式路径可以使用。
- 【部分完成】：有实际代码和可运行子路径，但主路径、数据或规则不完整。
- 【只有框架】：存在类型/预留接口/少量示例，但不能作为完整玩法使用。
- 【未实现】：当前代码没有可用实现。

| 系统 | 状态 | 当前真实实现 | 主要代码 |
|---|---|---|---|
| 玩家移动 | 【已完成】 | 移动版摇杆/键盘、碰撞/障碍和墙边滑动；桌面版有路线移动 | `SalonDemo.cs`、`SalonDemo.Mobile.cs`、`SalonMobileNavigation.cs`、`SalonPlayerRoute.cs` |
| 玩家交互 | 【已完成】 | 顾客/工位点击、移动近距离交互、工具栏、付款收取 | `SalonDemo.cs`、`SalonDemo.Mobile.cs`、`InteractionContext.cs` |
| 顾客生成 | 【已完成】 | Director 控制节奏，`SalonDemo` 选订单并调用模型 Spawn | `SalonDemo.cs`、`BusinessDaySystem.cs`、`SalonGameModel.cs` |
| 顾客 AI / 状态机 | 【部分完成】 | 有状态转换、耐心、离开、工位移动；没有独立 AI/状态机类 | `SalonGameModel.cs`、`SalonDemo.cs` |
| 等候系统 | 【已完成】 | FIFO 等待队列、等待容量、等待位、耐心下降 | `SalonGameModel.cs`、`SalonCustomerPath.cs` |
| 顾客跟随 | 【部分完成】 | 有移动版引导顾客和 `MovingToStation`，但没有独立 `Following` 状态，顾客不是完整跟随玩家行为树 | `SalonDemo.Mobile.cs`、`SalonGameModel.cs` |
| 工位系统 | 【已完成】 | `WorkstationModel`、占用/预约/到达、服务兼容性和工具能力 | `SalonGameModel.cs`、`CutStation/*` |
| 洗头 | 【部分完成】 | 领域层支持湿发、洗发、冲洗、毛巾等；普通移动路径压缩为一次计时服务 | `SalonGameModel.cs`、`Stage3WashServiceAdapter.cs`、`ActionResolver.cs` |
| 剪发 | 【已完成】 | 工具、进度、错误/完美/修复/过剪结果均有实现 | `SalonGameModel.cs`、`ServiceArchitecture/Core/*` |
| 吹发 | 【已完成】 | 手动长按、自动后台吹发、停止/超时/泡沫风险处理 | `SalonGameModel.cs`、`SalonDemo.Mobile.cs`、`Stage3WashServiceAdapter.cs` |
| 染发 | 【部分完成】 | 有染发处理、等待、清理、失败和工具/里程碑；普通订单池未使用 | `SalonGameModel.cs`、`Stage3WashServiceAdapter.cs`、`SalonDemo.cs` |
| 烫发 | 【部分完成】 | 有烫发工位和外部完成里程碑；普通订单池未使用 | `SalonGameModel.cs`、`Stage3WashServiceAdapter.cs`、`SalonDemo.cs` |
| 服务订单 | 【已完成】 | O001–O005 五个硬编码订单，支持多步骤 | `SalonServiceChain.cs`、`BusinessDaySystem.cs` |
| 工具系统 | 【已完成】 | `SalonTool`/`ServiceTool`、工位能力、工具合法性、工具栏 | `SalonGameModel.cs`、`ServiceArchitecture/Core/*`、`SalonDemo.cs` |
| 服务进度 | 【已完成】 | `CustomerModel.Step` 加 `ServiceProgress`/milestone；存在重复状态 | `SalonGameModel.cs`、`ServiceProgress.cs`、`Stage3WashServiceAdapter.cs` |
| 顾客满意度 | 【已完成】 | 顾客耐心、等待、错误工位、错误服务、事故影响，另有全店满意度 | `SalonGameModel.cs`、`ShopSatisfactionModel.cs` |
| 顾客性格 | 【未实现】 | 没有每个顾客的性格 traits；只有情绪、耐心和一个全局 `CustomerExperienceProfile` | `SalonGameModel.cs`、`SalonServiceChain.cs` |
| 收银 | 【部分完成】 | 工位生成 PaymentDrop，玩家附近收取并增加金币；没有 Cashier 阶段 | `SalonPaymentModel.cs`、`SalonDemo.cs`、`SalonDemo.Mobile.cs` |
| 金币 | 【已完成】 | 余额、订单奖励、小费、动画完成后入账、购买支出 | `SalonPaymentModel.cs`、`SalonGameModel.cs` |
| Day 系统 | 【已完成】 | PreOpen、Business、ClosingGrace、Result、ClosedManagement、重试、下一天 | `BusinessDaySystem.cs`、`SalonDemo.Mobile.cs` |
| 营业时间 | 【已完成】 | 营业倒计时、暂停、closing grace、强制收尾 | `BusinessDaySystem.cs`、`SalonGameModel.cs` |
| 店铺扩建 | 【未实现】 | 未找到区域扩建数据、购买点或实际扩建逻辑 | 无；`BusinessDaySystem` 只有未使用的投资枚举项 |
| 设备升级 | 【部分完成】 | 只有一个自动吹发台购买，固定价格和 bool 解锁，无等级系统 | `SalonServiceChain.cs`、`SalonGameModel.cs`、`SalonDemo.cs` |
| 扫地 | 【未实现】 | 未发现清扫动作、脏污状态或清扫工具 | 无 |
| 毛巾 / 洗衣 | 【部分完成】 | 毛巾包裹/拆除是服务状态；没有毛巾库存、洗衣机、脏毛巾循环 | `SalonGameModel.cs`、`ActionResolver.cs` |
| 事故系统 | 【部分完成】 | 有事故严重度、泡沫爆发、补救洗头、事故满意度惩罚 | `SalonServiceChain.cs`、`Stage3WashServiceAdapter.cs`、`SalonGameModel.cs` |
| 投诉系统 | 【未实现】 | 有不满、愤怒离开和满意度扣分，没有投诉对象、投诉处理流程 | 无独立投诉类 |
| 特殊顾客 | 【未实现】 | 没有顾客类型/标签/专属规则 | 无 |
| 特殊事件 | 【只有框架】 | 有 Rush 配置和洗发泡沫事故示例，没有通用事件运行时 | `BusinessDaySystem.cs`、`SalonServiceChain.cs` |
| 客流 Director | 【已完成】 | 根据阶段、Rush、拥堵、愤怒数、声誉倍率调整客流 | `BusinessDaySystem.cs` |
| UI | 【已完成】 | 运行时生成移动 HUD、工具栏、订单气泡、情绪、进度、暂停、结算、管理面板 | `SalonDemo.cs`、`SalonDemo.Mobile.cs`、各 `*View.cs` |
| 存档 | 【已完成】 | PlayerPrefs + JsonUtility 主/备份键，保存余额、天数、设备等安全边界状态 | `SalonProgressSave.cs`、`SalonDemo.Mobile.cs` |

“已完成”不等于“已数据驱动”或“已达到最终视觉质量”；这里只表示真实代码中可运行/可测试。

## 5. 核心类和数据关系

### 5.1 用户点名的类与真实名称

| 用户概念 | 当前代码事实 |
|---|---|
| `InteractionContext` | 存在，记录焦点顾客/工位、选择的服务工具、动作 token、上下文版本和物理版本 |
| `ActionResolver` | 存在，纯规则判断动作合法性并产生 `ActionResult` |
| `ActionResult` | 存在，描述动作结果、物理变化、里程碑、事故、指标和后续动作 |
| `ActionResultApplier` | 存在，把结果原子应用到服务聚合状态 |
| `CustomerPhysicalState` | 存在，保存湿度、泡沫、洗发、毛巾、污染、头发状态和物理版本 |
| `ServiceProgress` | 存在，保存服务 milestone 的状态和完成条件 |
| `Order` / `Requirement` | 没有同名 `Order` 主类；真实运行模型是 `OrderDefinition`、`ServiceRequirement`、`SalonOrderCatalog`，并投影到 `CustomerModel.Needs`/`Step` |
| `Station` | 没有通用 `Station` 主类；真实运行对象是 `WorkstationModel`；剪发资产侧另有 `CutStationData`/`CutStationFactory` |
| `Tool` | 没有通用 `Tool` 类；真实是 `SalonTool`、`ServiceTool` 枚举和工位工具列表 |
| `Customer` | 没有同名主类；真实顾客运行数据是 `CustomerModel` |
| `CustomerStateMachine` | 不存在；状态转换直接写在 `SalonGameModel.Tick`、`ChangeState` 和 `SalonDemo` 集成逻辑中 |
| `CustomerSpawner` | 不存在；`SalonDemo.SpawnRuntimeCustomer` 创建订单并调用 `SalonGameModel.Spawn` |
| `Director` | 没有通用 Director 基类；真实类是 `CustomerTrafficDirector` |

### 5.2 谁创建、谁调用、谁保存、谁负责规则

```text
Unity Scene / Bootstrap
        │
        ▼
SalonDemo.Start
        ├── 创建 SalonGameModel
        │       ├── 创建 SalonPaymentModel
        │       ├── 创建 WorkstationModel 列表
        │       └── 创建 Stage3WashServiceAdapter
        ├── 创建 BusinessDayController
        ├── 创建 CustomerTrafficDirector
        ├── 创建 ShopSatisfactionModel
        └── BuildWorld / BuildHud

BusinessDayController.Tick
        │
        ▼
SalonDemo.MaintainCustomerFlow
        ├── CustomerTrafficDirector.Evaluate
        ├── 选择 DayConfig / SalonMobileDayConfig 的订单
        ├── SalonOrderCatalog.Get
        └── SalonGameModel.Spawn
                │
                ▼
         CustomerModel + Needs + Step
                │
       Assign / ConfirmStationArrival
                │
                ▼
      Service action entry in SalonGameModel
                │
                ▼
Stage3WashServiceAdapter
   ├── InteractionContext / Action token
   ├── CustomerServiceState
   │     ├── CustomerPhysicalState
   │     ├── OrderDefinition
   │     ├── ServiceProgress
   │     └── CustomerMetricsState
   ├── ActionResolver.Resolve
   └── ActionResultApplier.Apply
                │
                ▼
Adapter.Project → CustomerModel
                │
                ▼
TryFinalizeCompletedOrder
        ├── SalonPaymentModel.CreateOrderPayment
        ├── DayStats
        └── Customer state Finished → Leaving → Exited
```

责任边界目前是混合的：

- `SalonGameModel` 保存顾客、工位、时间、耐心、服务生命周期和多数业务门槛。
- `Stage3WashServiceAdapter` 保存每个顾客的服务聚合会话，并把纯服务状态投影回 `CustomerModel`。
- `ActionResolver` 负责纯动作规则，不直接读取 `CustomerModel`。
- `ActionResultApplier` 负责校验 token/版本并原子更新 `CustomerServiceState`。
- `SalonDemo` 负责输入、动态场景、UI、视图、移动路径和把用户动作转成模型调用；它仍然承担了过多业务编排。
- `BusinessDayController` 负责 Day 阶段和 Day 统计，`CustomerTrafficDirector` 负责客流决策。
- `SalonPaymentModel` 保存当前余额和付款掉落；`SalonProgressSave` 保存跨天持久化状态。

## 6. 当前顾客状态机

### 6.1 实际状态

`CustomerState` 当前只有：

- `Entering`
- `Waiting`
- `MovingToStation`
- `Serving`
- `Finished`
- `Leaving`
- `Exited`

工位还有独立的 `WorkstationState`，包括 Available、Reserved、CustomerEnRoute、AwaitingService、AwaitingTransfer、InService、Rework、Completed。顾客没有单独的 `Following`、`AtStation`、`WaitingNextService` 或 `Cashier` 状态；这些概念由现有状态、字段和付款掉落组合表达。

### 6.2 真实转换

```text
Spawn
  → Entering
  → Waiting

Waiting -- Assign --> MovingToStation
MovingToStation -- 到达计时 --> Serving

Serving -- 当前服务完成且订单还有下一步 -->
  ├── 同一工位可继续：继续 Serving
  └── 需要转移：保留服务生命周期，等待下一次 Assign

Serving -- 所有需求完成且出口条件通过 --> Finished
Finished -- 反馈计时 --> Leaving
Leaving -- 离店计时 --> Exited / 从活动列表移除

Waiting / MovingToStation / Serving
  -- 耐心归零且当前不是不可打断操作 --> Leaving

营业结束 -- ClosingGrace 超时或强制收尾 -->
  ForceCloseRemainingCustomers → Exited / 统计未完成
```

### 6.3 最容易出 Bug 的转换和耦合点

1. `CustomerModel.State`、`CustomerModel.Step`、`WorkstationModel.State`、`ServiceProgress`、适配器会话和视图位置同时描述一个顾客，任意一个更新滞后都会出现“看起来在工位但模型还在移动”等问题。
2. `ForceCloseRemainingCustomers` 有直接赋值终态的路径，可能绕过普通完成、付款、满意度和离店动画。
3. 移动版服务和桌面版服务不是同一条动作序列；同一个订单在两个入口的“完成”语义不同。
4. `Assign` 允许错误工位进入流程并以计数/满意度惩罚处理，后续服务能否继续又由其他兼容性规则决定，错误状态的恢复路径复杂。
5. 服务适配器既保存领域状态又要 `Project` 回旧模型；加新 milestone 时容易出现双向同步遗漏。
6. `CustomerModel.CurrentNeed` 在订单完成时用 `Cut` 作为兼容性返回值，这是一个容易误导 UI/分配逻辑的哨兵语义。

## 7. 当前服务系统

### 7.1 订单如何生成

1. 顾客客流到达生成条件。
2. `SalonDemo.SpawnRuntimeCustomer` 按移动配置或 DayConfig 选订单 ID。
3. `SalonOrderCatalog` 从硬编码字典得到服务列表：

   - `O001`：Cut
   - `O002`：Wash + Dry
   - `O003`：Wash + Cut
   - `O004`：Cut + Dry
   - `O005`：Wash + Cut + Dry

4. `SalonGameModel.Spawn` 创建 `CustomerModel`，把服务列表放入 `Needs`，初始 `Step = 0`。
5. `Stage3WashServiceAdapter` 创建会话时再把 Needs 转成 `OrderDefinition`、milestone 和服务要求。

### 7.2 需求和进度保存位置

同一个订单目前有三层表示：

- `CustomerModel.Needs` + `CustomerModel.Step`：主游戏兼容层。
- `OrderDefinition` + `ServiceRequirement`：服务领域订单定义。
- `ServiceProgress` + milestone：动作层的完成状态。

这不是完全单一真相源。当前适配器通过同步/投影维持一致性，因此新增服务时应同时检查三层。

### 7.3 动作是否合法

合法性检查分两段：

1. `SalonGameModel` 做外部门槛：当前顾客、工位类型、是否移动中、工具/服务类型、是否可开始等。
2. `Stage3WashServiceAdapter` 创建带客户、工位、上下文版本、物理版本的 token，传入 `ActionResolver`。Resolver 基于订单快照、物理状态、上下文和动作请求输出 `ActionResult`；Applier 校验 token/版本和不变量后更新 `CustomerServiceState`。

### 7.4 正确、错误和额外操作

- 正确操作：更新物理状态、milestone、服务进度和顾客满意度/耐心，必要时进入下一步。
- 错误工具/错误工位/错误服务：记录错误服务类型、错误工位数、满意度惩罚或重做状态。
- 过剪、未请求的剪发、染发未清理、泡沫进入吹发等：可能产生低质量结果、事故或补救要求。
- 额外服务和不在当前需求中的动作由 `ServiceActionClassifier`/`ActionResolver` 分类。
- 没有投诉工单或投诉处理界面；错误多数转成满意度、情绪、事故或顾客离开。

### 7.5 多步骤订单

完成当前 step 后，如果下一 step 与当前工位兼容可以继续；不兼容时需要重新分配到下一个工位。洗头后的剪发/吹发，以及 Wash + Dry、Wash + Cut 等是当前已有多步骤路径。

### 7.6 工位、服务、工具是否解耦

只能说部分解耦：

- 服务动作规则和物理状态在 `ServiceArchitecture/Core` 中相对纯净，理论上可以独立测试。
- 但工位兼容性、工位 ID、服务点坐标和 UI 入口仍在 `SalonGameModel`/`SalonDemo` 中写死。
- `SalonTool` 和 `ServiceTool` 是两套相关枚举，工具到服务的映射仍由多处硬编码。
- 工位数据驱动程度在 `CutStation` 较高，但洗头/烫发等工位仍主要由 `SalonGameModel` 构造。

## 8. 当前时间 / 多任务机制

### 8.1 哪些行为会占用玩家

- 玩家手动剪发、手动吹发、正在进行的前台动作会设置 `PlayerBusy` 或 active action。
- 移动、选顾客、分配工位和收取金币不应持续锁死玩家。
- 普通移动版的洗头/剪发交互主要是一次持续计时；自动吹发可以后台完成。

### 8.2 后台计时器

后台任务由 `BackgroundTaskModel` 和 `Stage3WashServiceAdapter` 的服务会话保存，`SalonGameModel.Tick` 每帧推进：

- 自动吹发有安全停止/理想时间窗口。
- 染发/烫发等处理阶段可以在模型 Tick 中继续推进。
- 泡沫、等待、服务延迟和耐心也会继续变化。

### 8.3 玩家离开后的结果

- 自动吹发达到安全停止时会停止并等待玩家完成/确认。
- 玩家回来太晚，可能得到普通/较低质量结果。
- 泡沫未处理可能生成 `FunnyDisasterEvent` 和 `RecoveryRequirement`，并扣满意度。
- 染发没有按时清理会进入失败/未处理结果。
- 等待顾客耐心持续下降，可能愤怒离场。
- 营业结束后仍未完成会被 closing grace 或强制收尾处理。

### 8.4 是否支持并行

逻辑模型支持多个顾客同时存在：顾客列表、等待队列、工位占用、后台任务和耐心都在同一 Tick 中推进；测试也覆盖了 concurrent flow。

但它不是多人并行，也不是多个前台操作同时由玩家控制：当前 UI 只有一个本地玩家 `LocalPlayerId = 1`，玩家一次只能操作一个焦点/前台动作。准确结论是“多顾客后台并行已实现，玩家前台多任务并行未实现”。

## 9. 当前扩建和成长系统

### 9.1 金币和购买

`SalonPaymentModel` 构造时默认余额是 `8640`，完成付款收取动画后增加余额。`SalonGameModel.TrySpend`/`PurchaseAutoBlowStand` 负责支出。移动版在安全边界通过 `SalonProgressSave` 保存余额。

### 9.2 现有升级

当前只有一个实际产品：`AUTO_BLOW_STAND`，价格默认 `1200`。购买条件是首日完成/商店解锁，购买后通过 `AutoBlowPurchased` bool 改变自动吹发行为。没有设备等级、等级经验、重复购买或升级树。

`ManagementInvestmentType` 虽然列出 Equipment、Expansion、Upgrade、Marketing，但实际可用购买逻辑目前只落到了 Equipment 的自动吹发台。

### 9.3 区域扩建和购买点

当前没有：

- 区域扩建数据或解锁区域。
- 场景内购买点。
- `UpgradePad`/`PurchasePad` 类似的通用交互对象。
- 通用购买目录。

### 9.4 如果以后改成场景内点位购买，需要影响的系统

不能只改按钮。至少要一起改：

1. 购买点/交互对象和玩家近距离交互。
2. 商品/升级领域模型、价格、前置条件和幂等购买规则。
3. `SalonGameModel` 或独立商店服务的持久化购买状态。
4. `SalonProgressSave` schema 版本和迁移。
5. 场景/工位/资产 Manifest 的区域、占地、碰撞、anchor、sorting。
6. `SalonMobileNavigation`/`SalonPlayerRoute` 的障碍和可走区域。
7. HUD、管理面板、购买反馈和 Day 边界规则。
8. 业务测试、存档测试、WebGL 真实交互检查。

## 10. 当前事件系统

### 10.1 是否有通用事件架构

当前未发现以下通用类或可配置事件管线：

`Event`、`GameEvent`、`SpecialEvent`、`CustomerEvent`、通用 `Modifier`。

存在的近似物：

- `CustomerTrafficDirector`：客流导演，不是通用事件基类。
- `RushConfig` 和 `TrafficDecision.IsRushActive`：DayConfig 中的客流高峰参数，不是独立事件对象。
- `FunnyDisasterEvent`、`RecoveryRequirement`：服务事故和补救数据，范围只覆盖洗发/泡沫等服务错误。
- `AccidentSeverity`、顾客情绪、满意度扣分：结果数据，不是事件调度系统。

### 10.2 Rush 如何触发和施加效果

`CustomerTrafficDirector.Evaluate` 根据 Day 经过比例、客流阶段、等待超载、工位饱和、愤怒顾客和声誉倍率，结合 `DayConfig.Rush` 计算 `TrafficDecision`。Rush 的效果是客流强度/间隔调整，不是一个可以与其他事件并存、开始、结束、叠加的独立实例。

### 10.3 配置化和并存能力

- 参数可以来自 `DayConfig`/Inspector，但订单池、移动日配置和 Rush 结构仍主要是代码对象。
- 没有通用事件配置 JSON/ScriptableObject/DataTable。
- 没有任意多个事件的生命周期、优先级、叠加和冲突规则。
- Day 逻辑直接调用 Director 和 Rush 计算，因此“事件效果”部分写死在 Day/Director 语义里。

## 11. Day 系统是否写死

存在直接绑定 Day 的代码，但没有大量 `if Day == 5/8/15` 这种长表。目前最明确的绑定在 `SalonMobileDayConfig.cs`：

- `TargetOrdersForDay`：第 1 天目标 3，第 2 天目标 4，3 天及以上目标 5。
- `PickOrderForSpawn`：第 1 天前两个订单固定为 O001/O002，后续使用第 1 天池；第 2 天使用第 2 天池；3 天以上使用后期池。
- `BusinessDayController.PrepareDay` 每天调用移动配置应用。
- UI 日历/顶部 HUD 显示 Day，但这是显示绑定。
- `SalonProgressSave.MaxDayNumber = 100000`，不是内容解锁表。

没有发现 Day 5、8、15 的玩法解锁、设备解锁或事件分支。自动吹发台的解锁绑定的是 `FirstDayCompleteForShop` bool，而不是具体数字 Day。

因此改成“少数关键 Day 固定，其余营业动态生成”是可行方向，但需要先把移动订单/目标配置从 `SalonMobileDayConfig` 的条件分支迁到数据或规则层，并把事件/解锁从 `BusinessDayController` 分离。

## 12. 当前数据驱动程度

### 12.1 代码写死的内容

- 服务类型、工具枚举、工位类型。
- `SalonOrderCatalog` 的 O001–O005 订单。
- 订单默认时长、奖励、耐心和满意度阈值。
- `SalonGameModel` 构造时创建的工位 ID、类型、服务点、工具列表。
- 顾客路径点、等待点和很多运行时世界坐标。
- 第 1/2/3+ 天的订单池和目标订单数。
- 自动吹发台唯一商品和价格。
- UI 文本、部分图标/颜色/布局。
- 染发/烫发是代码中的外部 milestone 逻辑，不是外部数据表。
- 没有顾客性格配置、特殊顾客表或奖励配置表。

### 12.2 Inspector 配置

`SalonDemo` 暴露了若干可在 Inspector 设置的序列化配置：

- `HaircutSettings`
- `RewardSettings`
- `PatienceSettings`
- `FlowSettings`
- `ServiceSettings`
- `DaySettings`
- `SatisfactionSettings`

但是普通移动模式启动时会应用 `SalonMobileDayConfig`，因此 Inspector 配置不一定是普通 Demo 最终使用的值。

### 12.3 JSON / 资源配置

- `Assets/Resources/AssetPipeline/asset-manifest.json`：资产数据驱动，包含稳定 ID、状态、尺寸、方向、占地、碰撞、阴影、锚点和 sorting。
- `Assets/Resources/CutStations/cut-stations.json`：剪发工位数据。
- `Assets/Resources/AssetPipeline/reference-visual-baseline.json`：视觉参考基线。
- `SalonProgressData`：存档使用 `JsonUtility` 写入 PlayerPrefs/备份键，但这是进度数据，不是玩法配置。

### 12.4 未发现的配置类型

目前未发现用于核心玩法的 ScriptableObject、DataTable 或独立 JSON 配置：服务、订单、顾客、性格、Day、事件和奖励仍不是完整的外部内容数据。

## 13. 当前耦合和技术债

### 13.1 耦合较重的地方

- `SalonDemo.cs` 约 3500 行级别，混合输入、动态场景、UI、摄像机、顾客视图、服务编排、收款、Day 回调和浏览器验收分支。
- `SalonGameModel.cs` 同时保存顾客、工位、服务、耐心、事故、订单推进、支付和设备购买。
- `CustomerModel` 与 `CustomerServiceState`/`ServiceProgress` 双重保存服务事实，靠适配器同步。
- 移动、桌面、Browser Smoke、Debug/Acceptance 是多条行为分支，操作语义不完全相同。
- 工位兼容性、服务点坐标、等待位置和顾客路线存在代码硬编码。
- `SalonTool` 与 `ServiceTool` 重叠，工具/动作/服务的映射不在单一表中。
- `CustomerState`、`WorkstationState`、`AttentionState`、服务阶段、active action 同时表达流程阶段。

### 13.2 明显的 Demo 快速实现

- 运行时大量 `new GameObject`、固定坐标和固定 UI 布局。
- 订单和 Day 进度用 if/else 和字典写死。
- 自动吹发台用一个 bool 代表完整设备购买状态。
- 支付掉落替代了真正收银阶段。
- 染发/烫发部分依赖外部 milestone/开发入口，不在普通订单内容中闭环。
- 真实资产管线已开始 Manifest 化，但主玩法世界仍有旧的人工坐标和运行时组装。

### 13.3 继续加内容前最好整理什么

优先整理：

1. 明确普通移动路径是否就是产品主路径，以及洗/剪/吹/染/烫的统一服务契约。
2. 确定顾客状态唯一来源：至少把模型状态、服务进度、工位阶段的边界写成可测试协议。
3. 把订单、服务需求、Day 内容迁移到稳定数据边界。
4. 给工位能力、工位占地/锚点和服务兼容性建立统一注册表。
5. 将 `SalonDemo` 的输入/UI/视图/业务编排拆开，先不重写已经稳定的纯 Resolver/Applier 规则。

### 13.4 现在不要大规模重构的地方

- 不要在没有端到端基线测试前重写 `ActionResolver`/`ActionResultApplier`。
- 不要为了加入新服务先推翻已有剪发和服务领域规则；它们已有较多 EditMode 覆盖。
- 不要同时重做 `SalonDemo`、工位 Manifest 和移动导航；这会让当前可玩的验收路径失去对照。
- 不要把 AssetTestLab 的调试 UI 合入正式 HUD。
- 不要把当前自动生成的 Build/Library/Logs 当成源码架构的一部分提交。

## 14. 当前未完成事项

以下是基于代码真实缺口归类，不是产品愿望清单。

### P0：不处理会影响基础营业循环或交接判断

- 明确并统一“移动简化路径”和“桌面详细服务路径”的产品主路径；现在同一服务在两条路径的深度不同。
- 如果产品要求真正的“收银阶段”，需要增加独立 Cashier/收银点规则；当前只有工位金币掉落和附近收取。
- 修复/定位 Chromium 调试模式核心流程失败，以及候选场景缺少 `CANDIDATE_ALIGNMENT_READY` 运行标记；当前完整验收不能全通过。
- 建立顾客状态、工位状态、服务进度、适配器投影的转换协议和端到端测试，避免继续在多份状态上加分支。
- 确认 Day 结束强制收尾是否要支付、满意度、离场和统计都走统一结算路径。

### P1：核心玩法需要

- 将订单、服务需求、服务时长、奖励和 Day 内容做成真正可配置数据。
- 将工位能力、工具能力、服务兼容性统一到一套注册/数据结构。
- 让移动版洗头使用与服务领域一致的步骤，或明确把移动版定义为简化规则并补齐可见反馈。
- 完善染发、烫发从订单生成到普通玩家路径的闭环；当前代码存在但订单池不触发。
- 建立通用设备升级和购买模型，取代单一 `AutoBlowPurchased` bool。
- 建立扩建区域、占地、碰撞、导航和存档 schema 的成套模型。
- 添加每个顾客的性格/特殊顾客数据，而不是继续用全局经验配置代替。
- 建立通用事件/Modifier 生命周期，支持 Rush、事故、特殊顾客事件并存和配置化。
- 将投诉、扫地、毛巾库存/洗衣等纳入明确的系统边界；在产品确认前不要直接扩展。
- 继续拆解 `SalonDemo`，但以行为锁定测试保护当前营业循环。

### P2：可以以后增加

- 更丰富的特殊事件、故事内容、奖励和营销内容。
- 更完整的声音、特效、角色动画和视觉表现。
- 更多工位、更多顾客模板和更多订单。
- 更细的经营统计、报表和内容编辑工具。
- 其他设计文档里提到但当前范围未批准的多人、商业化等系统不应自动纳入下一步。

## 15. 最近开发历史和当前未提交修改

### 15.1 Git 历史

当前仓库可读取的 Git 历史只有 1 个 commit：

```text
a71e727 | 2026-08-30 | chore: checkpoint hair salon pipeline baseline
```

因此无法按要求总结最近 10–20 个重要 commit；仓库当前没有足够的提交历史可供分析。不能把工作区 diff 假装成已提交历史。

### 15.2 当前工作区

开始本次文档任务时，工作区已经在 `main` 上存在大量未提交修改；本次审计没有修改这些业务文件。主要变更范围包括：

- 资产管线：`.agents/skills/salon-asset-ingest`、`.agents/skills/salon-visual-qa`、`tools/asset-pipeline.mjs`、`tests/asset-pipeline.test.mjs`。
- 构建和验收：`tools/check-project.sh`、`tools/browser-check.py`、`Assets/Editor/BuildScript.cs`。
- 资产 Manifest 和视觉实验室：`asset-manifest.json`、`AssetTestLab.cs`、场景、资产验证/参考视觉/洗发区集成脚本。
- 主玩法模型：`SalonDemo.cs`、`SalonGameModel.cs`、`BusinessDaySystem.cs`、`SalonPaymentModel.cs`、`ShopSatisfactionModel.cs`、服务适配器。
- 移动路径：`SalonDemo.Mobile.cs`、`SalonMobileControls.cs`、`SalonMobileDayConfig.cs`、`SalonMobileNavigation.cs`、`SalonProgressSave.cs`、队列/玩家路线脚本。
- 自动测试：顾客生命周期、服务链、并发、移动流程、资产、存档、满意度和视觉相关测试。
- 未跟踪生成/工作文件：`Builds/`、`Library/`、`Logs/`、`Artifacts/`、`output/`、`docs/`、`prototypes/` 等，以及新资产来源/预览文件。

从文件内容可以确认这些修改覆盖资产接入、移动 Demo、存档、视觉验收和测试扩充；但因为它们尚未形成多个 commit，无法仅凭 Git 历史精确还原每个修改的产品意图。

## 16. 当前项目真实状态总结

### 玩家现在可以完整体验什么

在普通移动路径中，玩家可以开店、移动、接待顾客、把顾客分配到工位、完成当前订单中的洗/剪/吹主要流程、等待顾客结束、在工位附近收取付款、经历耐心和满意度结果、结束营业、查看结算并进入下一天管理界面。自动吹发购买和持久化余额/天数也已有可运行路径。

### 最大的 3–5 个技术问题

1. `SalonDemo` 过大，输入、业务、UI、视图和构建验收混在一起。
2. 移动简化路径、桌面详细路径和服务领域路径存在语义分叉。
3. 顾客状态和服务进度在 `CustomerModel`、适配器、`CustomerServiceState`、工位状态和视图之间重复保存。
4. 订单、Day、工位、服务和升级的数据驱动程度不足，扩内容会继续复制 if/else 和硬编码。
5. 当前浏览器完整验收未通过：调试 Demo 核心流程和候选资产对齐标记有问题。

### 继续开发最合理的下一步

先不加新玩法。先写一份“当前主路径契约”和端到端基线测试，明确移动路径是否为正式产品路径；然后以不改变当前可玩结果为前提，收敛顾客/工位/服务进度的状态所有权，再把订单、Day、工位能力迁移到数据边界。最后修复 Chromium 调试/候选场景验收失败，再继续增加内容。

### 设计文档里有、代码里实际上没有的内容

当前代码中没有可用实现或完整闭环的典型内容：

- 独立 Cashier/收银阶段。
- 店铺区域扩建和场景购买点。
- 通用设备等级/升级树。
- 扫地、毛巾库存和洗衣循环。
- 投诉系统。
- 特殊顾客和通用特殊事件/Modifier 系统。
- 每个顾客的性格系统。
- 以配置文件驱动的完整服务/订单/Day/奖励内容。
- 通用 `CustomerStateMachine`、`CustomerSpawner`、`Station`、`Tool` 类架构。

如果旧文档或规划文件提到上述内容，应先以本文件列出的代码事实为准，再决定是否重新设计。

## 17. 2026-09-23 续接审计（当前有效）

### 已完成并由当前构建复核

- 当前分支为 `phase-0-2-gameplay-gate`，工作区保留一批未提交的 9 月 20–22 日玩法修复；不要用旧交接文档覆盖这些修改。
- 正式入口仍是 `unity-hair-salon/Assets/Scenes/HairSalonDemo.unity`。当前 WebGL 正式 Demo 使用完整三维沙龙场景和同一套移动玩法；2D 只在显式诊断参数下保留。
- 订单完成时收入立即进入余额，不再生成金币堆或要求玩家额外收取。已完成订单与未完成订单共同参与每日接单上限，失败/流失订单不计目标。
- 接待后的护送宽限、在座顾客优先收尾、兼容工位占用门禁、最后顾客离店后再结算均已接入。
- 2026-09-23 当前工作区门禁（后续专项回归前的历史快照）：Node 资产管线 `17/17`；UI 字体检查 `367` 个必需字形、`629` 个打包字形；Unity EditMode XML `630/630`；Demo、Asset Lab、Candidate、Reference 四个 WebGL 构建均为 `Build Finished, Result: Success`。当前最新门禁见 §20，为 637/637。
- 当前构建真实 Chromium 触控整局证据：`unity-hair-salon/Builds/MobileEvidenceCurrent/report.json`。结果为首日 `3/3` 单、余额 `540`、满意度 `69`、后台吹发期间完成另一项服务、自动结算、手动金币收取 `0` 次、刷新恢复通过、浏览器错误 `0`。
- 当前构建失败/重试证据：`unity-hair-salon/Builds/MobileEvidenceFailureCurrent/report.json`。结果为 `0/3` 失败局、余额 `0`、点击重试回到第 1 天、满意度恢复到 `90`、浏览器错误 `0`。
- 当前正式 Demo、资产实验室、候选场景和参考场景的 844×390 浏览器检查记录在 `unity-hair-salon/Builds/PipelineEvidence/browser-check-all.json`；报告中的 `coreFlowPassed`、候选服务完成和资源错误均为通过/空。

### 接下来怎么接

1. 先使用 `http://127.0.0.1:8910/WebGLDemo/` 做产品试玩，重点记录仍然能稳定复现的操作阻塞；没有新复现材料时不要猜测修复或扩展玩法。
2. 下一轮若要改玩法，只认当前可玩的移动主路径，按 `$salon-bug-fix` 先复现、写最小回归、最小修复，再跑受影响流程和相邻流程。
3. 若产品负责人转而验收画面，使用 `$salon-visual-qa` 检查正式 `HairSalonDemo`；Asset Lab/Reference 只作资产技术检查，洗发台仍是 `NEEDS-REVIEW`，不能自行写回视觉基准。
4. 若没有新的产品问题，优先把主路径契约和现有状态所有权整理成独立文档/测试；不要在本轮加入新页面、收银阶段、多人、货币、升级或未经批准的服务。

### 当前限制

- 真实触控自动化证明了流程和边界，不等同于产品负责人对节奏和趣味的最终验收；本次满意度为 `69`，说明仍有操作时机损失。
- 自动化只针对 WebGL 横屏 `844×390`，本轮没有重新生成 macOS Standalone 包。
- 工作区尚未形成新的提交；提交前应先审阅全部未提交业务修改，确认没有并行任务正在编辑 `SalonDemo.cs` 或正式场景。

## 18. 2026-09-23 顾客接待/离场问题已修复（当前交接点）

产品负责人反馈的“第二个顾客无法正常接待”和“第一个顾客离开时瞬间消失”已经完成真实复现和修复，后续不要按旧的“先试玩定位”描述重复寻找这两个已知根因。

### 修复内容

1. `SalonDemo.Mobile.cs` 在移动交互开始前清理已离场的 `_mobileGuidedCustomer`；等待中的顾客可作为接待目标；`Greet` 与 `Guide` 分开执行，避免残留护送状态遮挡第二位顾客。
2. `SalonDemo.cs` 不再用剪发工位碰撞回滚 `Leaving` 顾客；结果状态遇到可见离场旅程时继续更新顾客视图，直到顾客真正 `Exited` 后才清理和显示结算面板。
3. `SalonCustomerPath.cs` 使用低位安全通道和左侧出口路线，绕开正式剪发工位及出口植物；`SalonGameModel.LeavingSeconds` 为 4.1 秒，足以让右墙工位顾客完成路线。

### 验证结果

- 专项回归：`ReportedGameplayRegressionTests` 3/3，`CustomerExitRouteRegressionTests` 2/2。
- 最终整体验收：Node 17/17，UI 字体 367 个必需字形/629 个打包字形，Unity EditMode 635/635，Demo/Asset Lab/Candidate/Reference 四个 WebGL 构建成功，844×390 Chromium 检查通过且无错误。
- 正常整局报告：`unity-hair-salon/Builds/MobileEvidenceReportedFinal3/report.json`。首日 3/3 单、余额 540、满意度 69、无手动金币拾取；第一位顾客最大屏幕位移 226.08px 并到达出口，第一位仍在 `Leaving` 时第二位成功执行 `Greet`。
- 离场截图：`unity-hair-salon/Builds/MobileEvidenceReportedFinal3/14-first-customer-leaving.png`。
- 失败重试报告：`unity-hair-salon/Builds/MobileEvidenceReportedFailureFinal2/report.json`。

### 接下来如何体验

使用 `http://127.0.0.1:8910/WebGLDemo/`，开店后接待第一位顾客并完成服务；第一位进入离场后立刻靠近第二位等待顾客并点击接待。应看到第一位沿低位通道走到左侧出口，同时第二位进入护送状态。等最后一位真正走出店外后才会出现日结面板。

本轮没有改正式场景布局、角色或 UI 方向，也没有加入新玩法。当前证据针对 844×390 横屏 WebGL；工作区仍有未提交修改，提交前需按仓库规则复核全部并行改动。

## 19. 2026-09-23 第二位顾客接待后指引断档已修复（当前交接点）

上一版修复了第二位的 `Greet` 命中，但真实试玩仍会在接待成功后看起来“没有接上”：因为玩家尚未到洗发工位，目标搜索把引导顾客丢掉，按钮退回“靠近顾客”。这不是模型状态丢失，而是移动目标呈现缺少中间态。

当前行为：

1. 靠近第二位等待顾客，按钮显示“接待 2 号”。
2. 点击后第二位进入 `guided=1`；即使玩家还在队列区，按钮也持续显示“前往洗发工位”，目标仍是第二位，底部提示显示“已接待 2 号 · 前往空闲洗发工位安排”。
3. 玩家到达空闲洗发工位附近，按钮切换为“安排洗发”。点击后第二位进入 `MovingToStation`，再正常走入工位。

专项测试：`SecondCustomerReceptionRegressionTests` 1/1，覆盖真实 O002 洗发→吹发订单的 `Greet → Assign → MovingToStation`；`ReportedGameplayRegressionTests` 3/3；`CustomerExitRouteRegressionTests` 2/2。最终 Unity 全量 EditMode 为 636/636，Node 17/17，四个 WebGL 构建和 844×390 Chromium 场景检查通过。

真实触控证据在 `unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptance/`：

- `report.json`：第一位仍为 `Leaving` 时完成第二位接待流程；`guidedTarget.action=前往洗发工位`，到站后 `assignBeforeTouch.action=安排洗发`，点击后第二位 `state=MovingToStation`、`station=0`。
- `15-second-customer-greet-ready.png`：接待第二位前。
- `16-second-customer-guided-to-wash.png`：接待成功后，明确显示“前往洗发工位”。
- `17-second-customer-wash-assign-ready.png`：到洗发工位后显示“安排洗发”。
- `18-second-customer-assigned-to-wash.png`：第二位已分配到洗发工位。

试玩入口仍为 `http://127.0.0.1:8910/WebGLDemo/`。这次不要在接待后再次点击队列顾客；按右侧按钮提示走到洗发工位，再点击“安排洗发”。

## 20. 2026-09-23 错误工位自由度已恢复（当前交接点）

本次用户澄清了上一轮验收的关键误解：第二位顾客 O002 是洗发顾客，玩家把她带到剪发区属于已经批准的“允许犯错”路径。上一版移动入口只搜索兼容工位，导致这条路径被入口层拦截，等于把已决定的规则重新做回去了。

当前接续行为：

1. 正常接待 O002 后，远离工位时仍显示“前往洗发工位”，到洗发区显示“安排洗发”。
2. 玩家也可以走到任意空闲剪发工位；此时同一个引导目标显示“安排剪发工位”，点击后真实进入 `MovingToStation`，而不是被 UI 禁止。
3. 模型记录一次错误工位、`Confused` 反应和既定满意度损失。顾客到错工位后，按钮变为“转移顾客”；玩家可以再到空闲洗发工位点击“安排洗发”完成纠正。
4. 错误次数和已发生的满意度损失不会因纠正而撤销；没有空闲工位时仍等待空位，这是物理占用限制。

实现范围只涉及 `Assets/Scripts/SalonDemo.Mobile.cs` 的目标选择、错误安排提示和移动证据字段，以及 `Assets/Tests/SecondCustomerReceptionRegressionTests.cs` 的回归覆盖；没有修改正式场景布局、顾客订单、角色或 UI 视觉方向。`SalonGameModel.Assign` 原本就承担错误工位后果，本轮没有另造一套规则。

### 当前证据

- 正确接待路径：`unity-hair-salon/Builds/MobileEvidenceSecondCustomerAcceptanceFinal2/report.json`，O002 在第一位离场期间完成 `Greet → 前往洗发工位 → 安排洗发`，进入洗发工位。
- 错误再纠正路径：`unity-hair-salon/Builds/MobileEvidenceWrongStationFinal2/report.json`，真实触控完成 `接待 2 号 → 安排剪发工位 → 转移顾客 → 安排洗发`，`WrongStationCount=1`、`Confused`、满意度 `70→67`。
- 全量门禁：`Builds/PipelineEditMode.xml` 为 638/638；Node 17/17、UI 字体 367/629、四个 WebGL 构建和 844×390 Chromium 检查通过。
- 专项 XML：`Builds/MobileAdmissionRegressionFinal.xml` 14/14、`Builds/SecondCustomerReceptionRegressionFinal.xml` 2/2、`Builds/ReportedGameplayRegressionFinal.xml` 3/3、`Builds/CustomerExitRouteRegressionFinal.xml` 2/2。

历史审计中把移动入口限制为兼容工位的描述只代表当时缺口；当前规则以 `Docs/ACTION_RULE_MATRIX.md` 的“物理上不可能才可禁止、流程上不正确允许执行”以及后续模型测试为准。下一轮继续按 `$salon-bug-fix` 先用真实触控复现，再做最小修复；不要重新收紧这条错误路径，也不要借此扩展未批准玩法。

## 21. 2026-09-23 一局时长复核（当前有效）

- 参考《胡闹厨房》普通关卡约 3–4 分钟的可操作倒计时，正式移动配置现在为 `BusinessDuration = 180s`、`ClosingGraceDuration = 15s`；收尾是独立的离场/结算缓冲，开始营业前的 `StartingDuration = 1.25s` 不计入营业倒计时。
- 达到每日目标后停止新增接待；顾客全部处理并离场时可以在 180 秒前进入结果页。未提前结束时，营业倒计时到零后最多再经历 15 秒收尾；暂停冻结倒计时。
- 原有无头模拟只证明 120 秒内会出现目标并发压力，模拟玩家可以瞬移；本次按竞品范围将活动段延长到 180 秒，未改客流、目标订单或服务时长。
- “玩 3 分钟”现在落实为约 3 分钟可操作营业段，另加最多 15 秒收尾；后续仍要记录完整首局从开始营业到 Result 的实际时长、达标/失败原因、重试意愿和是否愿意开始下一局，不能把自动化整局报告写成真人时长结论。
- 调整后真实触控复核：`Builds/MobileEvidenceSecondCustomerAcceptanceOvercooked180/report.json` 的 O002 正确接待路径通过；`Builds/MobileEvidenceWrongStationOvercooked180/report.json` 的错误剪发工位→洗发纠正路径通过，浏览器错误均为空。
