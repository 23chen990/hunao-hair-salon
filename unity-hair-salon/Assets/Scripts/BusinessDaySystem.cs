using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum DayState
    {
        PreOpen,
        Starting = PreOpen,
        Business,
        ClosingGrace,
        Result,
        ClosedManagement,
        Shop = ClosedManagement
    }
    public enum DayPressurePhase { OpeningLight, Normal, Busy, FinalPeak }
    public enum ManagementInvestmentType { Equipment, Expansion, Upgrade, Marketing }

    [Serializable]
    public sealed class ManagementTransaction
    {
        public ManagementInvestmentType Type;
        public string ItemId;
        public int Amount;
    }

    [Serializable]
    public sealed class ShopReputationConfig
    {
        public float InitialStars = 3f;
        public float MinimumStars = 1f;
        public float MaximumStars = 5f;
        public float HappyDelta = .04f;
        public float NormalDelta;
        public float UnhappyDelta = -.05f;
        public float VeryUnhappyDelta = -.1f;
        public float UnservedAtCloseDelta = -.04f;
        public float IncompleteAtCloseDelta = -.08f;
        public float AbandonedDelta = -.05f;
        public float MaxDailyGain = .1f;
        public float MaxDailyLoss = .2f;
    }

    [Serializable]
    public sealed class RushConfig
    {
        public float StartProgress = .62f;
        public float DurationSeconds = 24f;
        public float DurationProgress;
        public float IntensityMultiplier = 1.65f;
    }

    [Serializable]
    public sealed class DayOrderWeight
    {
        public string OrderId;
        public float Weight;

        public DayOrderWeight(string orderId, float weight)
        {
            OrderId = orderId;
            Weight = weight;
        }
    }

    [Serializable]
    public sealed class DayConfig
    {
        // Mobile profiles opt into these fields explicitly. Legacy configs keep their
        // existing traffic and timing behavior when IsMobileProfile is false.
        public bool IsMobileProfile;
        public int MobileDayNumber = 1;
        public int TargetOrders;
        public int WaitingCapacity;
        public bool AllowSpawnWhileWaitingOverload;
        public float StartingDuration = 2f;
        public float BusinessDuration = 180f;
        public float ClosingGraceDuration = 30f;
        public float BaseSpawnIntensity = 1f;
        public float MinSpawnInterval = 6f;
        public float MaxSpawnInterval = 9f;
        public int MaxConcurrentCustomers = 6;
        public int OverloadWaitingThreshold = 3;
        public float OverloadSlowdownMultiplier = 2.4f;
        public float PressurePhase2Start = .2f;
        public float PressurePhase3Start = .55f;
        public float PressurePhase4Start = .8f;
        public float OpeningIntensity = .55f;
        public float NormalIntensity = 1f;
        public float BusyIntensity = 1.3f;
        public float FinalPeakIntensity = 1.65f;
        public float MinimumTrafficMultiplier = .9f;
        public float MaximumTrafficMultiplier = 1.1f;
        public float ReputationTrafficPerStar = .05f;
        public RushConfig Rush = new RushConfig();
        public List<DayOrderWeight> OrderWeights = new List<DayOrderWeight>
        {
            new DayOrderWeight("O001", 1.25f),
            new DayOrderWeight("O002", 1f),
            new DayOrderWeight("O003", 1f),
            new DayOrderWeight("O004", .9f),
            new DayOrderWeight("O005", .65f)
        };

        public static DayConfig CreateDefault() => new DayConfig();

        public string PickOrder(float normalizedRoll)
        {
            float total = 0f;
            for (int i = 0; i < OrderWeights.Count; i++)
                if (IsImplementedOrder(OrderWeights[i].OrderId)) total += Math.Max(0f, OrderWeights[i].Weight);
            if (total <= 0f) return "O001";
            float cursor = Math.Max(0f, Math.Min(.999999f, normalizedRoll)) * total;
            for (int i = 0; i < OrderWeights.Count; i++)
            {
                DayOrderWeight entry = OrderWeights[i];
                if (!IsImplementedOrder(entry.OrderId)) continue;
                cursor -= Math.Max(0f, entry.Weight);
                if (cursor < 0f) return entry.OrderId;
            }
            return "O005";
        }

        /// <summary>
        /// Selects an order from the profile's deterministic progression. Legacy
        /// configs retain their weighted picker through PickOrder(float).
        /// </summary>
        public string PickOrderForSpawn(int spawnIndex, float normalizedProgress)
        {
            if (!IsMobileProfile)
            {
                float roll = DeterministicRoll(spawnIndex, normalizedProgress);
                return PickOrder(roll);
            }

            return SalonMobileDayConfig.PickOrderForSpawn(this, spawnIndex, normalizedProgress);
        }

        private static float DeterministicRoll(int spawnIndex, float normalizedProgress)
        {
            unchecked
            {
                int seed = spawnIndex * 1103515245 + 12345 +
                    (int)(Math.Max(0f, Math.Min(1f, normalizedProgress)) * 1000f);
                seed &= 0x7fffffff;
                return (seed % 10000) / 10000f;
            }
        }

        private static bool IsImplementedOrder(string orderId)
        {
            return orderId == "O001" || orderId == "O002" || orderId == "O003" ||
                   orderId == "O004" || orderId == "O005";
        }
    }

    public readonly struct TrafficSnapshot
    {
        public readonly int ActiveCustomers;
        public readonly int WaitingCustomers;
        public readonly int OccupiedStations;
        public readonly int AngryCustomers;
        public readonly int ServiceStationCount;

        public TrafficSnapshot(int activeCustomers, int waitingCustomers, int occupiedStations,
            int angryCustomers, int serviceStationCount)
        {
            ActiveCustomers = Math.Max(0, activeCustomers);
            WaitingCustomers = Math.Max(0, waitingCustomers);
            OccupiedStations = Math.Max(0, occupiedStations);
            AngryCustomers = Math.Max(0, angryCustomers);
            ServiceStationCount = Math.Max(1, serviceStationCount);
        }
    }

    public readonly struct TrafficDecision
    {
        public readonly bool ShouldSpawn;
        public readonly float SpawnInterval;
        public readonly DayPressurePhase Phase;
        public readonly bool IsRushActive;
        public readonly bool IsOverloaded;
        public readonly float ReputationMultiplier;

        public TrafficDecision(bool shouldSpawn, float spawnInterval, DayPressurePhase phase,
            bool isRushActive, bool isOverloaded, float reputationMultiplier = 1f)
        {
            ShouldSpawn = shouldSpawn;
            SpawnInterval = spawnInterval;
            Phase = phase;
            IsRushActive = isRushActive;
            IsOverloaded = isOverloaded;
            ReputationMultiplier = reputationMultiplier;
        }
    }

    public sealed class CustomerTrafficDirector
    {
        private readonly DayConfig _config;

        public CustomerTrafficDirector(DayConfig config)
        {
            _config = config ?? DayConfig.CreateDefault();
        }

        public DayPressurePhase GetPhase(float progress)
        {
            float value = Clamp01(progress);
            if (value < _config.PressurePhase2Start) return DayPressurePhase.OpeningLight;
            if (value < _config.PressurePhase3Start) return DayPressurePhase.Normal;
            if (value < _config.PressurePhase4Start) return DayPressurePhase.Busy;
            return DayPressurePhase.FinalPeak;
        }

        public TrafficDecision Evaluate(float progress, TrafficSnapshot snapshot, float intervalRoll = .5f,
            float reputationStars = 3f)
        {
            DayPressurePhase phase = GetPhase(progress);
            float phaseIntensity = phase == DayPressurePhase.OpeningLight ? _config.OpeningIntensity :
                phase == DayPressurePhase.Normal ? _config.NormalIntensity :
                phase == DayPressurePhase.Busy ? _config.BusyIntensity : _config.FinalPeakIntensity;
            float durationProgress = _config.Rush.DurationProgress > 0f ? _config.Rush.DurationProgress :
                _config.Rush.DurationSeconds / Math.Max(.01f, _config.BusinessDuration);
            bool rush = progress >= _config.Rush.StartProgress &&
                        progress < _config.Rush.StartProgress + durationProgress;
            bool stationSaturated = snapshot.OccupiedStations >= snapshot.ServiceStationCount;
            bool waitingOverload = snapshot.WaitingCustomers >= Math.Max(1, _config.OverloadWaitingThreshold);
            bool waitingAtCapacity = _config.WaitingCapacity > 0 &&
                                     snapshot.WaitingCustomers >= _config.WaitingCapacity;
            bool angryOverload = snapshot.AngryCustomers >= 2;
            bool overloaded = waitingOverload || (stationSaturated && snapshot.WaitingCustomers >= 2) || angryOverload;
            bool underHardCap = snapshot.ActiveCustomers < Math.Max(1, _config.MaxConcurrentCustomers);
            float reputationMultiplier = 1f + (Math.Max(1f, Math.Min(5f, reputationStars)) - 3f) *
                _config.ReputationTrafficPerStar;
            reputationMultiplier = Math.Max(_config.MinimumTrafficMultiplier,
                Math.Min(_config.MaximumTrafficMultiplier, reputationMultiplier));
            float intensity = Math.Max(.05f, _config.BaseSpawnIntensity) * Math.Max(.05f, phaseIntensity) *
                              Math.Max(.05f, reputationMultiplier);
            if (rush) intensity *= Math.Max(1f, _config.Rush.IntensityMultiplier);
            float min = Math.Max(.05f, _config.MinSpawnInterval);
            float max = Math.Max(min, _config.MaxSpawnInterval);
            float baseInterval = min + (max - min) * Clamp01(intervalRoll);
            float interval = baseInterval / intensity;
            if (overloaded) interval *= Math.Max(1f, _config.OverloadSlowdownMultiplier);
            bool waitingGate = waitingAtCapacity || (waitingOverload && !_config.AllowSpawnWhileWaitingOverload);
            return new TrafficDecision(underHardCap && !waitingGate, interval, phase, rush, overloaded,
                reputationMultiplier);
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    [Serializable]
    public sealed class DayStats
    {
        private readonly HashSet<int> _collectedPayments = new HashSet<int>();
        private readonly HashSet<int> _servedCustomers = new HashSet<int>();
        private readonly HashSet<int> _abandonedCustomers = new HashSet<int>();
        private readonly Dictionary<int, int> _wrongStationByCustomer = new Dictionary<int, int>();
        private readonly Dictionary<int, AccidentSeverity> _accidentByCustomer = new Dictionary<int, AccidentSeverity>();

        public int DayNumber;
        public int SpawnedCustomers;
        public int CompletedOrders;
        public int ServedCustomers => CompletedOrders;
        public int HappyCustomers;
        public int NormalCustomers;
        public int UnhappyCustomers;
        public int VeryUnhappyCustomers;
        public int AbandonedBeforeService;
        public int UnservedAtClose;
        public int IncompleteAtClose;
        public int OrderIncome;
        public int TipIncome;
        public int OperatingRewardIncome;
        public int CompensationExpense;
        public int OtherOperatingExpense;
        public int OperatingNetIncome => OrderIncome + TipIncome + OperatingRewardIncome -
                                         CompensationExpense - OtherOperatingExpense;
        public float ReputationBefore;
        public float ReputationDelta;
        public float ReputationAfter;
        internal bool ReputationApplied;
        public int WrongStationCount;
        public int MinorMistakeCount;
        public int ModerateMistakeCount;
        public int MajorMistakeCount;

        public DayStats(int dayNumber)
        {
            DayNumber = Math.Max(1, dayNumber);
        }

        public void RecordSpawn(CustomerModel customer)
        {
            if (customer != null) SpawnedCustomers++;
        }

        // Compatibility hook: creating a world pickup never recognizes revenue.
        public void RecordPaymentGenerated(PaymentDropModel drop) { }

        public void RecordPaymentCollected(PaymentDropModel drop)
        {
            if (drop == null || !_collectedPayments.Add(drop.Id)) return;
            OrderIncome += Math.Max(0, drop.OrderIncomeComponent);
            TipIncome += Math.Max(0, drop.TipIncomeComponent);
        }

        public void RecordOperatingReward(int amount) => OperatingRewardIncome += Math.Max(0, amount);
        public void RecordCompensation(int amount) => CompensationExpense += Math.Max(0, amount);
        public void RecordOtherOperatingExpense(int amount) => OtherOperatingExpense += Math.Max(0, amount);
        public void FinalizeOperatingResult() { }

        public void RecordCustomerSnapshot(CustomerModel customer)
        {
            if (customer == null) return;
            int previousWrong = _wrongStationByCustomer.TryGetValue(customer.Id, out int wrong) ? wrong : 0;
            if (customer.WrongStationCount > previousWrong)
                WrongStationCount += customer.WrongStationCount - previousWrong;
            _wrongStationByCustomer[customer.Id] = customer.WrongStationCount;

            AccidentSeverity previousSeverity = _accidentByCustomer.TryGetValue(customer.Id, out AccidentSeverity severity)
                ? severity : AccidentSeverity.None;
            if (customer.AccidentSeverity > previousSeverity)
            {
                if (customer.AccidentSeverity == AccidentSeverity.Minor) MinorMistakeCount++;
                else if (customer.AccidentSeverity == AccidentSeverity.Moderate) ModerateMistakeCount++;
                else if (customer.AccidentSeverity == AccidentSeverity.Major) MajorMistakeCount++;
            }
            _accidentByCustomer[customer.Id] = customer.AccidentSeverity;

            if (customer.State == CustomerState.Finished && customer.ServiceResult != CustomerServiceResult.Failed &&
                _servedCustomers.Add(customer.Id))
            {
                CompletedOrders++;
                if (customer.ServiceResult == CustomerServiceResult.HappyCompletion) HappyCustomers++;
                else if (customer.ServiceResult == CustomerServiceResult.NormalCompletion) NormalCustomers++;
                else if (customer.ServiceResult == CustomerServiceResult.UnhappyCompletion) UnhappyCustomers++;
                else if (customer.ServiceResult == CustomerServiceResult.SevereUnhappyCompletion) VeryUnhappyCustomers++;
            }
            if (customer.State == CustomerState.Leaving && customer.ServiceResult == CustomerServiceResult.Failed &&
                !customer.HasServiceEngaged && _abandonedCustomers.Add(customer.Id))
                AbandonedBeforeService++;
        }

        public void RecordUnservedAtClose() => UnservedAtClose++;
        public void RecordIncompleteAtClose() => IncompleteAtClose++;

        public override string ToString()
        {
            return $"Day {DayNumber}: spawned={SpawnedCustomers}, completed={CompletedOrders}, happy={HappyCustomers}, " +
                   $"normal={NormalCustomers}, unhappy={UnhappyCustomers}, veryUnhappy={VeryUnhappyCustomers}, " +
                   $"abandoned={AbandonedBeforeService}, unservedAtClose={UnservedAtClose}, " +
                   $"incompleteAtClose={IncompleteAtClose}, orderIncome={OrderIncome}, tipIncome={TipIncome}, " +
                   $"operatingNet={OperatingNetIncome}, reputation={ReputationBefore:0.0}->{ReputationAfter:0.0}, " +
                   $"wrongStation={WrongStationCount}, mistakes=" +
                   $"{MinorMistakeCount}/{ModerateMistakeCount}/{MajorMistakeCount}";
        }
    }

    public sealed class ShopReputationModel
    {
        private readonly ShopReputationConfig _config;
        public float CurrentStars { get; private set; }

        public ShopReputationModel(ShopReputationConfig config = null)
        {
            _config = config ?? new ShopReputationConfig();
            CurrentStars = Clamp(_config.InitialStars, _config.MinimumStars, _config.MaximumStars);
        }

        public void Restore(float stars)
        {
            CurrentStars = Clamp(stars, _config.MinimumStars, _config.MaximumStars);
        }

        public void ApplyDayResult(DayStats stats)
        {
            if (stats == null || stats.ReputationApplied) return;
            float raw = stats.HappyCustomers * _config.HappyDelta +
                        stats.NormalCustomers * _config.NormalDelta +
                        stats.UnhappyCustomers * _config.UnhappyDelta +
                        stats.VeryUnhappyCustomers * _config.VeryUnhappyDelta +
                        stats.UnservedAtClose * _config.UnservedAtCloseDelta +
                        stats.IncompleteAtClose * _config.IncompleteAtCloseDelta +
                        stats.AbandonedBeforeService * _config.AbandonedDelta;
            float delta = Math.Max(-Math.Max(0f, _config.MaxDailyLoss),
                Math.Min(Math.Max(0f, _config.MaxDailyGain), raw));
            stats.ReputationBefore = CurrentStars;
            CurrentStars = Clamp(CurrentStars + delta, _config.MinimumStars, _config.MaximumStars);
            stats.ReputationAfter = CurrentStars;
            stats.ReputationDelta = CurrentStars - stats.ReputationBefore;
            stats.ReputationApplied = true;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            float min = Math.Min(minimum, maximum);
            float max = Math.Max(minimum, maximum);
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public sealed class BusinessDayController
    {
        private readonly CustomerTrafficDirector _trafficDirector;
        private int _unfinishedOrders;
        public DayConfig Config { get; }
        public DayState State { get; private set; } = DayState.PreOpen;
        public int DayNumber { get; private set; } = 1;
        public float BusinessRemainingTime { get; private set; }
        public float ClosingGraceRemainingTime { get; private set; }
        public float BusinessProgress => Config.BusinessDuration <= 0f ? 1f :
            Math.Max(0f, Math.Min(1f, 1f - BusinessRemainingTime / Config.BusinessDuration));
        public DayPressurePhase CurrentPressurePhase => _trafficDirector.GetPhase(BusinessProgress);
        public bool IsPaused { get; private set; }
        private bool MobileGoalReached => Config.IsMobileProfile && Config.TargetOrders > 0 &&
                                          Stats.CompletedOrders >= Config.TargetOrders;
        public bool CanSpawnCustomers => State == DayState.Business && BusinessRemainingTime > 0f &&
                                         !IsPaused && (!Config.IsMobileProfile || Config.TargetOrders <= 0 ||
                                         Stats.CompletedOrders + _unfinishedOrders < Config.TargetOrders);
        public DayStats Stats { get; private set; }
        public ShopReputationModel Reputation { get; }
        public DayEvaluation CurrentDayEvaluation => EvaluateDay();
        public bool CanRetryDay => (State == DayState.Result || State == DayState.ClosedManagement) &&
                                    EvaluateDay().CanRetry;
        public event Action<DayState> StateChanged;

        public BusinessDayController(DayConfig config, ShopReputationConfig reputationConfig = null)
        {
            Config = config ?? DayConfig.CreateDefault();
            _trafficDirector = new CustomerTrafficDirector(Config);
            Reputation = new ShopReputationModel(reputationConfig);
            Stats = new DayStats(1);
        }

        public void PrepareDay(int dayNumber)
        {
            DayNumber = Math.Max(1, dayNumber);
            if (Config.IsMobileProfile)
                SalonMobileDayConfig.ApplyForDay(Config, DayNumber);
            Stats = new DayStats(DayNumber);
            _unfinishedOrders = 0;
            BusinessRemainingTime = Math.Max(0f, Config.BusinessDuration);
            ClosingGraceRemainingTime = Math.Max(0f, Config.ClosingGraceDuration);
            IsPaused = false;
            SetState(DayState.PreOpen, true);
        }

        public void StartDay(int dayNumber) => PrepareDay(dayNumber);
        public void StartBusiness()
        {
            if (State == DayState.PreOpen) SetState(DayState.Business);
        }

        public void Tick(float dt, int activeCustomerCount)
        {
            TickWithAdmissionCount(dt, activeCustomerCount, activeCustomerCount, 0);
        }

        public void Tick(float dt, int activeCustomerCount, int pendingSettlementCount)
        {
            TickWithAdmissionCount(dt, activeCustomerCount, activeCustomerCount, pendingSettlementCount);
        }

        /// <summary>
        /// Advances the day with separate lifecycle and admission counts.
        /// Finished/Leaving customers still keep the day alive until their
        /// visible exit route completes, but they are already completed (or
        /// already failed) and must not consume another unfinished-order slot.
        /// </summary>
        public void TickWithAdmissionCount(float dt, int activeUntilExitCount,
            int unfinishedOrderCount, int pendingSettlementCount = 0)
        {
            _unfinishedOrders = Math.Max(0, unfinishedOrderCount);
            if (IsPaused || State == DayState.PreOpen || State == DayState.Result ||
                State == DayState.ClosedManagement) return;
            float step = Math.Max(0f, dt);
            int pending = Math.Max(0, pendingSettlementCount);
            if (MobileGoalReached && activeUntilExitCount <= 0 && pending <= 0)
            {
                SetState(DayState.Result);
                return;
            }
            if (State == DayState.Business)
            {
                BusinessRemainingTime = Math.Max(0f, BusinessRemainingTime - step);
                if (BusinessRemainingTime <= 0f)
                {
                    if (activeUntilExitCount <= 0 && pending <= 0) SetState(DayState.Result);
                    else SetState(DayState.ClosingGrace);
                }
                return;
            }
            ClosingGraceRemainingTime = Math.Max(0f, ClosingGraceRemainingTime - step);
            if ((activeUntilExitCount <= 0 && pending <= 0) || ClosingGraceRemainingTime <= 0f)
                SetState(DayState.Result);
        }

        public void SetPaused(bool paused) => IsPaused = paused;
        public void ForceResult() => SetState(DayState.Result);
        public void FinalizeDayReputation() => Reputation.ApplyDayResult(Stats);
        public DayEvaluation EvaluateDay()
        {
            bool ended = State == DayState.Result || State == DayState.ClosedManagement;
            return SalonMobileDayConfig.Evaluate(Config, Stats == null ? 0 : Stats.CompletedOrders, ended);
        }

        public void RestoreReputation(float stars) => Reputation.Restore(stars);

        /// <summary>
        /// Restores a persisted post-day management state without applying the
        /// day's result again. A caller that already prepared the same day keeps
        /// that daily state; other states are safely re-prepared first.
        /// </summary>
        public bool RestoreClosedManagement(int dayNumber, float reputationStars)
        {
            int normalizedDay = Math.Max(1, dayNumber);
            if (State != DayState.PreOpen || DayNumber != normalizedDay)
                PrepareDay(normalizedDay);
            else if (Config.IsMobileProfile)
                SalonMobileDayConfig.ApplyForDay(Config, normalizedDay);

            RestoreReputation(reputationStars);
            SetState(DayState.ClosedManagement);
            return true;
        }

        public bool PrepareRetryDay()
        {
            if (!CanRetryDay) return false;
            PrepareDay(DayNumber);
            return true;
        }

        public void OpenClosedManagement()
        {
            if (State == DayState.Result) SetState(DayState.ClosedManagement);
        }
        public void OpenShop() => OpenClosedManagement();

        public void PrepareNextDay()
        {
            if (State != DayState.ClosedManagement) return;
            PrepareDay(DayNumber + 1);
        }
        public void BeginNextDay() => PrepareNextDay();

        private void SetState(DayState state, bool force = false)
        {
            if (!force && State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
