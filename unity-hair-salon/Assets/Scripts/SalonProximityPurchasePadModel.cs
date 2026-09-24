using System;

namespace HairSalon
{
    /// <summary>
    /// Runtime state for an in-world construction point. The scene decides when
    /// the player is close enough; this model only owns payment progress.
    /// </summary>
    public enum SalonProximityPurchasePadState
    {
        Locked,
        Building,
        Unlocked
    }

    /// <summary>
    /// A zero-UI purchase pad that can be paid while the player is standing in
    /// its trigger area. Payment is deliberately split into two operations:
    /// <see cref="CalculatePayment"/> never mutates state, then the caller spends
    /// that amount through the game's wallet and calls <see cref="ApplyPayment"/>
    /// only when the wallet succeeds.
    /// </summary>
    public sealed class SalonProximityPurchasePadModel
    {
        public string PadId { get; }
        public int Cost { get; }
        public int Paid { get; private set; }
        public SalonProximityPurchasePadState State { get; private set; }

        public int RemainingCost => Cost - Paid;
        public bool IsUnlocked => State == SalonProximityPurchasePadState.Unlocked;
        public float Progress => Cost <= 0 ? 1f : (float)Paid / Cost;

        public SalonProximityPurchasePadModel(string padId, int cost)
        {
            if (string.IsNullOrWhiteSpace(padId))
                throw new ArgumentException("A purchase pad needs a stable id.", nameof(padId));
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost), cost,
                    "A purchase pad cost cannot be negative.");
            if (cost == 0)
                throw new ArgumentException("A purchase pad cost must be greater than zero.", nameof(cost));

            PadId = padId.Trim();
            Cost = cost;
            Paid = 0;
            State = SalonProximityPurchasePadState.Locked;
        }

        /// <summary>
        /// Calculates the integer amount the pad may request for this frame.
        /// The available wallet balance and the remaining cost both cap the
        /// result. Invalid or non-positive inputs return zero and leave the
        /// model unchanged.
        /// </summary>
        public int CalculatePayment(float deltaTime, int availableBalance, int spendPerSecond)
        {
            return CalculatePayment(deltaTime, availableBalance, (float)spendPerSecond);
        }

        /// <summary>
        /// Float-rate overload for scene tuning. The returned amount is still
        /// an integer because the existing wallet stores whole coins.
        /// </summary>
        public int CalculatePayment(float deltaTime, int availableBalance, float spendPerSecond)
        {
            if (State == SalonProximityPurchasePadState.Unlocked ||
                deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                availableBalance <= 0 || spendPerSecond <= 0f ||
                float.IsNaN(spendPerSecond) || float.IsInfinity(spendPerSecond) ||
                RemainingCost <= 0)
                return 0;

            double requested = Math.Floor((double)deltaTime * spendPerSecond);
            if (requested <= 0d) return 0;

            int frameLimit = requested >= int.MaxValue ? int.MaxValue : (int)requested;
            return Math.Min(RemainingCost, Math.Min(availableBalance, frameLimit));
        }

        /// <summary>
        /// Applies a wallet-approved payment. The amount must be positive and
        /// no greater than the remaining cost. A completed payment transitions
        /// the pad to Unlocked exactly once; later payments are rejected.
        /// </summary>
        public bool ApplyPayment(int amount)
        {
            if (State == SalonProximityPurchasePadState.Unlocked ||
                amount <= 0 || amount > RemainingCost)
                return false;

            Paid += amount;
            if (Paid == Cost)
                State = SalonProximityPurchasePadState.Unlocked;
            else
                State = SalonProximityPurchasePadState.Building;
            return true;
        }
    }
}
