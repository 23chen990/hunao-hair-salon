using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;

public sealed class Phase6FinalAcceptanceTests
{
    [Test]
    public void WrongStationIsAllowedPenalizedOnceAndCanBeCorrected()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(1, new List<ServiceType> { ServiceType.Wash });
        Enter(game);
        float initial = customer.Satisfaction;

        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.Less(customer.Satisfaction, initial);
        float afterOnePenalty = customer.Satisfaction;
        float patience = customer.Patience;
        game.Tick(2f);
        Assert.AreEqual(1, customer.WrongStationCount,
            "Staying at one wrong station must not repeatedly register the same mistake.");
        Assert.AreEqual(afterOnePenalty, customer.Satisfaction,
            "Wrong-station waiting must not repeatedly drain satisfaction every tick.");
        Assert.Less(customer.Patience, patience);
        Assert.IsTrue(game.Assign(customer, 0));
        Assert.IsFalse(game.IsStationOccupied(1));
        Assert.IsTrue(game.IsStationOccupied(0));
    }

    [Test]
    public void WrongStationCanStartExtraServiceAndPatienceKeepsFallingAfterTheAction()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(2, new List<ServiceType> { ServiceType.Wash });
        Enter(game);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        float patience = customer.Patience;

        Assert.IsTrue(game.BeginActiveOperation(customer));
        Assert.IsTrue(customer.HasServiceEngaged);
        Assert.IsTrue(game.EndActiveOperation(customer));
        game.Tick(game.PatienceConfig.ServiceArrivalGraceSeconds + 1f);
        Assert.Less(customer.Patience, patience);
    }

    [Test]
    public void PatienceLossDoesNotDrainSatisfactionOnEveryWaitingTick()
    {
        var game = NewGame();
        CustomerModel customer = game.Spawn(3, new List<ServiceType> { ServiceType.Cut });
        Enter(game);
        float patience = customer.Patience;
        float satisfaction = customer.Satisfaction;

        game.Tick(5f);

        Assert.Less(customer.Patience, patience);
        Assert.AreEqual(satisfaction, customer.Satisfaction);
        FieldInfo lossField = typeof(CustomerModel).GetField("WaitingSatisfactionLoss");
        Assert.IsNotNull(lossField);
        Assert.Greater((float)lossField.GetValue(customer), 0f);
    }

    [Test]
    public void FinalExperienceTypesAndConfigurableRiskWindowsExist()
    {
        Assembly assembly = typeof(SalonGameModel).Assembly;
        Assert.IsNotNull(assembly.GetType("HairSalon.AccidentSeverity"));
        Type profile = assembly.GetType("HairSalon.CustomerExperienceProfile");
        Assert.IsNotNull(profile);
        Assert.IsNotNull(profile.GetField("InitialSatisfaction"));
        Assert.IsNotNull(profile.GetField("InitialPatience"));
        Assert.IsNotNull(profile.GetField("MaxPatience"));
        Assert.IsNotNull(profile.GetField("WaitingToSatisfactionRate"));
        Assert.IsNotNull(profile.GetField("WrongStationPenalty"));
        Assert.IsNotNull(profile.GetField("RecoveryCapMajor"));
        Assert.IsNotNull(typeof(SalonServiceConfig).GetField("FoamOptimalStart"));
        Assert.IsNotNull(typeof(SalonServiceConfig).GetField("FoamMinorLateThreshold"));
        Assert.IsNotNull(typeof(SalonServiceConfig).GetField("FoamModerateLateThreshold"));
        Assert.IsNotNull(typeof(SalonServiceConfig).GetField("ManualBlowGoodStart"));
        Assert.IsNotNull(typeof(SalonServiceConfig).GetField("AutoBlowSafetyStopTime"));
    }

    [Test]
    public void UnattendedFoamEscalatesIntoAnAccidentAndSlowsTheRinse()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 4, ServiceType.Wash, 0);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration);

        // 在理想窗口内回来：不该有任何事故。
        game.Tick(game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.AreEqual("None", Field(customer, "AccidentSeverity").ToString());
        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(customer));

        // 放着不管：泡沫迟到先升到 Minor，再升到 Moderate。
        game.Tick(game.ServiceConfig.FoamMinorLateThreshold + .01f);
        Assert.AreEqual("Minor", Field(customer, "AccidentSeverity").ToString());
        game.Tick(game.ServiceConfig.FoamModerateLateThreshold + .01f);
        Assert.AreEqual("Moderate", Field(customer, "AccidentSeverity").ToString());

        // 事故不是单纯扣分：冲洗被延长，玩家需要额外处理时间。
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.AreEqual(game.ServiceConfig.OverdueRinseDuration, customer.ActiveServiceDuration);
    }

    [Test]
    public void BaseBlowIsManualAndNeverRunsWhilePlayerIsAway()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 5, ServiceType.Dry, 1);
        MethodInfo start = typeof(SalonGameModel).GetMethod("StartManualBlow");
        MethodInfo tick = typeof(SalonGameModel).GetMethod("TickManualBlow");
        Assert.IsNotNull(start);
        Assert.IsNotNull(tick);
        Assert.IsTrue((bool)start.Invoke(game, new object[] { customer }));
        tick.Invoke(game, new object[] { customer, 3.5f });
        float held = (float)Field(customer, "ManualBlowElapsed");
        Invoke(game, "EndManualBlowHold", customer);
        game.Tick(5f);

        Assert.AreEqual(held, (float)Field(customer, "ManualBlowElapsed"), .001f);
        Assert.AreEqual(BackgroundTaskState.Inactive, customer.BackgroundTask.State);
    }

    [Test]
    public void AutoBlowProductIsAlwaysVisibleAndPurchaseChangesCapability()
    {
        var game = NewGame();
        object product = Property(game, "AutoBlowStandProduct");
        Assert.IsTrue((bool)Field(product, "Visible"));
        Assert.IsFalse((bool)Field(product, "Purchased"));
        Assert.IsFalse((bool)Invoke(game, "PurchaseAutoBlowStand"));
        Assert.IsTrue((bool)Field(product, "Visible"));
        Assert.IsNotEmpty((string)Field(product, "LockReason"));

        Invoke(game, "SetFirstDayCompleteForDebug", true);
        Assert.IsTrue((bool)Invoke(game, "PurchaseAutoBlowStand"));
        Assert.IsTrue((bool)Property(game, "HasAutoBlowStand"));
    }

    [Test]
    public void AutoBlowSafetyStopKeepsCustomerAndOrderAtTheChair()
    {
        var game = NewGame();
        Invoke(game, "SetFirstDayCompleteForDebug", true);
        Assert.IsTrue((bool)Invoke(game, "PurchaseAutoBlowStand"));
        CustomerModel customer = SpawnServing(game, 6, ServiceType.Dry, 1);
        Assert.IsTrue((bool)Invoke(game, "StartAutoBlow", customer));

        game.Tick((float)Field(game.ServiceConfig, "AutoBlowSafetyStopTime") + .1f);

        Assert.IsTrue((bool)Field(customer, "AutoBlowSafetyStopped"));
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.IsTrue(game.IsStationOccupied(1));
        Assert.IsFalse(customer.IsComplete);
        Assert.AreEqual("Moderate", Field(customer, "AccidentSeverity").ToString());
    }

    [Test]
    public void WrongStationToolUsesSeverityAndCompletedWashActionCanBeRelocatedMidStep()
    {
        var game = NewGame();
        CustomerModel wrong = SpawnServing(game, 7, ServiceType.Wash, 1);
        Assert.IsTrue(game.ApplyWrongStationTool(wrong, SalonTool.Scissors));
        Assert.AreEqual(AccidentSeverity.Major, wrong.AccidentSeverity);
        Assert.AreEqual(CustomerServiceResult.Failed, wrong.ServiceResult);

        CustomerModel washing = SpawnServing(game, 8, ServiceType.Wash, 0);
        Assert.IsTrue(game.BeginWashAction(washing, WashAction.Shower));
        game.TickActiveServiceAction(washing, game.ServiceConfig.RinseDuration);
        Assert.IsTrue(game.BeginWashAction(washing, WashAction.Shampoo));
        game.TickActiveServiceAction(washing, game.ServiceConfig.ShampooDuration);
        Assert.IsTrue(game.Assign(washing, 2),
            "An unfinished service step is customer state, not a station lock.");
        Assert.IsFalse(game.IsStationOccupied(0));
        Assert.IsTrue(game.IsStationOccupied(2));
        Assert.AreEqual(WashStage.FoamWait, washing.WashStage);
        Assert.AreEqual(BackgroundTaskState.Inactive, washing.BackgroundTask.State);
    }

    [Test]
    public void LockedOrUnaffordableShopProductStaysVisibleWithReason()
    {
        var game = NewGame();
        game.AutoBlowStandProduct.Price = game.Balance + 1;
        game.SetFirstDayCompleteForDebug(true);

        Assert.IsFalse(game.PurchaseAutoBlowStand());
        Assert.IsTrue(game.AutoBlowStandProduct.Visible);
        Assert.AreEqual("金币不足", game.AutoBlowStandProduct.LockReason);
    }

    [TestCase(40f, CustomerServiceResult.UnhappyCompletion)]
    [TestCase(20f, CustomerServiceResult.SevereUnhappyCompletion)]
    public void FinalSatisfactionBandsProduceUnhappyResultsButStillPay(
        float initialSatisfaction, CustomerServiceResult expected)
    {
        var game = new SalonGameModel(
            flowConfig: new SalonFlowConfig { MaxCustomers = 10, WaitingCapacity = 10 },
            experienceProfile: new CustomerExperienceProfile { InitialSatisfaction = initialSatisfaction });
        CustomerModel customer = SpawnServing(game, 9, ServiceType.Cut, 1);

        Assert.IsTrue(game.ApplyService(customer, ServiceType.Cut, 1f));

        Assert.AreEqual(expected, customer.ServiceResult);
        Assert.AreEqual(CustomerServiceFeedback.Dissatisfied, customer.ServiceFeedback);
        Assert.AreEqual(CustomerEmotion.Angry, customer.Emotion);
        Assert.AreEqual(1, game.Payments.Drops.Count, "Completed low-satisfaction orders still pay base income.");
        Assert.AreEqual(0, game.Payments.Drops[0].TipReward);
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig { MaxCustomers = 10, WaitingCapacity = 10 });
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id, ServiceType service, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { service });
        Enter(game);
        Assert.IsTrue(game.Assign(customer, station));
        Reach(game);
        return customer;
    }

    private static void Enter(SalonGameModel game) => game.Tick(SalonGameModel.EnteringSeconds + .01f);
    private static void Reach(SalonGameModel game) => game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

    private static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name);
        Assert.IsNotNull(field, "Missing field: " + name);
        return field.GetValue(target);
    }

    private static object Property(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(name);
        Assert.IsNotNull(property, "Missing property: " + name);
        return property.GetValue(target);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name);
        Assert.IsNotNull(method, "Missing method: " + name);
        return method.Invoke(target, args);
    }
}
