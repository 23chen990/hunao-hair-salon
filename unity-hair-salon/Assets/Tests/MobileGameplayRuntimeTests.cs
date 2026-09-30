using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.AssetPipeline;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MobileGameplayRuntimeTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;
    private BusinessDayController _day;
    private SalonMobileControls _controls;
    private EventSystem _events;
    private GameObject _button;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Mobile gameplay regression");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        _day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        _day.PrepareDay(1);
        _day.StartBusiness();
        _game.CustomerChanged += _day.Stats.RecordCustomerSnapshot;
        Set("_game", _game);
        Set("_dayController", _day);
        Set("_mobileMode", true);
        Set("_simple2DMode", true);
        Set("_player", Child("Player").transform);
        Set("_camera", Child("Camera").AddComponent<Camera>());
        Set("_mobileFloor", Rect.MinMaxRect(-10f, -10f, 10f, 10f));
        var canvas = Child("Canvas", typeof(RectTransform), typeof(Canvas));
        _controls = SalonMobileControls.Create(canvas.transform);
        Set("_mobileControls", _controls);
        var track = Child("Progress track", typeof(RectTransform));
        var fill = Child("Progress", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        Set("_mobileProgressFill", fill.GetComponent<Image>());
        _events = Child("Events").AddComponent<EventSystem>();
        _button = _controls.transform.Find("MobileInteractionButton").gameObject;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void ReleasingEarlyKeepsTheOrderAndAllowsASecondAttempt()
    {
        CustomerModel customer = Seated(1, 1, ServiceType.Cut);
        Hold(.15f);
        Release();
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(0, _game.Payments.Drops.Count);
        Assert.IsFalse(_game.PlayerBusy);

        Hold(1.8f);
        Assert.AreEqual(0, _game.Payments.Drops.Count, "Holding does not auto-complete a haircut.");
        Release();
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, _game.Payments.Drops.Count);
        Assert.AreEqual(1, _day.Stats.CompletedOrders);
    }

    [Test]
    public void HoldingPastTheWindowFailsWithoutCountingTowardTheDailyGoal()
    {
        CustomerModel customer = Seated(1, 1, ServiceType.Cut);
        Hold(2.6f);
        Release();
        Assert.AreEqual(CustomerServiceResult.Failed, customer.ServiceResult);
        Assert.AreEqual(0, _game.Payments.Drops.Count);
        Assert.AreEqual(0, _day.Stats.CompletedOrders, "A failed haircut cannot win the day.");
        Assert.IsFalse(_game.PlayerBusy);
    }

    [Test]
    public void TwoToolOrderRequiresTwoSeparatePressesAndPaysOnce()
    {
        CustomerModel customer = Seated(2, 1, ServiceType.Cut);
        _game.ConfigureHaircutOrder(customer, SalonTool.Scissors, SalonTool.ThinningShears);
        Hold(1.8f);
        Release();
        Assert.AreEqual(SalonTool.ThinningShears, customer.HaircutService.CurrentRequiredTool);
        Assert.AreEqual(0, _game.Payments.Drops.Count);
        Hold(1.7f);
        Release();
        Invoke("UpdateMobilePlay", .1f);
        Assert.AreEqual(1, _game.Payments.Drops.Count);
        Assert.AreEqual(1, _day.Stats.CompletedOrders);
    }

    [Test]
    public void UnbuiltStandUsesHeldManualDryingOnTheMobileActionButton()
    {
        CustomerModel customer = _game.Spawn(44, new[] { ServiceType.Wash, ServiceType.Dry });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(customer, 0));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(_game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        _game.Tick(_game.ServiceConfig.WashServiceDuration + .01f);
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.IsTrue(_game.Assign(customer, 1));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        var view = Child("Dry customer").AddComponent<SalonCustomerView>();
        view.Customer = customer;
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination").SetValue(view, true);
        ((List<SalonCustomerView>)Field("_customerViews")).Add(view);
        Transform anchor = Child("Dry anchor").transform;
        ((Dictionary<int, Transform>)Field("_playerServiceAnchors"))[1] = anchor;
        Assert.IsFalse(_game.HasAutoBlowStand);

        object target = Invoke("FindMobileTarget");
        Assert.AreEqual("StartDry", target.GetType().GetField("Action").GetValue(target).ToString());
        Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target));
        Invoke("UpdateMobilePlay", 0f);
        Pointer(true);
        Invoke("UpdateMobilePlay", 0f);
        Assert.IsTrue(customer.ManualBlowHolding,
            "The first button press should start the handheld dryer.");
        Assert.IsTrue(_controls.InteractionHeld, "The same pointer should remain held for manual drying.");
        Invoke("UpdateMobilePlay", _game.ServiceConfig.ManualBlowGoodStart + .1f);
        Assert.IsTrue(customer.ManualBlowHolding);
        Assert.IsFalse(customer.AutoBlowRunning);
        Assert.AreEqual(CustomerState.Serving, customer.State);

        Release();
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(BlowResult.Good, customer.LastBlowResult);
        Assert.AreEqual(1, _game.Payments.Drops.Count);
    }

    [Test]
    public void WashBedCanBeSelectedFromItsFrontAndRightApproaches()
    {
        var station = Child("Wash station footprint");
        var obstacle = Child("Wash collision").AddComponent<SalonFurnitureObstacle>();
        obstacle.transform.SetParent(station.transform, false);
        obstacle.transform.position = SalonDemo.PrimaryWashBedPosition;
        obstacle.Initialize(AssetManifestLoader.LoadFromResources().Find("furniture-wash-station"));
        ((Dictionary<int, GameObject>)Field("_stationRoots"))[0] = station;
        Transform serviceAnchor = Child("Wash service anchor").transform;
        serviceAnchor.position = SalonDemo.WashPlayerAnchorPosition;
        ((Dictionary<int, Transform>)Field("_playerServiceAnchors"))[0] = serviceAnchor;

        CustomerModel customer = _game.Spawn(45, new[] { ServiceType.Wash });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Set("_mobileGuidedCustomer", customer);
        Transform player = (Transform)Field("_player");
        foreach (Vector3 approach in new[] {
            new Vector3(-7.2f, 0f, 2.45f),
            new Vector3(-5.65f, 0f, 4.1f) })
        {
            player.position = approach;
            object target = Invoke("FindMobileTarget");
            Assert.AreEqual("Assign", target.GetType().GetField("Action").GetValue(target).ToString());
            Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target),
                "A guided customer should be assignable from the wash bed's front or right edge.");
        }

        Set("_mobileGuidedCustomer", null);
        Assert.IsTrue(_game.Assign(customer, 0));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        var view = Child("Washing customer").AddComponent<SalonCustomerView>();
        view.Customer = customer;
        view.transform.position = SalonDemo.WashCustomerAnchorPosition;
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination").SetValue(view, true);
        ((List<SalonCustomerView>)Field("_customerViews")).Add(view);
        foreach (Vector3 approach in new[] {
            new Vector3(-7.2f, 0f, 2.45f),
            new Vector3(-5.65f, 0f, 4.1f) })
        {
            player.position = approach;
            object target = Invoke("FindMobileTarget");
            Assert.AreEqual("Wash", target.GetType().GetField("Action").GetValue(target).ToString());
            Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target),
                "The same reachable edges should let the stylist wash a seated customer.");
        }
        player.position = new Vector3(-7.2f, 0f, 1.35f);
        object farTarget = Invoke("FindMobileTarget");
        Assert.IsFalse((bool)farTarget.GetType().GetField("Available").GetValue(farTarget),
            "A point well outside the wash footprint must remain out of range.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PausingAHaircutRequiresFreshInputAndDoesNotResolveAPhantomRelease(bool backgrounded)
    {
        CustomerModel customer = Seated(1, 1, ServiceType.Cut);
        Hold(.8f);
        float satisfaction = customer.Satisfaction;
        if (backgrounded) Invoke("OnApplicationPause", true);
        else Invoke("TogglePause");
        Assert.IsTrue(_day.IsPaused);
        Assert.IsFalse(_controls.InteractionHeld);
        Invoke("TogglePause");
        Invoke("UpdateMobilePlay", .1f);
        Assert.AreEqual(satisfaction, customer.Satisfaction,
            "Opening a menu must not become an early-release mistake after resuming.");
        Assert.AreEqual(0, _game.GetServiceActionHistory(customer).Count);
        Pointer(true);
        Invoke("UpdateMobilePlay", .9f);
        Release();
        Assert.AreEqual(1, _game.Payments.Drops.Count);
    }

    [Test]
    public void PauseButtonPressWinsOverSimultaneousHaircutReleaseAndDoesNotToggleTwice()
    {
        CustomerModel customer = Seated(1, 1, ServiceType.Cut);
        Hold(.8f);
        float satisfaction = customer.Satisfaction;
        Button pause = Child("Settings", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        pause.onClick.AddListener((UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), _demo, "TogglePause"));
        Invoke("BindMobilePauseButton", pause);

        ExecuteEvents.Execute(pause.gameObject, new PointerEventData(_events) { pointerId = 8 },
            ExecuteEvents.pointerDownHandler);
        Pointer(false);
        pause.onClick.Invoke();
        Assert.IsTrue(_day.IsPaused, "The later click must not undo the pause from pointer-down.");
        Invoke("TogglePause");
        Invoke("UpdateMobilePlay", .1f);
        Assert.AreEqual(satisfaction, customer.Satisfaction);
        Assert.AreEqual(0, _game.GetServiceActionHistory(customer).Count);
        Pointer(true);
        Invoke("UpdateMobilePlay", .9f);
        Release();
        Assert.AreEqual(1, _day.Stats.CompletedOrders);
    }

    [Test]
    public void GuidingAnotherCustomerDoesNotBlockFinishingAnOccupiedStation()
    {
        CustomerModel drying = Seated(1, 1, ServiceType.Dry);
        Seated(2, 2, ServiceType.Cut, new Vector3(5f, 0f, 0f));
        _game.InstallAutoBlowStand();
        Assert.IsTrue(_game.StartAutoBlow(drying));
        _game.Tick(drying.BackgroundTask.IdealStart + .1f);
        CustomerModel waiting = _game.Spawn(3, new[] { ServiceType.Cut });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Set("_mobileGuidedCustomer", waiting);

        object target = Invoke("FindMobileTarget");
        Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target),
            "The player must be able to free a chair while guiding another customer.");
        Assert.AreSame(drying, target.GetType().GetField("Customer").GetValue(target));
        Invoke("ExecuteMobileTarget", target);
        Assert.AreEqual(1, _game.Payments.Drops.Count);
        Assert.AreSame(waiting, Field("_mobileGuidedCustomer"));
    }

    [Test]
    public void ReadyServiceWinsOverAGuidedAssignmentWhenItIsCloser()
    {
        CustomerModel cutting = Seated(2, 1, ServiceType.Cut, new Vector3(.2f, 0f, 0f));
        CustomerModel waiting = _game.Spawn(3, new[] { ServiceType.Cut });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Set("_mobileGuidedCustomer", waiting);
        Transform guidedAnchor = Child("Guided haircut anchor").transform;
        guidedAnchor.position = new Vector3(1.2f, 0f, 0f);
        ((Dictionary<int, Transform>)Field("_playerServiceAnchors"))[2] = guidedAnchor;
        Set("_player", Child("Player position").transform);

        object target = Invoke("FindMobileTarget");
        Assert.AreSame(cutting, target.GetType().GetField("Customer").GetValue(target),
            "A ready occupied station must remain playable while a greeted customer is being escorted.");
        Assert.AreEqual("Cut", target.GetType().GetField("Action").GetValue(target).ToString());
    }

    [Test]
    public void WrongStationCustomerCanLeaveWhenEveryCompatibleStationIsOccupied()
    {
        CustomerModel transfer = Seated(1, 0, ServiceType.Dry);
        Seated(2, 1, ServiceType.Cut, new Vector3(2f, 0f, 0f));
        Seated(3, 2, ServiceType.Cut, new Vector3(4f, 0f, 0f));
        Set("_player", Child("Transfer player").transform);

        object target = Invoke("FindMobileTarget");
        Assert.AreSame(transfer, target.GetType().GetField("Customer").GetValue(target));
        Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target));
        Assert.AreEqual("Recall", target.GetType().GetField("Action").GetValue(target).ToString());
        Assert.AreEqual("请离工位", target.GetType().GetField("Label").GetValue(target));
        int wrongStations = transfer.WrongStationCount;
        Invoke("ExecuteMobileTarget", target);
        Assert.AreEqual(CustomerState.Waiting, transfer.State);
        Assert.AreEqual(-1, transfer.Station);
        Assert.IsFalse(_game.IsStationOccupied(0));
        Assert.AreEqual(wrongStations, transfer.WrongStationCount,
            "Standing up must not charge the wrong-station penalty again.");
    }

    [Test]
    public void CrossedStationsCanBeClearedAndSwappedWithoutAnotherPenalty()
    {
        Assert.IsTrue(_game.SetWorkstationAvailability(2, false));
        Assert.IsTrue(_game.SetWorkstationAvailability(3, false));
        Assert.IsTrue(_game.SetWorkstationAvailability(4, false));
        CustomerModel washOnHaircut = Seated(1, 1, ServiceType.Wash, new Vector3(2f, 0f, 0f));
        CustomerModel cutOnWash = Seated(2, 0, ServiceType.Cut);
        Assert.AreEqual(1, washOnHaircut.WrongStationCount);
        Assert.AreEqual(1, cutOnWash.WrongStationCount);
        Assert.IsFalse(_game.Assign(washOnHaircut, 0));
        Assert.IsFalse(_game.Assign(cutOnWash, 1));

        Transform player = (Transform)Field("_player");
        player.position = Vector3.zero;
        object recall = Invoke("FindMobileTarget");
        Assert.AreSame(cutOnWash, recall.GetType().GetField("Customer").GetValue(recall));
        Assert.AreEqual("请离工位", recall.GetType().GetField("Label").GetValue(recall));
        Invoke("ExecuteMobileTarget", recall);
        Assert.AreEqual(CustomerState.Waiting, cutOnWash.State);
        Assert.IsFalse(_game.IsStationOccupied(0));
        Assert.AreEqual(1, cutOnWash.WrongStationCount);

        player.position = new Vector3(2f, 0f, 0f);
        object transfer = Invoke("FindMobileTarget");
        Assert.AreSame(washOnHaircut, transfer.GetType().GetField("Customer").GetValue(transfer));
        Assert.AreEqual("转移顾客", transfer.GetType().GetField("Label").GetValue(transfer));
        Invoke("ExecuteMobileTarget", transfer);

        player.position = Vector3.zero;
        object seatWash = Invoke("FindMobileTarget");
        Assert.AreSame(washOnHaircut, seatWash.GetType().GetField("Customer").GetValue(seatWash));
        Assert.AreEqual("Assign", seatWash.GetType().GetField("Action").GetValue(seatWash).ToString());
        Assert.AreEqual(0, seatWash.GetType().GetField("Station").GetValue(seatWash));
        Invoke("ExecuteMobileTarget", seatWash);
        Assert.AreEqual(0, washOnHaircut.Station);
        Assert.AreEqual(1, washOnHaircut.WrongStationCount);
        Assert.IsFalse(_game.IsStationOccupied(1));

        player.position = new Vector3(2f, 0f, 0f);
        object seatCut = Invoke("FindMobileTarget");
        Assert.AreSame(cutOnWash, seatCut.GetType().GetField("Customer").GetValue(seatCut));
        Assert.AreEqual("Assign", seatCut.GetType().GetField("Action").GetValue(seatCut).ToString());
        Assert.AreEqual(1, seatCut.GetType().GetField("Station").GetValue(seatCut));
        Invoke("ExecuteMobileTarget", seatCut);
        Assert.AreEqual(1, cutOnWash.Station);
        Assert.AreEqual(1, cutOnWash.WrongStationCount);
        Assert.AreEqual(CustomerState.MovingToStation, cutOnWash.State);
    }

    [Test]
    public void RinsedCustomerCanLeaveTheWashBedWhenTheNextChairIsTaken()
    {
        Assert.IsTrue(_game.SetWorkstationAvailability(2, false));
        Assert.IsTrue(_game.SetWorkstationAvailability(4, false));
        Seated(2, 1, ServiceType.Cut, new Vector3(3f, 0f, 0f));
        CustomerModel washed = _game.Spawn(8, new[] { ServiceType.Wash, ServiceType.Dry });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(washed, 0));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        var view = Child("Rinsed customer").AddComponent<SalonCustomerView>();
        view.Customer = washed;
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination").SetValue(view, true);
        ((List<SalonCustomerView>)Field("_customerViews")).Add(view);
        ((Transform)Field("_player")).position = Vector3.zero;

        Assert.IsTrue(_game.BeginWashFoamHold(washed));
        _game.Tick(_game.ServiceConfig.ShampooDuration + .01f);
        Assert.IsFalse(_game.CanRecallFromStation(washed),
            "A customer still working up foam cannot be pulled off the bed.");
        _game.Tick(_game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(_game.FinishWashRinse(washed));
        Assert.AreEqual(ServiceType.Dry, washed.CurrentNeed);
        Assert.IsTrue(_game.CanRecallFromStation(washed),
            "After the rinse, waiting for a busy blow-dry chair must not glue them to the wash bed.");

        object target = Invoke("FindMobileTarget");
        Assert.AreSame(washed, target.GetType().GetField("Customer").GetValue(target));
        Assert.AreEqual("请离工位", target.GetType().GetField("Label").GetValue(target));
        Invoke("ExecuteMobileTarget", target);
        Assert.AreEqual(CustomerState.Waiting, washed.State);
        Assert.IsFalse(_game.IsStationOccupied(0));
        Assert.AreEqual(0, washed.WrongStationCount);
    }

    [Test]
    public void ActiveHaircutCannotBeRemovedFromItsStation()
    {
        CustomerModel cutting = Seated(1, 1, ServiceType.Cut);
        Assert.IsTrue(_game.BeginHaircutAction(cutting, SalonTool.Scissors, _demo.HaircutSettings));
        Assert.IsFalse(_game.CanRecallFromStation(cutting));
        Assert.IsFalse(_game.RecallFromStation(cutting));
        Assert.AreEqual(1, cutting.Station);
        Assert.AreEqual(CustomerState.Serving, cutting.State);
    }

    [Test]
    public void FinishedCustomerRemainsActiveUntilItExitsTheShop()
    {
        CustomerModel customer = _game.Spawn(4, new[] { ServiceType.Cut });
        customer.State = CustomerState.Finished;

        int activeUntilExit = (int)Invoke("CountActiveCustomers");

        Assert.AreEqual(1, activeUntilExit,
            "A finished customer still needs time to walk out before day settlement.");
        customer.State = CustomerState.Exited;
        Assert.AreEqual(0, (int)Invoke("CountActiveCustomers"));
    }

    [Test]
    public void NearbyFoamWaitDoesNotHideAReadyHaircut()
    {
        CustomerModel washing = Seated(1, 0, ServiceType.Wash);
        CustomerModel cutting = Seated(2, 1, ServiceType.Cut, new Vector3(1f, 0f, 0f));
        Assert.IsTrue(_game.BeginWashFoamHold(washing));
        _game.Tick(_game.ServiceConfig.ShampooDuration + .01f);
        object target = Invoke("FindMobileTarget");
        Assert.AreSame(cutting, target.GetType().GetField("Customer").GetValue(target));
        Assert.IsTrue((bool)target.GetType().GetField("Available").GetValue(target));
    }

    [Test]
    public void CompletedOrderOnlySettlesWhenCashierIsAttended()
    {
        int balance = _game.Balance;
        PaymentDropModel drop = _game.Payments.CreateFinalPayment(77, 1, 300, 60);
        Invoke("HandlePaymentCreated", drop);
        Assert.AreEqual(PaymentDropState.Pending, drop.State);
        Assert.AreEqual(balance, _game.Balance);
        var checkout = (SalonCheckoutModel)Field("_mobileCheckout");
        checkout.MarkArrived(77);
        checkout.Tick(.4f, true);
        Assert.AreEqual(PaymentDropState.Collected, drop.State);
        Assert.AreEqual(balance + 360, _game.Balance);
        Assert.AreEqual(300, _day.Stats.OrderIncome);
        Assert.AreEqual(60, _day.Stats.TipIncome);
    }

    [Test]
    public void WashThenDryOrderKeepsTheWholeOrderAliveUntilDryFinishes()
    {
        CustomerModel customer = _game.Spawn(88, new[] { ServiceType.Wash, ServiceType.Dry });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(customer, 0));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(_game.BeginWashFoamHold(customer));
        _game.Tick(_game.ServiceConfig.ShampooDuration + _game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(_game.FinishWashRinse(customer));
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed,
            "Finishing wash must advance the same customer to the dry step.");
        Assert.AreEqual(0, _game.Payments.Drops.Count,
            "A multi-step order must not pay after the first service.");

        Assert.IsTrue(_game.Assign(customer, 1));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        _game.InstallAutoBlowStand();
        Assert.IsTrue(_game.StartAutoBlow(customer));
        _game.Tick(customer.BackgroundTask.IdealStart + .01f);
        Assert.AreEqual(BlowResult.Good, _game.FinishAutoBlow(customer));
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, _game.Payments.Drops.Count,
            "The final dry step must create exactly one payment for the whole order.");
    }


    private CustomerModel Seated(int id, int station, ServiceType need, Vector3 position = default)
    {
        var customer = _game.Spawn(id, new[] { need });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(customer, station));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        var view = Child("Customer " + id).AddComponent<SalonCustomerView>();
        view.Customer = customer;
        view.transform.position = position;
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination").SetValue(view, true);
        ((List<SalonCustomerView>)Field("_customerViews")).Add(view);
        Transform anchor = Child("Anchor " + station).transform;
        anchor.position = position;
        ((Dictionary<int, Transform>)Field("_playerServiceAnchors"))[station] = anchor;
        return customer;
    }

    private void Hold(float seconds)
    {
        Invoke("UpdateMobilePlay", 0f);
        Pointer(true);
        Invoke("UpdateMobilePlay", 0f);
        Invoke("UpdateMobilePlay", seconds);
    }

    private void Release()
    {
        Pointer(false);
        Invoke("UpdateMobilePlay", 0f);
    }

    private void Pointer(bool down)
    {
        var data = new PointerEventData(_events) { pointerId = 7, position = Vector2.zero };
        if (down) ExecuteEvents.Execute(_button, data, ExecuteEvents.pointerDownHandler);
        else ExecuteEvents.Execute(_button, data, ExecuteEvents.pointerUpHandler);
    }

    private GameObject Child(string name, params System.Type[] components)
    {
        var child = new GameObject(name, components);
        child.transform.SetParent(_root.transform, false);
        return child;
    }

    private object Field(string name) => typeof(SalonDemo).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
    private void Set(string name, object value) => typeof(SalonDemo).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);
    private object Invoke(string name, params object[] args) => typeof(SalonDemo).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);
}
