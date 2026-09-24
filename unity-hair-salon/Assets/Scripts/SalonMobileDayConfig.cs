using System;

namespace HairSalon
{
    public enum DayOutcome
    {
        NotConfigured,
        InProgress,
        Achieved,
        NotAchieved,
        Missed = NotAchieved
    }

    public readonly struct DayEvaluation
    {
        public readonly int DayNumber;
        public readonly int TargetOrders;
        public readonly int CompletedOrders;
        public readonly int RemainingOrders;
        public readonly DayOutcome Outcome;

        public bool IsComplete => Outcome == DayOutcome.Achieved || Outcome == DayOutcome.NotAchieved;
        public bool TargetReached => TargetOrders > 0 && CompletedOrders >= TargetOrders;
        public bool CanRetry => Outcome == DayOutcome.NotAchieved;

        public DayEvaluation(int dayNumber, int targetOrders, int completedOrders, bool dayEnded,
            bool targetConfigured)
        {
            DayNumber = Math.Max(1, dayNumber);
            TargetOrders = Math.Max(0, targetOrders);
            CompletedOrders = Math.Max(0, completedOrders);
            RemainingOrders = Math.Max(0, TargetOrders - CompletedOrders);
            if (!targetConfigured || TargetOrders <= 0)
                Outcome = DayOutcome.NotConfigured;
            else if (!dayEnded)
                Outcome = DayOutcome.InProgress;
            else
                Outcome = CompletedOrders >= TargetOrders ? DayOutcome.Achieved : DayOutcome.NotAchieved;
        }

        public static DayEvaluation For(DayConfig config, int completedOrders, bool dayEnded)
        {
            if (config == null)
                return new DayEvaluation(1, 0, completedOrders, dayEnded, false);
            return new DayEvaluation(config.MobileDayNumber, config.TargetOrders, completedOrders, dayEnded,
                config.IsMobileProfile);
        }
    }

    /// <summary>
    /// Explicit mobile pacing profile. Legacy DayConfig defaults remain unchanged;
    /// callers opt in by creating this profile for the current day.
    /// </summary>
    public static class SalonMobileDayConfig
    {
        // Match the active countdown of a short Overcooked-style service level.
        // ClosingGrace remains a separate exit/settlement buffer.
        public const float BusinessDurationSeconds = 180f;
        public const float ClosingGraceSeconds = 15f;
        public const int WaitingCapacity = 4;
        public const int MaxConcurrentCustomers = 6;

        public static SalonServiceConfig CreateServiceConfig()
        {
            // Auto-blow consumes these shared timing fields. The mobile profile
            // allows a queue trip, one short service and the return journey;
            // legacy/manual profiles keep their existing timing defaults.
            return new SalonServiceConfig
            {
                WashServiceDuration = 3f,
                ManualBlowGoodStart = 12f,
                ManualBlowGoodEnd = 20f,
                ManualBlowMinorEnd = 26f,
                AutoBlowSafetyStopTime = 30f
            };
        }

        public static DayConfig CreateForDay(int dayNumber)
        {
            var config = new DayConfig();
            ApplyForDay(config, dayNumber);
            return config;
        }

        public static void ApplyForDay(DayConfig config, int dayNumber)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            int normalizedDay = Math.Max(1, dayNumber);
            config.IsMobileProfile = true;
            config.MobileDayNumber = normalizedDay;
            config.TargetOrders = TargetOrdersForDay(normalizedDay);
            config.StartingDuration = 1.25f;
            config.BusinessDuration = BusinessDurationSeconds;
            config.ClosingGraceDuration = ClosingGraceSeconds;
            config.MaxConcurrentCustomers = MaxConcurrentCustomers;
            config.WaitingCapacity = WaitingCapacity;
            config.OverloadWaitingThreshold = 3;
            config.AllowSpawnWhileWaitingOverload = true;
            config.OverloadSlowdownMultiplier = 2.1f;
            config.MinSpawnInterval = 5.5f;
            config.MaxSpawnInterval = 8.5f;
            config.PressurePhase2Start = .15f;
            config.PressurePhase3Start = .46f;
            config.PressurePhase4Start = .76f;
            config.OpeningIntensity = .62f;
            config.NormalIntensity = 1f;
            config.BusyIntensity = 1.25f;
            config.FinalPeakIntensity = 1.45f;
            config.Rush = new RushConfig
            {
                StartProgress = .7f,
                DurationSeconds = 18f,
                DurationProgress = .15f,
                IntensityMultiplier = 1.35f
            };
            config.OrderWeights = new System.Collections.Generic.List<DayOrderWeight>
            {
                new DayOrderWeight("O001", 1f),
                new DayOrderWeight("O002", 1f),
                new DayOrderWeight("O003", 1f),
                new DayOrderWeight("O004", 1f),
                new DayOrderWeight("O005", 1f)
            };
        }

        public static int TargetOrdersForDay(int dayNumber)
        {
            int normalizedDay = Math.Max(1, dayNumber);
            // Keep the first day finishable while the queue already teaches
            // pressure and background hand-offs. Add one order per day after it.
            if (normalizedDay == 1) return 3;
            if (normalizedDay == 2) return 4;
            return 5;
        }

        public static string PickOrderForSpawn(DayConfig config, int spawnIndex, float normalizedProgress)
        {
            int dayNumber = config == null ? 1 : config.MobileDayNumber;
            return PickOrderForSpawn(dayNumber, spawnIndex, normalizedProgress);
        }

        public static string PickOrderForSpawn(int dayNumber, int spawnIndex, float normalizedProgress)
        {
            int day = Math.Max(1, dayNumber);
            int index = Math.Max(0, spawnIndex);
            float progress = Clamp01(normalizedProgress);

            // The first two customers are deliberately fixed so the tutorial can
            // teach one short service followed by a two-step service.
            if (day == 1 && index == 0) return "O001";
            if (day == 1 && index == 1) return "O002";

            if (day == 1)
            {
                if (progress < .3f) return Pick(new[] { "O001", "O002" }, index);
                // Mid-day traffic mixes wash-first and cut-first orders so
                // neither service zone becomes a single bottleneck.
                if (progress < .62f) return Pick(new[] { "O002", "O003", "O004" }, index);
                if (progress < .86f) return Pick(new[] { "O003", "O004" }, index);
                return Pick(new[] { "O004", "O005" }, index);
            }

            if (day == 2)
            {
                if (progress < .22f) return Pick(new[] { "O001", "O002" }, index);
                if (progress < .58f) return Pick(new[] { "O002", "O003", "O004" }, index);
                return Pick(new[] { "O003", "O004", "O005" }, index);
            }

            if (progress < .18f) return Pick(new[] { "O001", "O002", "O003" }, index);
            if (progress < .52f) return Pick(new[] { "O003", "O004" }, index);
            return Pick(new[] { "O004", "O005" }, index);
        }

        /// <summary>
        /// Selects the deterministic haircut rhythm for the mobile path.
        /// The first haircut teaches the interaction with one tool; later
        /// haircuts occasionally require a second pass at the same chair.
        /// </summary>
        public static SalonTool[] GetHaircutToolsForSpawn(int dayNumber, int spawnIndex)
        {
            int day = Math.Max(1, dayNumber);
            int index = Math.Max(0, spawnIndex);
            if (index == 0)
                return new[] { SalonTool.Scissors };

            // Keep a small amount of tool variety without making the opening
            // order harder to read. Most later cuts use the approved
            // scissors-to-thinning sequence; every fourth one is a one-step
            // clipper order.
            if ((day + index) % 4 == 0)
                return new[] { SalonTool.Clippers };
            return new[] { SalonTool.Scissors, SalonTool.ThinningShears };
        }

        public static DayEvaluation Evaluate(int dayNumber, int completedOrders, bool dayEnded)
        {
            DayConfig config = CreateForDay(dayNumber);
            return DayEvaluation.For(config, completedOrders, dayEnded);
        }

        public static DayEvaluation Evaluate(DayConfig config, int completedOrders, bool dayEnded)
        {
            return DayEvaluation.For(config, completedOrders, dayEnded);
        }

        private static string Pick(string[] pool, int spawnIndex)
        {
            return pool[(spawnIndex * 31 + pool.Length) % pool.Length];
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
