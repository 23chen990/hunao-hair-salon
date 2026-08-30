using System;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase7RuntimeViewTests
{
    [Test]
    public void ClockFormattingUsesCeilingAndNeverShowsNegativeTime()
    {
        Assert.AreEqual("03:00", SalonDemo.FormatClock(179.2f));
        Assert.AreEqual("00:01", SalonDemo.FormatClock(.01f));
        Assert.AreEqual("00:00", SalonDemo.FormatClock(-3f));
    }

    [Test]
    public void RuntimeOwnsDayDirectorStatsAndRequiredScreens()
    {
        Type demo = typeof(SalonDemo);
        Assert.IsNotNull(demo.GetField("DaySettings"));
        Assert.IsNotNull(demo.GetField("_dayController", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_trafficDirector", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_businessClockLabel", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_dayStartPanel", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_resultPanel", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_shopPanel", BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.IsNotNull(demo.GetField("_pausePanel", BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Test]
    public void StartingRuntimeBuildsDayResultShopAndPauseViews()
    {
        var root = new GameObject("Phase7 Runtime");
        var demo = root.AddComponent<SalonDemo>();
        demo.FlowSettings.InitialCustomerCount = 0;
        demo.DaySettings.StartingDuration = 2f;

        Invoke(demo, "Start");

        Assert.IsNotNull(Field(demo, "_dayStartPanel"));
        Assert.IsNotNull(Field(demo, "_resultPanel"));
        Assert.IsNotNull(Field(demo, "_shopPanel"));
        Assert.IsNotNull(Field(demo, "_pausePanel"));
        Assert.AreEqual(DayState.Starting, Controller(demo).State);
        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void ResultContinueOpensShopAndNextDayKeepsAutoBlowPurchase()
    {
        var root = new GameObject("Phase7 Flow Runtime");
        var demo = root.AddComponent<SalonDemo>();
        demo.FlowSettings.InitialCustomerCount = 0;
        Invoke(demo, "Start");
        SalonGameModel game = (SalonGameModel)Field(demo, "_game");
        game.SetFirstDayCompleteForDebug(true);
        Assert.IsTrue(game.PurchaseAutoBlowStand());
        Controller(demo).ForceResult();

        Invoke(demo, "ContinueToShop");
        Assert.AreEqual(DayState.Shop, Controller(demo).State);
        Invoke(demo, "BeginNextDay");

        Assert.AreEqual(2, Controller(demo).DayNumber);
        Assert.IsTrue(game.HasAutoBlowStand);
        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    private static BusinessDayController Controller(SalonDemo demo)
    {
        return (BusinessDayController)Field(demo, "_dayController");
    }

    private static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    private static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    }

    private static void DestroyRuntimeObjects()
    {
        foreach (GameObject item in UnityEngine.Object.FindObjectsByType<GameObject>())
            if (item != null && item.name != "Phase7 Runtime" && item.name != "Phase7 Flow Runtime")
                UnityEngine.Object.DestroyImmediate(item);
    }
}
