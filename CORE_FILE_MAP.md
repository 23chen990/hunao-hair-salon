# 核心文件地图

> 以当前工作区代码为准。这里只列接手时最重要的代码文件，按“入口 → 主模型 → 服务 → Day/经济 → 移动/UI → 数据/测试”组织。

| 文件路径 | 负责什么 | 和哪些系统有关 |
|---|---|---|
| `unity-hair-salon/Assets/Scripts/Bootstrap.cs` | 没有入口对象时创建 `SalonDemo` | 启动、场景 |
| `unity-hair-salon/Assets/Scripts/SalonDemo.cs` | 主 MonoBehaviour；创建世界/HUD，处理输入、视图、顾客流程、收款和模式分支 | 几乎所有运行时系统 |
| `unity-hair-salon/Assets/Scripts/SalonDemo.Mobile.cs` | 移动版控制、近距离交互、移动服务、移动结算和存档 | 玩家、顾客、Day、UI、存档 |
| `unity-hair-salon/Assets/Scripts/SalonGameModel.cs` | 顾客/工位/服务/耐心/事故/订单推进/购买的主业务模型 | 顾客、工位、服务、金币 |
| `unity-hair-salon/Assets/Scripts/BusinessDaySystem.cs` | Day 状态、营业时间、结算统计、客流 Director、声誉和 Rush 参数 | Day、客流、结算 |
| `unity-hair-salon/Assets/Scripts/SalonMobileDayConfig.cs` | 移动版营业时长、目标订单和按 Day 分支的订单池 | Day、订单、客流 |
| `unity-hair-salon/Assets/Scripts/SalonServiceChain.cs` | 服务枚举、服务配置、订单目录、事故和设备产品 | 服务、订单、升级 |
| `unity-hair-salon/Assets/Scripts/SalonPaymentModel.cs` | 余额、订单奖励、小费、PaymentDrop 和收款状态 | 收银、金币、升级 |
| `unity-hair-salon/Assets/Scripts/SalonProgressSave.cs` | PlayerPrefs/JsonUtility 的跨天存档和 schema 校验 | 存档、金币、Day、设备 |
| `unity-hair-salon/Assets/Scripts/ShopSatisfactionModel.cs` | 100 分制全店满意度的日内结算 | 满意度、Day、UI |
| `unity-hair-salon/Assets/Scripts/SalonCustomerPath.cs` | 顾客进入、等待、工位、离店的固定路线和等待位 | 顾客移动、工位、视图 |
| `unity-hair-salon/Assets/Scripts/SalonMobileNavigation.cs` | 移动版玩家导航、障碍检测和移动碰撞 | 玩家移动、资产碰撞 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/InteractionContext.cs` | 当前焦点、工具、动作 token、上下文版本 | 输入、服务合法性 |
| `unity-hair-salon/Assets/Scripts/PlayerContext.cs` | 玩家焦点和选中动作；当前主要是本地玩家 | 输入、交互、服务 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/CustomerPhysicalState.cs` | 顾客头发/洗发物理事实和物理版本 | 洗头、吹发、Resolver |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/ServiceProgress.cs` | milestone、服务进度、订单完成条件 | 订单、服务、出口条件 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/Actions.cs` | `ActionRequest`、`ActionResult` 以及动作结果结构 | 服务规则、事故、指标 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Rules/ActionResolver.cs` | 纯函数式服务动作规则，判断合法/错误/额外和结果 | 服务、工具、物理状态 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/ActionResultApplier.cs` | 校验 token/版本并原子应用动作结果 | 服务状态、进度、事故 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/ServiceExecution.cs` | 通用持续服务执行控制器和时长 | 洗头、吹发、服务计时 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Interpretation/ServiceActionClassifier.cs` | 服务动作分类、正常/额外/错误和事故候选 | 服务、错误处理 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Adapters/Stage3WashServiceAdapter.cs` | 将纯服务聚合桥接到 `CustomerModel`，保存会话并投影结果 | 洗头、订单、进度、顾客 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/MetricsAndSettlement.cs` | 服务指标、质量和结算相关结构 | 满意度、结算 |
| `unity-hair-salon/Assets/Scripts/ServiceArchitecture/Core/ServiceTypes.cs` | 服务工具枚举和服务侧类型 | 工位、工具、服务 |
| `unity-hair-salon/Assets/Scripts/CutStation/CutStationData.cs` | 剪发工位的 JSON/Manifest 数据模型 | 剪发工位、资产管线 |
| `unity-hair-salon/Assets/Scripts/CutStation/CutStationFactory.cs` | 根据剪发工位数据创建运行对象 | 工位、场景、资产 |
| `unity-hair-salon/Assets/Scripts/AssetPipeline/AssetManifest.cs` | Manifest 资产结构和读取模型 | 资产、家具、场景 |
| `unity-hair-salon/Assets/Scripts/AssetPipeline/AssetManifestValidator.cs` | Manifest/资源字段校验 | 资产接入、构建 |
| `unity-hair-salon/Assets/Scripts/AssetPipeline/AssetTestLab.cs` | 独立资产测试场景运行逻辑 | 资产实验室、视觉 QA |
| `unity-hair-salon/Assets/Scripts/AssetPipeline/WashCraftIntegration.cs` | 将已确认洗发区资产接入正式 Demo 的视觉层 | 资产、洗头工位、视觉 |
| `unity-hair-salon/Assets/Scripts/CoinPileView.cs` | 工位付款掉落显示和收取动画 | 金币、收银、UI |
| `unity-hair-salon/Assets/Scripts/SalonMobileControls.cs` | 移动摇杆/按钮输入 | 玩家移动、移动交互 |
| `unity-hair-salon/Assets/Editor/BuildScript.cs` | Unity 构建、场景创建和 Pipeline 校验入口 | 构建、WebGL、验收 |
| `tools/check-project.sh` | 一键执行 Node、Unity、构建和浏览器检查 | 自动化验收 |
| `tools/browser-check.py` | Playwright Chromium 的运行标记、截图和视觉验收 | WebGL、Demo、AssetLab、Candidate |
| `tools/asset-pipeline.mjs` | 资产 Manifest 导入/校验辅助 | 资产管线 |
| `unity-hair-salon/Assets/Scenes/HairSalonDemo.unity` | 正式 Demo 入口场景 | 启动、Build |
| `unity-hair-salon/Assets/Scenes/AssetTestLab.unity` | 独立资产测试入口场景 | 资产 QA、Build |
| `unity-hair-salon/Assets/Resources/AssetPipeline/asset-manifest.json` | 资产稳定 ID、状态、方向、占地、碰撞、阴影、锚点和 sorting | 资产、场景、导航 |
| `unity-hair-salon/Assets/Resources/CutStations/cut-stations.json` | 剪发工位的外部数据 | 工位、资产、碰撞 |
