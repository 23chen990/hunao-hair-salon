using HairSalon;
using NUnit.Framework;

public sealed class SalonPacingRegressionTests
{
    [Test]
    public void FirstDayNeverAllowsSixSimultaneousCustomers()
    {
        var config = SalonMobileDayConfig.CreateForDay(1);
        Assert.LessOrEqual(config.MaxConcurrentCustomers, 2);
        Assert.IsFalse(config.AllowSpawnWhileWaitingOverload);
    }

    [Test]
    public void FirstDayOpeningAndPeakHaveReadableArrivalGaps()
    {
        var director = new CustomerTrafficDirector(SalonMobileDayConfig.CreateForDay(1));
        var empty = new TrafficSnapshot(0, 0, 0, 0, 1);
        Assert.GreaterOrEqual(director.Evaluate(0f, empty, 0f, 5f).SpawnInterval, 10f);
        Assert.GreaterOrEqual(director.Evaluate(.7f, empty, 0f, 5f).SpawnInterval, 7f);
    }

    [Test]
    public void FirstDayRecoveryDoesNotAddWorkWhileACustomerStillNeedsAttention()
    {
        var director = new CustomerTrafficDirector(SalonMobileDayConfig.CreateForDay(1));
        Assert.IsFalse(director.Evaluate(.36f, new TrafficSnapshot(1, 0, 1, 0, 2)).ShouldSpawn);
    }

    [Test]
    public void RecoveryWaveStillAdmitsOneCustomerWhenTheShopIsEmpty()
    {
        var director = new CustomerTrafficDirector(SalonMobileDayConfig.CreateForDay(1));
        var decision = director.Evaluate(.36f, new TrafficSnapshot(0, 0, 0, 0, 2));
        Assert.IsTrue(decision.ShouldSpawn);
        Assert.LessOrEqual(decision.SpawnInterval, 20f);
    }

    [Test]
    public void AnUnservedQueueStopsArrivalsInsteadOfMerelySlowingThem()
    {
        var director = new CustomerTrafficDirector(SalonMobileDayConfig.CreateForDay(3));
        Assert.IsFalse(director.Evaluate(.55f, new TrafficSnapshot(3, 2, 1, 0, 3)).ShouldSpawn);
    }

    [Test]
    public void ClosingLeavesEnoughTimeForExistingOrdersAndCheckout()
    {
        var config = SalonMobileDayConfig.CreateForDay(1);
        Assert.LessOrEqual(config.LastAdmissionProgress, .82f);
    }

    [Test]
    public void AdmissionsClosedAndAllCustomersGoneEndsTheDayWithoutEmptyCountdown()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1); day.StartBusiness();
        day.TickWithAdmissionCount(day.Config.BusinessDuration * .83f, 0, 0);
        Assert.AreEqual(DayState.Result, day.State);
    }

    [Test]
    public void QuietClosingStillWaitsForExitAnimationAndSettlement()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1); day.StartBusiness();
        day.TickWithAdmissionCount(day.Config.BusinessDuration * .83f, 1, 0);
        Assert.AreEqual(DayState.Business, day.State);
        day.TickWithAdmissionCount(.1f, 0, 0, 1);
        Assert.AreEqual(DayState.Business, day.State);
        day.TickWithAdmissionCount(.1f, 0, 0);
        Assert.AreEqual(DayState.Result, day.State);
    }
}
