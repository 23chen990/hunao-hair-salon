using System;
using System.Collections.Generic;
using HairSalon;
using HairSalon.AssetPipeline;
using UnityEngine;

/// <summary>
/// Second wash room behind the right wall: the third proximity pad of the
/// mobile shop. Buying it opens the wall, builds the annex shell and makes
/// wash station 4 usable; retry and slot changes can close it again.
/// </summary>
public sealed partial class SalonDemo
{
    private SalonProximityPurchasePadModel _mobileWashAnnexPad;
    private GameObject _mobileWashAnnexPadRoot;
    private Transform _mobileWashAnnexPadPoint;
    private TextMesh _mobileWashAnnexPadLabel;
    private GameObject _mobileWashAnnexRoot;
    private GameObject _entranceDoorRoot;
    private MeshFilter _mobileRoomFinishFilter;
    private Mesh _mobileRoomFinishClosedMesh;
    private Mesh _mobileRoomFinishOpenMesh;
    private readonly List<Renderer> _mobileProceduralRightWall = new List<Renderer>();
    private readonly Dictionary<string, Material> _mobileWashAnnexFallbackMaterials = new Dictionary<string, Material>();
    private float _mobileWashAnnexPadSpendElapsed;
    private bool _mobileWashAnnexPadWasNear;
    private bool _mobileWashAnnexPadInsufficientToastShown;
    private bool _mobileWashAnnexPadCheckpointDirty;
    private static readonly float MobileWashAnnexPadSpendRate =
        SalonUnlockRoute.Get(SalonUnlockId.WashAnnex).PaymentPerSecond;

    private bool IsMobileWashAnnexUnlocked => _mobileWashAnnexPad != null && _mobileWashAnnexPad.IsUnlocked;

    private Rect MobileCameraBounds()
    {
        if (IsMobileWashAnnexUnlocked) return SalonWashAnnexLayout.WalkableBounds;
        bool expanded = _mobileHaircutExpansionPad != null && _mobileHaircutExpansionPad.IsUnlocked;
        return expanded ? MobileSalonBounds : MobileZoneABounds;
    }

    private void CreateMobileWashAnnexPad(SalonProgressData data)
    {
        _mobileWashAnnexPad = new SalonProximityPurchasePadModel(
            SalonWashAnnexLayout.PadId, SalonProgressData.WashAnnexExpansionCost);
        RestoreMobileWashAnnexProgress(data);
        _mobileWashAnnexPadSpendElapsed = 0f;
        _mobileWashAnnexPadWasNear = false;
        _mobileWashAnnexPadInsufficientToastShown = false;
        _mobileWashAnnexPadCheckpointDirty = false;
    }

    private void RestoreMobileWashAnnexProgress(SalonProgressData data)
    {
        if (_mobileWashAnnexPad == null || data == null) return;
        int paid = Mathf.Clamp(data.WashAnnexExpansionPaid, 0, SalonProgressData.WashAnnexExpansionCost);
        if (data.WashAnnexExpansionPurchased) paid = SalonProgressData.WashAnnexExpansionCost;
        if (paid > 0) _mobileWashAnnexPad.ApplyPayment(paid);
    }

    /// <summary>The pad opens on its route day (day 3).</summary>
    private bool IsMobileWashAnnexPadVisible()
        => _mobileWashAnnexPad != null && !_mobileWashAnnexPad.IsUnlocked &&
           IsMobileUnlockPadVisible(SalonUnlockId.WashAnnex);

    private void BuildMobileWashAnnexPad(Transform stage)
    {
        if (_mobileWashAnnexPad == null || stage == null) return;
        _mobileWashAnnexPadRoot = new GameObject("Expansion Pad Wash Annex");
        Transform pad = _mobileWashAnnexPadRoot.transform;
        pad.SetParent(stage, false);
        pad.localPosition = SalonWashAnnexLayout.PadPosition;
        try
        {
            AssetDefinition padAsset = AssetManifestLoader.LoadFromResources().Find(SalonWashAnnexLayout.PadId);
            if (padAsset?.Shadow != null && padAsset.Shadow.Enabled)
                ContactShadow.Apply(pad, padAsset.Shadow, padAsset.Sorting?.Order - 1 ?? -1);
        }
        catch (Exception)
        {
            // Manifest validation is a build gate; see BuildMobileSupplyPurchasePad.
        }
        Cylinder("Wash Annex Pad Disc", pad, new Vector3(0f, .04f, 0f), new Vector3(1.55f, .08f, 1.55f), Gold);
        Block("Wash Annex Pad Ring Front", pad, new Vector3(0f, .1f, -.78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block("Wash Annex Pad Ring Back", pad, new Vector3(0f, .1f, .78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block("Wash Annex Pad Ring Left", pad, new Vector3(-.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        Block("Wash Annex Pad Ring Right", pad, new Vector3(.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        // Marks where the wall will open, in the same gold as the zone B barrier stripes.
        Vector3 wall = new Vector3(10.47f, 0f, 0f) - SalonWashAnnexLayout.PadPosition;
        Block("Wash Annex Opening Mark Front", pad, wall + new Vector3(0f, 1.3f, 1.25f), new Vector3(.06f, 2.6f, .12f), Gold);
        Block("Wash Annex Opening Mark Back", pad, wall + new Vector3(0f, 1.3f, 6.85f), new Vector3(.06f, 2.6f, .12f), Gold);
        Block("Wash Annex Opening Mark Top", pad, wall + new Vector3(0f, 2.6f, 4.05f), new Vector3(.06f, .12f, 5.72f), Gold);
        _mobileWashAnnexPadPoint = new GameObject("Wash Annex Pad Anchor").transform;
        _mobileWashAnnexPadPoint.SetParent(pad, false);
        var labelObject = new GameObject("Wash Annex Pad Label");
        labelObject.transform.SetParent(pad, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        _mobileWashAnnexPadLabel = labelObject.AddComponent<TextMesh>();
        _mobileWashAnnexPadLabel.alignment = TextAlignment.Center;
        _mobileWashAnnexPadLabel.anchor = TextAnchor.MiddleCenter;
        _mobileWashAnnexPadLabel.characterSize = .1f;
        _mobileWashAnnexPadLabel.fontSize = 36;
        _mobileWashAnnexPadLabel.font = SalonUiFactory.GetPackagedUiFont();
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = _mobileWashAnnexPadLabel.font.material;
        _mobileWashAnnexPadLabel.color = Cream;
        UpdateMobileWashAnnexPadVisual();
    }

    private void UpdateMobileWashAnnexPadVisual()
    {
        if (_mobileWashAnnexPadRoot != null)
            _mobileWashAnnexPadRoot.SetActive(IsMobileWashAnnexPadVisible());
        if (_mobileWashAnnexPadLabel == null || _mobileWashAnnexPad == null) return;
        _mobileWashAnnexPadLabel.text = "第二洗发区\n" + _mobileWashAnnexPad.Paid + "/" + _mobileWashAnnexPad.Cost + " 金币";
        if (_camera != null)
        {
            Vector3 toCamera = _mobileWashAnnexPadLabel.transform.position - _camera.transform.position;
            if (toCamera.sqrMagnitude > .001f)
                _mobileWashAnnexPadLabel.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    private void UpdateMobileWashAnnexPad(float dt)
    {
        if (!_mobileMode || _mobileWashAnnexPad == null || _mobileWashAnnexPadPoint == null ||
            _player == null || !CanInteractWithSalon()) return;
        bool nearPad = IsMobileWashAnnexPadVisible() &&
                       FlatDistance(_player.position, _mobileWashAnnexPadPoint.position) <= 1.25f;
        if (!nearPad)
        {
            bool shouldSave = _mobileWashAnnexPadWasNear && _mobileWashAnnexPadCheckpointDirty;
            _mobileWashAnnexPadWasNear = false;
            _mobileWashAnnexPadInsufficientToastShown = false;
            _mobileWashAnnexPadSpendElapsed = 0f;
            if (shouldSave) SaveMobileCheckpoint(false);
            UpdateMobileWashAnnexPadVisual();
            return;
        }
        if (!_mobileWashAnnexPadWasNear)
        {
            _mobileWashAnnexPadWasNear = true;
            _mobileWashAnnexPadInsufficientToastShown = false;
            ShowToast("停留投入金币 · 扩建第二洗发区");
        }
        _mobileWashAnnexPadSpendElapsed += Mathf.Max(0f, dt);
        int amount = _mobileWashAnnexPad.CalculatePayment(
            _mobileWashAnnexPadSpendElapsed, _game.Balance, MobileWashAnnexPadSpendRate);
        if (amount > 0)
        {
            int balanceBeforeSpend = _game.Balance;
            int customerLimitBefore = SalonPacingDirector.ActiveLimit(_mobileProgress);
            if (!_game.Payments.TrySpend(amount)) _mobileWashAnnexPadSpendElapsed = 0f;
            else if (!_mobileWashAnnexPad.ApplyPayment(amount))
            {
                _game.Payments.RestoreBalance(balanceBeforeSpend);
                _mobileWashAnnexPadSpendElapsed = 0f;
            }
            else
            {
                _mobileWashAnnexPadSpendElapsed = SalonProximityPurchasePadModel.RetainUnspentPaymentTime(
                    _mobileWashAnnexPadSpendElapsed, amount, MobileWashAnnexPadSpendRate);
                _mobileProgress.WashAnnexExpansionPaid = _mobileWashAnnexPad.Paid;
                _mobileProgress.WashAnnexExpansionPurchased = _mobileWashAnnexPad.IsUnlocked;
                _mobileWashAnnexPadCheckpointDirty = true;
                if (_coinBalanceLabel != null) _coinBalanceLabel.text = _game.Balance.ToString("N0");
                if (_mobileWashAnnexPad.IsUnlocked)
                {
                    ProtectMobileCapacityIncrease(customerLimitBefore);
                    OpenMobileWashAnnex();
                }
            }
        }
        else if (_game.Balance <= 0)
        {
            _mobileWashAnnexPadSpendElapsed = 0f;
            if (!_mobileWashAnnexPadInsufficientToastShown)
            {
                _mobileWashAnnexPadInsufficientToastShown = true;
                ShowToast("金币不足 · 先完成订单");
            }
        }
        UpdateMobileWashAnnexPadVisual();
    }

    private void OpenMobileWashAnnex()
    {
        _game.SetWorkstationAvailability(SalonWashAnnexLayout.StationId, true);
        ApplyMobileWorkstationPresentation();
        ApplyMobileWashAnnexPresentation();
        RefreshMobileUnlockPadVisuals();
        ShowToast("第二洗发区已开放 · 两位顾客可同时洗发");
        SaveMobileCheckpoint(false);
    }

    /// <summary>
    /// Applies the current annex state to the room: opens or closes the right
    /// wall, shows the annex shell and refreshes joystick collision.
    /// </summary>
    private void ApplyMobileWashAnnexPresentation()
    {
        if (!_mobileMode) return;
        GameObject map = GameObject.Find("Fixed Salon Map");
        if (map == null) return;
        bool open = IsMobileWashAnnexUnlocked;
        if (open && _mobileWashAnnexRoot == null) BuildMobileWashAnnexShell(map.transform);
        if (_mobileWashAnnexRoot != null) _mobileWashAnnexRoot.SetActive(open);
        if (_mobileRoomFinishFilter != null)
            _mobileRoomFinishFilter.sharedMesh = open ? _mobileRoomFinishOpenMesh : _mobileRoomFinishClosedMesh;
        foreach (Renderer wall in _mobileProceduralRightWall)
            if (wall != null) wall.enabled = !open;
        UpdateMobileWashAnnexPadVisual();
        if (_mobileControls != null) BuildMobileCollisionMap();
    }

    private void BuildMobileWashAnnexShell(Transform map)
    {
        Transform finish = map.Find("Crafted Room Finish");
        MeshRenderer finishRenderer = null;
        if (finish != null)
        {
            _mobileRoomFinishFilter = finish.GetComponentInChildren<MeshFilter>();
            finishRenderer = _mobileRoomFinishFilter == null ? null : _mobileRoomFinishFilter.GetComponent<MeshRenderer>();
            if (_mobileRoomFinishFilter != null)
            {
                _mobileRoomFinishClosedMesh = _mobileRoomFinishFilter.sharedMesh;
                _mobileRoomFinishOpenMesh = SalonRoomMeshCutter.RemoveIslands(_mobileRoomFinishClosedMesh,
                    SalonWashAnnexLayout.IsOldRightWallFace, out _);
            }
        }
        else
        {
            // washCraft=original keeps the procedural walls visible.
            foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "Right Wall") _mobileProceduralRightWall.Add(renderer);
        }

        var finishMaterials = new Dictionary<string, Material>();
        if (finishRenderer != null)
            foreach (Material material in finishRenderer.sharedMaterials)
                if (material != null && !finishMaterials.ContainsKey(material.name))
                    finishMaterials[material.name] = material;
        _mobileWashAnnexRoot = SalonWashAnnexBuilder.Build(map, name =>
            finishMaterials.TryGetValue(name, out Material material) ? material : FallbackWashAnnexMaterial(name));
        Transform root = _mobileWashAnnexRoot.transform;
        Block("Annex Exterior Base", root, new Vector3(14.3f, -.35f, 1f), new Vector3(5.1f, .55f, 15.5f), Exterior);
        BuildPlant(root, SalonWashAnnexLayout.PlantPosition);
        FurnitureShadow(root, "prop-potted-plant",
            new Vector3(SalonWashAnnexLayout.PlantPosition.x, 0f, SalonWashAnnexLayout.PlantPosition.z));
    }

    /// <summary>
    /// Replaces the crafted left cutaway wall with the same wall rebuilt
    /// around the entrance door. washCraft=original keeps the split procedural wall.
    /// </summary>
    private void ApplyEntranceDoor()
    {
        GameObject map = GameObject.Find("Fixed Salon Map");
        if (map == null || _entranceDoorRoot != null) return;
        Transform finish = map.transform.Find("Crafted Room Finish");
        MeshFilter filter = finish == null ? null : finish.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        filter.sharedMesh = SalonRoomMeshCutter.RemoveIslands(filter.sharedMesh,
            SalonEntranceDoor.IsOldLeftWallFace, out _);
        var finishMaterials = new Dictionary<string, Material>();
        MeshRenderer finishRenderer = filter.GetComponent<MeshRenderer>();
        if (finishRenderer != null)
            foreach (Material material in finishRenderer.sharedMaterials)
                if (material != null && !finishMaterials.ContainsKey(material.name))
                    finishMaterials[material.name] = material;
        _entranceDoorRoot = SalonEntranceDoor.Build(map.transform, name =>
            finishMaterials.TryGetValue(name, out Material material) ? material : FallbackWashAnnexMaterial(name));
    }

    private Material FallbackWashAnnexMaterial(string name)
    {
        if (_mobileWashAnnexFallbackMaterials.TryGetValue(name, out Material material)) return material;
        material = new Material(Resources.Load<Shader>("SalonCraftLit"))
        {
            name = name,
            color = SalonWashAnnexBuilder.FallbackColor(name)
        };
        _mobileWashAnnexFallbackMaterials[name] = material;
        return material;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Evidence-only save seeds for real browser checks of the unlock route.
    /// The normal player URL never matches; `mobileEvidence=1` is required too.
    /// Each seed starts on the day its pad opens with everything earlier built.
    /// </summary>
    public static SalonProgressData CreateMobileEvidenceSeed(string absoluteUrl)
    {
        if (string.IsNullOrEmpty(absoluteUrl) || !absoluteUrl.Contains("mobileEvidence=1")) return null;
        if (absoluteUrl.Contains("mobileSeed=core-wash"))
        {
            var wash = SalonProgressData.CreateDefault();
            wash.BlowDryerPurchased = wash.WashStationPurchased = true;
            wash.BlowDryerPaid = 600; wash.WashStationPaid = 1200;
            wash.RackStock = 2; wash.SourceStock = 12; wash.SupplyStockInitialized = true;
            return wash;
        }
        int day; int balance; bool annex = false;
        if (absoluteUrl.Contains("mobileSeed=annex-ready")) { day = 3; balance = 3900; }
        else if (absoluteUrl.Contains("mobileSeed=annex-open")) { day = 3; balance = 300; annex = true; }
        else if (absoluteUrl.Contains("mobileSeed=route-day2")) { day = 2; balance = 900; }
        else if (absoluteUrl.Contains("mobileSeed=route-day4")) { day = 4; balance = 1800; annex = true; }
        else if (absoluteUrl.Contains("mobileSeed=route-day5")) { day = 5; balance = 1400; annex = true; }
        else return null;
        var data = SalonProgressData.CreateDefault();
        data.DayNumber = day;
        data.Balance = balance;
        data.FirstDayComplete = true;
        for (int completed = 1; completed < day; completed++)
        {
            data.CompletedDays.Add(completed);
            data.BestCompletedOrders.Add(5);
        }
        data.BlowDryerPurchased = data.WashStationPurchased = true;
        data.BlowDryerPaid = 600; data.WashStationPaid = 1200;
        data.SupplyRackIntroduced = true; data.RackStock = 6; data.SupplyStockInitialized = true;
        data.HaircutExpansionPurchased = true;
        data.HaircutExpansionPaid = SalonProgressData.HaircutExpansionCost;
        data.SupplyRackExpansionPurchased = true;
        data.SupplyRackExpansionPaid = SalonProgressData.SupplyRackExpansionCost;
        data.WaitingSeatsPurchased = day >= 3;
        data.WaitingSeatsPaid = data.WaitingSeatsPurchased ? SalonProgressData.WaitingSeatsCost : 0;
        data.WashAnnexExpansionPurchased = annex;
        data.WashAnnexExpansionPaid = annex ? SalonProgressData.WashAnnexExpansionCost : 0;
        data.AutoBlowPurchased = day >= 5 || absoluteUrl.Contains("mobileSeed=annex-");
        data.BlowStandPaid = data.AutoBlowPurchased ? SalonProgressData.BlowStandCost : 0;
        return data;
    }
#endif
}
