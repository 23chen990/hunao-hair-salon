using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class CoreReworkAcceptanceTests
{
    [Test]
    public void NewShopStartsWithOneHundredCoinsAndDryerFirst()
    {
        Assert.AreEqual(100, SalonProgressData.CreateDefault().Balance);
        Assert.AreEqual(7, SalonUnlockRoute.Route.Count);
        Assert.AreEqual("BlowDryer", SalonUnlockRoute.Route[0].Id.ToString());
        Assert.AreEqual(600, SalonUnlockRoute.Route[0].Cost);
        var reward = new SalonRewardConfig();
        SalonMobileDayConfig.ApplyRewardProfile(reward);
        Assert.GreaterOrEqual(100 + 2 * (reward.RecoveredBaseReward + reward.TipRedReward), 600,
            "Two successful basic haircuts must afford the first unlock, even with the smallest tip.");
    }

    [Test]
    public void OpeningStartsWithASmallBatchWithinTwentySeconds()
    {
        DayConfig config = SalonMobileDayConfig.CreateForDay(1);
        var director = new CustomerTrafficDirector(config);
        int arrived = 0;
        float next = 0f;
        for (float t = 0f; t < 20f; t += .1f)
        {
            if (t < next) continue;
            var decision = director.Evaluate(t / config.BusinessDuration,
                new TrafficSnapshot(arrived, arrived, 0, 0, 1), .5f);
            if (!decision.ShouldSpawn) continue;
            arrived++;
            next = t + decision.SpawnInterval;
        }
        Assert.GreaterOrEqual(arrived, 1);
        Assert.LessOrEqual(arrived, 2,
            "首日先给小批上客，不能在二十秒内持续堆到三位顾客。");
        Assert.That(100f / SalonMobileDayConfig.PatienceDrainPerSecond(1), Is.InRange(60f, 65f));
    }

    [Test]
    public void FoamWaitTakesEightSecondsAndBuiltStandHasABackgroundReturnWindow()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        Assert.AreEqual(8f, game.ServiceConfig.FoamOptimalStart, .01f);
        var customer = game.Spawn(401, new List<ServiceType> { ServiceType.Dry });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.InstallAutoBlowStand();
        Assert.IsTrue(game.StartAutoBlow(customer));
        Assert.That(customer.BackgroundTask.IdealStart, Is.InRange(7f, 8f));
        Assert.Greater(customer.BackgroundTask.IdealEnd, customer.BackgroundTask.IdealStart);
    }

    [Test]
    public void FoamHoldCanPauseWithoutConsumingItsEightSecondWait()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        var c = game.Spawn(99, new[] { ServiceType.Wash, ServiceType.Cut });
        game.Tick(1f); game.Assign(c, 0); game.Tick(1f);
        Assert.IsTrue(game.BeginWashFoamHold(c));
        game.Tick(.4f);
        float progress = c.ActiveServiceElapsed;
        c.TimedActionPaused = true;
        game.Tick(2f);
        Assert.AreEqual(progress, c.ActiveServiceElapsed);
        Assert.IsFalse(game.IsWashFoamWaitRunning(c));
        c.TimedActionPaused = false;
        game.Tick(1f);
        Assert.IsTrue(game.IsWashFoamWaitRunning(c));
    }

    [Test]
    public void ClosingStartsWalksAndRecordsUnservedOnlyOnce()
    {
        var game = new SalonGameModel();
        var c = game.Spawn(98, new[] { ServiceType.Cut });
        game.Tick(1f);
        var stats = new DayStats(1);
        game.BeginClosingWalks(stats);
        game.BeginClosingWalks(stats);
        Assert.AreEqual(CustomerState.Leaving, c.State);
        Assert.AreEqual(1, game.Customers.Count);
        Assert.AreEqual(1, stats.UnservedAtClose);
    }

    [Test]
    public void ClosingDuringReadyFoamStillLetsTheCustomerFinishLeaving()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        var c = game.Spawn(97, new[] { ServiceType.Wash, ServiceType.Cut });
        game.Tick(1f); game.Assign(c, 0); game.Tick(1f);
        Assert.IsTrue(game.BeginWashFoamHold(c));
        game.Tick(1.3f); game.Tick(8f);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(c));
        var stats = new DayStats(1);
        game.BeginClosingWalks(stats);
        for (int i = 0; i < 50; i++) game.Tick(.2f);
        Assert.AreEqual(CustomerState.Exited, c.State);
        Assert.AreEqual(0, game.Customers.Count);
        Assert.AreEqual(1, stats.IncompleteAtClose);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void MobilePaymentWaitsForCheckout()
    {
        var root = new GameObject("Checkout integration");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            game.Payments.RestoreBalance(100);
            typeof(SalonDemo).GetField("_mobileMode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(demo, true);
            typeof(SalonDemo).GetField("_game", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(demo, game);
            var bill = game.Payments.CreateFinalPayment(1, 1, 300, 60);
            typeof(SalonDemo).GetMethod("HandlePaymentCreated", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(demo, new object[] { bill });
            Assert.AreEqual(100, game.Balance);
            Assert.AreEqual(PaymentDropState.Pending, bill.State);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CheckoutNeverPaysTwiceOrPaysAtClosing()
    {
        var checkout = new SalonCheckoutModel();
        checkout.RegisterBill(1, 360);
        checkout.RegisterBill(1, 360);
        checkout.RegisterBill(2, 300);
        Assert.AreEqual(2, checkout.Queue.Count);
        checkout.MarkArrived(1);
        checkout.Tick(.4f, true);
        Assert.AreEqual(360, checkout.PaidAmount);
        checkout.ForceClose();
        Assert.AreEqual(360, checkout.PaidAmount);
        Assert.AreEqual(300, checkout.UnpaidAmount);
        Assert.IsFalse(checkout.HasPendingCustomers);
    }

    [Test]
    public void WalkingAwayDuringPaymentStillAllowsAbandonment()
    {
        var checkout = new SalonCheckoutModel();
        checkout.RegisterBill(1, 300);
        checkout.MarkArrived(1);
        checkout.Tick(.1f, true);
        checkout.Tick(21f, false);
        Assert.AreEqual(1, checkout.UnpaidCount);
        Assert.AreEqual(0, checkout.PaidAmount);
    }

    [Test]
    public void EquipmentFiltersOrdersAndOnlyOneConstructionPadAppears()
    {
        foreach (string id in new[] { "O001", "O002", "O003", "O004", "O005" })
            Assert.AreEqual("O001", SalonServiceMenu.FilterOrder(id, new SalonServiceMenu(false, false)));
        var pads = SalonUnlockRoute.VisibleOn(99, id => false, id => 20);
        Assert.AreEqual(1, pads.Count);
        Assert.AreEqual(SalonUnlockId.BlowDryer, pads[0]);
        Assert.AreEqual("O004", SalonServiceMenu.TeachingOrderAfter(SalonUnlockId.BlowDryer));
        Assert.AreEqual("O003", SalonServiceMenu.TeachingOrderAfter(SalonUnlockId.WashStation));
    }

    [Test]
    public void WalkingOverAnUnrevealedChairPadCannotSpendMoney()
    {
        var root = new GameObject("Hidden construction regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel(); game.Payments.RestoreBalance(100);
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            day.PrepareDay(1); day.StartBusiness(); day.Tick(2f, 0);
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var pair in new Dictionary<string, object> {
                { "_mobileMode", true }, { "_game", game }, { "_dayController", day },
                { "_player", root.transform }, { "_mobileHaircutExpansionPadPoint", root.transform },
                { "_mobileProgress", SalonProgressData.CreateDefault() },
                { "_mobileHaircutExpansionPad", new SalonProximityPurchasePadModel("expansion-pad-haircut-2", 2000) } })
                typeof(SalonDemo).GetField(pair.Key, fields).SetValue(demo, pair.Value);
            typeof(SalonDemo).GetMethod("CreateMobileUnlockPads", fields)
                .Invoke(demo, new object[] { SalonProgressData.CreateDefault() });
            typeof(SalonDemo).GetMethod("UpdateMobileHaircutExpansionPad", fields)
                .Invoke(demo, new object[] { 1f });
            Assert.AreEqual(100, game.Balance);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ServiceProgressDoesNotShowPatiencePercentage()
    {
        var root = new GameObject("Service feedback");
        try
        {
            var customer = new CustomerModel { State = CustomerState.Serving,
                Needs = new List<ServiceType> { ServiceType.Cut }, Patience = 78f,
                ServicePhase = ServiceStepPhase.Active };
            var view = root.AddComponent<CustomerUrgencyView>();
            view.Initialize(customer, null);
            Assert.AreEqual("服务中", view.StatusText);
            Assert.AreEqual(string.Empty, view.PatienceText);
            customer.ServicePhase = ServiceStepPhase.Ready;
            view.Refresh();
            StringAssert.Contains("78", view.PatienceText);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
