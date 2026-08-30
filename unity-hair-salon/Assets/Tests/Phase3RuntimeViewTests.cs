using HairSalon;
using NUnit.Framework;
using UnityEngine;

public class Phase3RuntimeViewTests
{
    [Test]
    public void ToolbarCleanupPreservesAnUnbackedDirectChildLabel()
    {
        var toolbar = new GameObject("Toolbar", typeof(RectTransform));
        var label = new GameObject("Focus Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        label.transform.SetParent(toolbar.transform, false);

        Transform root = SalonUiFactory.DirectChildRoot(label.transform, toolbar.transform);

        Assert.AreSame(label.transform, root);
        Object.DestroyImmediate(toolbar);
    }

    [Test]
    public void DemandBubbleUsesGeneratedWidthProgressOnlyOnTheActiveRequirement()
    {
        var root = new GameObject("Demand Test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var service = new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears);
        var view = root.AddComponent<HaircutDemandBubbleView>();
        view.Initialize(service, cameraObject.GetComponent<Camera>());

        view.SetHoldProgress(.5f);

        Assert.AreEqual(2, view.StepCount);
        Assert.AreEqual(0, view.ActiveStepIndex);
        Assert.AreEqual(.5f, view.GetStepProgress(0), .001f,
            "Only the currently active requirement icon should show its radial hold progress.");
        Assert.AreEqual(0f, view.GetStepProgress(1), .001f);
        Transform firstFill = FindDescendant(root.transform, "Haircut Action Progress Fill 1");
        Transform secondTrack = FindDescendant(root.transform, "Haircut Action Progress Track 2");
        Assert.IsNotNull(firstFill);
        Assert.IsNotNull(secondTrack);
        Assert.AreEqual(.5f, firstFill.localScale.x, .001f);
        Assert.IsTrue(firstFill.parent.gameObject.activeSelf);
        Assert.IsFalse(secondTrack.gameObject.activeSelf);

        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Perfect);
        view.Refresh();
        view.SetHoldProgress(.35f);

        Assert.AreEqual(1, view.ActiveStepIndex);
        Assert.AreEqual(0f, view.GetStepProgress(0), .001f);
        Assert.AreEqual(.35f, view.GetStepProgress(1), .001f,
            "After step completion, generated progress must follow the newly active requirement.");
        Transform secondFill = FindDescendant(root.transform, "Haircut Action Progress Fill 2");
        Assert.AreEqual(.35f, secondFill.localScale.x, .001f);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void RuntimeContainsAnInstanceOwnedCustomerUiRoot()
    {
        Assert.IsNotNull(typeof(SalonDemo).Assembly.GetType("CustomerUIRootView"),
            "Each customer needs one instance-owned UI root that can move between fixed anchors.");
    }

    [Test]
    public void CustomerUiRootExposesAnIndependentMobileAnchor()
    {
        Assert.IsNotNull(typeof(CustomerUIRootView).GetMethod("SetMobileAnchor"),
            "Moving customers need a stable UI anchor that does not inherit their animated transform.");
    }

    [Test]
    public void CustomerUiRootExposesOneRequirementContainerAndASeparateEmotionSlot()
    {
        var owner = new GameObject("Customer UI Owner");
        var ui = owner.AddComponent<CustomerUIRootView>();
        ui.Initialize(new CustomerModel { Id = 18 });
        var requirement = typeof(CustomerUIRootView).GetProperty("RequirementContainer");
        var emotion = typeof(CustomerUIRootView).GetProperty("EmotionContainer");

        Assert.IsNotNull(requirement);
        Assert.IsNotNull(emotion);
        Assert.AreNotSame(requirement.GetValue(ui), emotion.GetValue(ui));
        Object.DestroyImmediate(owner);
    }

    [Test]
    public void CustomerUiRootCannotBeReboundAndHidesWithoutAnAnchor()
    {
        var owner = new GameObject("Customer A");
        var firstAnchor = new GameObject("Waiting Anchor").transform;
        var secondAnchor = new GameObject("Seat Anchor").transform;
        var first = new CustomerModel { Id = 11 };
        var second = new CustomerModel { Id = 12 };
        var ui = owner.AddComponent<CustomerUIRootView>();

        ui.Initialize(first);
        ui.SetAnchor(firstAnchor);
        Transform visualRoot = ui.VisualRoot;
        for (int i = 0; i < 32; i++)
            ui.SetAnchor((i & 1) == 0 ? secondAnchor : firstAnchor);
        ui.SetAnchor(secondAnchor);
        ui.Initialize(second);

        Assert.AreSame(first, ui.Customer, "A customer UI instance must never inherit another customer's data.");
        Assert.AreSame(visualRoot, ui.VisualRoot, "Changing seats must not duplicate the UI root.");
        Assert.AreSame(secondAnchor, ui.VisualRoot.parent);

        ui.SetAnchor(null);
        Assert.IsFalse(ui.VisualRoot.gameObject.activeSelf, "Leaving/exited customers must not leave world UI behind.");

        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(firstAnchor.gameObject);
        Object.DestroyImmediate(secondAnchor.gameObject);
    }

    [Test]
    public void PlayerOperationFeedbackDoesNotCreateAWorldCanvasOverCustomerRequirements()
    {
        var root = new GameObject("Operation Feedback Test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var customerView = root.AddComponent<SalonCustomerView>();
        var feedback = root.AddComponent<HaircutRuntimeFeedback>();

        feedback.Initialize(customerView, new HaircutConfig(), cameraObject.GetComponent<Camera>());

        Assert.IsNull(root.transform.Find("剪发操作进度（临时）"),
            "Player tool/hold UI belongs in the bottom operation HUD, not above the customer.");

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void CustomerUiViewsIgnoreDuplicateInitialization()
    {
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var demandRoot = new GameObject("Demand Root");
        var demand = demandRoot.AddComponent<HaircutDemandBubbleView>();
        var firstService = new HaircutServiceModel(SalonTool.Scissors);
        demand.Initialize(firstService, cameraObject.GetComponent<Camera>());
        demand.Initialize(new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears), cameraObject.GetComponent<Camera>());

        Assert.AreEqual(1, demand.StepCount, "A customer must not receive a second requirement group.");
        Assert.AreEqual(1, CountDirectChildren(demandRoot.transform, "剪发需求气泡 V1"));

        var emotionRoot = new GameObject("Emotion Root");
        var firstCustomer = new CustomerModel { Emotion = CustomerEmotion.Angry };
        var emotion = emotionRoot.AddComponent<CustomerEmotionView>();
        emotion.Initialize(firstCustomer, cameraObject.GetComponent<Camera>());
        emotion.Initialize(new CustomerModel { Emotion = CustomerEmotion.Calm }, cameraObject.GetComponent<Camera>());
        emotion.Refresh();

        Assert.IsTrue(emotion.IsVisible, "Emotion UI must remain bound to its original customer.");
        Assert.IsFalse(emotion.Symbol == "汗" || emotion.Symbol == "怒");
        Assert.AreEqual(1, CountDirectChildren(emotionRoot.transform, "Low-Poly Emotion Placeholder Slot"));

        Object.DestroyImmediate(demandRoot);
        Object.DestroyImmediate(emotionRoot);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void WaitingSlotRegistryCompactsCustomersWhenTheHeadLeaves()
    {
        System.Type type = typeof(SalonDemo).Assembly.GetType("CustomerWaitingSlotRegistry");
        Assert.IsNotNull(type);
        object registry = System.Activator.CreateInstance(type);
        var reserve = type.GetMethod("Reserve");
        var release = type.GetMethod("Release");
        Assert.IsNotNull(reserve);
        Assert.IsNotNull(release);
        var a = new CustomerModel { Id = 1 };
        var b = new CustomerModel { Id = 2 };
        var c = new CustomerModel { Id = 3 };

        Assert.AreEqual(0, reserve.Invoke(registry, new object[] { a, 4 }));
        Assert.AreEqual(1, reserve.Invoke(registry, new object[] { b, 4 }));
        release.Invoke(registry, new object[] { a });

        Assert.AreEqual(0, reserve.Invoke(registry, new object[] { b, 4 }),
            "The next FIFO customer must move into the released front slot.");
        Assert.AreEqual(1, reserve.Invoke(registry, new object[] { c, 4 }));
    }

    [Test]
    public void ToolbarChildIsHiddenBeforeDeferredDestroy()
    {
        var toolbar = new GameObject("Toolbar");
        var child = new GameObject("Old Tool");
        child.transform.SetParent(toolbar.transform, false);

        var hideAndDestroy = typeof(SalonUiFactory).GetMethod("HideAndDestroy");
        Assert.IsNotNull(hideAndDestroy);
        hideAndDestroy.Invoke(null, new object[] { child });

        Assert.IsFalse(child.activeSelf, "Old tool UI must disappear in the same frame as a rapid focus switch.");
        Object.DestroyImmediate(toolbar);
    }

    [Test]
    public void PlayerHoldProgressDoesNotCreateASeparateBottomBar()
    {
        System.Type type = typeof(SalonDemo).Assembly.GetType("HaircutOperationProgressView");
        Assert.IsNull(type, "The reference uses the active requirement icon's radial ring, not a second bottom progress bar.");
    }

    [TestCase(60, CoinPileSize.Small)]
    [TestCase(120, CoinPileSize.Medium)]
    [TestCase(300, CoinPileSize.Large)]
    public void CoinPileUsesOneVisualAndDoesNotShowAmountOnGround(int amount, CoinPileSize size)
    {
        var root = new GameObject("Coin Test");
        var drop = new PaymentDropModel { Id = 1, Amount = amount, Size = size };
        var view = root.AddComponent<CoinPileView>();

        view.Initialize(drop);

        Assert.AreEqual(6, view.VisualCoinCount);
        Assert.IsNull(FindDescendant(root.transform, "Coin Amount"),
            "The ground pickup should communicate collectability, not expose its backend amount.");
        Assert.IsNotNull(root.GetComponent<Collider>());
        Object.DestroyImmediate(root);
    }

    [Test]
    public void RuntimeDeclaresSeparateCustomerAndPlayerServiceAnchors()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Assert.IsNotNull(typeof(SalonDemo).GetField("_customerSeatAnchors", flags));
        Assert.IsNotNull(typeof(SalonDemo).GetField("_playerServiceAnchors", flags));
    }

    [Test]
    public void CustomerUsesSeatedPoseOnlyAfterReachingAWaitingOrServiceSeat()
    {
        var shouldSit = typeof(SalonCustomerView).GetMethod("ShouldUseSeatedPose");

        Assert.IsNotNull(shouldSit,
            "The runtime view needs an explicit state-to-pose rule instead of remaining in its spawn pose.");
        Assert.IsFalse((bool)shouldSit.Invoke(null, new object[] { CustomerState.Entering, true }));
        Assert.IsFalse((bool)shouldSit.Invoke(null, new object[] { CustomerState.Waiting, false }));
        Assert.IsTrue((bool)shouldSit.Invoke(null, new object[] { CustomerState.Waiting, true }));
        Assert.IsFalse((bool)shouldSit.Invoke(null, new object[] { CustomerState.MovingToStation, true }));
        Assert.IsTrue((bool)shouldSit.Invoke(null, new object[] { CustomerState.Serving, true }));
        Assert.IsTrue((bool)shouldSit.Invoke(null, new object[] { CustomerState.Finished, true }));
        Assert.IsFalse((bool)shouldSit.Invoke(null, new object[] { CustomerState.Leaving, true }));
    }

    [Test]
    public void SeatedPoseLowersCustomerAndHidesStandingLegs()
    {
        var root = new GameObject("Customer View");
        var body = new GameObject("Body").transform;
        var head = new GameObject("Head").transform;
        var leftLeg = new GameObject("Leg L").transform;
        var rightLeg = new GameObject("Leg R").transform;
        body.SetParent(root.transform, false);
        head.SetParent(root.transform, false);
        leftLeg.SetParent(root.transform, false);
        rightLeg.SetParent(root.transform, false);
        body.localPosition = new Vector3(0f, .95f, 0f);
        body.localScale = new Vector3(1.05f, 1.65f, .72f);
        head.localPosition = new Vector3(0f, 2.05f, -.03f);
        var view = root.AddComponent<SalonCustomerView>();
        var setPose = typeof(SalonCustomerView).GetMethod("SetSeatedPose");

        Assert.IsNotNull(setPose);
        setPose.Invoke(view, new object[] { true });

        Assert.AreEqual(.65f, body.localPosition.y, .001f);
        Assert.AreEqual(1.15f, body.localScale.y, .001f);
        Assert.AreEqual(1.62f, head.localPosition.y, .001f);
        Assert.IsFalse(leftLeg.gameObject.activeSelf);
        Assert.IsFalse(rightLeg.gameObject.activeSelf);

        setPose.Invoke(view, new object[] { false });
        Assert.AreEqual(.95f, body.localPosition.y, .001f);
        Assert.AreEqual(2.05f, head.localPosition.y, .001f);
        Assert.IsTrue(leftLeg.gameObject.activeSelf);
        Assert.IsTrue(rightLeg.gameObject.activeSelf);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void WaitingSeatAnchorsStayOnTheSofaAtSeatedRootHeight()
    {
        var field = typeof(SalonDemo).GetField("WaitingPositions",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var seats = (Vector3[])field.GetValue(null);

        Assert.AreEqual(SalonGameModel.WaitingCapacity, seats.Length);
        foreach (Vector3 seat in seats)
        {
            Assert.That(seat.x, Is.InRange(-9.2f, -5.2f), "Every waiting anchor must remain over the sofa cushion.");
            Assert.AreEqual(.4f, seat.y, .001f, "Waiting customers need a seated root height, not the entry standing height.");
            Assert.AreEqual(-4.3f, seat.z, .001f);
        }
    }

    [Test]
    public void FinishedCustomerRemembersTheSeatUsedForFeedbackUi()
    {
        var viewObject = new GameObject("Customer View");
        var anchorObject = new GameObject("CustomerUIAnchor");
        var view = viewObject.AddComponent<SalonCustomerView>();

        var remember = typeof(SalonCustomerView).GetMethod("RememberServiceAnchor");
        var stationId = typeof(SalonCustomerView).GetField("LastStationId");
        var serviceAnchor = typeof(SalonCustomerView).GetField("LastServiceUiAnchor");
        Assert.IsNotNull(remember);
        Assert.IsNotNull(stationId);
        Assert.IsNotNull(serviceAnchor);
        remember.Invoke(view, new object[] { 2, anchorObject.transform });

        Assert.AreEqual(2, stationId.GetValue(view));
        Assert.AreSame(anchorObject.transform, serviceAnchor.GetValue(view));
        Object.DestroyImmediate(viewObject);
        Object.DestroyImmediate(anchorObject);
    }

    [Test]
    public void EnteringAndLeavingUseDifferentWaypointLanes()
    {
        System.Type pathType = typeof(SalonDemo).Assembly.GetType("SalonCustomerPath");
        Assert.IsNotNull(pathType);
        var buildEntering = pathType.GetMethod("BuildEnteringRoute");
        var buildLeaving = pathType.GetMethod("BuildLeavingRoute");
        Assert.IsNotNull(buildEntering);
        Assert.IsNotNull(buildLeaving);
        Vector3[] entering = (Vector3[])buildEntering.Invoke(null, new object[]
        {
            new Vector3(-10.4f, 1f, -2.7f), new Vector3(-8.4f, 1f, -4.1f)
        });
        Vector3[] leaving = (Vector3[])buildLeaving.Invoke(null, new object[]
        {
            new Vector3(2.2f, 1f, .2f), new Vector3(-10.8f, 1f, 5.9f)
        });

        Assert.GreaterOrEqual(entering.Length, 2);
        Assert.GreaterOrEqual(leaving.Length, 3);
        Assert.AreNotEqual(entering[0].z, leaving[0].z,
            "Incoming and outgoing customers must not fully share one trajectory.");
        Assert.AreEqual(-10.8f, leaving[leaving.Length - 1].x, .001f);
    }

    [Test]
    public void PendingPickupsAtOneStationReceiveDistinctVisualOffsets()
    {
        System.Type layoutType = typeof(SalonDemo).Assembly.GetType("CoinPickupLayout");
        Assert.IsNotNull(layoutType);
        var offsetFor = layoutType.GetMethod("OffsetFor");
        Assert.IsNotNull(offsetFor);

        Vector3 first = (Vector3)offsetFor.Invoke(null, new object[] { 1 });
        Vector3 second = (Vector3)offsetFor.Invoke(null, new object[] { 2 });

        Assert.AreNotEqual(first, second,
            "Two uncollected service rewards at the same workstation must remain visually separable.");
    }

    [Test]
    public void ReputationHudIsExplicitlyMarkedUnavailableInThisPhase()
    {
        var field = typeof(SalonGameModel).GetField("ReputationSystemEnabled");
        Assert.IsNotNull(field);
        Assert.AreEqual(false, field.GetValue(null));
    }

    private static int CountDirectChildren(Transform root, string name)
    {
        int count = 0;
        foreach (Transform child in root)
            if (child.name == name) count++;
        return count;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
