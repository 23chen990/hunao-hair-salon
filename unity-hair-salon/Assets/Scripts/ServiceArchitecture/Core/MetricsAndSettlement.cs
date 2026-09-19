using System;
using System.Collections.Generic;
using System.Linq;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>满意度与耐心的不可变差量，非空差量必须带来源和原因。</summary>
    public sealed class CustomerMetricsDelta : IEquatable<CustomerMetricsDelta>
    {
        public static readonly CustomerMetricsDelta Empty = new CustomerMetricsDelta();

        private CustomerMetricsDelta()
        {
            Source = CustomerMetricsSource.None;
            Reason = string.Empty;
        }

        public CustomerMetricsDelta(
            float satisfactionDelta,
            float patienceDelta,
            CustomerMetricsSource source,
            string reason)
        {
            if (!Numeric.IsFinite(satisfactionDelta) || !Numeric.IsFinite(patienceDelta))
                throw new ArgumentOutOfRangeException(nameof(satisfactionDelta));
            if (source == CustomerMetricsSource.None || string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A non-empty metrics delta requires source and reason.");
            SatisfactionDelta = satisfactionDelta;
            PatienceDelta = patienceDelta;
            Source = source;
            Reason = reason;
        }

        public float SatisfactionDelta { get; }
        public float PatienceDelta { get; }
        public CustomerMetricsSource Source { get; }
        public string Reason { get; }
        public bool IsEmpty => SatisfactionDelta == 0f && PatienceDelta == 0f;

        public bool Equals(CustomerMetricsDelta other)
        {
            return !ReferenceEquals(other, null) && SatisfactionDelta == other.SatisfactionDelta
                && PatienceDelta == other.PatienceDelta && Source == other.Source && Reason == other.Reason;
        }

        public override bool Equals(object obj) => Equals(obj as CustomerMetricsDelta);
        public override int GetHashCode() => SatisfactionDelta.GetHashCode() ^ PatienceDelta.GetHashCode();
    }

    /// <summary>阶段2测试用的纯顾客指标状态。</summary>
    public sealed class CustomerMetricsState
    {
        public CustomerMetricsState(float satisfaction, float patience)
        {
            Satisfaction = Numeric.Clamp100(satisfaction);
            Patience = Numeric.Clamp100(patience);
        }

        public float Satisfaction { get; private set; }
        public float Patience { get; private set; }
        public CustomerMetricsSnapshot CreateSnapshot() => new CustomerMetricsSnapshot(Satisfaction, Patience);

        internal void Commit(float satisfaction, float patience)
        {
            Satisfaction = Numeric.Clamp100(satisfaction);
            Patience = Numeric.Clamp100(patience);
        }
    }

    /// <summary>Resolver读取的不可变顾客指标快照。</summary>
    public sealed class CustomerMetricsSnapshot : IEquatable<CustomerMetricsSnapshot>
    {
        public CustomerMetricsSnapshot(float satisfaction, float patience)
        {
            Satisfaction = satisfaction;
            Patience = patience;
        }

        public float Satisfaction { get; }
        public float Patience { get; }

        public bool Equals(CustomerMetricsSnapshot other)
        {
            return !ReferenceEquals(other, null) && Satisfaction == other.Satisfaction && Patience == other.Patience;
        }

        public override bool Equals(object obj) => Equals(obj as CustomerMetricsSnapshot);
        public override int GetHashCode() => Satisfaction.GetHashCode() ^ Patience.GetHashCode();
    }

    /// <summary>满意度与耐心变化的唯一领域入口。</summary>
    public static class CustomerMetricsDomain
    {
        public static void ApplyDelta(CustomerMetricsState state, CustomerMetricsDelta delta)
        {
            if (state == null || delta == null) throw new ArgumentNullException(nameof(state));
            state.Commit(state.Satisfaction + delta.SatisfactionDelta, state.Patience + delta.PatienceDelta);
        }

        internal static CustomerMetricsSnapshot Calculate(CustomerMetricsSnapshot state, CustomerMetricsDelta delta)
        {
            return new CustomerMetricsSnapshot(
                Numeric.Clamp100(state.Satisfaction + delta.SatisfactionDelta),
                Numeric.Clamp100(state.Patience + delta.PatienceDelta));
        }
    }

    /// <summary>离店准备判断结果及明确阻挡项。</summary>
    public sealed class ServiceExitReadinessResult
    {
        public ServiceExitReadinessResult(IEnumerable<ExitConstraint> blockingConstraints)
        {
            BlockingConstraints = blockingConstraints.ToArray();
        }

        public bool CanSettle => BlockingConstraints.Count == 0;
        public bool PhysicalExitReady => BlockingConstraints.All(item =>
            item == ExitConstraint.RequiredHistoricalEventsCompleted
            || item == ExitConstraint.RequiredRevalidatableStatesSatisfied);
        public IReadOnlyList<ExitConstraint> BlockingConstraints { get; }
        public bool OrderRequirementsCompleted =>
            !BlockingConstraints.Contains(ExitConstraint.RequiredHistoricalEventsCompleted)
            && !BlockingConstraints.Contains(ExitConstraint.RequiredRevalidatableStatesSatisfied);
        public ExitBlockReason PrimaryBlockReason => ExitBlockReasonSelector.Select(BlockingConstraints);
    }

    /// <summary>将既有ExitConstraint按可理解的收尾优先级映射为单一UI原因。</summary>
    public static class ExitBlockReasonSelector
    {
        public static ExitBlockReason Select(IEnumerable<ExitConstraint> constraints)
        {
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            var values = constraints as ICollection<ExitConstraint> ?? constraints.ToArray();
            if (values.Contains(ExitConstraint.NoFoam)) return ExitBlockReason.FoamRemaining;
            if (values.Contains(ExitConstraint.NoClumpedShampoo)
                || values.Contains(ExitConstraint.NoShampooResidue))
                return ExitBlockReason.ShampooResidue;
            if (values.Contains(ExitConstraint.NoTowel)) return ExitBlockReason.TowelWrapped;
            if (values.Contains(ExitConstraint.DryEnough)) return ExitBlockReason.WetHair;
            if (values.Contains(ExitConstraint.NotMoving)) return ExitBlockReason.Moving;
            if (values.Contains(ExitConstraint.NoActiveAction)) return ExitBlockReason.ActiveAction;
            if (values.Contains(ExitConstraint.RequiredHistoricalEventsCompleted)
                || values.Contains(ExitConstraint.RequiredRevalidatableStatesSatisfied))
                return ExitBlockReason.RequirementsIncomplete;
            return ExitBlockReason.None;
        }
    }

    /// <summary>独立于事故严重度的统一离店准备判断。</summary>
    public static class ServiceExitReadiness
    {
        public static ServiceExitReadinessResult Evaluate(
            OrderDefinition order,
            ServiceProgressSnapshot progress,
            CustomerPhysicalStateSnapshot physical,
            bool isMoving,
            bool hasActiveAction,
            ServiceRuleConfig config)
        {
            var blockers = new List<ExitConstraint>();
            if (order.RequiredMilestones.Any(definition =>
                definition.Kind == MilestoneKind.HistoricalEvent && !progress[definition.Id].EverCompleted))
                blockers.Add(ExitConstraint.RequiredHistoricalEventsCompleted);
            if (order.RequiredMilestones.Any(definition =>
                definition.Kind == MilestoneKind.RevalidatableState &&
                (!progress[definition.Id].EverCompleted || !progress[definition.Id].SatisfiedNow)))
                blockers.Add(ExitConstraint.RequiredRevalidatableStatesSatisfied);
            if (physical.FoamAmount > config.ExitFoamThreshold) blockers.Add(ExitConstraint.NoFoam);
            if (physical.ShampooState == ShampooState.ClumpedOnDryHair) blockers.Add(ExitConstraint.NoClumpedShampoo);
            if (physical.ShampooState != ShampooState.None) blockers.Add(ExitConstraint.NoShampooResidue);
            if (physical.IsTowelWrapped) blockers.Add(ExitConstraint.NoTowel);
            // Wet hair is only an exit constraint when the order explicitly asks for Dry.
            // Wash-only and Wash+Cut customers can leave with a wet head once all
            // displayed milestones and the remaining physical cleanup constraints are met.
            bool requiresDry = order.RequiredServices.Contains(RequiredService.Dry);
            if (requiresDry && physical.Wetness > config.ExitWetnessThreshold)
                blockers.Add(ExitConstraint.DryEnough);
            if (isMoving) blockers.Add(ExitConstraint.NotMoving);
            if (hasActiveAction) blockers.Add(ExitConstraint.NoActiveAction);
            return new ServiceExitReadinessResult(blockers);
        }
    }

    /// <summary>重大事故完成后的纯结算结果，不把事故等同于未完成。</summary>
    public sealed class ServiceOutcome
    {
        public ServiceOutcome(
            ServiceCompletionState completionState,
            ServiceCustomerOutcome customerOutcome,
            bool paymentWaived,
            ServiceLeaveReason leaveReason)
        {
            CompletionState = completionState;
            CustomerOutcome = customerOutcome;
            PaymentWaived = paymentWaived;
            LeaveReason = leaveReason;
        }

        public ServiceCompletionState CompletionState { get; }
        public ServiceCustomerOutcome CustomerOutcome { get; }
        public bool PaymentWaived { get; }
        public ServiceLeaveReason LeaveReason { get; }
    }

    /// <summary>阶段2纯结算口径。</summary>
    public static class ServiceOutcomeEvaluator
    {
        public static ServiceOutcome Evaluate(bool serviceCompleted, bool hasMajorIncident, bool paymentAlreadyWaived)
        {
            return new ServiceOutcome(
                serviceCompleted ? ServiceCompletionState.Completed : ServiceCompletionState.InProgress,
                hasMajorIncident ? ServiceCustomerOutcome.Unhappy : ServiceCustomerOutcome.Normal,
                paymentAlreadyWaived || hasMajorIncident,
                serviceCompleted ? ServiceLeaveReason.ServiceFinished : ServiceLeaveReason.None);
        }
    }
}
