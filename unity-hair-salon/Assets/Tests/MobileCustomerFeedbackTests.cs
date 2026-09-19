using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Regression coverage for the always-readable customer feedback that the mobile flow needs.
/// These tests intentionally stay at the view boundary: the customer model remains the source
/// of truth for order, patience and service state.
/// </summary>
public sealed class MobileCustomerFeedbackTests
{
    [Test]
    public void WrongServiceRefreshOnInactiveEmotionHierarchyDoesNotStartCoroutineOrLogError()
    {
        var emotionSlot = new GameObject("EmotionSlot");
        var view = emotionSlot.AddComponent<CustomerEmotionView>();
        var customer = new CustomerModel
        {
            Id = 901,
            Needs = new List<ServiceType> { ServiceType.Cut },
            ReactionKind = CustomerReactionKind.Protest,
            ReactionRemaining = 1f,
            WrongServiceKind = CustomerWrongServiceKind.Wash
        };

        view.Initialize(customer, null);
        emotionSlot.SetActive(false);

        var coroutineErrors = new List<string>();
        Application.LogCallback callback = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error && condition.IndexOf("Coroutine couldn't be started", StringComparison.Ordinal) >= 0)
                coroutineErrors.Add(condition);
        };
        Application.logMessageReceived += callback;
        bool oldIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
        LogAssert.ignoreFailingMessages = true;
        try
        {
            // This is the captured reproduction from the gameplay review: the view can receive a
            // state refresh while its EmotionSlot ancestor is inactive.
            view.Refresh();
        }
        finally
        {
            LogAssert.ignoreFailingMessages = oldIgnoreFailingMessages;
            Application.logMessageReceived -= callback;
        }

        Assert.IsEmpty(coroutineErrors,
            "Refreshing a wrong-service reaction below an inactive EmotionSlot must not call StartCoroutine.");
        Object.DestroyImmediate(emotionSlot);
    }

    [Test]
    public void DisablingEmotionHierarchyClearsWrongServicePulseBeforeTheNextRefresh()
    {
        var emotionSlot = new GameObject("EmotionSlot Cleanup");
        var view = emotionSlot.AddComponent<CustomerEmotionView>();
        var customer = new CustomerModel
        {
            Id = 902,
            Needs = new List<ServiceType> { ServiceType.Wash },
            ReactionKind = CustomerReactionKind.Protest,
            ReactionRemaining = 1f,
            WrongServiceKind = CustomerWrongServiceKind.Dry
        };

        view.Initialize(customer, null);
        emotionSlot.SetActive(false);
        view.Refresh();

        FieldInfo pulse = typeof(CustomerEmotionView).GetField("_wrongServicePulse",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(pulse, "The wrong-service feedback needs one lifecycle-owned coroutine handle.");
        Assert.IsNull(pulse.GetValue(view),
            "A hidden customer refresh must release the reaction pulse before the next active refresh.");

        Object.DestroyImmediate(emotionSlot);
    }

    [Test]
    public void WaitingCustomerFeedbackKeepsRequirementAndPatienceReadable()
    {
        CustomerModel customer = NewCustomer(CustomerState.Waiting, 62f);
        Component urgency = CreateUrgencyView(customer);

        RefreshUrgency(urgency);

        Assert.IsTrue(ReadBool(urgency, "IsVisible"), "A waiting customer's compact status must stay visible.");
        StringAssert.Contains("62", ReadString(urgency, "PatienceText"));
        Assert.AreEqual(.62f, ReadFloat(urgency, "PatienceProgress"), .001f);
        StringAssert.Contains("等待", ReadString(urgency, "StatusText"));
        Assert.IsTrue(FindDescendant(urgency.transform, "Customer Urgency Status") != null,
            "The mobile status must have a single compact world-space root beside the existing order bubble.");

        Object.DestroyImmediate(urgency.gameObject);
    }

    [Test]
    public void UnfinishedOrderWithClearPhysicalStateStillShowsTheActiveService()
    {
        CustomerModel customer = NewCustomer(CustomerState.Serving, 78f);
        // ExitReady is a physical-state projection and can already be true during a haircut.
        customer.ExitReady = true;
        customer.ExitBlockReason = ExitBlockReason.None;
        customer.ServicePhase = ServiceStepPhase.Active;
        Component urgency = CreateUrgencyView(customer);
        RefreshUrgency(urgency);
        Assert.AreEqual("服务中", ReadString(urgency, "StatusText"));
        Object.DestroyImmediate(urgency.gameObject);
    }

    [Test]
    public void CustomerFeedbackLabelsBackgroundRunningCanLeaveCleanupAndTimeout()
    {
        CustomerModel customer = NewCustomer(CustomerState.Serving, 78f);
        Component urgency = CreateUrgencyView(customer);

        customer.ProcessStage = ServiceProcessStage.DyeProcessing;
        customer.ProcessingDuration = 10f;
        customer.ProcessingElapsed = 3f;
        RefreshUrgency(urgency);
        StringAssert.Contains("后台", ReadString(urgency, "StatusText"));

        customer.ProcessStage = ServiceProcessStage.None;
        customer.Step = customer.Needs.Count;
        customer.ExitReady = true;
        customer.ExitBlockReason = ExitBlockReason.None;
        RefreshUrgency(urgency);
        StringAssert.Contains("可离开", ReadString(urgency, "StatusText"));

        customer.ExitReady = false;
        customer.ExitBlockReason = ExitBlockReason.FoamRemaining;
        RefreshUrgency(urgency);
        StringAssert.Contains("收尾", ReadString(urgency, "StatusText"));

        customer.State = CustomerState.Waiting;
        customer.Patience = 0f;
        customer.Emotion = CustomerEmotion.Angry;
        RefreshUrgency(urgency);
        StringAssert.Contains("超时", ReadString(urgency, "StatusText"));

        Object.DestroyImmediate(urgency.gameObject);
    }

    [Test]
    public void CustomerUiRootCreatesASeparateResidentUrgencyContainer()
    {
        var owner = new GameObject("Mobile Customer UI Owner");
        var ui = owner.AddComponent<CustomerUIRootView>();
        ui.Initialize(NewCustomer(CustomerState.Waiting, 80f));

        Transform urgencyContainer = FindDescendant(owner.transform, "Customer Urgency Status");
        Assert.IsNotNull(urgencyContainer,
            "Waiting and service state feedback must have a separate compact slot; the order bubble keeps its approved art.");
        Assert.AreNotSame(ui.RequirementContainer, urgencyContainer,
            "Patience/status feedback must not be folded into the approved demand bubble artwork.");

        Object.DestroyImmediate(owner);
    }

    private static CustomerModel NewCustomer(CustomerState state, float patience)
    {
        return new CustomerModel
        {
            Id = 903,
            Needs = new List<ServiceType> { ServiceType.Cut },
            State = state,
            Patience = patience,
            MaxPatience = 100f,
            ExitBlockReason = ExitBlockReason.RequirementsIncomplete
        };
    }

    private static Component CreateUrgencyView(CustomerModel customer)
    {
        Type type = typeof(CustomerUIRootView).Assembly.GetType("CustomerUrgencyView");
        Assert.IsNotNull(type,
            "Mobile customer feedback should be implemented as a dedicated CustomerUrgencyView.");
        var root = new GameObject("Urgency View Test");
        Component view = root.AddComponent(type);
        MethodInfo initialize = type.GetMethod("Initialize", new[] { typeof(CustomerModel), typeof(Camera) });
        Assert.IsNotNull(initialize, "CustomerUrgencyView.Initialize(CustomerModel, Camera) is the view boundary.");
        initialize.Invoke(view, new object[] { customer, null });
        return view;
    }

    private static void RefreshUrgency(Component view)
    {
        MethodInfo refresh = view.GetType().GetMethod("Refresh", Type.EmptyTypes);
        Assert.IsNotNull(refresh, "CustomerUrgencyView must expose an explicit state refresh.");
        refresh.Invoke(view, null);
    }

    private static string ReadString(Component view, string propertyName)
    {
        PropertyInfo property = view.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, propertyName);
        return (string)property.GetValue(view);
    }

    private static bool ReadBool(Component view, string propertyName)
    {
        PropertyInfo property = view.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, propertyName);
        return (bool)property.GetValue(view);
    }

    private static float ReadFloat(Component view, string propertyName)
    {
        PropertyInfo property = view.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, propertyName);
        return (float)property.GetValue(view);
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
