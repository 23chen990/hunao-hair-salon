using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum CheckoutCustomerState
    {
        WalkingToCheckout,
        Queued,
        Paying,
        PaidLeaving,
        UnpaidLeaving
    }

    public sealed class CheckoutBill
    {
        public int CustomerId;
        public int Amount;
        public CheckoutCustomerState State;
        public float WaitedSeconds;
        public float PaymentElapsed;
        public int QueueIndex;
        public bool HasArrived;
        public float WaitRatio(float patienceSeconds) =>
            patienceSeconds <= 0f ? 0f : Math.Max(0f, Math.Min(1f, 1f - WaitedSeconds / patienceSeconds));
    }

    public readonly struct CheckoutPaidEvent
    {
        public readonly int CustomerId;
        public readonly int Amount;
        public CheckoutPaidEvent(int customerId, int amount) { CustomerId = customerId; Amount = amount; }
    }

    public readonly struct CheckoutAbandonedEvent
    {
        public readonly int CustomerId;
        public readonly int LostAmount;
        public readonly float SatisfactionPenalty;
        public CheckoutAbandonedEvent(int customerId, int lostAmount, float satisfactionPenalty)
        {
            CustomerId = customerId;
            LostAmount = lostAmount;
            SatisfactionPenalty = satisfactionPenalty;
        }
    }

    /// <summary>
    /// Scene-independent checkout queue. Movement is owned by the presentation layer;
    /// this model only knows arrival order and queue slots.
    /// </summary>
    public sealed class SalonCheckoutModel
    {
        public const float DefaultPaymentSeconds = .35f;
        public const float DefaultPatienceSeconds = 20f;
        public const int DefaultQueueCapacity = 4;
        public const float DefaultAbandonmentPenalty = -.05f;

        private readonly List<CheckoutBill> _bills = new List<CheckoutBill>();
        private readonly List<CheckoutBill> _queue = new List<CheckoutBill>();
        private readonly float _paymentSeconds;
        private readonly float _patienceSeconds;

        public int QueueCapacity { get; }
        public float PaymentSeconds => _paymentSeconds;
        public float PatienceSeconds => _patienceSeconds;
        public float AbandonmentPenalty { get; }
        public int PaidAmount { get; private set; }
        public int UnpaidAmount { get; private set; }
        public int UnpaidCount { get; private set; }
        public bool HasPendingCustomers => _queue.Count > 0;
        public IReadOnlyList<CheckoutBill> Bills => _bills;
        public IReadOnlyList<CheckoutBill> Queue => _queue;
        public event Action<CheckoutPaidEvent> Paid;
        public event Action<CheckoutAbandonedEvent> Abandoned;

        public SalonCheckoutModel(int queueCapacity = DefaultQueueCapacity,
            float paymentSeconds = DefaultPaymentSeconds,
            float patienceSeconds = DefaultPatienceSeconds,
            float abandonmentPenalty = DefaultAbandonmentPenalty)
        {
            QueueCapacity = Math.Max(1, queueCapacity);
            _paymentSeconds = Math.Max(.01f, paymentSeconds);
            _patienceSeconds = Math.Max(.01f, patienceSeconds);
            AbandonmentPenalty = Math.Min(0f, abandonmentPenalty);
        }

        public CheckoutBill RegisterCompletedBill(int customerId, int amount)
        {
            if (amount <= 0) return null;
            CheckoutBill existing = Find(customerId);
            if (existing != null) return existing;
            var bill = new CheckoutBill {
                CustomerId = customerId, Amount = amount,
                State = CheckoutCustomerState.WalkingToCheckout
            };
            _bills.Add(bill);
            _queue.Add(bill);
            RefreshQueueIndices();
            return bill;
        }

        public CheckoutBill RegisterBill(int customerId, int amount) =>
            RegisterCompletedBill(customerId, amount);

        public bool MarkArrived(int customerId)
        {
            CheckoutBill bill = Find(customerId);
            if (bill == null || bill.State == CheckoutCustomerState.PaidLeaving ||
                bill.State == CheckoutCustomerState.UnpaidLeaving) return false;
            if (bill.HasArrived) return true;
            bill.HasArrived = true;
            bill.State = CheckoutCustomerState.Queued;
            return true;
        }

        public bool Arrive(int customerId) => MarkArrived(customerId);

        public void Tick(float dt, bool cashierAttended)
        {
            float step = Math.Max(0f, dt);
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                CheckoutBill bill = _queue[i];
                if (!bill.HasArrived) continue;
                if (!cashierAttended || i > 0) bill.WaitedSeconds += step;
                if (bill.WaitedSeconds >= _patienceSeconds)
                    AbandonBill(bill);
            }

            if (_queue.Count == 0 || !cashierAttended || !_queue[0].HasArrived) return;
            CheckoutBill first = _queue[0];
            if (first.State == CheckoutCustomerState.Queued)
                first.State = CheckoutCustomerState.Paying;
            if (first.State != CheckoutCustomerState.Paying) return;
            first.PaymentElapsed += step;
            if (first.PaymentElapsed >= _paymentSeconds) PayBill(first);
        }

        public float GetWaitingRatio(int customerId)
        {
            CheckoutBill bill = Find(customerId);
            return bill == null ? 0f : bill.WaitRatio(_patienceSeconds);
        }

        public float WaitingRatio(int customerId) => GetWaitingRatio(customerId);

        public CheckoutCustomerState GetState(int customerId)
        {
            CheckoutBill bill = Find(customerId);
            return bill == null ? CheckoutCustomerState.UnpaidLeaving : bill.State;
        }

        public void ForceClose()
        {
            while (_queue.Count > 0)
                AbandonBill(_queue[0]);
        }

        public void ForceEndOfDay() => ForceClose();

        private void PayBill(CheckoutBill bill)
        {
            _queue.Remove(bill);
            bill.State = CheckoutCustomerState.PaidLeaving;
            PaidAmount += bill.Amount;
            Paid?.Invoke(new CheckoutPaidEvent(bill.CustomerId, bill.Amount));
            RefreshQueueIndices();
        }

        private void AbandonBill(CheckoutBill bill)
        {
            _queue.Remove(bill);
            bill.State = CheckoutCustomerState.UnpaidLeaving;
            UnpaidAmount += bill.Amount;
            UnpaidCount++;
            Abandoned?.Invoke(new CheckoutAbandonedEvent(
                bill.CustomerId, bill.Amount, AbandonmentPenalty));
            RefreshQueueIndices();
        }

        public CheckoutBill Find(int customerId)
        {
            for (int i = 0; i < _bills.Count; i++)
                if (_bills[i].CustomerId == customerId) return _bills[i];
            return null;
        }

        private void RefreshQueueIndices()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].QueueIndex != i)
                {
                    _queue[i].HasArrived = false;
                    _queue[i].State = CheckoutCustomerState.WalkingToCheckout;
                }
                _queue[i].QueueIndex = i;
            }
        }
    }
}
