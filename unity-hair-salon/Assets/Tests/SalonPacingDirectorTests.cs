using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonPacingDirectorTests
{
    private static SalonProgressData Practiced() => new SalonProgressData
    { BlowDryerPurchased = true, WashStationPurchased = true, PaidCustomerCount = 10,
      PaidDryOrderCount = 3, PaidWashOrderCount = 3 };

    private static SalonGameModel Shop()
    {
        var game = new SalonGameModel();
        game.ConfigureWorkstationAvailability(false, false, false);
        return game;
    }

    [Test]
    public void FirstTwoCustomersAreLearnedOneAtATimeEvenOnALateCalendarDay()
    {
        var p = SalonProgressData.CreateDefault(); p.DayNumber = 8;
        var game = Shop(); game.Spawn(1, new[] { ServiceType.Cut });
        Assert.AreEqual(1, SalonPacingDirector.ActiveLimit(p));
        Assert.IsFalse(new SalonPacingDirector().CanAdmit(game, p));
    }

    [Test]
    public void BuyingEquipmentDoesNotImmediatelyCreateExpertTraffic()
    {
        var p = Practiced(); p.PaidWashOrderCount = 0;
        p.HaircutExpansionPurchased = true; p.WashAnnexExpansionPurchased = true;
        Assert.AreEqual(1, SalonPacingDirector.ActiveLimit(p));
    }

    [Test]
    public void CheckoutBacklogDefersNewArrivals()
    {
        var game = Shop(); var c = game.Spawn(1, new[] { ServiceType.Cut });
        c.State = CustomerState.Checkout;
        Assert.IsFalse(new SalonPacingDirector().CanAdmit(game, Practiced()));
    }

    [Test]
    public void EmptyingABatchAllowsEightSecondsForUsefulManagementWork()
    {
        var pacing = new SalonPacingDirector(); var game = Shop();
        pacing.RegisterArrival(); pacing.RegisterArrival();
        pacing.Tick(.1f, game, true);
        Assert.GreaterOrEqual(pacing.RestRemaining, 8f);
        pacing.Tick(7f, game, true);
        Assert.IsFalse(pacing.CanAdmit(game, Practiced()));
        pacing.Tick(1.1f, game);
        Assert.IsTrue(pacing.CanAdmit(game, Practiced()));
    }

    [Test]
    public void WashBacklogSelectsHaircutInsteadOfAnotherWashFirstOrder()
    {
        var game = Shop(); game.Spawn(1, new[] { ServiceType.Wash, ServiceType.Cut });
        string order = SalonPacingDirector.SelectOrder(game, Practiced(), 1, null, 6, 120f);
        Assert.AreEqual(ServiceType.Cut, SalonOrderCatalog.Get(order)[0]);
    }

    [Test]
    public void EmptyWashSuppliesDoNotGenerateUnserviceableWashOrders()
    {
        string order = SalonPacingDirector.SelectOrder(Shop(), Practiced(), 1, null, 0, 120f);
        CollectionAssert.DoesNotContain(SalonOrderCatalog.Get(order), ServiceType.Wash);
    }

    [Test]
    public void LearningDryingDoesNotTurnEveryOrderIntoAMultiStepOrder()
    {
        var p = Practiced(); p.WashStationPurchased = false; p.PaidDryOrderCount = 1;
        int shortOrders = 0;
        for (int i = 0; i < 12; i++)
            if (SalonPacingDirector.SelectOrder(Shop(), p, i, null, 0, 120f) == "O001") shortOrders++;
        Assert.GreaterOrEqual(shortOrders, 6);
        Assert.Less(shortOrders, 12);
    }

    [Test]
    public void ClosingRejectsLongOrdersButCanStillServeAShortHaircut()
    {
        Assert.AreEqual("O001", SalonPacingDirector.SelectOrder(Shop(), Practiced(), 9, null, 6, 20f));
        Assert.IsNull(SalonPacingDirector.SelectOrder(Shop(), Practiced(), 9, null, 6, 5f));
    }

    [Test]
    public void MasterySurvivesCloneAndJsonWithoutInventingItForOldSaves()
    {
        var p = Practiced(); var copy = p.Clone();
        Assert.AreEqual(10, copy.PaidCustomerCount);
        Assert.AreEqual(3, copy.PaidWashOrderCount);
        Assert.AreEqual(3, copy.PaidDryOrderCount);
        Assert.AreEqual(10, JsonUtility.FromJson<SalonProgressData>(JsonUtility.ToJson(copy)).PaidCustomerCount);
        Assert.AreEqual(0, JsonUtility.FromJson<SalonProgressData>("{\"SchemaVersion\":2,\"DayNumber\":4}").PaidCustomerCount);
    }

    [Test]
    public void ThreeStepOrdersWaitUntilBothServicesHaveBeenPracticed()
    {
        var p = Practiced(); p.PaidWashOrderCount = 2;
        for (int i = 0; i < 20; i++)
            Assert.AreNotEqual("O005", SalonPacingDirector.SelectOrder(Shop(), p, i, null, 6, 120f));
        p.PaidWashOrderCount = 3;
        Assert.AreEqual("O005", SalonPacingDirector.SelectOrder(Shop(), p, 9, null, 6, 120f));
    }

    [Test]
    public void PaidEquipmentAndServiceExperienceBothMatterForCapacity()
    {
        var p = Practiced();
        Assert.AreEqual(2, SalonPacingDirector.ActiveLimit(p));
        p.HaircutExpansionPurchased = true;
        Assert.AreEqual(3, SalonPacingDirector.ActiveLimit(p));
        p.AutoBlowPurchased = p.WashAnnexExpansionPurchased = p.ExtraSeatsPurchased = true;
        Assert.AreEqual(3, SalonPacingDirector.ActiveLimit(p));
        p.PaidCustomerCount = 18;
        Assert.AreEqual(4, SalonPacingDirector.ActiveLimit(p));
    }

    [Test]
    public void AnExistingAngryCustomerGetsAttentionBeforeMoreWork()
    {
        var game = Shop(); var c = game.Spawn(1, new[] { ServiceType.Wash });
        c.State = CustomerState.Serving; c.Emotion = CustomerEmotion.Angry;
        Assert.IsFalse(new SalonPacingDirector().CanAdmit(game, Practiced()));
    }

    [Test]
    public void ClearingABatchOfAbandonedCustomersAlsoAllowsRecovery()
    {
        var game = Shop(); var c = game.Spawn(1, new[] { ServiceType.Cut });
        var pacing = new SalonPacingDirector(); pacing.RegisterArrival();
        c.State = CustomerState.Leaving; pacing.Tick(.1f, game, true);
        Assert.AreEqual(8f, pacing.RestRemaining);
        pacing.Tick(8.1f, game);
        Assert.IsTrue(pacing.CanAdmit(game, Practiced()));
    }

    [Test]
    public void FullChairsDoNotReceiveAnotherCutFirstOrder()
    {
        var game = Shop(); game.Spawn(1, new[] { ServiceType.Cut, ServiceType.Dry });
        Assert.AreEqual(ServiceType.Wash, SalonOrderCatalog.Get(
            SalonPacingDirector.SelectOrder(game, Practiced(), 0, null, 6, 120f))[0]);
    }

    [Test]
    public void InconsistentMasteryDoesNotLoadAsEarnedExperience()
    {
        var p = SalonProgressData.CreateDefault(); p.PaidWashOrderCount = 1;
        Assert.IsFalse(p.TryValidate(out _));
        p.PaidWashOrderCount = 0; p.PaidCustomerCount = -1;
        Assert.IsFalse(p.TryValidate(out _));
    }

    [Test]
    public void ExtraCapacityGetsTwoPaidOrdersOfPracticeBeforeMoreTraffic()
    {
        var p = Practiced(); p.HaircutExpansionPurchased = true;
        p.CapacityPracticeLimit = 2; p.CapacityPracticeUntilPaid = p.PaidCustomerCount + 2;
        Assert.AreEqual(2, SalonPacingDirector.ActiveLimit(p));
        p.PaidCustomerCount++;
        Assert.AreEqual(2, SalonPacingDirector.ActiveLimit(p));
        Assert.AreEqual(12, p.Clone().CapacityPracticeUntilPaid);
        Assert.AreEqual(2, p.Clone().CapacityPracticeLimit);
        p.PaidCustomerCount++;
        Assert.AreEqual(3, SalonPacingDirector.ActiveLimit(p));
    }

    [Test]
    public void ServiceFinishedButNotYetAtCashierStillCountsAsWork()
    {
        var game = Shop(); game.RequireCheckout = true;
        var c = game.Spawn(1, new[] { ServiceType.Cut });
        c.Step = 1; c.State = CustomerState.Finished;
        var pacing = new SalonPacingDirector(); pacing.RegisterArrival();
        pacing.Tick(.1f, game);
        Assert.AreEqual(0f, pacing.RestRemaining, "The checkout trip cannot consume the recovery window.");
        Assert.AreEqual(1, SalonPacingDirector.ActiveCount(game));
        Assert.IsFalse(pacing.CanAdmit(game, Practiced()));
        c.State = CustomerState.Checkout; pacing.Tick(3f, game);
        Assert.AreEqual(0f, pacing.RestRemaining);
        c.State = CustomerState.Leaving; pacing.Tick(.1f, game, true);
        Assert.AreEqual(8f, pacing.RestRemaining);
    }

    [Test]
    public void AnEmptyShopWithoutUsefulWorkDoesNotForceEightIdleSeconds()
    {
        var pacing = new SalonPacingDirector(); var game = Shop();
        pacing.RegisterArrival(); pacing.Tick(.1f, game, false);
        Assert.LessOrEqual(pacing.RestRemaining, 2f);
        pacing.Tick(2.1f, game, false);
        Assert.IsTrue(pacing.CanAdmit(game, Practiced()));
    }

    [Test]
    public void FinishingManagementWorkReleasesTheRemainingEmptyWindow()
    {
        var pacing = new SalonPacingDirector(); var game = Shop();
        pacing.RegisterArrival(); pacing.Tick(.1f, game, true);
        pacing.Tick(.1f, game, false);
        Assert.LessOrEqual(pacing.RestRemaining, 2f);
    }
}
