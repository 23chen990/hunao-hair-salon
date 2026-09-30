using System.Collections.Generic;
using NUnit.Framework;
using HairSalon;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression coverage for the compact mobile queue feedback. The queue must
/// keep the existing demand icons, while making the customer state and
/// patience readable at the approved 844x390 landscape viewport.
/// </summary>
public sealed class FirstSessionFeedbackTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "First Session Feedback Root",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void MobileQueueCardsNameWaitingStateAndUseReadableType()
    {
        SalonMobileQueueView queue = SalonMobileQueueView.Create(_root.transform);
        CustomerModel waiting = NewCustomer(0, CustomerState.Waiting, 76f, ServiceType.Wash);
        CustomerModel entering = NewCustomer(1, CustomerState.Entering, 91f, ServiceType.Cut);

        queue.Refresh(new List<CustomerModel> { waiting, entering });

        Assert.That(queue.VisibleCardCount, Is.EqualTo(2));
        Assert.That(SalonMobileQueueView.CardWidth, Is.GreaterThanOrEqualTo(320f));
        Assert.That(SalonMobileQueueView.CardHeight, Is.GreaterThanOrEqualTo(90f));

        Transform first = FindChild(queue.transform, "Mobile Queue Card 1");
        Transform second = FindChild(queue.transform, "Mobile Queue Card 2");
        Text firstCustomer = FindNamedText(first, "Customer Number");
        Text secondCustomer = FindNamedText(second, "Customer Number");
        Text firstPatience = FindNamedText(first, "Patience Value");
        Assert.That(firstCustomer, Is.Not.Null);
        Assert.That(firstCustomer.text, Is.EqualTo("1号\n待接"));
        Assert.That(secondCustomer.text, Is.EqualTo("2号\n进场"));
        Assert.That(firstPatience, Is.Not.Null);
        Assert.That(firstPatience.fontSize, Is.GreaterThanOrEqualTo(32));
        Assert.That(firstPatience.resizeTextMinSize, Is.GreaterThanOrEqualTo(22));
        StringAssert.Contains("76", firstPatience.text);
    }

    [Test]
    public void AlreadyEngagedWaitingCustomerIsMarkedAsAccepted()
    {
        SalonMobileQueueView queue = SalonMobileQueueView.Create(_root.transform);
        CustomerModel customer = NewCustomer(0, CustomerState.Waiting, 84f, ServiceType.Wash);
        customer.HasServiceEngaged = true;

        queue.Refresh(new List<CustomerModel> { customer });

        Text customerLabel = FindNamedText(
            FindChild(queue.transform, "Mobile Queue Card 1"), "Customer Number");
        Assert.That(customerLabel, Is.Not.Null);
        Assert.That(customerLabel.text, Is.EqualTo("1号\n已接"),
            "A greeted customer can remain Waiting while being guided to a station; do not invite a second greeting.");
    }

    private static CustomerModel NewCustomer(int id, CustomerState state, float patience,
        ServiceType service)
    {
        return new CustomerModel
        {
            Id = id,
            State = state,
            Patience = patience,
            MaxPatience = 100f,
            Needs = new List<ServiceType> { service }
        };
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Text FindNamedText(Transform root, string name)
    {
        Transform target = FindChild(root, name);
        return target == null ? null : target.GetComponent<Text>();
    }
}
