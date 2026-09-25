using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonMobileSettlementTests
{
    [Test]
    public void PaymentCreatedSettlesImmediatelyExactlyOnce()
    {
        var root = new GameObject("Mobile closing settlement test");
        try
        {
            var owner = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            game.RestorePersistentState(0, false, false);
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            typeof(SalonDemo).GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, game);
            typeof(SalonDemo).GetField("_dayController", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, day);
            var first = game.Payments.CreateFinalPayment(1, 1, 100, 20);
            var second = game.Payments.CreateFinalPayment(2, 2, 200, 40);
            var payment = typeof(SalonDemo).GetMethod("HandlePaymentCreated", BindingFlags.Instance | BindingFlags.NonPublic);
            payment.Invoke(owner, new object[] { first });
            payment.Invoke(owner, new object[] { second });
            payment.Invoke(owner, new object[] { first });
            Assert.That(game.Balance, Is.EqualTo(360));
            Assert.That(day.Stats.OrderIncome, Is.EqualTo(300));
            Assert.That(day.Stats.TipIncome, Is.EqualTo(60));
            Assert.That(first.State, Is.EqualTo(PaymentDropState.Collected));
            Assert.That(second.State, Is.EqualTo(PaymentDropState.Collected));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
