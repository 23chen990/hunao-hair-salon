using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonMobileCheckpointRegressionTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private MemoryRepository _repository;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Mobile checkpoint regression");
        _demo = _root.AddComponent<SalonDemo>();
        _repository = new MemoryRepository();
        Set("_mobileMode", true);
        Set("_mobileRestoring", false);
        Set("_mobileSaves", _repository);
        Set("_mobileProgress", SalonProgressData.CreateDefault());
        Set("_game", new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig()));
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        day.PrepareDay(1);
        Set("_dayController", day);
        Set("_satisfaction", new ShopSatisfactionModel());
        Set("_mobileSupplyPad", new SalonProximityPurchasePadModel("pad", 180));
        Set("_mobileOpening", SalonProgressData.CreateDefault());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void SavingAnInDayCheckpointDoesNotRewriteTheImmutableDayOpening()
    {
        var pad = (SalonProximityPurchasePadModel)Field("_mobileSupplyPad");
        Assert.IsTrue(pad.ApplyPayment(45));

        Invoke("SaveMobileCheckpoint", false);

        var opening = (SalonProgressData)Field("_mobileOpening");
        Assert.AreEqual(0, opening.SupplyRackExpansionPaid);
        Assert.IsFalse(opening.SupplyRackExpansionPurchased);
        Assert.IsNotNull(_repository.Load());
        Assert.AreEqual(45, _repository.Load().SupplyRackExpansionPaid);
    }

    [Test]
    public void RestoringAStoredPartialPaymentRebuildsThePadProgress()
    {
        var stored = SalonProgressData.CreateDefault();
        stored.SupplyRackExpansionPaid = 75;
        Set("_mobileSupplyPad", new SalonProximityPurchasePadModel("pad", 180));

        Set("_mobileProgress", stored);
        Invoke("RestoreMobileSupplyPadProgress", stored);

        Assert.AreEqual(75, ((SalonProximityPurchasePadModel)Field("_mobileSupplyPad")).Paid);
        Assert.AreEqual(SalonProximityPurchasePadState.Building,
            ((SalonProximityPurchasePadModel)Field("_mobileSupplyPad")).State);
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
