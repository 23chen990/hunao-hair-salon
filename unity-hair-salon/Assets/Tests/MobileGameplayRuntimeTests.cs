using System.Collections.Generic;
using System.Reflection;
using HairSalon;
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
    public void TransferTargetIsDisabledWhenAllCompatibleStationsAreOccupied()
    {
        CustomerModel transfer = Seated(1, 0, ServiceType.Dry);
        Seated(2, 1, ServiceType.Cut, new Vector3(2f, 0f, 0f));
        Seated(3, 2, ServiceType.Cut, new Vector3(4f, 0f, 0f));
        Set("_player", Child("Transfer player").transform);

        object target = Invoke("FindMobileTarget");
        Assert.AreSame(transfer, target.GetType().GetField("Customer").GetValue(target));
        Assert.IsFalse((bool)target.GetType().GetField("Available").GetValue(target));
        Assert.AreEqual("等待空闲工位", target.GetType().GetField("Label").GetValue(target));
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
    public void CompletedOrderSettlesIncomeImmediatelyWithoutACoinPile()
    {
        int startingBalance = _game.Balance;
        PaymentDropModel drop = _game.Payments.CreateFinalPayment(77, 1, 300, 60);
        Invoke("HandlePaymentCreated", drop);

        Assert.AreEqual(PaymentDropState.Collected, drop.State);
        Assert.AreEqual(startingBalance + 360, _game.Balance);
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
