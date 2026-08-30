using System;
using System.Collections.Generic;
using System.Linq;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>订单中的一个里程碑定义。</summary>
    public sealed class ServiceMilestoneDefinition
    {
        public ServiceMilestoneDefinition(MilestoneId id, MilestoneKind kind, bool required)
        {
            Id = id;
            Kind = kind;
            Required = required;
        }

        public MilestoneId Id { get; }
        public MilestoneKind Kind { get; }
        public bool Required { get; }
    }

    /// <summary>里程碑当前进度；历史完成与当前满足分开保存。</summary>
    public sealed class ServiceMilestoneState : IEquatable<ServiceMilestoneState>
    {
        public ServiceMilestoneState(MilestoneId id, MilestoneKind kind, bool everCompleted, bool satisfiedNow)
        {
            Id = id;
            Kind = kind;
            EverCompleted = everCompleted;
            SatisfiedNow = satisfiedNow;
        }

        public MilestoneId Id { get; }
        public MilestoneKind Kind { get; }
        public bool EverCompleted { get; }
        public bool SatisfiedNow { get; }
        public bool NeedsRedo => Kind == MilestoneKind.RevalidatableState && EverCompleted && !SatisfiedNow;
        public bool IsCompleteForSettlement => Kind == MilestoneKind.HistoricalEvent ? EverCompleted : SatisfiedNow;

        public bool Equals(ServiceMilestoneState other)
        {
            return !ReferenceEquals(other, null) && Id == other.Id && Kind == other.Kind
                && EverCompleted == other.EverCompleted && SatisfiedNow == other.SatisfiedNow;
        }

        public override bool Equals(object obj) => Equals(obj as ServiceMilestoneState);
        public override int GetHashCode() => (int)Id * 397 ^ (int)Kind;
    }

    /// <summary>Resolver产生的里程碑事实。</summary>
    public sealed class MilestoneEvent : IEquatable<MilestoneEvent>
    {
        public MilestoneEvent(MilestoneId milestoneId, bool everCompleted, bool satisfiedNow)
        {
            MilestoneId = milestoneId;
            EverCompleted = everCompleted;
            SatisfiedNow = satisfiedNow;
        }

        public MilestoneId MilestoneId { get; }
        public bool EverCompleted { get; }
        public bool SatisfiedNow { get; }

        public bool Equals(MilestoneEvent other)
        {
            return !ReferenceEquals(other, null) && MilestoneId == other.MilestoneId
                && EverCompleted == other.EverCompleted && SatisfiedNow == other.SatisfiedNow;
        }

        public override bool Equals(object obj) => Equals(obj as MilestoneEvent);
        public override int GetHashCode() => (int)MilestoneId;
    }

    /// <summary>可提交的服务进度状态。</summary>
    public sealed class ServiceProgress
    {
        private readonly Dictionary<MilestoneId, ServiceMilestoneState> _states =
            new Dictionary<MilestoneId, ServiceMilestoneState>();

        public ServiceMilestoneState this[MilestoneId id]
        {
            get
            {
                return _states.TryGetValue(id, out ServiceMilestoneState state)
                    ? state
                    : new ServiceMilestoneState(id, MilestoneCatalog.KindOf(id), false, false);
            }
        }

        public void Set(ServiceMilestoneState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _states[state.Id] = state;
        }

        public ServiceProgressSnapshot CreateSnapshot() => new ServiceProgressSnapshot(_states.Values);

        internal void ReplaceWith(ServiceProgress next)
        {
            _states.Clear();
            foreach (ServiceMilestoneState state in next._states.Values) _states[state.Id] = state;
        }
    }

    /// <summary>Resolver读取的不可变服务进度快照。</summary>
    public sealed class ServiceProgressSnapshot
    {
        private readonly Dictionary<MilestoneId, ServiceMilestoneState> _states;

        public ServiceProgressSnapshot(IEnumerable<ServiceMilestoneState> states)
        {
            _states = states.ToDictionary(state => state.Id,
                state => new ServiceMilestoneState(state.Id, state.Kind, state.EverCompleted, state.SatisfiedNow));
        }

        public ServiceMilestoneState this[MilestoneId id]
        {
            get
            {
                return _states.TryGetValue(id, out ServiceMilestoneState state)
                    ? state
                    : new ServiceMilestoneState(id, MilestoneCatalog.KindOf(id), false, false);
            }
        }

        public IReadOnlyList<ServiceMilestoneState> States => _states.Values.ToArray();
    }

    /// <summary>里程碑类型的唯一目录。</summary>
    public static class MilestoneCatalog
    {
        public static MilestoneKind KindOf(MilestoneId id)
        {
            switch (id)
            {
                case MilestoneId.RinseClean:
                case MilestoneId.HairDry:
                case MilestoneId.Untoweled:
                    return MilestoneKind.RevalidatableState;
                default:
                    return MilestoneKind.HistoricalEvent;
            }
        }
    }

    /// <summary>只描述顾客需要什么，不决定按钮可用性。</summary>
    public sealed class OrderDefinition
    {
        private readonly RequiredService[] _requiredServices;
        private readonly ServiceMilestoneDefinition[] _milestones;
        private readonly HaircutTool[] _requiredCutTools;

        public OrderDefinition(
            IEnumerable<RequiredService> requiredServices,
            IEnumerable<ServiceMilestoneDefinition> milestoneDefinitions,
            IEnumerable<HaircutTool> requiredCutTools,
            bool requiresCrossStationTowelTransition)
        {
            _requiredServices = requiredServices.ToArray();
            _milestones = milestoneDefinitions.ToArray();
            _requiredCutTools = requiredCutTools.ToArray();
            RequiresCrossStationTowelTransition = requiresCrossStationTowelTransition;
        }

        public IReadOnlyList<RequiredService> RequiredServices => (RequiredService[])_requiredServices.Clone();
        public IReadOnlyList<ServiceMilestoneDefinition> RequiredMilestones =>
            _milestones.Where(item => item.Required).ToArray();
        public IReadOnlyList<ServiceMilestoneDefinition> MilestoneDefinitions =>
            (ServiceMilestoneDefinition[])_milestones.Clone();
        public IReadOnlyList<HaircutTool> RequiredCutTools => (HaircutTool[])_requiredCutTools.Clone();
        public bool RequiresCrossStationTowelTransition { get; }

        public bool IsRequired(MilestoneId id, MilestoneKind kind)
        {
            return _milestones.Any(item => item.Id == id && item.Kind == kind && item.Required);
        }

        public static OrderDefinition WashOnly()
        {
            return new OrderDefinition(
                new[] { RequiredService.Wash, RequiredService.Dry },
                new[]
                {
                    Required(MilestoneId.WetHairApplied),
                    Required(MilestoneId.Shampooed),
                    Required(MilestoneId.RinseClean),
                    Required(MilestoneId.HairDry)
                },
                Array.Empty<HaircutTool>(),
                false);
        }

        public static OrderDefinition WashThenCut()
        {
            return new OrderDefinition(
                new[] { RequiredService.Wash, RequiredService.Cut },
                new[]
                {
                    Required(MilestoneId.WetHairApplied),
                    Required(MilestoneId.Shampooed),
                    Required(MilestoneId.RinseClean),
                    Required(MilestoneId.CleanTowelApplied),
                    Required(MilestoneId.Untoweled)
                },
                new[] { HaircutTool.Scissors },
                true);
        }

        private static ServiceMilestoneDefinition Required(MilestoneId id)
        {
            return new ServiceMilestoneDefinition(id, MilestoneCatalog.KindOf(id), true);
        }
    }

    /// <summary>根据提交后的物理状态与动作事件重新计算进度。</summary>
    public static class ServiceProgressCalculator
    {
        public static ServiceProgress Recalculate(
            ServiceProgressSnapshot previous,
            CustomerPhysicalStateSnapshot physical,
            IReadOnlyList<MilestoneEvent> events,
            ServiceRuleConfig config)
        {
            if (previous == null || physical == null || events == null || config == null)
                throw new ArgumentNullException(nameof(previous));

            var result = new ServiceProgress();
            foreach (MilestoneId id in Enum.GetValues(typeof(MilestoneId)))
            {
                ServiceMilestoneState old = previous[id];
                bool ever = old.EverCompleted;
                bool now = old.SatisfiedNow;
                foreach (MilestoneEvent milestoneEvent in events)
                {
                    if (milestoneEvent.MilestoneId != id) continue;
                    ever |= milestoneEvent.EverCompleted;
                    now = milestoneEvent.SatisfiedNow;
                }

                if (MilestoneCatalog.KindOf(id) == MilestoneKind.RevalidatableState)
                {
                    var milestoneEvent = events.FirstOrDefault(item => item.MilestoneId == id);
                    if (milestoneEvent != null)
                    {
                        now = milestoneEvent.SatisfiedNow;
                    }
                    else
                    {
                        switch (id)
                        {
                            case MilestoneId.RinseClean:
                                now = physical.FoamAmount <= config.ExitFoamThreshold && physical.ShampooState == ShampooState.None;
                                break;
                            case MilestoneId.HairDry:
                                now = physical.IsHairDry(config);
                                break;
                            case MilestoneId.Untoweled:
                                now = !physical.IsTowelWrapped;
                                break;
                        }
                    }
                    ever |= events.Any(item => item.MilestoneId == id && item.EverCompleted);
                }
                result.Set(new ServiceMilestoneState(id, MilestoneCatalog.KindOf(id), ever, now));
            }
            return result;
        }
    }
}
