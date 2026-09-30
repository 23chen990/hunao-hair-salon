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
    private object _runtimePad;
    private float SpendElapsed => (float)_runtimePad.GetType().GetField("SpendElapsed").GetValue(_runtimePad);

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Purchase pad runtime integration");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        _game.RestorePersistentState(600, false, false);
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
        Invoke("CreateMobileUnlockPads", SalonProgressData.CreateDefault());
        var pads = (System.Collections.IDictionary)Field("_mobileUnlockPads");
        _runtimePad = pads[SalonUnlockId.BlowDryer];
        _runtimePad.GetType().GetField("Point").SetValue(_runtimePad, _padPoint);
        _pad = (SalonProximityPurchasePadModel)_runtimePad.GetType().GetField("Model").GetValue(_runtimePad);
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
            Invoke("UpdateMobileUnlockPad", _runtimePad, dt);
            frames++;
        }

        Assert.IsTrue(_pad.IsUnlocked);
        Assert.AreEqual(600, _pad.Paid);
        Assert.AreEqual(0, _game.Balance);
        Assert.That(frames / (float)framesPerSecond, Is.InRange(2.95f, 3.2f));
        Assert.That(SpendElapsed, Is.LessThan(dt + .0001f));
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
            Invoke("UpdateMobileUnlockPad", _runtimePad, dt);
        }

        Assert.IsTrue(_pad.IsUnlocked);
        Assert.AreEqual(0, _game.Balance);
        Assert.That(wallTime, Is.InRange(2.95f, 3.2f));
    }

    [Test]
    public void PauseAndLeavingDoNotCreatePaymentTimeCredit()
    {
        Invoke("UpdateMobileUnlockPad", _runtimePad, .5f);
        int paidBeforePause = _pad.Paid;
        _day.SetPaused(true);
        Invoke("UpdateMobileUnlockPad", _runtimePad, 2f);
        Assert.AreEqual(paidBeforePause, _pad.Paid);
        _day.SetPaused(false);

        _player.position = new Vector3(4f, 0f, 0f);
        Invoke("UpdateMobileUnlockPad", _runtimePad, .5f);
        int paidAfterLeave = _pad.Paid;
        _player.position = Vector3.zero;
        Invoke("UpdateMobileUnlockPad", _runtimePad, .001f);
        Assert.AreEqual(paidAfterLeave, _pad.Paid);
        Assert.That(SpendElapsed, Is.LessThan(.02f));
    }

    [Test]
    public void WalletRejectionLeavesPadAndTimerUnchanged()
    {
        _game.Payments.RestoreBalance(0);
        Invoke("UpdateMobileUnlockPad", _runtimePad, 1f);

        Assert.AreEqual(0, _pad.Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Locked, _pad.State);
        Assert.AreEqual(0f, SpendElapsed, .0001f);
    }

    [Test]
    public void BuyingSecondChairPersistsTwoPaidOrdersOfLowerPressure()
    {
        new GameObject("Fixed Salon Map").transform.SetParent(_root.transform, false);
        var progress = SalonProgressData.CreateDefault();
        progress.BlowDryerPurchased = progress.WashStationPurchased = true;
        progress.BlowDryerPaid = 600; progress.WashStationPaid = 1200;
        progress.PaidCustomerCount = 10;
        progress.PaidDryOrderCount = progress.PaidWashOrderCount = 3;
        _game.Payments.RestoreBalance(2000);
        Set("_mobileProgress", progress);
        Invoke("CreateMobileUnlockPads", progress);
        var model = new SalonProximityPurchasePadModel("expansion-pad-haircut-2", 2000);
        Set("_mobileHaircutExpansionPad", model);
        Set("_mobileHaircutExpansionPadPoint", _padPoint);
        for (int frame = 0; frame < 240 && !model.IsUnlocked; frame++)
            Invoke("UpdateMobileHaircutExpansionPad", 1f / 30f);

        Assert.IsTrue(model.IsUnlocked);
        Assert.AreEqual(0, _game.Balance);
        var saved = ((ISalonProgressRepository)Field("_mobileSaves")).Load();
        Assert.IsNotNull(saved);
        Assert.IsTrue(saved.HaircutExpansionPurchased);
        Assert.AreEqual(12, saved.CapacityPracticeUntilPaid);
        Assert.AreEqual(2, SalonPacingDirector.ActiveLimit(saved));
        saved.PaidCustomerCount = 12;
        Assert.AreEqual(3, SalonPacingDirector.ActiveLimit(saved));
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
