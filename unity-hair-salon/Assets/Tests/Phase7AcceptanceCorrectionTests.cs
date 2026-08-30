using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;

public sealed class Phase7AcceptanceCorrectionTests
{
    [Test]
    public void PendingPaymentDoesNotEnterOperatingIncomeUntilCollected()
    {
        var stats = new DayStats(1);
        var drop = new PaymentDropModel
        {
            Id = 1,
            OrderIncomeComponent = 100,
            TipIncomeComponent = 20,
            FinalPayment = 120
        };

        stats.RecordPaymentGenerated(drop);
        Assert.AreEqual(0, stats.OrderIncome);
        Assert.AreEqual(0, stats.TipIncome);
        Assert.AreEqual(0, stats.OperatingNetIncome);

        stats.RecordPaymentCollected(drop);
        stats.RecordPaymentCollected(drop);
        Assert.AreEqual(100, stats.OrderIncome);
        Assert.AreEqual(20, stats.TipIncome);
        Assert.AreEqual(120, stats.OperatingNetIncome);
    }

    [Test]
    public void FinalPaymentKeepsDiscountedOrderIncomeAndTipStrictlySeparate()
    {
        var payments = new SalonPaymentModel();

        PaymentDropModel drop = payments.CreateFinalPayment(1, 2, 80, 10);

        Assert.AreEqual(80, drop.OrderIncomeComponent);
        Assert.AreEqual(10, drop.TipIncomeComponent);
        Assert.AreEqual(90, drop.FinalPayment);
        Assert.AreEqual(1, payments.Drops.Count, "Order and tip must share one world pickup.");
    }

    [Test]
    public void FreeOrderCreatesZeroOrderIncomeWithoutInventingAnExpense()
    {
        var payments = new SalonPaymentModel();
        var stats = new DayStats(1);
        PaymentDropModel drop = payments.CreateFinalPayment(1, 2, 0, 0);

        stats.RecordPaymentCollected(drop);

        Assert.AreEqual(0, stats.OrderIncome);
        Assert.AreEqual(0, stats.TipIncome);
        Assert.AreEqual(0, stats.CompensationExpense);
        Assert.AreEqual(0, stats.OperatingNetIncome);
    }

    [Test]
    public void CompensationIsARealWalletExpenseAndReducesOperatingNetIncome()
    {
        var game = new SalonGameModel();
        var stats = new DayStats(1);
        int before = game.Balance;

        Assert.IsTrue(game.PayCompensation(stats, 50));

        Assert.AreEqual(before - 50, game.Balance);
        Assert.AreEqual(50, stats.CompensationExpense);
        Assert.AreEqual(-50, stats.OperatingNetIncome);
        Assert.AreEqual(0, stats.OrderIncome);
    }

    [Test]
    public void MultiStepOrderCreatesNoPaymentUntilWholeOrderIsComplete()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(4,
            new List<ServiceType> { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        CompleteWash(game, customer);

        Assert.AreEqual(0, game.Payments.Drops.Count, "A completed service step is not order income.");
        Assert.AreEqual(1, customer.Step);
    }

    [Test]
    public void PreOpenWaitsForExplicitStartWithoutAdvancingTimerOrSpawnPermission()
    {
        var config = DayConfig.CreateDefault();
        config.BusinessDuration = 180f;
        var day = new BusinessDayController(config);

        day.PrepareDay(1);
        day.Tick(90f, 0);

        Assert.AreEqual(DayState.PreOpen, day.State);
        Assert.AreEqual(180f, day.BusinessRemainingTime, .001f);
        Assert.IsFalse(day.CanSpawnCustomers);
        day.StartBusiness();
        Assert.AreEqual(DayState.Business, day.State);
    }

    [Test]
    public void ResultFlowsThroughClosedManagementThenNextDayPreOpen()
    {
        var day = new BusinessDayController(DayConfig.CreateDefault());
        day.PrepareDay(1);
        day.StartBusiness();
        day.ForceResult();

        day.OpenClosedManagement();
        Assert.AreEqual(DayState.ClosedManagement, day.State);
        day.PrepareNextDay();

        Assert.AreEqual(2, day.DayNumber);
        Assert.AreEqual(DayState.PreOpen, day.State);
        Assert.IsFalse(day.CanSpawnCustomers);
        day.StartBusiness();
        Assert.AreEqual(DayState.Business, day.State);
    }

    [Test]
    public void EquipmentPurchaseIsManagementInvestmentAndNeverRewritesFinalizedResult()
    {
        var game = new SalonGameModel();
        var stats = new DayStats(1);
        stats.RecordPaymentCollected(new PaymentDropModel
        {
            Id = 2,
            OrderIncomeComponent = 1500,
            TipIncomeComponent = 200,
            FinalPayment = 1700
        });
        int resultIncome = stats.OperatingNetIncome;
        game.SetFirstDayCompleteForDebug(true);
        int before = game.Balance;

        Assert.IsTrue(game.PurchaseAutoBlowStand());

        Assert.AreEqual(resultIncome, stats.OperatingNetIncome);
        Assert.AreEqual(before - game.AutoBlowStandProduct.Price, game.Balance);
        Assert.AreEqual(1, game.ManagementTransactions.Count);
        Assert.AreEqual(ManagementInvestmentType.Equipment,
            game.ManagementTransactions[0].Type);
    }

    [Test]
    public void ResultDisplayNeverAddsOperatingNetIncomeToWalletAgain()
    {
        var payments = new SalonPaymentModel(1000);
        var stats = new DayStats(1);
        PaymentDropModel drop = payments.CreateFinalPayment(1, 1, 900, 100);
        Assert.IsTrue(payments.BeginCollection(drop.Id));
        Assert.IsTrue(payments.CompleteCollection(drop.Id));
        stats.RecordPaymentCollected(drop);
        int balanceAtResult = payments.Balance;

        stats.FinalizeOperatingResult();

        Assert.AreEqual(2000, balanceAtResult);
        Assert.AreEqual(balanceAtResult, payments.Balance);
        Assert.AreEqual(1000, stats.OperatingNetIncome);
    }

    [Test]
    public void ReputationUsesFinalResultsOnceAndRespectsDailyCaps()
    {
        var config = new ShopReputationConfig
        {
            InitialStars = 3f,
            HappyDelta = .08f,
            NormalDelta = 0f,
            UnhappyDelta = -.08f,
            VeryUnhappyDelta = -.15f,
            UnservedAtCloseDelta = -.08f,
            IncompleteAtCloseDelta = -.15f,
            MaxDailyGain = .1f,
            MaxDailyLoss = .2f
        };
        var reputation = new ShopReputationModel(config);
        var happyDay = new DayStats(1) { HappyCustomers = 5 };

        reputation.ApplyDayResult(happyDay);
        reputation.ApplyDayResult(happyDay);

        Assert.AreEqual(3f, happyDay.ReputationBefore, .001f);
        Assert.AreEqual(.1f, happyDay.ReputationDelta, .001f);
        Assert.AreEqual(3.1f, happyDay.ReputationAfter, .001f);
        Assert.AreEqual(3.1f, reputation.CurrentStars, .001f,
            "The same DayStats cannot apply reputation twice.");
    }

    [Test]
    public void ReputationPersistsAcrossDaysAndOnlyLightlyChangesTraffic()
    {
        var config = DayConfig.CreateDefault();
        config.MinimumTrafficMultiplier = .9f;
        config.MaximumTrafficMultiplier = 1.1f;
        config.ReputationTrafficPerStar = .05f;
        var director = new CustomerTrafficDirector(config);
        var snapshot = new TrafficSnapshot(0, 0, 0, 0, 4);

        TrafficDecision low = director.Evaluate(.3f, snapshot, .5f, 1f);
        TrafficDecision average = director.Evaluate(.3f, snapshot, .5f, 3f);
        TrafficDecision high = director.Evaluate(.3f, snapshot, .5f, 5f);

        Assert.Greater(low.SpawnInterval, average.SpawnInterval);
        Assert.Less(high.SpawnInterval, average.SpawnInterval);
        Assert.AreEqual(.9f, low.ReputationMultiplier, .001f);
        Assert.AreEqual(1.1f, high.ReputationMultiplier, .001f);
    }

    [Test]
    public void OfficialDayStatsNoLongerExposeGeneratedOrUncollectedIncome()
    {
        Type stats = typeof(DayStats);
        Assert.IsNull(stats.GetField("GeneratedIncome"));
        Assert.IsNull(stats.GetProperty("UncollectedIncome"));
        Assert.IsNotNull(stats.GetField("OrderIncome"));
        Assert.IsNotNull(stats.GetField("TipIncome"));
        Assert.IsNotNull(stats.GetProperty("OperatingNetIncome"));
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

    private static void CompleteWash(SalonGameModel game, CustomerModel customer)
    {
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        Assert.IsTrue(game.BeginTowelRemoval(customer) || customer.TowelWrapped,
            "Wash completion must still leave a physical towel transition before order finalization.");
    }
}
