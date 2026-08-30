using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class Phase3WashIntegrationTests
{
    [Test]
    public void OfficialWashFlow_UsesResolverApplyAndCompletesWashStep()
    {
        SalonGameModel game = CreateServingWashCustomer(3101, out CustomerModel customer);

        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));

        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(ShampooState.None, physical.ShampooState);
        Assert.AreEqual(0f, physical.FoamAmount);
        Assert.IsTrue(physical.IsTowelWrapped);
        Assert.AreEqual(4, physical.PhysicalStateRevision);
        Assert.AreEqual(4, game.GetServiceActionHistory(customer).Count);
        Assert.AreEqual(1, customer.Step);
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed);
    }

    [Test]
    public void DryHairShampoo_ClumpsThenCanBeRecovered()
    {
        SalonGameModel game = CreateServingWashCustomer(3102, out CustomerModel customer);
        float satisfactionBefore = customer.Satisfaction;

        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);

        CustomerPhysicalStateSnapshot clumped = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(ShampooState.ClumpedOnDryHair, clumped.ShampooState);
        Assert.IsFalse(game.GetServiceProgressSnapshot(customer)[MilestoneId.Shampooed].EverCompleted);
        Assert.Less(customer.Satisfaction, satisfactionBefore);

        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);

        CustomerPhysicalStateSnapshot recovered = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(ShampooState.Normal, recovered.ShampooState);
        Assert.Greater(recovered.FoamAmount, 0f);
        Assert.IsTrue(game.GetServiceProgressSnapshot(customer)[MilestoneId.Shampooed].EverCompleted);
    }

    [Test]
    public void FoamyTowel_RemoveAtWashStationPreservesFoamAndAllowsRecovery()
    {
        SalonGameModel game = CreateServingWashCustomer(3103, out CustomerModel customer);
        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        float foamBefore = game.GetServicePhysicalSnapshot(customer).FoamAmount;

        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        CustomerPhysicalStateSnapshot wrapped = game.GetServicePhysicalSnapshot(customer);
        Assert.IsTrue(wrapped.IsTowelWrapped);
        Assert.AreEqual(TowelContamination.Foam, wrapped.TowelContamination);
        Assert.AreEqual(0, customer.Step);

        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel));
        CustomerPhysicalStateSnapshot removed = game.GetServicePhysicalSnapshot(customer);
        Assert.IsFalse(removed.IsTowelWrapped);
        Assert.AreEqual(foamBefore, removed.FoamAmount, .001f);

        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(0f, game.GetServicePhysicalSnapshot(customer).FoamAmount);
        Assert.AreEqual(ShampooState.None, game.GetServicePhysicalSnapshot(customer).ShampooState);
    }

    [Test]
    public void FoamyCustomer_MovesToHaircutStationWithoutLosingDomainState()
    {
        SalonGameModel game = CreateServingWashCustomer(3104, out CustomerModel customer);
        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CustomerPhysicalStateSnapshot before = game.GetServicePhysicalSnapshot(customer);
        int contextVersionBefore = game.GetServiceInteractionSnapshot(customer).ContextVersion;

        Assert.IsTrue(game.Assign(customer, 1));

        CustomerPhysicalStateSnapshot duringMove = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(before.FoamAmount, duringMove.FoamAmount);
        Assert.AreEqual(before.ShampooState, duringMove.ShampooState);
        Assert.Greater(game.GetServiceInteractionSnapshot(customer).ContextVersion, contextVersionBefore);
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(before.FoamAmount, game.GetServicePhysicalSnapshot(customer).FoamAmount);
        Assert.AreEqual(ShampooState.Normal, game.GetServicePhysicalSnapshot(customer).ShampooState);
    }

    [Test]
    public void StationArrival_InvalidatesMovementContextWithoutChangingPhysicalRevision()
    {
        SalonGameModel game = CreateServingWashCustomer(3108, out CustomerModel customer);
        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        int revision = game.GetServicePhysicalSnapshot(customer).PhysicalStateRevision;

        Assert.IsTrue(game.Assign(customer, 1));
        int movementVersion = game.GetServiceInteractionSnapshot(customer).ContextVersion;
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.Greater(game.GetServiceInteractionSnapshot(customer).ContextVersion, movementVersion);
        Assert.AreEqual(revision, game.GetServicePhysicalSnapshot(customer).PhysicalStateRevision);
    }

    [Test]
    public void InterruptedOfficialWashAction_CommitsPartialElapsed()
    {
        SalonGameModel game = CreateServingWashCustomer(3105, out CustomerModel customer);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration * .25f));

        game.CancelActiveServiceAction(customer);

        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
        Assert.Greater(physical.Wetness, 0f);
        Assert.Less(physical.Wetness, 1f);
        Assert.AreEqual(1, physical.PhysicalStateRevision);
    }

    [Test]
    public void LegacyWashFields_AreOneWayProjectionsOfDomainState()
    {
        SalonGameModel game = CreateServingWashCustomer(3106, out CustomerModel customer);
        CompleteHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);

        Assert.AreEqual(physical.Wetness > 0.05f, customer.HairWet);
        Assert.AreEqual(physical.ShampooState != ShampooState.None || physical.FoamAmount > 0f,
            customer.ShampooApplied);
        Assert.AreEqual(physical.IsTowelWrapped, customer.TowelWrapped);
    }

    [Test]
    public void Haircut_AppendsDomainHistoryAfterPhase4AMigration()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(3107, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(1, game.GetServiceActionHistory(customer).Count);
        Assert.AreEqual(ServiceActionType.Haircut,
            game.GetServiceActionHistory(customer)[0].ActionResult.ActionToken.ActionType);
    }

    private static SalonGameModel CreateServingWashCustomer(int id, out CustomerModel customer)
    {
        var game = new SalonGameModel();
        customer = game.Spawn(id, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.SelectCustomer(customer);
        return game;
    }

    private static void CompleteHold(
        SalonGameModel game,
        CustomerModel customer,
        WashAction action,
        float duration)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }
}
