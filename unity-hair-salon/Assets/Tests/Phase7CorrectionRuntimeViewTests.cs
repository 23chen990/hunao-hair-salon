using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class Phase7CorrectionRuntimeViewTests
{
    [Test]
    public void RuntimeBuildsPreOpenStartButtonReputationAndManagementFunds()
    {
        var root = new GameObject("Corrected Phase7 Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        Assert.AreEqual(DayState.PreOpen, Controller(demo).State);
        Assert.IsNotNull(Field(demo, "_startBusinessButton"));
        Assert.IsNotNull(Field(demo, "_reputationLabel"));
        Assert.IsNotNull(Field(demo, "_managementFundsLabel"));
        Assert.IsTrue(((GameObject)Field(demo, "_dayStartPanel")).activeSelf);

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void StartBusinessButtonIsTheOnlyTransitionOutOfPreOpen()
    {
        var root = new GameObject("Corrected Phase7 Start Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        float remaining = Controller(demo).BusinessRemainingTime;
        Controller(demo).Tick(100f, 0);
        Assert.AreEqual(remaining, Controller(demo).BusinessRemainingTime, .001f);

        Invoke(demo, "StartBusinessDay");

        Assert.AreEqual(DayState.Business, Controller(demo).State);
        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void OpeningBusinessSpawnsExactlyOneCustomerImmediately()
    {
        var root = new GameObject("Immediate Opening Customer Runtime");
        var demo = root.AddComponent<SalonDemo>();
        demo.FlowSettings.InitialCustomerCount = 0;
        Invoke(demo, "Start");
        SalonGameModel game = (SalonGameModel)Field(demo, "_game");

        Invoke(demo, "StartBusinessDay");
        InvokeWithArgument(demo, "MaintainCustomerFlow", 0f);

        Assert.AreEqual(1, game.Customers.Count,
            "The first customer should arrive as soon as the player opens the salon.");
        InvokeWithArgument(demo, "MaintainCustomerFlow", 0f);
        Assert.AreEqual(1, game.Customers.Count,
            "Later customers must still respect the normal traffic interval.");

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void ResultCopyUsesOperatingLedgerAndNeverMentionsUncollectedIncome()
    {
        var root = new GameObject("Corrected Phase7 Result Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        Controller(demo).Stats.RecordPaymentCollected(new PaymentDropModel
        {
            Id = 22,
            OrderIncomeComponent = 100,
            TipIncomeComponent = 20,
            FinalPayment = 120
        });
        Controller(demo).ForceResult();

        string result = ((Text)Field(demo, "_resultSummaryLabel")).text;
        StringAssert.Contains("今日收支", result);
        StringAssert.Contains("订单收入", result);
        StringAssert.Contains("小费收入", result);
        StringAssert.Contains("今日营业净收入", result);
        StringAssert.Contains("完成订单", result);
        StringAssert.Contains("正常", result);
        StringAssert.DoesNotContain("未拾取", result);
        StringAssert.DoesNotContain("理论收入", result);

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void ClosedManagementUsesPlayerFacingLockedCopy()
    {
        var root = new GameObject("Corrected Phase7 Management Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        Controller(demo).ForceResult();
        Invoke(demo, "ContinueToShop");

        GameObject shop = (GameObject)Field(demo, "_shopPanel");
        string copy = string.Join("\n", System.Array.ConvertAll(shop.GetComponentsInChildren<Text>(true), x => x.text));
        StringAssert.Contains("闭店经营", copy);
        StringAssert.Contains("当前资金", copy);
        StringAssert.Contains("准备下一天", copy);
        StringAssert.DoesNotContain("尚未开发", copy);
        StringAssert.DoesNotContain("后续阶段", copy);

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void EnteringResultClearsPendingWorldPaymentsWithoutIncome()
    {
        var root = new GameObject("Corrected Phase7 Pickup Cleanup Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        SalonGameModel game = (SalonGameModel)Field(demo, "_game");
        PaymentDropModel drop = game.Payments.CreateFinalPayment(1, 1, 100, 20);
        InvokeWithArgument(demo, "HandlePaymentCreated", drop);
        Assert.AreEqual(1, game.Payments.Drops.Count);

        Controller(demo).ForceResult();

        Assert.AreEqual(0, game.Payments.Drops.Count);
        Assert.AreEqual(0, Controller(demo).Stats.OrderIncome);
        Assert.AreEqual(0, Controller(demo).Stats.TipIncome);
        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    private static BusinessDayController Controller(SalonDemo demo) =>
        (BusinessDayController)Field(demo, "_dayController");

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    private static void InvokeWithArgument(object target, string name, object argument) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target,
            new[] { argument });

    private static void DestroyRuntimeObjects()
    {
        foreach (GameObject item in UnityEngine.Object.FindObjectsByType<GameObject>())
            if (item != null && item.name != "Corrected Phase7 Runtime" &&
                item.name != "Corrected Phase7 Start Runtime" &&
                item.name != "Corrected Phase7 Result Runtime" &&
                item.name != "Corrected Phase7 Management Runtime" &&
                item.name != "Corrected Phase7 Pickup Cleanup Runtime")
                UnityEngine.Object.DestroyImmediate(item);
    }
}
