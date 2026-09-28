using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class SalonStartFlowTests
{
    [Test]
    public void SaveSlotKeysKeepLegacySlotOneAndSeparateThreeSlots()
    {
        Assert.AreEqual(PlayerPrefsSalonProgressRepository.PrimaryKey, SalonSaveSlots.GetPrimaryKey(1));
        Assert.AreEqual(PlayerPrefsSalonProgressRepository.BackupKey, SalonSaveSlots.GetBackupKey(1));
        Assert.AreNotEqual(SalonSaveSlots.GetPrimaryKey(1), SalonSaveSlots.GetPrimaryKey(2));
        Assert.AreNotEqual(SalonSaveSlots.GetPrimaryKey(2), SalonSaveSlots.GetPrimaryKey(3));
        Assert.AreEqual(3, SalonSaveSlots.Count);
    }

    [Test]
    public void RepositoryKeepsSlotDataIsolated()
    {
        var storage = new MemoryStorage();
        var slotOne = new PlayerPrefsSalonProgressRepository(storage, 1);
        var slotTwo = new PlayerPrefsSalonProgressRepository(storage, 2);
        SalonProgressData data = SalonProgressData.CreateDefault();
        data.DayNumber = 4;
        data.Balance = 360;

        Assert.IsTrue(slotTwo.Save(data));
        Assert.IsNull(slotOne.Load());
        Assert.AreEqual(4, slotTwo.Load().DayNumber);
        Assert.IsTrue(storage.Values.ContainsKey(SalonSaveSlots.GetPrimaryKey(2)));
        Assert.IsFalse(storage.Values.ContainsKey(SalonSaveSlots.GetPrimaryKey(1)));
    }

    [Test]
    public void SlotSummaryDistinguishesEmptyAndOccupiedProgress()
    {
        var empty = SalonSaveSlotSummary.From(1, null);
        Assert.AreEqual(1, empty.SlotId);
        Assert.IsFalse(empty.HasSave);
        Assert.AreEqual("新进度", empty.DisplayStatus);

        SalonProgressData data = SalonProgressData.CreateDefault();
        data.DayNumber = 3;
        data.Balance = 245;
        data.CompletedDays.Add(1);
        data.CompletedDays.Add(2);
        var occupied = SalonSaveSlotSummary.From(2, data);
        Assert.IsTrue(occupied.HasSave);
        Assert.AreEqual(3, occupied.DayNumber);
        Assert.AreEqual(245, occupied.Balance);
        Assert.AreEqual(2, occupied.CompletedDayCount);
        StringAssert.Contains("DAY 3", occupied.DisplayStatus);
    }

    [Test]
    public void SlotIdsNormalizeToThreeVisibleChoices()
    {
        Assert.AreEqual(1, SalonSaveSlots.NormalizeSlot(0));
        Assert.AreEqual(1, SalonSaveSlots.NormalizeSlot(-4));
        Assert.AreEqual(3, SalonSaveSlots.NormalizeSlot(99));
        Assert.AreEqual(2, SalonSaveSlots.NormalizeSlot(2));
    }

    private sealed class MemoryStorage : ISalonProgressStorage
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public bool HasKey(string key) => Values.ContainsKey(key);
        public string GetString(string key, string defaultValue) => Values.TryGetValue(key, out string value) ? value : defaultValue;
        public void SetString(string key, string value) => Values[key] = value;
        public void DeleteKey(string key) => Values.Remove(key);
        public void Save() { }
    }
}
