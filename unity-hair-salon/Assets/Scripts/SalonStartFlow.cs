using System;
using System.Collections.Generic;

namespace HairSalon
{
    public enum SalonPlayMode
    {
        SinglePlayer = 1,
        TwoPlayer = 2
    }

    /// <summary>
    /// The three campaign slots exposed by the normal start screen. Slot one
    /// deliberately keeps the original save keys so existing players keep
    /// their progress after the start flow is introduced.
    /// </summary>
    public static class SalonSaveSlots
    {
        public const int Count = 3;
        public const string SelectedSlotKey = "HairSalon.Progress.SelectedSlot.v1";

        public static int NormalizeSlot(int slotId)
        {
            if (slotId < 1) return 1;
            return slotId > Count ? Count : slotId;
        }

        public static string GetPrimaryKey(int slotId)
        {
            int normalized = NormalizeSlot(slotId);
            return normalized == 1
                ? PlayerPrefsSalonProgressRepository.PrimaryKey
                : "HairSalon.Progress.Slot" + normalized + ".Primary.v1";
        }

        public static string GetBackupKey(int slotId)
        {
            int normalized = NormalizeSlot(slotId);
            return normalized == 1
                ? PlayerPrefsSalonProgressRepository.BackupKey
                : "HairSalon.Progress.Slot" + normalized + ".Backup.v1";
        }

        public static List<SalonSaveSlotSummary> ReadSummaries(ISalonProgressStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            var summaries = new List<SalonSaveSlotSummary>(Count);
            for (int slotId = 1; slotId <= Count; slotId++)
            {
                var repository = new PlayerPrefsSalonProgressRepository(storage, slotId);
                summaries.Add(SalonSaveSlotSummary.From(slotId, repository.Load()));
            }
            return summaries;
        }
    }

    public readonly struct SalonSaveSlotSummary
    {
        public readonly int SlotId;
        public readonly bool HasSave;
        public readonly int DayNumber;
        public readonly int Balance;
        public readonly int CompletedDayCount;

        public string DisplayStatus
        {
            get
            {
                if (!HasSave) return "新进度";
                return "DAY " + DayNumber + "  ·  " + Balance.ToString("N0") + " 金币\n已完成 " +
                       CompletedDayCount + " 天";
            }
        }

        private SalonSaveSlotSummary(int slotId, bool hasSave, int dayNumber, int balance, int completedDayCount)
        {
            SlotId = SalonSaveSlots.NormalizeSlot(slotId);
            HasSave = hasSave;
            DayNumber = Math.Max(1, dayNumber);
            Balance = Math.Max(0, balance);
            CompletedDayCount = Math.Max(0, completedDayCount);
        }

        public static SalonSaveSlotSummary From(int slotId, SalonProgressData data)
        {
            if (data == null)
                return new SalonSaveSlotSummary(slotId, false, 1, 0, 0);
            return new SalonSaveSlotSummary(slotId, true, data.DayNumber, data.Balance,
                data.CompletedDays == null ? 0 : data.CompletedDays.Count);
        }
    }
}
