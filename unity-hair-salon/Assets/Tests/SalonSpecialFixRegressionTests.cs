using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class SalonSpecialFixRegressionTests
{
    [Test]
    public void WrappedWashCustomerTransfersToHaircutWithoutWrongStationPenalty()
    {
        SalonGameModel game = CreateServingWashCustomer(1001, ServiceType.Cut);
        CustomerModel customer = game.Customers[0];
        CompleteWash(game, customer, wrapTowel: true);
        int wrongStationsBefore = customer.WrongStationCount;
        float satisfactionBefore = customer.Satisfaction;

        Assert.AreEqual(WashStage.Toweled, customer.WashStage);
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(ServiceType.Cut, customer.CurrentNeed);
        Assert.AreEqual(WorkstationType.Haircut, customer.NextRequiredStation);
        Assert.IsTrue(game.Assign(customer, 1));

        Assert.AreEqual(wrongStationsBefore, customer.WrongStationCount);
        Assert.AreEqual(satisfactionBefore, customer.Satisfaction);
    }

    [Test]
    public void RinsedCustomerCanMoveEarlyAndReceivesWrongStationPenalty()
    {
        SalonGameModel game = CreateServingWashCustomer(1002, ServiceType.Cut);
        CustomerModel customer = game.Customers[0];
        CompleteWash(game, customer, wrapTowel: false);
        float satisfactionBefore = customer.Satisfaction;

        Assert.AreEqual(WashStage.Rinsed, customer.WashStage);
        Assert.AreEqual(ServiceType.Wash, customer.CurrentNeed);
        Assert.IsTrue(game.Assign(customer, 1), "Early movement is allowed.");

        Assert.AreEqual(1, customer.WrongStationCount);
        Assert.Less(customer.Satisfaction, satisfactionBefore);
        Assert.AreEqual(1, customer.Station);
    }

    [Test]
    public void WrappedCustomerWaitingForTransferHasNoSelectableWashTool()
    {
        SalonGameModel game = CreateServingWashCustomer(1008, ServiceType.Cut);
        CustomerModel customer = game.Customers[0];
        CompleteWash(game, customer, wrapTowel: true);

        Assert.IsTrue(game.IsAwaitingTransfer(customer));
        Assert.IsFalse(SalonDemo.IsWashActionAvailable(customer, ActiveServiceAction.Shower));
        Assert.IsFalse(SalonDemo.IsWashActionAvailable(customer, ActiveServiceAction.Shampoo));
        Assert.IsFalse(SalonDemo.IsWashActionAvailable(customer, ActiveServiceAction.WrapTowel));
    }

    [Test]
    public void SwitchingCustomerClearsPlayerToolSelection()
    {
        var game = new SalonGameModel();
        CustomerModel first = game.Spawn(1003, new List<ServiceType> { ServiceType.Wash });
        CustomerModel second = game.Spawn(1004, new List<ServiceType> { ServiceType.Cut });
        game.SelectCustomer(first);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        context.SelectedTool = SalonTool.Shampoo;
        context.ActiveAction = ActiveServiceAction.Shower;

        game.SelectCustomer(second);

        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
    }

    [Test]
    public void MovingFocusedCustomerClearsToolAndUpdatesFocusedStation()
    {
        SalonGameModel game = CreateServingWashCustomer(1005, ServiceType.Cut);
        CustomerModel customer = game.Customers[0];
        CompleteWash(game, customer, wrapTowel: true);
        game.SelectCustomer(customer);
        PlayerContext context = game.GetOrCreatePlayerContext(SalonGameModel.LocalPlayerId);
        context.SelectedTool = SalonTool.Shampoo;
        context.ActiveAction = ActiveServiceAction.Shower;

        Assert.IsTrue(game.Assign(customer, 1));

        Assert.IsNull(context.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, context.ActiveAction);
        Assert.AreEqual(1, context.FocusedStationId);
    }

    [Test]
    public void RecommendedSecondHaircutToolAdvancesOrderAndIsNotReportedAsExtra()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(1006,
            new List<ServiceType> { ServiceType.Cut, ServiceType.Dry });
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors, SalonTool.ThinningShears);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(SalonTool.ThinningShears, customer.HaircutService.CurrentRequiredTool);
        int extraBefore = customer.ExtraServiceCount;

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.ThinningShears,
            HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(ServiceType.Dry, customer.CurrentNeed);
        Assert.AreEqual(extraBefore, customer.ExtraServiceCount);
        Assert.IsFalse(SalonDemo.ShouldReportExtraHaircut(customer, true, extraBefore));
    }

    [Test]
    public void UnneededHaircutRemainsARealExtraService()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(1007, new List<ServiceType> { ServiceType.Wash });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        int extraBefore = customer.ExtraServiceCount;

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.ThinningShears,
            HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(0, customer.Step);
        Assert.Greater(customer.ExtraServiceCount, extraBefore);
        Assert.IsTrue(SalonDemo.ShouldReportExtraHaircut(customer, false, extraBefore));
    }

    private static SalonGameModel CreateServingWashCustomer(int id, ServiceType next)
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { ServiceType.Wash, next });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        return game;
    }

    private static void CompleteWash(SalonGameModel game, CustomerModel customer, bool wrapTowel)
    {
        CompleteAction(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        CompleteAction(game, customer, WashAction.Shampoo, game.ServiceConfig.ShampooDuration);
        CompleteAction(game, customer, WashAction.Shower, game.ServiceConfig.RinseDuration);
        if (wrapTowel) Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
    }

    private static void CompleteAction(
        SalonGameModel game, CustomerModel customer, WashAction action, float duration)
    {
        Assert.IsTrue(game.BeginWashAction(customer, action));
        Assert.IsTrue(game.TickActiveServiceAction(customer, duration));
    }
}
