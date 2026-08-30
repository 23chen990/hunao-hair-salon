using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class Phase7BusinessDayTests
{
    [Test]
    public void BusinessDurationAndPressureThresholdsComeFromDayConfig()
    {
        var config = DayConfig.CreateDefault();
        config.BusinessDuration = 240f;
        config.PressurePhase2Start = .15f;
        config.PressurePhase3Start = .5f;
        config.PressurePhase4Start = .85f;
        var day = new BusinessDayController(config);

        day.StartDay(3);
        day.StartBusiness();
        day.Tick(60f, 1);

        Assert.AreEqual(DayState.Business, day.State);
        Assert.AreEqual(180f, day.BusinessRemainingTime, .001f);
        Assert.AreEqual(.25f, day.BusinessProgress, .001f);
        Assert.AreEqual(DayPressurePhase.Normal, day.CurrentPressurePhase);
    }

    [Test]
    public void PreOpenAndPauseFreezeBusinessAndClosingTimers()
    {
        var config = DayConfig.CreateDefault();
        config.StartingDuration = 2f;
        config.BusinessDuration = 10f;
        config.ClosingGraceDuration = 5f;
        var day = new BusinessDayController(config);

        day.StartDay(1);
        day.Tick(20f, 2);
        Assert.AreEqual(DayState.PreOpen, day.State);
        Assert.AreEqual(10f, day.BusinessRemainingTime, .001f);

        day.StartBusiness();
        day.SetPaused(true);
        day.Tick(20f, 2);
        Assert.AreEqual(10f, day.BusinessRemainingTime, .001f);

        day.SetPaused(false);
        day.Tick(10f, 2);
        Assert.AreEqual(DayState.ClosingGrace, day.State);
        day.SetPaused(true);
        day.Tick(20f, 2);
        Assert.AreEqual(5f, day.ClosingGraceRemainingTime, .001f);
    }

    [Test]
    public void BusinessStopsAtZeroAndClosingHasOneFixedWindow()
    {
        var config = DayConfig.CreateDefault();
        config.StartingDuration = 0f;
        config.BusinessDuration = 4f;
        config.ClosingGraceDuration = 3f;
        var day = new BusinessDayController(config);
        day.StartDay(1);
        day.StartBusiness();

        day.Tick(4f, 3);
        Assert.AreEqual(DayState.ClosingGrace, day.State);
        Assert.IsFalse(day.CanSpawnCustomers);

        day.Tick(3f, 2);
        Assert.AreEqual(DayState.Result, day.State);
        day.Tick(30f, 1);
        Assert.AreEqual(DayState.Result, day.State, "Closing grace must never extend itself.");
    }

    [Test]
    public void EmptyShopEndsClosingGraceEarly()
    {
        var config = DayConfig.CreateDefault();
        config.StartingDuration = 0f;
        config.BusinessDuration = 1f;
        config.ClosingGraceDuration = 30f;
        var day = new BusinessDayController(config);
        day.StartDay(1);
        day.StartBusiness();

        day.Tick(1f, 0);

        Assert.AreEqual(DayState.Result, day.State);
    }

    [Test]
    public void TrafficDirectorSlowsOverloadAndRushCannotBreakHardCap()
    {
        var config = DayConfig.CreateDefault();
        config.MaxConcurrentCustomers = 6;
        config.OverloadWaitingThreshold = 3;
        config.Rush.StartProgress = .6f;
        config.Rush.DurationProgress = .12f;
        var director = new CustomerTrafficDirector(config);

        TrafficDecision normal = director.Evaluate(.4f,
            new TrafficSnapshot(2, 1, 1, 0, 3));
        TrafficDecision overloaded = director.Evaluate(.4f,
            new TrafficSnapshot(5, 4, 3, 2, 3));
        TrafficDecision rush = director.Evaluate(.65f,
            new TrafficSnapshot(4, 1, 2, 0, 3));
        TrafficDecision cappedRush = director.Evaluate(.65f,
            new TrafficSnapshot(6, 2, 3, 0, 3));

        Assert.Greater(overloaded.SpawnInterval, normal.SpawnInterval);
        Assert.IsTrue(rush.IsRushActive);
        Assert.Less(rush.SpawnInterval, normal.SpawnInterval);
        Assert.IsFalse(cappedRush.ShouldSpawn);
    }

    [Test]
    public void PressureCurveMovesFromLightToFinalPeakByProgress()
    {
        var director = new CustomerTrafficDirector(DayConfig.CreateDefault());
        var light = director.Evaluate(.1f, new TrafficSnapshot(0, 0, 0, 0, 3));
        var normal = director.Evaluate(.3f, new TrafficSnapshot(0, 0, 0, 0, 3));
        var busy = director.Evaluate(.56f, new TrafficSnapshot(0, 0, 0, 0, 3));
        var peak = director.Evaluate(.9f, new TrafficSnapshot(0, 0, 0, 0, 3));

        Assert.AreEqual(DayPressurePhase.OpeningLight, light.Phase);
        Assert.AreEqual(DayPressurePhase.Normal, normal.Phase);
        Assert.AreEqual(DayPressurePhase.Busy, busy.Phase);
        Assert.AreEqual(DayPressurePhase.FinalPeak, peak.Phase);
        Assert.Greater(light.SpawnInterval, normal.SpawnInterval);
        Assert.Greater(normal.SpawnInterval, busy.SpawnInterval);
        Assert.Greater(busy.SpawnInterval, peak.SpawnInterval);
    }

    [Test]
    public void DayStatsSeparatesCollectedOrderAndTipIncome()
    {
        var stats = new DayStats(1);
        var drop = new PaymentDropModel
        {
            Id = 4,
            CustomerId = 7,
            OrderIncomeComponent = 100,
            TipIncomeComponent = 20,
            FinalPayment = 120
        };

        stats.RecordPaymentGenerated(drop);
        Assert.AreEqual(0, stats.OrderIncome);
        Assert.AreEqual(0, stats.TipIncome);

        stats.RecordPaymentCollected(drop);
        stats.RecordPaymentCollected(drop);
        Assert.AreEqual(100, stats.OrderIncome, "One pickup cannot be counted twice.");
        Assert.AreEqual(20, stats.TipIncome);
        Assert.AreEqual(120, stats.OperatingNetIncome);
    }

    [Test]
    public void ForcedCloseDistinguishesUnservedFromIncompleteCustomers()
    {
        var game = NewGame();
        var unserved = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        var incomplete = game.Spawn(2, new List<ServiceType> { ServiceType.Wash, ServiceType.Dry });
        game.Tick(SalonGameModel.EnteringSeconds);
        Assert.IsTrue(game.Assign(incomplete, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds);
        Assert.IsTrue(game.BeginWashAction(incomplete, WashAction.Shower));
        var stats = new DayStats(1);

        game.ForceCloseRemainingCustomers(stats);

        Assert.AreEqual(1, stats.UnservedAtClose);
        Assert.AreEqual(1, stats.IncompleteAtClose);
        Assert.AreEqual(CustomerServiceResult.Failed, unserved.ServiceResult);
        Assert.AreEqual(CustomerServiceResult.SevereUnhappyCompletion, incomplete.ServiceResult);
        Assert.AreEqual(0, game.Customers.Count);
    }

    [Test]
    public void ForcedCloseTreatsCompletedRequirementsWithATowelAsIncomplete()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(3, new List<ServiceType> { ServiceType.Wash });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        Assert.IsTrue(customer.IsComplete);
        Assert.IsTrue(customer.HasBlockingPhysicalState);
        var stats = new DayStats(1);

        game.ForceCloseRemainingCustomers(stats);

        Assert.AreEqual(1, stats.IncompleteAtClose);
        Assert.AreEqual(CustomerServiceResult.SevereUnhappyCompletion, customer.ServiceResult);
    }

    [Test]
    public void ResultShopNextDayResetsDailyStateButKeepsPermanentPurchase()
    {
        var game = NewGame();
        game.SetFirstDayCompleteForDebug(true);
        Assert.IsTrue(game.PurchaseAutoBlowStand());
        int balanceAfterPurchase = game.Balance;
        game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        var day = new BusinessDayController(DayConfig.CreateDefault());
        day.StartDay(1);
        day.ForceResult();
        day.OpenShop();

        day.BeginNextDay();
        game.ResetForNextDay();

        Assert.AreEqual(2, day.DayNumber);
        Assert.AreEqual(DayState.Starting, day.State);
        Assert.AreEqual(0, game.Customers.Count);
        Assert.AreEqual(balanceAfterPurchase, game.Balance);
        Assert.IsTrue(game.HasAutoBlowStand);
    }

    [Test]
    public void ConfiguredOrderPickerOnlyReturnsImplementedOrders()
    {
        var config = DayConfig.CreateDefault();
        string[] allowed = { "O001", "O002", "O003", "O004", "O005" };

        for (int i = 0; i <= 100; i++)
            CollectionAssert.Contains(allowed, config.PickOrder(i / 100f));
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 8,
            WaitingCapacity = 8,
            InitialCustomerCount = 0,
            BusinessDuration = 180f
        });
    }
}
