using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonMobileSettlementTests
{
    [Test]
    public void ClosingCollectsPendingAndAnimatingPaymentsExactlyOnce()
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
            game.Payments.CreateFinalPayment(1, 1, 100, 20);
            var animating = game.Payments.CreateFinalPayment(2, 2, 200, 40);
            game.Payments.BeginCollection(animating.Id);
            var close = typeof(SalonDemo).GetMethod("CollectMobilePaymentsAtClose", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(close, Is.Not.Null, "Closing must finish earned payments before destroying their animated views.");
            close.Invoke(owner, null);
            close.Invoke(owner, null);
            Assert.That(game.Balance, Is.EqualTo(360));
            Assert.That(day.Stats.OrderIncome, Is.EqualTo(300));
            Assert.That(day.Stats.TipIncome, Is.EqualTo(60));
            Assert.That(game.Payments.CompleteCollection(animating.Id), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
