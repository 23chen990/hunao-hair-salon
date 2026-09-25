using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class ShampooInteractionRedesignTests
{
    [Test]
    public void WashToolbarExposesOnlyWashService()
    {
        CollectionAssert.AreEqual(
            new[] { "Wash" },
            Array.ConvertAll(ToArray(SalonDemo.GetWashStationServices()), service => service.ToString()));
    }

    [Test]
    public void DryHairShampooExecutesAsRecoverableClump()
    {
        SalonGameModel game = CreateServingWashCustomer(out CustomerModel customer);

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.AreEqual(ShampooState.ClumpedOnDryHair,
            game.GetServicePhysicalSnapshot(customer).ShampooState);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(customer.HairWet);
        Assert.AreEqual(WashStage.ShampooApplied, customer.WashStage);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
    }

    [Test]
    public void CompletedRubbingProducesFoamAndStartsBackgroundWashWait()
    {
        SalonGameModel game = CreateServingWashCustomer(out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);

        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);

        Assert.AreEqual(WashStage.Foamy, customer.WashStage);
        Assert.IsTrue(customer.ShampooApplied);
        // Phase 2 设计：洗发后立即进入后台泡沫等待，玩家可以离开，稍后必须回来冲洗。
        // 这里锁定的是新语义，旧语义（打完泡沫即 Inactive）已被有意替换。
        Assert.AreEqual(BackgroundTaskState.Running, customer.BackgroundTask.State);
        Assert.IsTrue(game.IsWashFoamWaitRunning(customer));
        Assert.IsFalse(game.IsWashFoamReadyToRinse(customer));

        game.Tick(game.ServiceConfig.FoamOptimalStart + .01f);
        Assert.IsTrue(game.IsWashFoamReadyToRinse(customer));
    }

    [Test]
    public void ShowerAutomaticallyRinsesFoamAndTowelIsAnImmediateAction()
    {
        SalonGameModel game = CreateServingWashCustomer(out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);

        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(WashStage.Rinsed, customer.WashStage);
        Assert.IsFalse(customer.ShampooApplied);

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        Assert.AreEqual(ActiveServiceAction.None, customer.ActiveServiceAction);
        Assert.AreEqual(WashStage.Toweled, customer.WashStage);
        Assert.IsTrue(customer.TowelWrapped);
    }

    [Test]
    public void TutorialControllerExistsAndPersistsOnlyAfterNextStationTransfer()
    {
        Type tutorialType = typeof(SalonGameModel).Assembly.GetType("HairSalon.ShampooTutorialController");
        Assert.NotNull(tutorialType);
        Assert.NotNull(tutorialType.GetMethod("TryBegin"));
        Assert.NotNull(tutorialType.GetMethod("NotifyToolSelected"));
        Assert.NotNull(tutorialType.GetMethod("NotifyCustomerStageChanged"));
        Assert.NotNull(tutorialType.GetMethod("NotifyMovedToNextStation"));
        Assert.NotNull(typeof(SalonDemo).Assembly.GetType("SalonTutorialFingerPulse"));
    }

    [Test]
    public void FirstTutorialAdvancesThroughVisualStepsAndCompletesAfterTransfer()
    {
        SalonGameModel game = CreateServingWashCustomer(out CustomerModel customer);
        var store = new MemoryTutorialStore();
        var tutorial = new ShampooTutorialController(store);

        Assert.IsTrue(tutorial.TryBegin(customer));
        Assert.AreEqual(ShampooTutorialStep.SelectShowerForWet, tutorial.Step);
        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shower);
        Assert.AreEqual(ShampooTutorialStep.HoldToWet, tutorial.Step);

        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        Assert.AreEqual(ShampooTutorialStep.SelectShampoo, tutorial.Step);
        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shampoo);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        Assert.AreEqual(ShampooTutorialStep.SelectShowerForRinse, tutorial.Step);

        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shower);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        Assert.AreEqual(ShampooTutorialStep.SelectTowel, tutorial.Step);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
        tutorial.NotifyCustomerStageChanged(customer);
        Assert.AreEqual(ShampooTutorialStep.MoveToNextStation, tutorial.Step);
        Assert.IsFalse(store.IsCompleted);

        Assert.IsTrue(game.Assign(customer, 1));
        tutorial.NotifyMovedToNextStation(customer);
        Assert.IsTrue(store.IsCompleted);
        Assert.AreEqual(ShampooTutorialStep.Completed, tutorial.Step);
        Assert.IsFalse(new ShampooTutorialController(store).TryBegin(customer));
    }

    private static SalonGameModel CreateServingWashCustomer(out CustomerModel customer)
    {
        var game = new SalonGameModel();
        customer = game.Spawn(901, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds);
        return game;
    }

    private static ServiceExecutionType[] ToArray(IReadOnlyList<ServiceExecutionType> source)
    {
        var result = new ServiceExecutionType[source.Count];
        for (int i = 0; i < source.Count; i++) result[i] = source[i];
        return result;
    }

    private static void CompleteActive(SalonGameModel game, CustomerModel customer, WashAction action, float duration)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }

    private sealed class MemoryTutorialStore : IShampooTutorialProgressStore
    {
        public bool IsCompleted { get; private set; }
        public void MarkCompleted() => IsCompleted = true;
    }
}
