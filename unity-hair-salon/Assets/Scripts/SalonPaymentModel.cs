using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum CoinPileSize { Small, Medium, Large }
    public enum PaymentDropState { Pending, Collecting, Collected }

    [Serializable]
    public sealed class SalonRewardConfig
    {
        public int RecoveredBaseReward = 120;
        public int PerfectBaseReward = 300;
        public int HappyTipReward = 60;
        public int WashBaseReward = 140;
        public int BlowBaseReward = 160;
        public float HappyMaxWaitSeconds = 12f;
        public float HappyMaxServiceSeconds = 15f;
    }

    [Serializable]
    public sealed class PaymentDropModel
    {
        public int Id;
        public int CustomerId;
        public int WorkstationId;
        public int Amount;
        public int BaseReward;
        public int TipReward;
        public int TotalReward;
        public int OrderIncomeComponent;
        public int TipIncomeComponent;
        public int FinalPayment;
        public CoinPileSize Size;
        public PaymentDropState State = PaymentDropState.Pending;
    }

    public sealed class SalonPaymentModel
    {
        private readonly List<PaymentDropModel> _drops = new List<PaymentDropModel>();
        private int _nextDropId = 1;

        public int Balance { get; private set; }
        public SalonRewardConfig Config { get; }
        public IReadOnlyList<PaymentDropModel> Drops => _drops;

        public SalonPaymentModel(int startingBalance = 0, SalonRewardConfig config = null)
        {
            Balance = Math.Max(0, startingBalance);
            Config = config ?? new SalonRewardConfig();
        }

        /// <summary>
        /// Restores the persisted balance without touching the current day's payment
        /// drops or their collection states. Call only during initialization or a
        /// business-day boundary; callers owning the game model enforce that boundary.
        /// </summary>
        public void RestoreBalance(int balance)
        {
            if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
            Balance = balance;
        }

        public PaymentDropModel CreateHaircutPayment(
            int customerId, int workstationId, HaircutServiceRating rating)
        {
            return CreateHaircutPayment(customerId, workstationId, rating, false);
        }

        public PaymentDropModel CreateHaircutPayment(
            int customerId, int workstationId, HaircutServiceRating rating, bool happyCompletion)
        {
            int baseReward;
            switch (rating)
            {
                case HaircutServiceRating.Perfect:
                    baseReward = Math.Max(0, Config.PerfectBaseReward);
                    break;
                case HaircutServiceRating.Recovered:
                    baseReward = Math.Max(0, Config.RecoveredBaseReward);
                    break;
                default:
                    throw new ArgumentException("Only a completed service can create payment.", nameof(rating));
            }

            int tipReward = happyCompletion ? Math.Max(0, Config.HappyTipReward) : 0;
            return CreatePaymentDrop(customerId, workstationId, baseReward, tipReward);
        }

        public PaymentDropModel CreateOrderPayment(
            int customerId, int workstationId, IReadOnlyList<ServiceType> order,
            HaircutServiceRating haircutRating, bool happyCompletion)
        {
            if (order == null || order.Count == 0)
                throw new ArgumentException("A completed order must contain at least one service.", nameof(order));
            int baseReward = 0;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == ServiceType.Wash) baseReward += Math.Max(0, Config.WashBaseReward);
                else if (order[i] == ServiceType.Dry) baseReward += Math.Max(0, Config.BlowBaseReward);
                else if (order[i] == ServiceType.Cut)
                {
                    if (haircutRating == HaircutServiceRating.Recovered)
                        baseReward += Math.Max(0, Config.RecoveredBaseReward);
                    else if (haircutRating == HaircutServiceRating.Perfect)
                        baseReward += Math.Max(0, Config.PerfectBaseReward);
                    else
                        throw new ArgumentException("A completed cut step requires a completed haircut rating.", nameof(haircutRating));
                }
            }
            int tipReward = happyCompletion ? Math.Max(0, Config.HappyTipReward) : 0;
            return CreatePaymentDrop(customerId, workstationId, baseReward, tipReward);
        }

        public PaymentDropModel CreateFinalPayment(
            int customerId, int workstationId, int finalOrderPrice, int tipIncome)
        {
            return CreatePaymentDrop(customerId, workstationId,
                Math.Max(0, finalOrderPrice), Math.Max(0, tipIncome));
        }

        private PaymentDropModel CreatePaymentDrop(
            int customerId, int workstationId, int baseReward, int tipReward)
        {
            int totalReward = baseReward + tipReward;

            var drop = new PaymentDropModel
            {
                Id = _nextDropId++,
                CustomerId = customerId,
                WorkstationId = workstationId,
                Amount = totalReward,
                BaseReward = baseReward,
                TipReward = tipReward,
                TotalReward = totalReward,
                OrderIncomeComponent = baseReward,
                TipIncomeComponent = tipReward,
                FinalPayment = totalReward,
                Size = CoinPileSize.Medium
            };
            _drops.Add(drop);
            return drop;
        }

        public bool BeginCollection(int dropId)
        {
            PaymentDropModel drop = Find(dropId);
            if (drop == null || drop.State != PaymentDropState.Pending) return false;
            drop.State = PaymentDropState.Collecting;
            return true;
        }

        public bool CompleteCollection(int dropId)
        {
            PaymentDropModel drop = Find(dropId);
            if (drop == null || drop.State != PaymentDropState.Collecting) return false;
            drop.State = PaymentDropState.Collected;
            Balance += drop.TotalReward;
            return true;
        }

        public bool TrySpend(int amount)
        {
            int cost = Math.Max(0, amount);
            if (Balance < cost) return false;
            Balance -= cost;
            return true;
        }

        public void ClearDayDrops()
        {
            _drops.Clear();
        }

        public PaymentDropModel Find(int dropId)
        {
            for (int i = 0; i < _drops.Count; i++)
                if (_drops[i].Id == dropId) return _drops[i];
            return null;
        }
    }
}
