using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonPurchasePadRuntimeIntegrationTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;
    private BusinessDayController _day;
    private SalonProximityPurchasePadModel _pad;
    private Transform _player;
    private Transform _padPoint;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Purchase pad runtime integration");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        _game.RestorePersistentState(180, false, false);
        _day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        _day.PrepareDay(1);
        _day.StartBusiness();
        _pad = new SalonProximityPurchasePadModel("pad", 180);
        _player = new GameObject("Runtime player").transform;
        _player.SetParent(_root.transform, false);
        _padPoint = new GameObject("Runtime pad point").transform;
        _padPoint.SetParent(_root.transform, false);

        Set("_mobileMode", true);
        Set("_game", _game);
        Set("_dayController", _day);
        Set("_mobileSupplyPad", _pad);
        Set("_player", _player);
        Set("_mobileSupplyPadPoint", _padPoint);
        Set("_mobileProgress", SalonProgressData.CreateDefault());
        Set("_mobileOpening", SalonProgressData.CreateDefault());
        Set("_satisfaction", new ShopSatisfactionModel());
        Set("_mobileSaves", new MemoryRepository());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(90)]
    [TestCase(120)]
    public void ActualMobilePurchasePadLoopFinishesAtNominalRate(int framesPerSecond)
    {
        float dt = 1f / framesPerSecond;
        int frames = 0;
        while (!_pad.IsUnlocked && frames < framesPerSecond * 8)
        {
            Invoke("UpdateMobilePurchasePad", dt);
            frames++;
        }

        Assert.IsTrue(_pad.IsUnlocked);
        Assert.AreEqual(180, _pad.Paid);
        Assert.AreEqual(0, _game.Balance);
        Assert.That(frames / (float)framesPerSecond, Is.InRange(2.95f, 3.2f));
        Assert.That((float)Field("_mobilePadSpendElapsed"), Is.LessThan(dt + .0001f));
    }

    [Test]
    public void NonUniformRuntimeDeltasPreservePaymentCredit()
    {
        float[] deltas = { .007f, .021f, .013f, .031f, .009f, .019f };
        float wallTime = 0f;
        int index = 0;
        while (!_pad.IsUnlocked && wallTime < 8f)
        {
            float dt = deltas[index++ % deltas.Length];
            wallTime += dt;
            Invoke("UpdateMobilePurchasePad", dt);
        }

        Assert.IsTrue(_pad.IsUnlocked);
        Assert.AreEqual(0, _game.Balance);
        Assert.That(wallTime, Is.InRange(2.95f, 3.2f));
    }

    [Test]
    public void PauseAndLeavingDoNotCreatePaymentTimeCredit()
    {
        Invoke("UpdateMobilePurchasePad", .5f);
        int paidBeforePause = _pad.Paid;
        _day.SetPaused(true);
        Invoke("UpdateMobilePurchasePad", 2f);
        Assert.AreEqual(paidBeforePause, _pad.Paid);
        _day.SetPaused(false);

        _player.position = new Vector3(4f, 0f, 0f);
        Invoke("UpdateMobilePurchasePad", .5f);
        int paidAfterLeave = _pad.Paid;
        _player.position = Vector3.zero;
        Invoke("UpdateMobilePurchasePad", .01f);
        Assert.AreEqual(paidAfterLeave, _pad.Paid);
        Assert.That((float)Field("_mobilePadSpendElapsed"), Is.LessThan(.02f));
    }

    [Test]
    public void WalletRejectionLeavesPadAndTimerUnchanged()
    {
        _game.Payments.RestoreBalance(0);
        Invoke("UpdateMobilePurchasePad", 1f);

        Assert.AreEqual(0, _pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Locked, _pad.State);
        Assert.AreEqual(0f, (float)Field("_mobilePadSpendElapsed"), .0001f);
    }

    private object Field(string name) => typeof(SalonDemo).GetField(
        name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);

    private void Set(string name, object value) => typeof(SalonDemo).GetField(
        name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);

    private object Invoke(string name, params object[] args) => typeof(SalonDemo).GetMethod(
        name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);

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
