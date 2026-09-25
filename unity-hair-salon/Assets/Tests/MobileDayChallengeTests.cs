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
    public void MobileProfileUsesThreeMinuteBusinessAndExplicitTrafficLimits()
    {
        DayConfig day = SalonMobileDayConfig.CreateForDay(1);

        Assert.IsTrue(day.IsMobileProfile);
        Assert.AreEqual(180f, day.BusinessDuration, .001f,
            "The mobile business phase is three minutes, matching the approved round length.");
        Assert.AreEqual(15f, day.ClosingGraceDuration, .001f);
        Assert.AreEqual(6, day.MaxConcurrentCustomers);
        Assert.AreEqual(4, day.WaitingCapacity);
        Assert.IsTrue(day.AllowSpawnWhileWaitingOverload);
        Assert.Greater(day.TargetOrders, 0);
    }

    [Test]
    public void MobileHandoffGivesAPlayerTimeToEscortTheSelectedCustomer()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        var customer = game.Spawn(1204, new System.Collections.Generic.List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        float beforeHandoff = customer.Patience;

        Assert.IsTrue(game.EngageCustomerForHandoff(customer));
        float afterEngage = customer.Patience;
        game.Tick(8f);

        Assert.AreEqual(CustomerState.Waiting, customer.State);
        Assert.AreEqual(afterEngage, customer.Patience, .001f,
            "A customer already accepted by the player must not keep draining while the player reaches the station.");
        Assert.GreaterOrEqual(afterEngage, beforeHandoff);
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

        var midDayOrders = new System.Collections.Generic.HashSet<string>();
        for (int index = 2; index < 8; index++)
            midDayOrders.Add(first.PickOrderForSpawn(index, .45f));
        Assert.IsTrue(midDayOrders.Contains("O004"),
            "Mid-day traffic must include a cut-first order so both service zones stay useful.");
    }

    [Test]
    public void MobileHaircutProgressionIntroducesTwoStepToolOrdersAfterIntro()
    {
        SalonTool[] intro = SalonMobileDayConfig.GetHaircutToolsForSpawn(1, 0);
        SalonTool[] followUp = SalonMobileDayConfig.GetHaircutToolsForSpawn(1, 1);

        Assert.AreEqual(1, intro.Length, "The first mobile haircut remains a one-step teaching order.");
        Assert.AreEqual(SalonTool.Scissors, intro[0]);
        Assert.AreEqual(2, followUp.Length,
            "A later mobile haircut must require a second tool so the player has to return to the same service step.");
        Assert.AreEqual(SalonTool.Scissors, followUp[0]);
        Assert.AreEqual(SalonTool.ThinningShears, followUp[1]);

        SalonTool[] secondCut = SalonMobileDayConfig.GetHaircutToolsForSpawn(1, 2);
        Assert.AreEqual(2, secondCut.Length,
            "The first multi-service follow-up cut on Day 1 must use the same two-step rhythm.");
    }

    [Test]
    public void MobileWashToCutHandoffKeepsTheSecondHaircutToolRequired()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        var customer = game.Spawn(1203,
            new System.Collections.Generic.List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        game.Tick(game.ServiceConfig.ShampooDuration + game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(game.FinishWashRinse(customer));
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed);

        game.ConfigureHaircutOrder(customer, SalonMobileDayConfig.GetHaircutToolsForSpawn(1, 2));
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.BeginHaircutAction(customer, SalonTool.Scissors, new HaircutConfig()));
        float duration = game.HaircutHoldDurationFor(SalonTool.Scissors, new HaircutConfig());
        Assert.AreEqual(HaircutResult.Perfect, game.CompleteHaircutAction(customer, duration, false));
        game.EndActiveOperation(customer);

        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed);
        Assert.AreEqual(SalonTool.ThinningShears, customer.HaircutService.CurrentRequiredTool);
    }

    [Test]
    public void MobileTrafficKeepsAcceptingCustomersAfterTheDailyGoalUntilCapacityOrClose()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        day.StartBusiness();
        day.Stats.CompletedOrders = day.Config.TargetOrders - 1;
        Assert.IsTrue(day.CanSpawnCustomers);
        day.Stats.CompletedOrders++;
        Assert.IsTrue(day.CanSpawnCustomers);
        day.Tick(.1f, 1);
        Assert.AreEqual(DayState.Business, day.State, "Keep servicing customers already in the salon.");
        day.Tick(.1f, 0);
        Assert.AreEqual(DayState.Business, day.State,
            "Reaching the reference target must not end the business day early.");
        day.Tick(day.BusinessRemainingTime, 0);
        Assert.AreEqual(DayState.Result, day.State);
        Assert.AreEqual(DayOutcome.Achieved, day.EvaluateDay().Outcome);
    }

    [Test]
    public void MobileAdmissionCountsExistingOrdersAndReopensWhenAnOrderIsLost()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        day.StartBusiness();
        day.Tick(.1f, 3);
        Assert.IsTrue(day.CanSpawnCustomers,
            "The reference target must not stop admission while normal capacity allows it.");
        day.Tick(.1f, 2); // A customer left without a completed order.
        Assert.IsTrue(day.CanSpawnCustomers, "Allow a replacement for the lost order.");
        day.Stats.CompletedOrders = 1;
        day.Tick(.1f, 2);
        Assert.IsTrue(day.CanSpawnCustomers);
    }

    [Test]
    public void MobileAdmissionDoesNotCountFinishedCustomersStillLeavingAsUnfinishedOrders()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        day.StartBusiness();
        day.Stats.CompletedOrders = 2;

        day.TickWithAdmissionCount(.1f, activeUntilExitCount: 1, unfinishedOrderCount: 0);
        Assert.IsTrue(day.CanSpawnCustomers,
            "A finished customer still walking to the exit must not consume the final unfinished-order slot.");

        day.TickWithAdmissionCount(.1f, activeUntilExitCount: 1, unfinishedOrderCount: 1);
        Assert.IsTrue(day.CanSpawnCustomers,
            "Unfinished orders affect traffic overload, not the removed daily target gate.");
    }

    [Test]
    public void MobileAdmissionStillRequiresBusinessTimeAndUnpausedBusinessState()
    {
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        Assert.IsFalse(day.CanSpawnCustomers);
        day.StartBusiness();
        Assert.IsTrue(day.CanSpawnCustomers);
        day.SetPaused(true);
        Assert.IsFalse(day.CanSpawnCustomers);
        day.SetPaused(false);
        day.Tick(day.BusinessRemainingTime, 0);
        Assert.IsFalse(day.CanSpawnCustomers);
    }

    [Test]
    public void NonMobileTrafficStillRunsUntilClosingTime()
    {
        var day = new BusinessDayController(DayConfig.CreateDefault());
        day.PrepareDay(1);
        day.StartBusiness();
        day.Stats.CompletedOrders = 100;
        day.Tick(.1f, 0);
        Assert.IsTrue(day.CanSpawnCustomers);
        Assert.AreEqual(DayState.Business, day.State);
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
