using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DyePermServiceTests
{
    [Test]
    public void DyeWaitsForFocusedPlayerCleanupAfterColorProcessingFinishes()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel dye = SpawnServing(game, 210, ServiceType.Dye, 1);
        StartProcessing(game, dye, config.DyeApplyDuration);

        game.Tick(config.DyeProcessingDuration);

        Assert.AreEqual("DyeReadyForCleanup", dye.ProcessStage.ToString());
        Assert.IsFalse(dye.IsComplete, "Color development alone must not complete the dye requirement.");
        Assert.IsTrue(game.Workstations[1].IsOccupied);
        MethodInfo cleanup = typeof(SalonGameModel).GetMethod("ResolveDyeCleanup");
        Assert.IsNotNull(cleanup);
        Assert.IsFalse((bool)cleanup.Invoke(game, new object[] { dye }),
            "The player must return focus to this customer before handling the dye paste.");

        game.SelectCustomer(dye);
        Assert.IsTrue((bool)cleanup.Invoke(game, new object[] { dye }));
        Assert.IsTrue(dye.IsComplete);
        Assert.AreEqual(ServiceProcessStage.Complete, dye.ProcessStage);
    }

    [Test]
    public void UnhandledDyePasteFailsAfterGraceWindow()
    {
        var config = FastConfig();
        var graceField = typeof(SalonServiceConfig).GetField("DyeCleanupGraceDuration");
        Assert.IsNotNull(graceField, "Dye cleanup needs a centralized grace duration.");
        graceField.SetValue(config, 1f);
        var game = NewGame(config);
        CustomerModel dye = SpawnServing(game, 211, ServiceType.Dye, 1);
        StartProcessing(game, dye, config.DyeApplyDuration);

        game.Tick(config.DyeProcessingDuration + 1.01f);

        Assert.AreEqual("DyeFailed", dye.ProcessStage.ToString());
        Assert.AreEqual(CustomerServiceResult.Failed, dye.ServiceResult);
        Assert.AreEqual(CustomerServiceFeedback.Dissatisfied, dye.ServiceFeedback);
        Assert.IsFalse(dye.IsComplete);
    }

    [Test]
    public void DyeCustomerAssignedToEitherHairStationDoesNotReceiveWrongStationConfusion()
    {
        var game = NewGame();
        CustomerModel first = game.Spawn(201, new List<ServiceType> { ServiceType.Dye });
        CustomerModel second = game.Spawn(202, new List<ServiceType> { ServiceType.Dye });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.Assign(first, 1));
        Assert.IsTrue(game.Assign(second, 2));

        Assert.AreEqual(0, first.WrongStationCount);
        Assert.AreEqual(0, second.WrongStationCount);
        Assert.AreEqual(CustomerReactionKind.None, first.ReactionKind);
        Assert.AreEqual(CustomerReactionKind.None, second.ReactionKind);
    }

    [Test]
    public void CorrectHairStationAssignmentClearsAStaleWrongStationQuestionMark()
    {
        var game = NewGame();
        CustomerModel dye = game.Spawn(203, new List<ServiceType> { ServiceType.Dye });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        dye.ReactionKind = CustomerReactionKind.Confused;
        dye.ReactionRemaining = 1.2f;

        Assert.IsTrue(game.Assign(dye, 1));

        Assert.AreEqual(CustomerReactionKind.None, dye.ReactionKind,
            "A correct Dye → HairStation move must never leave a wrong-station question mark overhead.");
        Assert.AreEqual(0f, dye.ReactionRemaining, .001f);
        Assert.AreEqual(0, dye.WrongStationCount);
    }

    [Test]
    public void CorrectHairStationAlwaysClearsWrongStationConfusionForADyeCustomer()
    {
        var game = NewGame();
        CustomerModel dye = game.Spawn(204, new List<ServiceType> { ServiceType.Dye });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        dye.ReactionKind = CustomerReactionKind.Confused;
        dye.ReactionRemaining = 1.2f;
        dye.WrongServiceKind = CustomerWrongServiceKind.Haircut;

        Assert.IsTrue(game.Assign(dye, 2));

        Assert.AreEqual(CustomerReactionKind.None, dye.ReactionKind,
            "Dye is compatible with either HairStation, so no wrong-station question mark may survive assignment.");
        Assert.AreEqual(0f, dye.ReactionRemaining, .001f);
        Assert.AreEqual(0, dye.WrongStationCount);
    }

    [Test]
    public void DyeSharesHairStationWhilePermUsesDedicatedPermStation()
    {
        var game = NewGame();

        Assert.AreEqual(WorkstationType.Haircut, SalonGameModel.RequiredStationFor(ServiceType.Dye));
        Assert.AreEqual(WorkstationType.Perm, SalonGameModel.RequiredStationFor(ServiceType.Perm));
        Assert.IsTrue(SalonGameModel.IsCompatibleStation(ServiceType.Dye, WorkstationType.Haircut));
        Assert.IsFalse(SalonGameModel.IsCompatibleStation(ServiceType.Dye, WorkstationType.Perm));
        Assert.IsFalse(SalonGameModel.IsCompatibleStation(ServiceType.Perm, WorkstationType.Haircut));
        CollectionAssert.DoesNotContain(Enum.GetNames(typeof(WorkstationType)), "Dye");

        Assert.GreaterOrEqual(game.Workstations.Count(item => item.Type == WorkstationType.Haircut), 2);
        Assert.GreaterOrEqual(game.Workstations.Count(item => item.Type == WorkstationType.Wash), 1);
        Assert.GreaterOrEqual(game.Workstations.Count(item => item.Type == WorkstationType.Perm), 1);
    }

    [Test]
    public void WorkstationCarriesOccupancyServiceAndInteractionDataIndependentlyOfPlayer()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 1, ServiceType.Dye, 1);
        WorkstationModel station = game.Workstations[1];

        Assert.AreEqual(station.Id, station.StationId);
        Assert.AreEqual(station.Type, station.StationType);
        Assert.AreEqual(customer.Id, station.OccupiedCustomerId);
        Assert.AreEqual(ServiceType.Dye, station.CurrentService);
        Assert.IsTrue(station.IsOccupied);
        Assert.IsNotNull(station.InteractionPoint);
        Assert.IsFalse(game.PlayerBusy);
    }

    [Test]
    public void DyeApplyReleasesPlayerButKeepsHairStationOccupiedDuringProcessing()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel dye = SpawnServing(game, 2, ServiceType.Dye, 1);

        Assert.IsTrue(game.BeginProcessingServiceApply(dye));
        Assert.AreEqual(ServiceProcessStage.DyeApply, dye.ProcessStage);
        Assert.IsTrue(game.PlayerBusy);
        Assert.IsTrue(game.TickActiveServiceAction(dye, config.DyeApplyDuration));

        Assert.AreEqual(ServiceProcessStage.DyeProcessing, dye.ProcessStage);
        Assert.IsFalse(game.PlayerBusy);
        Assert.IsTrue(game.Workstations[1].IsOccupied);
        Assert.AreEqual(dye.Id, game.Workstations[1].OccupiedCustomerId);

        float patience = dye.Patience;
        float satisfaction = dye.Satisfaction;
        game.Tick(config.DyeProcessingDuration * .5f);

        Assert.That(dye.ProcessingProgress, Is.InRange(.49f, .51f));
        Assert.AreEqual(patience, dye.Patience, .001f);
        Assert.AreEqual(satisfaction, dye.Satisfaction, .001f);
        Assert.IsTrue(game.Workstations[1].IsOccupied);
    }

    [Test]
    public void SecondDyeCustomerCanStartImmediatelyAfterFirstCustomerEntersProcessing()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel first = SpawnServing(game, 205, ServiceType.Dye, 1);
        CustomerModel second = SpawnServing(game, 206, ServiceType.Dye, 2);

        game.SelectCustomer(first);
        Assert.IsTrue(game.BeginProcessingServiceApply(first));
        Assert.IsTrue(game.TickActiveServiceAction(first, config.DyeApplyDuration));
        Assert.AreEqual(ServiceProcessStage.DyeProcessing, first.ProcessStage);
        Assert.IsFalse(game.PlayerBusy);

        game.SelectCustomer(second);
        Assert.IsTrue(game.BeginProcessingServiceApply(second),
            "The first HairStation stays occupied, but the released player must be able to apply dye at the second HairStation.");
        Assert.AreEqual(ActiveServiceAction.ApplyDye, second.ActiveServiceAction);
    }

    [Test]
    public void ReachedHairStationAcceptsDyeHoldEvenIfTimedMovementStateHasNotCaughtUp()
    {
        var game = NewGame();
        CustomerModel dye = SpawnWaiting(game, 207, ServiceType.Dye);
        Assert.IsTrue(game.Assign(dye, 1));
        Assert.AreEqual(CustomerState.MovingToStation, dye.State);
        dye.ReactionKind = CustomerReactionKind.Confused;
        dye.ReactionRemaining = 1.2f;
        game.SelectCustomer(dye);
        game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId)
            .SelectServiceAction(dye, ActiveServiceAction.ApplyDye, SalonTool.DyeBottle);

        var demoObject = new GameObject("Dye hold integration demo");
        var demo = demoObject.AddComponent<SalonDemo>();
        typeof(SalonDemo).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(demo, game);
        typeof(SalonDemo).GetField("_haircutInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(demo, new HaircutInteraction(new HaircutConfig()));
        typeof(SalonDemo).GetField("_selectedServiceAction", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(demo, ActiveServiceAction.ApplyDye);

        var customerObject = new GameObject("Reached dye customer");
        var view = customerObject.AddComponent<SalonCustomerView>();
        view.Owner = demo;
        view.Customer = dye;
        Vector3 reachedPosition = customerObject.transform.position;
        for (int i = 0; i < 8 && !view.IsAtMovementDestination; i++)
            view.MoveAlongCurrentRoute(CustomerState.MovingToStation,
                reachedPosition, 100f, 1f);
        Assert.IsTrue(view.IsAtMovementDestination);

        Assert.IsTrue(demo.HandleHaircutPointerDown(view, 41),
            "Once the customer is visibly at HairStation, holding must start dye application instead of silently failing.");
        Assert.AreEqual(CustomerState.Serving, dye.State);
        Assert.AreEqual(ActiveServiceAction.ApplyDye, dye.ActiveServiceAction);
        Assert.AreEqual(CustomerReactionKind.None, dye.ReactionKind,
            "Dye at HairStation must never retain a wrong-station question mark.");

        UnityEngine.Object.DestroyImmediate(customerObject);
        UnityEngine.Object.DestroyImmediate(demoObject);
    }

    [Test]
    public void SecondDyeCustomerCanRebindTheArmedBrushAfterFirstCustomerStartsProcessing()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel first = SpawnServing(game, 208, ServiceType.Dye, 1);
        CustomerModel second = SpawnServing(game, 209, ServiceType.Dye, 2);
        game.SelectCustomer(first);
        StartProcessing(game, first, config.DyeApplyDuration);
        Assert.IsFalse(game.PlayerBusy);

        game.SelectCustomer(second);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        context.SelectServiceAction(second, ActiveServiceAction.ApplyDye, SalonTool.DyeBottle);

        var demoObject = new GameObject("Second dye hold integration demo");
        var demo = demoObject.AddComponent<SalonDemo>();
        SetPrivate(demo, "_game", game);
        SetPrivate(demo, "_haircutInteraction", new HaircutInteraction(new HaircutConfig()));
        SetPrivate(demo, "_selectedServiceAction", ActiveServiceAction.ApplyDye);
        SetPrivate(demo, "_selectedToolIndex", 5);

        // Arrival/view refreshes can invalidate the per-player context after the toolbar has
        // armed the brush. The visible armed tool must remain authoritative for this hold.
        context.ClearInteractionContext();

        var customerObject = new GameObject("Second reached dye customer");
        var view = customerObject.AddComponent<SalonCustomerView>();
        view.Owner = demo;
        view.Customer = second;
        for (int i = 0; i < 8 && !view.IsAtMovementDestination; i++)
            view.MoveAlongCurrentRoute(CustomerState.Serving,
                customerObject.transform.position, 100f, 1f);
        Assert.IsTrue(view.IsAtMovementDestination);

        Assert.IsTrue(demo.HandleHaircutPointerDown(view, 42),
            "The second customer's armed dye brush must rebind on hold instead of silently becoming unclickable.");
        Assert.AreEqual(ActiveServiceAction.ApplyDye, second.ActiveServiceAction);
        Assert.IsTrue(context.OwnsSelection(second));

        UnityEngine.Object.DestroyImmediate(customerObject);
        UnityEngine.Object.DestroyImmediate(demoObject);
    }

    [Test]
    public void DyeCustomerArrivalAtEitherHairStationClearsAnyLateQuestionMark()
    {
        var game = NewGame();
        CustomerModel first = SpawnWaiting(game, 212, ServiceType.Dye);
        CustomerModel second = SpawnWaiting(game, 213, ServiceType.Dye);
        Assert.IsTrue(game.Assign(first, 1));
        Assert.IsTrue(game.Assign(second, 2));

        // Reproduce a late UI/model event writing confusion after assignment but before the
        // physical customer reaches the chair.
        first.ReactionKind = CustomerReactionKind.Confused;
        first.ReactionRemaining = 1.2f;
        second.ReactionKind = CustomerReactionKind.Confused;
        second.ReactionRemaining = 1.2f;

        Assert.IsTrue(game.ConfirmStationArrival(first));
        Assert.IsTrue(game.ConfirmStationArrival(second));

        Assert.AreEqual(CustomerReactionKind.None, first.ReactionKind);
        Assert.AreEqual(CustomerReactionKind.None, second.ReactionKind);
        Assert.AreEqual(0f, first.ReactionRemaining, .001f);
        Assert.AreEqual(0f, second.ReactionRemaining, .001f);
        Assert.AreEqual(0, first.WrongStationCount);
        Assert.AreEqual(0, second.WrongStationCount);
    }

    [Test]
    public void LatestPlayerContextActionWinsIfAStaleDyeActionSurvivesInTheToolbarState()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(214,
            new List<ServiceType> { ServiceType.Dye, ServiceType.Dry });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        customer.Step = 1;
        customer.ProcessStage = ServiceProcessStage.Complete;
        customer.BlowStage = BlowStage.AwaitingStart;
        game.Workstations[1].CurrentService = ServiceType.Dry;
        game.SelectCustomer(customer);
        game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId)
            .SelectServiceAction(customer, ActiveServiceAction.ManualBlow, SalonTool.BlowDryer);

        var demoObject = new GameObject("Blow must not reuse stale dye action");
        var demo = demoObject.AddComponent<SalonDemo>();
        SetPrivate(demo, "_game", game);
        SetPrivate(demo, "_haircutInteraction", new HaircutInteraction(new HaircutConfig()));
        SetPrivate(demo, "_selectedServiceAction", ActiveServiceAction.ApplyDye);

        var customerObject = new GameObject("Dry customer");
        var view = customerObject.AddComponent<SalonCustomerView>();
        view.Owner = demo;
        view.Customer = customer;
        for (int i = 0; i < 8 && !view.IsAtMovementDestination; i++)
            view.MoveAlongCurrentRoute(CustomerState.Serving,
                customerObject.transform.position, 100f, 1f);

        Assert.IsTrue(demo.HandleHaircutPointerDown(view, 43),
            "Clicking the blow dryer must start Dry even if an old ApplyDye UI field survived an event.");
        Assert.AreEqual(ActiveServiceAction.ManualBlow, customer.ActiveServiceAction);
        Assert.IsTrue(customer.ManualBlowHolding);
        Assert.AreEqual(1, customer.Step, "Blowing must not complete or restart the dye requirement.");

        UnityEngine.Object.DestroyImmediate(customerObject);
        UnityEngine.Object.DestroyImmediate(demoObject);
    }

    [Test]
    public void RealSecondCustomerToolbarBrushClickThenHoldStartsDyeApplyOnly()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel first = SpawnServing(game, 215, ServiceType.Dye, 1);
        CustomerModel second = SpawnServing(game, 216, ServiceType.Dye, 2);
        game.SelectCustomer(first);
        StartProcessing(game, first, config.DyeApplyDuration);
        Assert.AreEqual(ServiceProcessStage.DyeProcessing, first.ProcessStage);
        Assert.IsFalse(game.PlayerBusy);

        var demoObject = new GameObject("Exact two-customer UI sequence");
        var demo = demoObject.AddComponent<SalonDemo>();
        SetPrivate(demo, "_game", game);
        SetPrivate(demo, "_haircutInteraction", new HaircutInteraction(new HaircutConfig()));
        var toolbar = new GameObject("Workstation Tools", typeof(RectTransform));
        toolbar.transform.SetParent(demoObject.transform, false);
        var labelObject = new GameObject("Focus Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(toolbar.transform, false);
        SetPrivate(demo, "_toolBar", toolbar);
        SetPrivate(demo, "_focusLabel", labelObject.GetComponent<Text>());

        game.SelectCustomer(second);
        InvokePrivate(demo, "BuildToolBar", second);
        Transform dyeTool = toolbar.transform.Find("Tool 5");
        Assert.IsNotNull(dyeTool, "HairStation must expose the dye brush for the second dye customer.");
        Button dyeButton = dyeTool.GetComponent<Button>();
        Assert.IsNotNull(dyeButton);
        Assert.IsTrue(dyeButton.interactable);
        dyeButton.onClick.Invoke();

        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        Assert.AreEqual(ActiveServiceAction.ApplyDye, context.ActiveAction,
            "The real toolbar click must arm ApplyDye for customer B.");
        Assert.AreEqual(second.Id, context.SelectedToolOwnerCustomerId);

        var customerObject = new GameObject("Second dye customer view");
        var view = customerObject.AddComponent<SalonCustomerView>();
        view.Owner = demo;
        view.Customer = second;
        for (int i = 0; i < 8 && !view.IsAtMovementDestination; i++)
            view.MoveAlongCurrentRoute(CustomerState.Serving,
                customerObject.transform.position, 100f, 1f);

        var pointer = new PointerEventData(EventSystem.current) { pointerId = 44 };
        view.OnPointerDown(pointer);
        Assert.AreEqual(ActiveServiceAction.ApplyDye, second.ActiveServiceAction);
        Assert.AreEqual(ServiceProcessStage.DyeApply, second.ProcessStage);
        Assert.AreEqual(0, second.Step, "Applying paste must not mark dye complete.");
        Assert.IsFalse(second.IsComplete);

        // A real mouse/touch hold commonly crosses Unity's tiny drag threshold. That must
        // remain the same hold, not cancel dye and turn into a customer-move gesture.
        view.OnBeginDrag(pointer);
        Assert.AreEqual(ActiveServiceAction.ApplyDye, second.ActiveServiceAction,
            "Pointer drift during a successful hold must not cancel the second customer's dye action.");

        Assert.IsTrue(game.TickActiveServiceAction(second, config.DyeApplyDuration));
        Assert.AreEqual(ServiceProcessStage.DyeProcessing, second.ProcessStage,
            "Finishing the hold must enter color processing, not complete dye.");
        Assert.AreEqual(0, second.Step);
        Assert.IsFalse(second.IsComplete);
        Assert.IsTrue(game.Workstations[2].IsOccupied);
        Assert.IsFalse(game.PlayerBusy);

        UnityEngine.Object.DestroyImmediate(customerObject);
        UnityEngine.Object.DestroyImmediate(toolbar);
        UnityEngine.Object.DestroyImmediate(demoObject);
    }

    [Test]
    public void DyeAndPermProcessingAdvanceTogetherWhilePlayerServesAThirdCustomer()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel dye = SpawnServing(game, 3, ServiceType.Dye, 1);
        CustomerModel perm = SpawnServing(game, 4, ServiceType.Perm, 3);
        CustomerModel cut = SpawnServing(game, 5, ServiceType.Cut, 2);

        StartProcessing(game, dye, config.DyeApplyDuration);
        StartProcessing(game, perm, config.PermApplyDuration);
        Assert.IsTrue(game.BeginActiveOperation(cut));

        game.Tick(.5f);

        Assert.AreEqual(.5f, dye.ProcessingElapsed, .001f);
        Assert.AreEqual(.5f, perm.ProcessingElapsed, .001f);
        Assert.IsTrue(game.PlayerBusy);
        Assert.IsTrue(game.Workstations[dye.Station].IsOccupied);
        Assert.IsTrue(game.Workstations[perm.Station].IsOccupied);
    }

    [Test]
    public void ProcessingCompletesAutomaticallyAndContendingCustomersMustWaitForTheStation()
    {
        var config = FastConfig();
        var game = NewGame(config);
        CustomerModel firstPerm = SpawnServing(game, 6, ServiceType.Perm, 3);
        CustomerModel waitingPerm = SpawnWaiting(game, 7, ServiceType.Perm);
        StartProcessing(game, firstPerm, config.PermApplyDuration);

        Assert.IsFalse(game.Assign(waitingPerm, 3));
        game.Tick(config.PermProcessingDuration);

        Assert.IsTrue(firstPerm.IsComplete);
        Assert.AreEqual(ServiceProcessStage.Complete, firstPerm.ProcessStage);
        Assert.AreNotEqual(CustomerState.Serving, firstPerm.State);
    }

    [Test]
    public void ApplyCannotStartAtTheWrongStationAndDurationsAreCentralized()
    {
        var config = new SalonServiceConfig();
        Assert.AreEqual(4f, config.HaircutServiceDuration);
        Assert.AreEqual(5f, config.WashServiceDuration);
        Assert.AreEqual(3f, config.BlowDryServiceDuration);
        Assert.AreEqual(3f, config.DyeApplyDuration);
        Assert.AreEqual(8f, config.DyeProcessingDuration);
        Assert.AreEqual(4f, config.PermApplyDuration);
        Assert.AreEqual(10f, config.PermProcessingDuration);

        var game = NewGame(config);
        CustomerModel dyeAtPerm = SpawnServing(game, 8, ServiceType.Dye, 3);
        CustomerModel permAtHair = SpawnServing(game, 9, ServiceType.Perm, 1);

        Assert.IsFalse(game.BeginProcessingServiceApply(dyeAtPerm));
        Assert.IsFalse(game.BeginProcessingServiceApply(permAtHair));
        Assert.AreEqual(ServiceProcessStage.None, dyeAtPerm.ProcessStage);
        Assert.AreEqual(ServiceProcessStage.None, permAtHair.ProcessStage);
    }

    [Test]
    public void BackgroundProcessingUsesASeparateSmallStatusFromPlayerActionProgress()
    {
        var cameraObject = new GameObject("Dye Process Camera", typeof(Camera));
        var customerObject = new GameObject("Dye Process Customer");
        var customer = new CustomerModel { Id = 99, Needs = new List<ServiceType> { ServiceType.Dye } };
        var bubble = customerObject.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, cameraObject.GetComponent<Camera>());

        bubble.SetServiceProgress(.25f, Color.yellow);
        bubble.SetProcessingWaitProgress("染发中", .5f, Color.cyan);

        Assert.IsTrue(bubble.BackgroundWaitVisible);
        Assert.AreEqual("染发中", bubble.BackgroundWaitText);
        Assert.AreEqual(.5f, bubble.BackgroundWaitProgress, .001f);
        Assert.AreEqual(.25f, bubble.GetServiceProgress(0), .001f,
            "The small wait indicator must not overwrite the player's action ring.");

        UnityEngine.Object.DestroyImmediate(customerObject);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void DyeAndPermClassificationKeepsServiceRelationSeparateFromDisasterCandidate()
    {
        var game = NewGame();
        CustomerModel dye = SpawnServing(game, 100, ServiceType.Dye, 1);
        CustomerModel cut = SpawnServing(game, 101, ServiceType.Cut, 2);

        ServiceActionClassification normal = game.ClassifyProcessingServiceApply(dye, ServiceType.Dye);
        ServiceActionClassification wrong = game.ClassifyProcessingServiceApply(cut, ServiceType.Dye);

        Assert.AreEqual(ServiceRelation.NormalService, normal.Relation);
        Assert.AreEqual(ServiceRelation.WrongService, wrong.Relation);
        Assert.IsFalse(normal.IsDisasterCandidate);
        Assert.IsFalse(wrong.IsDisasterCandidate);
    }

    private static SalonGameModel NewGame(SalonServiceConfig config = null)
    {
        return new SalonGameModel(
            flowConfig: new SalonFlowConfig { MaxCustomers = 8, WaitingCapacity = 8 },
            serviceConfig: config ?? FastConfig());
    }

    private static SalonServiceConfig FastConfig()
    {
        return new SalonServiceConfig
        {
            DyeApplyDuration = 1f,
            DyeProcessingDuration = 2f,
            PermApplyDuration = 1f,
            PermProcessingDuration = 2f
        };
    }

    private static CustomerModel SpawnWaiting(SalonGameModel game, int id, ServiceType service)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { service });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);
        return customer;
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, ServiceType service, int station)
    {
        CustomerModel customer = SpawnWaiting(game, id, service);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }

    private static void StartProcessing(SalonGameModel game, CustomerModel customer, float applyDuration)
    {
        Assert.IsTrue(game.BeginProcessingServiceApply(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, applyDuration));
        Assert.IsTrue(customer.IsProcessing);
    }

    private static void SetPrivate(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        return method.Invoke(target, args);
    }
}
