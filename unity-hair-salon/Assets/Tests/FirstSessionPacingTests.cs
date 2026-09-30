using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Evidence-focused checks for the approved first-session pacing and the first
/// two existing expansion points. These tests do not rebalance the 180-second
/// round or add a new progression system.
/// </summary>
public sealed class FirstSessionPacingTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;
    private BusinessDayController _day;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("First session pacing regression");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        _game.RestorePersistentState(180, false, false);
        _day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
        _day.PrepareDay(1);
        _day.StartBusiness();

        Set("_mobileMode", true);
        Set("_game", _game);
        Set("_dayController", _day);
        Set("_mobileProgress", SalonProgressData.CreateDefault());
        Set("_mobileHaircutExpansionPad",
            new SalonProximityPurchasePadModel("expansion-pad-haircut-2", SalonProgressData.HaircutExpansionCost));
        Set("_mobileSupplyPad",
            new SalonProximityPurchasePadModel("expansion-pad-wash-rack", SalonProgressData.SupplyRackExpansionCost));

        GameObject playerObject = new GameObject("First session player");
        playerObject.transform.SetParent(_root.transform, false);
        playerObject.transform.position = Vector3.zero;
        Set("_player", playerObject.transform);

        // This is the runtime state created by BuildMobileSupplyPurchasePad
        // before the first (haircut) expansion is purchased. The anchor still
        // exists under the inactive root, which is the condition under test.
        GameObject hiddenSupplyPad = new GameObject("Expansion Pad Wash Rack");
        hiddenSupplyPad.transform.SetParent(_root.transform, false);
        hiddenSupplyPad.SetActive(false);
        Transform hiddenAnchor = new GameObject("Expansion Pad Stand Anchor").transform;
        hiddenAnchor.SetParent(hiddenSupplyPad.transform, false);
        Set("_mobileSupplyPadRoot", hiddenSupplyPad);
        Set("_mobileSupplyPadPoint", hiddenAnchor);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void HiddenSupplyPadDoesNotChargeBeforeHaircutExpansionIsUnlocked()
    {
        Assert.IsFalse(((GameObject)Field("_mobileSupplyPadRoot")).activeInHierarchy,
            "The first locked expansion hides the supply-rack pad in the scene.");
        Assert.IsFalse(((SalonProximityPurchasePadModel)Field("_mobileHaircutExpansionPad")).IsUnlocked);

        Invoke("UpdateMobilePurchasePad", 1f);

        SalonProximityPurchasePadModel supplyPad =
            (SalonProximityPurchasePadModel)Field("_mobileSupplyPad");
        Assert.AreEqual(0, supplyPad.Paid,
            "Walking through an invisible pad anchor must not spend the player's balance.");
        Assert.AreEqual(180, _game.Balance,
            "A hidden construction point must not consume the first-session funds.");
    }

    [Test]
    public void SupplyShelfNeverChargesAfterHaircutExpansion()
    {
        var haircut = (SalonProximityPurchasePadModel)Field("_mobileHaircutExpansionPad");
        haircut.ApplyPayment(haircut.Cost);
        Invoke("UpdateMobilePurchasePadVisual");
        Assert.IsFalse(((GameObject)Field("_mobileSupplyPadRoot")).activeInHierarchy);
        int balance = _game.Balance;
        Invoke("UpdateMobilePurchasePad", 1f);
        Assert.AreEqual(balance, _game.Balance, "The restock shelf is now introduced free.");
    }

    [Test]
    public void SupplyPadHidesAgainWhenBothExpansionsAreUnstarted()
    {
        GameObject supplyRoot = (GameObject)Field("_mobileSupplyPadRoot");
        supplyRoot.SetActive(true);

        Invoke("UpdateMobilePurchasePadVisual");

        Assert.IsFalse(supplyRoot.activeInHierarchy,
            "An unstarted supply pad stays hidden until the haircut expansion is complete.");
    }

    [Test]
    public void LegacySupplyInvestmentIsPreservedWithoutPaidPad()
    {
        var supply = (SalonProximityPurchasePadModel)Field("_mobileSupplyPad");
        supply.ApplyPayment(60);
        Invoke("UpdateMobilePurchasePadVisual");
        Assert.IsFalse(((GameObject)Field("_mobileSupplyPadRoot")).activeInHierarchy);
        Assert.AreEqual(60, supply.Paid, "Keep the legacy investment record.");
    }

    [Test]
    public void MobileProfileRetainsTheApprovedRoundAndFirstDayTarget()
    {
        DayConfig config = SalonMobileDayConfig.CreateForDay(1);

        Assert.AreEqual(180f, config.BusinessDuration, .001f);
        Assert.AreEqual(15f, config.ClosingGraceDuration, .001f);
        Assert.AreEqual(3, config.TargetOrders);
    }

    private object Field(string name)
    {
        return typeof(SalonDemo).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
    }

    private void Set(string name, object value)
    {
        typeof(SalonDemo).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);
    }

    private object Invoke(string name, params object[] args)
    {
        return typeof(SalonDemo).GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_demo, args);
    }
}
