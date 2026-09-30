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
            // Foam and the basic dryer are ready after eight seconds. The green
            // return window lasts six seconds; orange and red warn about lateness.
            return new SalonServiceConfig
            {
                WashServiceDuration = 3f,
                FoamOptimalStart = 8f,
                ShampooDuration = 1.2f,
                FoamMinorLateThreshold = 14f,
                FoamModerateLateThreshold = 20f,
                ReadyDelayGrace = 6f,
                BackgroundFoamIsNotServiceDelay = true,
                ManualBlowGoodStart = 7.25f,
                ManualBlowGoodEnd = 13.25f,
                ManualBlowMinorEnd = 19.25f,
                AutoBlowSafetyStopTime = 22f
            };
        }

        /// <summary>
        /// Overcooked tips every served dish by ticket colour (8/5/3 points on a
        /// 20-point base). Customer patience is this salon's ticket timer.
        /// </summary>
        public static void ApplyRewardProfile(SalonRewardConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.RecoveredBaseReward = 250;
            config.PerfectBaseReward = 300;
            config.SpeedTipTiers = true;
            config.TipGreenPatienceRatio = .66f;
            config.TipYellowPatienceRatio = .33f;
            config.TipGreenReward = 60;
            config.TipYellowReward = 38;
            config.TipRedReward = 22;
        }

        /// <summary>
        /// Overcooked removes 10 points for an expired ticket against 20+ for a
        /// served dish, so one good order recovers one miss. Shop satisfaction
        /// follows the same order-of-magnitude instead of +2 per success versus
        /// -17 for one late rinse.
        /// </summary>
        public static void ApplySatisfactionProfile(ShopSatisfactionConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.CorrectServiceDelta = 6;
            config.FastErrorFreeBonus = 2;
            config.LongWaitPenalty = 1;
            config.WrongServicePenalty = 4;
            config.MinorAccidentPenalty = 2;
            config.DisasterPenalty = 6;
            config.FailedServicePenalty = 4;
        }

        public const int OvernightSatisfactionTarget = 90;

        /// <summary>
        /// Each Overcooked level starts from a fresh score. The salon keeps its
        /// reputation, but closes half of the gap to the opening value overnight.
        /// </summary>
        public static int OvernightSatisfaction(int endOfDaySatisfaction)
        {
            int value = Math.Max(0, Math.Min(100, endOfDaySatisfaction));
            if (value >= OvernightSatisfactionTarget) return value;
            return value + (OvernightSatisfactionTarget - value + 1) / 2;
        }

        public static float PatienceDrainPerSecond(int dayNumber) => Math.Max(1, dayNumber) <= 1 ? 1.6f : 1.8f;

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
            config.MaxConcurrentCustomers = normalizedDay == 1 ? 2 : normalizedDay == 2 ? 3 : 4;
            config.WaitingCapacity = WaitingCapacity;
            config.OverloadWaitingThreshold = 2;
            config.AllowSpawnWhileWaitingOverload = false;
            config.OverloadSlowdownMultiplier = 1.35f;
            config.MinSpawnInterval = normalizedDay == 1 ? 13f : normalizedDay == 2 ? 11f : 9f;
            config.MaxSpawnInterval = config.MinSpawnInterval + 4f;
            config.PressurePhase2Start = .15f;
            config.PressurePhase3Start = .46f;
            config.PressurePhase4Start = .76f;
            config.OpeningIntensity = .62f;
            config.NormalIntensity = 1f;
            config.BusyIntensity = 1.25f;
            config.FinalPeakIntensity = 1.45f;
            config.ReputationTrafficPerStar = .125f;
            config.MinimumTrafficMultiplier = .8f;
            config.MaximumTrafficMultiplier = 1.25f;
            config.TrafficWaves = CreateTrafficWaves(normalizedDay);
            config.LastAdmissionProgress = .82f;
            config.Rush = new RushConfig
            {
                StartProgress = .7f,
                DurationSeconds = 18f,
                DurationProgress = .15f,
                // The wave is the peak; do not multiply a second rush on top.
                IntensityMultiplier = 1f
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

        public static ShopReputationConfig CreateReputationConfig()
        {
            return new ShopReputationConfig
            {
                InitialStars = 3f,
                MinimumStars = 1f,
                MaximumStars = 5f,
                // One- and two-step orders top out below the 85 satisfaction
                // needed for Happy, so a plain completion must hold reputation.
                HappyDelta = .05f,
                NormalDelta = .03f,
                UnhappyDelta = -.06f,
                VeryUnhappyDelta = -.1f,
                UnservedAtCloseDelta = 0f,
                // Like an unfinished Overcooked dish at time-up: a customer
                // still being served at closing must cost less than a normal
                // completion earns.
                IncompleteAtCloseDelta = -.02f,
                AbandonedDelta = -.06f,
                MaxDailyGain = .1f,
                MaxDailyLoss = .2f
            };
        }

        private static System.Collections.Generic.List<TrafficWave> CreateTrafficWaves(int day)
        {
            if (day == 1)
            {
                return new System.Collections.Generic.List<TrafficWave>
                {
                    new TrafficWave(0f, .8f, TrafficWaveKind.Steady),
                    new TrafficWave(.22f, 1f, TrafficWaveKind.Arrivals),
                    new TrafficWave(.32f, 0f, TrafficWaveKind.Recovery),
                    new TrafficWave(.40f, 1f, TrafficWaveKind.Steady),
                    new TrafficWave(.60f, 1.15f, TrafficWaveKind.Rush),
                    new TrafficWave(.72f, 0f, TrafficWaveKind.Recovery),
                    new TrafficWave(.79f, .75f, TrafficWaveKind.Closing)
                };
            }
            if (day == 2)
            {
                return new System.Collections.Generic.List<TrafficWave>
                {
                    new TrafficWave(0f, 1f, TrafficWaveKind.Steady),
                    new TrafficWave(.20f, 1.1f, TrafficWaveKind.Arrivals),
                    new TrafficWave(.34f, 0f, TrafficWaveKind.Recovery),
                    new TrafficWave(.41f, 1f, TrafficWaveKind.Steady),
                    new TrafficWave(.58f, 1.2f, TrafficWaveKind.Rush),
                    new TrafficWave(.73f, 0f, TrafficWaveKind.Recovery),
                    new TrafficWave(.80f, .7f, TrafficWaveKind.Closing)
                };
            }
            return new System.Collections.Generic.List<TrafficWave>
            {
                new TrafficWave(0f, 1f, TrafficWaveKind.Steady),
                new TrafficWave(.18f, 1.15f, TrafficWaveKind.Arrivals),
                new TrafficWave(.34f, 0f, TrafficWaveKind.Recovery),
                new TrafficWave(.41f, 1f, TrafficWaveKind.Steady),
                new TrafficWave(.58f, 1.25f, TrafficWaveKind.Rush),
                new TrafficWave(.74f, 0f, TrafficWaveKind.Recovery),
                new TrafficWave(.80f, .75f, TrafficWaveKind.Closing)
            };
        }

        public static float ReputationTrafficMultiplier(float stars)
        {
            float value = 1f + (Math.Max(1f, Math.Min(5f, stars)) - 3f) * .125f;
            return Math.Max(.8f, Math.Min(1.25f, value));
        }

        public static string DescribeReputationChange(float before, float after)
        {
            return string.Format("口碑 {0:0.0} 星 → {1:0.0} 星 · 明日客流 {2:+0%;-0%;0%}",
                before, after, ReputationTrafficMultiplier(after) - 1f);
        }

        public static string DescribeReputationForOpening(float stars)
        {
            return string.Format("口碑 {0:0.0} 星 · 客流 {1:+0%;-0%;0%}",
                stars, ReputationTrafficMultiplier(stars) - 1f);
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
            if (day == 1 && index == 2) return "O003";

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
