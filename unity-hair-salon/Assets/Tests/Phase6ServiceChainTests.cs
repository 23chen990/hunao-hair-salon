using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class Phase6ServiceChainTests
{
    [Test]
    public void CatalogContainsOnlyTheFiveApprovedOrders()
    {
        Assert.AreEqual(5, SalonOrderCatalog.All.Count);
        CollectionAssert.AreEqual(new[] { ServiceType.Cut }, SalonOrderCatalog.Get("O001"));
        CollectionAssert.AreEqual(new[] { ServiceType.Wash, ServiceType.Dry }, SalonOrderCatalog.Get("O002"));
        CollectionAssert.AreEqual(new[] { ServiceType.Wash, ServiceType.Cut }, SalonOrderCatalog.Get("O003"));
        CollectionAssert.AreEqual(new[] { ServiceType.Cut, ServiceType.Dry }, SalonOrderCatalog.Get("O004"));
        CollectionAssert.AreEqual(new[] { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry }, SalonOrderCatalog.Get("O005"));
    }

    [Test]
    public void OrderExposesCurrentCompletedAndNextSteps()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 1, 0, "O005");

        Assert.AreEqual(0, customer.CurrentStepIndex);
        Assert.AreEqual(ServiceType.Wash, customer.CurrentNeed);
        Assert.AreEqual(ServiceType.Cut, customer.NextNeed);
        Assert.AreEqual(0, customer.CompletedStepCount);
        Assert.IsFalse(customer.IsComplete);
    }

    [Test]
    public void WashKeepsCustomerAtTheBedAfterWrappingUntilTheNextStationIsChosen()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 2, 0, "O003");

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
        Assert.IsTrue(game.IsStationOccupied(0));

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.AreEqual(WashStage.Rinsed, customer.WashStage);
        Assert.IsTrue(game.IsStationOccupied(0));

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        Assert.AreEqual(1, customer.CompletedStepCount);
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(0, customer.Station);
        Assert.IsTrue(game.IsStationOccupied(0));
        CollectionAssert.DoesNotContain(game.WaitingQueue, customer);
        Assert.AreEqual("AwaitingTransfer", game.Workstations[0].State.ToString());
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void TwoWashCustomersCanUseTheTwoVisibleWashBedsIndependently()
    {
        var game = NewGame();
        CustomerModel first = game.Spawn(70, new List<ServiceType>(SalonOrderCatalog.Get("O003")));
        CustomerModel second = game.Spawn(71, new List<ServiceType>(SalonOrderCatalog.Get("O003")));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.Assign(first, 0));
        Assert.IsTrue(game.Assign(second, 4));
        Assert.AreEqual(WorkstationType.Wash, game.Workstations[4].Type);
        Assert.AreEqual(first.Id, game.Workstations[0].CurrentCustomerId);
        Assert.AreEqual(second.Id, game.Workstations[4].CurrentCustomerId);
    }

    [Test]
    public void WrappedCustomerMustRemoveTowelBeforeCutOrBlow()
    {
        var game = NewGame();
        CustomerModel customer = CompleteWashAndAssignStyling(game, 3, "O003");

        Assert.IsFalse(game.BeginActiveOperation(customer));
        Assert.IsTrue(game.BeginTowelRemoval(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RemoveTowelDuration));
        Assert.IsFalse(customer.TowelWrapped);
        Assert.IsTrue(game.BeginActiveOperation(customer));
    }

    [Test]
    public void WrappedReturningCustomerCanBeSentToAnEmptyChairAheadOfAnotherWaitingCustomer()
    {
        var game = NewGame();
        CustomerModel returning = SpawnServing(game, 72, 0, "O003");
        CustomerModel earlierWaiting = game.Spawn(73,
            new List<ServiceType>(SalonOrderCatalog.Get("O001")));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        CompleteWash(game, returning);

        CollectionAssert.AreEqual(new[] { earlierWaiting }, game.WaitingQueue);
        Assert.IsTrue(returning.TowelWrapped);
        Assert.AreEqual(ServiceType.Cut, returning.CurrentNeed);
        Assert.IsTrue(game.IsStationOccupied(0));
        Assert.IsTrue(game.Assign(returning, 1));
        Assert.IsFalse(game.IsStationOccupied(0));
        Assert.IsTrue(game.IsStationOccupied(1));
        Assert.AreEqual(CustomerState.MovingToStation, returning.State);
    }

    [Test]
    public void PartialWashStateTravelsWithCustomerAcrossStations()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 74, 0, "O003");
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));

        Assert.IsTrue(game.Assign(customer, 1));
        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
        Assert.AreEqual(BackgroundTaskState.Inactive, customer.BackgroundTask.State);

        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
        Assert.IsTrue(game.Assign(customer, 0));
        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
    }

    [Test]
    public void WrappedTowelStateSurvivesTransfersInBothDirections()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 75, 0, "O003");
        CompleteWash(game, customer);

        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(customer.TowelWrapped);

        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(WashStage.TowelWrapped, customer.WashStage);
    }

    [Test]
    public void IncompleteHaircutCanMoveToWashStationWithoutLosingProgress()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 76, 1, "O001");
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors, SalonTool.Clippers);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(1, customer.HaircutService.CurrentStepIndex);

        Assert.IsTrue(game.Assign(customer, 0));

        Assert.AreEqual(1, customer.HaircutService.CurrentStepIndex);
        Assert.AreEqual(HaircutServiceState.Active, customer.HaircutService.State);
    }

    [Test]
    public void ActiveOperationAloneLocksTransferAndCancellationRestoresIt()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 77, 0, "O003");
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));

        Assert.IsFalse(game.Assign(customer, 1));
        Assert.AreEqual(0, customer.Station);

        game.CancelActiveServiceAction(customer);

        Assert.IsTrue(game.Assign(customer, 1));
        Assert.AreEqual(WashStage.Dry, customer.WashStage);
    }

    [Test]
    public void CutThenBlowKeepsTheSameStylingStationOccupied()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 4, 1, "O004");
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(1, customer.Station);
        Assert.IsTrue(game.IsStationOccupied(1));
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void ManualBlowProgressesOnlyWhileHeldAndCapsAtModerate()
    {
        var game = NewGame();
        CustomerModel early = SpawnServing(game, 10, 1, "O002", completeWashFirst: true);
        RemoveTowel(game, early);
        Assert.IsTrue(game.StartManualBlow(early));
        game.TickManualBlow(early, 2f);
        Assert.AreEqual(BlowResult.Undone, game.EndManualBlowHold(early));
        Assert.AreEqual(CustomerState.Serving, early.State);

        Assert.IsTrue(game.StartManualBlow(early));
        game.TickManualBlow(early, 1.5f);
        Assert.AreEqual(BlowResult.Good, game.EndManualBlowHold(early));
        Assert.AreEqual(CustomerState.Finished, early.State);
        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);

        CustomerModel late = SpawnServing(game, 11, 1, "O004", completeCutFirst: true);
        Assert.IsTrue(game.StartManualBlow(late));
        game.TickManualBlow(late, 6f);
        Assert.AreEqual(BlowResult.Minor, game.EndManualBlowHold(late));
        Assert.AreEqual(AccidentSeverity.Minor, late.AccidentSeverity);
        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);

        CustomerModel moderate = SpawnServing(game, 12, 1, "O004", completeCutFirst: true);
        Assert.IsTrue(game.StartManualBlow(moderate));
        game.TickManualBlow(moderate, 8f);
        Assert.AreEqual(BlowResult.Moderate, game.EndManualBlowHold(moderate));
        Assert.AreEqual(AccidentSeverity.Moderate, moderate.AccidentSeverity);
        Assert.AreEqual(CustomerState.Finished, moderate.State);
        Assert.AreEqual(CustomerServiceResult.NormalCompletion, moderate.ServiceResult);
    }

    [Test]
    public void WashAndBlowBackgroundWorkContinuesDuringAnotherHaircut()
    {
        var game = NewGame(new CustomerPatienceConfig { DrainPerSecond = 10f });
        CustomerModel washing = SpawnServing(game, 20, 0, "O003");
        Assert.IsTrue(game.BeginWashAction(washing, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(washing, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(washing, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(washing, game.ServiceConfig.ShampooDuration));

        CustomerModel blowing = SpawnServing(game, 21, 1, "O004", completeCutFirst: true);
        game.SetFirstDayCompleteForDebug(true);
        Assert.IsTrue(game.PurchaseAutoBlowStand());
        Assert.IsTrue(game.StartAutoBlow(blowing));

        CustomerModel waiting = game.Spawn(23, new List<ServiceType>(SalonOrderCatalog.Get("O003")));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        CustomerModel cutting = SpawnServing(game, 22, 2, "O001");
        game.ConfigureHaircutOrder(cutting, SalonTool.Scissors);
        Assert.IsTrue(game.BeginActiveOperation(cutting));
        float blowBeforeHaircut = blowing.BackgroundTask.Elapsed;
        float haircutHold = 5.5f - blowBeforeHaircut;
        game.Tick(haircutHold);

        Assert.AreEqual(WashStage.ReadyToRinse, washing.WashStage);
        Assert.AreEqual(haircutHold,
            blowing.BackgroundTask.Elapsed - blowBeforeHaircut, .001f);
        Assert.AreEqual(BlowStage.Good, blowing.BlowStage);
        Assert.AreEqual(CustomerState.Waiting, waiting.State);
        Assert.AreEqual(CustomerEmotion.Impatient, waiting.Emotion);
        Assert.IsTrue(game.IsStationOccupied(0), "Ready wash customer must keep blocking the wash station.");
    }

    [Test]
    public void ReturningForAnotherStationUsesServiceDelayNotOldQueuePatience()
    {
        var game = NewGame(new CustomerPatienceConfig { DrainPerSecond = 100f });
        CustomerModel customer = SpawnServing(game, 30, 0, "O003");
        CompleteWash(game, customer);
        float patienceAfterService = customer.Patience;

        game.Tick(10f);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(0, customer.Station);
        Assert.IsTrue(game.IsStationOccupied(0));
        CollectionAssert.DoesNotContain(game.WaitingQueue, customer);
        Assert.Less(customer.Patience, patienceAfterService,
            "Between-step delay resumes patience pressure at the configured slower rate.");
        Assert.GreaterOrEqual(customer.ServiceDelaySeconds, 10f);
    }

    [Test]
    public void BusinessDurationIsConfigurable()
    {
        var game = new SalonGameModel(flowConfig: new SalonFlowConfig { BusinessDuration = 240f });
        game.Tick(180f);
        Assert.IsFalse(game.IsOver);
        Assert.AreEqual(60f, game.RemainingTime, .001f);
    }

    [Test]
    public void FullThreeStepOrderCreatesExactlyOnePayment()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 40, 0, "O005");
        CompleteWash(game, customer);
        Assert.AreEqual(0, game.Payments.Drops.Count);
        AssignAndReach(game, customer, 1);
        RemoveTowel(game, customer);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(0, game.Payments.Drops.Count);
        Assert.IsTrue(game.StartManualBlow(customer));
        game.TickManualBlow(customer, 3.5f);
        Assert.AreEqual(BlowResult.Good, game.EndManualBlowHold(customer));

        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, game.Payments.Drops.Count);
        Assert.Greater(game.Payments.Drops[0].BaseReward, game.Payments.Config.PerfectBaseReward,
            "A complete wash-cut-blow order should price the whole order, not only its cut step.");
        Assert.AreEqual(1, game.Served);
    }

    private static SalonGameModel NewGame(CustomerPatienceConfig patience = null)
    {
        return new SalonGameModel(
            patienceConfig: patience,
            flowConfig: new SalonFlowConfig { MaxCustomers = 12, WaitingCapacity = 12 });
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, int station, string orderId,
        bool completeWashFirst = false, bool completeCutFirst = false)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType>(SalonOrderCatalog.Get(orderId)));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        if (completeWashFirst)
        {
            Assert.IsTrue(game.Assign(customer, 0));
            game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
            CompleteWash(game, customer);
        }
        if (customer.Station != station) AssignAndReach(game, customer, station);
        if (completeCutFirst)
        {
            game.ConfigureHaircutOrder(customer, SalonTool.Scissors);
            Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));
        }
        return customer;
    }

    private static CustomerModel CompleteWashAndAssignStyling(SalonGameModel game, int id, string orderId)
    {
        CustomerModel customer = SpawnServing(game, id, 0, orderId);
        CompleteWash(game, customer);
        AssignAndReach(game, customer, 1);
        return customer;
    }

    private static void CompleteWash(SalonGameModel game, CustomerModel customer)
    {
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
    }

    private static void RemoveTowel(SalonGameModel game, CustomerModel customer)
    {
        if (!customer.TowelWrapped) return;
        Assert.IsTrue(game.BeginTowelRemoval(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RemoveTowelDuration));
    }

    private static void AssignAndReach(SalonGameModel game, CustomerModel customer, int station)
    {
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
    }
}
