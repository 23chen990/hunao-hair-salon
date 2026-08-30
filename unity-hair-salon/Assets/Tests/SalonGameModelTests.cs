using NUnit.Framework;
using HairSalon;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class SalonGameModelTests
{
    private static void FinishEntering(SalonGameModel game)
    {
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
    }

    private static void ReachStation(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }

    [Test] public void SpawnAllowsMultipleCustomersUntilCapacity()
    {
        var game = new SalonGameModel();
        for (int i = 0; i < SalonGameModel.WaitingCapacity; i++) Assert.NotNull(game.Spawn(i, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut }));
        Assert.IsNull(game.Spawn(99, new List<ServiceType> { ServiceType.Cut }));
        FinishEntering(game);
        Assert.IsTrue(game.Assign(game.Customers[0], 0));
        Assert.NotNull(game.Spawn(99, new List<ServiceType> { ServiceType.Cut }));
    }

    [Test] public void WaitingCustomerLosesPatience()
    {
        var game = new SalonGameModel(); var c = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game); game.Tick(10f); Assert.Less(c.Patience, 100f);
    }

    [Test] public void WrongToolCreatesMistakeAndLowersSatisfaction()
    {
        var game = new SalonGameModel(); var c = game.Spawn(1, new List<ServiceType> { ServiceType.Wash });
        float before = c.Satisfaction; Assert.IsFalse(game.ApplyService(c, ServiceType.Cut, 1f));
        Assert.AreEqual(1, game.Mistakes); Assert.Less(c.Satisfaction, before);
    }

    [Test] public void CompletingAllNeedsCreatesPaymentWithoutChangingBalance()
    {
        var game = new SalonGameModel(); var c = game.Spawn(1, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        int before = game.Balance;
        FinishEntering(game); game.Assign(c, 0); ReachStation(game); game.ApplyService(c, ServiceType.Wash, 1f);
        game.Assign(c, 1); ReachStation(game); game.ApplyService(c, ServiceType.Cut, 1f);
        Assert.AreEqual(1, game.Served);
        Assert.AreEqual(before, game.Balance);
        Assert.AreEqual(1, game.Payments.Drops.Count);
        Assert.AreEqual(CustomerState.Finished, c.State);
    }

    [Test]
    public void OvercutHaircutFailsWithoutCreatingPayment()
    {
        HaircutResult result = HaircutResult.Overcut;
        var game = new SalonGameModel();
        var customer = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        int before = game.Balance;
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, result, new HaircutConfig()));

        Assert.AreEqual(HaircutServiceRating.Failed, customer.LastHaircutRating);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(before, game.Balance);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test] public void AngryCustomerLeavingReleasesOccupiedWorkstation()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);

        game.Tick(100f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.AreEqual(-1, customer.Station);
        Assert.AreEqual(WorkstationState.Available, game.Workstations[1].State);
        Assert.AreEqual(-1, game.Workstations[1].CurrentCustomerId);
    }

    [Test] public void ThreeMinuteSessionExpiresAtZero()
    {
        var game = new SalonGameModel();
        game.Tick(180f);
        Assert.IsTrue(game.IsOver);
        Assert.AreEqual(0f, game.RemainingTime);
    }

    [Test] public void DemoSceneContainsDirectRuntimeEntryPoint()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HairSalonDemo.unity", OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid());
        Assert.IsNotNull(Object.FindAnyObjectByType<SalonDemo>());
    }

    [Test] public void OccupiedStationRejectsSecondCustomer()
    {
        var game = new SalonGameModel();
        var first = game.Spawn(1, new List<ServiceType> { ServiceType.Wash });
        var second = game.Spawn(2, new List<ServiceType> { ServiceType.Wash });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(first, 0));
        Assert.IsFalse(game.Assign(second, 0));
    }

    [Test] public void OperationalWrongStationAllowsExtraServiceWithoutBlockingRelocation()
    {
        var game = new SalonGameModel();
        var washCustomer = game.Spawn(1, new List<ServiceType> { ServiceType.Wash });
        var cutCustomer = game.Spawn(2, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(washCustomer, 1));
        ReachStation(game);
        Assert.IsTrue(game.BeginActiveOperation(washCustomer));
        Assert.IsTrue(game.EndActiveOperation(washCustomer));
        Assert.IsTrue(game.Assign(washCustomer, 0));
        Assert.IsTrue(game.Assign(cutCustomer, 1));
    }

    [Test] public void CompletedStepKeepsCustomerAtOldStationUntilNextStationIsChosen()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(1, new List<ServiceType> { ServiceType.Wash, ServiceType.Dry, ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 0));
        ReachStation(game);
        Assert.IsTrue(game.ApplyService(customer, ServiceType.Wash, 1f));
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(0, customer.Station);
        Assert.IsTrue(game.IsStationOccupied(0));
        Assert.IsTrue(game.Assign(customer, 1));
        Assert.IsFalse(game.IsStationOccupied(0));
        Assert.IsTrue(game.IsStationOccupied(1));
    }

    [Test] public void SelectingCustomersSwitchesFocusWithoutStoppingWorldTime()
    {
        var game = new SalonGameModel();
        var first = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        var second = game.Spawn(2, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(first, 1));
        Assert.IsTrue(game.Assign(second, 2));
        ReachStation(game);

        game.SelectCustomer(first);
        Assert.AreEqual(SalonViewState.WorkstationFocus, game.ViewState);
        Assert.AreSame(first, game.SelectedCustomer);
        float before = second.Patience;
        float worldBefore = game.WorldElapsed;

        game.Tick(2f);
        game.SelectCustomer(second);

        Assert.AreSame(second, game.SelectedCustomer);
        Assert.Less(second.Patience, before);
        Assert.Greater(game.WorldElapsed, worldBefore);
    }

    [Test] public void ClearingFocusReturnsToOverviewWithoutChangingCustomerStates()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 1));
        game.SelectCustomer(customer);

        game.ClearFocus();

        Assert.AreEqual(SalonViewState.Overview, game.ViewState);
        Assert.IsNull(game.SelectedCustomer);
        Assert.AreEqual(CustomerState.MovingToStation, customer.State);
        Assert.IsTrue(game.IsRunning);
    }

    [Test] public void HaircutWorkstationExposesCuttingToolsAndManualDryer()
    {
        var station = WorkstationModel.Haircut(7);

        CollectionAssert.AreEqual(
            new[]
            {
                SalonTool.Scissors, SalonTool.ThinningShears, SalonTool.Clippers,
                SalonTool.BlowDryer, SalonTool.DyeBottle
            },
            station.AvailableTools);
        Assert.AreEqual(WorkstationType.Haircut, station.Type);
        Assert.IsNotNull(station.ServicePoint);
    }

    [Test] public void BackgroundTaskKeepsProgressingWhileAnotherCustomerIsFocused()
    {
        var game = new SalonGameModel();
        var processing = game.Spawn(1, new List<ServiceType> { ServiceType.Dye });
        var focused = game.Spawn(2, new List<ServiceType> { ServiceType.Cut });
        processing.BackgroundTask.Start(4f, 7f, 10f);
        game.SelectCustomer(focused);

        game.Tick(5f);

        Assert.AreEqual(5f, processing.BackgroundTask.Elapsed, .001f);
        Assert.AreEqual(BackgroundRiskState.Ideal, processing.BackgroundTask.RiskState);
    }

    [Test] public void PatienceIsExposedAsNormalizedVisualProgress()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(1, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game); game.Tick(25f);

        Assert.That(customer.PatienceProgress, Is.InRange(0f, 1f));
        Assert.AreEqual(customer.Patience / 100f, customer.PatienceProgress, .001f);
    }

    [Test] public void FixedMapSharesHairStationsForDyeAndKeepsPermIndependent()
    {
        var game = new SalonGameModel();
        var dyeCustomer = game.Spawn(1, new List<ServiceType> { ServiceType.Dye });
        var permCustomer = game.Spawn(2, new List<ServiceType> { ServiceType.Perm });
        FinishEntering(game);

        Assert.AreEqual(5, game.Workstations.Count);
        Assert.IsTrue(game.Assign(dyeCustomer, 1));
        Assert.IsTrue(game.Assign(permCustomer, 3));
        Assert.AreEqual(WorkstationType.Haircut, game.Workstations[1].Type);
        Assert.AreEqual(WorkstationType.Perm, game.Workstations[3].Type);
        Assert.AreNotSame(game.Workstations[1].ServicePoint, game.Workstations[3].ServicePoint);
    }

    [Test] public void PlayerBuildCarriesDedicatedLowPolyShader()
    {
        Assert.IsNotNull(Resources.Load<Shader>("SalonLowPoly"));
    }

    [Test] public void BackedLabelUsesSeparateGraphicsForPanelAndText()
    {
        var root = new GameObject("UI Test", typeof(RectTransform));
        var label = SalonUiFactory.CreateLabel("营业中", root.transform, Color.white, Color.black);

        Assert.IsNotNull(label);
        Assert.IsNotNull(label.GetComponent<Text>());
        Assert.IsNull(label.GetComponent<Image>());
        Assert.IsNotNull(label.transform.parent.GetComponent<Image>());

        Object.DestroyImmediate(root);
    }

    [Test]
    public void QuickPerfectServiceIsHappyAndCreatesATip()
    {
        var game = new SalonGameModel(experienceProfile: new CustomerExperienceProfile { HappyThreshold = 75f });
        var customer = game.Spawn(300, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual("HappyCompletion", CustomerServiceResultName(customer));
        Assert.AreEqual(CustomerEmotion.Happy, customer.Emotion);
        Assert.Greater(PaymentValue(game.Payments.Drops[0], "TipReward"), 0);
        Assert.AreEqual(PaymentValue(game.Payments.Drops[0], "BaseReward") +
            PaymentValue(game.Payments.Drops[0], "TipReward"), game.Payments.Drops[0].Amount);
    }

    [Test]
    public void CorrectButDelayedServiceIsNormalWithoutHappyEmotionOrTip()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(301, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        game.Tick(15f);
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion,
            "This case is delayed but has not yet become impatient.");
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual("NormalCompletion", CustomerServiceResultName(customer));
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion);
        Assert.AreEqual(0, PaymentValue(game.Payments.Drops[0], "TipReward"));
    }

    [Test]
    public void RecoveredServiceIsNormalWithoutTip()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(302, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);
        Assert.IsTrue(game.Assign(customer, 1));
        ReachStation(game);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);
        var config = new HaircutConfig();

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Undercut, config));
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, config));

        Assert.AreEqual("NormalCompletion", CustomerServiceResultName(customer));
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion);
        Assert.AreEqual(0, PaymentValue(game.Payments.Drops[0], "TipReward"));
    }

    private static string CustomerServiceResultName(CustomerModel customer)
    {
        var field = typeof(CustomerModel).GetField("ServiceResult");
        Assert.IsNotNull(field);
        object value = field.GetValue(customer);
        return value == null ? string.Empty : value.ToString();
    }

    private static int PaymentValue(PaymentDropModel drop, string fieldName)
    {
        var field = typeof(PaymentDropModel).GetField(fieldName);
        Assert.IsNotNull(field);
        return (int)field.GetValue(drop);
    }

    [Test] public void PrimitiveFactoryPlacesPartsRelativeToTheirEntityRoot()
    {
        var root = new GameObject("Entity Root");
        root.transform.position = new Vector3(10f, 2f, -4f);

        var part = SalonPrimitiveFactory.CreateCube(root.transform, new Vector3(1f, 2f, 3f), Vector3.one, Color.red);

        Assert.AreEqual(new Vector3(1f, 2f, 3f), part.transform.localPosition);
        Assert.AreEqual(new Vector3(11f, 4f, -1f), part.transform.position);
        Object.DestroyImmediate(root);
    }

    [Test] public void DemandRingUsesCircularSpriteInsteadOfSquarePanel()
    {
        var sprite = SalonUiFactory.GetCircleSprite();
        var texture = sprite.texture;

        Assert.AreEqual(0f, texture.GetPixel(0, 0).a, .001f);
        Assert.AreEqual(1f, texture.GetPixel(texture.width / 2, texture.height / 2).a, .001f);
    }

    [Test] public void DecorativeFullscreenUiDoesNotBlockWorldClicks()
    {
        var overlay = new GameObject("Transparent Overlay", typeof(RectTransform), typeof(Image));
        var image = overlay.GetComponent<Image>();
        image.raycastTarget = true;

        SalonUiFactory.MakeClickThrough(overlay);

        Assert.IsFalse(image.raycastTarget);
        Object.DestroyImmediate(overlay);
    }

    [Test]
    public void MultiCustomerSealPressureFlowKeepsWorldAndPickupsIndependent()
    {
        var game = new SalonGameModel();
        var paid = game.Spawn(400, new List<ServiceType> { ServiceType.Cut });
        var active = game.Spawn(401, new List<ServiceType> { ServiceType.Cut });
        var waiting = game.Spawn(402, new List<ServiceType> { ServiceType.Cut });
        FinishEntering(game);

        Assert.IsTrue(game.Assign(paid, 1));
        ReachStation(game);
        Assert.IsTrue(game.ApplyService(paid, ServiceType.Cut, 1f));
        PaymentDropModel paidDrop = game.Payments.Drops[0];
        Assert.AreEqual(PaymentDropState.Pending, paidDrop.State);

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);
        Assert.IsTrue(game.Assign(active, 1));
        ReachStation(game);
        game.Tick(40f);
        Assert.AreEqual(CustomerEmotion.Impatient, waiting.Emotion);
        var entering = game.Spawn(403, new List<ServiceType> { ServiceType.Cut });
        Assert.AreEqual(CustomerState.Entering, entering.State);

        float worldBefore = game.WorldElapsed;
        game.SelectCustomer(active);
        game.Tick(.2f);
        game.SelectCustomer(waiting);
        game.ClearFocus();
        Assert.IsTrue(game.Payments.BeginCollection(paidDrop.Id));
        Assert.IsTrue(game.Payments.CompleteCollection(paidDrop.Id));
        int balanceAfterPaid = game.Balance;
        Assert.IsFalse(game.Payments.CompleteCollection(paidDrop.Id));
        game.SelectCustomer(entering);
        game.SelectCustomer(active);
        Assert.Greater(game.WorldElapsed, worldBefore);

        Assert.IsTrue(game.ApplyService(active, ServiceType.Cut, 1f));
        PaymentDropModel activeDrop = game.Payments.Drops[1];
        Assert.AreEqual(PaymentDropState.Pending, activeDrop.State);
        Assert.AreEqual(balanceAfterPaid, game.Balance);

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);
        Assert.IsTrue(game.Assign(waiting, 1));
        ReachStation(game);
        Assert.IsTrue(game.ApplyService(waiting, ServiceType.Cut, 1f));
        PaymentDropModel waitingDrop = game.Payments.Drops[2];

        Assert.AreNotEqual(activeDrop.Id, waitingDrop.Id);
        Assert.AreEqual(PaymentDropState.Pending, activeDrop.State);
        Assert.AreEqual(PaymentDropState.Pending, waitingDrop.State);
        Assert.AreEqual(balanceAfterPaid, game.Balance,
            "Pending pickups must not credit currency until each is collected.");
    }
}
