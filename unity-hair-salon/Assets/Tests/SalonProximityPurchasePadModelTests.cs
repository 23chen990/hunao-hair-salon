using HairSalon;
using NUnit.Framework;

public class SalonProximityPurchasePadModelTests
{
    [Test]
    public void NewPadStartsLockedWithStableIdentityAndNoPaidAmount()
    {
        var pad = new SalonProximityPurchasePadModel("expansion-pad-wash-rack", 180);

        Assert.AreEqual("expansion-pad-wash-rack", pad.PadId);
        Assert.AreEqual(180, pad.Cost);
        Assert.AreEqual(0, pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Locked, pad.State);
        Assert.AreEqual(0f, pad.Progress, 0.0001f);
    }

    [Test]
    public void CalculatePaymentIsCappedByDeltaTimeBalanceAndRemainingCost()
    {
        var pad = new SalonProximityPurchasePadModel("pad", 180);

        int payment = pad.CalculatePayment(0.5f, 100, 60);

        Assert.AreEqual(30, payment);
        Assert.AreEqual(0, pad.Paid,
            "Calculating a payment must not mutate the pad before the caller's wallet succeeds.");
        Assert.IsTrue(pad.ApplyPayment(payment));
        Assert.AreEqual(30, pad.Paid);

        Assert.AreEqual(20, pad.CalculatePayment(1f, 20, 60),
            "The wallet balance caps the amount the pad may ask the caller to spend.");
        Assert.AreEqual(150, pad.CalculatePayment(10f, 500, 60),
            "The remaining cost caps a long frame or a large balance.");
    }

    [Test]
    public void InsufficientBalanceProducesNoPayment()
    {
        var pad = new SalonProximityPurchasePadModel("pad", 180);

        Assert.AreEqual(0, pad.CalculatePayment(1f, 0, 60));
        Assert.AreEqual(0, pad.CalculatePayment(1f, -1, 60));
        Assert.AreEqual(0, pad.CalculatePayment(0f, 100, 60));
        Assert.AreEqual(0, pad.CalculatePayment(-1f, 100, 60));
        Assert.AreEqual(0, pad.CalculatePayment(1f, 100, -60));
        Assert.AreEqual(0, pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Locked, pad.State);
    }

    [Test]
    public void ExactPaymentTransitionsToUnlockedOnce()
    {
        var pad = new SalonProximityPurchasePadModel("pad", 100);

        Assert.IsTrue(pad.ApplyPayment(40));
        Assert.AreEqual(SalonProximityPurchasePadState.Building, pad.State);
        Assert.AreEqual(0.4f, pad.Progress, 0.0001f);

        Assert.IsTrue(pad.ApplyPayment(60));
        Assert.AreEqual(100, pad.Paid);
        Assert.AreEqual(1f, pad.Progress, 0.0001f);
        Assert.AreEqual(SalonProximityPurchasePadState.Unlocked, pad.State);

        Assert.AreEqual(0, pad.CalculatePayment(1f, 100, 60));
        Assert.IsFalse(pad.ApplyPayment(1), "An unlocked pad must not accept a second payment.");
        Assert.AreEqual(100, pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Unlocked, pad.State);
    }

    [Test]
    public void NegativeZeroAndOverPaymentsAreRejectedWithoutChangingProgress()
    {
        var pad = new SalonProximityPurchasePadModel("pad", 100);

        Assert.IsFalse(pad.ApplyPayment(-1));
        Assert.IsFalse(pad.ApplyPayment(0));
        Assert.IsFalse(pad.ApplyPayment(101));
        Assert.AreEqual(0, pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Locked, pad.State);

        Assert.IsTrue(pad.ApplyPayment(75));
        Assert.IsFalse(pad.ApplyPayment(26), "A payment above the remaining cost must be rejected atomically.");
        Assert.AreEqual(75, pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Building, pad.State);
    }

    [Test]
    public void PartialPaymentSurvivesLeavingAndReEnteringThePad()
    {
        var pad = new SalonProximityPurchasePadModel("pad", 120);

        Assert.IsTrue(pad.ApplyPayment(45));
        Assert.AreEqual(45, pad.Paid);

        // The scene owns proximity. Leaving it must not reset this model's paid progress.
        Assert.AreEqual(45, pad.Paid);
        Assert.AreEqual(75, pad.CalculatePayment(1f, 1000, 75));
        Assert.AreEqual(SalonProximityPurchasePadState.Building, pad.State);
    }

    [Test]
    public void InvalidPadIdentityAndCostAreRejected()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new SalonProximityPurchasePadModel("", 100));
        Assert.Throws<System.ArgumentException>(() =>
            new SalonProximityPurchasePadModel("pad", 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new SalonProximityPurchasePadModel("pad", -1));
    }
}
