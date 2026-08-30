using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;

public sealed class Round2SpecialFixTests
{
    [Test]
    public void DryHairShampooIsExtraServiceWithoutChangingRequiredStep()
    {
        SalonGameModel game = CreateServingWashCustomer(1201, out CustomerModel customer);
        int stepBefore = customer.Step;
        ServiceType needBefore = customer.CurrentNeed;
        float satisfactionBefore = customer.Satisfaction;

        ServiceActionResult result = game.ResolveWashToolSelection(customer, WashAction.Shampoo);

        Assert.AreEqual(ServiceActionResult.ExtraService, result);
        Assert.AreEqual(stepBefore, customer.Step);
        Assert.AreEqual(needBefore, customer.CurrentNeed);
        Assert.AreEqual(WashStage.Dry, customer.WashStage);
        Assert.IsFalse(customer.ShampooApplied);
        Assert.IsFalse(customer.TowelWrapped);
        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.Less(customer.Satisfaction, satisfactionBefore);
    }

    [Test]
    public void RinsedHairShampooIsExtraServiceWithoutReopeningWashFlow()
    {
        SalonGameModel game = CreateServingWashCustomer(1202, out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        int stepBefore = customer.Step;

        ServiceActionResult result = game.ResolveWashToolSelection(customer, WashAction.Shampoo);

        Assert.AreEqual(ServiceActionResult.ExtraService, result);
        Assert.AreEqual(stepBefore, customer.Step);
        Assert.AreEqual(ServiceType.Wash, customer.CurrentNeed);
        Assert.AreEqual(WashStage.Rinsed, customer.WashStage);
        Assert.IsFalse(customer.ShampooApplied);
        Assert.IsFalse(customer.TowelWrapped);
    }

    [Test]
    public void WrapTowelQuickActionCompletesImmediatelyAndClearsSelection()
    {
        SalonGameModel game = CreateServingWashCustomer(1203, out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        game.SelectCustomer(customer);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        context.SelectServiceAction(customer, ActiveServiceAction.Shower, null);

        ServiceActionResult result = game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel);

        Assert.AreEqual(ServiceActionResult.QuickActionCompleted, result);
        Assert.AreEqual(WashStage.Toweled, customer.WashStage);
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(ActiveServiceAction.None, customer.ActiveServiceAction);
        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
    }

    [Test]
    public void RemoveTowelQuickActionCompletesImmediatelyAndClearsSelection()
    {
        SalonGameModel game = CreateServingWashCustomer(1204, out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        game.SelectCustomer(customer);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        context.SelectServiceAction(customer, ActiveServiceAction.Shower, null);

        ServiceActionResult result = game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel);

        Assert.AreEqual(ServiceActionResult.QuickActionCompleted, result);
        Assert.IsFalse(customer.TowelWrapped);
        Assert.AreEqual(ActiveServiceAction.None, customer.ActiveServiceAction);
        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
    }

    [Test]
    public void RemovingMissingTowelIsInvalidAndNeverSelectsAnAction()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(1205, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        game.SelectCustomer(customer);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);

        ServiceActionResult result = game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel);

        Assert.AreEqual(ServiceActionResult.Invalid, result);
        Assert.IsFalse(customer.TowelWrapped);
        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
    }

    [Test]
    public void SelectionOwnershipRejectsAnotherCustomerStationOrWashStage()
    {
        SalonGameModel game = CreateServingWashCustomer(1206, out CustomerModel customer);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        game.SelectCustomer(customer);
        context.SelectServiceAction(customer, ActiveServiceAction.Shower, null);

        Assert.IsTrue(context.OwnsSelection(customer));

        customer.WashStage = WashStage.Wet;
        Assert.IsFalse(context.OwnsSelection(customer));

        context.ClearInteractionContext();
        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
    }

    [Test]
    public void CorrectWashToolsStillAdvanceTheExistingMainFlow()
    {
        SalonGameModel game = CreateServingWashCustomer(1207, out CustomerModel customer);

        Assert.AreEqual(ServiceActionResult.Progressed,
            game.ResolveWashToolSelection(customer, WashAction.Shower));
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(ServiceActionResult.Progressed,
            game.ResolveWashToolSelection(customer, WashAction.Shampoo));
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        Assert.AreEqual(ServiceActionResult.Progressed,
            game.ResolveWashToolSelection(customer, WashAction.Shower));
    }

    [Test]
    public void CompletedTutorialDoesNotStartForSecondWashCustomer()
    {
        SalonGameModel game = CreateServingWashCustomer(1208, out CustomerModel first);
        var store = new MemoryTutorialStore();
        var tutorial = new ShampooTutorialController(store);
        Assert.IsTrue(tutorial.TryBegin(first));
        CompleteTutorialWash(game, tutorial, first);
        Assert.IsTrue(game.Assign(first, 1));
        tutorial.NotifyMovedToNextStation(first);
        Assert.IsTrue(store.IsCompleted);

        CustomerModel second = game.Spawn(1209, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(second, 4));
        Reach(game);

        Assert.IsFalse(tutorial.TryBegin(second));
        Assert.AreEqual(ShampooTutorialStep.Completed, tutorial.Step);
    }

    [Test]
    public void FoamVisualDecreasesContinuouslyWhileShowerIsRinsing()
    {
        var root = new GameObject("Round 2 Wash Visual");
        var hair = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hair.name = "Faceted Hair Test";
        hair.transform.SetParent(root.transform, false);
        var view = root.AddComponent<SalonCustomerView>();
        view.InitializeHair(Color.black);
        var customer = new CustomerModel
        {
            WashStage = WashStage.Rinsing,
            HairWet = true,
            ShampooApplied = true,
            ActiveServiceDuration = 2f,
            ActiveServiceElapsed = .4f
        };
        view.SetWashVisual(customer);
        int earlyFoam = view.VisibleFoamPartCount;

        customer.ActiveServiceElapsed = 1.6f;
        view.SetWashVisual(customer);

        Assert.Greater(earlyFoam, view.VisibleFoamPartCount);
        Assert.Greater(view.VisibleFoamPartCount, 0);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void TowelVisualUsesAReadableMultiPartLowPolySilhouette()
    {
        var root = new GameObject("Round 2 Towel Visual");
        var view = root.AddComponent<SalonCustomerView>();

        view.SetTowelWrapped(true);

        Assert.IsTrue(view.IsTowelVisualVisible);
        Assert.GreaterOrEqual(view.TowelVisualPartCount, 4);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void WashAndHaircutOrder_RemainsUntilWetHairCleanup()
    {
        SalonGameModel game = CreateServingWashCustomer(1210, out CustomerModel customer);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel));

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));

        Assert.IsTrue(customer.IsComplete);
        Assert.IsFalse(customer.TowelWrapped);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.IsFalse(customer.ExitReady);
        Assert.AreEqual(ExitBlockReason.WetHair, customer.ExitBlockReason);
    }

    [Test]
    public void ToastDurationIsAlwaysLimitedToOneThroughOnePointFiveSeconds()
    {
        Assert.AreEqual(1f, SalonDemo.GetToastDuration(.1f), .001f);
        Assert.AreEqual(1.25f, SalonDemo.GetToastDuration(1.25f), .001f);
        Assert.AreEqual(1.5f, SalonDemo.GetToastDuration(9f), .001f);
    }

    [Test]
    public void CustomerArrivalInvalidatesItsMovingStateToast()
    {
        var root = new GameObject("Round 2 Toast Owner");
        var demo = root.AddComponent<SalonDemo>();
        var game = new SalonGameModel();
        var customer = new CustomerModel
        {
            Id = 1211,
            State = CustomerState.Serving,
            Station = -1
        };
        SetPrivateField(demo, "_game", game);
        SetPrivateField(demo, "_toastCustomerId", customer.Id);
        SetPrivateField(demo, "_toastExpectedCustomerState",
            (CustomerState?)CustomerState.MovingToStation);

        typeof(SalonDemo).GetMethod("HandleCustomerChanged",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, new object[] { customer });

        Assert.AreEqual(-1, GetPrivateField<int>(demo, "_toastCustomerId"));
        Assert.IsNull(GetPrivateField<CustomerState?>(demo, "_toastExpectedCustomerState"));
        Object.DestroyImmediate(root);
    }

    [Test]
    public void WrongHaircutToolIsExtraServiceWithoutFailingOrAdvancingOrder()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(1212, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);
        int stepBefore = customer.Step;
        int haircutStepBefore = customer.HaircutService.CurrentStepIndex;

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Clippers,
            HaircutResult.WrongTool, new HaircutConfig()));

        Assert.AreEqual(stepBefore, customer.Step);
        Assert.AreEqual(haircutStepBefore, customer.HaircutService.CurrentStepIndex);
        Assert.AreEqual(HaircutServiceState.Active, customer.HaircutService.State);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    private static SalonGameModel CreateServingWashCustomer(int id, out CustomerModel customer)
    {
        var game = new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 10,
            WaitingCapacity = 10
        });
        customer = game.Spawn(id, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        Reach(game);
        return game;
    }

    private static void CompleteTutorialWash(
        SalonGameModel game, ShampooTutorialController tutorial, CustomerModel customer)
    {
        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shower);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shampoo);
        CompleteActive(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        tutorial.NotifyToolSelected(customer, ActiveServiceAction.Shower);
        CompleteActive(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        tutorial.NotifyCustomerStageChanged(customer);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        tutorial.NotifyCustomerStageChanged(customer);
    }

    private static void CompleteActive(
        SalonGameModel game, CustomerModel customer, WashAction action, float duration)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }

    private static void Reach(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }

    private static void SetPrivateField<T>(object target, string name, T value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private sealed class MemoryTutorialStore : IShampooTutorialProgressStore
    {
        public bool IsCompleted { get; private set; }
        public void MarkCompleted() => IsCompleted = true;
    }
}
