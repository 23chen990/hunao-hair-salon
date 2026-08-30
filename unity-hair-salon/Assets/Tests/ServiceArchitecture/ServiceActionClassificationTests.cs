using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class ServiceActionClassificationTests
{
    [Test]
    public void HaircutOnly_Shower_IsWrongService()
    {
        ServiceActionClassification classification = Classify(
            CutOrder(HaircutTool.Scissors), Result(ServiceActionType.Shower, ServiceTool.Shower));

        Assert.AreEqual(ServiceRelation.WrongService, classification.Relation);
        Assert.IsTrue(classification.Reason.HasFlag(ServiceActionClassificationReason.UnrequestedService));
    }

    [Test]
    public void HaircutOnly_BlowDry_IsWrongService()
    {
        ServiceActionClassification classification = Classify(
            CutOrder(HaircutTool.Scissors), Result(ServiceActionType.BlowDry, ServiceTool.BlowDryer));

        Assert.AreEqual(ServiceRelation.WrongService, classification.Relation);
        Assert.IsTrue(classification.Reason.HasFlag(ServiceActionClassificationReason.UnrequestedService));
    }

    [Test]
    public void CompletedWash_RepeatedShower_IsExtraService()
    {
        var progress = new ServiceProgress();
        progress.Set(State(MilestoneId.WetHairApplied, true, true));
        progress.Set(State(MilestoneId.Shampooed, true, true));
        progress.Set(State(MilestoneId.RinseClean, true, true));

        ServiceActionClassification classification = Classify(
            WashOrder(), Result(ServiceActionType.Shower, ServiceTool.Shower), progress.CreateSnapshot());

        Assert.AreEqual(ServiceRelation.ExtraService, classification.Relation);
        Assert.IsTrue(classification.Reason.HasFlag(ServiceActionClassificationReason.CompletedServiceRepeated));
    }

    [Test]
    public void RequiredThinningShears_Scissors_IsWrongHaircutTool()
    {
        ServiceActionClassification classification = Classify(
            CutOrder(HaircutTool.ThinningShears), Result(ServiceActionType.Haircut, ServiceTool.Scissors));

        Assert.AreEqual(ServiceRelation.WrongService, classification.Relation);
        Assert.IsTrue(classification.Reason.HasFlag(ServiceActionClassificationReason.WrongHaircutTool));
    }

    [Test]
    public void CorrectHaircutOver_IsNormalAndDisasterCandidate()
    {
        ServiceActionClassification classification = Classify(
            CutOrder(HaircutTool.Scissors),
            Result(ServiceActionType.Haircut, ServiceTool.Scissors, ExecutionQuality.Over));

        Assert.AreEqual(ServiceRelation.NormalService, classification.Relation);
        Assert.IsTrue(classification.IsDisasterCandidate);
        Assert.IsTrue(classification.Reason.HasFlag(ServiceActionClassificationReason.ExecutionOver));
    }

    [Test]
    public void HaircutBeforeOrderedWashCompletion_RemainsNormalService()
    {
        ServiceActionClassification classification = Classify(
            WashThenCutOrder(), Result(ServiceActionType.Haircut, ServiceTool.Scissors));

        Assert.AreEqual(ServiceRelation.NormalService, classification.Relation);
    }

    [Test]
    public void StaleResult_ProducesNoFeedback()
    {
        SalonGameModel game = CreateGameWithHaircutCustomer(6101, out CustomerModel customer);
        ApplyFixture fixture = CreateApplyFixture(customer.Id, customer.Station);
        ActionResult result = Result(fixture.Token, ServiceActionType.Shower, ServiceTool.Shower);
        fixture.Context.ClearInteractionContext(ClearReason.FocusExited);
        ApplyActionResult stale = fixture.Applier.Apply(fixture.State, fixture.Context, result);

        ProjectFeedback(game, customer, stale, ServiceActionClassifier.Classify(
            result, CutOrder(HaircutTool.Scissors), fixture.State.Progress.CreateSnapshot(),
            fixture.State.Physical.CreateSnapshot()));

        Assert.AreEqual(ApplyStatus.StaleContext, stale.Status);
        Assert.AreEqual(CustomerReactionKind.None, customer.ReactionKind);
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion);
        Assert.AreEqual(CustomerWrongServiceKind.None, customer.WrongServiceKind);
        Assert.AreEqual(0, customer.ExtraServiceCount);
        Assert.AreEqual(0, game.Mistakes);
        Assert.AreEqual(0f, customer.ReactionRemaining);
    }

    [Test]
    public void DuplicateActionId_ProducesFeedbackOnce()
    {
        SalonGameModel game = CreateGameWithHaircutCustomer(6102, out CustomerModel customer);
        ApplyFixture fixture = CreateApplyFixture(customer.Id, customer.Station);
        ActionResult result = Result(fixture.Token, ServiceActionType.Shower, ServiceTool.Shower);
        ServiceActionClassification classification = ServiceActionClassifier.Classify(
            result, CutOrder(HaircutTool.Scissors), fixture.State.Progress.CreateSnapshot(),
            fixture.State.Physical.CreateSnapshot());

        ApplyActionResult first = fixture.Applier.Apply(fixture.State, fixture.Context, result);
        ProjectFeedback(game, customer, first, classification);
        ApplyActionResult duplicate = fixture.Applier.Apply(fixture.State, fixture.Context, result);
        ProjectFeedback(game, customer, duplicate, classification);

        Assert.AreEqual(ApplyStatus.Applied, first.Status);
        Assert.AreEqual(ApplyStatus.AlreadyApplied, duplicate.Status);
        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.AreEqual(1, game.Mistakes);
    }

    [Test]
    public void WrongHaircutTool_FeedbackDoesNotRewriteBusinessOutcomeFlagsOrDoubleCount()
    {
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(6103, new List<ServiceType> { ServiceType.Cut });
        Assert.IsTrue(game.ConfigureHaircutOrder(customer, SalonTool.ThinningShears));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(game.ApplyHaircutResult(
            customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.AreEqual(1, game.Mistakes);
        Assert.IsFalse(customer.HadServiceDelay);
        Assert.AreEqual(CustomerServiceFeedback.None, customer.ServiceFeedback);
        Assert.AreEqual(CustomerWrongServiceKind.Haircut, customer.WrongServiceKind);
    }

    private static ServiceActionClassification Classify(
        OrderDefinition order,
        ActionResult result,
        ServiceProgressSnapshot progress = null)
    {
        return ServiceActionClassifier.Classify(
            result,
            order,
            progress ?? new ServiceProgress().CreateSnapshot(),
            new CustomerPhysicalState().CreateSnapshot());
    }

    private static ActionResult Result(
        ServiceActionType action,
        ServiceTool tool,
        ExecutionQuality quality = ExecutionQuality.Perfect)
    {
        return Result(new ActionToken("classification-action", 1, 1, 1, 0, action, tool, 0f),
            action, tool, quality);
    }

    private static ActionResult Result(
        ActionToken token,
        ServiceActionType action,
        ServiceTool tool,
        ExecutionQuality quality = ExecutionQuality.Perfect)
    {
        return new ActionResult(token, ExecutionStatus.Executed, OrderEffect.None,
            quality == ExecutionQuality.Over ? MistakeSeverity.Major : MistakeSeverity.None,
            quality, PhysicalStateDelta.Empty, Array.Empty<MilestoneEvent>(),
            CustomerMetricsDelta.Empty, 0, null, DiagnosticCode.None, 1f);
    }

    private static OrderDefinition CutOrder(HaircutTool tool)
    {
        MilestoneId milestone = tool == HaircutTool.ThinningShears
            ? MilestoneId.ThinningShearsCompleted
            : tool == HaircutTool.Clippers ? MilestoneId.ClippersCompleted : MilestoneId.ScissorsCompleted;
        return new OrderDefinition(
            new[] { RequiredService.Cut },
            new[] { new ServiceMilestoneDefinition(milestone, MilestoneCatalog.KindOf(milestone), true) },
            new[] { tool }, false);
    }

    private static OrderDefinition WashOrder()
    {
        return new OrderDefinition(
            new[] { RequiredService.Wash },
            new[]
            {
                Required(MilestoneId.WetHairApplied),
                Required(MilestoneId.Shampooed),
                Required(MilestoneId.RinseClean)
            },
            Array.Empty<HaircutTool>(), false);
    }

    private static OrderDefinition WashThenCutOrder()
    {
        return new OrderDefinition(
            new[] { RequiredService.Wash, RequiredService.Cut },
            new[]
            {
                Required(MilestoneId.WetHairApplied),
                Required(MilestoneId.Shampooed),
                Required(MilestoneId.RinseClean),
                Required(MilestoneId.ScissorsCompleted)
            },
            new[] { HaircutTool.Scissors }, true);
    }

    private static ServiceMilestoneDefinition Required(MilestoneId id) =>
        new ServiceMilestoneDefinition(id, MilestoneCatalog.KindOf(id), true);

    private static ServiceMilestoneState State(MilestoneId id, bool ever, bool now) =>
        new ServiceMilestoneState(id, MilestoneCatalog.KindOf(id), ever, now);

    private static SalonGameModel CreateGameWithHaircutCustomer(int id, out CustomerModel customer)
    {
        var game = new SalonGameModel();
        customer = game.Spawn(id, new List<ServiceType> { ServiceType.Cut });
        customer.Station = 1;
        customer.State = CustomerState.Serving;
        return game;
    }

    private static ApplyFixture CreateApplyFixture(int customerId, int stationId)
    {
        var context = new InteractionContext();
        context.Focus(customerId, stationId);
        ActionToken token = context.BeginAction(ServiceActionType.Shower, ServiceTool.Shower, 0, 0f);
        var state = new CustomerServiceState(customerId, stationId, new CustomerPhysicalState(),
            CutOrder(HaircutTool.Scissors), new ServiceProgress(), new CustomerMetricsState(70f, 100f));
        return new ApplyFixture(state, context, token,
            new ActionResultApplier(new ServiceRuleConfig(), new InMemoryActionHistory(), _ => { }));
    }

    private static void ProjectFeedback(
        SalonGameModel game,
        CustomerModel customer,
        ApplyActionResult applied,
        ServiceActionClassification classification)
    {
        MethodInfo method = typeof(SalonGameModel).GetMethod("ProjectDomainActionFeedback",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(game, new object[] { customer, applied, classification });
    }

    private sealed class ApplyFixture
    {
        public ApplyFixture(CustomerServiceState state, InteractionContext context, ActionToken token,
            ActionResultApplier applier)
        {
            State = state;
            Context = context;
            Token = token;
            Applier = applier;
        }

        public CustomerServiceState State { get; }
        public InteractionContext Context { get; }
        public ActionToken Token { get; }
        public ActionResultApplier Applier { get; }
    }
}
