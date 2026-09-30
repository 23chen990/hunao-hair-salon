using System;

namespace HairSalon
{
    /// <summary>Admission and service mix, separate from room capacity and the clock.</summary>
    public sealed class SalonPacingDirector
    {
        public float RestRemaining { get; private set; }
        public string AdmissionReason { get; private set; } = "ready";
        public int BatchArrivals { get; private set; }
        private static readonly string[] DryerPractice = { "O001", "O004", "O001", "O001", "O004", "O001" };
        private static readonly string[] WashPractice = { "O001", "O003", "O001", "O004", "O001", "O003" };
        private static readonly string[] FluentMenu = { "O001", "O003", "O004", "O001", "O002", "O001", "O003", "O004", "O001", "O005" };

        public void Tick(float dt, SalonGameModel game, bool hasUsefulManagementWork = false)
        {
            RestRemaining = Math.Max(0f, RestRemaining - Math.Max(0f, dt));
            if (!hasUsefulManagementWork) RestRemaining = Math.Min(RestRemaining, 2f);
            if (BatchArrivals > 0 && ActiveCount(game) == 0)
            {
                // Start at the end of the actual workload, not while the player
                // is still collecting payment. A longer window is useful only
                // when there is construction or restocking to do, never forced idling.
                RestRemaining = hasUsefulManagementWork ? 8f : 2f;
                BatchArrivals = 0;
            }
        }

        public void RegisterArrival() { BatchArrivals++; }

        public bool CanAdmit(SalonGameModel game, SalonProgressData progress)
        {
            AdmissionReason = "ready";
            if (RestRemaining > 0f) return Defer("recovery");
            int limit = ActiveLimit(progress);
            if (ActiveCount(game) >= limit) return Defer(limit == 1 ? "learning" : "workload");
            if (BatchArrivals >= Math.Max(2, limit)) return Defer("finish-batch");
            foreach (var c in game.Customers)
            {
                if (!IsActive(c)) continue;
                if (c.State == CustomerState.Checkout || c.State == CustomerState.Finished) return Defer("checkout");
                if (c.Emotion == CustomerEmotion.Angry || c.Patience <= 30f) return Defer("recover-patience");
                if (c.State == CustomerState.Waiting || c.State == CustomerState.Entering) return Defer("greet-existing");
            }
            return true;
        }

        private bool Defer(string reason) { AdmissionReason = reason; return false; }

        public static int ActiveLimit(SalonProgressData progress)
        {
            int limit = ExperiencedLimit(progress);
            if (progress.PaidCustomerCount < progress.CapacityPracticeUntilPaid &&
                progress.CapacityPracticeLimit > 0)
                limit = Math.Min(limit, progress.CapacityPracticeLimit);
            return limit;
        }

        private static int ExperiencedLimit(SalonProgressData progress)
        {
            if (progress.PaidCustomerCount < 2 ||
                (progress.BlowDryerPurchased && progress.PaidDryOrderCount == 0) ||
                (progress.WashStationPurchased && progress.PaidWashOrderCount == 0)) return 1;
            if (!progress.HaircutExpansionPurchased || progress.PaidCustomerCount < 8 ||
                progress.PaidDryOrderCount < 2 || progress.PaidWashOrderCount < 2) return 2;
            if (!progress.AutoBlowPurchased || !progress.WashAnnexExpansionPurchased ||
                !progress.ExtraSeatsPurchased || progress.PaidCustomerCount < 18) return 3;
            return 4;
        }

        public static bool IsActive(CustomerModel c) => c != null &&
            // In the mobile checkout flow Finished is the service-complete
            // animation BEFORE walking to the cashier, not a paid order.
            c.State != CustomerState.Leaving && c.State != CustomerState.Exited;

        public static int ActiveCount(SalonGameModel game)
        {
            int count = 0;
            foreach (var c in game.Customers) if (IsActive(c)) count++;
            return count;
        }

        public static string SelectOrder(SalonGameModel game, SalonProgressData progress,
            int sequence, string teachingOrder, int washKits, float remainingSeconds)
        {
            var menu = new SalonServiceMenu(progress.BlowDryerPurchased, progress.WashStationPurchased);
            // Reloaded saves without mastery re-teach equipment safely; money
            // still buys immediately and never fabricates service experience.
            string lesson = !string.IsNullOrEmpty(teachingOrder) ? teachingOrder :
                menu.HasBlowDryer && progress.PaidDryOrderCount == 0 ? "O004" :
                menu.HasWashStation && progress.PaidWashOrderCount == 0 ? "O003" : null;
            if (lesson != null && Fits(lesson, game, menu, washKits, remainingSeconds)) return lesson;

            string[] pool = !menu.HasWashStation ? DryerPractice :
                progress.PaidWashOrderCount >= 3 && progress.PaidDryOrderCount >= 3 ? FluentMenu : WashPractice;
            for (int offset = 0; offset < pool.Length; offset++)
            {
                string candidate = pool[(Math.Max(0, sequence) % pool.Length + offset) % pool.Length];
                if (Fits(candidate, game, menu, washKits, remainingSeconds)) return candidate;
            }
            return null;
        }

        private static bool Fits(string orderId, SalonGameModel game, SalonServiceMenu menu, int washKits, float seconds)
        {
            if (!menu.IsOrderAvailable(orderId)) return false;
            var needs = SalonOrderCatalog.Get(orderId);
            // Local conservative touch-play budgets, not claimed competitor timings.
            float budget = orderId == "O001" ? 18f : orderId == "O005" ? 48f : 34f;
            if (seconds < budget) return false;
            int basins = 0, chairs = 0, washDemand = 0, chairDemand = 0, reservedKits = 0;
            foreach (var station in game.Workstations)
            {
                if (!station.IsUsable) continue;
                if (station.Type == WorkstationType.Wash) basins++;
                if (station.Type == WorkstationType.Haircut) chairs++;
            }
            foreach (var c in game.Customers)
            {
                if (!IsActive(c) || c.IsComplete) continue;
                if (c.CurrentNeed == ServiceType.Cut || c.CurrentNeed == ServiceType.Dry) chairDemand++;
                for (int step = c.Step; step < c.Needs.Count; step++)
                    if (c.Needs[step] == ServiceType.Wash)
                    {
                        washDemand++;
                        if (!c.ShampooApplied) reservedKits++;
                        break;
                    }
            }
            if (needs[0] == ServiceType.Wash)
            {
                if (washDemand >= basins || washKits <= reservedKits) return false;
                // A second basin is extra parallel capacity, not a reason to
                // keep stacking wash-first tickets while haircut chairs sit idle.
                if (washDemand > 0 && chairDemand < chairs) return false;
                return true;
            }
            return chairDemand < chairs;
        }
    }
}
