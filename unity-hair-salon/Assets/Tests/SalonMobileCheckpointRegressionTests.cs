using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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

    [Test]
    public void LowPerformanceResultPreservesProgressAndFinalizesHistoryOnlyOnce()
    {
        var game = (SalonGameModel)Field("_game");
        game.RestorePersistentState(120, false, false);
        var pad = (SalonProximityPurchasePadModel)Field("_mobileSupplyPad");
        Assert.IsTrue(pad.ApplyPayment(45));

        var resultTitle = new GameObject("Result title").AddComponent<Text>();
        var resultSummary = new GameObject("Result summary").AddComponent<Text>();
        var continueButton = new GameObject("Continue").AddComponent<Button>();
        var retryButton = new GameObject("Retry").AddComponent<Button>();
        Set("_resultTitleLabel", resultTitle);
        Set("_resultSummaryLabel", resultSummary);
        Set("_resultContinueButton", continueButton);
        Set("_mobileRetryButton", retryButton);

        var day = (BusinessDayController)Field("_dayController");
        day.Stats.CompletedOrders = 0;
        Invoke("HandleMobileDayState", DayState.Result);
        Invoke("HandleMobileDayState", DayState.Result);

        SalonProgressData saved = _repository.Load();
        Assert.IsNotNull(saved);
        Assert.AreEqual(120, saved.Balance,
            "A missed target must keep already earned balance instead of restoring the opening snapshot.");
        Assert.AreEqual(45, saved.SupplyRackExpansionPaid,
            "A missed target must keep paid construction progress.");
        Assert.IsTrue(saved.DaySettled);
        Assert.AreEqual(1, saved.CompletedDays.Count,
            "Repeated result callbacks must not duplicate the day history.");
        Assert.IsTrue(continueButton.gameObject.activeSelf);
        Assert.IsFalse(retryButton.gameObject.activeSelf);
    }

    [Test]
    public void FailedSettlementSaveCanRetryWithoutRepeatingSettlement()
    {
        var game = (SalonGameModel)Field("_game");
        game.RestorePersistentState(120, false, false);
        var pad = (SalonProximityPurchasePadModel)Field("_mobileSupplyPad");
        Assert.IsTrue(pad.ApplyPayment(45));

        var resultTitle = new GameObject("Retry result title").AddComponent<Text>();
        var resultSummary = new GameObject("Retry result summary").AddComponent<Text>();
        var continueButton = new GameObject("Retry continue").AddComponent<Button>();
        var retryButton = new GameObject("Retry legacy").AddComponent<Button>();
        var saveRetryButton = new GameObject("Save retry").AddComponent<Button>();
        Set("_resultTitleLabel", resultTitle);
        Set("_resultSummaryLabel", resultSummary);
        Set("_resultContinueButton", continueButton);
        Set("_mobileRetryButton", retryButton);
        Set("_mobileSaveRetryButton", saveRetryButton);
        _repository.FailNextSave = true;

        Invoke("HandleMobileDayState", DayState.Result);
        Assert.IsTrue(saveRetryButton.gameObject.activeSelf);
        Assert.IsTrue(resultSummary.text.Contains("保存失败"));
        Assert.AreEqual(1, _repository.SaveAttempts);

        Invoke("RetrySaveMobileCheckpoint");

        Assert.AreEqual(2, _repository.SaveAttempts,
            "The retry button must perform a second persistence attempt.");
        Assert.IsFalse(saveRetryButton.gameObject.activeSelf);
        Assert.IsTrue(resultSummary.text.Contains("进度已保存"));
        Assert.AreEqual(1, ((SalonProgressData)Field("_mobileProgress")).CompletedDays.Count,
            "Retrying persistence must not repeat daily settlement history.");
        Assert.AreEqual(120, _repository.Load().Balance);
        Assert.AreEqual(45, _repository.Load().SupplyRackExpansionPaid);
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
        public bool FailNextSave;
        public int SaveAttempts;

        public SalonProgressData Load() => _data == null ? null : _data.Clone();

        public bool Save(SalonProgressData data)
        {
            SaveAttempts++;
            if (FailNextSave)
            {
                FailNextSave = false;
                return false;
            }
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
