using HairSalon;
using NUnit.Framework;

/// <summary>
/// Model/save integration fixtures for the R1 checkpoint rules. These tests
/// exercise the real wallet, purchase-pad, supply and progress repository
/// types; the formal WebGL touch path is covered separately by
/// tools/check-r1-rollback.py.
/// </summary>
public sealed class SalonR1CheckpointRollbackIntegrationTests
{
    [Test]
    public void FullPurchaseFailureRestoresOpeningEconomyAndSupply()
    {
        SalonProgressData opening = Snapshot(balance: 240, paid: 0, purchased: false);
        SalonProgressData changed = opening.Clone();
        var repository = new MemoryRepository();
        var game = NewGame(opening);
        var pad = new SalonProximityPurchasePadModel("supply-rack", SalonProgressData.SupplyRackExpansionCost);
        var supply = new SalonSupplyModel(sourceStock: 12, carryCapacity: 3, washRackCapacity: 6);

        Assert.IsTrue(game.Payments.TrySpend(SalonProgressData.SupplyRackExpansionCost));
        Assert.IsTrue(pad.ApplyPayment(SalonProgressData.SupplyRackExpansionCost));
        Assert.IsTrue(supply.TryPickUpWashKit());
        Assert.IsTrue(supply.TryDeliverWashKit());
        Assert.IsTrue(supply.TryConsumeWashKit());
        changed.Balance = game.Balance;
        changed.SupplyRackExpansionPaid = pad.Paid;
        changed.SupplyRackExpansionPurchased = pad.IsUnlocked;
        Assert.IsTrue(repository.Save(changed));
        SalonProgressData loadedCheckpoint = repository.Load();

        Assert.AreEqual(60, loadedCheckpoint.Balance);
        Assert.IsTrue(loadedCheckpoint.SupplyRackExpansionPurchased);
        Assert.AreEqual(11, supply.SourceWashKits);

        SalonProgressData retry = opening.Clone();
        game = NewGame(retry);
        pad = RestorePad(retry);
        supply.ResetDay(sourceStock: 12);

        Assert.AreEqual(240, game.Balance);
        Assert.AreEqual(0, pad.Paid);
        Assert.IsFalse(pad.IsUnlocked);
        Assert.AreEqual(12, supply.SourceWashKits);
        Assert.AreEqual(0, supply.CarriedWashKits);
        Assert.AreEqual(0, supply.WashRackWashKits);
    }

    [Test]
    public void RefreshedCheckpointBecomesNewOpeningForLaterFailure()
    {
        // This is the state a valid refresh restores into PreOpen. Starting
        // business from it must make it the new immutable DayOpening.
        SalonProgressData refreshedOpening = Snapshot(balance: 140, paid: 75, purchased: false);
        SalonProgressData changed = refreshedOpening.Clone();
        var repository = new MemoryRepository();
        Assert.IsTrue(repository.Save(refreshedOpening));
        refreshedOpening = repository.Load();
        var game = NewGame(refreshedOpening);
        var pad = RestorePad(refreshedOpening);

        Assert.AreEqual(75, pad.Paid);
        Assert.IsTrue(game.Payments.TrySpend(105));
        Assert.IsTrue(pad.ApplyPayment(105));
        changed.Balance = game.Balance;
        changed.SupplyRackExpansionPaid = pad.Paid;
        changed.SupplyRackExpansionPurchased = pad.IsUnlocked;
        Assert.IsTrue(repository.Save(changed));
        SalonProgressData loadedCheckpoint = repository.Load();
        Assert.AreEqual(35, changed.Balance);
        Assert.AreEqual(35, loadedCheckpoint.Balance);
        Assert.IsTrue(loadedCheckpoint.SupplyRackExpansionPurchased);

        // Failure restores the refreshed opening, not the fully paid state.
        game = NewGame(refreshedOpening);
        pad = RestorePad(refreshedOpening);
        Assert.AreEqual(140, game.Balance);
        Assert.AreEqual(75, pad.Paid);
        Assert.IsFalse(pad.IsUnlocked);
    }

    [Test]
    public void PurchasedBeforeOpeningRemainsPurchasedAfterFailure()
    {
        SalonProgressData opening = Snapshot(balance: 60, paid: 180, purchased: true);
        var game = NewGame(opening);
        var pad = RestorePad(opening);

        Assert.AreEqual(60, game.Balance);
        Assert.AreEqual(180, pad.Paid);
        Assert.IsTrue(pad.IsUnlocked);

        // A later failed attempt must preserve the pre-opening expansion.
        game = NewGame(opening);
        pad = RestorePad(opening);
        Assert.AreEqual(60, game.Balance);
        Assert.AreEqual(180, pad.Paid);
        Assert.IsTrue(pad.IsUnlocked);
    }

    private static SalonProgressData Snapshot(int balance, int paid, bool purchased)
    {
        var snapshot = SalonProgressData.CreateDefault();
        snapshot.Balance = balance;
        snapshot.SupplyRackExpansionPaid = paid;
        snapshot.SupplyRackExpansionPurchased = purchased;
        return snapshot;
    }

    private static SalonGameModel NewGame(SalonProgressData snapshot)
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        game.RestorePersistentState(snapshot.Balance, snapshot.AutoBlowPurchased, snapshot.FirstDayComplete);
        return game;
    }

    private static SalonProximityPurchasePadModel RestorePad(SalonProgressData snapshot)
    {
        var pad = new SalonProximityPurchasePadModel(
            "supply-rack", SalonProgressData.SupplyRackExpansionCost);
        if (snapshot.SupplyRackExpansionPaid > 0)
            Assert.IsTrue(pad.ApplyPayment(snapshot.SupplyRackExpansionPaid));
        return pad;
    }

    private sealed class MemoryRepository : ISalonProgressRepository
    {
        private SalonProgressData _data;

        public SalonProgressData Load() => _data == null ? null : _data.Clone();

        public bool Save(SalonProgressData data)
        {
            if (data == null || !data.TryValidate(out _)) return false;
            _data = data.Clone();
            return true;
        }

        public bool Delete()
        {
            _data = null;
            return true;
        }
    }
}
