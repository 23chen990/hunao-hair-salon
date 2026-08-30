using System;
using System.Collections.Generic;
using System.Linq;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>由运行时剪发配置复制出的纯值参数，Resolver不读取MonoBehaviour或旧模型。</summary>
    public sealed class HaircutActionParameters
    {
        public HaircutActionParameters(
            float perfectMin,
            float perfectMax,
            float underPenalty,
            float overPenalty,
            float wrongToolPenalty)
        {
            PerfectMin = perfectMin;
            PerfectMax = perfectMax;
            UnderPenalty = underPenalty;
            OverPenalty = overPenalty;
            WrongToolPenalty = wrongToolPenalty;
        }

        public float PerfectMin { get; }
        public float PerfectMax { get; }
        public float UnderPenalty { get; }
        public float OverPenalty { get; }
        public float WrongToolPenalty { get; }

        public bool IsValid()
        {
            return Numeric.IsFinite(PerfectMin) && Numeric.IsFinite(PerfectMax)
                && Numeric.IsFinite(UnderPenalty) && Numeric.IsFinite(OverPenalty)
                && Numeric.IsFinite(WrongToolPenalty) && PerfectMin >= 0f
                && PerfectMax >= PerfectMin && UnderPenalty >= 0f
                && OverPenalty >= 0f && WrongToolPenalty >= 0f;
        }
    }

    /// <summary>只携带纯值和ActionToken的动作请求。</summary>
    public sealed class ActionRequest
    {
        public ActionRequest(
            ActionToken actionToken,
            int customerId,
            int stationId,
            ServiceActionType actionType,
            ServiceTool tool,
            float elapsedTime,
            bool isInterrupted,
            HaircutActionParameters haircutParameters = null)
        {
            ActionToken = actionToken ?? throw new ArgumentNullException(nameof(actionToken));
            CustomerId = customerId;
            StationId = stationId;
            ActionType = actionType;
            Tool = tool;
            ElapsedTime = elapsedTime;
            IsInterrupted = isInterrupted;
            HaircutParameters = haircutParameters;
        }

        public ActionToken ActionToken { get; }
        public int CustomerId { get; }
        public int StationId { get; }
        public ServiceActionType ActionType { get; }
        public ServiceTool Tool { get; }
        public float ElapsedTime { get; }
        public bool IsInterrupted { get; }
        public HaircutActionParameters HaircutParameters { get; }
    }

    /// <summary>Resolver的纯计算输出；ExpectedRevision在Apply阶段不可改写。</summary>
    public sealed class ActionResult : IEquatable<ActionResult>
    {
        public ActionResult(
            ActionToken actionToken,
            ExecutionStatus executionStatus,
            OrderEffect orderEffect,
            MistakeSeverity mistakeSeverity,
            ExecutionQuality executionQuality,
            PhysicalStateDelta physicalStateDelta,
            IEnumerable<MilestoneEvent> milestoneEvents,
            CustomerMetricsDelta metricsDelta,
            int incidentDelta,
            string suggestedInteractionContinuation,
            DiagnosticCode diagnosticCode,
            float elapsedTimeForLog)
        {
            ActionToken = actionToken ?? throw new ArgumentNullException(nameof(actionToken));
            ExecutionStatus = executionStatus;
            OrderEffect = orderEffect;
            MistakeSeverity = mistakeSeverity;
            ExecutionQuality = executionQuality;
            PhysicalStateDelta = physicalStateDelta ?? PhysicalStateDelta.Empty;
            MilestoneEvents = milestoneEvents?.ToArray() ?? Array.Empty<MilestoneEvent>();
            MetricsDelta = metricsDelta ?? CustomerMetricsDelta.Empty;
            IncidentDelta = incidentDelta;
            SuggestedInteractionContinuation = suggestedInteractionContinuation;
            DiagnosticCode = diagnosticCode;
            ElapsedTimeForLog = elapsedTimeForLog;
        }

        public string ActionId => ActionToken.ActionId;
        public int ExpectedPhysicalRevision => ActionToken.ExpectedPhysicalRevision;
        public ActionToken ActionToken { get; }
        public ExecutionStatus ExecutionStatus { get; }
        public OrderEffect OrderEffect { get; }
        public MistakeSeverity MistakeSeverity { get; }
        public ExecutionQuality ExecutionQuality { get; }
        public PhysicalStateDelta PhysicalStateDelta { get; }
        public IReadOnlyList<MilestoneEvent> MilestoneEvents { get; }
        public CustomerMetricsDelta MetricsDelta { get; }
        public int IncidentDelta { get; }
        public string SuggestedInteractionContinuation { get; }
        public DiagnosticCode DiagnosticCode { get; }
        public float ElapsedTimeForLog { get; }

        public bool IsInternallyValid()
        {
            if (!Numeric.IsFinite(ElapsedTimeForLog) || ElapsedTimeForLog < 0f || IncidentDelta < 0)
                return false;
            if (!PhysicalStateDelta.IsValid()) return false;
            if (ExecutionStatus != ExecutionStatus.Executed)
                return !PhysicalStateDelta.HasChanges && MetricsDelta.IsEmpty && MilestoneEvents.Count == 0 && IncidentDelta == 0;
            return true;
        }

        public bool Equals(ActionResult other)
        {
            return !ReferenceEquals(other, null) && Equals(ActionToken, other.ActionToken)
                && ExecutionStatus == other.ExecutionStatus && OrderEffect == other.OrderEffect
                && MistakeSeverity == other.MistakeSeverity && ExecutionQuality == other.ExecutionQuality
                && Equals(PhysicalStateDelta, other.PhysicalStateDelta)
                && MilestoneEvents.SequenceEqual(other.MilestoneEvents)
                && Equals(MetricsDelta, other.MetricsDelta) && IncidentDelta == other.IncidentDelta
                && SuggestedInteractionContinuation == other.SuggestedInteractionContinuation
                && DiagnosticCode == other.DiagnosticCode && ElapsedTimeForLog == other.ElapsedTimeForLog;
        }

        public override bool Equals(object obj) => Equals(obj as ActionResult);
        public override int GetHashCode() => ActionId.GetHashCode();

        public static ActionResult Unhandled(ActionRequest request)
        {
            return new ActionResult(request.ActionToken, ExecutionStatus.UnhandledActionRule, OrderEffect.None,
                MistakeSeverity.None, ExecutionQuality.NotApplicable, PhysicalStateDelta.Empty,
                Array.Empty<MilestoneEvent>(), CustomerMetricsDelta.Empty, 0, null,
                DiagnosticCode.UnhandledActionRule, Math.Max(0f, request.ElapsedTime));
        }

        public static ActionResult Invalid(ActionRequest request)
        {
            return new ActionResult(request.ActionToken, ExecutionStatus.Invalid, OrderEffect.None,
                MistakeSeverity.None, ExecutionQuality.NotApplicable, PhysicalStateDelta.Empty,
                Array.Empty<MilestoneEvent>(), CustomerMetricsDelta.Empty, 0, null,
                DiagnosticCode.InvalidRequest, Math.Max(0f, request.ElapsedTime));
        }
    }

    public enum FunnyDisasterType
    {
        FoamBurst
    }

    public enum RecoveryType
    {
        ReWash
    }

    /// <summary>最小事故事件，用于兼容层与事故修复链路。</summary>
    public sealed class FunnyDisasterEvent
    {
        public FunnyDisasterEvent(
            string eventId,
            FunnyDisasterType eventType,
            int customerId,
            ServiceActionType sourceAction,
            MistakeSeverity severity,
            bool isResolved)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            EventType = eventType;
            CustomerId = customerId;
            SourceAction = sourceAction;
            Severity = severity;
            IsResolved = isResolved;
        }

        public string EventId { get; }
        public FunnyDisasterType EventType { get; }
        public int CustomerId { get; }
        public ServiceActionType SourceAction { get; }
        public MistakeSeverity Severity { get; }
        public bool IsResolved { get; private set; }

        public void Resolve()
        {
            IsResolved = true;
        }
    }

    /// <summary>最小修复要求，用于玩家自主补救闭环。</summary>
    public sealed class RecoveryRequirement
    {
        public RecoveryRequirement(
            string recoveryId,
            int customerId,
            RecoveryType recoveryType,
            ServiceActionType[] requiredActions,
            string requiredStation,
            bool isCompleted)
        {
            RecoveryId = recoveryId ?? throw new ArgumentNullException(nameof(recoveryId));
            CustomerId = customerId;
            RecoveryType = recoveryType;
            RequiredActions = requiredActions ?? new ServiceActionType[0];
            RequiredStation = requiredStation;
            IsCompleted = isCompleted;
        }

        public string RecoveryId { get; }
        public int CustomerId { get; }
        public RecoveryType RecoveryType { get; }
        public ServiceActionType[] RequiredActions { get; }
        public string RequiredStation { get; }
        public bool IsCompleted { get; set; }
    }
}
