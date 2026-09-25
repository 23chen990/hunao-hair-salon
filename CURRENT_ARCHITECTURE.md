# 当前架构简图

> 这是当前代码结构的解释，不是目标架构设计。图中保留了现在存在的重复状态和分叉路径。

## 1. 启动与主循环

```text
HairSalonDemo.unity
      │
      ▼
Bootstrap（必要时创建 SalonDemo）
      │
      ▼
SalonDemo.Start
      ├── RestoreMobileGame / PrepareDay
      ├── new SalonGameModel
      ├── new BusinessDayController
      ├── new CustomerTrafficDirector
      ├── new ShopSatisfactionModel
      ├── BuildWorld（运行时地图/工位/玩家/顾客视图）
      └── BuildHud（运行时 uGUI）
      │
      ▼
SalonDemo.Update
      ├── BusinessDayController.Tick
      ├── SalonGameModel.Tick
      ├── MaintainCustomerFlow
      ├── UpdateMobilePlay 或桌面服务输入
      ├── UpdateCustomerViews
      └── Refresh HUD / 结算 / 验收标记
```

## 2. 顾客生命周期

```text
CustomerTrafficDirector
      │ 决定是否生成
      ▼
SalonDemo.SpawnRuntimeCustomer
      │ 选订单 ID
      ▼
SalonOrderCatalog.Get
      │ 得到 List<ServiceType>
      ▼
SalonGameModel.Spawn
      │
      ├── CustomerModel
      │     ├── Needs
      │     ├── Step
      │     ├── patience / satisfaction / emotion
      │     └── CustomerState
      ├── waiting queue
      └── Stage3WashServiceAdapter.Register
      │
      ▼
Entering → Waiting → MovingToStation → Serving
      │                                  │
      │                                  ├── 下一步：继续或转工位
      │                                  └── 完成：TryFinalizeCompletedOrder
      ▼
Finished → Leaving → Exited
      │
      ├── SalonPaymentModel.CreateOrderPayment
      ├── ShopSatisfactionModel.TrySettleCustomer
      └── DayStats.RecordCustomerSnapshot
```

## 3. 服务动作链

```text
输入/UI
  ├── 移动近距离按钮
  ├── 桌面工具栏
  └── 开发/验收入口
          │
          ▼
SalonGameModel 的服务门槛检查
          │
          ▼
Stage3WashServiceAdapter
  ├── InteractionContext / ActionToken
  ├── CustomerServiceState 快照
  │     ├── CustomerPhysicalState
  │     ├── OrderDefinition
  │     ├── ServiceProgress
  │     └── CustomerMetricsState
  ├── ActionResolver.Resolve(request, snapshots)
  │     └── ActionResult
  └── ActionResultApplier.Apply(result)
          │
          ▼
Adapter.Project
  ├── CustomerModel.Needs / Step
  ├── HairWet / ShampooApplied / TowelWrapped
  ├── satisfaction / patience / accident
  └── exit readiness
```

这里的关键风险是：`CustomerModel` 和 `CustomerServiceState` 都在保存服务事实；适配器是同步边界，不是单纯的无状态转换器。

## 4. Day、客流和结算

```text
BusinessDayController
  ├── PreOpen
  ├── Business ── Tick ── CustomerTrafficDirector.Evaluate
  │                          ├── phase
  │                          ├── rush
  │                          ├── station saturation
  │                          └── waiting/angry overload
  ├── ClosingGrace
  ├── Result
  └── ClosedManagement
          │
          ├── DayStats
          ├── ShopSatisfactionModel
          ├── ShopReputationModel（当前主开关关闭）
          ├── SalonPaymentModel.Balance
          └── SalonProgressSave
```

## 5. 当前真实状态机（简图）

```text
            ┌──────────────┐
            │   Entering   │
            └──────┬───────┘
                   ▼
            ┌──────────────┐
            │   Waiting    │◄──────────────┐
            └──────┬───────┘               │
          Assign  │                       │ 下一步需转移
                   ▼                       │
         ┌──────────────────┐              │
         │ MovingToStation  │──────────────┘
         └────────┬─────────┘
                  │ 到达
                  ▼
            ┌──────────────┐
            │   Serving    │
            └───┬──────┬───┘
    全部完成    │      │ 耐心归零/失败
                 ▼      ▼
          ┌──────────┐ ┌─────────┐
          │ Finished │ │ Leaving │
          └────┬─────┘ └────┬────┘
               │ 反馈       │ 离店
               ▼            ▼
          ┌─────────┐   ┌────────┐
          │ Leaving │   │ Exited │
          └─────────┘   └────────┘
```

代码中不存在 `Following`、`AtStation`、`WaitingNextService`、`Cashier` 这些独立顾客状态；它们目前是字段、工位状态、UI 和 PaymentDrop 的组合语义。

## 6. 资产与正式场景

```text
assets/inbox/
      │
      ▼
tools/asset-pipeline.mjs
      │ 校验稳定 ID、方向、pivot、footprint、collision、shadow、anchors、sorting
      ▼
Assets/Resources/AssetPipeline/asset-manifest.json
      │
      ├── AssetManifestLoader / Validator
      ├── AssetTestLab（独立实验室）
      └── WashCraftIntegration / CutStationFactory
              │
              ▼
        HairSalonDemo 运行时视觉
```

资产数据驱动正在形成，但主玩法的工位创建、服务点和顾客路线仍有不少 `SalonDemo`/`SalonGameModel` 代码坐标，因此不能把整个场景称为完全 Manifest 驱动。

