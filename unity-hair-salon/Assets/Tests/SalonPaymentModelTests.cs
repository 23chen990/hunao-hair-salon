using System;
using HairSalon;
using NUnit.Framework;

public class SalonPaymentModelTests
{
    [TestCase(HaircutServiceRating.Recovered, 120, CoinPileSize.Medium)]
    [TestCase(HaircutServiceRating.Perfect, 300, CoinPileSize.Medium)]
    public void HaircutRatingCreatesExpectedPayment(
        HaircutServiceRating rating, int expectedAmount, CoinPileSize expectedSize)
    {
        var payments = new SalonPaymentModel(8640);

        PaymentDropModel drop = payments.CreateHaircutPayment(12, 1, rating);

        Assert.AreEqual(expectedAmount, drop.Amount);
        Assert.AreEqual(expectedSize, drop.Size);
        Assert.AreEqual(PaymentDropState.Pending, drop.State);
        Assert.AreEqual(8640, payments.Balance);
    }

    [Test]
    public void FailedIncompleteServiceCannotCreatePayment()
    {
        var payments = new SalonPaymentModel(8640);

        Assert.Throws<ArgumentException>(() =>
            payments.CreateHaircutPayment(12, 1, HaircutServiceRating.Failed));
        Assert.AreEqual(0, payments.Drops.Count);
        Assert.AreEqual(8640, payments.Balance);
    }

    [Test]
    public void BalanceIncreasesOnlyAfterCollectionAnimationCompletes()
    {
        var payments = new SalonPaymentModel(8640);
        PaymentDropModel drop = payments.CreateHaircutPayment(12, 1, HaircutServiceRating.Recovered);

        Assert.IsTrue(payments.BeginCollection(drop.Id));
        Assert.AreEqual(PaymentDropState.Collecting, drop.State);
        Assert.AreEqual(8640, payments.Balance);

        Assert.IsTrue(payments.CompleteCollection(drop.Id));
        Assert.AreEqual(8760, payments.Balance);
        Assert.AreEqual(PaymentDropState.Collected, drop.State);
    }

    [Test]
    public void PaymentCannotBeCollectedTwice()
    {
        var payments = new SalonPaymentModel();
        PaymentDropModel drop = payments.CreateHaircutPayment(12, 1, HaircutServiceRating.Perfect);

        Assert.IsTrue(payments.BeginCollection(drop.Id));
        Assert.IsTrue(payments.CompleteCollection(drop.Id));
        Assert.IsFalse(payments.BeginCollection(drop.Id));
        Assert.IsFalse(payments.CompleteCollection(drop.Id));
        Assert.AreEqual(300, payments.Balance);
    }

    [Test]
    public void DifferentCustomersKeepIndependentUniformPickups()
    {
        var payments = new SalonPaymentModel(1000);
        PaymentDropModel first = payments.CreateHaircutPayment(21, 1, HaircutServiceRating.Recovered);
        PaymentDropModel second = payments.CreateHaircutPayment(22, 1, HaircutServiceRating.Perfect);

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(first.CustomerId, second.CustomerId);
        Assert.AreEqual(first.Size, second.Size,
            "Reward amount is data; every world pickup must use the same visual classification.");
        Assert.AreEqual(2, payments.Drops.Count);
        Assert.IsTrue(payments.BeginCollection(first.Id));
        Assert.IsTrue(payments.CompleteCollection(first.Id));
        Assert.AreEqual(1120, payments.Balance);
        Assert.AreEqual(PaymentDropState.Collected, first.State);
        Assert.AreEqual(PaymentDropState.Pending, second.State,
            "Collecting customer A's pickup must not merge or collect customer B's pickup.");
    }

    [Test]
    public void HappyPickupKeepsBaseAndConfigurableTipSeparate()
    {
        System.Type configType = typeof(SalonPaymentModel).Assembly.GetType("HairSalon.SalonRewardConfig");
        Assert.IsNotNull(configType);
        object config = System.Activator.CreateInstance(configType);
        configType.GetField("HappyTipReward").SetValue(config, 77);
        var constructor = typeof(SalonPaymentModel).GetConstructor(new[] { typeof(int), configType });
        Assert.IsNotNull(constructor);
        var payments = (SalonPaymentModel)constructor.Invoke(new[] { (object)500, config });
        var method = typeof(SalonPaymentModel).GetMethod("CreateHaircutPayment", new[]
        {
            typeof(int), typeof(int), typeof(HaircutServiceRating), typeof(bool)
        });
        Assert.IsNotNull(method);

        var drop = (PaymentDropModel)method.Invoke(payments, new object[]
        {
            30, 2, HaircutServiceRating.Perfect, true
        });
        var baseReward = typeof(PaymentDropModel).GetField("BaseReward");
        var tipReward = typeof(PaymentDropModel).GetField("TipReward");
        var totalReward = typeof(PaymentDropModel).GetField("TotalReward");
        Assert.IsNotNull(baseReward);
        Assert.IsNotNull(tipReward);
        Assert.IsNotNull(totalReward);
        Assert.AreEqual(300, baseReward.GetValue(drop));
        Assert.AreEqual(77, tipReward.GetValue(drop));
        Assert.AreEqual(377, totalReward.GetValue(drop));
    }
}
