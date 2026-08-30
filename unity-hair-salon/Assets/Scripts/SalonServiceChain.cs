using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum AccidentSeverity { None, Minor, Moderate, Major }
    public enum ServiceStepPhase { Active, BackgroundRunning, Ready, Late, Complete }
    public enum WashStage
    {
        Dry,
        Wetting,
        Wet,
        ShampooApplied,
        Shampooing,
        Foamy,
        Rinsing,
        Rinsed,
        Toweled,

        // Legacy names remain source-compatible with older acceptance fixtures.
        AwaitingShampoo = Dry,
        FoamWait = Foamy,
        ReadyToRinse = Foamy,
        ReadyToWrap = Rinsed,
        WrappingTowel = Rinsed,
        TowelWrapped = Toweled
    }
    public enum WashAction { Shower, Shampoo, WrapTowel, Wet, Rinse }
    public enum ServiceActionResult
    {
        Progressed,
        QuickActionCompleted,
        ExtraService,
        Invalid,
        Failed
    }
    public enum BlowStage { AwaitingStart, ManualHolding, AutoRunning, Early, Good, Minor, Moderate, SafetyStopped, Complete }
    public enum BlowResult
    {
        None,
        Undone,
        Good,
        Minor,
        Moderate,
        EarlyUndone = Undone,
        Late = Minor,
        SevereLate = Moderate
    }
    public enum ActiveServiceAction
    {
        None, Shower, Shampoo, WrapTowel, RemoveTowel, ManualBlow, AutoBlowStart, Wet, Rinse,
        ApplyDye, ApplyPerm
    }
    public enum ServiceProcessStage
    {
        None, DyeApply, DyeProcessing, DyeReadyForCleanup, DyeFailed,
        PermApply, PermProcessing, Complete
    }

    [Serializable]
    public sealed class CustomerExperienceProfile
    {
        public float InitialPatience = 100f;
        public float MaxPatience = 100f;
        public float InitialSatisfaction = 70f;
        public float WaitingToSatisfactionRate = .12f;
        public float WrongStationPenalty = 3f;
        public float PositiveServiceGain = 6f;
        public float MinorPenalty = 4f;
        public float ModeratePenalty = 10f;
        public float HappyThreshold = 85f;
        public float UnhappyThreshold = 60f;
        public float SevereUnhappyThreshold = 35f;
        public float RecoveryCapMinor = 100f;
        public float RecoveryCapModerate = 84f;
        public float RecoveryCapMajor = 59f;
        public float PatienceDecayMultiplier = 1f;
        public float ServiceDelayPatienceMultiplier = .55f;
        public float WrongStationSensitivity = 1f;
        public float MistakeSensitivity = 1f;
        public float PositiveServiceMultiplier = 1f;
        public float TipMultiplier = 1f;
        public float ApologyResponseMultiplier = 1f;
        public float DiscountResponseMultiplier = 1f;
        public float CompensationResponseMultiplier = 1f;
    }

    [Serializable]
    public sealed class EquipmentProductModel
    {
        public string Id = "AUTO_BLOW_STAND";
        public string DisplayName = "自动吹风支架";
        public int Price = 1200;
        public bool Visible = true;
        public bool Purchased;
        public string LockReason = "完成首日营业后开放";
        public bool CanPurchase;
    }

    [Serializable]
    public sealed class SalonServiceConfig
    {
        public float WashServiceDuration = 5f;
        public float HaircutServiceDuration = 4f;
        public float BlowDryServiceDuration = 3f;
        public float DyeApplyDuration = 3f;
        public float DyeProcessingDuration = 8f;
        public float DyeCleanupGraceDuration = 5f;
        public float PermApplyDuration = 4f;
        public float PermProcessingDuration = 10f;
        public float ShampooDuration = 2f;
        public float FoamDuration = 4f;
        public float FoamOptimalStart = 4f;
        public float FoamMinorLateThreshold = 7f;
        public float FoamModerateLateThreshold = 12f;
        public float RinseDuration = 2f;
        public float OverdueRinseDuration = 2.75f;
        public float WrapTowelDuration = .65f;
        public float RemoveTowelDuration = .55f;
        public float BlowIdealStart = 5f;
        public float BlowIdealEnd = 7f;
        public float BlowLateEnd = 10f;
        public float ManualBlowUndoneEnd = 2f;
        public float ManualBlowGoodStart = 3f;
        public float ManualBlowGoodEnd = 5f;
        public float ManualBlowMinorEnd = 7f;
        public float AutoBlowStartDuration = .75f;
        public float AutoBlowSafetyStopTime = 9f;
        public float ReadyDelayGrace = 2f;
        public float LateSatisfactionPenalty = 8f;
        public float SevereLateSatisfactionPenalty = 24f;
    }

    public static class SalonOrderCatalog
    {
        private static readonly Dictionary<string, IReadOnlyList<ServiceType>> Orders =
            new Dictionary<string, IReadOnlyList<ServiceType>>
            {
                { "O001", Array.AsReadOnly(new[] { ServiceType.Cut }) },
                { "O002", Array.AsReadOnly(new[] { ServiceType.Wash, ServiceType.Dry }) },
                { "O003", Array.AsReadOnly(new[] { ServiceType.Wash, ServiceType.Cut }) },
                { "O004", Array.AsReadOnly(new[] { ServiceType.Cut, ServiceType.Dry }) },
                { "O005", Array.AsReadOnly(new[] { ServiceType.Wash, ServiceType.Cut, ServiceType.Dry }) }
            };

        public static IReadOnlyDictionary<string, IReadOnlyList<ServiceType>> All => Orders;

        public static IReadOnlyList<ServiceType> Get(string orderId)
        {
            if (string.IsNullOrEmpty(orderId) || !Orders.TryGetValue(orderId, out var order))
                throw new ArgumentException("Only orders O001 through O005 are available.", nameof(orderId));
            return order;
        }
    }
}
