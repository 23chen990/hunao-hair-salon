using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reproduces local co-op hand-offs through SalonDemo's real target picker.
/// The customer model performs the wash transition; the view/anchor fixture
/// then exercises P2's Guide -> Assign -> service path.
/// </summary>
public sealed class FirstSessionCoopFlowTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;
    private SalonCoopPlayerState _playerTwo;
    private SalonCustomerView _view;
    private Transform _washAnchor;
    private Transform _haircutAnchor;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("First session co-op flow regression");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        Set("_game", _game);
        Set("_mobileMode", true);
        Set("_coopMode", true);
        Set("_simple2DMode", true);

        GameObject p2 = NewObject("P2", new Vector3(0f, .05f, 0f));
        _playerTwo = new SalonCoopPlayerState(2) { Transform = p2.transform };
        Set("_coopPlayerTwo", _playerTwo);

        _washAnchor = NewObject("Wash service anchor", new Vector3(0f, 0f, 0f)).transform;
        _haircutAnchor = NewObject("Haircut service anchor", new Vector3(3f, 0f, 0f)).transform;
        Dictionary<int, Transform> anchors = Field<Dictionary<int, Transform>>("_playerServiceAnchors");
        anchors.Clear();
        anchors[0] = _washAnchor;
        anchors[1] = _haircutAnchor;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void CoopHandheldDryerWorksBeforeAutomaticStandIsBuilt()
    {
        CustomerModel customer = PrepareManualDry();
        object target = FindCoopTarget();
        AssertTarget(target, customer, "StartDry", 1, true, "The handheld dryer must be usable before its stand.");
        Assert.AreEqual("按住吹发", target.GetType().GetField("Label").GetValue(target));
        DryPointer(true);
        ExecuteCoopTarget(target);
        Assert.IsTrue(customer.ManualBlowHolding);
        Assert.AreEqual(2, customer.InteractionOwnerPlayerId);
        Assert.IsFalse(customer.AutoBlowRunning);
        Invoke("TickCoopWork", _playerTwo, _game.ServiceConfig.ManualBlowGoodStart + .1f);
        DryPointer(false);
        Invoke("TickCoopWork", _playerTwo, 0f);
        Assert.AreEqual(BlowResult.Good, customer.LastBlowResult);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.IsFalse(_playerTwo.IsWorking);
    }

    [Test]
    public void CoopHandheldDryerPauseWaitsForFreshHoldWithoutLosingProgress()
    {
        CustomerModel customer = PrepareManualDry();
        DryPointer(true);
        ExecuteCoopTarget(FindCoopTarget());
        Assert.IsTrue(customer.ManualBlowHolding);
        Invoke("TickCoopWork", _playerTwo, 1f);
        float elapsed = customer.ManualBlowElapsed;
        Invoke("SuspendCoopInput");
        Invoke("TickCoopWork", _playerTwo, 2f);
        Assert.IsTrue(customer.ManualBlowHolding);
        Assert.AreEqual(elapsed, customer.ManualBlowElapsed);
        DryPointer(true);
        Invoke("TickCoopWork", _playerTwo, _game.ServiceConfig.ManualBlowGoodStart + .1f - elapsed);
        DryPointer(false);
        Invoke("TickCoopWork", _playerTwo, 0f);
        Assert.AreEqual(BlowResult.Good, customer.LastBlowResult);
    }

    private CustomerModel PrepareManualDry()
    {
        Assert.IsFalse(_game.HasAutoBlowStand);
        CustomerModel customer = SpawnServing(new[] { ServiceType.Dry }, 1, 850);
        AttachView(customer, _haircutAnchor.position);
        _playerTwo.Transform.position = _haircutAnchor.position;
        GameObject canvas = new GameObject("P2 controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(_root.transform);
        _playerTwo.Controls = SalonMobileControls.Create(canvas.transform);
        NewObject("Events", Vector3.zero).AddComponent<EventSystem>();
        return customer;
    }

    private void DryPointer(bool held)
    {
        GameObject button = _playerTwo.Controls.transform.Find("MobileInteractionButton").gameObject;
        var data = new PointerEventData(_root.GetComponentInChildren<EventSystem>()) { pointerId = 2 };
        if (held) ExecuteEvents.Execute(button, data, ExecuteEvents.pointerDownHandler);
        else ExecuteEvents.Execute(button, data, ExecuteEvents.pointerUpHandler);
    }

    [Test]
    public void CoopWashThenDryCustomerCanBeGuidedFromWashBedToBlowChair()
    {
        CustomerModel customer = SpawnServing(new[] { ServiceType.Wash, ServiceType.Dry }, 0, 701);
        AttachView(customer, _washAnchor.position);
        CompleteWashForPlayerTwo(customer);

        object target = FindCoopTarget();
        AssertTarget(target, customer, "Guide", 0, true,
            "A dry customer left at a wash bed must request transfer instead of starting blow-dry there.");
        ExecuteCoopTarget(target);
        Assert.AreSame(customer, _playerTwo.GuidedCustomer,
            "P2 must retain the customer while walking to the compatible blow-dry chair.");

        _playerTwo.Transform.position = _haircutAnchor.position;
        target = FindCoopTarget();
        AssertTarget(target, customer, "Assign", 1, true,
            "P2 must receive an actionable compatible chair target after the transfer prompt.");
        ExecuteCoopTarget(target);
        Assert.IsNull(_playerTwo.GuidedCustomer);
        Assert.AreEqual(1, customer.Station);
        Assert.AreEqual(CustomerState.MovingToStation, customer.State);

        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        _view.transform.position = _haircutAnchor.position;
        SetReachedDestination(true);
        _game.InstallAutoBlowStand();
        target = FindCoopTarget();
        AssertTarget(target, customer, "StartDry", 1, true,
            "After arriving at the blow-dry chair, P2 must be able to start the background service.");
        ExecuteCoopTarget(target);
        Assert.IsTrue(customer.AutoBlowRunning,
            "The blow-dry operation must start only after the compatible transfer.");
    }

    [Test]
    public void CoopWrongStationCanBeCorrectedWithoutAddingAnotherWrongStationPenalty()
    {
        CustomerModel customer = SpawnServing(new[] { ServiceType.Dry }, 0, 702);
        int wrongStationCount = customer.WrongStationCount;
        Assert.AreEqual(1, wrongStationCount,
            "The deliberately wrong wash-bed assignment should be recorded once.");
        AttachView(customer, _washAnchor.position);

        object target = FindCoopTarget();
        AssertTarget(target, customer, "Guide", 0, true,
            "An active customer at an incompatible station must expose the correction path.");
        ExecuteCoopTarget(target);
        _playerTwo.Transform.position = _haircutAnchor.position;

        target = FindCoopTarget();
        AssertTarget(target, customer, "Assign", 1, true,
            "The correction path must choose the free compatible haircut station.");
        ExecuteCoopTarget(target);

        Assert.AreEqual(CustomerState.MovingToStation, customer.State);
        Assert.AreEqual(1, customer.Station);
        Assert.AreEqual(wrongStationCount, customer.WrongStationCount,
            "Moving from the wrong station to the compatible station must not add a second penalty.");
        Assert.IsNull(_playerTwo.GuidedCustomer);
    }

    [Test]
    public void CoopEscortCanStandUpTheCustomerBlockingTheOnlyChair()
    {
        Assert.IsTrue(_game.SetWorkstationAvailability(2, false));
        Assert.IsTrue(_game.SetWorkstationAvailability(3, false));
        Assert.IsTrue(_game.SetWorkstationAvailability(4, false));
        CustomerModel cutOnWash = SpawnServing(new[] { ServiceType.Cut }, 0, 801);
        SpawnServing(new[] { ServiceType.Wash }, 1, 802);
        AttachView(cutOnWash, _washAnchor.position);
        CustomerModel guided = _game.Spawn(803, new[] { ServiceType.Wash });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        guided.State = CustomerState.Waiting;
        Assert.IsTrue(_game.EngageCustomerForHandoff(guided));
        _playerTwo.GuidedCustomer = guided;
        _playerTwo.Transform.position = _washAnchor.position;

        object target = FindCoopTarget();
        AssertTarget(target, cutOnWash, "Recall", 0, true,
            "P2 must be able to stand up the customer sitting on the only chair they need.");
        ExecuteCoopTarget(target);
        Assert.AreEqual(CustomerState.Waiting, cutOnWash.State);
        Assert.IsFalse(_game.IsStationOccupied(0));
        Assert.AreSame(cutOnWash, _playerTwo.GuidedCustomer);
        Assert.AreSame(guided, _playerTwo.ResumeGuidedCustomer,
            "Standing someone up must keep the customer P2 was already walking.");
    }

    private CustomerModel SpawnServing(IList<ServiceType> needs, int station, int id)
    {
        CustomerModel customer = _game.Spawn(id, needs);
        Assert.IsNotNull(customer);
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(customer, station));
        _game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }

    private void CompleteWashForPlayerTwo(CustomerModel customer)
    {
        Assert.IsTrue(_game.BeginWashFoamHold(2, customer));
        Assert.IsTrue(_game.TickActiveServiceAction(customer,
            _game.ServiceConfig.ShampooDuration + .01f));
        _game.Tick(_game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(_game.IsWashFoamReadyToRinse(customer));
        Assert.IsTrue(_game.FinishWashRinse(2, customer));
        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.AreEqual(0, customer.Station,
            "The completed wash intentionally leaves the customer at the wash bed until transferred.");
        Assert.AreEqual(WorkstationState.AwaitingTransfer, _game.Workstations[0].State);
    }

    private void AttachView(CustomerModel customer, Vector3 position)
    {
        GameObject viewObject = NewObject("Customer " + customer.Id, position);
        _view = viewObject.AddComponent<SalonCustomerView>();
        _view.Customer = customer;
        SetReachedDestination(true);
        Field<List<SalonCustomerView>>("_customerViews").Add(_view);
    }

    private object FindCoopTarget() => Invoke("FindCoopMobileTarget", _playerTwo);

    private void ExecuteCoopTarget(object target) => Invoke("ExecuteCoopMobileTarget", _playerTwo, target);

    private void AssertTarget(object target, CustomerModel customer, string action,
        int station, bool available, string message)
    {
        Assert.AreSame(customer, target.GetType().GetField("Customer").GetValue(target), message);
        Assert.AreEqual(action, target.GetType().GetField("Action").GetValue(target).ToString(), message);
        Assert.AreEqual(station, (int)target.GetType().GetField("Station").GetValue(target), message);
        Assert.AreEqual(available, (bool)target.GetType().GetField("Available").GetValue(target), message);
    }

    private GameObject NewObject(string name, Vector3 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(_root.transform, false);
        gameObject.transform.position = position;
        return gameObject;
    }

    private void SetReachedDestination(bool reached)
    {
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination",
            BindingFlags.Instance | BindingFlags.Public).SetValue(_view, reached);
    }

    private T Field<T>(string name)
    {
        FieldInfo field = typeof(SalonDemo).GetField(name, InstancePrivate);
        Assert.IsNotNull(field, "Missing SalonDemo field: " + name);
        return (T)field.GetValue(_demo);
    }

    private void Set(string name, object value)
    {
        FieldInfo field = typeof(SalonDemo).GetField(name, InstancePrivate);
        Assert.IsNotNull(field, "Missing SalonDemo field: " + name);
        field.SetValue(_demo, value);
    }

    private object Invoke(string name, params object[] args)
    {
        MethodInfo method = typeof(SalonDemo).GetMethod(name, InstancePrivate);
        Assert.IsNotNull(method, "Missing SalonDemo method: " + name);
        return method.Invoke(_demo, args);
    }
}
