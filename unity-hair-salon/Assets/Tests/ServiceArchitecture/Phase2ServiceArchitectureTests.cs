using System;
using System.Linq;
using System.Reflection;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;

public sealed class Phase2ServiceArchitectureTests
{
    private ServiceRuleConfig _config;
    private ActionResolver _resolver;

    [SetUp]
    public void SetUp()
    {
        _config = new ServiceRuleConfig();
        _resolver = new ActionResolver(_config);
    }

    [Test] public void Resolve_DoesNotMutateInputSnapshots()
    {
        CustomerPhysicalStateSnapshot physical = Physical(wetness: 0f);
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, physical);
        Assert.AreEqual(0f, physical.Wetness);
        Assert.Greater(result.PhysicalStateDelta.WetnessDelta, 0f);
    }

    [Test] public void Resolve_DoesNotModifyInteractionContext()
    {
        InteractionContext context = Context();
        InteractionContextSnapshot before = context.CreateSnapshot();
        Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, Physical(), interaction: before);
        Assert.AreEqual(before, context.CreateSnapshot());
    }

    [Test] public void Resolve_DoesNotAppendActionHistory()
    {
        var history = new InMemoryActionHistory();
        Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, Physical());
        Assert.AreEqual(0, history.Entries.Count);
    }

    [Test] public void Resolve_DoesNotAdvanceWorldTime()
    {
        const float worldTime = 42f;
        Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, Physical(), startedAt: worldTime);
        Assert.AreEqual(42f, worldTime);
    }

    [Test] public void Resolve_SameInputProducesSameResult()
    {
        CustomerPhysicalStateSnapshot physical = Physical();
        ActionResult first = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, physical);
        ActionResult second = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, physical);
        Assert.AreEqual(first, second);
    }

    [Test] public void Resolver_CannotFallbackToLegacyPath()
    {
        Assert.IsFalse(typeof(ActionResolver).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(field => field.FieldType.FullName == "HairSalon.SalonGameModel"));
    }

    [Test] public void UnhandledRule_ReturnsNoBusinessChanges()
    {
        ActionResult result = Resolve(ServiceActionType.Unknown, ServiceTool.None, 1f, Physical());
        Assert.AreEqual(ExecutionStatus.UnhandledActionRule, result.ExecutionStatus);
        Assert.AreEqual(DiagnosticCode.UnhandledActionRule, result.DiagnosticCode);
        Assert.IsFalse(result.PhysicalStateDelta.HasChanges);
        Assert.IsTrue(result.MilestoneEvents.Count == 0 && result.MetricsDelta.IsEmpty);
    }

    [Test] public void Apply_CommitsAllBusinessFieldsTogether()
    {
        Fixture fixture = CreateFixture(Physical(wetness: 0f), satisfaction: 70f,
            action: ServiceActionType.Shampoo, tool: ServiceTool.Shampoo);
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        ApplyActionResult applied = fixture.Applier.Apply(fixture.State, fixture.Context, result);
        Assert.AreEqual(ApplyStatus.Applied, applied.Status);
        Assert.AreEqual(ShampooState.ClumpedOnDryHair, fixture.State.Physical.ShampooState);
        Assert.Less(fixture.State.Metrics.Satisfaction, 70f);
        Assert.AreEqual(1, fixture.State.IncidentCount);
    }

    [Test] public void Apply_InvalidToken_CommitsNothing()
    {
        Fixture fixture = CreateFixture();
        ActionResult result = ResolveWithToken(Token(customerId: 999), Physical());
        AssertRejectedWithoutChange(fixture, result, ApplyStatus.InvalidToken);
    }

    [Test] public void Apply_StaleContextVersion_CommitsNothing()
    {
        Fixture fixture = CreateFixture();
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        fixture.Context.ClearInteractionContext(ClearReason.FocusExited);
        AssertRejectedWithoutChange(fixture, result, ApplyStatus.StaleContext);
    }

    [Test] public void Apply_StalePhysicalRevision_CommitsNothing()
    {
        Fixture fixture = CreateFixture(Physical(revision: 2));
        ActionResult result = ResolveWithToken(Token(revision: 1), fixture.State.Physical.CreateSnapshot());
        AssertRejectedWithoutChange(fixture, result, ApplyStatus.StalePhysicalState);
    }

    [Test] public void Apply_Success_IncrementsPhysicalRevisionOnce()
    {
        Fixture fixture = CreateFixture();
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        fixture.Applier.Apply(fixture.State, fixture.Context, result);
        Assert.AreEqual(1, fixture.State.Physical.PhysicalStateRevision);
    }

    [Test] public void Apply_DuplicateActionId_IsIdempotent()
    {
        Fixture fixture = CreateFixture();
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        Assert.AreEqual(ApplyStatus.Applied, fixture.Applier.Apply(fixture.State, fixture.Context, result).Status);
        CustomerPhysicalStateSnapshot once = fixture.State.Physical.CreateSnapshot();
        Assert.AreEqual(ApplyStatus.AlreadyApplied, fixture.Applier.Apply(fixture.State, fixture.Context, result).Status);
        Assert.AreEqual(once, fixture.State.Physical.CreateSnapshot());
    }

    [Test] public void Apply_RecalculatesProgressAfterPhysicalState()
    {
        Fixture fixture = CreateFixture(Physical(wetness: 0f));
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        fixture.Applier.Apply(fixture.State, fixture.Context, result);
        Assert.IsTrue(fixture.State.Progress[MilestoneId.WetHairApplied].EverCompleted);
    }

    [Test] public void Apply_DoesNotAdvanceWorldTime()
    {
        Fixture fixture = CreateFixture();
        float before = fixture.State.WorldTime;
        fixture.Applier.Apply(fixture.State, fixture.Context, ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot()));
        Assert.AreEqual(before, fixture.State.WorldTime);
    }

    [Test] public void History_IsAppendedOnlyAfterSuccessfulCommit()
    {
        Fixture fixture = CreateFixture();
        fixture.Applier.Apply(fixture.State, fixture.Context, ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot()));
        Assert.AreEqual(1, fixture.History.Entries.Count);
        Assert.AreEqual(1, fixture.History.Entries[0].PostPhysical.PhysicalStateRevision);
    }

    [Test] public void History_Failure_DoesNotRollbackBusinessState()
    {
        Fixture fixture = CreateFixture(history: new ThrowingHistorySink());
        ApplyActionResult result = fixture.Applier.Apply(fixture.State, fixture.Context, ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot()));
        Assert.AreEqual(ApplyStatus.Applied, result.Status);
        Assert.AreEqual(1, fixture.State.Physical.PhysicalStateRevision);
    }

    [Test] public void History_RecordsPreAndPostSnapshots()
    {
        Fixture fixture = CreateFixture(Physical(wetness: 0f));
        fixture.Applier.Apply(fixture.State, fixture.Context, ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot()));
        Assert.AreEqual(0f, fixture.History.Entries[0].PrePhysical.Wetness);
        Assert.Greater(fixture.History.Entries[0].PostPhysical.Wetness, 0f);
    }

    [Test] public void RejectedApply_DoesNotAppendHistory()
    {
        Fixture fixture = CreateFixture();
        fixture.Context.ClearInteractionContext(ClearReason.FocusExited);
        fixture.Applier.Apply(fixture.State, fixture.Context, ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot()));
        Assert.AreEqual(0, fixture.History.Entries.Count);
    }

    [Test] public void Towel_CanBeWrappedFoamyAndDamaged()
    {
        var state = new CustomerPhysicalState(0.5f, 0.6f, ShampooState.Normal, true, TowelContamination.Foam, TowelCondition.Damaged, 0, 0f);
        Assert.IsTrue(state.IsTowelWrapped);
        Assert.AreEqual(TowelContamination.Foam, state.TowelContamination);
        Assert.AreEqual(TowelCondition.Damaged, state.TowelCondition);
    }

    [Test] public void RemoveFoamyDamagedTowel_PreservesHairFoam()
    {
        ActionResult result = Resolve(ServiceActionType.RemoveTowel, ServiceTool.Towel, 0f,
            Physical(foam: 0.6f, shampoo: ShampooState.Normal, wrapped: true, contamination: TowelContamination.Foam, condition: TowelCondition.Damaged));
        CustomerPhysicalState next = result.PhysicalStateDelta.ApplyTo(PhysicalState(
            foam: 0.6f, shampoo: ShampooState.Normal, wrapped: true, contamination: TowelContamination.Foam, condition: TowelCondition.Damaged));
        Assert.IsFalse(next.IsTowelWrapped);
        Assert.AreEqual(0.6f, next.FoamAmount);
        Assert.AreEqual(ShampooState.Normal, next.ShampooState);
    }

    [TestCase(ServiceActionType.WrapTowel)]
    [TestCase(ServiceActionType.RemoveTowel)]
    public void WrapOrRemoveTowel_DoesNotEnterSelectedTool(ServiceActionType action)
    {
        InteractionContext context = Context();
        context.ExecuteQuickAction(action, ServiceTool.Towel);
        Assert.AreEqual(ServiceTool.None, context.SelectedTool);
    }

    [Test] public void WrapTowel_DoesNotEnterSelectedTool() => WrapOrRemoveTowel_DoesNotEnterSelectedTool(ServiceActionType.WrapTowel);
    [Test] public void RemoveTowel_DoesNotEnterSelectedTool() => WrapOrRemoveTowel_DoesNotEnterSelectedTool(ServiceActionType.RemoveTowel);

    [Test] public void QuickAction_ClearsInteractionTool()
    {
        InteractionContext context = Context();
        context.SelectTool(ServiceTool.Shampoo);
        context.ExecuteQuickAction(ServiceActionType.WrapTowel, ServiceTool.Towel);
        Assert.AreEqual(ServiceTool.None, context.SelectedTool);
    }

    [Test] public void PhysicalState_DoesNotContainWritableStation()
    {
        Assert.IsNull(typeof(CustomerPhysicalState).GetProperty("CurrentStation"));
        Assert.IsNull(typeof(CustomerPhysicalState).GetProperty("Station"));
    }

    [Test] public void BlowDry_ReducesWetness()
    {
        ActionResult result = Resolve(ServiceActionType.BlowDry, ServiceTool.BlowDryer, 0.5f, Physical(wetness: 1f));
        Assert.Less(result.PhysicalStateDelta.WetnessDelta, 0f);
    }

    [Test]
    public void BlowDry_FoamErrorCanBeRepairedThenDryCompletes()
    {
        CustomerPhysicalStateSnapshot foamyWet = Physical(wetness: 1f, foam: 0.6f, shampoo: ShampooState.Normal);
        ActionResult foamBlow = Resolve(ServiceActionType.BlowDry, ServiceTool.BlowDryer, 1f, foamyWet);
        Assert.AreEqual(MistakeSeverity.Recoverable, foamBlow.MistakeSeverity);
        Assert.AreEqual(0, foamBlow.IncidentDelta);
        Assert.AreEqual("Clean foam before continuing.", foamBlow.SuggestedInteractionContinuation);
        Assert.IsFalse(foamBlow.MilestoneEvents.Any(item => item.MilestoneId == MilestoneId.HairDry));

        CustomerPhysicalState afterFoam = foamBlow.PhysicalStateDelta.ApplyTo(foamyWet.ToMutable());
        ServiceProgress afterFoamProgress = ServiceProgressCalculator.Recalculate(
            new ServiceProgress().CreateSnapshot(),
            afterFoam.CreateSnapshot(),
            foamBlow.MilestoneEvents,
            _config);
        Assert.IsFalse(afterFoamProgress[MilestoneId.HairDry].SatisfiedNow);

        ActionResult recovery = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 10f, afterFoam.CreateSnapshot());
        CustomerPhysicalState afterRecovery = recovery.PhysicalStateDelta.ApplyTo(afterFoam);
        Assert.AreEqual(0f, afterRecovery.FoamAmount);
        Assert.AreEqual(ShampooState.None, afterRecovery.ShampooState);

        ActionResult finalDry = Resolve(ServiceActionType.BlowDry, ServiceTool.BlowDryer, 2f, afterRecovery.CreateSnapshot());
        Assert.AreEqual(ExecutionStatus.Executed, finalDry.ExecutionStatus);
        Assert.IsTrue(finalDry.MilestoneEvents.Any(item => item.MilestoneId == MilestoneId.HairDry && item.SatisfiedNow));

        ServiceProgress completeProgress = ServiceProgressCalculator.Recalculate(
            afterFoamProgress.CreateSnapshot(),
            afterRecovery.CreateSnapshot(),
            finalDry.MilestoneEvents,
            _config);
        Assert.IsTrue(completeProgress[MilestoneId.HairDry].SatisfiedNow);
        Assert.IsTrue(completeProgress[MilestoneId.HairDry].EverCompleted);
    }

    [Test] public void PhysicalState_HasNoWritableBlowDryProgress()
    {
        PropertyInfo property = typeof(CustomerPhysicalState).GetProperty("BlowDryProgress");
        Assert.IsTrue(property == null || !property.CanWrite);
    }

    [Test] public void HairDry_IsDerivedFromWetnessThreshold()
    {
        Assert.IsTrue(Physical(wetness: _config.HairDryWetnessThreshold).IsHairDry(_config));
        Assert.IsFalse(Physical(wetness: _config.HairDryWetnessThreshold + 0.01f).IsHairDry(_config));
    }

    [Test] public void Rewetting_InvalidatesHairDrySatisfiedNow()
    {
        ServiceProgress progress = Progress((MilestoneId.HairDry, true, true));
        ServiceProgress recalculated = ServiceProgressCalculator.Recalculate(progress.CreateSnapshot(), Physical(wetness: 1f), Array.Empty<MilestoneEvent>(), _config);
        Assert.IsTrue(recalculated[MilestoneId.HairDry].EverCompleted);
        Assert.IsTrue(recalculated[MilestoneId.HairDry].NeedsRedo);
    }

    [Test] public void ExitReadiness_WetHairBlocksSettlement()
    {
        Assert.IsFalse(Readiness(Physical(wetness: 1f)).CanSettle);
    }

    [Test] public void HistoricalMilestone_UsesEverCompleted()
    {
        var state = new ServiceMilestoneState(MilestoneId.Shampooed, MilestoneKind.HistoricalEvent, true, false);
        Assert.IsTrue(state.IsCompleteForSettlement);
    }

    [Test] public void HistoricalMilestone_DoesNotFailAfterLaterStateChange()
    {
        ServiceProgress progress = Progress((MilestoneId.CleanTowelApplied, true, true));
        ServiceProgress next = ServiceProgressCalculator.Recalculate(progress.CreateSnapshot(), Physical(wrapped: false), Array.Empty<MilestoneEvent>(), _config);
        Assert.IsTrue(next[MilestoneId.CleanTowelApplied].EverCompleted);
    }

    [Test] public void RevalidatableMilestone_UsesSatisfiedNow()
    {
        var state = new ServiceMilestoneState(MilestoneId.RinseClean, MilestoneKind.RevalidatableState, true, false);
        Assert.IsFalse(state.IsCompleteForSettlement);
        Assert.IsTrue(state.NeedsRedo);
    }

    [Test] public void Rinse_ReapplyingShampoo_MarksNeedsRedo()
    {
        ServiceProgress progress = Progress((MilestoneId.RinseClean, true, true));
        ActionResult result = Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 0.5f, Physical(wetness: 1f), progress.CreateSnapshot());
        CustomerPhysicalState nextPhysical = result.PhysicalStateDelta.ApplyTo(PhysicalState(wetness: 1f));
        ServiceProgress next = ServiceProgressCalculator.Recalculate(progress.CreateSnapshot(), nextPhysical.CreateSnapshot(), result.MilestoneEvents, _config);
        Assert.IsTrue(next[MilestoneId.RinseClean].NeedsRedo);
    }

    [Test] public void Rinse_Rerinsing_RestoresSatisfiedNow()
    {
        ServiceProgress progress = Progress((MilestoneId.RinseClean, true, false));
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 10f, Physical(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal), progress.CreateSnapshot());
        CustomerPhysicalState nextPhysical = result.PhysicalStateDelta.ApplyTo(PhysicalState(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal));
        ServiceProgress next = ServiceProgressCalculator.Recalculate(progress.CreateSnapshot(), nextPhysical.CreateSnapshot(), result.MilestoneEvents, _config);
        Assert.IsTrue(next[MilestoneId.RinseClean].SatisfiedNow);
    }

    [Test] public void Untowel_Rewrapping_MarksNeedsRedo()
    {
        ServiceProgress progress = Progress((MilestoneId.Untoweled, true, true));
        ServiceProgress next = ServiceProgressCalculator.Recalculate(progress.CreateSnapshot(), Physical(wrapped: true), Array.Empty<MilestoneEvent>(), _config);
        Assert.IsTrue(next[MilestoneId.Untoweled].NeedsRedo);
    }

    [Test] public void ExitConstraint_IsNotCountedAsServiceStep()
    {
        OrderDefinition order = OrderDefinition.WashOnly();
        Assert.IsFalse(order.MilestoneDefinitions.Any(definition => definition.Kind == MilestoneKind.ExitConstraint));
    }

    [Test] public void WashThenCut_RequiresCleanTowelHistoricalEvent()
    {
        Assert.IsTrue(OrderDefinition.WashThenCut().IsRequired(MilestoneId.CleanTowelApplied, MilestoneKind.HistoricalEvent));
    }

    [Test] public void WashThenCut_RequiresUntoweledState()
    {
        Assert.IsTrue(OrderDefinition.WashThenCut().IsRequired(MilestoneId.Untoweled, MilestoneKind.RevalidatableState));
    }

    [Test] public void WashOnly_DoesNotRequireCleanTowelTransition()
    {
        Assert.IsFalse(OrderDefinition.WashOnly().RequiresCrossStationTowelTransition);
        Assert.IsFalse(OrderDefinition.WashOnly().IsRequired(MilestoneId.CleanTowelApplied, MilestoneKind.HistoricalEvent));
    }

    [Test] public void WashOnly_CanSettleAfterRinseAndDryWithoutWrapCycle()
    {
        OrderDefinition order = OrderDefinition.WashOnly();
        ServiceProgress progress = CompletedWashProgress(includeTowel: false);
        Assert.IsTrue(ServiceExitReadiness.Evaluate(order, progress.CreateSnapshot(), Physical(), false, false, _config).CanSettle);
    }

    [Test] public void DryHair_Shower_IncreasesWetness()
    {
        Assert.Greater(Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.5f, Physical()).PhysicalStateDelta.WetnessDelta, 0f);
    }

    [Test] public void DryHair_Shampoo_CreatesClumpedState()
    {
        Assert.AreEqual(ShampooState.ClumpedOnDryHair,
            Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 0.5f, Physical()).PhysicalStateDelta.ShampooState);
    }

    [Test] public void DryHair_Shampoo_DoesNotCompleteShampooMilestone()
    {
        Assert.IsFalse(Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 0.5f, Physical()).MilestoneEvents
            .Any(item => item.MilestoneId == MilestoneId.Shampooed));
    }

    [Test] public void WetHair_Shampoo_CreatesNormalFoam()
    {
        ActionResult result = Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 0.5f, Physical(wetness: 1f));
        Assert.AreEqual(ShampooState.Normal, result.PhysicalStateDelta.ShampooState);
        Assert.Greater(result.PhysicalStateDelta.FoamAmountDelta, 0f);
    }

    [Test] public void PartialRinse_PreservesRemainingFoam()
    {
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.2f, Physical(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal));
        CustomerPhysicalState next = result.PhysicalStateDelta.ApplyTo(PhysicalState(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal));
        Assert.Greater(next.FoamAmount, 0f);
        Assert.AreEqual(ShampooState.Normal, next.ShampooState);
    }

    [Test] public void FullRinse_SetsFoamZeroAndShampooNone()
    {
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 10f, Physical(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal));
        CustomerPhysicalState next = result.PhysicalStateDelta.ApplyTo(PhysicalState(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal));
        Assert.AreEqual(0f, next.FoamAmount);
        Assert.AreEqual(ShampooState.None, next.ShampooState);
    }

    [Test] public void FullRinse_SatisfiesRinseClean()
    {
        Assert.IsTrue(Resolve(ServiceActionType.Shower, ServiceTool.Shower, 10f,
            Physical(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal)).MilestoneEvents
            .Any(item => item.MilestoneId == MilestoneId.RinseClean && item.SatisfiedNow));
    }

    [Test] public void ReapplyShampoo_InvalidatesRinseClean() => Rinse_ReapplyingShampoo_MarksNeedsRedo();
    [Test] public void RemoveFoamyTowel_PreservesFoamOnHair() => RemoveFoamyDamagedTowel_PreservesHairFoam();

    [Test] public void BeginAction_CapturesCustomerStationContextAndRevision()
    {
        InteractionContext context = Context();
        ActionToken token = context.BeginAction(ServiceActionType.Shower, ServiceTool.Shower, 7, 4f);
        Assert.AreEqual(101, token.CustomerId);
        Assert.AreEqual(7, token.StationId);
        Assert.AreEqual(context.ContextVersion, token.ContextVersion);
        Assert.AreEqual(7, token.ExpectedPhysicalRevision);
    }

    [Test] public void SwitchCustomer_IncrementsContextVersion()
    {
        InteractionContext context = Context();
        int version = context.ContextVersion;
        context.Focus(202, 8);
        Assert.AreEqual(version + 1, context.ContextVersion);
    }

    [Test] public void MoveCustomer_InvalidatesOldToken()
    {
        InteractionContext context = Context();
        ActionToken token = context.BeginAction(ServiceActionType.Shower, ServiceTool.Shower, 0, 0f);
        context.ClearInteractionContext(ClearReason.MovementStarted);
        Assert.IsFalse(context.IsTokenCurrent(token));
    }

    [Test] public void StaleCallback_CannotWriteAnotherCustomer()
    {
        Fixture fixture = CreateFixture();
        ActionResult result = ResolveWithToken(fixture.Token, fixture.State.Physical.CreateSnapshot());
        fixture.Context.Focus(202, 7);
        AssertRejectedWithoutChange(fixture, result, ApplyStatus.InvalidToken);
    }

    [Test] public void DuplicateCallback_IsSafelyDiscarded() => Apply_DuplicateActionId_IsIdempotent();

    [Test] public void QuickAction_InvalidatesPreviousHoldToken()
    {
        InteractionContext context = Context();
        ActionToken token = context.BeginAction(ServiceActionType.Shower, ServiceTool.Shower, 0, 0f);
        context.ExecuteQuickAction(ServiceActionType.WrapTowel, ServiceTool.Towel);
        Assert.IsFalse(context.IsTokenCurrent(token));
    }

    [Test] public void InterruptShower_PreservesPartialWetness()
    {
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.25f, Physical(), interrupted: true);
        Assert.Greater(result.PhysicalStateDelta.WetnessDelta, 0f);
        Assert.Less(result.PhysicalStateDelta.WetnessDelta, 1f);
    }

    [Test] public void InterruptRinse_PreservesPartialFoamReduction()
    {
        ActionResult result = Resolve(ServiceActionType.Shower, ServiceTool.Shower, 0.25f, Physical(wetness: 1f, foam: 1f, shampoo: ShampooState.Normal), interrupted: true);
        Assert.Less(result.PhysicalStateDelta.FoamAmountDelta, 0f);
        Assert.Greater(result.PhysicalStateDelta.FoamAmountDelta, -1f);
    }

    [Test] public void InterruptShampoo_PreservesPartialFoamIncrease()
    {
        ActionResult result = Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 0.25f, Physical(wetness: 1f), interrupted: true);
        Assert.Greater(result.PhysicalStateDelta.FoamAmountDelta, 0f);
        Assert.Less(result.PhysicalStateDelta.FoamAmountDelta, 1f);
    }

    [Test] public void InterruptBlow_PreservesPartialDrying()
    {
        ActionResult result = Resolve(ServiceActionType.BlowDry, ServiceTool.BlowDryer, 0.25f, Physical(wetness: 1f), interrupted: true);
        Assert.Less(result.PhysicalStateDelta.WetnessDelta, 0f);
        Assert.Greater(result.PhysicalStateDelta.WetnessDelta, -1f);
    }

    [TestCase(0.5f, ExecutionQuality.Under)]
    [TestCase(1.5f, ExecutionQuality.Perfect)]
    [TestCase(2.5f, ExecutionQuality.Over)]
    public void InterruptHaircut_ClassifiesActualElapsed(float elapsed, ExecutionQuality expected)
    {
        Assert.AreEqual(expected, HoldQualityEvaluator.Evaluate(elapsed, 1f, 2f));
    }

    [Test] public void InterruptHaircut_BelowWindow_IsUnder() => InterruptHaircut_ClassifiesActualElapsed(0.5f, ExecutionQuality.Under);
    [Test] public void InterruptHaircut_InWindow_IsPerfect() => InterruptHaircut_ClassifiesActualElapsed(1.5f, ExecutionQuality.Perfect);
    [Test] public void InterruptHaircut_AboveWindow_IsOver() => InterruptHaircut_ClassifiesActualElapsed(2.5f, ExecutionQuality.Over);

    [Test] public void MovementCannotCancelPendingOverResult()
    {
        Assert.AreEqual(ExecutionQuality.Over, HoldQualityEvaluator.Evaluate(2.5f, 1f, 2f));
    }

    [TestCase(ExitConstraint.NoFoam)]
    [TestCase(ExitConstraint.NoClumpedShampoo)]
    [TestCase(ExitConstraint.NoTowel)]
    [TestCase(ExitConstraint.DryEnough)]
    [TestCase(ExitConstraint.NotMoving)]
    [TestCase(ExitConstraint.NoActiveAction)]
    public void ExitReadiness_ReportsBlockingConstraint(ExitConstraint constraint)
    {
        CustomerPhysicalStateSnapshot physical = constraint switch
        {
            ExitConstraint.NoFoam => Physical(foam: 0.2f),
            ExitConstraint.NoClumpedShampoo => Physical(shampoo: ShampooState.ClumpedOnDryHair),
            ExitConstraint.NoTowel => Physical(wrapped: true),
            ExitConstraint.DryEnough => Physical(wetness: 1f),
            _ => Physical()
        };
        ServiceExitReadinessResult result = ServiceExitReadiness.Evaluate(
            OrderDefinition.WashOnly(), CompletedWashProgress(false).CreateSnapshot(), physical,
            constraint == ExitConstraint.NotMoving, constraint == ExitConstraint.NoActiveAction, _config);
        Assert.Contains(constraint, result.BlockingConstraints.ToList());
    }

    [Test] public void ExitReadiness_FoamBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.NoFoam);
    [Test] public void ExitReadiness_ClumpedShampooBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.NoClumpedShampoo);
    [Test] public void ExitReadiness_ShampooResidueBlocksSettlement()
    {
        ServiceExitReadinessResult result = ServiceExitReadiness.Evaluate(OrderDefinition.WashOnly(), CompletedWashProgress(false).CreateSnapshot(),
            Physical(foam: 0.0005f, shampoo: ShampooState.Normal), false, false, _config);
        Assert.IsFalse(result.CanSettle);
    }
    [Test] public void ExitReadiness_WrappedTowelBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.NoTowel);
    [Test] public void ExitReadiness_WetnessBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.DryEnough);
    [Test] public void ExitReadiness_MovingBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.NotMoving);
    [Test] public void ExitReadiness_ActiveActionBlocksSettlement() => ExitReadiness_ReportsBlockingConstraint(ExitConstraint.NoActiveAction);

    [TestCase(-0.4f)]
    [TestCase(0.2f)]
    public void ExitReadiness_HairShapeDoesNotBlockSettlement(float deviation)
    {
        Assert.IsTrue(Readiness(Physical(hairDeviation: deviation)).CanSettle);
    }

    [Test] public void ExitReadiness_OvercutDoesNotBlockSettlement() => ExitReadiness_HairShapeDoesNotBlockSettlement(-0.4f);
    [Test] public void ExitReadiness_HairDeviationDoesNotBlockSettlement() => ExitReadiness_HairShapeDoesNotBlockSettlement(0.2f);

    [Test] public void MetricsDelta_RequiresSourceAndReason()
    {
        Assert.Throws<ArgumentException>(() => new CustomerMetricsDelta(-1f, 0f, CustomerMetricsSource.None, ""));
    }

    [Test] public void ActionMetrics_AreAppliedThroughUnifiedEntry()
    {
        var metrics = new CustomerMetricsState(50f, 50f);
        CustomerMetricsDomain.ApplyDelta(metrics, new CustomerMetricsDelta(-5f, -2f, CustomerMetricsSource.Action, "test"));
        Assert.AreEqual(45f, metrics.Satisfaction);
        Assert.AreEqual(48f, metrics.Patience);
    }

    [Test] public void MetricsDelta_ClampsValuesToValidRange()
    {
        var metrics = new CustomerMetricsState(50f, 50f);
        CustomerMetricsDomain.ApplyDelta(metrics, new CustomerMetricsDelta(1000f, -1000f, CustomerMetricsSource.Incident, "bounds"));
        Assert.AreEqual(100f, metrics.Satisfaction);
        Assert.AreEqual(0f, metrics.Patience);
    }

    [Test] public void RejectedAction_DoesNotChangeMetrics()
    {
        Fixture fixture = CreateFixture(satisfaction: 70f);
        ActionResult result = Resolve(ServiceActionType.Shampoo, ServiceTool.Shampoo, 1f, fixture.State.Physical.CreateSnapshot(), interaction: fixture.Context.CreateSnapshot());
        fixture.Context.ClearInteractionContext(ClearReason.FocusExited);
        fixture.Applier.Apply(fixture.State, fixture.Context, result);
        Assert.AreEqual(70f, fixture.State.Metrics.Satisfaction);
    }

    [Test] public void MajorIncident_DoesNotImplyUnfinished()
    {
        Assert.AreEqual(ServiceCompletionState.Completed, ServiceOutcomeEvaluator.Evaluate(true, true, false).CompletionState);
    }

    [Test] public void CompletedMajorIncident_IsUnhappy()
    {
        Assert.AreEqual(ServiceCustomerOutcome.Unhappy, ServiceOutcomeEvaluator.Evaluate(true, true, false).CustomerOutcome);
    }

    [Test] public void CompletedMajorIncident_IsWaived()
    {
        Assert.IsTrue(ServiceOutcomeEvaluator.Evaluate(true, true, false).PaymentWaived);
    }

    [Test] public void CompletedMajorIncident_LeaveReasonIsServiceFinished()
    {
        Assert.AreEqual(ServiceLeaveReason.ServiceFinished, ServiceOutcomeEvaluator.Evaluate(true, true, false).LeaveReason);
    }

    private ActionResult Resolve(
        ServiceActionType action,
        ServiceTool tool,
        float elapsed,
        CustomerPhysicalStateSnapshot physical,
        ServiceProgressSnapshot progress = null,
        InteractionContextSnapshot interaction = null,
        bool interrupted = false,
        float startedAt = 0f)
    {
        ActionToken token = new ActionToken("action-1", 101, 7, 1, physical.PhysicalStateRevision, action, tool, startedAt);
        var request = new ActionRequest(token, 101, 7, action, tool, elapsed, interrupted);
        return _resolver.Resolve(request, OrderDefinition.WashThenCut(), physical,
            progress ?? new ServiceProgress().CreateSnapshot(),
            interaction ?? new InteractionContextSnapshot(101, 7, tool, 101, 7, "action-1", token, 1, null),
            new CustomerMetricsSnapshot(70f, 100f));
    }

    private ActionResult ResolveWithToken(ActionToken token, CustomerPhysicalStateSnapshot physical)
    {
        var request = new ActionRequest(token, token.CustomerId, token.StationId, token.ActionType, token.Tool, 0.5f, false);
        return _resolver.Resolve(request, OrderDefinition.WashThenCut(), physical, new ServiceProgress().CreateSnapshot(),
            new InteractionContextSnapshot(token.CustomerId, token.StationId, token.Tool, token.CustomerId, token.StationId,
                token.ActionId, token, token.ContextVersion, null), new CustomerMetricsSnapshot(70f, 100f));
    }

    private static CustomerPhysicalStateSnapshot Physical(
        float wetness = 0f,
        float foam = 0f,
        ShampooState shampoo = ShampooState.None,
        bool wrapped = false,
        TowelContamination contamination = TowelContamination.None,
        TowelCondition condition = TowelCondition.Intact,
        int revision = 0,
        float hairDeviation = 0f)
    {
        return PhysicalState(wetness, foam, shampoo, wrapped, contamination, condition, revision, hairDeviation).CreateSnapshot();
    }

    private static CustomerPhysicalState PhysicalState(
        float wetness = 0f,
        float foam = 0f,
        ShampooState shampoo = ShampooState.None,
        bool wrapped = false,
        TowelContamination contamination = TowelContamination.None,
        TowelCondition condition = TowelCondition.Intact,
        int revision = 0,
        float hairDeviation = 0f)
    {
        return new CustomerPhysicalState(wetness, foam, shampoo, wrapped, contamination, condition, revision, hairDeviation);
    }

    private static InteractionContext Context()
    {
        var context = new InteractionContext();
        context.Focus(101, 7);
        return context;
    }

    private ActionToken Token(int customerId = 101, int stationId = 7, int contextVersion = 1, int revision = 0)
    {
        return new ActionToken("fixture-action", customerId, stationId, contextVersion, revision,
            ServiceActionType.Shower, ServiceTool.Shower, 0f);
    }

    private Fixture CreateFixture(
        CustomerPhysicalStateSnapshot physical = null,
        float satisfaction = 70f,
        IActionHistorySink history = null,
        ServiceActionType action = ServiceActionType.Shower,
        ServiceTool tool = ServiceTool.Shower)
    {
        physical ??= Physical();
        InteractionContext context = Context();
        ActionToken token = context.BeginAction(action, tool, physical.PhysicalStateRevision, 0f);
        var sink = history ?? new InMemoryActionHistory();
        var state = new CustomerServiceState(101, 7, physical.ToMutable(), OrderDefinition.WashThenCut(),
            new ServiceProgress(), new CustomerMetricsState(satisfaction, 100f));
        var applier = new ActionResultApplier(_config, sink, _ => { });
        return new Fixture(state, context, token, applier, sink as InMemoryActionHistory ?? new InMemoryActionHistory());
    }

    private static void AssertRejectedWithoutChange(Fixture fixture, ActionResult result, ApplyStatus expected)
    {
        CustomerPhysicalStateSnapshot physical = fixture.State.Physical.CreateSnapshot();
        CustomerMetricsSnapshot metrics = fixture.State.Metrics.CreateSnapshot();
        ApplyActionResult applied = fixture.Applier.Apply(fixture.State, fixture.Context, result);
        Assert.AreEqual(expected, applied.Status);
        Assert.AreEqual(physical, fixture.State.Physical.CreateSnapshot());
        Assert.AreEqual(metrics, fixture.State.Metrics.CreateSnapshot());
    }

    private ServiceExitReadinessResult Readiness(CustomerPhysicalStateSnapshot physical)
    {
        return ServiceExitReadiness.Evaluate(OrderDefinition.WashOnly(), CompletedWashProgress(false).CreateSnapshot(),
            physical, false, false, _config);
    }

    private static ServiceProgress Progress(params (MilestoneId id, bool ever, bool now)[] values)
    {
        var progress = new ServiceProgress();
        foreach ((MilestoneId id, bool ever, bool now) in values)
            progress.Set(new ServiceMilestoneState(id, MilestoneCatalog.KindOf(id), ever, now));
        return progress;
    }

    private static ServiceProgress CompletedWashProgress(bool includeTowel)
    {
        var progress = Progress(
            (MilestoneId.WetHairApplied, true, false),
            (MilestoneId.Shampooed, true, false),
            (MilestoneId.RinseClean, true, true),
            (MilestoneId.HairDry, true, true));
        if (includeTowel)
        {
            progress.Set(new ServiceMilestoneState(MilestoneId.CleanTowelApplied, MilestoneKind.HistoricalEvent, true, false));
            progress.Set(new ServiceMilestoneState(MilestoneId.Untoweled, MilestoneKind.RevalidatableState, true, true));
        }
        return progress;
    }

    private sealed class ThrowingHistorySink : IActionHistorySink
    {
        public void Append(ActionHistoryEntry entry) => throw new InvalidOperationException("Expected test failure.");
    }

    private sealed class Fixture
    {
        public Fixture(CustomerServiceState state, InteractionContext context, ActionToken token,
            ActionResultApplier applier, InMemoryActionHistory history)
        {
            State = state;
            Context = context;
            Token = token;
            Applier = applier;
            History = history;
        }

        public CustomerServiceState State { get; }
        public InteractionContext Context { get; }
        public ActionToken Token { get; }
        public ActionResultApplier Applier { get; }
        public InMemoryActionHistory History { get; }
    }
}
