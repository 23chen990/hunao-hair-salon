using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Mobile pacing benchmarked against Overcooked: a finished background step
/// gives a walk-away window with escalating lateness, every served order earns
/// a speed tip, and one good order roughly recovers one miss.
/// </summary>
public sealed class CompetitorBalanceTests
{
    [Test]
    public void MobileFoamWaitLeavesTimeForOneErrandWithoutDelayOrAccident()
    {
        SalonGameModel game = NewMobileGame();
        CustomerModel customer = SpawnServing(game, 7001,
            new List<ServiceType> { ServiceType.Wash, ServiceType.Dry }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        TickFor(game, game.ServiceConfig.ShampooDuration + .05f);
        Assert.IsTrue(game.IsWashFoamWaitRunning(customer));
        float patienceAtFoam = customer.Patience;

        // Foam is ready at 8 s; returning 5 s later fits the six-second grace.
        TickFor(game, game.ServiceConfig.FoamOptimalStart + 5f);

        Assert.IsTrue(game.IsWashFoamReadyToRinse(customer));
        Assert.AreEqual(AccidentSeverity.None, customer.AccidentSeverity,
            "One cross-shop errand after the foam is ready must not be an accident.");
        Assert.IsFalse(customer.HadServiceDelay,
            "The designed foam wait must not count as the player's service delay.");
        Assert.AreEqual(patienceAtFoam, customer.Patience, .001f,
            "Like the unattended dryer, protected foam time does not drain patience.");
        Assert.IsTrue(game.FinishWashRinse(customer));
    }

    [Test]
    public void MobileFoamStillEscalatesWhenTheWindowIsIgnored()
    {
        SalonGameModel game = NewMobileGame();
        CustomerModel customer = SpawnServing(game, 7002,
            new List<ServiceType> { ServiceType.Wash, ServiceType.Dry }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        TickFor(game, game.ServiceConfig.ShampooDuration + .05f);

        TickFor(game, game.ServiceConfig.FoamMinorLateThreshold + .1f);
        Assert.AreEqual(AccidentSeverity.Minor, customer.AccidentSeverity);
        TickFor(game, game.ServiceConfig.FoamModerateLateThreshold - game.ServiceConfig.FoamMinorLateThreshold);
        Assert.AreEqual(AccidentSeverity.Moderate, customer.AccidentSeverity);
    }

    [TestCase(.9f, AccidentSeverity.None, false, 60)]
    [TestCase(.5f, AccidentSeverity.None, false, 38)]
    [TestCase(.1f, AccidentSeverity.None, false, 22)]
    [TestCase(.9f, AccidentSeverity.Minor, false, 38)]
    [TestCase(.9f, AccidentSeverity.None, true, 38)]
    [TestCase(.1f, AccidentSeverity.Minor, false, 0)]
    [TestCase(.9f, AccidentSeverity.Moderate, false, 0)]
    public void SpeedTipFollowsRemainingPatience(float patienceRatio, AccidentSeverity accident,
        bool wrongStation, int expected)
    {
        var config = new SalonRewardConfig();
        SalonMobileDayConfig.ApplyRewardProfile(config);
        var payments = new SalonPaymentModel(0, config);

        Assert.AreEqual(expected, payments.ResolveSpeedTip(patienceRatio, accident, wrongStation));
    }

    [Test]
    public void EveryServedMobileOrderPaysASpeedTip()
    {
        SalonGameModel fast = NewMobileGame();
        CustomerModel quick = SpawnServing(fast, 7011, new List<ServiceType> { ServiceType.Cut }, 1);
        Assert.IsTrue(fast.ApplyService(quick, ServiceType.Cut, 1f));
        Assert.AreEqual(60, fast.Payments.Drops[0].TipReward,
            "A promptly served single cut earns the top tip even though it is not a strict happy completion.");

        SalonGameModel slow = NewMobileGame();
        CustomerModel late = SpawnServing(slow, 7012, new List<ServiceType> { ServiceType.Cut }, 1);
        late.Patience = 10f;
        Assert.IsTrue(slow.ApplyService(late, ServiceType.Cut, 1f));
        Assert.AreEqual(22, slow.Payments.Drops[0].TipReward);
    }

    [Test]
    public void LegacyRewardConfigKeepsHappyOnlyTips()
    {
        var game = new SalonGameModel(flowConfig: new SalonFlowConfig { MaxCustomers = 10, WaitingCapacity = 10 });
        CustomerModel customer = SpawnServing(game, 7013, new List<ServiceType> { ServiceType.Cut }, 1);
        Assert.IsTrue(game.ApplyService(customer, ServiceType.Cut, 1f));
        Assert.AreEqual(0, game.Payments.Drops[0].TipReward);
    }

    [Test]
    public void MobileShopSatisfactionCanRecoverFromAMiss()
    {
        var config = new ShopSatisfactionConfig();
        SalonMobileDayConfig.ApplySatisfactionProfile(config);
        var satisfaction = new ShopSatisfactionModel(config);

        Settle(satisfaction, 1, CustomerServiceResult.NormalCompletion);
        Assert.AreEqual(96, satisfaction.CurrentSatisfaction, "A clean order visibly helps.");

        Settle(satisfaction, 2, CustomerServiceResult.NormalCompletion, AccidentSeverity.Minor);
        Assert.AreEqual(100, satisfaction.CurrentSatisfaction, "A slightly late rinse still nets positive.");

        Settle(satisfaction, 3, CustomerServiceResult.NormalCompletion, AccidentSeverity.Moderate, 20f);
        Assert.AreEqual(99, satisfaction.CurrentSatisfaction, "A completed order offsets some accident cost.");

        Settle(satisfaction, 4, CustomerServiceResult.Failed, AccidentSeverity.None, 60f);
        Assert.AreEqual(94, satisfaction.CurrentSatisfaction);

        Settle(satisfaction, 5, CustomerServiceResult.NormalCompletion);
        Settle(satisfaction, 6, CustomerServiceResult.NormalCompletion);
        Assert.GreaterOrEqual(satisfaction.CurrentSatisfaction, 92,
            "Two clean orders recover a walkout, as one Overcooked delivery recovers one expired ticket.");
    }

    [Test]
    public void LegacySatisfactionConfigKeepsItsAccidentPenalty()
    {
        var satisfaction = new ShopSatisfactionModel();
        Settle(satisfaction, 1, CustomerServiceResult.NormalCompletion, AccidentSeverity.Minor);
        Assert.AreEqual(77, satisfaction.CurrentSatisfaction);
    }

    [TestCase(0, 45)]
    [TestCase(35, 63)]
    [TestCase(89, 90)]
    [TestCase(90, 90)]
    [TestCase(97, 97)]
    public void OvernightSatisfactionClosesHalfTheGap(int endOfDay, int expected)
    {
        Assert.AreEqual(expected, SalonMobileDayConfig.OvernightSatisfaction(endOfDay));
    }

    [Test]
    public void PatienceDrainUsesTheApprovedMobilePacingProfile()
    {
        Assert.AreEqual(1.6f, SalonMobileDayConfig.PatienceDrainPerSecond(1), .0001f);
        Assert.AreEqual(1.8f, SalonMobileDayConfig.PatienceDrainPerSecond(2), .0001f);
        Assert.AreEqual(1.8f, SalonMobileDayConfig.PatienceDrainPerSecond(5), .0001f);
    }

    [Test]
    public void GuidedCustomerPointsToTheSeatedCustomerBlockingTheOnlyCompatibleStation()
    {
        var root = new GameObject("Blocked wash station guidance regression");
        try
        {
            BlockedWashScene scene = BuildBlockedWashScene(root);

            object blocked = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Guided, TargetField<CustomerModel>(blocked, "Customer"));
            Assert.AreEqual("先为 2 号冲洗", TargetField<string>(blocked, "Label"),
                "The only wash chair is held by a customer who just needs rinsing; say so.");
            Assert.AreEqual(0, TargetField<int>(blocked, "Station"));
            Assert.IsFalse(TargetField<bool>(blocked, "Available"));
            StringAssert.Contains("3 号", TargetField<string>(blocked, "Hint"));

            scene.Player.position = scene.WashAnchor.position + new Vector3(.4f, 0f, 0f);
            object rinse = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Seated, TargetField<CustomerModel>(rinse, "Customer"),
                "At the blocked chair, the seated customer's rinse is still executable while guiding.");
            Assert.AreEqual("RinseWash", TargetField<object>(rinse, "Action").ToString());
            Assert.IsTrue(TargetField<bool>(rinse, "Available"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RinsedBlockerCanBeMovedWhileGuidingAndReceptionResumes()
    {
        var root = new GameObject("Rinsed blocker transfer regression");
        try
        {
            BlockedWashScene scene = BuildBlockedWashScene(root);
            Assert.IsTrue(scene.Game.FinishWashRinse(scene.Seated));
            Assert.AreEqual(ServiceType.Dry, scene.Seated.CurrentNeed);
            Assert.IsTrue(scene.Game.IsStationOccupied(0), "A rinsed customer still holds the wash chair.");

            object blocked = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Guided, TargetField<CustomerModel>(blocked, "Customer"));
            Assert.AreEqual("先转移 2 号", TargetField<string>(blocked, "Label"),
                "After rinsing, the wash chair frees only once the seated customer is moved on.");
            Assert.AreEqual(0, TargetField<int>(blocked, "Station"));
            Assert.IsFalse(TargetField<bool>(blocked, "Available"));
            StringAssert.Contains("3 号", TargetField<string>(blocked, "Hint"));

            scene.Player.position = scene.WashAnchor.position + new Vector3(.4f, 0f, 0f);
            object transfer = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Seated, TargetField<CustomerModel>(transfer, "Customer"),
                "The blocking customer must be movable without cancelling the current reception.");
            Assert.AreEqual("Guide", TargetField<object>(transfer, "Action").ToString());
            Assert.IsTrue(TargetField<bool>(transfer, "Available"));
            Invoke(scene.Demo, "ExecuteMobileTarget", transfer);
            Assert.AreSame(scene.Seated, Field<CustomerModel>(scene.Demo, "_mobileGuidedCustomer"));

            scene.Player.position = scene.HaircutAnchor.position + new Vector3(.4f, 0f, 0f);
            object assign = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Seated, TargetField<CustomerModel>(assign, "Customer"));
            Assert.AreEqual("Assign", TargetField<object>(assign, "Action").ToString());
            Assert.AreEqual(1, TargetField<int>(assign, "Station"));
            Invoke(scene.Demo, "ExecuteMobileTarget", assign);
            Assert.AreEqual(1, scene.Seated.Station);
            Assert.IsFalse(scene.Game.IsStationOccupied(0));
            Assert.AreSame(scene.Guided, Field<CustomerModel>(scene.Demo, "_mobileGuidedCustomer"),
                "Moving the blocker hands reception back to the customer who was waiting for its chair.");

            scene.Player.position = scene.WashAnchor.position + new Vector3(.4f, 0f, 0f);
            object seat = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Guided, TargetField<CustomerModel>(seat, "Customer"));
            Assert.AreEqual("Assign", TargetField<object>(seat, "Action").ToString());
            Assert.AreEqual(0, TargetField<int>(seat, "Station"));
            Assert.IsTrue(TargetField<bool>(seat, "Available"));
            Assert.AreEqual("安排洗发", TargetField<string>(seat, "Label"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CancelledReceptionCanBeGreetedAgain()
    {
        var root = new GameObject("Cancelled reception regression");
        try
        {
            BlockedWashScene scene = BuildBlockedWashScene(root);
            var waitingView = new GameObject("Received waiting customer").AddComponent<SalonCustomerView>();
            waitingView.transform.SetParent(root.transform, false);
            waitingView.transform.position = new Vector3(-9f, 0f, -4.3f);
            waitingView.Customer = scene.Guided;
            Field<List<SalonCustomerView>>(scene.Demo, "_customerViews").Add(waitingView);

            SetField(scene.Demo, "_mobileGuidedCustomer", null);
            scene.Game.ClearFocus();
            scene.Player.position = waitingView.transform.position + new Vector3(.5f, 0f, 0f);

            object greet = Invoke(scene.Demo, "FindMobileTarget");
            Assert.AreSame(scene.Guided, TargetField<CustomerModel>(greet, "Customer"));
            Assert.AreEqual("Greet", TargetField<object>(greet, "Action").ToString());
            Assert.IsTrue(TargetField<bool>(greet, "Available"));
            Invoke(scene.Demo, "ExecuteMobileTarget", greet);
            Assert.AreSame(scene.Guided, Field<CustomerModel>(scene.Demo, "_mobileGuidedCustomer"),
                "Cancelling reception must not strand an already-received customer in the queue.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private sealed class BlockedWashScene
    {
        public SalonDemo Demo;
        public SalonGameModel Game;
        public CustomerModel Seated;
        public CustomerModel Guided;
        public Transform Player;
        public Transform WashAnchor;
        public Transform HaircutAnchor;
    }

    private static BlockedWashScene BuildBlockedWashScene(GameObject root)
    {
        var demo = root.AddComponent<SalonDemo>();
        var game = new SalonGameModel(flowConfig: new SalonFlowConfig { MaxCustomers = 10, WaitingCapacity = 10 });
        for (int station = 2; station < game.Workstations.Count; station++)
            game.Workstations[station].State = WorkstationState.Locked;
        CustomerModel seated = SpawnServing(game, 1,
            new List<ServiceType> { ServiceType.Wash, ServiceType.Dry }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(seated));
        TickFor(game, game.ServiceConfig.ShampooDuration + game.ServiceConfig.FoamOptimalStart + .2f);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(seated));

        CustomerModel guided = game.Spawn(2, new List<ServiceType> { ServiceType.Wash, ServiceType.Dry });
        guided.State = CustomerState.Waiting;
        Assert.IsTrue(game.EngageCustomerForHandoff(guided));

        SetField(demo, "_game", game);
        SetField(demo, "_mobileMode", true);
        SetField(demo, "_mobileGuidedCustomer", guided);
        var player = new GameObject("Player in the middle of the shop");
        player.transform.SetParent(root.transform, false);
        player.transform.position = new Vector3(-3.5f, .05f, 3.7f);
        SetField(demo, "_player", player.transform);

        var washAnchor = new GameObject("Wash anchor");
        washAnchor.transform.SetParent(root.transform, false);
        washAnchor.transform.position = new Vector3(-8.75f, 0f, 4.05f);
        var haircutAnchor = new GameObject("Free haircut anchor");
        haircutAnchor.transform.SetParent(root.transform, false);
        haircutAnchor.transform.position = new Vector3(-2.1f, 0f, -1.2f);
        Dictionary<int, Transform> anchors = Field<Dictionary<int, Transform>>(demo, "_playerServiceAnchors");
        anchors[0] = washAnchor.transform;
        anchors[1] = haircutAnchor.transform;

        var seatedView = new GameObject("Seated customer").AddComponent<SalonCustomerView>();
        seatedView.transform.SetParent(root.transform, false);
        seatedView.transform.position = washAnchor.transform.position;
        seatedView.Customer = seated;
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination",
            BindingFlags.Instance | BindingFlags.Public).SetValue(seatedView, true);
        Field<List<SalonCustomerView>>(demo, "_customerViews").Add(seatedView);

        return new BlockedWashScene
        {
            Demo = demo, Game = game, Seated = seated, Guided = guided,
            Player = player.transform, WashAnchor = washAnchor.transform, HaircutAnchor = haircutAnchor.transform
        };
    }

    private static T TargetField<T>(object target, string name)
        => (T)target.GetType().GetField(name).GetValue(target);

    private static SalonGameModel NewMobileGame()
    {
        var reward = new SalonRewardConfig();
        SalonMobileDayConfig.ApplyRewardProfile(reward);
        return new SalonGameModel(reward,
            flowConfig: new SalonFlowConfig { MaxCustomers = 10, WaitingCapacity = 10 },
            serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id, List<ServiceType> needs, int station)
    {
        CustomerModel customer = game.Spawn(id, needs);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }

    private static void TickFor(SalonGameModel game, float seconds)
    {
        for (float elapsed = 0f; elapsed < seconds; elapsed += .1f)
            game.Tick(Mathf.Min(.1f, seconds - elapsed));
    }

    private static void Settle(ShopSatisfactionModel satisfaction, int id, CustomerServiceResult result,
        AccidentSeverity accident = AccidentSeverity.None, float waitSeconds = 0f)
    {
        var customer = new CustomerModel
        {
            Id = id,
            State = CustomerState.Exited,
            ServiceResult = result,
            AccidentSeverity = accident,
            TotalWaitSeconds = waitSeconds
        };
        Assert.IsTrue(satisfaction.TrySettleCustomer(customer));
    }

    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        field.SetValue(target, value);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, name);
        return method.Invoke(target, args);
    }
}
