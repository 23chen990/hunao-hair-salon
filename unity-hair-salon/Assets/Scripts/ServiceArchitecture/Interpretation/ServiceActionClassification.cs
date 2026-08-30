using System;

namespace HairSalon.ServiceArchitecture
{
    public enum ServiceRelation
    {
        NormalService,
        ExtraService,
        WrongService
    }

    [Flags]
    public enum ServiceActionClassificationReason
    {
        None = 0,
        UnrequestedService = 1 << 0,
        CompletedServiceRepeated = 1 << 1,
        WrongHaircutTool = 1 << 2,
        ExecutionOver = 1 << 3,
        BlowDryWithFoam = 1 << 4
    }

    /// <summary>订单关系与事故候选是正交维度；Reason可同时保留多个解释事实。</summary>
    public sealed class ServiceActionClassification
    {
        public ServiceActionClassification(
            ServiceRelation relation,
            bool isDisasterCandidate,
            ServiceActionClassificationReason reason)
        {
            Relation = relation;
            IsDisasterCandidate = isDisasterCandidate;
            Reason = reason;
        }

        public ServiceRelation Relation { get; }
        public bool IsDisasterCandidate { get; }
        public ServiceActionClassificationReason Reason { get; }
    }
}
