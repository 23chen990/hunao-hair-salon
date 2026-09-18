using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

/// <summary>
/// Phase 2「基础忙乱 Demo」的行为锁定测试。
/// 覆盖：顾客生命周期、多顾客并行、洗头后台、吹发后台、剪发主动占用、
/// 付款幂等、Day 边界不重复结算，以及能同时产生 4 个待处理顾客的忙乱场景。
///
/// 这些测试只依赖 SalonGameModel 与 ServiceArchitecture，不依赖任何 View，
/// 因此移动与桌面两条输入路径最终都必须满足同一组行为。
/// </summary>
public sealed class Phase2BusySalonFlowTests
{
    // ---------------------------------------------------------------- 生命周期

    [Test]
    public void CustomerLifecycleRunsFromEntryToExit()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = game.Spawn(3001, new List<ServiceType> { ServiceType.Cut });

        Assert.AreEqual(CustomerState.Entering, customer.State);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);

        Assert.IsTrue(game.Assign(customer, 1));
        Assert.AreEqual(CustomerState.MovingToStation, customer.State);
        ReachStation(game);
        Assert.AreEqual(CustomerState.Serving, customer.State);

        CompleteHaircut(game, customer);
        Assert.AreEqual(CustomerState.Finished, customer.State);

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);
        Assert.AreEqual(CustomerState.Leaving, customer.State);

        game.Tick(SalonGameModel.LeavingSeconds + .01f);
        Assert.IsFalse(game.Customers.Contains(customer),
            "An exited customer must be removed from the active set.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    // ------------------------------------------------------------ 洗头后台节奏

    [Test]
    public void WashKeepsRunningAfterTheActiveBeatAndNeedsThePlayerToReturn()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 3011,
            new[] { ServiceType.Wash, ServiceType.Cut }, 0);

        Assert.IsTrue(game.BeginWashFoamHold(customer),
            "The first wash beat must start from a wash station with a Wash need.");

        // 短主动阶段结束后，玩家已经可以自由离开。
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        Assert.AreEqual(ActiveServiceAction.None, customer.ActiveServiceAction);
        Assert.IsFalse(game.PlayerBusy, "The foam wait must not keep the player busy.");
        Assert.IsTrue(game.IsWashFoamWaitRunning(customer));
        Assert.IsFalse(game.IsWashFoamReadyToRinse(customer));

        // 玩家离开期间，泡沫等待继续推进。
        game.Tick(game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(customer));

        Assert.IsTrue(game.FinishWashRinse(customer));
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed,
            "Rinsing must advance the order to its next requested service.");
        Assert.IsFalse(game.IsWashFoamWaitRunning(customer));
    }

    [Test]
    public void WashCannotStartWhileThePlayerIsBusyWithAnotherCustomer()
    {
        SalonGameModel game = NewGame();
        CustomerModel cutting = SpawnServing(game, 3021, new[] { ServiceType.Cut }, 1);
        CustomerModel washing = SpawnServing(game, 3022, new[] { ServiceType.Wash }, 0);

        game.SelectCustomer(cutting);
        Assert.IsTrue(game.BeginHaircutAction(cutting, SalonTool.Scissors, new HaircutConfig()));
        Assert.IsTrue(game.PlayerBusy);

        Assert.IsFalse(game.BeginWashFoamHold(washing),
            "One player can only hold one foreground operation at a time.");
    }

    // ------------------------------------------------------------ 吹发后台节奏

    [Test]
    public void AutoBlowKeepsRunningWhileThePlayerWalksAway()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = PrepareDryCustomer(game, 3031);

        Assert.IsTrue(game.StartAutoBlow(customer));
        float before = customer.BackgroundTask.Elapsed;

        game.Tick(3f);
        Assert.Greater(customer.BackgroundTask.Elapsed, before,
            "The blow-dry background timer must advance without the player.");
        Assert.IsFalse(game.PlayerBusy,
            "A background blow-dry must not lock the player.");

        game.Tick(customer.BackgroundTask.IdealStart - customer.BackgroundTask.Elapsed + .1f);
        Assert.AreEqual(BlowResult.Good, game.FinishAutoBlow(customer));
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    // -------------------------------------------------------- 剪发主动占用压力

    [Test]
    public void HaircutHoldsThePlayerButTheRestOfTheSalonKeepsRunning()
    {
        SalonGameModel game = NewGame();
        CustomerModel cutting = SpawnServing(game, 3041, new[] { ServiceType.Cut }, 1);
        CustomerModel washing = SpawnServing(game, 3042, new[] { ServiceType.Wash, ServiceType.Cut }, 0);
        CustomerModel waiting = game.Spawn(3043, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.BeginWashFoamHold(washing));
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        float foamBefore = washing.BackgroundTask.Elapsed;
        float patienceBefore = waiting.Patience;

        game.SelectCustomer(cutting);
        Assert.IsTrue(game.BeginHaircutAction(cutting, SalonTool.Scissors, new HaircutConfig()));
        Assert.IsTrue(game.PlayerBusy, "A haircut is the sustained foreground task.");

        game.Tick(2f);

        Assert.Greater(washing.BackgroundTask.Elapsed, foamBefore,
            "The wash background timer keeps running while the player cuts hair.");
        Assert.Less(waiting.Patience, patienceBefore,
            "A waiting customer keeps losing patience while the player is busy.");
        Assert.AreEqual(CustomerState.Waiting, waiting.State);
        Assert.AreEqual(ActiveServiceAction.None, cutting.ActiveServiceAction);
        Assert.IsTrue(cutting.AttentionState == CustomerAttentionState.ActiveOperation);

        Assert.AreEqual(HaircutResult.Perfect, game.CompleteHaircutAction(
            cutting, game.HaircutHoldDurationFor(SalonTool.Scissors, new HaircutConfig()), false));
        Assert.IsTrue(game.EndActiveOperation(cutting));
        Assert.IsFalse(game.PlayerBusy);
    }

    // ------------------------------------------------------------ 多顾客并行压力

    [Test]
    public void FourCustomersCanDemandAttentionAtTheSameTime()
    {
        SalonGameModel game = NewGame();

        // 吹发顾客先用掉洗头工位再转到理发椅，把 0 号洗头床空出来给下一位。
        CustomerModel drying = PrepareDryCustomer(game, 3053, dryStation: 2);
        CustomerModel cutting = SpawnServing(game, 3051, new[] { ServiceType.Cut }, 1);
        CustomerModel washing = SpawnServing(game, 3052, new[] { ServiceType.Wash, ServiceType.Cut }, 0);
        CustomerModel waiting = game.Spawn(3054, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.BeginWashFoamHold(washing));
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        Assert.IsTrue(game.StartAutoBlow(drying));
        game.SelectCustomer(cutting);
        Assert.IsTrue(game.BeginHaircutAction(cutting, SalonTool.Scissors, new HaircutConfig()));

        // 这一刻正是本轮想要的优先级选择：
        //   顾客 A 正在剪发（玩家被占住）
        //   顾客 B 泡沫等待即将可冲洗
        //   顾客 C 吹发后台运行中
        //   顾客 D 在等候，耐心下降
        Assert.AreEqual(4, game.Customers.Count);
        Assert.IsTrue(game.PlayerBusy);
        Assert.IsTrue(game.IsWashFoamWaitRunning(washing));
        Assert.IsTrue(drying.AutoBlowRunning);
        Assert.AreEqual(CustomerState.Waiting, waiting.State);

        float foamBefore = washing.BackgroundTask.Elapsed;
        float blowBefore = drying.BackgroundTask.Elapsed;
        float patienceBefore = waiting.Patience;

        game.Tick(game.ServiceConfig.FoamOptimalStart + .01f);

        Assert.Greater(washing.BackgroundTask.Elapsed, foamBefore);
        Assert.Greater(drying.BackgroundTask.Elapsed, blowBefore);
        Assert.Less(waiting.Patience, patienceBefore);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(washing),
            "While the player is still cutting, the wash must become ready to rinse.");
    }

    // ------------------------------------------------------------------ 付款

    [Test]
    public void ACompletedOrderCreatesExactlyOnePaymentThatCreditsOnce()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 3061, new[] { ServiceType.Cut }, 1);
        CompleteHaircut(game, customer);

        Assert.AreEqual(1, game.Payments.Drops.Count,
            "One settled order must create exactly one payment drop.");
        PaymentDropModel drop = game.Payments.Drops[0];
        Assert.AreEqual(customer.Id, drop.CustomerId);

        var stats = new DayStats(1);
        int incomeBefore = stats.OrderIncome + stats.TipIncome;
        Assert.IsTrue(game.Payments.BeginCollection(drop.Id));
        Assert.IsTrue(game.Payments.CompleteCollection(drop.Id));
        stats.RecordPaymentCollected(drop);
        stats.RecordPaymentCollected(drop);

        int credited = stats.OrderIncome + stats.TipIncome - incomeBefore;
        Assert.AreEqual(drop.OrderIncomeComponent + drop.TipIncomeComponent, credited,
            "Collecting the same drop twice must credit it once.");
    }

    // ------------------------------------------------------------- Day 边界

    [Test]
    public void ForceCloseDoesNotDuplicatePaymentsOrDayStatistics()
    {
        SalonGameModel game = NewGame();
        CustomerModel finished = SpawnServing(game, 3071, new[] { ServiceType.Cut }, 1);
        CompleteHaircut(game, finished);
        CustomerModel halfDone = SpawnServing(game, 3072, new[] { ServiceType.Wash, ServiceType.Cut }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(halfDone));

        Assert.AreEqual(1, game.Payments.Drops.Count);

        var stats = new DayStats(1);
        game.ForceCloseRemainingCustomers(stats);

        Assert.AreEqual(0, game.Customers.Count);
        Assert.AreEqual(1, game.Payments.Drops.Count,
            "Force-closing must not create or duplicate a payment.");
        Assert.AreEqual(1, stats.IncompleteAtClose);

        game.ForceCloseRemainingCustomers(stats);
        Assert.AreEqual(1, stats.IncompleteAtClose,
            "A second force-close must not double-count the same customer.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void ResetForNextDayClearsFoamWaitState()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 3081,
            new[] { ServiceType.Wash, ServiceType.Cut }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        Assert.IsTrue(game.IsWashFoamWaitRunning(customer));

        game.ResetForNextDay();

        Assert.AreEqual(0, game.Customers.Count);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    // ------------------------------------------------------------------ 辅助

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 10,
            WaitingCapacity = 10,
            InitialCustomerCount = 0
        });
    }

    private static void ReachStation(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, IReadOnlyList<ServiceType> needs, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType>(needs));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, station));
        ReachStation(game);
        return customer;
    }

    private static void CompleteHaircut(SalonGameModel game, CustomerModel customer)
    {
        game.SelectCustomer(customer);
        var config = new HaircutConfig();
        Assert.IsTrue(game.BeginHaircutAction(customer, SalonTool.Scissors, config));
        Assert.AreEqual(HaircutResult.Perfect, game.CompleteHaircutAction(
            customer, game.HaircutHoldDurationFor(SalonTool.Scissors, config), false));
        Assert.IsTrue(game.EndActiveOperation(customer));
    }

    private static CustomerModel PrepareDryCustomer(SalonGameModel game, int id, int dryStation = 1)
    {
        CustomerModel customer = SpawnServing(game, id,
            new[] { ServiceType.Wash, ServiceType.Dry }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        game.Tick(game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(game.FinishWashRinse(customer));
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.IsTrue(game.Assign(customer, dryStation));
        ReachStation(game);
        return customer;
    }
}
