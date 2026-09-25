using System;
using System.Collections.Generic;
using HairSalon;
using HairSalon.AssetPipeline;
using HairSalon.Character;
using HairSalon.ServiceArchitecture;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SalonDemo
{
    private bool _mobileMode;
    private SalonMobileControls _mobileControls;
    private SalonMobileQueueView _mobileQueue;
    private ISalonProgressRepository _mobileSaves;
    private SalonProgressData _mobileProgress;
    private SalonProgressData _mobileOpening;
    private CustomerModel _mobileGuidedCustomer;
    private SalonCustomerView _mobileWorkingView;
    private ServiceType _mobileWorkingService;
    private SalonTool _mobileWorkingTool;
    private float _mobileWorkElapsed;
    private float _mobileWorkDuration;
    private bool _mobileHaircutSuspended;
    private readonly List<Rect> _mobileObstacles = new List<Rect>();
    private Rect _mobileFloor;
    private Text _mobileGoalLabel;
    private GameObject _mobileGoalRoot;
    private Button _mobileRetryButton;
    private Button _mobileSaveRetryButton;
    private Button _mobileCancelGuideButton;
    private Image _mobileProgressFill;
    private string _mobileActionLabel = "靠近顾客";
    private bool _mobileActionAvailable;
    private float _mobileEvidenceAt;
    private bool _mobileRestoring;
    private bool _mobileResultFinalizedForDay;
    private bool _mobileResultSavePending;
    private SalonSupplyModel _mobileSupplies;
    private SalonProximityPurchasePadModel _mobileSupplyPad;
    private Transform _mobileSupplyStageRoot;
    private Transform _mobileSupplySourcePoint;
    private Transform _mobileWashRackPoint;
    private Transform _mobileSupplyPadPoint;
    private Transform _mobileExpandedWashRackPoint;
    private GameObject _mobileSupplyPadRoot;
    private TextMesh _mobileSupplyPadLabel;
    private GameObject _mobileExpandedRackRoot;
    private Transform _mobileExpandedRackShadowAnchor;
    private Transform _mobileCarryVisualRoot;
    private readonly List<GameObject> _mobileCarryItems = new List<GameObject>();
    private readonly List<GameObject> _mobileSourceItems = new List<GameObject>();
    private readonly List<GameObject> _mobileRackItems = new List<GameObject>();
    private readonly List<GameObject> _mobileExpandedRackItems = new List<GameObject>();
    private float _mobileSupplyTickElapsed;
    private float _mobilePadSpendElapsed;
    private bool _mobilePadWasNear;
    private bool _mobilePadInsufficientToastShown;
    private bool _mobilePadCheckpointDirty;
    private bool _mobileExpandedRackBuilt;
    private const float MobileSupplyTickInterval = .24f;
    private const int MobileSupplySourceStock = 12;
    private const int MobileSupplyCarryCapacity = 3;
    private const int MobileSupplyRackCapacity = 6;
    private const int MobileSupplyPadCost = SalonProgressData.SupplyRackExpansionCost;
    private const float MobileSupplyPadSpendRate = 60f;
    private static readonly Vector3 MobileSupplySourcePosition = new Vector3(8.35f, .2f, 1.65f);
    private static readonly Vector3 MobileWashRackPosition = new Vector3(-1.25f, .2f, 5.55f);
    // In front of the expansion rack, with enough separation from the first
    // haircut station that the player can see and stand on the pad directly.
    private static readonly Vector3 MobileSupplyPadPosition = new Vector3(-4.25f, .2f, 1.1f);
    private static readonly Vector3 MobileExpandedWashRackPosition = new Vector3(-4.1f, .2f, 2.25f);

    private enum MobileAction { None, Greet, Assign, Guide, Wash, RinseWash, Cut, StartDry, FinishDry }
    private struct MobileTarget
    {
        public MobileAction Action;
        public CustomerModel Customer;
        public int Station;
        public string Label;
        public string Hint;
        public bool Available;
    }

    /// <summary>
    /// Keeps the formal playable entry on the authored salon presentation.
    /// The procedural 2D greybox remains useful for diagnostics, but it must
    /// be explicitly requested so the browser build and the reusable Unity
    /// demo cannot silently become different games.
    /// </summary>
    public static bool ShouldUseSimple2DPresentation(string absoluteUrl)
    {
        if (string.IsNullOrEmpty(absoluteUrl)) return false;
        string url = absoluteUrl.ToLowerInvariant();
        return url.Contains("?simple2d=1") || url.Contains("&simple2d=1") ||
               url.Contains("?simple2d=true") || url.Contains("&simple2d=true");
    }

    private void ConfigureMobileGame()
    {
        // Acceptance harnesses exercise their original APIs. The playable entry always uses mobile controls.
        _mobileMode = Application.isPlaying && IsNormalGameplayMode &&
            !HairSalon.AssetPipeline.BrowserCoreFlowSmoke.IsRequested(Application.absoluteURL) &&
            !Application.absoluteURL.Contains("washCraft=detail") &&
            !Application.absoluteURL.Contains("washCraft=service");
        _simple2DMode = ShouldUseSimple2DPresentation(Application.absoluteURL);
        if (!_mobileMode) return;
        _mobileSaves = new PlayerPrefsSalonProgressRepository();
        _mobileProgress = _mobileSaves.Load() ?? SalonProgressData.CreateDefault();
        DaySettings = SalonMobileDayConfig.CreateForDay(_mobileProgress.DayNumber);
        FlowSettings.WaitingCapacity = SalonMobileDayConfig.WaitingCapacity;
        ServiceSettings = SalonMobileDayConfig.CreateServiceConfig();
        // Day 1 is the onboarding run: the first customer still creates
        // pressure, but the queue must survive a normal escort to a station.
        // Later days restore the sharper waiting pressure.
        PatienceSettings.DrainPerSecond = _mobileProgress.DayNumber <= 1 ? .85f : 1.0f;
        _mobileSupplies = new SalonSupplyModel(
            MobileSupplySourceStock, MobileSupplyCarryCapacity, MobileSupplyRackCapacity);
        _mobileSupplies.Changed += HandleMobileSupplyChanged;
        _mobileSupplyPad = new SalonProximityPurchasePadModel(
            "expansion-pad-wash-rack", MobileSupplyPadCost);
        RestoreMobileSupplyPadProgress(_mobileProgress);
    }

    /// <summary>
    /// Stage 1 uses procedural greybox furniture with stable Manifest IDs. The
    /// scene still owns the final positions, while interaction and collision
    /// anchors come from the same asset definitions used by the pipeline.
    /// </summary>
    private void BuildStage1SupplyProps(Transform root)
    {
        if (!_mobileMode || root == null) return;
        Transform stage = new GameObject("Stage1 Supply Loop").transform;
        stage.SetParent(root, false);
        _mobileSupplyStageRoot = stage;

        FurnitureShadow(stage, "furniture-supply-crate", MobileSupplySourcePosition);
        Block("Wash Supply Crate", stage, MobileSupplySourcePosition + new Vector3(0f, .42f, 0f),
            new Vector3(1.7f, .75f, 1.2f), Wood);
        Block("Supply Crate Rim", stage, MobileSupplySourcePosition + new Vector3(0f, .84f, -.02f),
            new Vector3(1.4f, .12f, .95f), Gold);
        Block("Supply Crate Label", stage, MobileSupplySourcePosition + new Vector3(0f, .67f, -.62f),
            new Vector3(.9f, .18f, .06f), TealDark);
        for (int i = 0; i < 3; i++)
        {
            GameObject item = Block("Source Wash Kit " + i, stage,
                MobileSupplySourcePosition + new Vector3((i - 1) * .38f, .98f, -.05f),
                new Vector3(.27f, .3f, .27f), i == 1 ? Gold : Teal);
            _mobileSourceItems.Add(item);
        }

        FurnitureShadow(stage, "furniture-wash-supply-rack", MobileWashRackPosition);
        Block("Wash Supply Rack Back", stage, MobileWashRackPosition + new Vector3(0f, 1.0f, .2f),
            new Vector3(2.25f, 1.85f, .3f), Wood);
        Block("Wash Supply Rack Shelf", stage, MobileWashRackPosition + new Vector3(0f, .32f, -.12f),
            new Vector3(2.2f, .14f, .75f), Gold);
        Block("Wash Supply Rack Label", stage, MobileWashRackPosition + new Vector3(0f, 1.55f, -.02f),
            new Vector3(1.5f, .16f, .08f), Teal);
        for (int i = 0; i < MobileSupplyRackCapacity; i++)
        {
            float x = (i % 3 - 1) * .53f;
            float z = i < 3 ? -.28f : .08f;
            GameObject item = Block("Rack Wash Kit " + i, stage,
                MobileWashRackPosition + new Vector3(x, .57f + (i >= 3 ? .32f : 0f), z),
                new Vector3(.34f, .26f, .28f), i % 2 == 0 ? Gold : Teal);
            _mobileRackItems.Add(item);
        }

        _mobileSupplySourcePoint = new GameObject("Wash Supply Pickup Anchor").transform;
        _mobileSupplySourcePoint.SetParent(stage, false);
        _mobileSupplySourcePoint.position = MobileSupplySourcePosition + new Vector3(0f, 0f, -.95f);
        _mobileWashRackPoint = new GameObject("Wash Supply Dropoff Anchor").transform;
        _mobileWashRackPoint.SetParent(stage, false);
        _mobileWashRackPoint.position = MobileWashRackPosition + new Vector3(0f, 0f, -.95f);
        BuildMobileSupplyPurchasePad(stage);
        if (_mobileSupplyPad != null && _mobileSupplyPad.IsUnlocked)
            BuildExpandedWashRackVisual();
        UpdateMobileSupplyVisuals();
    }

    private void BuildMobileSupplyPurchasePad(Transform stage)
    {
        if (_mobileSupplyPad == null || stage == null) return;
        _mobileSupplyPadRoot = new GameObject("Expansion Pad Wash Rack");
        _mobileSupplyPadRoot.transform.SetParent(stage, false);
        _mobileSupplyPadRoot.transform.localPosition = MobileSupplyPadPosition;
        try
        {
            AssetDefinition padAsset = AssetManifestLoader.LoadFromResources().Find("expansion-pad-wash-rack");
            if (padAsset?.Shadow != null && padAsset.Shadow.Enabled)
                ContactShadow.Apply(_mobileSupplyPadRoot.transform, padAsset.Shadow,
                    padAsset.Sorting?.Order - 1 ?? -1);
        }
        catch (Exception)
        {
            // Manifest validation is a build gate; a missing optional shadow
            // must not prevent the gameplay pad from being usable in a dev scene.
        }
        Cylinder("Expansion Pad Disc", _mobileSupplyPadRoot.transform,
            new Vector3(0f, .04f, 0f), new Vector3(1.55f, .08f, 1.55f), Gold);
        Block("Expansion Pad Ring Front", _mobileSupplyPadRoot.transform,
            new Vector3(0f, .1f, -.78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block("Expansion Pad Ring Back", _mobileSupplyPadRoot.transform,
            new Vector3(0f, .1f, .78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block("Expansion Pad Ring Left", _mobileSupplyPadRoot.transform,
            new Vector3(-.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        Block("Expansion Pad Ring Right", _mobileSupplyPadRoot.transform,
            new Vector3(.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        _mobileSupplyPadPoint = new GameObject("Expansion Pad Stand Anchor").transform;
        _mobileSupplyPadPoint.SetParent(_mobileSupplyPadRoot.transform, false);
        _mobileSupplyPadPoint.localPosition = Vector3.zero;
        var labelObject = new GameObject("Expansion Pad Label");
        labelObject.transform.SetParent(_mobileSupplyPadRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        _mobileSupplyPadLabel = labelObject.AddComponent<TextMesh>();
        _mobileSupplyPadLabel.alignment = TextAlignment.Center;
        _mobileSupplyPadLabel.anchor = TextAnchor.MiddleCenter;
        _mobileSupplyPadLabel.characterSize = .1f;
        _mobileSupplyPadLabel.fontSize = 36;
        _mobileSupplyPadLabel.color = Cream;
        UpdateMobilePurchasePadVisual();
    }

    private void BuildExpandedWashRackVisual()
    {
        if (!_mobileMode || _mobileExpandedRackBuilt || _mobileSupplyStageRoot == null) return;
        Transform stage = _mobileSupplyStageRoot;
        _mobileExpandedRackShadowAnchor = FurnitureShadow(stage, "furniture-wash-supply-rack",
            MobileExpandedWashRackPosition);
        _mobileExpandedRackRoot = new GameObject("Expanded Wash Supply Rack");
        _mobileExpandedRackRoot.transform.SetParent(stage, false);
        _mobileExpandedRackRoot.transform.localPosition = MobileExpandedWashRackPosition;
        Block("Expanded Wash Supply Rack Back", _mobileExpandedRackRoot.transform,
            new Vector3(0f, 1.0f, .2f), new Vector3(2.25f, 1.85f, .3f), Wood);
        Block("Expanded Wash Supply Rack Shelf", _mobileExpandedRackRoot.transform,
            new Vector3(0f, .32f, -.12f), new Vector3(2.2f, .14f, .75f), Gold);
        Block("Expanded Wash Supply Rack Label", _mobileExpandedRackRoot.transform,
            new Vector3(0f, 1.55f, -.02f), new Vector3(1.5f, .16f, .08f), Teal);
        for (int i = 0; i < MobileSupplyRackCapacity; i++)
        {
            float x = (i % 3 - 1) * .53f;
            float z = i < 3 ? -.28f : .08f;
            GameObject item = Block("Expanded Rack Wash Kit " + i, _mobileExpandedRackRoot.transform,
                new Vector3(x, .57f + (i >= 3 ? .32f : 0f), z),
                new Vector3(.34f, .26f, .28f), i % 2 == 0 ? Gold : Teal);
            _mobileExpandedRackItems.Add(item);
        }
        _mobileExpandedWashRackPoint = new GameObject("Expanded Wash Supply Dropoff Anchor").transform;
        _mobileExpandedWashRackPoint.SetParent(_mobileExpandedRackRoot.transform, false);
        _mobileExpandedWashRackPoint.localPosition = new Vector3(0f, 0f, -.95f);
        _mobileExpandedRackBuilt = true;
        UpdateMobileSupplyVisuals();
        if (_mobileControls != null) BuildMobileCollisionMap();
    }

    private void RemoveExpandedWashRackVisual()
    {
        if (_mobileExpandedRackRoot != null)
        {
            _mobileExpandedRackRoot.SetActive(false);
            DestroyMobileObject(_mobileExpandedRackRoot);
        }
        if (_mobileExpandedRackShadowAnchor != null)
        {
            _mobileExpandedRackShadowAnchor.gameObject.SetActive(false);
            DestroyMobileObject(_mobileExpandedRackShadowAnchor.gameObject);
        }
        _mobileExpandedRackRoot = null;
        _mobileExpandedRackShadowAnchor = null;
        _mobileExpandedWashRackPoint = null;
        _mobileExpandedRackItems.Clear();
        _mobileExpandedRackBuilt = false;
        if (_mobileControls != null) BuildMobileCollisionMap();
        UpdateMobileSupplyVisuals();
    }

    private static void DestroyMobileObject(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void UpdateMobilePurchasePadVisual()
    {
        if (_mobileSupplyPadLabel == null || _mobileSupplyPad == null) return;
        _mobileSupplyPadLabel.text = _mobileSupplyPad.IsUnlocked
            ? "OPEN"
            : "BUILD " + _mobileSupplyPad.Paid + "/" + _mobileSupplyPad.Cost;
        _mobileSupplyPadLabel.color = _mobileSupplyPad.IsUnlocked ? Teal : Cream;
        if (_camera != null)
        {
            Vector3 toCamera = _mobileSupplyPadLabel.transform.position - _camera.transform.position;
            if (toCamera.sqrMagnitude > .001f)
                _mobileSupplyPadLabel.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    private void BuildStage1CarryVisual(Transform player)
    {
        if (!_mobileMode || player == null) return;
        _mobileCarryVisualRoot = new GameObject("Wash Kit Carry Stack").transform;
        _mobileCarryVisualRoot.SetParent(player, false);
        _mobileCarryVisualRoot.localPosition = Vector3.zero;
        for (int i = 0; i < MobileSupplyCarryCapacity; i++)
        {
            GameObject item = Block("Carried Wash Kit " + i, _mobileCarryVisualRoot,
                new Vector3((i - 1) * .3f, 2.15f + i * .28f, -.05f),
                new Vector3(.26f, .25f, .26f), i == 1 ? Gold : Teal);
            _mobileCarryItems.Add(item);
        }
        UpdateMobileSupplyVisuals();
    }

    private void HandleMobileSupplyChanged()
    {
        UpdateMobileSupplyVisuals();
        RefreshMobileDayPresentation();
    }

    private void UpdateMobileSupplyVisuals()
    {
        if (_mobileSupplies == null) return;
        for (int i = 0; i < _mobileCarryItems.Count; i++)
            if (_mobileCarryItems[i] != null)
                _mobileCarryItems[i].SetActive(i < _mobileSupplies.CarriedWashKits);
        for (int i = 0; i < _mobileSourceItems.Count; i++)
            if (_mobileSourceItems[i] != null)
                _mobileSourceItems[i].SetActive(i < Mathf.Min(3, _mobileSupplies.SourceWashKits));
        int primaryRackItems;
        int expandedRackItems;
        if (_mobileExpandedRackItems.Count > 0)
        {
            // The two racks are unload entrances into one shared six-item
            // stock. Split the visual stack deterministically (primary gets
            // the extra item) so the total never duplicates or disappears.
            primaryRackItems = (_mobileSupplies.WashRackWashKits + 1) / 2;
            expandedRackItems = _mobileSupplies.WashRackWashKits / 2;
        }
        else
        {
            primaryRackItems = _mobileSupplies.WashRackWashKits;
            expandedRackItems = 0;
        }
        for (int i = 0; i < _mobileRackItems.Count; i++)
            if (_mobileRackItems[i] != null)
            {
                bool visible = i < primaryRackItems;
                _mobileRackItems[i].SetActive(visible);
            }
        for (int i = 0; i < _mobileExpandedRackItems.Count; i++)
            if (_mobileExpandedRackItems[i] != null)
            {
                bool visible = i < expandedRackItems;
                _mobileExpandedRackItems[i].SetActive(visible);
            }
        UpdateMobilePurchasePadVisual();
    }

    private void UpdateMobileSupplyLoop(float dt)
    {
        if (!_mobileMode || _mobileSupplies == null || _player == null ||
            !CanInteractWithSalon() || _mobileSupplySourcePoint == null || _mobileWashRackPoint == null)
            return;

        bool nearSource = FlatDistance(_player.position, _mobileSupplySourcePoint.position) <= 1.45f;
        bool nearRack = FlatDistance(_player.position, _mobileWashRackPoint.position) <= 1.45f ||
                        (_mobileExpandedWashRackPoint != null &&
                         FlatDistance(_player.position, _mobileExpandedWashRackPoint.position) <= 1.45f);
        bool canPick = nearSource && _mobileSupplies.SourceWashKits > 0 &&
                       _mobileSupplies.CarriedWashKits < _mobileSupplies.CarryCapacity;
        bool canDeliver = nearRack && _mobileSupplies.CarriedWashKits > 0 &&
                          _mobileSupplies.WashRackWashKits < _mobileSupplies.WashRackCapacity;
        if (!canPick && !canDeliver)
        {
            _mobileSupplyTickElapsed = 0f;
            return;
        }

        _mobileSupplyTickElapsed += Mathf.Max(0f, dt);
        if (_mobileSupplyTickElapsed < MobileSupplyTickInterval) return;
        _mobileSupplyTickElapsed = 0f;
        if (canPick && _mobileSupplies.TryPickUpWashKit())
        {
            if (_mobileSupplies.CarriedWashKits == 1)
                ShowToast("洗发用品已取 · 靠近架子自动放下");
            else if (_mobileSupplies.CarriedWashKits == _mobileSupplies.CarryCapacity)
                ShowToast("容量已满 · 回洗发区");
        }
        else if (canDeliver && _mobileSupplies.TryDeliverWashKit())
        {
            if (_mobileSupplies.CarriedWashKits == 0)
                ShowToast("洗发用品已补 · 可以开始洗发");
        }
    }

    private void UpdateMobilePurchasePad(float dt)
    {
        if (!_mobileMode || _mobileSupplyPad == null || _mobileSupplyPadPoint == null ||
            _player == null || !CanInteractWithSalon())
            return;

        bool nearPad = FlatDistance(_player.position, _mobileSupplyPadPoint.position) <= 1.25f;
        if (!nearPad)
        {
            bool shouldSaveCheckpoint = _mobilePadWasNear && _mobilePadCheckpointDirty;
            _mobilePadWasNear = false;
            _mobilePadInsufficientToastShown = false;
            _mobilePadSpendElapsed = 0f;
            if (shouldSaveCheckpoint) SaveMobileCheckpoint(false);
            UpdateMobilePurchasePadVisual();
            return;
        }

        if (!_mobilePadWasNear)
        {
            _mobilePadWasNear = true;
            _mobilePadInsufficientToastShown = false;
            if (!_mobileSupplyPad.IsUnlocked)
                ShowToast("STAND ON PAD TO BUILD");
        }

        if (!_mobileSupplyPad.IsUnlocked)
        {
            _mobilePadSpendElapsed += Mathf.Max(0f, dt);
            int amount = _mobileSupplyPad.CalculatePayment(
                _mobilePadSpendElapsed, _game.Balance, MobileSupplyPadSpendRate);
            if (amount > 0)
            {
                int balanceBeforeSpend = _game.Balance;
                if (_game.Payments.TrySpend(amount))
                {
                    if (_mobileSupplyPad.ApplyPayment(amount))
                    {
                        _mobilePadSpendElapsed = SalonProximityPurchasePadModel.RetainUnspentPaymentTime(
                            _mobilePadSpendElapsed, amount, MobileSupplyPadSpendRate);
                        _mobileProgress.SupplyRackExpansionPaid = _mobileSupplyPad.Paid;
                        _mobileProgress.SupplyRackExpansionPurchased = _mobileSupplyPad.IsUnlocked;
                        _mobilePadCheckpointDirty = true;
                        if (_coinBalanceLabel != null)
                            _coinBalanceLabel.text = _game.Balance.ToString("N0");
                        if (_mobileSupplyPad.IsUnlocked)
                        {
                            BuildExpandedWashRackVisual();
                            ShowToast("SUPPLY RACK OPEN");
                            SaveMobileCheckpoint(false);
                        }
                    }
                    else
                    {
                        // Keep the wallet and the pad atomic if a future model
                        // change rejects the calculated amount.
                        _game.Payments.RestoreBalance(balanceBeforeSpend);
                        _mobilePadSpendElapsed = 0f;
                    }
                }
                else
                {
                    // A wallet rejection is a failed transaction. Do not let
                    // the elapsed time survive and become a surprise charge
                    // after the player earns more coins.
                    _mobilePadSpendElapsed = 0f;
                }
            }
            else if (_game.Balance <= 0)
            {
                _mobilePadSpendElapsed = 0f;
                if (!_mobilePadInsufficientToastShown)
                {
                    _mobilePadInsufficientToastShown = true;
                    ShowToast("金币不足 · 先完成订单");
                }
            }
        }
        UpdateMobilePurchasePadVisual();
    }

    private void RestoreMobileGame()
    {
        if (!_mobileMode) return;
        _mobileRestoring = true;
        _game.RestorePersistentState(_mobileProgress.Balance, _mobileProgress.AutoBlowPurchased,
            _mobileProgress.FirstDayComplete);
        _dayController.RestoreReputation(_mobileProgress.ReputationStars);
        _satisfaction.BeginDay(_mobileProgress.ShopSatisfaction);
        _dayController.PrepareDay(_mobileProgress.DayNumber);
        if (_mobileProgress.DaySettled)
            _dayController.RestoreClosedManagement(_mobileProgress.DayNumber, _mobileProgress.ReputationStars);
        _mobileRestoring = false;
        _mobileOpening = CaptureMobileProgress(false);
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        RefreshMobileDayPresentation();
        if (!_mobileProgress.DaySettled) SaveMobileCheckpoint(false);
    }

    private void RestoreMobileSupplyPadProgress(SalonProgressData data)
    {
        if (_mobileSupplyPad == null || data == null) return;
        int paid = Mathf.Clamp(data.SupplyRackExpansionPaid, 0, MobileSupplyPadCost);
        if (data.SupplyRackExpansionPurchased) paid = MobileSupplyPadCost;
        if (paid > 0) _mobileSupplyPad.ApplyPayment(paid);
    }

    private void BuildMobileHud(Transform safeParent)
    {
        if (!_mobileMode) return;
        _mobileControls = SalonMobileControls.Create(safeParent);
        _mobileQueue = SalonMobileQueueView.Create(safeParent);
        // Modals are built afterwards, above all touch targets.
        _mobileGoalLabel = UiLabel("", safeParent, 26, Cream, TextAnchor.MiddleCenter,
            new Vector2(-150f, -168f), new Vector2(420f, 58f), DarkWood, new Vector2(.5f, 1f));
        _mobileGoalRoot = _mobileGoalLabel.transform.parent.gameObject;
        ((RectTransform)_businessClockLabel.transform.parent).anchoredPosition = new Vector2(210f, -168f);
        SalonUiFactory.MakeClickThrough(_mobileGoalRoot);
        GameObject track = UiPanel("Mobile Service Progress", safeParent, DarkWood,
            new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 145f), new Vector2(480f, 18f));
        _mobileProgressFill = UiPanel("Fill", track.transform, Teal, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero).GetComponent<Image>();
        _mobileProgressFill.type = Image.Type.Filled;
        _mobileProgressFill.fillMethod = Image.FillMethod.Horizontal;
        _mobileProgressFill.fillAmount = 0f;
        track.SetActive(false);
        SalonUiFactory.MakeClickThrough(track);
        _mobileCancelGuideButton = UiButton("取消接待", safeParent, new Vector2(.5f, 0f),
            new Vector2(0f, 190f), new Vector2(230f, 70f), DarkWood, () =>
            {
                _mobileGuidedCustomer = null;
                _game.ClearFocus();
            });
        _mobileCancelGuideButton.gameObject.SetActive(false);
        BuildMobileCollisionMap();
    }

    private static void SetTopAnchor(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
    }

    private void BuildMobileResultControls()
    {
        if (!_mobileMode) return;
        Transform surface = _resultSummaryLabel.transform.parent;
        _mobileRetryButton = UiButton("再试一次", surface, new Vector2(.5f, .5f),
            new Vector2(0f, -335f), new Vector2(350f, 76f), Teal, RetryMobileDay);
        _mobileRetryButton.gameObject.SetActive(false);
        _mobileSaveRetryButton = UiButton("重试保存", surface, new Vector2(.5f, .5f),
            new Vector2(0f, -245f), new Vector2(350f, 76f), Teal, RetrySaveMobileCheckpoint);
        _mobileSaveRetryButton.gameObject.SetActive(false);
    }

    private void BuildMobileCollisionMap()
    {
        const float bodyRadius = .15f;
        _mobileObstacles.Clear();
        foreach (var obstacle in FindObjectsByType<SalonFurnitureObstacle>(FindObjectsInactive.Include))
            if (obstacle.gameObject.activeInHierarchy && obstacle.transform != _mobileExpandedRackShadowAnchor)
                _mobileObstacles.Add(obstacle.WorldBounds(bodyRadius));
        foreach (var station in _cutStations.Values)
        {
            var layout = station.Layout;
            Vector2 center = new Vector2(layout.Origin.x, layout.Origin.z) + layout.Collision.Center;
            Vector2 half = layout.Collision.Size * .5f + Vector2.one * bodyRadius;
            _mobileObstacles.Add(Rect.MinMaxRect(center.x - half.x, center.y - half.y,
                center.x + half.x, center.y + half.y));
        }
        // Room limits follow the floor already built by the scene, not another coordinate map.
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (var renderer in GameObject.Find("Fixed Salon Map").GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name != "Floor Tile") continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        _mobileFloor = Rect.MinMaxRect(bounds.min.x + bodyRadius, bounds.min.z + bodyRadius,
            bounds.max.x - bodyRadius, bounds.max.z - bodyRadius);
    }

    private SalonProgressData CaptureMobileProgress(bool settled)
    {
        var data = (_mobileProgress ?? SalonProgressData.CreateDefault()).Clone();
        data.DayNumber = _dayController.DayNumber;
        data.Balance = _game.Balance;
        data.AutoBlowPurchased = _game.HasAutoBlowStand;
        data.SupplyRackExpansionPurchased = _mobileSupplyPad != null && _mobileSupplyPad.IsUnlocked;
        data.SupplyRackExpansionPaid = _mobileSupplyPad == null ? 0 : _mobileSupplyPad.Paid;
        data.FirstDayComplete = _game.FirstDayCompleteForShop;
        data.ReputationStars = _dayController.Reputation.CurrentStars;
        data.ShopSatisfaction = _satisfaction.CurrentSatisfaction;
        data.DaySettled = settled;
        return data;
    }

    private bool SaveMobileCheckpoint(bool settled)
    {
        if (!_mobileMode || _mobileRestoring || _mobileSaves == null) return false;
        _mobileProgress = CaptureMobileProgress(settled);
        bool saved = _mobileSaves.Save(_mobileProgress);
        if (!saved)
            ShowToast("本次进度无法保存，请保留当前页面");
        if (saved) _mobilePadCheckpointDirty = false;
        return saved;
    }

    private void HandleMobileDayState(DayState state)
    {
        if (!_mobileMode) return;
        _mobileControls?.ResetInput();
        if (state == DayState.PreOpen)
        {
            _mobileResultFinalizedForDay = false;
            _mobileResultSavePending = false;
            SalonMobileDayConfig.ApplyForDay(DaySettings, _dayController.DayNumber);
            _mobileSupplies?.ResetDay(MobileSupplySourceStock);
            _mobileSupplyTickElapsed = 0f;
            _nextCustomerId = 0;
            _satisfaction.BeginDay(_satisfaction.CurrentSatisfaction);
            _mobileGuidedCustomer = null;
            EndMobileWork();
            if (_player != null) _player.position = new Vector3(0f, .05f, -3.2f);
            if (!_mobileRestoring)
            {
                _mobileOpening = CaptureMobileProgress(false);
                SaveMobileCheckpoint(false);
            }
        }
        if (state == DayState.Result)
        {
            if (_mobileResultFinalizedForDay)
            {
                RefreshMobileDayPresentation();
                return;
            }
            _mobileResultFinalizedForDay = true;
            EndMobileWork();
            _mobileGuidedCustomer = null;
            bool passed = _dayController.Stats.CompletedOrders >= DaySettings.TargetOrders;
            _resultTitleLabel.text = "DAY " + _dayController.DayNumber + (passed ? "  目标达成" : "  本日已结算");
            _resultContinueButton.gameObject.SetActive(true);
            _mobileRetryButton.gameObject.SetActive(false);
            if (_mobileSaveRetryButton != null) _mobileSaveRetryButton.gameObject.SetActive(false);
            bool saved = false;
            if (_mobileProgress != null)
            {
                int index = _mobileProgress.CompletedDays.IndexOf(_dayController.DayNumber);
                if (index < 0)
                {
                    _mobileProgress.CompletedDays.Add(_dayController.DayNumber);
                    _mobileProgress.BestCompletedOrders.Add(_dayController.Stats.CompletedOrders);
                }
                else _mobileProgress.BestCompletedOrders[index] = Mathf.Max(
                    _mobileProgress.BestCompletedOrders[index], _dayController.Stats.CompletedOrders);
                saved = SaveMobileCheckpoint(true);
            }
            _mobileResultSavePending = !saved;
            if (_mobileSaveRetryButton != null)
                _mobileSaveRetryButton.gameObject.SetActive(_mobileResultSavePending);
            _resultSummaryLabel.text = "完成订单   " + _dayController.Stats.CompletedOrders + " / " + DaySettings.TargetOrders +
                "\n\n" + (passed ? "忙碌的一天完成了，去升级设备吧！" : "本日营业已结束，收入和投入进度已保留。") +
                "\n\n订单收入   " + _dayController.Stats.OrderIncome +
                "\n小费收入   " + _dayController.Stats.TipIncome +
                "\n离店顾客   " + _game.AngryLeaves +
                "\n\n" + (saved ? "进度已保存" : "保存失败，请保留当前页面");
        }
        RefreshMobileDayPresentation();
    }

    private void RetrySaveMobileCheckpoint()
    {
        if (!_mobileMode || !_mobileResultFinalizedForDay || !_mobileResultSavePending) return;
        bool saved = SaveMobileCheckpoint(true);
        if (!saved) return;
        _mobileResultSavePending = false;
        if (_mobileSaveRetryButton != null) _mobileSaveRetryButton.gameObject.SetActive(false);
        if (_resultSummaryLabel != null)
            _resultSummaryLabel.text = _resultSummaryLabel.text.Replace("保存失败，请保留当前页面", "进度已保存");
    }

    private void RefreshMobileDayPresentation()
    {
        if (!_mobileMode || _dayController == null) return;
        if (_dayController.State == DayState.PreOpen && _preOpenInfoLabel != null)
        {
            _preOpenInfoLabel.fontSize = 24;
            _preOpenInfoLabel.text = "3 分钟 · 完成 " + DaySettings.TargetOrders + " 单\n左摇杆移动 · 右按钮就近操作\n先取洗发用品，再去洗发区";
        }
        bool playable = CanInteractWithSalon();
        if (_closingLabel != null) _closingLabel.transform.parent.gameObject.SetActive(false);
        _mobileControls?.SetVisible(playable);
        _mobileQueue?.SetVisible(playable);
        _mobileQueue?.Refresh(_game.Customers);
        if (_mobileGoalLabel != null)
        {
            _mobileGoalRoot.SetActive(playable);
            string phase = _dayController.State == DayState.ClosingGrace ? "收尾" :
                _dayController.BusinessProgress >= .7f ? "高峰" :
                _dayController.BusinessProgress >= .4f ? "忙碌" : "接待";
            string supply = _mobileSupplies == null ? string.Empty :
                "  ·  用品 " + _mobileSupplies.CarriedWashKits + "/" + _mobileSupplies.CarryCapacity +
                "  架 " + _mobileSupplies.WashRackWashKits + "/" + _mobileSupplies.WashRackCapacity;
            string pad = _mobileSupplyPad == null ? string.Empty :
                (_mobileSupplyPad.IsUnlocked
                    ? "  ·  BUILD OPEN"
                    : "  ·  BUILD " + _mobileSupplyPad.Paid + "/" + _mobileSupplyPad.Cost);
            _mobileGoalLabel.text = "目标 " + _dayController.Stats.CompletedOrders + "/" + DaySettings.TargetOrders + " 单  ·  " + phase + supply + pad;
        }
        if (_mobileCancelGuideButton != null)
            _mobileCancelGuideButton.gameObject.SetActive(playable && _mobileGuidedCustomer != null);
        if (_mobileProgressFill != null && !playable) _mobileProgressFill.transform.parent.gameObject.SetActive(false);
    }

    private void RetryMobileDay()
    {
        if (!_mobileMode || _dayController.State != DayState.Result || _mobileOpening == null) return;
        var checkpoint = _mobileOpening.Clone();
        _mobileRestoring = true;
        ClearCustomerViews();
        _game.ResetForNextDay();
        _game.RestorePersistentState(checkpoint.Balance, checkpoint.AutoBlowPurchased, checkpoint.FirstDayComplete);
        _dayController.RestoreReputation(checkpoint.ReputationStars);
        _satisfaction.BeginDay(checkpoint.ShopSatisfaction);
        _mobileProgress = checkpoint;
        _mobileSupplyPad = new SalonProximityPurchasePadModel(
            "expansion-pad-wash-rack", MobileSupplyPadCost);
        RestoreMobileSupplyPadProgress(checkpoint);
        if (_mobileSupplyPad.IsUnlocked)
            BuildExpandedWashRackVisual();
        else if (_mobileExpandedRackBuilt)
            RemoveExpandedWashRackVisual();
        UpdateMobilePurchasePadVisual();
        _mobilePadSpendElapsed = 0f;
        _mobilePadWasNear = false;
        _mobilePadInsufficientToastShown = false;
        _mobilePadCheckpointDirty = false;
        _dayController.PrepareDay(checkpoint.DayNumber);
        _mobileRestoring = false;
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        SaveMobileCheckpoint(false);
        ApplyOverview(true);
    }

    private void UpdateMobilePlay(float dt)
    {
        if (_mobileControls == null) return;
        ClearStaleMobileGuidedCustomer();
        if (_mobileWorkingView != null)
        {
            TickMobileWork(dt);
            _mobileControls.ConsumeInteractionPressed();
            return;
        }
        Vector3 before = _player.position;
        Vector3 cameraRight = _simple2DMode ? Vector3.right : _camera.transform.right;
        Vector3 cameraForward = _simple2DMode ? Vector3.forward : _camera.transform.forward;
        Vector3 next = SalonMobileNavigation.Move(before, _mobileControls.Move,
            cameraRight, cameraForward, 5.5f * dt, _mobileFloor, _mobileObstacles);
        if (_playerCharacter != null)
        {
            _playerCharacter.Move(next - before, dt);
            _player.position = next;
        }
        else _player.position = next;
        _playerTarget = next;
        UpdateMobileSupplyLoop(dt);
        UpdateMobilePurchasePad(dt);
        MobileTarget target = FindMobileTarget();
        _mobileActionLabel = target.Label;
        _mobileActionAvailable = target.Available;
        _mobileControls.SetInteraction(target.Label, target.Available);
        _mobileControls.SetHint(target.Hint);
        foreach (var pair in _selectionPlates)
            pair.Value.SetActive(target.Available && target.Station == pair.Key && target.Action != MobileAction.Greet);
        if (_mobileControls.ConsumeInteractionPressed() && target.Available) ExecuteMobileTarget(target);
    }

    private MobileTarget FindMobileTarget()
    {
        ClearStaleMobileGuidedCustomer();
        var target = new MobileTarget { Station = -1, Label = "靠近顾客", Hint = "移动到等候顾客身边，点右侧按钮接待" };
        float nearest = float.PositiveInfinity;
        bool hasTarget = false;
        bool nearestAvailable = false;
        if (_mobileGuidedCustomer != null)
        {
            MobileTarget guided = new MobileTarget
            {
                Customer = _mobileGuidedCustomer,
                Station = -1,
                Action = MobileAction.Assign
            };
            string service = MobileServiceName(_mobileGuidedCustomer.CurrentNeed);
            guided.Hint = "已接待 " + (_mobileGuidedCustomer.Id + 1) + " 号 · 前往空闲" + service + "工位安排";
            guided.Label = "前往" + service + "工位";
            int nearbyStation = -1;
            float nearbyDistance = float.PositiveInfinity;
            int fallbackCompatibleStation = -1;
            float fallbackCompatibleDistance = float.PositiveInfinity;
            int fallbackAnyStation = -1;
            float fallbackAnyDistance = float.PositiveInfinity;
            foreach (var pair in _playerServiceAnchors)
            {
                // A free station is a physically valid hand-off even when it
                // is the wrong service type. The model owns the consequence
                // (confused reaction, one wrong-station penalty and a later
                // correction path); the mobile target must not erase that
                // already-approved mistake space.
                if (_game.IsStationOccupied(pair.Key)) continue;
                float distance = FlatDistance(_player.position, pair.Value.position);
                if (distance < fallbackAnyDistance)
                {
                    fallbackAnyDistance = distance;
                    fallbackAnyStation = pair.Key;
                }
                bool compatible = SalonGameModel.IsCompatibleStation(
                    _mobileGuidedCustomer.CurrentNeed, _game.Workstations[pair.Key].Type);
                if (compatible && distance < fallbackCompatibleDistance)
                {
                    fallbackCompatibleDistance = distance;
                    fallbackCompatibleStation = pair.Key;
                }
                if (distance <= 1.5f && distance < nearbyDistance)
                {
                    nearbyDistance = distance;
                    nearbyStation = pair.Key;
                }
            }
            bool hasNearbyStation = nearbyStation >= 0;
            guided.Station = hasNearbyStation ? nearbyStation :
                fallbackCompatibleStation >= 0 ? fallbackCompatibleStation : fallbackAnyStation;
            float best = hasNearbyStation ? nearbyDistance :
                fallbackCompatibleStation >= 0 ? fallbackCompatibleDistance : fallbackAnyDistance;
            if (guided.Station < 0)
            {
                guided.Label = "等待空闲" + service + "工位";
                guided.Hint = "已接待 " + (_mobileGuidedCustomer.Id + 1) + " 号 · 兼容工位正在使用中";
            }
            else if (hasNearbyStation)
            {
                WorkstationType targetType = _game.Workstations[guided.Station].Type;
                bool compatible = SalonGameModel.IsCompatibleStation(
                    _mobileGuidedCustomer.CurrentNeed, targetType);
                guided.Label = compatible
                    ? "安排" + service
                    : "安排" + MobileWorkstationName(targetType);
                guided.Hint = compatible
                    ? "已接待 " + (_mobileGuidedCustomer.Id + 1) + " 号 · 前往空闲" + service + "工位安排"
                    : "已接待 " + (_mobileGuidedCustomer.Id + 1) + " 号 · 当前需求" + service + "，错误工位仍可安排";
                guided.Available = true;
            }
            // Preserve the reception state and next step even while the
            // player is travelling. Previously this target was discarded
            // outside the anchor radius and the button reverted to the
            // misleading generic "靠近顾客" prompt.
            target = guided;
            nearest = best;
            nearestAvailable = guided.Available;
            hasTarget = true;
        }
        foreach (var view in _customerViews)
        {
            if (view == null || view.Customer == null) continue;
            CustomerModel customer = view.Customer;
            if (customer.State != CustomerState.Waiting && customer.State != CustomerState.Serving) continue;
            bool waiting = customer.State == CustomerState.Waiting;
            // Keep the guided customer, but allow existing services to finish
            // so the player can free an occupied chair without cancelling reception.
            if (_mobileGuidedCustomer != null && (waiting || !SalonGameModel.IsCompatibleStation(
                customer.CurrentNeed, _game.Workstations[customer.Station].Type))) continue;
            Vector3 anchor = !waiting && _playerServiceAnchors.TryGetValue(customer.Station, out var work)
                ? work.position : view.transform.position;
            float distance = FlatDistance(_player.position, anchor);
            if (distance > 1.8f || (!waiting && distance > 1.45f)) continue;

            MobileTarget candidate = new MobileTarget
            {
                Customer = customer,
                Station = customer.Station,
                // Waiting customers are interactable as soon as they are
                // visible in the queue. Their slot can move when the person
                // ahead leaves; blocking the greeting during that movement
                // made the second customer appear permanently unavailable.
                Available = waiting || view.IsAtMovementDestination
            };
            if (!waiting && !view.IsAtMovementDestination)
            {
                candidate.Label = "顾客入座中";
                candidate.Hint = "等顾客到达座位后即可操作";
            }
            else
            {
                candidate.Hint = (customer.Id + 1) + " 号 · " + MobileServiceName(customer.CurrentNeed);
                if (waiting)
                {
                    candidate.Action = MobileAction.Greet;
                    candidate.Label = "接待 " + (customer.Id + 1) + " 号";
                }
                else if (!SalonGameModel.IsCompatibleStation(customer.CurrentNeed, _game.Workstations[customer.Station].Type))
                {
                    candidate.Action = MobileAction.Guide;
                    bool transferReady = HasFreeCompatibleStation(customer.CurrentNeed);
                    candidate.Available = transferReady;
                    candidate.Label = transferReady ? "转移顾客" : "等待空闲工位";
                    candidate.Hint += transferReady ? " · 带到下一工位" : " · 兼容工位正在使用中";
                }
                else if (customer.CurrentNeed == ServiceType.Wash)
                {
                    if (customer.ActiveServiceAction == ActiveServiceAction.Shampoo)
                    {
                        candidate.Action = MobileAction.Wash;
                        candidate.Available = false;
                        candidate.Label = "洗发中";
                        candidate.Hint += " · 正在打泡沫";
                    }
                    else if (_game.IsWashFoamWaitRunning(customer))
                    {
                        // 后台泡沫等待：这是玩家可以离开、之后必须回来处理的节奏点。
                        candidate.Action = MobileAction.RinseWash;
                        bool ready = _game.IsWashFoamReadyToRinse(customer);
                        candidate.Available = ready;
                        candidate.Label = ready ? "冲洗" : "泡沫中";
                        candidate.Hint += ready ? " · 回来冲洗收尾" : " · 可以先照顾其他顾客";
                    }
                    else
                    {
                        candidate.Action = MobileAction.Wash;
                        candidate.Label = "洗发";
                        candidate.Hint += _mobileSupplies != null && !_mobileSupplies.CanStartWash
                            ? " · 先去洗发区补"
                            : " · 打好泡沫后可离开";
                    }
                }
                else if (customer.CurrentNeed == ServiceType.Cut)
                {
                    candidate.Action = MobileAction.Cut;
                    candidate.Label = "剪发";
                    candidate.Hint += " · 按住到绿色区间再松手";
                }
                else if (customer.CurrentNeed == ServiceType.Dry)
                {
                    bool running = customer.AutoBlowRunning || customer.AutoBlowSafetyStopped;
                    bool ready = running && customer.BackgroundTask.Elapsed >= customer.BackgroundTask.IdealStart;
                    candidate.Action = running ? MobileAction.FinishDry : MobileAction.StartDry;
                    candidate.Available = !running || ready;
                    candidate.Label = !running ? "启动吹发" : ready ? "吹发收尾" : "吹发运行中";
                    candidate.Hint += running ? ready ? " · 回来收尾即可完成" : " · 可以先照顾其他顾客" : " · 启动后可离开";
                }
            }

            if (!hasTarget || ShouldPreferMobileTarget(candidate.Available, distance,
                nearestAvailable, nearest))
            {
                target = candidate;
                nearest = distance;
                nearestAvailable = candidate.Available;
                hasTarget = true;
            }
        }
        return target;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private void ClearStaleMobileGuidedCustomer()
    {
        if (_mobileGuidedCustomer == null) return;
        if (_mobileGuidedCustomer.State == CustomerState.Leaving ||
            _mobileGuidedCustomer.State == CustomerState.Exited)
            _mobileGuidedCustomer = null;
    }

    private bool HasFreeCompatibleStation(ServiceType service)
    {
        if (_game == null) return false;
        for (int station = 0; station < _game.Workstations.Count; station++)
        {
            if (!SalonGameModel.IsCompatibleStation(service, _game.Workstations[station].Type)) continue;
            if (!_game.IsStationOccupied(station)) return true;
        }
        return false;
    }

    private static string MobileServiceName(ServiceType service)
        => service == ServiceType.Wash ? "洗发" : service == ServiceType.Cut ? "剪发" : "吹发";

    private static string MobileWorkstationName(WorkstationType type)
        => type == WorkstationType.Wash ? "洗发工位" :
            type == WorkstationType.Haircut ? "剪发工位" : "烫发工位";

    private void ExecuteMobileTarget(MobileTarget target)
    {
        CustomerModel customer = target.Customer;
        if (customer == null) return;
        _game.SelectCustomer(customer);
        if (target.Action == MobileAction.Greet)
        {
            if (!_game.EngageCustomerForHandoff(customer))
            {
                ShowToast("顾客正在离店或已被接待");
                return;
            }
            _mobileGuidedCustomer = customer;
            ShowToast("接待成功 · 去空闲" + MobileServiceName(customer.CurrentNeed) + "工位");
        }
        else if (target.Action == MobileAction.Guide)
        {
            // A completed wash can be Serving while it waits for its next
            // compatible station. It already has service engagement, so the
            // Waiting-only hand-off guard used by Greet must not reject this
            // transfer.
            _mobileGuidedCustomer = customer;
            ShowToast("转移顾客 · 去空闲" + MobileServiceName(customer.CurrentNeed) + "工位");
        }
        else if (target.Action == MobileAction.Assign)
        {
            // A multi-service order can reach its haircut step only after the
            // wash is finished. Initialize the same deterministic tool rhythm
            // at that hand-off, before Assign creates its default service.
            if (_mobileMode && customer.CurrentNeed == ServiceType.Cut && customer.HaircutService == null)
                _game.ConfigureHaircutOrder(customer,
                    SalonMobileDayConfig.GetHaircutToolsForSpawn(DaySettings.MobileDayNumber, customer.Id));
            bool wrongStation = target.Station < 0 || target.Station >= _game.Workstations.Count ||
                !SalonGameModel.IsCompatibleStation(customer.CurrentNeed,
                    _game.Workstations[target.Station].Type);
            if (_game.Assign(customer, target.Station))
            {
                _mobileGuidedCustomer = null;
                ShowToast(wrongStation ? "顾客被安排到了错误工位" : "顾客正在入座");
            }
        }
        else if (target.Action == MobileAction.StartDry)
        {
            if (_game.StartAutoBlow(customer)) ShowToast("吹发已启动 · 可以照顾下一位");
        }
        else if (target.Action == MobileAction.FinishDry)
        {
            BlowResult result = _game.FinishAutoBlow(customer);
            ShowToast(result == BlowResult.Good ? "吹发完成！" : "收尾完成 · 下次早一点回来");
        }
        else if (target.Action == MobileAction.RinseWash)
        {
            if (_game.FinishWashRinse(customer))
                ShowToast(customer.IsComplete ? "冲洗完成！" : "冲洗完成 · 继续下一项");
            else ShowToast("还没到冲洗时间");
        }
        else if (target.Action == MobileAction.Cut || target.Action == MobileAction.Wash)
        {
            SalonCustomerView view = FindCustomerView(customer);
            if (view == null || !view.IsAtMovementDestination) return;
            bool wash = target.Action == MobileAction.Wash;
            bool supplyEnabled = wash && _mobileSupplies != null;
            if (supplyEnabled && !_mobileSupplies.CanStartWash)
            {
                ShowToast("洗发用品用完了 · 去洗发区补");
                return;
            }
            _mobileWorkingTool = customer.HaircutService == null ? SalonTool.Scissors : customer.HaircutService.CurrentRequiredTool;
            bool began = wash
                ? _game.BeginWashFoamHold(customer)
                : _game.BeginHaircutAction(customer, _mobileWorkingTool, HaircutSettings);
            if (!began) { ShowToast("顾客尚未准备好"); return; }
            if (supplyEnabled && !_mobileSupplies.TryConsumeWashKit())
            {
                _game.CancelActiveServiceAction(customer);
                ShowToast("洗发用品不足 · 请先补");
                return;
            }
            _mobileWorkingView = view;
            _mobileWorkingService = wash ? ServiceType.Wash : ServiceType.Cut;
            _mobileWorkElapsed = 0f;
            _mobileHaircutSuspended = false;
            _mobileWorkDuration = wash
                ? ServiceSettings.ShampooDuration
                : HaircutSettings.GetPerfectMax(_mobileWorkingTool);
            _playerCharacter?.FaceTowards(view.transform.position - _player.position);
            _playerCharacter?.BeginService(wash
                ? HairdresserAnimationState.WashHair : HairdresserAnimationState.CutHair);
            if (!wash) view.HaircutFeedback?.BeginHold(_mobileWorkingTool);
        }
        RefreshMobileDayPresentation();
    }

    private void TickMobileWork(float dt)
    {
        var view = _mobileWorkingView;
        if (view == null || view.Customer.State == CustomerState.Leaving || view.Customer.State == CustomerState.Exited)
        { EndMobileWork(); return; }

        if (_mobileWorkingService == ServiceType.Cut)
        {
            TickMobileHaircut(view, dt);
            return;
        }

        _mobileWorkElapsed += dt;
        float progress = Mathf.Clamp01(_mobileWorkElapsed / Mathf.Max(.1f, _mobileWorkDuration));
        _mobileActionLabel = MobileServiceName(_mobileWorkingService) + "中";
        _mobileActionAvailable = false;
        _mobileControls.SetInteraction(_mobileActionLabel, false);
        _mobileControls.SetHint("正在" + MobileServiceName(_mobileWorkingService) + " · " + Mathf.RoundToInt(progress * 100f) + "%");
        _mobileProgressFill.transform.parent.gameObject.SetActive(true);
        _mobileProgressFill.fillAmount = progress;
        _mobileProgressFill.color = Teal;
        view.ActionProgress?.SetProgressForService(_mobileWorkingService, "", progress, Teal);
        if (_mobileWorkElapsed < _mobileWorkDuration) return;
        view.OrderDemand?.Refresh();
        ShowToast("泡沫已打好 · 可以先去照顾别人，回来冲洗");
        EndMobileWork();
    }

    private void TickMobileHaircut(SalonCustomerView view, float dt)
    {
        bool held = _mobileControls != null && _mobileControls.InteractionHeld;
        if (_mobileHaircutSuspended && !held)
        {
            _mobileActionLabel = "继续剪发";
            _mobileActionAvailable = true;
            _mobileControls.SetInteraction(_mobileActionLabel, true);
            _mobileControls.SetHint("按住继续剪发 · 绿色时松手");
            return;
        }
        _mobileHaircutSuspended = false;
        if (held) _mobileWorkElapsed += Mathf.Max(0f, dt);

        float perfectMin = Mathf.Max(.01f, HaircutSettings.GetPerfectMin(_mobileWorkingTool));
        float perfectMax = HaircutSettings.GetPerfectMax(_mobileWorkingTool);
        float progress = Mathf.Clamp01(_mobileWorkElapsed / perfectMax);
        bool ready = _mobileWorkElapsed >= perfectMin;
        Color progressColor = ready ? SalonPalette.Success : SalonPalette.Warning;
        _mobileActionLabel = ready ? "松手完成" : "按住剪发";
        _mobileActionAvailable = true;
        _mobileControls.SetInteraction(_mobileActionLabel, true);
        _mobileControls.SetHint(ready ? "现在松手 · 剪发完成" : "正在剪发 · 绿色时松手");
        _mobileProgressFill.transform.parent.gameObject.SetActive(true);
        _mobileProgressFill.fillAmount = progress;
        _mobileProgressFill.color = progressColor;
        view.ActionProgress?.SetProgressForService(ServiceType.Cut, "", progress, progressColor);
        view.HaircutFeedback?.TickHold(_mobileWorkElapsed, progress);

        // Releasing resolves the same undercut/perfect window as the desktop
        // hold interaction. Holding past the upper bound resolves overcut on
        // the first frame after the window closes.
        if (held && _mobileWorkElapsed <= perfectMax) return;
        ResolveMobileHaircut(view);
    }

    private void ResolveMobileHaircut(SalonCustomerView view)
    {
        if (view == null || view.Customer == null) { EndMobileWork(); return; }
        HaircutResult result = _game.CompleteHaircutAction(
            view.Customer, _mobileWorkElapsed, false);
        _game.EndActiveOperation(view.Customer);
        view.HaircutFeedback?.Stop(result);
        view.ApplyHairStage(view.Customer.HairStage);
        view.OrderDemand?.Refresh();
        if (result == HaircutResult.Undercut)
            ShowToast("剪得还不够 · 再按住补剪");
        else if (result == HaircutResult.Overcut)
            ShowToast("剪过头了 · 顾客不满意");
        else if (result == HaircutResult.Perfect)
            ShowToast(view.Customer.IsComplete ? "剪发完成！" : "剪发完成 · 继续下一项");
        else
            ShowToast("剪发没有完成 · 请重新操作");
        EndMobileWork();
    }

    private void EndMobileWork()
    {
        if (_mobileWorkingView != null) _mobileWorkingView.ActionProgress?.ClearProgress();
        _mobileWorkingView = null;
        _mobileWorkElapsed = 0f;
        _mobileHaircutSuspended = false;
        _playerCharacter?.EndService();
        if (_mobileProgressFill != null) _mobileProgressFill.transform.parent.gameObject.SetActive(false);
    }

    private void SuspendMobileInput()
    {
        if (_mobileWorkingView != null && _mobileWorkingService == ServiceType.Cut)
            _mobileHaircutSuspended = true;
        _mobileControls?.ResetInput();
    }

    private void BindMobilePauseButton(Button button)
    {
        // Pause on press, before a simultaneous haircut release can be
        // processed. Do not toggle again on the button's later click event.
        button.onClick.RemoveListener(TogglePause);
        var trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        foreach (var eventType in new[] { UnityEngine.EventSystems.EventTriggerType.PointerDown,
                                          UnityEngine.EventSystems.EventTriggerType.Submit })
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => TogglePause());
            trigger.triggers.Add(entry);
        }
    }

    // Read-only evidence for real browser input checks; absent from release builds.
    private void EmitMobileEvidence()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_mobileMode || !Application.absoluteURL.Contains("mobileEvidence=1") ||
            Time.unscaledTime < _mobileEvidenceAt || _player == null) return;
        _mobileEvidenceAt = Time.unscaledTime + .3f;
        MobileTarget evidenceTarget = FindMobileTarget();
        var evidence = new MobileEvidence
        {
            day = _dayController.DayNumber, state = _dayController.State.ToString(),
            paused = _dayController.IsPaused, progress = _dayController.BusinessProgress,
            completed = _dayController.Stats.CompletedOrders, target = DaySettings.TargetOrders,
            balance = _game.Balance, purchased = _game.HasAutoBlowStand,
            satisfaction = _satisfaction.CurrentSatisfaction,
            player = _player.position, action = _mobileActionLabel, available = _mobileActionAvailable,
            guided = _mobileGuidedCustomer == null ? -1 : _mobileGuidedCustomer.Id,
            working = _mobileWorkingView == null ? -1 : _mobileWorkingView.Customer.Id,
            targetCustomer = evidenceTarget.Customer == null ? -1 : evidenceTarget.Customer.Id,
            targetAction = evidenceTarget.Action.ToString(),
            workingElapsed = _mobileWorkElapsed,
            workingSuspended = _mobileHaircutSuspended,
            targetTool = evidenceTarget.Customer == null || evidenceTarget.Customer.HaircutService == null
                ? string.Empty : evidenceTarget.Customer.HaircutService.CurrentRequiredTool.ToString(),
            sourceWashKits = _mobileSupplies == null ? -1 : _mobileSupplies.SourceWashKits,
            carriedWashKits = _mobileSupplies == null ? -1 : _mobileSupplies.CarriedWashKits,
            washRackWashKits = _mobileSupplies == null ? -1 : _mobileSupplies.WashRackWashKits,
            padPaid = _mobileSupplyPad == null ? -1 : _mobileSupplyPad.Paid,
            padUnlocked = _mobileSupplyPad != null && _mobileSupplyPad.IsUnlocked,
            cameraRight = _camera.transform.right, cameraForward = _camera.transform.forward,
            floor = _mobileFloor, obstacles = _mobileObstacles.ToArray()
        };
        foreach (var view in _customerViews)
        {
            if (view == null) continue;
            var c = view.Customer;
            evidence.customers.Add(new MobileCustomerEvidence
            {
                id = c.Id, state = c.State.ToString(), station = c.Station,
                need = c.CurrentNeed.ToString(), step = c.Step, patience = c.Patience,
                position = view.transform.position, arrived = view.IsAtMovementDestination,
                autoRunning = c.AutoBlowRunning, autoStopped = c.AutoBlowSafetyStopped,
                autoElapsed = c.BackgroundTask.Elapsed, autoReady = c.BackgroundTask.IdealStart,
                foamRunning = _game.IsWashFoamWaitRunning(c), foamReady = _game.IsWashFoamReadyToRinse(c),
                activeAction = c.ActiveServiceAction.ToString(), serviceResult = c.ServiceResult.ToString(),
                hairStage = c.HairStage.ToString(), satisfaction = c.Satisfaction,
                wrongStationCount = c.WrongStationCount, reactionKind = c.ReactionKind.ToString(),
                needsTransfer = c.State == CustomerState.Serving && !SalonGameModel.IsCompatibleStation(
                    c.CurrentNeed, _game.Workstations[c.Station].Type),
                haircutTool = c.HaircutService == null ? string.Empty : c.HaircutService.CurrentRequiredTool.ToString(),
                complete = c.IsComplete, screen = _camera.WorldToScreenPoint(view.transform.position)
            });
        }
        foreach (var pair in _playerServiceAnchors)
            evidence.stations.Add(new MobileStationEvidence
            {
                id = pair.Key, position = pair.Value.position,
                type = _game.Workstations[pair.Key].Type.ToString(),
                occupied = _game.IsStationOccupied(pair.Key)
            });
        foreach (var button in FindObjectsByType<Button>())
        {
            if (!button.gameObject.activeInHierarchy) continue;
            var label = button.GetComponentInChildren<Text>();
            evidence.buttons.Add(new MobileButtonEvidence { label = label == null ? button.name : label.text,
                available = button.interactable, position = UiScreenCenter(button.transform as RectTransform) });
        }
        GameObject joystick = GameObject.Find("MobileJoystick");
        GameObject action = GameObject.Find("MobileInteractionButton");
        if (joystick != null) evidence.joystick = UiScreenCenter(joystick.transform as RectTransform);
        if (action != null) evidence.interaction = UiScreenCenter(action.transform as RectTransform);
        Debug.Log("[MOBILE_STATE] " + JsonUtility.ToJson(evidence));
#endif
    }

    private static Vector2 UiScreenCenter(RectTransform rect)
        => rect == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

    [Serializable] private sealed class MobileEvidence
    {
        public int day, completed, target, balance, guided, working, targetCustomer, satisfaction;
        public int sourceWashKits, carriedWashKits, washRackWashKits, padPaid;
        public bool paused, purchased, padUnlocked, available, workingSuspended;
        public string state, action, targetTool, targetAction;
        public float progress, workingElapsed;
        public Vector3 player, cameraRight, cameraForward;
        public Rect floor;
        public Rect[] obstacles;
        public Vector2 joystick, interaction;
        public List<MobileCustomerEvidence> customers = new List<MobileCustomerEvidence>();
        public List<MobileStationEvidence> stations = new List<MobileStationEvidence>();
        public List<MobileButtonEvidence> buttons = new List<MobileButtonEvidence>();
    }
    [Serializable] private sealed class MobileCustomerEvidence
    {
        public int id, station, step, wrongStationCount;
        public string state, need, activeAction, serviceResult, haircutTool, hairStage, reactionKind;
        public Vector3 position, screen;
        public bool arrived, autoRunning, autoStopped, complete, foamRunning, foamReady, needsTransfer;
        public float patience, autoElapsed, autoReady, satisfaction;
    }
    [Serializable] private sealed class MobileStationEvidence
    {
        public int id;
        public Vector3 position;
        public string type;
        public bool occupied;
    }
    [Serializable] private sealed class MobileButtonEvidence
    {
        public string label;
        public bool available;
        public Vector2 position;
    }
}
