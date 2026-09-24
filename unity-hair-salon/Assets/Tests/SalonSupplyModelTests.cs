using HairSalon;
using NUnit.Framework;

public class SalonSupplyModelTests
{
    [Test]
    public void PickUpStopsAtCarryCapacity()
    {
        var supply = new SalonSupplyModel(sourceStock: 5, carryCapacity: 3, washRackCapacity: 6);

        Assert.IsTrue(supply.TryPickUpWashKit());
        Assert.IsTrue(supply.TryPickUpWashKit());
        Assert.IsTrue(supply.TryPickUpWashKit());
        Assert.IsFalse(supply.TryPickUpWashKit(), "满背包不能继续从箱子取货");
        Assert.AreEqual(3, supply.CarriedWashKits);
        Assert.AreEqual(2, supply.SourceWashKits);
    }

    [Test]
    public void DeliverMovesOneKitFromCarryToSharedRack()
    {
        var supply = new SalonSupplyModel(sourceStock: 3, carryCapacity: 3, washRackCapacity: 6);
        supply.TryPickUpWashKit();
        supply.TryPickUpWashKit();

        Assert.IsTrue(supply.TryDeliverWashKit());
        Assert.AreEqual(1, supply.CarriedWashKits);
        Assert.AreEqual(1, supply.WashRackWashKits);
        Assert.AreEqual(1, supply.SourceWashKits);
    }

    [Test]
    public void DeliverDoesNotOverflowRackOrCreateStock()
    {
        var supply = new SalonSupplyModel(sourceStock: 3, carryCapacity: 3, washRackCapacity: 1);
        supply.TryPickUpWashKit();
        supply.TryPickUpWashKit();

        Assert.IsTrue(supply.TryDeliverWashKit());
        Assert.IsFalse(supply.TryDeliverWashKit(), "满补给架不能吞掉玩家手里的物资");
        Assert.AreEqual(1, supply.WashRackWashKits);
        Assert.AreEqual(1, supply.CarriedWashKits);
    }

    [Test]
    public void ConsumeRemovesExactlyOneKitAndEmptyRackBlocksOnlyWash()
    {
        var supply = new SalonSupplyModel(sourceStock: 2, carryCapacity: 3, washRackCapacity: 6);
        supply.TryPickUpWashKit();
        supply.TryDeliverWashKit();

        Assert.IsTrue(supply.TryConsumeWashKit());
        Assert.AreEqual(0, supply.WashRackWashKits);
        Assert.IsFalse(supply.TryConsumeWashKit());
        Assert.IsFalse(supply.CanStartWash);
    }

    [Test]
    public void ResetDayClearsTemporaryCarryAndRestoresSourceStock()
    {
        var supply = new SalonSupplyModel(sourceStock: 2, carryCapacity: 3, washRackCapacity: 6);
        supply.TryPickUpWashKit();
        supply.TryDeliverWashKit();
        supply.TryPickUpWashKit();

        supply.ResetDay(sourceStock: 12);

        Assert.AreEqual(12, supply.SourceWashKits);
        Assert.AreEqual(0, supply.CarriedWashKits);
        Assert.AreEqual(0, supply.WashRackWashKits);
        Assert.IsFalse(supply.CanStartWash);
    }
}
