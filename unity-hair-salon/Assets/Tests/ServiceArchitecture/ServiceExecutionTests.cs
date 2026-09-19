using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using UnityEngine;

public sealed class ServiceExecutionTests
{
    [Test]
    public void RuntimeWash_UsesCentralFiveSecondDemoTuningWithOneFinalSubmission()
    {
        var game = new HairSalon.SalonGameModel();
        HairSalon.CustomerModel customer = game.Spawn(101,
            new[] { HairSalon.ServiceType.Wash });
        customer.State = HairSalon.CustomerState.Serving;
        customer.Station = 0;
        game.Workstations[0].CurrentCustomerId = customer.Id;

        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        Assert.AreEqual(ServiceExecutionState.Executing, customer.ServiceExecution.State);
        Assert.AreEqual(5f, customer.ServiceExecution.Duration);
        Assert.IsFalse(game.Assign(customer, 1), "Executing service must keep its station occupied.");

        game.Tick(4.99f);
        Assert.AreEqual(0, game.GetServiceActionHistory(customer).Count);
        game.Tick(.01f);

        Assert.AreEqual(ServiceExecutionState.Completed, customer.ServiceExecution.State);
        Assert.AreEqual(1, game.GetServiceActionHistory(customer).Count);
        Assert.AreEqual(ApplyStatus.Applied,
            customer.ServiceExecutionCompletion.ApplyResult.Status);
    }

    [Test]
    public void AutomaticServiceExecutionKeepsTheStationBusyButReleasesThePlayer()
    {
        var game = new HairSalon.SalonGameModel();
        HairSalon.CustomerModel washing = game.Spawn(111,
            new[] { HairSalon.ServiceType.Wash });
        washing.State = HairSalon.CustomerState.Serving;
        washing.Station = 0;
        game.Workstations[0].CurrentCustomerId = washing.Id;

        Assert.IsTrue(game.BeginServiceExecution(washing, ServiceExecutionType.Wash));

        Assert.IsFalse(game.PlayerBusy,
            "An automatic station timer must not prevent the player from operating another station.");
        Assert.IsTrue(game.Workstations[0].IsOccupied);
    }

    [Test]
    public void AutomaticWash_DoesNotCompleteAnOutstandingHaircutOrder()
    {
        var game = new HairSalon.SalonGameModel();
        HairSalon.CustomerModel customer = game.Spawn(112,
            new[] { HairSalon.ServiceType.Cut });
        customer.State = HairSalon.CustomerState.Serving;
        customer.Station = 0;
        game.Workstations[0].CurrentCustomerId = customer.Id;

        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration);

        Assert.AreEqual(0, customer.Step,
            "A wash must not advance an outstanding haircut requirement.");
        Assert.AreEqual(HairSalon.CustomerState.Serving, customer.State);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void RuntimeWholeWash_ThenManualDry_CanCompleteApprovedWashDryOrder()
    {
        var game = new HairSalon.SalonGameModel();
        HairSalon.CustomerModel customer = game.Spawn(113,
            new[] { HairSalon.ServiceType.Wash, HairSalon.ServiceType.Dry });
        game.Tick(HairSalon.SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 0));
        game.Tick(HairSalon.SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
        game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        Assert.AreEqual(HairSalon.ServiceType.Dry, customer.CurrentNeed);

        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(HairSalon.SalonGameModel.MovingToStationSeconds + .01f);
        Assert.IsTrue(game.StartManualBlow(customer));
        game.TickManualBlow(customer, 3.5f);
        Assert.AreEqual(HairSalon.BlowResult.Good, game.EndManualBlowHold(customer));

        Assert.AreEqual(HairSalon.CustomerState.Finished, customer.State,
            "The real whole-wash button must leave every wash milestone satisfiable by the visible UI.");
        Assert.AreEqual(1, game.Payments.Drops.Count);
    }

    [TestCase("O001")]
    [TestCase("O002")]
    [TestCase("O003")]
    [TestCase("O004")]
    [TestCase("O005")]
    public void EveryApprovedOrderCanSettleThroughTheVisibleWholeServiceFlow(string orderId)
    {
        var game = new HairSalon.SalonGameModel();
        HairSalon.CustomerModel customer = game.Spawn(120,
            new System.Collections.Generic.List<HairSalon.ServiceType>(
                HairSalon.SalonOrderCatalog.Get(orderId)));
        game.Tick(HairSalon.SalonGameModel.EnteringSeconds + .01f);

        if (customer.CurrentNeed == HairSalon.ServiceType.Wash)
        {
            Assert.IsTrue(game.Assign(customer, 0));
            game.Tick(HairSalon.SalonGameModel.MovingToStationSeconds + .01f);
            Assert.IsTrue(game.BeginServiceExecution(customer, ServiceExecutionType.Wash));
            game.Tick(game.ServiceConfig.WashServiceDuration + .01f);
        }

        if (!customer.IsComplete && customer.CurrentNeed == HairSalon.ServiceType.Cut)
        {
            if (customer.Station != 1)
            {
                Assert.IsTrue(game.Assign(customer, 1));
                game.Tick(HairSalon.SalonGameModel.MovingToStationSeconds + .01f);
            }
            HairSalon.SalonTool tool = customer.HaircutService.CurrentRequiredTool;
            Assert.IsTrue(game.ApplyHaircutResult(customer, tool,
                HairSalon.HaircutResult.Perfect, new HairSalon.HaircutConfig()));
        }

        bool needsDryStep = !customer.IsComplete && customer.CurrentNeed == HairSalon.ServiceType.Dry;
        bool needsWetHairCleanup = customer.IsComplete && !customer.ExitReady &&
                                   customer.ExitBlockReason == ExitBlockReason.WetHair;
        if (needsDryStep || needsWetHairCleanup)
        {
            if (customer.Station != 1)
            {
                Assert.IsTrue(game.Assign(customer, 1));
                game.Tick(HairSalon.SalonGameModel.MovingToStationSeconds + .01f);
            }
            Assert.IsTrue(game.StartManualBlow(customer));
            game.TickManualBlow(customer, 3.5f);
            Assert.AreEqual(HairSalon.BlowResult.Good, game.EndManualBlowHold(customer));
        }

        Assert.AreEqual(HairSalon.CustomerState.Finished, customer.State,
            orderId + ": step=" + customer.Step + "/" + customer.Needs.Count +
            ", requirements=" + customer.OrderRequirementsCompleted +
            ", exitReady=" + customer.ExitReady +
            ", blocker=" + customer.ExitBlockReason +
            ", wet=" + customer.HairWet);
        Assert.AreEqual(1, game.Payments.Drops.Count, orderId);
    }

    [Test]
    public void RuntimeToolbar_DoesNotExposeWashSubActions()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/SalonDemo.cs"));

        StringAssert.DoesNotContain("花洒", source);
        StringAssert.DoesNotContain("洗发水", source);
        StringAssert.DoesNotContain("包毛巾", source);
        StringAssert.DoesNotContain("拆毛巾", source);
        StringAssert.DoesNotContain("BeginWashAction", source);
        StringAssert.DoesNotContain("new ShampooTutorialController", source);
    }

    [Test]
    public void Start_Wash_EntersExecutingWithoutResolvingResult()
    {
        var fixture = CreateFixture();

        ServiceExecution execution = fixture.Controller.Start(ServiceExecutionType.Wash, 8f, 10f);

        Assert.AreEqual(ServiceExecutionState.Executing, execution.State);
        Assert.AreEqual(0f, execution.Elapsed);
        Assert.IsNull(execution.Result);
        Assert.AreEqual(0, fixture.History.Entries.Count);
    }

    [Test]
    public void ExecutingService_CannotSubmitCompletionTwice()
    {
        var fixture = CreateFixture();
        ServiceExecution execution = fixture.Controller.Start(ServiceExecutionType.Wash, 8f, 10f);
        execution.Tick(8f);

        Assert.AreEqual(ApplyStatus.Applied, fixture.Controller.Complete().ApplyResult.Status);
        Assert.AreEqual(ServiceExecutionState.Completed, execution.State);
        Assert.AreEqual(ApplyStatus.AlreadyApplied, fixture.Controller.Complete().ApplyResult.Status);
        Assert.AreEqual(1, fixture.History.Entries.Count);
    }

    [Test]
    public void Completion_ProducesAppliedActionResultThroughApplier()
    {
        var fixture = CreateFixture();
        ServiceExecution execution = fixture.Controller.Start(ServiceExecutionType.Wash, 8f, 10f);
        execution.Tick(8f);

        ServiceExecutionCompletion completion = fixture.Controller.Complete();

        Assert.AreEqual(ApplyStatus.Applied, completion.ApplyResult.Status);
        Assert.IsNotNull(completion.ActionResult);
        Assert.AreEqual(ExecutionStatus.Executed, completion.ActionResult.ExecutionStatus);
        Assert.AreSame(completion.ActionResult, completion.ApplyResult.ActionResult);
    }

    [Test]
    public void ServiceExecution_DoesNotDependOnLegacyStepStageOrCurrentNeedSemantics()
    {
        string path = Path.Combine(Application.dataPath,
            "Scripts/ServiceArchitecture/Core/ServiceExecution.cs");
        string source = File.ReadAllText(path);

        Assert.IsFalse(source.Contains("CurrentStep"));
        Assert.IsFalse(source.Contains("WashStage"));
        Assert.IsFalse(source.Contains("CurrentNeed"));
    }

    private static Fixture CreateFixture()
    {
        var history = new InMemoryActionHistory();
        var state = new CustomerServiceState(7, 0, new CustomerPhysicalState(),
            OrderDefinition.WashOnly(), new ServiceProgress(), new CustomerMetricsState(70f, 100f));
        var context = new InteractionContext();
        context.Focus(7, 0);
        var applier = new ActionResultApplier(new ServiceRuleConfig(), history);
        return new Fixture(new ServiceExecutionController(
            new ActionResolver(new ServiceRuleConfig()), applier, state, context), history);
    }

    private sealed class Fixture
    {
        public Fixture(ServiceExecutionController controller, InMemoryActionHistory history)
        {
            Controller = controller;
            History = history;
        }

        public ServiceExecutionController Controller { get; }
        public InMemoryActionHistory History { get; }
    }
}
