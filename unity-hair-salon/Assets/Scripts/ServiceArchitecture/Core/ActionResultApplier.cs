using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>阶段2独立服务聚合状态，不适配CustomerModel。</summary>
    public sealed class CustomerServiceState
    {
        private readonly HashSet<string> _appliedActionIds = new HashSet<string>();

        public CustomerServiceState(
            int customerId,
            int stationId,
            CustomerPhysicalState physical,
            OrderDefinition order,
            ServiceProgress progress,
            CustomerMetricsState metrics)
        {
            CustomerId = customerId;
            StationId = stationId;
            Physical = physical ?? throw new ArgumentNullException(nameof(physical));
            Order = order ?? throw new ArgumentNullException(nameof(order));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public int CustomerId { get; }
        public int StationId { get; private set; }
        public CustomerPhysicalState Physical { get; }
        public OrderDefinition Order { get; private set; }
        public ServiceProgress Progress { get; }
        public CustomerMetricsState Metrics { get; }
        public int IncidentCount { get; private set; }
        public bool IsMoving { get; private set; }
        public bool HasLeft { get; private set; }
        public bool SettlementCompleted { get; private set; }
        public float WorldTime { get; private set; }
        public ServiceExitReadinessResult ExitReadiness { get; private set; }

        internal bool WasApplied(string actionId) => _appliedActionIds.Contains(actionId);
        internal void MarkApplied(string actionId) => _appliedActionIds.Add(actionId);
        internal void CommitIncidentCount(int next) => IncidentCount = next;
        internal void CommitExitReadiness(ServiceExitReadinessResult readiness) => ExitReadiness = readiness;

        /// <summary>在正式动作开始前同步订单定义，不触碰物理状态或进度。</summary>
        public void ReplaceOrder(OrderDefinition order)
        {
            Order = order ?? throw new ArgumentNullException(nameof(order));
        }

        /// <summary>更新动作验证使用的工位；物理状态本身不保存工位。</summary>
        public void Relocate(int stationId)
        {
            StationId = stationId;
        }

        /// <summary>开始移动并更新动作验证工位，不触碰物理Revision。</summary>
        public void BeginMovement(int stationId)
        {
            StationId = stationId;
            IsMoving = true;
        }

        /// <summary>标记已到站，不触碰物理Revision。</summary>
        public void CompleteMovement()
        {
            IsMoving = false;
        }

        /// <summary>接入期从仍属CustomerModel权威的非动作指标同步当前值。</summary>
        public void SynchronizeExternalMetrics(float satisfaction, float patience)
        {
            Metrics.Commit(satisfaction, patience);
        }
    }

    /// <summary>一次Apply调用的明确结果。</summary>
    public sealed class ApplyActionResult
    {
        public ApplyActionResult(
            ApplyStatus status,
            DiagnosticCode diagnosticCode,
            ActionResult actionResult = null)
        {
            Status = status;
            DiagnosticCode = diagnosticCode;
            ActionResult = actionResult;
        }

        public ApplyStatus Status { get; }
        public DiagnosticCode DiagnosticCode { get; }
        public ActionResult ActionResult { get; }
    }

    /// <summary>领域状态提交前的集中不变量检查。</summary>
    public static class ServiceInvariants
    {
        public static bool AreValid(
            CustomerPhysicalStateSnapshot physical,
            CustomerMetricsSnapshot metrics,
            int incidentCount)
        {
            return physical != null && metrics != null && incidentCount >= 0
                && Numeric.IsFinite(physical.Wetness) && physical.Wetness >= 0f && physical.Wetness <= 1f
                && Numeric.IsFinite(physical.FoamAmount) && physical.FoamAmount >= 0f && physical.FoamAmount <= 1f
                && !(physical.FoamAmount == 0f && physical.ShampooState == ShampooState.Normal)
                && Numeric.IsFinite(metrics.Satisfaction) && metrics.Satisfaction >= 0f && metrics.Satisfaction <= 100f
                && Numeric.IsFinite(metrics.Patience) && metrics.Patience >= 0f && metrics.Patience <= 100f;
        }
    }

    /// <summary>先完整验证和计算NextState，再一次性提交的原子提交器。</summary>
    public sealed class ActionResultApplier
    {
        private readonly ServiceRuleConfig _config;
        private readonly IActionHistorySink _history;
        private readonly Action<string> _errorLogger;

        public ActionResultApplier(
            ServiceRuleConfig config,
            IActionHistorySink history,
            Action<string> errorLogger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _errorLogger = errorLogger ?? Debug.LogError;
        }

        public ApplyActionResult Apply(
            CustomerServiceState state,
            InteractionContext interaction,
            ActionResult result)
        {
            if (state == null || interaction == null || result == null)
                throw new ArgumentNullException(nameof(state));

            ActionToken token = result.ActionToken;
            if (state.WasApplied(result.ActionId))
                return Rejected(ApplyStatus.AlreadyApplied, DiagnosticCode.AlreadyApplied);
            if (token.CustomerId != state.CustomerId || token.StationId != state.StationId)
                return Rejected(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            if (interaction.FocusedCustomerId >= 0 && interaction.FocusedCustomerId != token.CustomerId)
                return Rejected(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            if (token.ContextVersion != interaction.ContextVersion)
                return Rejected(ApplyStatus.StaleContext, DiagnosticCode.StaleContext);
            if (interaction.FocusedCustomerId != token.CustomerId
                || interaction.FocusedStationId != token.StationId)
                return Rejected(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            if (result.ExpectedPhysicalRevision != state.Physical.PhysicalStateRevision)
                return Rejected(ApplyStatus.StalePhysicalState, DiagnosticCode.StalePhysicalState);
            if (!interaction.IsTokenCurrent(token))
                return Rejected(ApplyStatus.InvalidToken, DiagnosticCode.InvalidToken);
            if (state.HasLeft || state.SettlementCompleted || !result.IsInternallyValid())
                return Rejected(ApplyStatus.InvalidResult, DiagnosticCode.InvalidResult);
            if (result.ExecutionStatus == ExecutionStatus.Resisted)
                return ApplyResisted(state, interaction, result);
            if (result.ExecutionStatus != ExecutionStatus.Executed)
                return Rejected(ApplyStatus.InvalidResult, DiagnosticCode.InvalidResult);

            CustomerPhysicalStateSnapshot prePhysical = state.Physical.CreateSnapshot();
            CustomerMetricsSnapshot preMetrics = state.Metrics.CreateSnapshot();
            CustomerPhysicalState nextPhysical;
            CustomerMetricsSnapshot nextMetrics;
            ServiceProgress nextProgress;
            ServiceExitReadinessResult nextReadiness;
            int nextIncidents;
            try
            {
                nextPhysical = result.PhysicalStateDelta.ApplyTo(state.Physical);
                nextMetrics = CustomerMetricsDomain.Calculate(preMetrics, result.MetricsDelta);
                nextIncidents = checked(state.IncidentCount + result.IncidentDelta);
                nextProgress = ServiceProgressCalculator.Recalculate(
                    state.Progress.CreateSnapshot(), nextPhysical.CreateSnapshot(), result.MilestoneEvents, _config);
                nextReadiness = ServiceExitReadiness.Evaluate(
                    state.Order, nextProgress.CreateSnapshot(), nextPhysical.CreateSnapshot(),
                    state.IsMoving, false, _config);
            }
            catch (Exception)
            {
                return Rejected(ApplyStatus.InvalidResult, DiagnosticCode.InvalidResult);
            }

            if (!ServiceInvariants.AreValid(nextPhysical.CreateSnapshot(), nextMetrics, nextIncidents))
                return Rejected(ApplyStatus.InvariantViolation, DiagnosticCode.InvariantViolation);

            state.Physical.CommitFrom(nextPhysical);
            state.Metrics.Commit(nextMetrics.Satisfaction, nextMetrics.Patience);
            state.Progress.ReplaceWith(nextProgress);
            state.CommitIncidentCount(nextIncidents);
            state.CommitExitReadiness(nextReadiness);
            state.MarkApplied(result.ActionId);
            interaction.CompleteAction(token);

            var entry = new ActionHistoryEntry(result, prePhysical, state.Physical.CreateSnapshot(),
                preMetrics, state.Metrics.CreateSnapshot());
            try
            {
                _history.Append(entry);
            }
            catch (Exception exception)
            {
                _errorLogger($"ActionHistory append failed after business commit: {exception}");
            }
            return new ApplyActionResult(ApplyStatus.Applied, DiagnosticCode.None, result);
        }

        private ApplyActionResult ApplyResisted(
            CustomerServiceState state,
            InteractionContext interaction,
            ActionResult result)
        {
            CustomerPhysicalStateSnapshot physical = state.Physical.CreateSnapshot();
            CustomerMetricsSnapshot metrics = state.Metrics.CreateSnapshot();
            ServiceExitReadinessResult readiness = ServiceExitReadiness.Evaluate(
                state.Order, state.Progress.CreateSnapshot(), physical, state.IsMoving, false, _config);
            state.CommitExitReadiness(readiness);
            state.MarkApplied(result.ActionId);
            interaction.CompleteAction(result.ActionToken);
            try
            {
                _history.Append(new ActionHistoryEntry(result, physical, physical, metrics, metrics));
            }
            catch (Exception exception)
            {
                _errorLogger($"ActionHistory append failed after resisted action: {exception}");
            }
            return new ApplyActionResult(ApplyStatus.Applied, DiagnosticCode.None, result);
        }

        private static ApplyActionResult Rejected(ApplyStatus status, DiagnosticCode code)
        {
            return new ApplyActionResult(status, code);
        }
    }
}
