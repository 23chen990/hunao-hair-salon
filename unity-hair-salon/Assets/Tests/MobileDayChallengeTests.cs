using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class MobileDayChallengeTests
{
    [Test]
    public void MobileBlowWindowAllowsAQueueTripAnotherHaircutAndReturn()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        var background = game.Spawn(1201, new System.Collections.Generic.List<ServiceType>
            { ServiceType.Wash, ServiceType.Dry });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(background, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.BeginServiceExecution(background, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        Assert.IsTrue(game.Assign(background, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.StartAutoBlow(background));

        game.Tick(4f); // Walk back to the waiting area.
        var foreground = game.Spawn(1202, new System.Collections.Generic.List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(foreground, 2));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.Tick(4f); // Return to the second work area.
        game.SelectCustomer(foreground);
        var haircut = new HaircutConfig();
        float duration = haircut.GetPerfectMin(SalonTool.Scissors) + .05f;
        Assert.IsTrue(game.BeginHaircutAction(foreground, SalonTool.Scissors, haircut));
        game.Tick(duration);
        Assert.AreEqual(HaircutResult.Perfect, game.CompleteHaircutAction(foreground, duration, false));
        game.EndActiveOperation(foreground);
        game.Tick(4f); // Return for the background customer's finish.

        Assert.IsFalse(background.AutoBlowSafetyStopped,
            "A normal trip to another customer should fit inside the mobile background window.");
        game.SelectCustomer(background);
        Assert.AreEqual(BlowResult.Good, game.FinishAutoBlow(background));
        Assert.AreEqual(2, game.Payments.Drops.Count);
    }

    [Test]
    public void MobileProfileUsesShortDayAndExplicitTrafficLimits()
    {
        DayConfig day = SalonMobileDayConfig.CreateForDay(1);

        Assert.IsTrue(day.IsMobileProfile);
        Assert.AreEqual(120f, day.BusinessDuration, .001f);
        Assert.AreEqual(15f, day.ClosingGraceDuration, .001f);
        Assert.AreEqual(6, day.MaxConcurrentCustomers);
        Assert.AreEqual(4, day.WaitingCapacity);
        Assert.IsTrue(day.AllowSpawnWhileWaitingOverload);
        Assert.Greater(day.TargetOrders, 0);
    }

    [Test]
    public void MobileDaysProgressTargetAndOrderComplexityInSmallSteps()
    {
        DayConfig first = SalonMobileDayConfig.CreateForDay(1);
        DayConfig second = SalonMobileDayConfig.CreateForDay(2);
        DayConfig third = SalonMobileDayConfig.CreateForDay(3);

        Assert.Less(first.TargetOrders, second.TargetOrders);
        Assert.Less(second.TargetOrders, third.TargetOrders);
        Assert.AreEqual("O001", first.PickOrderForSpawn(0, 0f));
        Assert.AreEqual("O002", first.PickOrderForSpawn(1, .05f));
        Assert.AreEqual(first.PickOrderForSpawn(0, 0f), first.PickOrderForSpawn(0, 0f));
        Assert.AreEqual(first.PickOrderForSpawn(7, .65f), first.PickOrderForSpawn(7, .65f));
    }

    [Test]
    public void MobileOverloadSlowsAtThreeWaitingButStopsAtCapacity()
    {
        DayConfig config = SalonMobileDayConfig.CreateForDay(1);
        var director = new CustomerTrafficDirector(config);

        TrafficDecision threeWaiting = director.Evaluate(.45f,
            new TrafficSnapshot(4, 3, 2, 0, 3));
        TrafficDecision fourWaiting = director.Evaluate(.45f,
            new TrafficSnapshot(5, 4, 2, 0, 3));

        Assert.IsTrue(threeWaiting.IsOverloaded);
        Assert.IsTrue(threeWaiting.ShouldSpawn);
        Assert.IsFalse(fourWaiting.ShouldSpawn);
        Assert.Greater(threeWaiting.SpawnInterval,
            director.Evaluate(.45f, new TrafficSnapshot(4, 1, 2, 0, 3)).SpawnInterval);
    }

    [Test]
    public void MobileEvaluationReportsTargetAndRetryabilityOnlyAfterDayEnds()
    {
        DayEvaluation ongoing = SalonMobileDayConfig.Evaluate(1, 5, false);
        DayEvaluation achieved = SalonMobileDayConfig.Evaluate(1, 6, true);
        DayEvaluation missed = SalonMobileDayConfig.Evaluate(1, 2, true);

        Assert.AreEqual(DayOutcome.InProgress, ongoing.Outcome);
        Assert.IsFalse(ongoing.CanRetry);
        Assert.AreEqual(DayOutcome.Achieved, achieved.Outcome);
        Assert.IsFalse(achieved.CanRetry);
        Assert.AreEqual(DayOutcome.NotAchieved, missed.Outcome);
        Assert.IsTrue(missed.CanRetry);
        Assert.AreEqual(1, missed.RemainingOrders);
    }

    [Test]
    public void BusinessDayCanRetrySameDayAndRestoreClampedReputation()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        day.StartBusiness();
        day.ForceResult();

        Assert.IsTrue(day.PrepareRetryDay());
        Assert.AreEqual(1, day.DayNumber);
        Assert.AreEqual(DayState.PreOpen, day.State);

        day.RestoreReputation(99f);
        Assert.AreEqual(5f, day.Reputation.CurrentStars, .001f);
        day.RestoreReputation(-99f);
        Assert.AreEqual(1f, day.Reputation.CurrentStars, .001f);
    }

    [Test]
    public void RestoreClosedManagementDoesNotReapplyDayOrSkipStateEvent()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(2);
        var states = new System.Collections.Generic.List<DayState>();
        day.StateChanged += states.Add;

        Assert.IsTrue(day.RestoreClosedManagement(2, 4.5f));

        Assert.AreEqual(DayState.ClosedManagement, day.State);
        Assert.AreEqual(2, day.DayNumber);
        Assert.AreEqual(4, day.Config.TargetOrders);
        Assert.AreEqual(4.5f, day.Reputation.CurrentStars, .001f);
        Assert.AreEqual(0, day.Stats.CompletedOrders);
        Assert.IsTrue(states.Contains(DayState.ClosedManagement));
    }
}
