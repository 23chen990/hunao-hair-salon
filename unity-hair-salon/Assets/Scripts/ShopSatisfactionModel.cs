using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum SatisfactionMood
    {
        Happy,
        Neutral,
        Sad
    }

    public readonly struct ShopSatisfactionSnapshot
    {
        public ShopSatisfactionSnapshot(int value)
        {
            Value = Math.Max(0, Math.Min(100, value));
        }

        public int Value { get; }
        public float NormalizedProgress => Value / 100f;
        public string ValueText => Value + "/100";
        public SatisfactionMood Mood => Value >= 80
            ? SatisfactionMood.Happy
            : Value >= 40 ? SatisfactionMood.Neutral : SatisfactionMood.Sad;
    }

    [Serializable]
    public sealed class ShopSatisfactionConfig
    {
        public int InitialValue = 90;
        public int CorrectServiceDelta = 2;
        public int FastErrorFreeBonus = 1;
        public int LongWaitPenalty = 4;
        public int WrongServicePenalty = 8;
        public int DisasterPenalty = 15;
        public int FailedServicePenalty = 8;
        public float LongWaitSeconds = 12f;
    }

    public sealed class ShopSatisfactionModel
    {
        private readonly ShopSatisfactionConfig _config;
        private readonly HashSet<int> _settledCustomers = new HashSet<int>();
        private ShopSatisfactionSnapshot _snapshot;

        public ShopSatisfactionModel(ShopSatisfactionConfig config = null)
        {
            _config = config ?? new ShopSatisfactionConfig();
            _snapshot = new ShopSatisfactionSnapshot(_config.InitialValue);
        }

        public int CurrentSatisfaction => _snapshot.Value;
        public float NormalizedProgress => _snapshot.NormalizedProgress;
        public string ValueText => _snapshot.ValueText;
        public SatisfactionMood Mood => _snapshot.Mood;
        public event Action<ShopSatisfactionSnapshot> Changed;

        public void SetCurrent(int value)
        {
            var next = new ShopSatisfactionSnapshot(value);
            if (next.Value == _snapshot.Value) return;
            _snapshot = next;
            Changed?.Invoke(_snapshot);
        }

        public void Adjust(int delta) => SetCurrent(CurrentSatisfaction + delta);

        public bool TrySettleCustomer(CustomerModel customer)
        {
            if (customer == null || !IsTerminal(customer.State) || !_settledCustomers.Add(customer.Id))
                return false;

            int delta = 0;
            bool successful = customer.ServiceResult == CustomerServiceResult.HappyCompletion ||
                              customer.ServiceResult == CustomerServiceResult.NormalCompletion;
            if (successful) delta += Math.Max(0, _config.CorrectServiceDelta);
            if (customer.ServiceResult == CustomerServiceResult.Failed)
                delta -= Math.Max(0, _config.FailedServicePenalty);
            if (customer.TotalWaitSeconds > Math.Max(0f, _config.LongWaitSeconds) || customer.HadServiceDelay)
                delta -= Math.Max(0, _config.LongWaitPenalty);
            if (customer.WrongStationCount > 0 || customer.WrongServiceKind != CustomerWrongServiceKind.None)
                delta -= Math.Max(0, _config.WrongServicePenalty);
            if (customer.AccidentSeverity != AccidentSeverity.None)
                delta -= Math.Max(0, _config.DisasterPenalty);
            if (customer.ServiceResult == CustomerServiceResult.HappyCompletion &&
                customer.TotalWaitSeconds <= Math.Max(0f, _config.LongWaitSeconds) &&
                !customer.HadServiceDelay && customer.WrongStationCount == 0 &&
                customer.WrongServiceKind == CustomerWrongServiceKind.None &&
                customer.AccidentSeverity == AccidentSeverity.None)
                delta += Math.Max(0, _config.FastErrorFreeBonus);

            Adjust(delta);
            return true;
        }

        private static bool IsTerminal(CustomerState state) =>
            state == CustomerState.Leaving || state == CustomerState.Exited;
    }
}
