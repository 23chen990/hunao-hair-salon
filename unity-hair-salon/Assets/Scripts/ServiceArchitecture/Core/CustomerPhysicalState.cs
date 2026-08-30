using System;

namespace HairSalon.ServiceArchitecture
{
    /// <summary>按剪发工具分别保存的固定长度进度，不依赖Dictionary序列化。</summary>
    [Serializable]
    public sealed class HaircutProgressByTool
    {
        private readonly float[] _values;

        public HaircutProgressByTool() : this(new float[3]) { }

        public HaircutProgressByTool(float[] values)
        {
            if (values == null || values.Length != 3) throw new ArgumentException("Exactly three haircut values are required.", nameof(values));
            _values = (float[])values.Clone();
            for (int index = 0; index < _values.Length; index++)
            {
                if (!Numeric.IsFinite(_values[index])) throw new ArgumentOutOfRangeException(nameof(values));
                _values[index] = Numeric.Clamp01(_values[index]);
            }
        }

        public float this[HaircutTool tool] => _values[(int)tool];
        public float[] CopyValues() => (float[])_values.Clone();

        internal HaircutProgressByTool WithDelta(HaircutTool tool, float delta)
        {
            float[] copy = CopyValues();
            copy[(int)tool] = Numeric.Clamp01(copy[(int)tool] + delta);
            return new HaircutProgressByTool(copy);
        }
    }

    /// <summary>顾客物理状态的唯一可写领域真相；不保存工位。</summary>
    [Serializable]
    public sealed class CustomerPhysicalState
    {
        public CustomerPhysicalState(
            float wetness = 0f,
            float foamAmount = 0f,
            ShampooState shampooState = ShampooState.None,
            bool isTowelWrapped = false,
            TowelContamination towelContamination = TowelContamination.None,
            TowelCondition towelCondition = TowelCondition.Intact,
            int physicalStateRevision = 0,
            float hairLengthDeviation = 0f,
            HaircutProgressByTool haircutProgressByTool = null)
        {
            if (!Numeric.IsFinite(wetness) || !Numeric.IsFinite(foamAmount) || !Numeric.IsFinite(hairLengthDeviation))
                throw new ArgumentOutOfRangeException(nameof(wetness));
            if (physicalStateRevision < 0) throw new ArgumentOutOfRangeException(nameof(physicalStateRevision));
            Wetness = Numeric.Clamp01(wetness);
            FoamAmount = Numeric.Clamp01(foamAmount);
            ShampooState = shampooState;
            IsTowelWrapped = isTowelWrapped;
            TowelContamination = towelContamination;
            TowelCondition = towelCondition;
            PhysicalStateRevision = physicalStateRevision;
            HairLengthDeviation = hairLengthDeviation;
            HaircutProgressByTool = haircutProgressByTool ?? new HaircutProgressByTool();
            NormalizeShampoo();
        }

        /// <summary>唯一可写干湿度，范围0..1。</summary>
        public float Wetness { get; private set; }
        /// <summary>头发泡沫量，范围0..1。</summary>
        public float FoamAmount { get; private set; }
        public ShampooState ShampooState { get; private set; }
        public bool IsTowelWrapped { get; private set; }
        public TowelContamination TowelContamination { get; private set; }
        public TowelCondition TowelCondition { get; private set; }
        /// <summary>唯一可写头发长度偏差。</summary>
        public float HairLengthDeviation { get; private set; }
        public HaircutProgressByTool HaircutProgressByTool { get; private set; }
        /// <summary>每次成功原子提交恰好增加一次。</summary>
        public int PhysicalStateRevision { get; private set; }
        public float OvercutSeverity => Math.Max(0f, -HairLengthDeviation);

        public CustomerPhysicalStateSnapshot CreateSnapshot()
        {
            return new CustomerPhysicalStateSnapshot(
                Wetness, FoamAmount, ShampooState, IsTowelWrapped, TowelContamination,
                TowelCondition, PhysicalStateRevision, HairLengthDeviation, HaircutProgressByTool.CopyValues());
        }

        internal void CommitFrom(CustomerPhysicalState next)
        {
            Wetness = next.Wetness;
            FoamAmount = next.FoamAmount;
            ShampooState = next.ShampooState;
            IsTowelWrapped = next.IsTowelWrapped;
            TowelContamination = next.TowelContamination;
            TowelCondition = next.TowelCondition;
            HairLengthDeviation = next.HairLengthDeviation;
            HaircutProgressByTool = new HaircutProgressByTool(next.HaircutProgressByTool.CopyValues());
            PhysicalStateRevision++;
        }

        private void NormalizeShampoo()
        {
            if (FoamAmount <= 0f && ShampooState == ShampooState.Normal)
                ShampooState = ShampooState.None;
        }
    }

    /// <summary>Resolver读取的不可变物理状态快照。</summary>
    public sealed class CustomerPhysicalStateSnapshot : IEquatable<CustomerPhysicalStateSnapshot>
    {
        private readonly float[] _haircutProgress;

        public CustomerPhysicalStateSnapshot(
            float wetness,
            float foamAmount,
            ShampooState shampooState,
            bool isTowelWrapped,
            TowelContamination towelContamination,
            TowelCondition towelCondition,
            int physicalStateRevision,
            float hairLengthDeviation,
            float[] haircutProgress)
        {
            Wetness = wetness;
            FoamAmount = foamAmount;
            ShampooState = shampooState;
            IsTowelWrapped = isTowelWrapped;
            TowelContamination = towelContamination;
            TowelCondition = towelCondition;
            PhysicalStateRevision = physicalStateRevision;
            HairLengthDeviation = hairLengthDeviation;
            _haircutProgress = (float[])haircutProgress.Clone();
        }

        public float Wetness { get; }
        public float FoamAmount { get; }
        public ShampooState ShampooState { get; }
        public bool IsTowelWrapped { get; }
        public TowelContamination TowelContamination { get; }
        public TowelCondition TowelCondition { get; }
        public int PhysicalStateRevision { get; }
        public float HairLengthDeviation { get; }
        public float OvercutSeverity => Math.Max(0f, -HairLengthDeviation);
        public float HaircutProgress(HaircutTool tool) => _haircutProgress[(int)tool];
        public bool IsHairDry(ServiceRuleConfig config) => Wetness <= config.HairDryWetnessThreshold;

        public CustomerPhysicalState ToMutable()
        {
            return new CustomerPhysicalState(Wetness, FoamAmount, ShampooState, IsTowelWrapped,
                TowelContamination, TowelCondition, PhysicalStateRevision, HairLengthDeviation,
                new HaircutProgressByTool(_haircutProgress));
        }

        public bool Equals(CustomerPhysicalStateSnapshot other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (Wetness != other.Wetness || FoamAmount != other.FoamAmount || ShampooState != other.ShampooState
                || IsTowelWrapped != other.IsTowelWrapped || TowelContamination != other.TowelContamination
                || TowelCondition != other.TowelCondition || PhysicalStateRevision != other.PhysicalStateRevision
                || HairLengthDeviation != other.HairLengthDeviation) return false;
            for (int index = 0; index < _haircutProgress.Length; index++)
                if (_haircutProgress[index] != other._haircutProgress[index]) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as CustomerPhysicalStateSnapshot);
        public override int GetHashCode() => PhysicalStateRevision * 397 ^ Wetness.GetHashCode();
    }

    /// <summary>Resolver输出的、可完整验证的物理差量。</summary>
    public sealed class PhysicalStateDelta : IEquatable<PhysicalStateDelta>
    {
        public static readonly PhysicalStateDelta Empty = new PhysicalStateDelta();

        public PhysicalStateDelta(
            float wetnessDelta = 0f,
            float foamAmountDelta = 0f,
            ShampooState? shampooState = null,
            bool? isTowelWrapped = null,
            TowelContamination? towelContamination = null,
            TowelCondition? towelCondition = null,
            float hairLengthDeviationDelta = 0f,
            HaircutTool? haircutTool = null,
            float haircutProgressDelta = 0f)
        {
            WetnessDelta = wetnessDelta;
            FoamAmountDelta = foamAmountDelta;
            ShampooState = shampooState;
            IsTowelWrapped = isTowelWrapped;
            TowelContamination = towelContamination;
            TowelCondition = towelCondition;
            HairLengthDeviationDelta = hairLengthDeviationDelta;
            HaircutTool = haircutTool;
            HaircutProgressDelta = haircutProgressDelta;
        }

        public float WetnessDelta { get; }
        public float FoamAmountDelta { get; }
        public ShampooState? ShampooState { get; }
        public bool? IsTowelWrapped { get; }
        public TowelContamination? TowelContamination { get; }
        public TowelCondition? TowelCondition { get; }
        public float HairLengthDeviationDelta { get; }
        public HaircutTool? HaircutTool { get; }
        public float HaircutProgressDelta { get; }
        public bool HasChanges => WetnessDelta != 0f || FoamAmountDelta != 0f || ShampooState.HasValue
            || IsTowelWrapped.HasValue || TowelContamination.HasValue || TowelCondition.HasValue
            || HairLengthDeviationDelta != 0f || (HaircutTool.HasValue && HaircutProgressDelta != 0f);

        public bool IsValid()
        {
            return Numeric.IsFinite(WetnessDelta) && Numeric.IsFinite(FoamAmountDelta)
                && Numeric.IsFinite(HairLengthDeviationDelta) && Numeric.IsFinite(HaircutProgressDelta);
        }

        public CustomerPhysicalState ApplyTo(CustomerPhysicalState current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            HaircutProgressByTool haircut = new HaircutProgressByTool(current.HaircutProgressByTool.CopyValues());
            if (HaircutTool.HasValue) haircut = haircut.WithDelta(HaircutTool.Value, HaircutProgressDelta);
            return new CustomerPhysicalState(
                current.Wetness + WetnessDelta,
                current.FoamAmount + FoamAmountDelta,
                ShampooState ?? current.ShampooState,
                IsTowelWrapped ?? current.IsTowelWrapped,
                TowelContamination ?? current.TowelContamination,
                TowelCondition ?? current.TowelCondition,
                current.PhysicalStateRevision,
                current.HairLengthDeviation + HairLengthDeviationDelta,
                haircut);
        }

        public bool Equals(PhysicalStateDelta other)
        {
            return !ReferenceEquals(other, null) && WetnessDelta == other.WetnessDelta
                && FoamAmountDelta == other.FoamAmountDelta && ShampooState == other.ShampooState
                && IsTowelWrapped == other.IsTowelWrapped && TowelContamination == other.TowelContamination
                && TowelCondition == other.TowelCondition && HairLengthDeviationDelta == other.HairLengthDeviationDelta
                && HaircutTool == other.HaircutTool && HaircutProgressDelta == other.HaircutProgressDelta;
        }

        public override bool Equals(object obj) => Equals(obj as PhysicalStateDelta);
        public override int GetHashCode() => WetnessDelta.GetHashCode() ^ FoamAmountDelta.GetHashCode();
    }
}
