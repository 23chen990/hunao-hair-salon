using System;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>顾客头发上的洗发水形态。</summary>
    public enum ShampooState { None, Normal, ClumpedOnDryHair }
    /// <summary>毛巾污染来源，与毛巾损坏状态正交。</summary>
    public enum TowelContamination { None, Foam, Shampoo }
    /// <summary>毛巾本体状态。</summary>
    public enum TowelCondition { Intact, Damaged }
    /// <summary>阶段2支持的领域动作。</summary>
    public enum ServiceActionType { Unknown, Shower, Shampoo, BlowDry, WrapTowel, RemoveTowel, Haircut, ServiceCompletion }
    /// <summary>动作使用的纯领域工具标识。</summary>
    public enum ServiceTool { None, Shower, Shampoo, Towel, BlowDryer, Scissors, ThinningShears, Clippers }
    /// <summary>订单服务类别。</summary>
    public enum RequiredService { Wash, Dry, Cut, Dye, Perm }
    /// <summary>剪发工具进度索引。</summary>
    public enum HaircutTool { Scissors, ThinningShears, Clippers }
    /// <summary>里程碑的结算语义。</summary>
    public enum MilestoneKind { HistoricalEvent, RevalidatableState, ExitConstraint }
    /// <summary>阶段2里程碑标识。</summary>
    public enum MilestoneId
    {
        WetHairApplied, Shampooed, CleanTowelApplied,
        ScissorsCompleted, ThinningShearsCompleted, ClippersCompleted,
        DyeCompleted, PermCompleted,
        RinseClean, HairDry, Untoweled
    }
    /// <summary>离店前必须满足、但不计入服务步骤的约束。</summary>
    public enum ExitConstraint
    {
        RequiredHistoricalEventsCompleted,
        RequiredRevalidatableStatesSatisfied,
        NoFoam, NoClumpedShampoo, NoShampooResidue, NoTowel, DryEnough, NotMoving, NoActiveAction
    }
    public enum ExecutionStatus { Executed, Resisted, Invalid, UnhandledActionRule, StaleState }
    public enum OrderEffect { None, Progress, ExtraService }
    public enum MistakeSeverity { None, Minor, Recoverable, Major }
    public enum ExecutionQuality { NotApplicable, Under, Perfect, Over }
    public enum CustomerMetricsSource { None, Action, WaitingTick, WrongStation, Recovery, Incident }
    public enum DiagnosticCode
    {
        None, UnhandledActionRule, InvalidRequest, InvalidToken, StaleContext,
        StalePhysicalState, AlreadyApplied, InvalidResult, InvariantViolation
    }
    public enum ClearReason
    {
        CustomerChanged, MovementStarted, StationChanged, FocusExited,
        QuickAction, ToolBecameInvalid, OrderCompleted, SceneUnloaded
    }
    public enum ApplyStatus
    {
        Applied, InvalidToken, StaleContext, StalePhysicalState,
        AlreadyApplied, InvalidResult, InvariantViolation
    }
    public enum ServiceCompletionState { InProgress, Completed }
    public enum ServiceExecutionType { Wash, Haircut, BlowDry }
    public enum ServiceExecutionState { Idle, Started, Executing, Completed }
    public enum ServiceCustomerOutcome { Normal, Unhappy }
    public enum ServiceLeaveReason { None, ServiceFinished }
    /// <summary>面向玩家的主要离店阻挡原因，由ExitConstraint派生，不保存第二套状态。</summary>
    public enum ExitBlockReason
    {
        None, RequirementsIncomplete, FoamRemaining, ShampooResidue,
        TowelWrapped, WetHair, Moving, ActiveAction
    }

    /// <summary>集中保存Resolver与结算使用的确定性阈值。</summary>
    [Serializable]
    public sealed class ServiceRuleConfig
    {
        public ServiceRuleConfig(
            float resistanceWindowSeconds = .5f,
            float unrequestedServiceSatisfactionPenalty = 8f)
        {
            if (!Numeric.IsFinite(resistanceWindowSeconds) || resistanceWindowSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(resistanceWindowSeconds));
            if (!Numeric.IsFinite(unrequestedServiceSatisfactionPenalty)
                || unrequestedServiceSatisfactionPenalty < 0f)
                throw new ArgumentOutOfRangeException(nameof(unrequestedServiceSatisfactionPenalty));
            ResistanceWindowSeconds = resistanceWindowSeconds;
            UnrequestedServiceSatisfactionPenalty = unrequestedServiceSatisfactionPenalty;
        }

        public float WetHairThreshold { get; } = 0.35f;
        public float HairDryWetnessThreshold { get; } = 0.05f;
        public float ExitWetnessThreshold { get; } = 0.05f;
        public float ExitFoamThreshold { get; } = 0.001f;
        public float ShampooMilestoneFoamThreshold { get; } = 0.65f;
        public float ShowerWetnessPerSecond { get; } = 0.8f;
        public float ShampooFoamPerSecond { get; } = 0.7f;
        public float RinseFoamPerSecond { get; } = 0.8f;
        public float BlowDryWetnessPerSecond { get; } = 0.65f;
        public float BlowDryFoamEffectMultiplier { get; } = 0.4f;
        public float BlowDryTowelDamageSeconds { get; } = 6f;
        public float BlowDryDryGoodStartSeconds { get; } = 3f;
        public float BlowDryDryGoodEndSeconds { get; } = 5f;
        public float BlowDryDryMajorThresholdSeconds { get; } = 7f;
        public float DryShampooSatisfactionPenalty { get; } = -4f;
        public float ResistanceWindowSeconds { get; }
        public float UnrequestedServiceSatisfactionPenalty { get; }
    }

    internal static class Numeric
    {
        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        public static float Clamp100(float value) => Math.Max(0f, Math.Min(100f, value));
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>根据真实按住时长对连续剪发动作分类。</summary>
    public static class HoldQualityEvaluator
    {
        public static ExecutionQuality Evaluate(float elapsed, float perfectMin, float perfectMax)
        {
            if (!Numeric.IsFinite(elapsed) || !Numeric.IsFinite(perfectMin) || !Numeric.IsFinite(perfectMax)
                || perfectMin < 0f || perfectMax < perfectMin)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (elapsed < perfectMin) return ExecutionQuality.Under;
            return elapsed <= perfectMax ? ExecutionQuality.Perfect : ExecutionQuality.Over;
        }
    }
}
