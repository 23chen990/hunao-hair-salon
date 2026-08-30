using System;
using System.Collections.Generic;
using System.Linq;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>仅依赖请求和不可变快照的确定性动作解析器。</summary>
    public sealed class ActionResolver
    {
        private readonly ServiceRuleConfig _config;
        private readonly Dictionary<ServiceActionType, Func<ActionRequest, OrderDefinition, CustomerPhysicalStateSnapshot,
            ServiceProgressSnapshot, ActionResult>> _rules;

        public ActionResolver(ServiceRuleConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rules = new Dictionary<ServiceActionType, Func<ActionRequest, OrderDefinition, CustomerPhysicalStateSnapshot,
                ServiceProgressSnapshot, ActionResult>>
            {
                { ServiceActionType.Shower, ResolveShower },
                { ServiceActionType.Shampoo, ResolveShampoo },
                { ServiceActionType.BlowDry, ResolveBlowDry },
                { ServiceActionType.WrapTowel, ResolveWrapTowel },
                { ServiceActionType.RemoveTowel, ResolveRemoveTowel }
            };
        }

        public ActionResult Resolve(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress,
            InteractionContextSnapshot interaction,
            CustomerMetricsSnapshot metrics)
        {
            if (request == null || order == null || physical == null || progress == null || interaction == null || metrics == null)
                throw new ArgumentNullException(nameof(request));
            if (!IsRequestConsistent(request, physical, interaction)) return ActionResult.Invalid(request);
            if (request.ActionType == ServiceActionType.Haircut)
                return ResolveHaircut(request, order, physical, progress);
            if (request.ActionType == ServiceActionType.ServiceCompletion)
                return ResolveServiceCompletion(request, order, physical);
            if (!_rules.TryGetValue(request.ActionType, out Func<ActionRequest, OrderDefinition, CustomerPhysicalStateSnapshot,
                ServiceProgressSnapshot, ActionResult> rule))
                return ActionResult.Unhandled(request);
            return rule(request, order, physical, progress);
        }

        private static ActionResult ResolveServiceCompletion(
            ActionRequest request, OrderDefinition order, CustomerPhysicalStateSnapshot physical)
        {
            var events = new List<MilestoneEvent>();
            PhysicalStateDelta delta = PhysicalStateDelta.Empty;
            if (request.Tool == ServiceTool.Shower)
            {
                AddIfRequired(events, order, MilestoneId.WetHairApplied);
                AddIfRequired(events, order, MilestoneId.Shampooed);
                AddIfRequired(events, order, MilestoneId.RinseClean);
                delta = new PhysicalStateDelta(
                    wetnessDelta: 1f - physical.Wetness,
                    foamAmountDelta: -physical.FoamAmount,
                    shampooState: ShampooState.None,
                    isTowelWrapped: false,
                    towelContamination: TowelContamination.None);
            }
            else if (request.Tool == ServiceTool.BlowDryer)
            {
                AddIfRequired(events, order, MilestoneId.HairDry);
                delta = new PhysicalStateDelta(wetnessDelta: -physical.Wetness);
            }
            else if (request.Tool == ServiceTool.Scissors)
            {
                AddIfRequired(events, order, MilestoneId.ScissorsCompleted);
                AddIfRequired(events, order, MilestoneId.ThinningShearsCompleted);
                AddIfRequired(events, order, MilestoneId.ClippersCompleted);
            }

            return new ActionResult(request.ActionToken, ExecutionStatus.Executed,
                OrderEffect.Progress, MistakeSeverity.None, ExecutionQuality.Perfect,
                delta, events, CustomerMetricsDelta.Empty, 0,
                "NormalService", DiagnosticCode.None, request.ElapsedTime);
        }

        private static void AddIfRequired(
            List<MilestoneEvent> events, OrderDefinition order, MilestoneId id)
        {
            if (order.IsRequired(id, MilestoneCatalog.KindOf(id)))
                events.Add(new MilestoneEvent(id, true, true));
        }

        private static bool IsRequestConsistent(
            ActionRequest request,
            CustomerPhysicalStateSnapshot physical,
            InteractionContextSnapshot interaction)
        {
            ActionToken token = request.ActionToken;
            return Numeric.IsFinite(request.ElapsedTime) && request.ElapsedTime >= 0f
                && request.CustomerId == token.CustomerId && request.StationId == token.StationId
                && request.ActionType == token.ActionType && request.Tool == token.Tool
                && token.ExpectedPhysicalRevision == physical.PhysicalStateRevision
                && interaction.FocusedCustomerId == token.CustomerId
                && interaction.FocusedStationId == token.StationId
                && interaction.ContextVersion == token.ContextVersion
                && interaction.ActiveActionId == token.ActionId;
        }

        private ActionResult ResolveShower(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (request.Tool != ServiceTool.Shower) return ActionResult.Unhandled(request);
            bool requestedWash = order.RequiredServices.Contains(RequiredService.Wash);
            if (physical.FoamAmount > 0f && physical.ShampooState == ShampooState.Normal)
            {
                float reduction = Math.Min(physical.FoamAmount, request.ElapsedTime * _config.RinseFoamPerSecond);
                float remaining = Numeric.Clamp01(physical.FoamAmount - reduction);
                bool complete = remaining <= _config.ExitFoamThreshold;
                var delta = new PhysicalStateDelta(
                    wetnessDelta: Math.Min(1f - physical.Wetness, request.ElapsedTime * _config.ShowerWetnessPerSecond),
                    foamAmountDelta: -reduction,
                    shampooState: complete ? ShampooState.None : ShampooState.Normal);
                MilestoneEvent[] events = complete && requestedWash
                    ? new[] { new MilestoneEvent(MilestoneId.RinseClean, true, true) }
                    : Array.Empty<MilestoneEvent>();
                return requestedWash
                    ? Executed(request, complete ? OrderEffect.Progress : OrderEffect.None, delta, events)
                    : UnrequestedWash(request, delta, events, "Unrequested shower service.");
            }

            float wetnessDelta = Math.Min(1f - physical.Wetness, request.ElapsedTime * _config.ShowerWetnessPerSecond);
            bool reachesWet = physical.Wetness + wetnessDelta >= _config.WetHairThreshold;
            MilestoneEvent[] wetEvents = reachesWet && requestedWash
                ? new[] { new MilestoneEvent(MilestoneId.WetHairApplied, true, true) }
                : Array.Empty<MilestoneEvent>();
            var wetDelta = new PhysicalStateDelta(wetnessDelta: wetnessDelta);
            return requestedWash
                ? Executed(request, reachesWet ? OrderEffect.Progress : OrderEffect.None, wetDelta, wetEvents)
                : UnrequestedWash(request, wetDelta, wetEvents, "Unrequested shower service.");
        }

        private ActionResult ResolveShampoo(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (request.Tool != ServiceTool.Shampoo) return ActionResult.Unhandled(request);
            bool requestedWash = order.RequiredServices.Contains(RequiredService.Wash);
            if (physical.Wetness < _config.WetHairThreshold)
            {
                return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.None,
                    MistakeSeverity.Recoverable, ExecutionQuality.NotApplicable,
                    new PhysicalStateDelta(shampooState: ShampooState.ClumpedOnDryHair),
                    Array.Empty<MilestoneEvent>(),
                    new CustomerMetricsDelta(_config.DryShampooSatisfactionPenalty
                        - (requestedWash ? 0f : _config.UnrequestedServiceSatisfactionPenalty), 0f,
                        CustomerMetricsSource.Action, "Shampoo applied to dry hair."),
                    requestedWash ? 1 : 2, "Wet hair, then shampoo again.",
                    DiagnosticCode.None, request.ElapsedTime);
            }

            float increase = Math.Min(1f - physical.FoamAmount, request.ElapsedTime * _config.ShampooFoamPerSecond);
            float nextFoam = Numeric.Clamp01(physical.FoamAmount + increase);
            bool completed = nextFoam >= _config.ShampooMilestoneFoamThreshold;
            bool wasCompleted = progress[MilestoneId.Shampooed].EverCompleted;
            OrderEffect effect = !requestedWash ? OrderEffect.ExtraService
                : completed && !wasCompleted ? OrderEffect.Progress
                : wasCompleted ? OrderEffect.ExtraService : OrderEffect.None;
            MistakeSeverity mistake = !requestedWash || wasCompleted
                ? MistakeSeverity.Minor : MistakeSeverity.None;
            var events = new List<MilestoneEvent>
            {
                new MilestoneEvent(MilestoneId.RinseClean, false, false)
            };
            if (completed && requestedWash)
                events.Add(new MilestoneEvent(MilestoneId.Shampooed, true, true));
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, effect, mistake,
                ExecutionQuality.NotApplicable,
                new PhysicalStateDelta(foamAmountDelta: increase, shampooState: ShampooState.Normal),
                events,
                requestedWash ? CustomerMetricsDelta.Empty
                    : new CustomerMetricsDelta(-_config.UnrequestedServiceSatisfactionPenalty, 0f,
                        CustomerMetricsSource.Action, "Unrequested shampoo service."),
                requestedWash ? 0 : 1, null, DiagnosticCode.None, request.ElapsedTime);
        }

        private ActionResult ResolveBlowDry(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (request.Tool != ServiceTool.BlowDryer) return ActionResult.Unhandled(request);

            if (physical.IsTowelWrapped)
                return ResolveBlowDryOnTowel(request, physical);

            if (physical.FoamAmount > _config.ExitFoamThreshold)
                return ResolveBlowDryOnFoam(request, physical);

            if (physical.Wetness <= _config.HairDryWetnessThreshold)
                return ResolveDryBlow(request, physical);

            float reduction = Math.Min(physical.Wetness, request.ElapsedTime * _config.BlowDryWetnessPerSecond);
            bool dry = physical.Wetness - reduction <= _config.HairDryWetnessThreshold;
            MilestoneEvent[] events = dry
                ? new[] { new MilestoneEvent(MilestoneId.HairDry, true, true) }
                : Array.Empty<MilestoneEvent>();
            return Executed(
                request,
                dry ? OrderEffect.Progress : OrderEffect.None,
                new PhysicalStateDelta(wetnessDelta: -reduction),
                events,
                dry ? ExecutionQuality.Perfect : ExecutionQuality.Under);
        }

        private ActionResult ResolveBlowDryOnFoam(
            ActionRequest request,
            CustomerPhysicalStateSnapshot physical)
        {
            float effectiveRate = Math.Max(0f, _config.BlowDryWetnessPerSecond * _config.BlowDryFoamEffectMultiplier);
            float reduction = Math.Min(physical.Wetness, request.ElapsedTime * effectiveRate);
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.None,
                MistakeSeverity.Recoverable, ExecutionQuality.NotApplicable,
                new PhysicalStateDelta(wetnessDelta: -reduction),
                Array.Empty<MilestoneEvent>(),
                new CustomerMetricsDelta(-Math.Max(0.1f, _config.UnrequestedServiceSatisfactionPenalty * .25f), 0f,
                    CustomerMetricsSource.Action, "Blow drying foam causes splash and requires recovery."),
                0, "Clean foam before continuing.", DiagnosticCode.None,
                request.ElapsedTime);
        }

        private ActionResult ResolveBlowDryOnTowel(
            ActionRequest request,
            CustomerPhysicalStateSnapshot physical)
        {
            bool repeatedOrOverlong = physical.TowelCondition == TowelCondition.Damaged
                || request.ElapsedTime > _config.BlowDryTowelDamageSeconds;
            if (!repeatedOrOverlong)
            {
                return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.None,
                    MistakeSeverity.Recoverable, ExecutionQuality.Under,
                    PhysicalStateDelta.Empty,
                    Array.Empty<MilestoneEvent>(),
                    new CustomerMetricsDelta(-2f, 0f,
                        CustomerMetricsSource.Action, "Blow-drying with towel; towel is becoming loose."),
                    0, "Remove the towel before normal blowdry.", DiagnosticCode.None,
                    request.ElapsedTime);
            }
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.ExtraService,
                MistakeSeverity.Major, ExecutionQuality.Over,
                new PhysicalStateDelta(towelCondition: TowelCondition.Damaged),
                Array.Empty<MilestoneEvent>(),
                new CustomerMetricsDelta(-Math.Max(0.1f, _config.UnrequestedServiceSatisfactionPenalty * .75f), 0f,
                    CustomerMetricsSource.Incident, "Towel damage from repeated/long blowdry."),
                1, "Remove/replace towel; use correct flow.", DiagnosticCode.None,
                request.ElapsedTime);
        }

        private ActionResult ResolveDryBlow(
            ActionRequest request,
            CustomerPhysicalStateSnapshot physical)
        {
            bool causesIncident = request.ElapsedTime > _config.BlowDryDryMajorThresholdSeconds;
            ExecutionQuality quality = request.ElapsedTime < _config.BlowDryDryGoodStartSeconds
                ? ExecutionQuality.Under
                : request.ElapsedTime <= _config.BlowDryDryGoodEndSeconds
                    ? ExecutionQuality.Perfect
                    : ExecutionQuality.Over;

            if (!causesIncident)
                return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.ExtraService,
                    MistakeSeverity.None, quality, PhysicalStateDelta.Empty, Array.Empty<MilestoneEvent>(),
                    CustomerMetricsDelta.Empty, 0, "Dry hair already, extra blow only.",
                    DiagnosticCode.None, request.ElapsedTime);

            MistakeSeverity severity = request.ElapsedTime <= _config.BlowDryDryMajorThresholdSeconds * 1.8f
                ? MistakeSeverity.Minor
                : MistakeSeverity.Major;
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.ExtraService,
                severity, quality,
                PhysicalStateDelta.Empty, Array.Empty<MilestoneEvent>(),
                new CustomerMetricsDelta(
                    -Math.Max(1f, 2f + (request.ElapsedTime - _config.BlowDryDryMajorThresholdSeconds)),
                    0f, CustomerMetricsSource.Incident,
                    "Overheat during dry-state blowdry."),
                1, "Cooling down is needed.", DiagnosticCode.None, request.ElapsedTime);
        }

        private ActionResult ResolveWrapTowel(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (request.Tool != ServiceTool.Towel) return ActionResult.Unhandled(request);
            TowelContamination contamination = physical.FoamAmount > _config.ExitFoamThreshold
                ? TowelContamination.Foam
                : physical.ShampooState != ShampooState.None ? TowelContamination.Shampoo : TowelContamination.None;
            bool clean = contamination == TowelContamination.None;
            MilestoneEvent[] events = clean
                ? new[] { new MilestoneEvent(MilestoneId.CleanTowelApplied, true, true) }
                : Array.Empty<MilestoneEvent>();
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed,
                clean ? OrderEffect.Progress : OrderEffect.None,
                clean ? MistakeSeverity.None : MistakeSeverity.Recoverable,
                ExecutionQuality.NotApplicable,
                new PhysicalStateDelta(isTowelWrapped: true, towelContamination: contamination,
                    towelCondition: TowelCondition.Intact),
                events,
                clean ? CustomerMetricsDelta.Empty : new CustomerMetricsDelta(-3f, 0f,
                    CustomerMetricsSource.Action, "Wrapped towel before hair was clean."),
                clean ? 0 : 1, clean ? null : "Remove towel and rinse hair.",
                DiagnosticCode.None, request.ElapsedTime);
        }

        private ActionResult ResolveRemoveTowel(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (request.Tool != ServiceTool.Towel) return ActionResult.Unhandled(request);
            if (!physical.IsTowelWrapped) return ActionResult.Invalid(request);
            return Executed(request, OrderEffect.Progress,
                new PhysicalStateDelta(isTowelWrapped: false),
                new[] { new MilestoneEvent(MilestoneId.Untoweled, true, true) });
        }

        private ActionResult ResolveHaircut(
            ActionRequest request,
            OrderDefinition order,
            CustomerPhysicalStateSnapshot physical,
            ServiceProgressSnapshot progress)
        {
            if (!TryHaircutTool(request.Tool, out HaircutTool selectedTool)
                || request.HaircutParameters == null || !request.HaircutParameters.IsValid())
                return ActionResult.Unhandled(request);

            HaircutActionParameters parameters = request.HaircutParameters;
            HaircutTool? requiredTool = CurrentRequiredHaircutTool(order, progress);
            if (!requiredTool.HasValue && request.ElapsedTime <= _config.ResistanceWindowSeconds)
            {
                return new ActionResult(request.ActionToken, ExecutionStatus.Resisted,
                    OrderEffect.None, MistakeSeverity.Minor, ExecutionQuality.NotApplicable,
                    PhysicalStateDelta.Empty, Array.Empty<MilestoneEvent>(),
                    CustomerMetricsDelta.Empty, 0,
                    "Customer resists this unrequested irreversible haircut.",
                    DiagnosticCode.None, request.ElapsedTime);
            }
            if (physical.IsTowelWrapped)
            {
                return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.None,
                    MistakeSeverity.Recoverable, ExecutionQuality.NotApplicable,
                    new PhysicalStateDelta(towelCondition: TowelCondition.Damaged),
                    Array.Empty<MilestoneEvent>(),
                    new CustomerMetricsDelta(-Math.Max(1f, parameters.WrongToolPenalty * .25f), 0f,
                        CustomerMetricsSource.Action, "Haircut tool damaged the wrapped towel."),
                    1, "Remove the damaged towel before cutting.", DiagnosticCode.None, request.ElapsedTime);
            }

            bool fulfillsOrder = requiredTool.HasValue;
            if (!fulfillsOrder)
                return ResolvePersistedUnrequestedHaircut(request, physical, selectedTool, parameters);
            bool correctTool = !requiredTool.HasValue || requiredTool.Value == selectedTool;
            float foamPenalty = physical.FoamAmount > _config.ExitFoamThreshold ? 5f : 0f;
            int foamIncident = foamPenalty > 0f ? 1 : 0;

            if (!correctTool)
            {
                float wrongProgress = Math.Min(1f - physical.HaircutProgress(selectedTool),
                    Math.Max(.2f, request.ElapsedTime / Math.Max(.01f, parameters.PerfectMax) * .5f));
                return new ActionResult(request.ActionToken, ExecutionStatus.Executed, OrderEffect.None,
                    MistakeSeverity.Recoverable, ExecutionQuality.NotApplicable,
                    new PhysicalStateDelta(hairLengthDeviationDelta: -.08f,
                        haircutTool: selectedTool, haircutProgressDelta: wrongProgress),
                    Array.Empty<MilestoneEvent>(),
                    new CustomerMetricsDelta(-(parameters.WrongToolPenalty + foamPenalty), 0f,
                        CustomerMetricsSource.Action, "Wrong haircut tool used."),
                    1 + foamIncident, "Use the required haircut tool to recover.",
                    DiagnosticCode.None, request.ElapsedTime);
            }

            ExecutionQuality quality = HoldQualityEvaluator.Evaluate(
                request.ElapsedTime, parameters.PerfectMin, parameters.PerfectMax);
            float currentProgress = physical.HaircutProgress(selectedTool);
            float progressDelta;
            float deviationDelta;
            MistakeSeverity mistake;
            OrderEffect effect;
            float satisfactionPenalty;
            int incidents;
            string continuation;

            if (quality == ExecutionQuality.Under)
            {
                float ratio = parameters.PerfectMin <= 0f ? 1f : request.ElapsedTime / parameters.PerfectMin;
                progressDelta = Math.Min(1f - currentProgress, Math.Max(.05f, ratio * .65f));
                deviationDelta = Math.Max(0f, 1f - currentProgress - progressDelta) * .12f;
                mistake = MistakeSeverity.Minor;
                effect = OrderEffect.None;
                satisfactionPenalty = parameters.UnderPenalty + foamPenalty;
                incidents = foamIncident;
                continuation = "Continue with the same tool.";
            }
            else if (quality == ExecutionQuality.Perfect)
            {
                progressDelta = 1f - currentProgress;
                deviationDelta = physical.HairLengthDeviation > 0f ? -physical.HairLengthDeviation : 0f;
                mistake = foamPenalty > 0f ? MistakeSeverity.Recoverable
                    : fulfillsOrder ? MistakeSeverity.None : MistakeSeverity.Minor;
                effect = fulfillsOrder ? OrderEffect.Progress : OrderEffect.ExtraService;
                satisfactionPenalty = foamPenalty + (fulfillsOrder
                    ? 0f : Math.Max(1f, parameters.WrongToolPenalty / 3f));
                incidents = foamIncident + (fulfillsOrder ? 0 : 1);
                continuation = foamPenalty > 0f ? "Rinse remaining foam after the haircut." : null;
            }
            else
            {
                progressDelta = 1f - currentProgress;
                float excess = request.ElapsedTime - parameters.PerfectMax;
                deviationDelta = -Math.Max(.25f, excess / Math.Max(.01f, parameters.PerfectMax) * .25f)
                    - Math.Max(0f, physical.HairLengthDeviation);
                mistake = MistakeSeverity.Major;
                effect = fulfillsOrder ? OrderEffect.Progress : OrderEffect.ExtraService;
                satisfactionPenalty = parameters.OverPenalty + foamPenalty;
                incidents = 1 + foamIncident;
                continuation = null;
            }

            MilestoneEvent[] events = quality == ExecutionQuality.Under || !fulfillsOrder
                ? Array.Empty<MilestoneEvent>()
                : new[] { new MilestoneEvent(HaircutMilestone(selectedTool), true, true) };
            CustomerMetricsDelta metrics = satisfactionPenalty <= 0f
                ? CustomerMetricsDelta.Empty
                : new CustomerMetricsDelta(-satisfactionPenalty, 0f, CustomerMetricsSource.Action,
                    quality == ExecutionQuality.Over ? "Hair was cut too short."
                    : foamPenalty > 0f ? "Haircut performed while foam remained."
                    : "Haircut needs correction.");
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, effect, mistake, quality,
                new PhysicalStateDelta(hairLengthDeviationDelta: deviationDelta,
                    haircutTool: selectedTool, haircutProgressDelta: progressDelta),
                events, metrics, incidents, continuation, DiagnosticCode.None, request.ElapsedTime);
        }

        private ActionResult ResolvePersistedUnrequestedHaircut(
            ActionRequest request,
            CustomerPhysicalStateSnapshot physical,
            HaircutTool selectedTool,
            HaircutActionParameters parameters)
        {
            float effectiveElapsed = Math.Max(0f, request.ElapsedTime - _config.ResistanceWindowSeconds);
            ExecutionQuality quality = HoldQualityEvaluator.Evaluate(
                effectiveElapsed, parameters.PerfectMin, parameters.PerfectMax);
            float current = physical.HaircutProgress(selectedTool);
            float ratio = parameters.PerfectMax <= 0f ? 1f : effectiveElapsed / parameters.PerfectMax;
            float progress = Math.Min(1f - current, Math.Max(.05f, ratio));
            float foamPenalty = physical.FoamAmount > _config.ExitFoamThreshold ? 5f : 0f;
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed,
                OrderEffect.ExtraService, MistakeSeverity.Major, quality,
                new PhysicalStateDelta(
                    hairLengthDeviationDelta: -.12f,
                    haircutTool: selectedTool,
                    haircutProgressDelta: progress),
                Array.Empty<MilestoneEvent>(),
                new CustomerMetricsDelta(-(Math.Max(parameters.WrongToolPenalty,
                    _config.UnrequestedServiceSatisfactionPenalty) + foamPenalty), 0f,
                    CustomerMetricsSource.Incident,
                    "Customer resistance was ignored for an unrequested haircut."),
                1, null, DiagnosticCode.None, request.ElapsedTime);
        }

        private static HaircutTool? CurrentRequiredHaircutTool(
            OrderDefinition order,
            ServiceProgressSnapshot progress)
        {
            foreach (HaircutTool tool in order.RequiredCutTools)
                if (!progress[HaircutMilestone(tool)].EverCompleted) return tool;
            return null;
        }

        private static MilestoneId HaircutMilestone(HaircutTool tool)
        {
            if (tool == HaircutTool.ThinningShears) return MilestoneId.ThinningShearsCompleted;
            return tool == HaircutTool.Clippers ? MilestoneId.ClippersCompleted : MilestoneId.ScissorsCompleted;
        }

        private static bool TryHaircutTool(ServiceTool tool, out HaircutTool haircutTool)
        {
            if (tool == ServiceTool.Scissors)
            {
                haircutTool = HaircutTool.Scissors;
                return true;
            }
            if (tool == ServiceTool.ThinningShears)
            {
                haircutTool = HaircutTool.ThinningShears;
                return true;
            }
            if (tool == ServiceTool.Clippers)
            {
                haircutTool = HaircutTool.Clippers;
                return true;
            }
            haircutTool = HaircutTool.Scissors;
            return false;
        }

        private ActionResult UnrequestedWash(
            ActionRequest request,
            PhysicalStateDelta delta,
            IReadOnlyList<MilestoneEvent> events,
            string reason)
        {
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed,
                OrderEffect.ExtraService, MistakeSeverity.Minor, ExecutionQuality.NotApplicable,
                delta, events,
                new CustomerMetricsDelta(-_config.UnrequestedServiceSatisfactionPenalty, 0f,
                    CustomerMetricsSource.Action, reason),
                1, null, DiagnosticCode.None, request.ElapsedTime);
        }

        private static ActionResult Executed(
            ActionRequest request,
            OrderEffect effect,
            PhysicalStateDelta delta,
            IReadOnlyList<MilestoneEvent> events,
            ExecutionQuality quality = ExecutionQuality.NotApplicable)
        {
            return new ActionResult(request.ActionToken, ExecutionStatus.Executed, effect,
                MistakeSeverity.None, quality, delta, events,
                CustomerMetricsDelta.Empty, 0, null, DiagnosticCode.None, request.ElapsedTime);
        }
    }
}
