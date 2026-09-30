using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum SalonUnlockId
    {
        HaircutChair,
        SupplyRack,
        WaitingSeats,
        WashAnnex,
        BlowStand,
        ExtraSeats,
        BlowDryer,
        WashStation
    }

    public sealed class SalonUnlockDefinition
    {
        public SalonUnlockId Id { get; }
        public string PadId { get; }
        public string DisplayName { get; }
        public int OpenDay { get; }
        public int Cost { get; }
        public float FillSeconds { get; }
        public SalonUnlockId? Requires { get; }

        public float PaymentPerSecond => Cost / FillSeconds;

        public SalonUnlockDefinition(SalonUnlockId id, string padId, string displayName, int openDay,
            int cost, float fillSeconds, SalonUnlockId? requires = null)
        {
            Id = id;
            PadId = padId;
            DisplayName = displayName;
            OpenDay = openDay;
            Cost = cost;
            FillSeconds = fillSeconds;
            Requires = requires;
        }
    }

    /// <summary>Money-driven equipment sequence; one unfinished construction pad is visible.</summary>
    public static class SalonUnlockRoute
    {
        public const int MaxVisibleUnfinished = 1;
        public const float SeatedWaitingPatienceMultiplier = .75f;
        public const int BaseWaitingCapacity = 4;
        public const int ExtraSeatCount = 2;
        public const int BaseMaxConcurrentCustomers = 6;

        private static readonly SalonUnlockDefinition[] RouteDefinitions =
        {
            new SalonUnlockDefinition(SalonUnlockId.BlowDryer, "expansion-pad-blow-dryer", "吹风机", 1, 600, 3f),
            new SalonUnlockDefinition(SalonUnlockId.WashStation, "expansion-pad-wash-station", "洗头台", 1, 1200, 5f, SalonUnlockId.BlowDryer),
            new SalonUnlockDefinition(SalonUnlockId.HaircutChair, "expansion-pad-haircut-2", "第二理发椅", 1, 2000, 6f, SalonUnlockId.WashStation),
            new SalonUnlockDefinition(SalonUnlockId.WaitingSeats, "expansion-pad-waiting-seats", "等候座椅", 1, 2400, 6f, SalonUnlockId.HaircutChair),
            new SalonUnlockDefinition(SalonUnlockId.BlowStand, "expansion-pad-blow-stand", "吹风支架", 1, 3000, 7f, SalonUnlockId.WaitingSeats),
            new SalonUnlockDefinition(SalonUnlockId.WashAnnex, "expansion-pad-wash-annex", "第二洗发区", 1, 3600, 8f, SalonUnlockId.BlowStand),
            new SalonUnlockDefinition(SalonUnlockId.ExtraSeats, "expansion-pad-extra-seats", "加座长凳", 1, 4200, 8f, SalonUnlockId.WashAnnex)
        };

        public static IReadOnlyList<SalonUnlockDefinition> Route => RouteDefinitions;

        public static SalonUnlockDefinition Get(SalonUnlockId id)
        {
            // Retain the old paid rack record for save migration; it is now introduced for free.
            if (id == SalonUnlockId.SupplyRack)
                return new SalonUnlockDefinition(id, "expansion-pad-wash-rack", "补货架", 1, 180, 3f);
            foreach (SalonUnlockDefinition definition in RouteDefinitions)
                if (definition.Id == id) return definition;
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown unlock.");
        }

        public static int CostOf(SalonUnlockId id) => Get(id).Cost;

        /// <summary>Show the first unbuilt equipment; preserve later partial payments in the save.</summary>
        public static bool IsVisible(SalonUnlockId id, int day,
            Func<SalonUnlockId, bool> unlocked, Func<SalonUnlockId, int> paid)
        {
            return VisibleOn(day, unlocked, paid).Contains(id);
        }

        public static List<SalonUnlockId> VisibleOn(int day,
            Func<SalonUnlockId, bool> unlocked, Func<SalonUnlockId, int> paid)
        {
            if (unlocked == null) throw new ArgumentNullException(nameof(unlocked));
            if (paid == null) throw new ArgumentNullException(nameof(paid));
            var visible = new List<SalonUnlockId>();
            foreach (SalonUnlockDefinition definition in RouteDefinitions)
            {
                if (visible.Count >= MaxVisibleUnfinished) break;
                if (!unlocked(definition.Id))
                    visible.Add(definition.Id);
            }
            return visible;
        }

        /// <summary>Construction introduced by this day's level, if any.</summary>
        public static SalonUnlockDefinition FirstOpeningOn(int day)
        {
            foreach (SalonUnlockDefinition definition in RouteDefinitions)
                if (definition.OpenDay == day) return definition;
            return null;
        }

        public static bool IsRouteComplete(Func<SalonUnlockId, bool> unlocked)
        {
            if (unlocked == null) throw new ArgumentNullException(nameof(unlocked));
            foreach (SalonUnlockDefinition definition in RouteDefinitions)
                if (!unlocked(definition.Id)) return false;
            return true;
        }

        public static int WaitingCapacity(bool extraSeats)
            => BaseWaitingCapacity + (extraSeats ? ExtraSeatCount : 0);

        public static int MaxConcurrentCustomers(bool extraSeats)
            => BaseMaxConcurrentCustomers + (extraSeats ? ExtraSeatCount : 0);

        public static string DescribeOpening(SalonUnlockDefinition definition)
            => definition == null ? string.Empty
                : "下个解锁：" + definition.DisplayName + " · " + definition.Cost + " 金币";

        public static string TeachingOrderAfter(SalonUnlockId id)
            => id == SalonUnlockId.BlowDryer ? "O004" : id == SalonUnlockId.WashStation ? "O003" : string.Empty;

        public static string LearningPurpose(SalonUnlockId id)
        {
            switch (id)
            {
                case SalonUnlockId.BlowDryer: return "剪发熟了，练习剪吹服务";
                case SalonUnlockId.WashStation: return "学会洗头，再接组合服务";
                case SalonUnlockId.HaircutChair: return "多一把椅子，轮换服务";
                case SalonUnlockId.WaitingSeats: return "让等候更从容";
                case SalonUnlockId.BlowStand: return "熟悉吹发后，减少看守";
                case SalonUnlockId.WashAnnex: return "洗头忙起来，再加工位";
                case SalonUnlockId.ExtraSeats: return "店铺忙起来，再加等候位置";
                default: return string.Empty;
            }
        }

        private static bool IsEligible(SalonUnlockDefinition definition, int day,
            Func<SalonUnlockId, bool> unlocked)
        {
            if (unlocked(definition.Id) || Math.Max(1, day) < definition.OpenDay) return false;
            return !definition.Requires.HasValue || unlocked(definition.Requires.Value);
        }
    }
}
