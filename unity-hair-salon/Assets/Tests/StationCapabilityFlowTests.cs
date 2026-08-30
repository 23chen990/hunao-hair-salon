using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class StationCapabilityFlowTests
{
    [Test]
    public void CutOnlyCustomerCanReceiveARealExtraWash()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 201, new[] { ServiceType.Cut }, 0);
        float satisfactionAfterWrongPlacement = customer.Satisfaction;

        CompleteWashCycle(game, customer);

        Assert.AreEqual(0, customer.Step, "Extra wash must not complete the cut requirement.");
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(WashStage.TowelWrapped, customer.WashStage);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.Less(customer.Satisfaction, satisfactionAfterWrongPlacement,
            "Actually performing an unnecessary service must cost satisfaction separately from waiting.");
    }

    [Test]
    public void CompletedWashRequirementCanBeRepeatedAsExtraService()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 202,
            new[] { ServiceType.Wash, ServiceType.Cut }, 0);
        CompleteWashCycle(game, customer);
        Assert.AreEqual(1, customer.Step);

        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.IsTrue(game.BeginTowelRemoval(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RemoveTowelDuration));
        Assert.IsTrue(game.Assign(customer, 0));
        Reach(game);

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.AreEqual(1, customer.Step, "Repeating wash must not reopen or advance requirements.");
    }

    [Test]
    public void FinalWashRequirementStillWaitsForWetHairAfterTowelRemoval()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 203, new[] { ServiceType.Wash }, 0);

        CompleteWashCycle(game, customer);

        Assert.IsTrue(customer.IsComplete);
        Assert.IsTrue(customer.TowelWrapped);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(0, game.Payments.Drops.Count);

        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.IsTrue(game.BeginTowelRemoval(customer));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RemoveTowelDuration));

        Assert.IsFalse(customer.TowelWrapped);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.IsFalse(customer.ExitReady);
        Assert.AreEqual(ExitBlockReason.WetHair, customer.ExitBlockReason);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [TestCase(SalonTool.Scissors)]
    [TestCase(SalonTool.ThinningShears)]
    [TestCase(SalonTool.Clippers)]
    public void LegacyTowelFlag_DoesNotBlockDomainHaircutTool(SalonTool tool)
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 204, new[] { ServiceType.Cut }, 1);
        game.ConfigureHaircutOrder(customer, tool);
        customer.TowelWrapped = true;

        Assert.IsTrue(game.BeginActiveOperation(customer));
        Assert.IsTrue(game.ApplyHaircutResult(customer, tool,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.IsFalse(game.GetServicePhysicalSnapshot(customer).IsTowelWrapped);
        Assert.AreEqual(1f, game.GetServicePhysicalSnapshot(customer).HaircutProgress(
            tool == SalonTool.ThinningShears ? HaircutTool.ThinningShears
            : tool == SalonTool.Clippers ? HaircutTool.Clippers : HaircutTool.Scissors));
    }

    [Test]
    public void WashStationExposesOneWholeService()
    {
        MethodInfo method = typeof(SalonDemo).GetMethod("GetWashStationServices",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);

        var services = (IEnumerable)method.Invoke(null, null);
        CollectionAssert.AreEqual(new[] { "Wash" }, ActionNames(services));
    }

    [Test]
    public void HaircutStationToolbarExposesEveryToolRegardlessOfCurrentCustomerNeed()
    {
        MethodInfo serviceMethod = typeof(SalonDemo).GetMethod("GetHaircutStationServices",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(serviceMethod);

        CollectionAssert.AreEqual(new[] { ServiceExecutionType.BlowDry },
            (IEnumerable)serviceMethod.Invoke(null, null));

        MethodInfo toolMethod = typeof(SalonDemo).GetMethod("GetHaircutStationTools",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(toolMethod);
        CollectionAssert.AreEqual(new[]
        {
            SalonTool.Scissors,
            SalonTool.ThinningShears,
            SalonTool.Clippers,
            SalonTool.BlowDryer,
            SalonTool.DyeBottle
        }, (IEnumerable)toolMethod.Invoke(null, null));

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts/SalonDemo.cs"));
        StringAssert.Contains("foreach (SalonTool stationTool in HaircutStationTools)", source,
            "The runtime toolbar must render the whole HairStation catalog instead of branching by CurrentNeed.");
        StringAssert.DoesNotContain("当前需求：", source,
            "The workstation toolbar must not guess or duplicate the customer's demand in its heading.");
    }

    [Test]
    public void HaircutCannotStartAsAutomaticTimedService()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 208, new[] { ServiceType.Cut }, 1);

        Assert.IsFalse(game.BeginServiceExecution(customer, ServiceExecutionType.Haircut));
        game.Tick(game.ServiceConfig.HaircutServiceDuration + 1f);

        Assert.AreEqual(0, customer.Step);
        Assert.IsNull(customer.ServiceExecution);
        Assert.AreEqual(0f,
            game.GetServicePhysicalSnapshot(customer).HaircutProgress(HaircutTool.Scissors));
    }

    [Test]
    public void StationToolbarDoesNotAutoSelectTheRequirementAnswer()
    {
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Wash },
            State = CustomerState.Serving,
            Station = 0,
            WashStage = WashStage.AwaitingShampoo
        };

        Assert.AreEqual(ActiveServiceAction.None, SalonDemo.GetDefaultHoldAction(customer));
    }

    [Test]
    public void WashOnlyCustomerCanReceiveARealExtraHaircut()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 205, new[] { ServiceType.Wash }, 1);
        float satisfactionAfterWrongPlacement = customer.Satisfaction;

        Assert.IsTrue(game.BeginActiveOperation(customer));
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        game.EndActiveOperation(customer);

        Assert.AreEqual(0, customer.Step);
        Assert.AreEqual(HairStage.Complete, customer.HairStage);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.Less(customer.Satisfaction, satisfactionAfterWrongPlacement);
    }

    [Test]
    public void WetHairStateTravelsWithoutApplyingShampoo()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 206, new[] { ServiceType.Cut }, 0);

        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(customer.HairWet);
        Assert.IsFalse(customer.ShampooApplied);

        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);
        Assert.IsTrue(customer.HairWet);
        Assert.IsFalse(customer.ShampooApplied);
        Assert.IsTrue(game.Assign(customer, 0));
        Reach(game);
        Assert.IsTrue(customer.HairWet);
    }

    [Test]
    public void UnrinsedShampoo_DoesNotDisableHaircutButRemainsPhysical()
    {
        var game = NewGame();
        CustomerModel customer = SpawnServing(game, 207, new[] { ServiceType.Cut }, 0);
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.IsTrue(customer.ShampooApplied);
        Assert.IsTrue(game.Assign(customer, 1));
        Reach(game);

        Assert.IsFalse(game.BeginActiveOperation(customer));
        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors,
            HaircutResult.Perfect, new HaircutConfig()));
        Assert.Greater(game.GetServicePhysicalSnapshot(customer).FoamAmount, 0f);
        Assert.AreEqual(1f,
            game.GetServicePhysicalSnapshot(customer).HaircutProgress(HaircutTool.Scissors));
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 10,
            WaitingCapacity = 10
        });
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, IList<ServiceType> needs, int station)
    {
        CustomerModel customer = game.Spawn(id, needs);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, station));
        Reach(game);
        return customer;
    }

    private static void CompleteWashCycle(SalonGameModel game, CustomerModel customer)
    {
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shampoo));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.ShampooDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.Shower));
        Assert.IsTrue(game.TickActiveServiceAction(customer, game.ServiceConfig.RinseDuration));
        Assert.IsTrue(game.BeginWashAction(customer, WashAction.WrapTowel));
    }

    private static void Reach(SalonGameModel game)
    {
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
    }

    private static IList<string> ActionNames(IEnumerable actions)
    {
        var names = new List<string>();
        foreach (object action in actions) names.Add(action.ToString());
        return names;
    }
}
