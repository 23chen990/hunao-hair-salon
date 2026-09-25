using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon
{
    /// <summary>
    /// Serializable progress that survives a scene reload or a WebGL page refresh.
    /// This deliberately contains only durable business progress; live customers,
    /// timers and scene objects are restored by the owning gameplay layer.
    /// </summary>
    [Serializable]
    public sealed class SalonProgressData
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxDayNumber = 100000;
        public const int DefaultShopSatisfaction = 90;
        public const int SupplyRackExpansionCost = 180;
        public const int HaircutExpansionCost = 180;

        public int SchemaVersion = CurrentSchemaVersion;
        public int DayNumber = 1;
        public int Balance;
        public bool AutoBlowPurchased;
        // Optional field added without a schema bump: older JSON simply reads
        // the missing value as false, while the pad purchase survives reloads.
        public bool SupplyRackExpansionPurchased;
        public int SupplyRackExpansionPaid;
        public bool HaircutExpansionPurchased;
        public int HaircutExpansionPaid;
        public bool FirstDayComplete;
        public bool DaySettled;
        public int ShopSatisfaction = DefaultShopSatisfaction;
        public float ReputationStars = 3f;
        public bool TutorialCompleted;
        public List<int> CompletedDays = new List<int>();
        public List<int> BestCompletedOrders = new List<int>();

        public static SalonProgressData CreateDefault()
        {
            return new SalonProgressData();
        }

        /// <summary>
        /// Checks the values that can be restored into the live game model.
        /// A failed check leaves the source object untouched.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (SchemaVersion != CurrentSchemaVersion)
            {
                error = SchemaVersion > CurrentSchemaVersion
                    ? "Save schema is newer than this build."
                    : "Save schema is unsupported.";
                return false;
            }

            if (DayNumber < 1 || DayNumber > MaxDayNumber)
            {
                error = "DayNumber is outside the supported range.";
                return false;
            }

            if (Balance < 0)
            {
                error = "Balance cannot be negative.";
                return false;
            }

            if (SupplyRackExpansionPaid < 0 ||
                SupplyRackExpansionPaid > SupplyRackExpansionCost)
            {
                error = "SupplyRackExpansionPaid is outside the supported range.";
                return false;
            }

            if (SupplyRackExpansionPurchased &&
                SupplyRackExpansionPaid != SupplyRackExpansionCost)
            {
                error = "A purchased supply rack must have the full construction payment.";
                return false;
            }

            if (HaircutExpansionPaid < 0 || HaircutExpansionPaid > HaircutExpansionCost)
            {
                error = "HaircutExpansionPaid is outside the supported range.";
                return false;
            }

            if (HaircutExpansionPurchased && HaircutExpansionPaid != HaircutExpansionCost)
            {
                error = "A purchased haircut station must have the full construction payment.";
                return false;
            }

            if (ShopSatisfaction < 0 || ShopSatisfaction > 100)
            {
                error = "ShopSatisfaction must be in the range 0 to 100.";
                return false;
            }

            if (float.IsNaN(ReputationStars) || float.IsInfinity(ReputationStars) ||
                ReputationStars < 1f || ReputationStars > 5f)
            {
                error = "ReputationStars must be a finite value from 1 to 5.";
                return false;
            }

            // JsonUtility leaves omitted list fields null. Treat those optional
            // history fields as empty so old, smaller saves remain readable.
            if (CompletedDays != null && BestCompletedOrders != null &&
                CompletedDays.Count != BestCompletedOrders.Count)
            {
                error = "CompletedDays and BestCompletedOrders must have matching lengths.";
                return false;
            }

            if (CompletedDays != null)
            {
                int previousDay = 0;
                for (int i = 0; i < CompletedDays.Count; i++)
                {
                    int completedDay = CompletedDays[i];
                    if (completedDay < 1 || completedDay > DayNumber || completedDay <= previousDay)
                    {
                        error = "CompletedDays must be unique, ascending and within DayNumber.";
                        return false;
                    }
                    previousDay = completedDay;
                }
            }

            if (BestCompletedOrders != null)
            {
                for (int i = 0; i < BestCompletedOrders.Count; i++)
                {
                    if (BestCompletedOrders[i] < 0)
                    {
                        error = "BestCompletedOrders cannot contain negative values.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Makes a plain-data copy and supplies empty history lists for JSON
        /// documents that predate those optional fields.
        /// </summary>
        public SalonProgressData Clone()
        {
            return new SalonProgressData
            {
                SchemaVersion = SchemaVersion,
                DayNumber = DayNumber,
                Balance = Balance,
                AutoBlowPurchased = AutoBlowPurchased,
                SupplyRackExpansionPurchased = SupplyRackExpansionPurchased,
                SupplyRackExpansionPaid = SupplyRackExpansionPaid,
                HaircutExpansionPurchased = HaircutExpansionPurchased,
                HaircutExpansionPaid = HaircutExpansionPaid,
                FirstDayComplete = FirstDayComplete,
                DaySettled = DaySettled,
                ShopSatisfaction = ShopSatisfaction,
                ReputationStars = ReputationStars,
                TutorialCompleted = TutorialCompleted,
                CompletedDays = CompletedDays == null
                    ? new List<int>() : new List<int>(CompletedDays),
                BestCompletedOrders = BestCompletedOrders == null
                    ? new List<int>() : new List<int>(BestCompletedOrders)
            };
        }
    }

    /// <summary>
    /// Small key-value boundary around PlayerPrefs. Tests can inject an in-memory
    /// implementation without touching the real device or browser storage.
    /// </summary>
    public interface ISalonProgressStorage
    {
        bool HasKey(string key);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsSalonProgressStorage : ISalonProgressStorage
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public string GetString(string key, string defaultValue)
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Repository used by the owning gameplay layer at safe business boundaries.
    /// The two keys form a current slot plus last-known-good backup. PlayerPrefs
    /// is available on WebGL, where it is backed by browser local storage.
    /// </summary>
    public interface ISalonProgressRepository
    {
        SalonProgressData Load();
        bool Save(SalonProgressData data);
        bool Delete();
    }

    public sealed class PlayerPrefsSalonProgressRepository : ISalonProgressRepository
    {
        public const string PrimaryKey = "HairSalon.Progress.Primary.v1";
        public const string BackupKey = "HairSalon.Progress.Backup.v1";

        private readonly ISalonProgressStorage _storage;

        public PlayerPrefsSalonProgressRepository()
            : this(new PlayerPrefsSalonProgressStorage())
        {
        }

        public PlayerPrefsSalonProgressRepository(ISalonProgressStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>
        /// Returns the newest valid current save, or the last valid backup when
        /// the primary slot is absent/corrupt. A future schema is never applied.
        /// </summary>
        public SalonProgressData Load()
        {
            SlotRead primary = ReadSlot(PrimaryKey);
            if (primary.IsFuture) return null;
            if (primary.Data != null) return primary.Data;

            SlotRead backup = ReadSlot(BackupKey);
            if (backup.IsFuture) return null;
            return backup.Data;
        }

        /// <summary>
        /// Writes a validated snapshot. The previous valid primary is copied to
        /// backup before replacing primary, so a torn or corrupt primary remains
        /// recoverable. Existing future-schema data is preserved and rejects the
        /// write rather than being silently downgraded.
        /// </summary>
        public bool Save(SalonProgressData data)
        {
            if (data == null) return false;

            SalonProgressData snapshot = data.Clone();
            if (!snapshot.TryValidate(out _)) return false;

            SlotRead primary = ReadSlot(PrimaryKey);
            SlotRead backup = ReadSlot(BackupKey);
            if (primary.IsFuture || backup.IsFuture) return false;

            string json;
            try
            {
                json = JsonUtility.ToJson(snapshot);
                if (string.IsNullOrEmpty(json)) return false;

                // Preserve the exact last-good primary as the backup. When the
                // primary is corrupt/missing, leave an existing backup intact.
                if (!string.IsNullOrEmpty(primary.Raw))
                    _storage.SetString(BackupKey, primary.Raw);
                _storage.SetString(PrimaryKey, json);
                _storage.Save();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Delete()
        {
            try
            {
                _storage.DeleteKey(PrimaryKey);
                _storage.DeleteKey(BackupKey);
                _storage.Save();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private SlotRead ReadSlot(string key)
        {
            try
            {
                if (!_storage.HasKey(key)) return SlotRead.Empty;
                string raw = _storage.GetString(key, string.Empty);
                if (string.IsNullOrWhiteSpace(raw)) return SlotRead.Empty;
                if (!raw.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    return new SlotRead(raw, null, false);

                SalonProgressData data = JsonUtility.FromJson<SalonProgressData>(raw);
                if (data == null) return new SlotRead(raw, null, false);
                if (data.SchemaVersion > SalonProgressData.CurrentSchemaVersion)
                    return new SlotRead(raw, null, true);

                // Schema 1 saves written before the satisfaction field was
                // introduced deserialize a missing int as zero. Preserve the
                // established starting value for those documents while keeping
                // an explicit zero valid for newer saves.
                if (raw.IndexOf("\"ShopSatisfaction\"", StringComparison.Ordinal) < 0)
                    data.ShopSatisfaction = SalonProgressData.DefaultShopSatisfaction;

                // R1 added the paid amount without bumping the schema. A
                // legacy purchased flag means the rack was already built, so
                // upgrade it atomically to the full construction amount.
                if (raw.IndexOf("\"SupplyRackExpansionPaid\"", StringComparison.Ordinal) < 0)
                    data.SupplyRackExpansionPaid = data.SupplyRackExpansionPurchased
                        ? SalonProgressData.SupplyRackExpansionCost : 0;

                SalonProgressData normalized = data.Clone();
                if (!normalized.TryValidate(out _))
                    return new SlotRead(raw, null, false);
                return new SlotRead(raw, normalized, false);
            }
            catch (Exception)
            {
                return SlotRead.Empty;
            }
        }

        private readonly struct SlotRead
        {
            public static SlotRead Empty => new SlotRead(string.Empty, null, false);

            public readonly string Raw;
            public readonly SalonProgressData Data;
            public readonly bool IsFuture;

            public SlotRead(string raw, SalonProgressData data, bool isFuture)
            {
                Raw = raw;
                Data = data;
                IsFuture = isFuture;
            }
        }
    }
}
