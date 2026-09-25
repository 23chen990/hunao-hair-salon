using System;
using System.Collections.Generic;
using HairSalon.ServiceArchitecture;

namespace HairSalon
{
    /// <summary>阶段3洗头与阶段4A剪发正式入口到纯领域核心的唯一适配器。</summary>
    public sealed class Stage3WashServiceAdapter
    {
        private sealed class Session
        {
            public Session(CustomerServiceState state, InteractionContext interaction, InMemoryActionHistory history)
            {
                State = state;
                Interaction = interaction;
                History = history;
            }

            public CustomerServiceState State { get; }
            public InteractionContext Interaction { get; }
            public InMemoryActionHistory History { get; }
            public HaircutActionParameters ActiveHaircutParameters { get; set; }
            public ServiceExecutionController ExecutionController { get; set; }
            /// <summary>
            /// The mobile continuous service entry point represents the approved
            /// wash hand-off. It has no wrap/remove-towel interaction, so its
            /// order must not require those manual-only milestones.
            /// </summary>
            public bool UsesContinuousServiceExecution { get; set; }
            /// <summary>
            /// Background actions keep their domain token while the player focuses a
            /// different customer. Foreground actions are still invalidated on focus
            /// changes through the normal InteractionContext lifecycle.
            /// </summary>
            public bool PreserveActiveActionOnFocus { get; set; }
        }

        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly ServiceRuleConfig _config;
        private readonly ActionResolver _resolver;
        private readonly Dictionary<int, ActionResultApplier> _appliers =
            new Dictionary<int, ActionResultApplier>();

        public Stage3WashServiceAdapter(ServiceRuleConfig config = null)
        {
            _config = config ?? new ServiceRuleConfig();
            _resolver = new ActionResolver(_config);
        }

        /// <summary>Discard per-customer service sessions at a safe day boundary.</summary>
        public void ResetForNextDay()
        {
            _sessions.Clear();
            _appliers.Clear();
        }

        public void Register(CustomerModel customer)
        {
            Ensure(customer);
        }

        public ServiceExecution StartServiceExecution(
            CustomerModel customer, ServiceExecutionType type, float duration, float worldTime)
        {
            Session session = Ensure(customer);
            session.UsesContinuousServiceExecution = true;
            SynchronizeOrder(customer, session);
            session.State.Relocate(customer.Station);
            session.Interaction.Focus(customer.Id, customer.Station);
            if (session.ExecutionController == null)
                session.ExecutionController = new ServiceExecutionController(
                    _resolver, _appliers[customer.Id], session.State, session.Interaction);
            return session.ExecutionController.Start(type, duration, worldTime);
        }

        public ServiceExecutionCompletion CompleteServiceExecution(CustomerModel customer)
        {
            Session session = Ensure(customer);
            if (session.ExecutionController == null)
                throw new InvalidOperationException("No service execution has been started.");
            ServiceExecutionCompletion completion = session.ExecutionController.Complete();
            if (completion.ApplyResult.Status == ApplyStatus.Applied) Project(customer);
            return completion;
        }

        public bool TickServiceExecution(CustomerModel customer, float deltaTime)
        {
            Session session = Ensure(customer);
            ServiceExecution execution = session.ExecutionController == null
                ? null : session.ExecutionController.Active;
            if (execution == null || execution.State != ServiceExecutionState.Executing) return false;
            execution.Tick(deltaTime);
            if (execution.Progress >= 1f) CompleteServiceExecution(customer);
            return true;
        }

        public void FocusCustomer(CustomerModel customer)
        {
            if (customer == null) return;
            foreach (KeyValuePair<int, Session> pair in _sessions)
            {
                if (pair.Key == customer.Id) continue;
                InteractionContext context = pair.Value.Interaction;
                bool preserveBackgroundAction = pair.Value.PreserveActiveActionOnFocus &&
                    context.ActiveActionToken != null;
                if (context.FocusedCustomerId >= 0 && !preserveBackgroundAction)
                    context.ClearInteractionContext(ClearReason.CustomerChanged);
            }
            Session session = Ensure(customer);
            session.Interaction.Focus(customer.Id, customer.Station);
        }

        public void MovementStarted(CustomerModel customer, int stationId)
        {
            Session session = Ensure(customer);
            session.State.SynchronizeExternalMetrics(customer.Satisfaction, customer.Patience);
            session.Interaction.ClearInteractionContext(ClearReason.MovementStarted);
            session.State.BeginMovement(stationId);
            session.Interaction.Focus(customer.Id, stationId);
            Project(customer);
        }

        public void StationArrived(CustomerModel customer)
        {
            Session session = Ensure(customer);
            session.State.SynchronizeExternalMetrics(customer.Satisfaction, customer.Patience);
            session.State.CompleteMovement();
            session.Interaction.ClearInteractionContext(ClearReason.StationChanged);
            Project(customer);
        }

        public bool BeginTimedAction(
            CustomerModel customer,
            ServiceActionType action,
            ServiceTool tool,
            float worldTime)
        {
            return BeginTimedAction(customer, action, tool, worldTime, false);
        }

        public bool BeginBackgroundTimedAction(
            CustomerModel customer,
            ServiceActionType action,
            ServiceTool tool,
            float worldTime)
        {
            return BeginTimedAction(customer, action, tool, worldTime, true);
        }

        private bool BeginTimedAction(
            CustomerModel customer,
            ServiceActionType action,
            ServiceTool tool,
            float worldTime,
            bool preserveOnFocus)
        {
            Session session = Ensure(customer);
            session.State.Relocate(customer.Station);
            session.State.SynchronizeExternalMetrics(customer.Satisfaction, customer.Patience);
            session.Interaction.Focus(customer.Id, customer.Station);
            session.PreserveActiveActionOnFocus = preserveOnFocus;
            session.Interaction.BeginAction(action, tool,
                session.State.Physical.PhysicalStateRevision, worldTime);
            return true;
        }

        public bool BeginHaircutAction(
            CustomerModel customer,
            SalonTool tool,
            HaircutConfig config,
            float worldTime)
        {
            if (customer == null || config == null || !TryServiceTool(tool, out ServiceTool serviceTool))
                return false;
            Session session = Ensure(customer);
            SynchronizeOrder(customer, session);
            session.ActiveHaircutParameters = new HaircutActionParameters(
                config.GetPerfectMin(tool),
                config.GetPerfectMax(tool),
                Math.Max(0f, config.UndercutPenalty),
                Math.Max(0f, config.OvercutPenalty),
                Math.Max(0f, config.WrongToolPenalty));
            return BeginTimedAction(customer, ServiceActionType.Haircut, serviceTool, worldTime);
        }

        public HaircutActionApplyResult CompleteHaircutAction(
            CustomerModel customer,
            float elapsedTime,
            bool interrupted)
        {
            Session session = Ensure(customer);
            ActionToken token = session.Interaction.ActiveActionToken;
            if (token == null || token.ActionType != ServiceActionType.Haircut
                || session.ActiveHaircutParameters == null)
                return new HaircutActionApplyResult(
                    new ApplyActionResult(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken), null);
            CustomerPhysicalStateSnapshot prePhysical = session.State.Physical.CreateSnapshot();
            var request = new ActionRequest(token, customer.Id, customer.Station,
                token.ActionType, token.Tool, Math.Max(0f, elapsedTime), interrupted,
                session.ActiveHaircutParameters);
            ActionResult result = _resolver.Resolve(
                request,
                session.State.Order,
                session.State.Physical.CreateSnapshot(),
                session.State.Progress.CreateSnapshot(),
                session.Interaction.CreateSnapshot(),
                session.State.Metrics.CreateSnapshot());
            ApplyActionResult applied = _appliers[customer.Id].Apply(session.State, session.Interaction, result);
            session.ActiveHaircutParameters = null;
            if (applied.Status == ApplyStatus.Applied)
            {
                Project(customer);
                ProcessFoamBurstRecovery(customer, session, result, prePhysical);
            }
            return new HaircutActionApplyResult(applied, result);
        }

        public void ConfigureHaircutOrder(CustomerModel customer)
        {
            if (customer == null) return;
            SynchronizeOrder(customer, Ensure(customer));
        }

        public ApplyActionResult CompleteTimedAction(
            CustomerModel customer,
            float elapsedTime,
            bool interrupted)
        {
            Session session = Ensure(customer);
            ActionToken token = session.Interaction.ActiveActionToken;
            if (token == null)
            {
                session.PreserveActiveActionOnFocus = false;
                return new ApplyActionResult(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            }
            CustomerPhysicalStateSnapshot prePhysical = session.State.Physical.CreateSnapshot();
            var request = new ActionRequest(token, customer.Id, customer.Station,
                token.ActionType, token.Tool, Math.Max(0f, elapsedTime), interrupted);
            ActionResult result = _resolver.Resolve(
                request,
                session.State.Order,
                session.State.Physical.CreateSnapshot(),
                session.State.Progress.CreateSnapshot(),
                session.Interaction.CreateSnapshot(),
                session.State.Metrics.CreateSnapshot());
            ApplyActionResult applied = _appliers[customer.Id].Apply(session.State, session.Interaction, result);
            if (applied.Status == ApplyStatus.Applied)
            {
                Project(customer);
                ProcessFoamBurstRecovery(customer, session, result, prePhysical);
            }
            session.PreserveActiveActionOnFocus = false;
            return applied;
        }

        public ActionResult PreviewTimedActionResult(CustomerModel customer, float elapsedTime)
        {
            Session session = Ensure(customer);
            ActionToken token = session.Interaction.ActiveActionToken;
            if (token == null || token.ActionType == ServiceActionType.Haircut) return null;
            return PreviewResult(session, customer, token, Math.Max(0f, elapsedTime), false);
        }

        public ActionResult PreviewHaircutResult(
            CustomerModel customer,
            float elapsedTime,
            HaircutActionParameters parameters = null)
        {
            Session session = Ensure(customer);
            ActionToken token = session.Interaction.ActiveActionToken;
            if (token == null || token.ActionType != ServiceActionType.Haircut
                || session.ActiveHaircutParameters == null)
                return null;
            return PreviewResult(
                session,
                customer,
                token,
                Math.Max(0f, elapsedTime),
                true,
                parameters ?? session.ActiveHaircutParameters);
        }

        private ActionResult PreviewResult(
            Session session,
            CustomerModel customer,
            ActionToken token,
            float elapsedTime,
            bool isHaircut,
            HaircutActionParameters haircutParameters = null)
        {
            if (!isHaircut)
            {
                var request = new ActionRequest(token, customer.Id, customer.Station,
                    token.ActionType, token.Tool, elapsedTime, false);
                return _resolver.Resolve(
                    request,
                    session.State.Order,
                    session.State.Physical.CreateSnapshot(),
                    session.State.Progress.CreateSnapshot(),
                    session.Interaction.CreateSnapshot(),
                    session.State.Metrics.CreateSnapshot());
            }

            var haircutRequest = new ActionRequest(token, customer.Id, customer.Station,
                token.ActionType, token.Tool, elapsedTime, false, haircutParameters);
            return _resolver.Resolve(
                haircutRequest,
                session.State.Order,
                session.State.Physical.CreateSnapshot(),
                session.State.Progress.CreateSnapshot(),
                session.Interaction.CreateSnapshot(),
                session.State.Metrics.CreateSnapshot());
        }

        public void DiscardTimedAction(CustomerModel customer, ClearReason reason)
        {
            Session session = Ensure(customer);
            session.Interaction.ClearInteractionContext(reason);
            session.PreserveActiveActionOnFocus = false;
            Project(customer);
        }

        public ApplyActionResult ExecuteQuickAction(
            CustomerModel customer,
            ServiceActionType action,
            ServiceTool tool,
            float worldTime)
        {
            Session session = Ensure(customer);
            session.State.Relocate(customer.Station);
            session.State.SynchronizeExternalMetrics(customer.Satisfaction, customer.Patience);
            session.Interaction.Focus(customer.Id, customer.Station);
            session.Interaction.ExecuteQuickAction(action, tool);
            ActionToken token = session.Interaction.BeginAction(
                action, tool, session.State.Physical.PhysicalStateRevision, worldTime);
            CustomerPhysicalStateSnapshot prePhysical = session.State.Physical.CreateSnapshot();
            var request = new ActionRequest(token, customer.Id, customer.Station, action, tool, 0f, false);
            ActionResult result = _resolver.Resolve(
                request,
                session.State.Order,
                session.State.Physical.CreateSnapshot(),
                session.State.Progress.CreateSnapshot(),
                session.Interaction.CreateSnapshot(),
                session.State.Metrics.CreateSnapshot());
            ApplyActionResult applied = _appliers[customer.Id].Apply(session.State, session.Interaction, result);
            if (applied.Status == ApplyStatus.Applied)
            {
                Project(customer);
                ProcessFoamBurstRecovery(customer, session, result, prePhysical);
            }
            return applied;
        }

        public CustomerPhysicalStateSnapshot Physical(CustomerModel customer) =>
            Ensure(customer).State.Physical.CreateSnapshot();

        public ServiceProgressSnapshot Progress(CustomerModel customer) =>
            Ensure(customer).State.Progress.CreateSnapshot();

        public OrderDefinition Order(CustomerModel customer) => Ensure(customer).State.Order;

        public InteractionContextSnapshot Interaction(CustomerModel customer) =>
            Ensure(customer).Interaction.CreateSnapshot();

        public IReadOnlyList<ActionHistoryEntry> History(CustomerModel customer) =>
            Ensure(customer).History.Entries;

        public ServiceExitReadinessResult ExitReadiness(CustomerModel customer)
        {
            Session session = Ensure(customer);
            return EvaluateExitReadiness(session);
        }

        private void ProcessFoamBurstRecovery(
            CustomerModel customer,
            Session session,
            ActionResult result,
            CustomerPhysicalStateSnapshot prePhysical)
        {
            if (customer == null || session == null || result == null) return;

            if (session.State.Physical == null || session.State.Physical.PhysicalStateRevision < 0) return;

            if (HasUnresolvedFoamBurst(customer) && session.State.Physical.FoamAmount <= _config.ExitFoamThreshold)
            {
                ResolveRecoveryIfPossible(customer);
            }

            if (result.ActionToken == null || result.ActionToken.ActionType != ServiceActionType.BlowDry)
                return;

            bool hadFoamBefore = prePhysical != null && prePhysical.FoamAmount > _config.ExitFoamThreshold;
            if (!hadFoamBefore || HasUnresolvedFoamBurst(customer)) return;

            var eventId = Guid.NewGuid().ToString("N");
            customer.ActiveDisasterEvents.Add(new FunnyDisasterEvent(
                eventId,
                FunnyDisasterType.FoamBurst,
                customer.Id,
                ServiceActionType.BlowDry,
                MistakeSeverity.Minor,
                false));
            customer.RecoveryRequirements.Add(new RecoveryRequirement(
                Guid.NewGuid().ToString("N"),
                customer.Id,
                RecoveryType.ReWash,
                new[] { ServiceActionType.Shower },
                "Wash",
                false));
            if (customer.ReactionKind == CustomerReactionKind.None)
                customer.ReactionKind = CustomerReactionKind.Protest;
            customer.ReactionRemaining = Math.Max(2.2f, customer.ReactionRemaining);
            if (customer.Emotion == CustomerEmotion.Calm)
                customer.Emotion = CustomerEmotion.Impatient;
        }

        private void ResolveRecoveryIfPossible(CustomerModel customer)
        {
            bool stillBlocked = false;
            for (int i = 0; i < customer.RecoveryRequirements.Count; i++)
            {
                RecoveryRequirement req = customer.RecoveryRequirements[i];
                if (req == null || req.RecoveryType != RecoveryType.ReWash)
                    continue;
                if (req.IsCompleted)
                    continue;

                req.IsCompleted = customer.ServicePhysicalState != null
                                  && customer.ServicePhysicalState.FoamAmount <= _config.ExitFoamThreshold;
                if (!req.IsCompleted) stillBlocked = true;
            }

            if (stillBlocked) return;

            for (int i = 0; i < customer.ActiveDisasterEvents.Count; i++)
            {
                FunnyDisasterEvent evt = customer.ActiveDisasterEvents[i];
                if (evt != null && evt.EventType == FunnyDisasterType.FoamBurst && !evt.IsResolved)
                    evt.Resolve();
            }

            if (customer.ReactionKind == CustomerReactionKind.Protest)
            {
                customer.ReactionKind = CustomerReactionKind.None;
                customer.ReactionRemaining = 0f;
            }
        }

        private bool HasUnresolvedFoamBurst(CustomerModel customer)
        {
            for (int i = 0; i < customer.ActiveDisasterEvents.Count; i++)
            {
                FunnyDisasterEvent evt = customer.ActiveDisasterEvents[i];
                if (evt != null && evt.EventType == FunnyDisasterType.FoamBurst && !evt.IsResolved)
                    return true;
            }
            return false;
        }

        public bool IsOutstandingRequiredCutMilestone(CustomerModel customer, SalonTool tool)
        {
            if (customer == null) return false;
            if (!TryHaircutTool(tool, out HaircutTool haircutTool)) return false;
            Session session = Ensure(customer);
            MilestoneId milestone = HaircutMilestone(haircutTool);
            if (!session.State.Order.IsRequired(milestone, MilestoneCatalog.KindOf(milestone)))
                return false;
            ServiceProgressSnapshot progress = session.State.Progress.CreateSnapshot();
            return !progress[milestone].EverCompleted;
        }

        public void CompleteExternalServiceMilestone(CustomerModel customer, MilestoneId milestone)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (milestone != MilestoneId.DyeCompleted && milestone != MilestoneId.PermCompleted)
                throw new ArgumentOutOfRangeException(nameof(milestone));
            Session session = Ensure(customer);
            session.State.Progress.Set(new ServiceMilestoneState(
                milestone, MilestoneCatalog.KindOf(milestone), true, true));
            Project(customer);
        }

        public bool IsWashReadyForTransition(CustomerModel customer)
        {
            ServiceProgressSnapshot progress = Progress(customer);
            CustomerPhysicalStateSnapshot physical = Physical(customer);
            return progress[MilestoneId.WetHairApplied].EverCompleted
                && progress[MilestoneId.Shampooed].EverCompleted
                && progress[MilestoneId.RinseClean].SatisfiedNow
                && physical.FoamAmount <= _config.ExitFoamThreshold
                && physical.ShampooState == ShampooState.None;
        }

        public void Project(CustomerModel customer)
        {
            if (customer == null) return;
            Session session = Ensure(customer);
            CustomerPhysicalStateSnapshot physical = session.State.Physical.CreateSnapshot();
            ServiceProgressSnapshot progress = session.State.Progress.CreateSnapshot();
            ServiceExitReadinessResult readiness = EvaluateExitReadiness(session);
            session.State.CommitExitReadiness(readiness);
            customer.ServicePhysicalState = physical;
            customer.HairWet = physical.Wetness > _config.HairDryWetnessThreshold;
            customer.ShampooApplied = physical.ShampooState != ShampooState.None
                || physical.FoamAmount > _config.ExitFoamThreshold;
            customer.TowelWrapped = physical.IsTowelWrapped;
            customer.Satisfaction = session.State.Metrics.Satisfaction;
            customer.Patience = session.State.Metrics.Patience;
            customer.OrderRequirementsCompleted = readiness.OrderRequirementsCompleted;
            customer.ExitReady = readiness.PhysicalExitReady;
            customer.ExitBlockReason = readiness.PrimaryBlockReason;

            if (customer.ActiveServiceAction == ActiveServiceAction.Shower)
                customer.WashStage = physical.FoamAmount > _config.ExitFoamThreshold
                    && physical.ShampooState == ShampooState.Normal
                    ? WashStage.Rinsing : WashStage.Wetting;
            else if (customer.ActiveServiceAction == ActiveServiceAction.Shampoo)
                customer.WashStage = WashStage.Shampooing;
            else if (physical.IsTowelWrapped)
                customer.WashStage = WashStage.Toweled;
            else if (physical.ShampooState == ShampooState.ClumpedOnDryHair)
                customer.WashStage = WashStage.ShampooApplied;
            else if (physical.FoamAmount > _config.ExitFoamThreshold)
                customer.WashStage = WashStage.Foamy;
            else if (progress[MilestoneId.CleanTowelApplied].EverCompleted)
                customer.WashStage = WashStage.Toweled;
            else if (progress[MilestoneId.RinseClean].EverCompleted
                     && progress[MilestoneId.RinseClean].SatisfiedNow)
                customer.WashStage = WashStage.Rinsed;
            else if (physical.Wetness >= _config.WetHairThreshold)
                customer.WashStage = WashStage.Wet;
            else
                customer.WashStage = WashStage.Dry;
        }

        private ServiceExitReadinessResult EvaluateExitReadiness(Session session)
        {
            return ServiceExitReadiness.Evaluate(
                session.State.Order,
                session.State.Progress.CreateSnapshot(),
                session.State.Physical.CreateSnapshot(),
                session.State.IsMoving,
                session.Interaction.ActiveActionToken != null,
                _config);
        }

        private Session Ensure(CustomerModel customer)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (_sessions.TryGetValue(customer.Id, out Session existing)) return existing;

            float wetness = customer.HairWet ? 1f : 0f;
            float foam = customer.ShampooApplied ? 1f : 0f;
            ShampooState shampoo = customer.ShampooApplied ? ShampooState.Normal : ShampooState.None;
            var physical = new CustomerPhysicalState(
                wetness, foam, shampoo, customer.TowelWrapped,
                customer.ShampooApplied ? TowelContamination.Foam : TowelContamination.None,
                TowelCondition.Intact, 0, 0f);
            OrderDefinition order = CreateOrder(customer);
            var progress = new ServiceProgress();
            var metrics = new CustomerMetricsState(customer.Satisfaction, customer.Patience);
            var state = new CustomerServiceState(customer.Id, customer.Station, physical, order, progress, metrics);
            var interaction = new InteractionContext();
            interaction.Focus(customer.Id, customer.Station);
            var history = new InMemoryActionHistory();
            var session = new Session(state, interaction, history);
            _sessions.Add(customer.Id, session);
            _appliers.Add(customer.Id, new ActionResultApplier(_config, history));
            Project(customer);
            return session;
        }

        private static void SynchronizeOrder(CustomerModel customer, Session session)
        {
            session.State.ReplaceOrder(CreateOrder(customer, !session.UsesContinuousServiceExecution));
        }

        private static OrderDefinition CreateOrder(
            CustomerModel customer, bool includeManualTowelTransitionRequirements = true)
        {
            var services = new List<RequiredService>();
            var milestones = new List<ServiceMilestoneDefinition>();
            var cutTools = new List<HaircutTool>();
            bool hasWash = customer.Needs.Contains(ServiceType.Wash);
            bool hasLaterService = hasWash && customer.Needs.Count > 1;
            if (hasWash)
            {
                services.Add(RequiredService.Wash);
                AddRequired(milestones, MilestoneId.WetHairApplied);
                AddRequired(milestones, MilestoneId.Shampooed);
                AddRequired(milestones, MilestoneId.RinseClean);
                if (hasLaterService && includeManualTowelTransitionRequirements)
                {
                    AddRequired(milestones, MilestoneId.CleanTowelApplied);
                    AddRequired(milestones, MilestoneId.Untoweled);
                }
            }
            if (customer.Needs.Contains(ServiceType.Cut))
            {
                services.Add(RequiredService.Cut);
                if (customer.HaircutService != null)
                {
                    foreach (SalonTool tool in customer.HaircutService.RequiredTools)
                        if (TryHaircutTool(tool, out HaircutTool haircutTool)) cutTools.Add(haircutTool);
                }
                if (cutTools.Count == 0) cutTools.Add(HaircutTool.Scissors);
                foreach (HaircutTool tool in cutTools) AddRequired(milestones, HaircutMilestone(tool));
            }
            if (customer.Needs.Contains(ServiceType.Dry))
            {
                services.Add(RequiredService.Dry);
                AddRequired(milestones, MilestoneId.HairDry);
            }
            if (customer.Needs.Contains(ServiceType.Dye))
            {
                services.Add(RequiredService.Dye);
                AddRequired(milestones, MilestoneId.DyeCompleted);
            }
            if (customer.Needs.Contains(ServiceType.Perm))
            {
                services.Add(RequiredService.Perm);
                AddRequired(milestones, MilestoneId.PermCompleted);
            }
            return new OrderDefinition(services, milestones, cutTools, hasLaterService);
        }

        private static void AddRequired(List<ServiceMilestoneDefinition> milestones, MilestoneId id)
        {
            milestones.Add(new ServiceMilestoneDefinition(id, MilestoneCatalog.KindOf(id), true));
        }

        private static MilestoneId HaircutMilestone(HaircutTool tool)
        {
            if (tool == HaircutTool.ThinningShears) return MilestoneId.ThinningShearsCompleted;
            return tool == HaircutTool.Clippers ? MilestoneId.ClippersCompleted : MilestoneId.ScissorsCompleted;
        }

        private static bool TryServiceTool(SalonTool tool, out ServiceTool serviceTool)
        {
            if (tool == SalonTool.Scissors)
            {
                serviceTool = ServiceTool.Scissors;
                return true;
            }
            if (tool == SalonTool.ThinningShears)
            {
                serviceTool = ServiceTool.ThinningShears;
                return true;
            }
            if (tool == SalonTool.Clippers)
            {
                serviceTool = ServiceTool.Clippers;
                return true;
            }
            serviceTool = ServiceTool.None;
            return false;
        }

        private static bool TryHaircutTool(SalonTool tool, out HaircutTool haircutTool)
        {
            if (tool == SalonTool.Scissors)
            {
                haircutTool = HaircutTool.Scissors;
                return true;
            }
            if (tool == SalonTool.ThinningShears)
            {
                haircutTool = HaircutTool.ThinningShears;
                return true;
            }
            if (tool == SalonTool.Clippers)
            {
                haircutTool = HaircutTool.Clippers;
                return true;
            }
            haircutTool = HaircutTool.Scissors;
            return false;
        }
    }

    public sealed class HaircutActionApplyResult
    {
        public HaircutActionApplyResult(ApplyActionResult applyResult, ActionResult actionResult)
        {
            ApplyResult = applyResult;
            ActionResult = actionResult;
        }

        public ApplyActionResult ApplyResult { get; }
        public ActionResult ActionResult { get; }
    }
}
