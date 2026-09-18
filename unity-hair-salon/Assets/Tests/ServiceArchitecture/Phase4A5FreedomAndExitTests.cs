using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase4A5FreedomAndExitTests
{
    [Test]
    public void WrongStation_RemainsReachableAndRecordsConfusedReaction()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnWaiting(game, 4501, SalonTool.Scissors);

        Assert.IsTrue(game.Assign(customer, 0));

        Assert.AreEqual(0, customer.Step);
        Assert.AreEqual(1, customer.WrongStationCount);
        Assert.AreEqual(CustomerReactionKind.Confused, customer.ReactionKind);
        Assert.Less(customer.Satisfaction, 70f);
    }

    [Test]
    public void UnrequestedShower_ChangesWetnessAndRecordsExtraService()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4502, SalonTool.Scissors, 0);
        float satisfaction = customer.Satisfaction;

        CompleteWashHold(game, customer, WashAction.Shower);

        ActionResult action = LastAction(game, customer);
        Assert.Greater(game.GetServicePhysicalSnapshot(customer).Wetness, 0f);
        Assert.AreEqual(0f,
            game.GetServicePhysicalSnapshot(customer).HaircutProgress(HaircutTool.Scissors));
        Assert.AreEqual(OrderEffect.ExtraService, action.OrderEffect);
        Assert.Less(customer.Satisfaction, satisfaction);
        Assert.AreEqual(CustomerReactionKind.Protest, customer.ReactionKind);
    }

    [Test]
    public void WaitingAtWrongStation_DrainsPatienceWithoutChangingOrderProgress()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4503, SalonTool.Scissors, 0);
        float patience = customer.Patience;
        float satisfaction = customer.Satisfaction;
        ServiceProgressSnapshot progress = game.GetServiceProgressSnapshot(customer);

        game.Tick(5f);

        Assert.Less(customer.Patience, patience);
        Assert.AreEqual(satisfaction, customer.Satisfaction,
            "Waiting drains patience; it must not also drain satisfaction every tick.");
        Assert.AreEqual(progress[MilestoneId.ScissorsCompleted],
            game.GetServiceProgressSnapshot(customer)[MilestoneId.ScissorsCompleted]);
    }

    [Test]
    public void WetHairCompletion_ClosesWashCutWithoutAnUnrequestedDryStep()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4504,
            new[] { SalonTool.Scissors, SalonTool.ThinningShears }, 0);
        float before = customer.Satisfaction;
        CompleteWashHold(game, customer, WashAction.Shower);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.ThinningShears,
            HaircutResult.Perfect, new HaircutConfig()));

        Assert.IsTrue(customer.IsComplete);
        Assert.IsTrue(customer.OrderRequirementsCompleted);
        Assert.IsTrue(customer.ExitReady);
        Assert.AreEqual(ExitBlockReason.None, customer.ExitBlockReason);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.Less(customer.Satisfaction, before);
        Assert.Greater(game.Payments.Drops.Count, 0);
    }

    [Test]
    public void ExitBlockReason_ReportsWetHairFoamAndTowel()
    {
        SalonGameModel wetGame = NewGame();
        CustomerModel wet = SpawnServing(wetGame, 4505, SalonTool.Scissors, 0);
        CompleteWashHold(wetGame, wet, WashAction.Shower);
        Assert.AreEqual(ExitBlockReason.RequirementsIncomplete, wet.ExitBlockReason);

        SalonGameModel foamGame = NewGame();
        CustomerModel foam = SpawnServing(foamGame, 4506, SalonTool.Scissors, 0);
        CompleteWashHold(foamGame, foam, WashAction.Shower);
        CompleteWashHold(foamGame, foam, WashAction.Shampoo);
        Assert.AreEqual(ExitBlockReason.FoamRemaining, foam.ExitBlockReason);

        SalonGameModel towelGame = NewGame();
        CustomerModel towel = SpawnServing(towelGame, 4507, SalonTool.Scissors, 0);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            towelGame.PerformQuickAction(towel, ActiveServiceAction.WrapTowel));
        Assert.AreEqual(ExitBlockReason.TowelWrapped, towel.ExitBlockReason);
    }

    [Test]
    public void ExtraWashPhysicalState_SurvivesTransfer()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4508, SalonTool.Scissors, 0);
        CompleteWashHold(game, customer, WashAction.Shower);
        float wetness = game.GetServicePhysicalSnapshot(customer).Wetness;

        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);

        Assert.AreEqual(wetness, game.GetServicePhysicalSnapshot(customer).Wetness);
    }

    [Test]
    public void WrongWashLeavesFoamCleanupPriority()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4510, new[] { SalonTool.Scissors, SalonTool.ThinningShears }, 0);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration * .28f));
        Assert.AreEqual(
            ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.ThinningShears,
            HaircutResult.Perfect, new HaircutConfig()));

        var root = new GameObject("Wrong wash cleanup root");
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);
        view.Refresh();

        Assert.IsTrue(view.CleanupVisible);
        StringAssert.Contains("泡沫残留", view.CleanupText);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void NormalFlow_NoCleanupHintAfterCorrectHaircut()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4511, new[] { SalonTool.Scissors }, 1);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));

        var root = new GameObject("No cleanup after clean flow");
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);
        view.Refresh();

        Assert.IsFalse(view.CleanupVisible);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void HaircutWithoutCustomer_IsPhysicalInvalidAndChangesNothing()
    {
        SalonGameModel game = NewGame();

        Assert.IsFalse(game.BeginHaircutAction(null, SalonTool.Scissors, new HaircutConfig()));
        Assert.AreEqual(0, game.Customers.Count);
    }

    [Test]
    public void RequirementMismatch_DoesNotBlockShowerFromResolver()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4509, SalonTool.Scissors, 0);

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.AreEqual(ExecutionStatus.Executed, LastAction(game, customer).ExecutionStatus);
    }

    [Test]
    public void UnrequestedIrreversibleHaircut_UsesResistanceWindow()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = game.Spawn(4510, new List<ServiceType> { ServiceType.Wash });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        CustomerPhysicalStateSnapshot before = game.GetServicePhysicalSnapshot(customer);

        Assert.IsTrue(game.BeginHaircutAction(customer, SalonTool.Clippers, new HaircutConfig()));
        HaircutResult result = game.CompleteHaircutAction(customer, .25f, false);
        game.EndActiveOperation(customer);

        CustomerPhysicalStateSnapshot after = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(HaircutResult.Resisted, result);
        Assert.AreEqual(before, after);
        Assert.AreEqual(ExecutionStatus.Resisted, LastAction(game, customer).ExecutionStatus);
        Assert.AreEqual(CustomerReactionKind.Resistance, customer.ReactionKind);
    }

    [Test]
    public void CompletedButBlocked_OrderBubbleShowsCleanupReason()
    {
        SalonGameModel game = NewGame();
        CustomerModel customer = SpawnServing(game, 4511, SalonTool.Scissors, 0);
        CompleteWashHold(game, customer, WashAction.Shower);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));

        var root = new GameObject("Exit blocker bubble test");
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);
        view.Refresh();

        Assert.IsFalse(view.CleanupVisible,
            "A completed Wash+Cut order does not require an unrequested Dry cleanup.");
        Object.DestroyImmediate(root);
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 12,
            WaitingCapacity = 12
        });
    }

    private static CustomerModel SpawnWaiting(
        SalonGameModel game,
        int id,
        params SalonTool[] tools)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { ServiceType.Cut });
        Assert.IsTrue(game.ConfigureHaircutOrder(customer, tools));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        return customer;
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game,
        int id,
        SalonTool tool,
        int station)
    {
        return SpawnServing(game, id, new[] { tool }, station);
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game,
        int id,
        SalonTool[] tools,
        int station)
    {
        CustomerModel customer = SpawnWaiting(game, id, tools);
        Assert.IsTrue(game.Assign(customer, station));
        Reach(game);
        game.SelectCustomer(customer);
        return customer;
    }

    private static void CompleteWashHold(
        SalonGameModel game,
        CustomerModel customer,
        WashAction action)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        float duration = action == WashAction.Shampoo
            ? game.ServiceConfig.ShampooDuration : game.ServiceConfig.RinseDuration;
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }

    private static ActionResult LastAction(SalonGameModel game, CustomerModel customer)
    {
        IReadOnlyList<ActionHistoryEntry> history = game.GetServiceActionHistory(customer);
        Assert.Greater(history.Count, 0);
        return history[history.Count - 1].ActionResult;
    }

    private static void Reach(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }
}
