using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public sealed class Phase6RuntimeViewTests
{
    [Test]
    public void OneOrderBubbleShowsAllServiceStepsAndCompletionState()
    {
        var root = new GameObject("Order Bubble Test");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry }
        };
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);

        Assert.AreEqual(3, view.StepCount);
        Assert.AreEqual(0, view.ActiveStepIndex);
        Assert.IsFalse(view.IsStepChecked(0));

        customer.Step = 1;
        view.Refresh();

        Assert.IsTrue(view.IsStepChecked(0));
        Assert.AreEqual(1, view.ActiveStepIndex);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void TowelWrapIsAVisualStateOnTheCustomerNotAnOrderStep()
    {
        var root = new GameObject("Customer Towel Test");
        var view = root.AddComponent<SalonCustomerView>();
        view.SetTowelWrapped(true);

        Assert.IsTrue(view.IsTowelVisualVisible);

        view.SetTowelWrapped(false);
        Assert.IsFalse(view.IsTowelVisualVisible);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void ActiveWashStepUsesANeutralTrackAndVisibleRunningArc()
    {
        var root = new GameObject("Wash Progress Test");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Wash, ServiceType.Cut }
        };
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);
        view.SetServiceProgress(.5f, SalonPalette.Success);

        Assert.AreEqual(.5f, view.GetServiceProgress(0), .001f);
        Assert.AreEqual(SalonPalette.Track, view.GetTrackColor(0));
        Object.DestroyImmediate(root);
    }

    [Test]
    public void WashCustomerAnchorLiesAboveAndTowardTheFootOfThePrimaryWashBed()
    {
        Assert.AreEqual(SalonDemo.PrimaryWashBedPosition.x, SalonDemo.WashCustomerAnchorPosition.x, .001f);
        Assert.GreaterOrEqual(SalonDemo.WashCustomerAnchorPosition.y,
            SalonDemo.PrimaryWashBedPosition.y + 1.25f,
            "The rotated body root must clear the top surface instead of sinking into the bed mesh.");
        Assert.Less(SalonDemo.WashCustomerAnchorPosition.z, SalonDemo.PrimaryWashBedPosition.z);
        Assert.Greater(Vector3.Distance(SalonDemo.WashCustomerAnchorPosition,
            SalonDemo.WashPlayerAnchorPosition), 1f);
    }

    [Test]
    public void SecondaryWashBedExposesItsOwnCustomerAndPlayerAnchors()
    {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
        var bedField = typeof(SalonDemo).GetField("SecondaryWashBedPosition", flags);
        var customerField = typeof(SalonDemo).GetField("SecondaryWashCustomerAnchorPosition", flags);
        var playerField = typeof(SalonDemo).GetField("SecondaryWashPlayerAnchorPosition", flags);

        Assert.IsNotNull(bedField);
        Assert.IsNotNull(customerField);
        Assert.IsNotNull(playerField);
    }

    [Test]
    public void CustomerUsesALyingPoseAfterReachingTheWashStation()
    {
        var root = new GameObject("Wash Pose Test");
        var view = root.AddComponent<SalonCustomerView>();

        view.ApplyStationPose(CustomerState.Serving, true, WorkstationType.Wash);

        Assert.IsTrue(view.IsWashPose);
        Assert.Greater(Quaternion.Angle(Quaternion.identity, root.transform.localRotation), 45f);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void AStageWithOneValidActionStillRequiresThePlayerToSelectIt()
    {
        var method = typeof(SalonDemo).GetMethod("GetDefaultHoldAction",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method);
        var customer = new CustomerModel
        {
            State = CustomerState.Serving,
            Needs = new List<ServiceType> { ServiceType.Wash },
            WashStage = WashStage.ReadyToWrap
        };

        Assert.AreEqual(ActiveServiceAction.None,
            (ActiveServiceAction)method.Invoke(null, new object[] { customer }));
    }

    [Test]
    public void CustomerAwaitingAnotherStationDoesNotOfferTheNextStationsHoldActionYet()
    {
        var customer = new CustomerModel
        {
            State = CustomerState.Serving,
            Needs = new List<ServiceType> { ServiceType.Wash, ServiceType.Cut },
            Step = 1,
            Station = 0,
            TowelWrapped = true
        };

        Assert.AreEqual(ActiveServiceAction.None, SalonDemo.GetDefaultHoldAction(customer));
    }

    [Test]
    public void ServiceTimingFeedbackIsYellowUntilValidAndShampooHasNoOverdueState()
    {
        MethodInfo method = typeof(SalonDemo).GetMethod("GetActiveServiceProgressColor",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "All hold services need one shared threshold-color rule.");
        var config = new SalonServiceConfig { ShampooDuration = 2f, ManualBlowGoodStart = 3f, ManualBlowGoodEnd = 5f };

        Assert.AreEqual(SalonPalette.Warning,
            (Color)method.Invoke(null, new object[] { ActiveServiceAction.Shampoo, 1f, 2f, config }));
        Assert.AreEqual(SalonPalette.Success,
            (Color)method.Invoke(null, new object[] { ActiveServiceAction.Shampoo, 2f, 2f, config }));
        Assert.AreEqual(SalonPalette.Success,
            (Color)method.Invoke(null, new object[] { ActiveServiceAction.Shampoo, 20f, 2f, config }),
            "Shampoo application has no timeout state.");
        Assert.AreEqual(SalonPalette.Warning,
            (Color)method.Invoke(null, new object[] { ActiveServiceAction.ManualBlow, 2f, 5f, config }));
        Assert.AreEqual(SalonPalette.Success,
            (Color)method.Invoke(null, new object[] { ActiveServiceAction.ManualBlow, 4f, 5f, config }));
    }

    [Test]
    public void BackgroundWaitingShowsAutomaticProgressWithoutNumericCountdown()
    {
        var root = new GameObject("Foam Wait Progress Test");
        var customer = new CustomerModel { Needs = new List<ServiceType> { ServiceType.Wash } };
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);
        MethodInfo setWaitProgress = typeof(OrderDemandBubbleView).GetMethod("SetBackgroundWaitProgress");
        MethodInfo getCountdown = typeof(OrderDemandBubbleView).GetMethod("GetServiceCountdownText");
        Assert.IsNotNull(setWaitProgress);
        Assert.IsNotNull(getCountdown);

        setWaitProgress.Invoke(view, new object[] { .5f, SalonPalette.Warning });
        Assert.AreEqual(.5f, view.GetServiceProgress(0), .001f);
        Assert.AreEqual(string.Empty, (string)getCountdown.Invoke(view, new object[] { 0 }));
        Object.DestroyImmediate(root);
    }
}
