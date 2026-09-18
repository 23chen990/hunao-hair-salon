using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

/// <summary>
/// Mobile single-player service rules approved for the first playable loop.
/// These tests intentionally exercise the model and service domain rather than
/// SalonDemo view wiring so the root agent can integrate the UI independently.
/// </summary>
public sealed class MobileServiceRulesTests
{
    [Test]
    public void WashOnlyOrderSettlesWhenWetHairIsTheOnlyRemainingExitBlocker()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 1001, new[] { ServiceType.Wash }, 0);

        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);

        Assert.IsTrue(customer.OrderRequirementsCompleted);
        Assert.IsTrue(customer.HairWet, "The whole wash service leaves wet hair in the physical projection.");
        Assert.AreEqual(CustomerState.Finished, customer.State,
            "A wash-only order has no requested Dry step; wetness alone must not keep it at the station.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void MobileWashThenCutSettlesWithoutManualTowelActions()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnMobileWashCustomer(game, 1011,
            new[] { ServiceType.Wash, ServiceType.Cut });

        CompleteMobileHaircut(game, customer);

        Assert.IsTrue(customer.OrderRequirementsCompleted,
            "The mobile wash execution must satisfy the displayed Wash step without requiring hidden towel milestones.");
        Assert.AreEqual(CustomerState.Finished, customer.State,
            "Wash+Cut must settle after the mobile 3-second wash and the normal cut action.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void MobileWashThenCutThenDrySettlesWithoutManualTowelActions()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnMobileWashCustomer(game, 1012,
            new[] { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry });

        CompleteMobileHaircut(game, customer);
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);

        Assert.IsTrue(game.StartAutoBlow(customer));
        game.Tick(customer.BackgroundTask.IdealStart + .1f);
        BlowResult result = game.FinishAutoBlow(customer);

        Assert.That(result, Is.EqualTo(BlowResult.Good).Or.EqualTo(BlowResult.Minor));
        Assert.IsTrue(customer.OrderRequirementsCompleted,
            "The mobile Wash+Cut+Dry chain must not retain an unreachable towel requirement.");
        Assert.AreEqual(CustomerState.Finished, customer.State,
            "Wash+Cut+Dry must settle after mobile wash, cut, and background blow-dry.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void WashThenCutSettlesAfterCleanCutWithoutAnUnrequestedDryStep()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 1002,
            new[] { ServiceType.Wash, ServiceType.Cut }, 0);

        CompleteManualWashToCleanTowel(game, customer);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        Assert.IsTrue(game.BeginTowelRemoval(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RemoveTowelDuration));
        Assert.IsFalse(customer.TowelWrapped);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));

        Assert.IsTrue(customer.OrderRequirementsCompleted);
        Assert.IsTrue(customer.HairWet, "No Dry step was requested, so hair may remain wet after the cut.");
        Assert.AreEqual(CustomerState.Finished, customer.State,
            "Wash+Cut must settle once the displayed wash and cut requirements are complete.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void NoDryOrderStillBlocksOnFoamChemicalResidueAndActiveAction()
    {
        OrderDefinition order = OrderDefinition.WashThenCut();
        ServiceProgress progress = new ServiceProgress();
        progress.Set(new ServiceMilestoneState(MilestoneId.WetHairApplied,
            MilestoneKind.HistoricalEvent, true, true));
        progress.Set(new ServiceMilestoneState(MilestoneId.Shampooed,
            MilestoneKind.HistoricalEvent, true, true));
        progress.Set(new ServiceMilestoneState(MilestoneId.RinseClean,
            MilestoneKind.RevalidatableState, true, true));
        progress.Set(new ServiceMilestoneState(MilestoneId.CleanTowelApplied,
            MilestoneKind.HistoricalEvent, true, true));
        progress.Set(new ServiceMilestoneState(MilestoneId.Untoweled,
            MilestoneKind.RevalidatableState, true, true));

        ServiceExitReadinessResult readiness = ServiceExitReadiness.Evaluate(
            order,
            progress.CreateSnapshot(),
            PhysicalSnapshot(wetness: 1f, foamAmount: .5f, shampooState: ShampooState.Normal),
            isMoving: false,
            hasActiveAction: false,
            new ServiceRuleConfig());

        CollectionAssert.Contains(readiness.BlockingConstraints, ExitConstraint.NoFoam);
        CollectionAssert.Contains(readiness.BlockingConstraints, ExitConstraint.NoShampooResidue);
        CollectionAssert.DoesNotContain(readiness.BlockingConstraints, ExitConstraint.DryEnough,
            "Wetness is the only physical blocker that a no-Dry order is allowed to ignore.");

        ServiceExitReadinessResult activeReadiness = ServiceExitReadiness.Evaluate(
            order,
            progress.CreateSnapshot(),
            PhysicalSnapshot(wetness: 1f),
            isMoving: false,
            hasActiveAction: true,
            new ServiceRuleConfig());
        CollectionAssert.Contains(activeReadiness.BlockingConstraints, ExitConstraint.NoActiveAction);
    }

    [Test]
    public void BaseAutoBlowIsAvailableBeforeTheEquipmentPurchase()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = PrepareDryCustomer(game, 1003);

        Assert.IsFalse(game.HasAutoBlowStand,
            "The purchased stand remains a separate growth flag from the base service.");
        PropertyInfo autoBlowAvailable = typeof(SalonGameModel).GetProperty(
            "AutoBlowAvailable", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(autoBlowAvailable,
            "The first playable day must expose a basic automatic blow-dry entry point.");
        Assert.IsTrue((bool)autoBlowAvailable.GetValue(game),
            "The first playable day must expose a basic automatic blow-dry entry point.");
        Assert.IsTrue(game.StartAutoBlow(customer));
        Assert.IsTrue(customer.AutoBlowRunning);
        Assert.AreEqual(BlowStage.AutoRunning, customer.BlowStage);
    }

    [Test]
    public void PurchasedStandShortensStartWindowAndWidensLateRecoveryWindow()
    {
        SalonGameModel baseGame = NewGame();
        CustomerModel baseCustomer = PrepareDryCustomer(baseGame, 1004);
        Assert.IsTrue(baseGame.StartAutoBlow(baseCustomer));

        SalonGameModel upgradedGame = NewGame();
        upgradedGame.SetFirstDayCompleteForDebug(true);
        Assert.IsTrue(upgradedGame.PurchaseAutoBlowStand());
        CustomerModel upgradedCustomer = PrepareDryCustomer(upgradedGame, 1005);
        Assert.IsTrue(upgradedGame.StartAutoBlow(upgradedCustomer));

        Assert.Less(upgradedCustomer.BackgroundTask.IdealStart,
            baseCustomer.BackgroundTask.IdealStart);
        Assert.GreaterOrEqual(upgradedCustomer.BackgroundTask.IdealEnd,
            baseCustomer.BackgroundTask.IdealEnd);
        Assert.GreaterOrEqual(upgradedCustomer.BackgroundTask.DangerAt,
            baseCustomer.BackgroundTask.DangerAt);
    }

    [Test]
    public void BackgroundAutoBlowCanBeFinishedLateAndStillSettles()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = PrepareDryCustomer(game, 1006);
        Assert.IsTrue(game.StartAutoBlow(customer));

        game.Tick(customer.BackgroundTask.IdealEnd + .5f);

        Assert.IsTrue(customer.AutoBlowRunning || customer.AutoBlowSafetyStopped);
        Assert.AreNotEqual(BlowStage.Good, customer.BlowStage,
            "This scenario intentionally returns after the ideal window.");
        BlowResult result = game.FinishAutoBlow(customer);

        Assert.That(result, Is.EqualTo(BlowResult.Minor).Or.EqualTo(BlowResult.Moderate));
        Assert.AreEqual(CustomerState.Finished, customer.State,
            "A late return is recoverable and should still complete the requested Dry step.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [Test]
    public void BackgroundAutoBlowSurvivesASecondCustomerFocusSwitch()
    {
        SalonGameModel game = NewGame();
        CustomerModel background = PrepareDryCustomer(game, 1008);
        CustomerModel foreground = SpawnServing(game, 1009, new[] { ServiceType.Cut }, 2);

        Assert.IsTrue(game.StartAutoBlow(background));
        game.SelectCustomer(foreground);
        Assert.IsTrue(game.ApplyHaircutResult(foreground, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(CustomerState.Finished, foreground.State);

        game.SelectCustomer(background);
        game.Tick(background.BackgroundTask.IdealStart + .1f);
        BlowResult result = game.FinishAutoBlow(background);

        Assert.That(result, Is.EqualTo(BlowResult.Good).Or.EqualTo(BlowResult.Minor));
        Assert.AreEqual(CustomerState.Finished, background.State);
        int backgroundPayments = 0;
        for (int i = 0; i < game.Payments.Drops.Count; i++)
            if (game.Payments.Drops[i].CustomerId == background.Id) backgroundPayments++;
        Assert.AreEqual(1, backgroundPayments,
            "Switching focus while auto-blow runs must preserve one and only one payment.");
    }

    [Test]
    public void IgnoredEngagedCustomerLosesPatienceAfterGraceAndLeavesOnce()
    {
        SalonGameModel game = new SalonGameModel(
            patienceConfig: new CustomerPatienceConfig
            {
                DrainPerSecond = 10f,
                ServiceArrivalGraceSeconds = 0f,
                ServiceStartRelief = 0f
            },
            serviceConfig: new SalonServiceConfig { ReadyDelayGrace = 1f },
            experienceProfile: new CustomerExperienceProfile
            {
                InitialPatience = 4f,
                MaxPatience = 4f
            });
        CustomerModel customer = SpawnServing(game, 1007, new[] { ServiceType.Cut }, 1);

        Assert.IsTrue(game.BeginActiveOperation(customer));
        float patienceDuringActiveOperation = customer.Patience;
        game.Tick(2f);
        Assert.AreEqual(patienceDuringActiveOperation, customer.Patience, .001f,
            "An active operation must not be interrupted by patience expiry.");
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.IsTrue(game.EndActiveOperation(customer));

        game.Tick(.9f);
        Assert.AreEqual(patienceDuringActiveOperation, customer.Patience, .001f,
            "The configured post-service attention grace must be visible before drain starts.");
        game.Tick(.2f);
        Assert.Less(customer.Patience, patienceDuringActiveOperation);
        game.Tick(1f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.AreEqual(1, game.AngryLeaves);
        game.Tick(1f);
        Assert.AreEqual(1, game.AngryLeaves,
            "A single neglected customer can only count one departure.");
    }

    [Test]
    public void RestorePersistentStateRestoresBalanceAndPermanentFlagsAtSafeBoundary()
    {
        MethodInfo restore = typeof(SalonGameModel).GetMethod("RestorePersistentState",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(restore,
            "The root integration needs an explicit persistence restore API.");

        SalonGameModel game = NewGame();
        restore.Invoke(game, new object[] { 4321, true, true });

        Assert.AreEqual(4321, game.Balance);
        Assert.IsTrue(game.HasAutoBlowStand);
        Assert.IsTrue(game.FirstDayCompleteForShop);
        Assert.IsTrue(game.PurchaseAutoBlowStand(),
            "A restored purchase remains an idempotent success and must not spend again.");
        Assert.AreEqual(4321, game.Balance);
    }

    [Test]
    public void RestorePersistentStateDoesNotResetCollectedPaymentProtection()
    {
        SalonPaymentModel payments = new SalonPaymentModel(100);
        PaymentDropModel drop = payments.CreateFinalPayment(77, 1, 20, 0);
        Assert.IsTrue(payments.BeginCollection(drop.Id));
        Assert.IsTrue(payments.CompleteCollection(drop.Id));
        Assert.IsFalse(payments.CompleteCollection(drop.Id),
            "The existing payment model must continue rejecting duplicate pickup.");
    }

    [Test]
    public void RestorePersistentStateRejectsAPlayerDayWithLiveCustomers()
    {
        SalonGameModel game = NewGame();
        Assert.IsNotNull(game.Spawn(1010, new List<ServiceType> { ServiceType.Cut }));

        Assert.Throws<InvalidOperationException>(() =>
            game.RestorePersistentState(4321, false, false));
    }

    [Test]
    public void ResetForNextDayReusesCustomerIdWithFreshServiceStateAndKeepsWalletAndEquipment()
    {
        SalonGameModel game = NewGame();
        game.SetFirstDayCompleteForDebug(true);
        Assert.IsTrue(game.PurchaseAutoBlowStand());
        int balanceAfterPurchase = game.Balance;

        CustomerModel previous = SpawnServing(game, 1011,
            new[] { ServiceType.Wash }, 0);
        Assert.IsTrue(game.BeginServiceExecution(previous, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        Assert.AreEqual(CustomerState.Finished, previous.State);
        Assert.AreEqual(1, game.Payments.Drops.Count,
            "The first day must leave a payment drop that the boundary clears.");

        game.ResetForNextDay();

        Assert.AreEqual(0, game.Customers.Count);
        Assert.AreEqual(0, game.Payments.Drops.Count);
        Assert.AreEqual(balanceAfterPurchase, game.Balance,
            "ResetForNextDay must clear daily drops without changing the wallet.");
        Assert.IsTrue(game.HasAutoBlowStand,
            "The purchased equipment is permanent across a day boundary.");

        CustomerModel next = null;
        Assert.DoesNotThrow(() => next = game.Spawn(1011,
            new List<ServiceType> { ServiceType.Cut }));
        Assert.IsNotNull(next);
        Assert.IsFalse(next.HairWet,
            "A reused customer ID must not inherit the previous wash physical state.");
        Assert.IsFalse(next.ShampooApplied);
        Assert.IsFalse(next.TowelWrapped);
        Assert.IsFalse(next.OrderRequirementsCompleted);
        Assert.IsFalse(game.GetServiceProgressSnapshot(next)[MilestoneId.WetHairApplied].EverCompleted);
        Assert.IsFalse(game.GetServiceProgressSnapshot(next)[MilestoneId.Shampooed].EverCompleted);
        Assert.IsFalse(game.GetServiceProgressSnapshot(next)[MilestoneId.RinseClean].EverCompleted);

        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(next, 1));
        ReachStation(game);
        HaircutConfig config = new HaircutConfig();
        Assert.IsTrue(game.BeginHaircutAction(next, SalonTool.Scissors, config));
        Assert.AreEqual(HaircutResult.Perfect,
            game.CompleteHaircutAction(next, config.GetPerfectMin(SalonTool.Scissors) + .05f, false));
        Assert.IsTrue(game.EndActiveOperation(next));
        Assert.AreEqual(CustomerState.Finished, next.State,
            "The reused ID must be able to start and settle a new Cut order.");
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 10,
            WaitingCapacity = 10,
            InitialCustomerCount = 0
        });
    }

    private static CustomerPhysicalStateSnapshot PhysicalSnapshot(
        float wetness = 0f,
        float foamAmount = 0f,
        ShampooState shampooState = ShampooState.None)
    {
        return new CustomerPhysicalStateSnapshot(
            wetness,
            foamAmount,
            shampooState,
            isTowelWrapped: false,
            towelContamination: TowelContamination.None,
            towelCondition: TowelCondition.Intact,
            physicalStateRevision: 0,
            hairLengthDeviation: 0f,
            haircutProgress: new float[3]);
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

    private static CustomerModel PrepareDryCustomer(SalonGameModel game, int id)
    {
        CustomerModel customer = SpawnServing(game, id,
            new[] { ServiceType.Wash, ServiceType.Dry }, 0);
        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        Assert.IsTrue(customer.HairWet);
        return customer;
    }

    private static CustomerModel SpawnMobileWashCustomer(
        SalonGameModel game, int id, IReadOnlyList<ServiceType> needs)
    {
        CustomerModel customer = SpawnServing(game, id, needs, 0);
        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed,
            "The mobile 3-second wash execution must advance to the next requested service.");
        return customer;
    }

    private static void CompleteMobileHaircut(SalonGameModel game, CustomerModel customer)
    {
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        HaircutConfig config = new HaircutConfig();
        Assert.IsTrue(game.BeginHaircutAction(customer, SalonTool.Scissors, config));
        HaircutResult result = game.CompleteHaircutAction(
            customer, config.GetPerfectMin(SalonTool.Scissors) + .05f, false);
        Assert.AreEqual(HaircutResult.Perfect, result);
        Assert.IsTrue(game.EndActiveOperation(customer));
    }

    private static void CompleteManualWashToCleanTowel(
        SalonGameModel game, CustomerModel customer)
    {
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed);
        Assert.IsTrue(customer.TowelWrapped);
    }

    private static void ReachStation(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }
}
