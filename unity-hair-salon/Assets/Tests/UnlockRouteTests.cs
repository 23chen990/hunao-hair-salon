using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;

public sealed class UnlockRouteTests
{
    [Test]
    public void RouteOrderCostsAndOpeningDaysMatchTheApprovedExpansionPlan()
    {
        var ids = new[] { SalonUnlockId.BlowDryer, SalonUnlockId.WashStation, SalonUnlockId.HaircutChair,
            SalonUnlockId.WaitingSeats, SalonUnlockId.BlowStand, SalonUnlockId.WashAnnex, SalonUnlockId.ExtraSeats };
        var costs = new[] { 600, 1200, 2000, 2400, 3000, 3600, 4200 };
        Assert.AreEqual(ids.Length, SalonUnlockRoute.Route.Count);
        for (int i = 0; i < ids.Length; i++)
        {
            Assert.AreEqual(ids[i], SalonUnlockRoute.Route[i].Id);
            Assert.AreEqual(costs[i], SalonUnlockRoute.Route[i].Cost);
            Assert.AreEqual(1, SalonUnlockRoute.Route[i].OpenDay);
        }
    }

    [Test]
    public void VisibilityFollowsEquipmentSequenceWithOnePad()
    {
        var owned = new HashSet<SalonUnlockId>();
        foreach (var definition in SalonUnlockRoute.Route)
        {
            foreach (int day in new[] { 1, 2, 7 })
                CollectionAssert.AreEqual(new[] { definition.Id }, SalonUnlockRoute.VisibleOn(day, owned.Contains, id => 0));
            owned.Add(definition.Id);
        }
        Assert.IsEmpty(SalonUnlockRoute.VisibleOn(1, owned.Contains, id => 0));
    }

    [Test]
    public void LegacyPartialPaymentDoesNotRevealSeveralPads()
    {
        CollectionAssert.AreEqual(new[] { SalonUnlockId.BlowDryer },
            SalonUnlockRoute.VisibleOn(1, id => false, id => id == SalonUnlockId.ExtraSeats ? 1 : 0));
    }

    [Test]
    public void ExtraSeatsRequiresWaitingSeatsAndHelpersReportOpeningsAndCapacity()
    {
        Assert.AreEqual(SalonUnlockId.WashAnnex,
            SalonUnlockRoute.Get(SalonUnlockId.ExtraSeats).Requires.Value);
        Assert.IsNull(SalonUnlockRoute.FirstOpeningOn(6));
        Assert.IsNull(SalonUnlockRoute.FirstOpeningOn(2));
        Assert.AreEqual(4, SalonUnlockRoute.WaitingCapacity(false));
        Assert.AreEqual(6, SalonUnlockRoute.WaitingCapacity(true));
        Assert.AreEqual(6, SalonUnlockRoute.MaxConcurrentCustomers(false));
        Assert.AreEqual(8, SalonUnlockRoute.MaxConcurrentCustomers(true));
        Assert.AreEqual(450f, SalonUnlockRoute.Get(SalonUnlockId.WashAnnex).PaymentPerSecond);
        Assert.AreEqual(60f, SalonUnlockRoute.Get(SalonUnlockId.SupplyRack).PaymentPerSecond);
    }

    [Test]
    public void WaitingSeatsReduceUnengagedWaitingPatienceDrainToSeventyFivePercent()
    {
        SalonGameModel withoutSeats = NewPatienceModel();
        CustomerModel standing = withoutSeats.Spawn(1, new[] { ServiceType.Wash });
        withoutSeats.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, standing.State);
        float standingBefore = standing.Patience;
        withoutSeats.Tick(1f);
        float standingLoss = standingBefore - standing.Patience;

        SalonGameModel withSeats = NewPatienceModel();
        withSeats.WaitingSeatsInstalled = true;
        CustomerModel seated = withSeats.Spawn(2, new[] { ServiceType.Wash });
        withSeats.Tick(SalonGameModel.EnteringSeconds + .01f);
        float seatedBefore = seated.Patience;
        withSeats.Tick(1f);
        float seatedLoss = seatedBefore - seated.Patience;

        Assert.Greater(standingLoss, 0f);
        Assert.AreEqual(.75f, seatedLoss / standingLoss, .001f);
    }

    [Test]
    public void InstallingAutoBlowStandDoesNotSpendBalance()
    {
        var game = NewPatienceModel();
        int balance = game.Balance;
        game.InstallAutoBlowStand();
        Assert.AreEqual(balance, game.Balance);
        Assert.IsTrue(game.HasAutoBlowStand);
    }

    [Test]
    public void NewProgressFieldsValidatePurchaseCompletionAndPrerequisite()
    {
        SalonProgressData data = SalonProgressData.CreateDefault();
        data.WaitingSeatsPurchased = true;
        data.WaitingSeatsPaid = SalonProgressData.WaitingSeatsCost;
        data.BlowStandPaid = SalonProgressData.BlowStandCost;
        data.ExtraSeatsPurchased = true;
        data.ExtraSeatsPaid = SalonProgressData.ExtraSeatsCost;
        Assert.IsTrue(data.TryValidate(out _));

        data.ExtraSeatsPurchased = false;
        data.ExtraSeatsPaid = SalonProgressData.ExtraSeatsCost - 1;
        Assert.IsTrue(data.TryValidate(out _));
        data.ExtraSeatsPurchased = true;
        Assert.IsFalse(data.TryValidate(out _), "A purchased bench needs its full payment.");
        data.ExtraSeatsPaid = SalonProgressData.ExtraSeatsCost;
        data.WaitingSeatsPurchased = false;
        Assert.IsFalse(data.TryValidate(out _), "The bench requires the waiting seats.");

        SalonProgressData shopEra = SalonProgressData.CreateDefault();
        shopEra.AutoBlowPurchased = true;
        Assert.IsTrue(shopEra.TryValidate(out _), "A stand bought in the removed shop stays valid.");
        shopEra.BlowStandPaid = SalonProgressData.BlowStandCost + 1;
        Assert.IsFalse(shopEra.TryValidate(out _));
    }

    [Test]
    public void LegacyDayTwoSaveMigratesSeatsAnnexAndBlowStandPayments()
    {
        var storage = new MemoryStorage();
        var repository = new PlayerPrefsSalonProgressRepository(storage);
        storage.SetString(PlayerPrefsSalonProgressRepository.PrimaryKey,
            "{\"SchemaVersion\":1,\"DayNumber\":2,\"Balance\":700," +
            "\"AutoBlowPurchased\":true,\"FirstDayComplete\":false," +
            "\"WashAnnexExpansionPurchased\":true,\"WashAnnexExpansionPaid\":600," +
            "\"ReputationStars\":3,\"CompletedDays\":[],\"BestCompletedOrders\":[]}");

        SalonProgressData loaded = repository.Load();
        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.WaitingSeatsPurchased);
        Assert.AreEqual(2400, loaded.WaitingSeatsPaid);
        Assert.AreEqual(3600, loaded.WashAnnexExpansionPaid);
        Assert.AreEqual(3000, loaded.BlowStandPaid);
        Assert.IsTrue(loaded.BlowDryerPurchased && loaded.WashStationPurchased);
        Assert.IsTrue(loaded.TryValidate(out _));
    }

    [Test]
    public void NewSaveWithoutWaitingSeatsPurchaseStaysUnpurchased()
    {
        var storage = new MemoryStorage();
        var repository = new PlayerPrefsSalonProgressRepository(storage);
        SalonProgressData data = SalonProgressData.CreateDefault();
        Assert.IsTrue(repository.Save(data));
        Assert.IsFalse(repository.Load().WaitingSeatsPurchased);
    }

    [Test]
    public void SeatedPoseRequiresBuiltWaitingSeats()
    {
        MethodInfo method = typeof(SalonCustomerView).GetMethod("ShouldUseSeatedPose",
            BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(CustomerState), typeof(bool), typeof(bool) }, null);
        Assert.IsNotNull(method);
        Assert.IsFalse((bool)method.Invoke(null, new object[] { CustomerState.Waiting, true, false }));
        Assert.IsTrue((bool)method.Invoke(null, new object[] { CustomerState.Waiting, true, true }));
        Assert.IsTrue((bool)method.Invoke(null, new object[] { CustomerState.Serving, true, false }));
    }

    [Test]
    public void FurnitureBuiltUnderThePlayerPushesThemToTheNearestFreeFloor()
    {
        var floor = new UnityEngine.Rect(-10f, -10f, 20f, 20f);
        var stand = new List<UnityEngine.Rect> { UnityEngine.Rect.MinMaxRect(-4.4f, -.25f, -3.2f, .95f) };
        var onPad = new UnityEngine.Vector3(-3.8f, .2f, .35f);

        UnityEngine.Vector3 stuck = SalonMobileNavigation.Move(onPad, UnityEngine.Vector2.right,
            UnityEngine.Vector3.right, UnityEngine.Vector3.forward, 1f, floor, stand);
        Assert.AreEqual(onPad, stuck, "Movement alone cannot leave furniture that appeared on the player.");

        UnityEngine.Vector3 freed = SalonMobileNavigation.ResolveOverlap(onPad, floor, stand);
        Assert.IsFalse(stand[0].Contains(new UnityEngine.Vector2(freed.x, freed.z)));
        Assert.LessOrEqual(UnityEngine.Vector3.Distance(onPad, freed), .75f);
        Assert.AreEqual(onPad.y, freed.y);
        UnityEngine.Vector3 walked = SalonMobileNavigation.Move(freed, UnityEngine.Vector2.up,
            UnityEngine.Vector3.right, UnityEngine.Vector3.forward, .5f, floor, stand);
        Assert.AreNotEqual(freed, walked);

        var clear = new UnityEngine.Vector3(0f, .2f, 0f);
        Assert.AreEqual(clear, SalonMobileNavigation.ResolveOverlap(clear, floor, stand));
    }

    private static Func<SalonUnlockId, bool> Built(params SalonUnlockId[] ids)
        => candidate => Array.IndexOf(ids, candidate) >= 0;

    private static SalonGameModel NewPatienceModel()
    {
        return new SalonGameModel(
            patienceConfig: new CustomerPatienceConfig
            {
                InitialPatience = 100f,
                DrainPerSecond = 10f,
                ServiceArrivalGraceSeconds = 0f
            },
            flowConfig: new SalonFlowConfig { WaitingCapacity = 4, MaxCustomers = 6 });
    }

    private sealed class MemoryStorage : ISalonProgressStorage
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public bool HasKey(string key) => _values.ContainsKey(key);
        public string GetString(string key, string fallback) =>
            _values.TryGetValue(key, out string value) ? value : fallback;
        public void SetString(string key, string value) => _values[key] = value;
        public void DeleteKey(string key) => _values.Remove(key);
        public void Save() { }
    }
}
