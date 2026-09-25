using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class P103ExpansionTests
{
    [Test]
    public void MobileOpeningLocksUnpurchasedWorkstationsWithoutChangingStableIds()
    {
        var game = new SalonGameModel();
        game.ConfigureWorkstationAvailability(false, false, false);

        Assert.AreEqual(5, game.Workstations.Count);
        Assert.AreEqual(WorkstationState.Available, game.Workstations[0].State);
        Assert.AreEqual(WorkstationState.Available, game.Workstations[1].State);
        Assert.AreEqual(WorkstationState.Locked, game.Workstations[2].State);
        Assert.AreEqual(WorkstationState.Locked, game.Workstations[3].State);
        Assert.AreEqual(WorkstationState.Locked, game.Workstations[4].State);

        CustomerModel cut = game.Spawn(10301, new[] { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsFalse(game.Assign(cut, 2));
        Assert.IsFalse(game.Assign(cut, 3));
        Assert.IsFalse(game.Assign(cut, 4));
    }

    [Test]
    public void UnlockingSecondHaircutMakesItImmediatelyUsable()
    {
        var game = new SalonGameModel();
        game.ConfigureWorkstationAvailability(false, false, false);
        game.ConfigureWorkstationAvailability(true, false, false);

        Assert.AreEqual(WorkstationState.Available, game.Workstations[2].State);
        CustomerModel cut = game.Spawn(10302, new[] { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(cut, 2));
    }

    [Test]
    public void HaircutExpansionProgressUsesItsOwnPersistentLedger()
    {
        var data = SalonProgressData.CreateDefault();
        data.HaircutExpansionPaid = 75;
        Assert.IsTrue(data.TryValidate(out _));
        var clone = data.Clone();
        Assert.AreEqual(75, clone.HaircutExpansionPaid);
        Assert.IsFalse(clone.HaircutExpansionPurchased);
    }

    [Test]
    public void HaircutExpansionPadUsesStableIdIndependentCostAndPartialPayment()
    {
        var pad = new SalonProximityPurchasePadModel("expansion-pad-haircut-2", SalonProgressData.HaircutExpansionCost);
        Assert.AreEqual("expansion-pad-haircut-2", pad.PadId);
        Assert.AreEqual(180, pad.Cost);
        Assert.AreEqual(60, pad.CalculatePayment(1f, 999, 60));
        Assert.IsTrue(pad.ApplyPayment(60));
        Assert.AreEqual(60, pad.Paid);
        Assert.IsFalse(pad.IsUnlocked);
        Assert.IsTrue(pad.ApplyPayment(120));
        Assert.IsTrue(pad.IsUnlocked);
        Assert.IsFalse(pad.ApplyPayment(1));
    }

    [Test]
    public void LegacyProgressJsonDefaultsNewExpansionToUnpurchased()
    {
        var legacy = JsonUtility.FromJson<SalonProgressData>(
            "{\"SchemaVersion\":1,\"DayNumber\":1,\"Balance\":40,\"ShopSatisfaction\":90,\"ReputationStars\":3}");
        Assert.IsFalse(legacy.HaircutExpansionPurchased);
        Assert.AreEqual(0, legacy.HaircutExpansionPaid);
        Assert.IsTrue(legacy.TryValidate(out _));
    }
}
