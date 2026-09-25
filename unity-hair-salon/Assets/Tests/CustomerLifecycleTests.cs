using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class CustomerLifecycleTests
{
    [Test]
    public void SpawnedCustomerEntersBeforeJoiningTheWaitingQueue()
    {
        var game = new SalonGameModel();

        CustomerModel customer = game.Spawn(101, new List<ServiceType> { ServiceType.Cut });

        Assert.AreEqual(CustomerState.Entering, customer.State);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);
    }

    [Test]
    public void AssignmentMovesCustomerToStationBeforeServiceCanStart()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnWaiting(game, 102);

        Assert.IsTrue(game.Assign(customer, 1));
        Assert.AreEqual(CustomerState.MovingToStation, customer.State);
        Assert.IsFalse(game.ApplyService(customer, ServiceType.Cut, 1f));

        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
    }

    [Test]
    public void CompletedServiceShowsFeedbackThenLeavesAndExits()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 103);

        Assert.IsTrue(game.ApplyService(customer, ServiceType.Cut, 1f));

        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(CustomerServiceFeedback.Satisfied, customer.ServiceFeedback);
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion,
            "A correct short order is satisfied, but does not automatically cross the happy threshold.");
        Assert.AreEqual(1, game.Payments.Drops.Count);

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);
        Assert.AreEqual(CustomerState.Leaving, customer.State);
        game.Tick(SalonGameModel.LeavingSeconds + .01f);
        Assert.AreEqual(CustomerState.Exited, customer.State);
        CollectionAssert.DoesNotContain(game.Customers, customer);
    }

    [Test]
    public void FinishedCustomerKeepsSeatReservedUntilLeavingStarts()
    {
        var game = new SalonGameModel();
        CustomerModel finished = game.Spawn(110, new List<ServiceType> { ServiceType.Cut });
        CustomerModel next = game.Spawn(111, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(finished, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(game.ApplyService(finished, ServiceType.Cut, 1f));

        Assert.AreEqual(CustomerState.Finished, finished.State);
        Assert.AreEqual(1, finished.Station);
        Assert.AreEqual(finished.Id, game.Workstations[1].CurrentCustomerId);
        Assert.AreEqual(WorkstationState.Completed, game.Workstations[1].State);
        Assert.IsFalse(game.Assign(next, 1),
            "The next customer must not enter while the finished customer is still seated.");

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);

        Assert.AreEqual(CustomerState.Leaving, finished.State);
        Assert.AreEqual(-1, finished.Station);
        Assert.AreEqual(WorkstationState.Available, game.Workstations[1].State);
        Assert.IsTrue(game.Assign(next, 1));
    }

    [Test]
    public void LeavingAfterFeedbackAlwaysClearsTheFocusedCustomer()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 109);
        game.SelectCustomer(customer);
        Assert.IsTrue(game.ApplyService(customer, ServiceType.Cut, 1f));

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.IsNull(game.SelectedCustomer);
        Assert.AreEqual(SalonViewState.Overview, game.ViewState);
    }

    [Test]
    public void PatienceChangesOnlyEmotionAndDoesNotChangeServiceRequest()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnWaiting(game, 104);
        ServiceType originalRequest = customer.CurrentNeed;

        game.Tick(40f);
        Assert.AreEqual(CustomerEmotion.Impatient, customer.Emotion);
        Assert.AreEqual(originalRequest, customer.CurrentNeed);

        game.Tick(30f);
        Assert.AreEqual(CustomerEmotion.Angry, customer.Emotion);
        Assert.AreEqual(originalRequest, customer.CurrentNeed);
    }

    [Test]
    public void AnUnservedCustomerLeavesAngryWithoutCreatingPayment()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnWaiting(game, 105);

        game.Tick(100f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.AreEqual(CustomerEmotion.Angry, customer.Emotion);
        Assert.AreEqual(CustomerServiceFeedback.Dissatisfied, customer.ServiceFeedback);
        Assert.AreEqual(1, game.AngryLeaves);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void FocusDoesNotPauseOtherCustomersLifecycle()
    {
        var game = new SalonGameModel();
        CustomerModel focused = game.Spawn(106, new List<ServiceType> { ServiceType.Cut });
        CustomerModel background = game.Spawn(107, new List<ServiceType> { ServiceType.Cut });
        game.SelectCustomer(focused);

        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.AreEqual(CustomerState.Waiting, background.State);
        Assert.Greater(game.WorldElapsed, 0f);
    }

    [Test]
    public void ActiveOperationPausesNearCriticalPatienceUntilServiceCompletes()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 120);
        customer.Patience = .2f;

        Assert.IsTrue(InvokeAttention(game, "BeginActiveOperation", customer));
        float relievedPatience = customer.Patience;
        Assert.Greater(relievedPatience, .2f);
        Assert.Less(relievedPatience, 100f, "Starting service must not fully refill patience.");

        game.Tick(20f);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(relievedPatience, customer.Patience, .001f,
            "Waiting patience must be paused throughout the atomic operation.");
        Assert.IsTrue(game.ApplyService(customer, ServiceType.Cut, 1f));
        InvokeAttention(game, "EndActiveOperation", customer);
        Assert.AreEqual(CustomerState.Finished, customer.State);
    }

    [Test]
    public void ReturningAfterAnOperationDoesNotCauseImmediateLeave()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 121);
        customer.Patience = 1f;
        Assert.IsTrue(InvokeAttention(game, "BeginActiveOperation", customer));
        InvokeAttention(game, "EndActiveOperation", customer);
        float afterRelief = customer.Patience;

        game.Tick(1f);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(afterRelief, customer.Patience, .001f);
        Assert.IsTrue(InvokeAttention(game, "BeginActiveOperation", customer));
        Assert.AreEqual(CustomerState.Serving, customer.State);
    }

    [Test]
    public void FormalServiceEventuallyLeavesAfterTheDelayGraceIsSpent()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 122);
        Assert.IsTrue(InvokeAttention(game, "BeginActiveOperation", customer));
        InvokeAttention(game, "EndActiveOperation", customer);
        customer.Patience = 1f;

        game.Tick(5f);
        game.Tick(20f);

        Assert.AreEqual(CustomerState.Exited, customer.State);
        Assert.AreEqual(0f, customer.Patience, .001f);
        Assert.AreEqual(1, game.AngryLeaves);
    }

    [Test]
    public void FormallyEngagedCustomerDoesNotLeaveWhenAnOperationEnds()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 123);
        Assert.IsTrue(InvokeAttention(game, "BeginActiveOperation", customer));
        customer.Patience = 0f;

        game.Tick(1f);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        InvokeAttention(game, "EndActiveOperation", customer);
        Assert.AreEqual(CustomerState.Serving, customer.State);
    }

    [Test]
    public void EmotionBadgeIsHiddenWhenCalmAndNeverUsesDevelopmentWords()
    {
        var root = new GameObject("Emotion Badge Test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var customer = new CustomerModel();
        var view = root.AddComponent<CustomerEmotionView>();
        view.Initialize(customer, cameraObject.GetComponent<Camera>());

        view.Refresh();
        Assert.IsFalse(view.IsVisible);

        customer.Emotion = CustomerEmotion.Impatient;
        view.Refresh();
        Assert.IsTrue(view.IsVisible);
        Assert.AreEqual(string.Empty, view.Symbol);

        customer.Emotion = CustomerEmotion.Happy;
        view.Refresh();
        Assert.AreEqual(string.Empty, view.Symbol);

        customer.Emotion = CustomerEmotion.Angry;
        view.Refresh();
        Assert.AreEqual(string.Empty, view.Symbol);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void HaircutRequestBubbleDoesNotContainAPatienceBar()
    {
        var root = new GameObject("Demand Bubble Rule Test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var view = root.AddComponent<HaircutDemandBubbleView>();
        view.Initialize(new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears), cameraObject.GetComponent<Camera>());

        Assert.IsNull(FindDescendant(root.transform, "Patience Track"));

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void NearZeroPatienceCannotInterruptGuidanceToAWorkstation()
    {
        var game = new SalonGameModel(
            patienceConfig: new CustomerPatienceConfig { DrainPerSecond = 100f });
        CustomerModel customer = SpawnWaiting(game, 130);
        customer.Patience = .01f;
        customer.Emotion = CustomerEmotion.Angry;

        Assert.IsTrue(game.Assign(customer, 1));
        float committedPatience = customer.Patience;
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(committedPatience, customer.Patience, .001f);
        Assert.AreEqual(customer.Id, game.Workstations[1].CurrentCustomerId);
        Assert.AreEqual(0, game.AngryLeaves);
        Assert.IsTrue(customer.WasImpatientBeforeService,
            "Guidance protection must not erase the customer's waiting history.");
    }

    [Test]
    public void ArrivalGracePreventsImmediateLeaveButExpiresIfServiceNeverStarts()
    {
        var patience = new CustomerPatienceConfig
        {
            DrainPerSecond = 100f,
            ServiceArrivalGraceSeconds = 1.5f
        };
        var game = new SalonGameModel(patienceConfig: patience);
        CustomerModel customer = SpawnWaiting(game, 131);
        customer.Patience = .01f;

        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.Tick(patience.ServiceArrivalGraceSeconds);

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(.01f, customer.Patience, .001f);

        game.Tick(.01f);
        Assert.AreEqual(CustomerState.Leaving, customer.State,
            "An unserved customer may leave only after guidance and the arrival grace are over.");
    }

    private static CustomerModel SpawnWaiting(SalonGameModel game, int id)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);
        return customer;
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id)
    {
        CustomerModel customer = SpawnWaiting(game, id);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
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

    private static bool InvokeAttention(SalonGameModel game, string methodName, CustomerModel customer)
    {
        var method = typeof(SalonGameModel).GetMethod(methodName);
        Assert.IsNotNull(method, methodName + " must be part of the service-attention state machine.");
        object result = method.Invoke(game, new object[] { customer });
        return result is bool value ? value : true;
    }
}
