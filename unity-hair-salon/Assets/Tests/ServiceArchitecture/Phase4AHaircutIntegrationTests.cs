using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class Phase4AHaircutIntegrationTests
{
    [Test]
    public void NormalUntowelAndHaircut_UsesDomainChain()
    {
        SalonGameModel game = CreateWashedHaircutCustomer(4101, out CustomerModel customer);

        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.RemoveTowel));
        HaircutResult result = CompleteHaircut(game, customer, SalonTool.Scissors, 2f);

        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(HaircutResult.Perfect, result);
        Assert.IsFalse(physical.IsTowelWrapped);
        Assert.AreEqual(1f, physical.HaircutProgress(HaircutTool.Scissors));
        Assert.AreEqual(6, physical.PhysicalStateRevision);
        Assert.AreEqual(6, game.GetServiceActionHistory(customer).Count);
        Assert.IsTrue(customer.IsComplete);
    }

    [Test]
    public void HaircutWhileTowelWrapped_DamagesTowelWithoutAdvancingCut()
    {
        SalonGameModel game = CreateWashedHaircutCustomer(4102, out CustomerModel customer);
        float satisfactionBefore = customer.Satisfaction;

        HaircutResult result = CompleteHaircut(game, customer, SalonTool.Scissors, 2f);

        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(HaircutResult.WrongTool, result);
        Assert.IsTrue(physical.IsTowelWrapped);
        Assert.AreEqual(TowelCondition.Damaged, physical.TowelCondition);
        Assert.AreEqual(0f, physical.HaircutProgress(HaircutTool.Scissors));
        Assert.Less(customer.Satisfaction, satisfactionBefore);
        Assert.AreEqual(0, customer.HaircutService.CurrentStepIndex);
    }

    [TestCase(.5f, HaircutResult.Undercut, ExecutionQuality.Under)]
    [TestCase(2f, HaircutResult.Perfect, ExecutionQuality.Perfect)]
    [TestCase(3f, HaircutResult.Overcut, ExecutionQuality.Over)]
    public void HaircutElapsed_ResolvesUnderPerfectOver(
        float elapsed,
        HaircutResult expectedResult,
        ExecutionQuality expectedQuality)
    {
        SalonGameModel game = CreateCutCustomer(4103 + (int)(elapsed * 10), SalonTool.Scissors,
            out CustomerModel customer);

        HaircutResult result = CompleteHaircut(game, customer, SalonTool.Scissors, elapsed);

        ActionHistoryEntry history = game.GetServiceActionHistory(customer)[0];
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(expectedQuality, history.ActionResult.ExecutionQuality);
        Assert.AreEqual(1, game.GetServicePhysicalSnapshot(customer).PhysicalStateRevision);
    }

    [Test]
    public void WrongHaircutTool_IsRecoverableWithRequiredTool()
    {
        SalonGameModel game = CreateCutCustomer(4107, SalonTool.Scissors, out CustomerModel customer);
        float satisfactionBefore = customer.Satisfaction;

        HaircutResult wrong = CompleteHaircut(game, customer, SalonTool.Clippers, 1f);
        CustomerPhysicalStateSnapshot afterWrong = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(HaircutResult.WrongTool, wrong);
        Assert.Greater(afterWrong.HaircutProgress(HaircutTool.Clippers), 0f);
        Assert.AreEqual(0f, afterWrong.HaircutProgress(HaircutTool.Scissors));
        Assert.Less(customer.Satisfaction, satisfactionBefore);
        Assert.AreEqual(CustomerState.Serving, customer.State);

        HaircutResult recovered = CompleteHaircut(game, customer, SalonTool.Scissors, 2f);
        Assert.AreEqual(HaircutResult.Perfect, recovered);
        Assert.AreEqual(1f,
            game.GetServicePhysicalSnapshot(customer).HaircutProgress(HaircutTool.Scissors));
        Assert.IsTrue(customer.IsComplete);
        Assert.AreEqual(2, game.GetServiceActionHistory(customer).Count);
    }

    [Test]
    public void FoamyCustomer_MovesToHaircutAndCanContinueDomainHaircut()
    {
        SalonGameModel game = CreateServingWashCustomer(4108, out CustomerModel customer);
        CompleteWashHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteWashHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        float foamBefore = game.GetServicePhysicalSnapshot(customer).FoamAmount;
        float satisfactionBefore = customer.Satisfaction;

        HaircutResult result = CompleteHaircut(game, customer, SalonTool.Scissors, 2f);

        CustomerPhysicalStateSnapshot physical = game.GetServicePhysicalSnapshot(customer);
        Assert.AreEqual(HaircutResult.Perfect, result);
        Assert.AreEqual(foamBefore, physical.FoamAmount);
        Assert.AreEqual(1f, physical.HaircutProgress(HaircutTool.Scissors));
        Assert.Less(customer.Satisfaction, satisfactionBefore);
        Assert.AreEqual(3, game.GetServiceActionHistory(customer).Count);
    }

    [Test]
    public void LegacyHaircutFields_DoNotOverrideDomainPhysicalState()
    {
        SalonGameModel game = CreateCutCustomer(4109, SalonTool.Scissors, out CustomerModel customer);
        customer.TowelWrapped = true;
        customer.ShampooApplied = true;
        customer.WashStage = WashStage.Foamy;

        HaircutResult result = CompleteHaircut(game, customer, SalonTool.Scissors, 2f);

        Assert.AreEqual(HaircutResult.Perfect, result);
        Assert.AreEqual(1f,
            game.GetServicePhysicalSnapshot(customer).HaircutProgress(HaircutTool.Scissors));
    }

    private static HaircutResult CompleteHaircut(
        SalonGameModel game,
        CustomerModel customer,
        SalonTool tool,
        float elapsed)
    {
        var config = new HaircutConfig();
        Assert.IsTrue(game.BeginHaircutAction(customer, tool, config));
        return game.CompleteHaircutAction(customer, elapsed, false);
    }

    private static SalonGameModel CreateCutCustomer(
        int id,
        SalonTool requiredTool,
        out CustomerModel customer)
    {
        var game = new SalonGameModel();
        customer = game.Spawn(id, new List<ServiceType> { ServiceType.Cut });
        Assert.IsTrue(game.ConfigureHaircutOrder(customer, requiredTool));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.SelectCustomer(customer);
        return game;
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

    private static SalonGameModel CreateWashedHaircutCustomer(int id, out CustomerModel customer)
    {
        SalonGameModel game = CreateServingWashCustomer(id, out customer);
        CompleteWashHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteWashHold(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteWashHold(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        Assert.AreEqual(ServiceActionResult.QuickActionCompleted,
            game.PerformQuickAction(customer, ActiveServiceAction.WrapTowel));
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.SelectCustomer(customer);
        return game;
    }

    private static void CompleteWashHold(
        SalonGameModel game,
        CustomerModel customer,
        WashAction action,
        float duration)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }
}
