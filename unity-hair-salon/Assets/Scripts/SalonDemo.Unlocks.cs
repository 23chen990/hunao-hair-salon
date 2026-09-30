using System;
using System.Collections.Generic;
using HairSalon;
using HairSalon.AssetPipeline;
using UnityEngine;

/// <summary>Construction payments and immediate equipment presentation for the mobile shop.</summary>
public sealed partial class SalonDemo
{
    private sealed class MobileUnlockPad
    {
        public SalonUnlockDefinition Definition;
        public SalonProximityPurchasePadModel Model;
        public GameObject Root;
        public Transform Point;
        public TextMesh Label;
        public float SpendElapsed;
        public bool WasNear;
        public bool InsufficientToastShown;
        public bool CheckpointDirty;
        public SalonUnlockId Id => Definition.Id;
    }

    private const float MobileUnlockPadRadius = 1.25f;
    // In front of the waiting wall, right of the standing queue; the bench
    // later fills the same spot so the second seat pad reuses it.
    private static readonly Vector3 MobileWaitingSeatsPadPosition = new Vector3(-3.35f, .2f, -4.2f);
    // Beside the first haircut chair, clear of its stylist and queue anchors.
    private static readonly Vector3 MobileBlowStandPosition = new Vector3(-3.8f, .2f, .35f);
    private static readonly Vector3 WaitingBenchPosition = new Vector3(-3.375f, 0f, -4.3f);
    private static readonly Vector3[] ExtraWaitingPositions =
    {
        new Vector3(-4.0f, .4f, -4.3f),
        new Vector3(-2.75f, .4f, -4.3f)
    };

    private static readonly SalonUnlockId[] MobileUnlockPadIds =
        { SalonUnlockId.BlowDryer, SalonUnlockId.WashStation, SalonUnlockId.WaitingSeats, SalonUnlockId.BlowStand, SalonUnlockId.ExtraSeats };

    private readonly Dictionary<SalonUnlockId, MobileUnlockPad> _mobileUnlockPads =
        new Dictionary<SalonUnlockId, MobileUnlockPad>();
    private GameObject _waitingSeatsRoot;
    private GameObject _standingQueueRoot;
    private GameObject _waitingBenchRoot;
    private GameObject _blowStandRoot;

    private int MobileRouteDay => _dayController != null ? _dayController.DayNumber
        : _mobileProgress != null ? _mobileProgress.DayNumber : 1;

    /// <summary>Desktop acceptance harnesses keep the original furnished salon.</summary>
    private bool AreWaitingSeatsBuilt => !_mobileMode || IsMobileUnlockBuilt(SalonUnlockId.WaitingSeats);

    private bool IsMobileUnlockBuilt(SalonUnlockId id)
    {
        switch (id)
        {
            case SalonUnlockId.HaircutChair:
                return _mobileHaircutExpansionPad != null && _mobileHaircutExpansionPad.IsUnlocked;
            case SalonUnlockId.SupplyRack:
                return _mobileSupplyPad != null && _mobileSupplyPad.IsUnlocked;
            case SalonUnlockId.WashAnnex:
                return IsMobileWashAnnexUnlocked;
            case SalonUnlockId.BlowStand:
                return (_game != null && _game.HasAutoBlowStand) ||
                       (_mobileUnlockPads.TryGetValue(id, out MobileUnlockPad stand) && stand.Model.IsUnlocked);
            default:
                return _mobileUnlockPads.TryGetValue(id, out MobileUnlockPad pad) && pad.Model.IsUnlocked;
        }
    }

    private int MobileUnlockPaid(SalonUnlockId id)
    {
        switch (id)
        {
            case SalonUnlockId.HaircutChair: return _mobileHaircutExpansionPad?.Paid ?? 0;
            case SalonUnlockId.SupplyRack: return _mobileSupplyPad?.Paid ?? 0;
            case SalonUnlockId.WashAnnex: return _mobileWashAnnexPad?.Paid ?? 0;
            default: return _mobileUnlockPads.TryGetValue(id, out MobileUnlockPad pad) ? pad.Model.Paid : 0;
        }
    }

    private bool IsMobileUnlockPadVisible(SalonUnlockId id)
        => _mobileMode && SalonUnlockRoute.IsVisible(id, MobileRouteDay, IsMobileUnlockBuilt, MobileUnlockPaid);

    private void CreateMobileUnlockPads(SalonProgressData data)
    {
        data ??= SalonProgressData.CreateDefault();
        foreach (SalonUnlockId id in MobileUnlockPadIds)
        {
            if (!_mobileUnlockPads.TryGetValue(id, out MobileUnlockPad pad))
            {
                pad = new MobileUnlockPad { Definition = SalonUnlockRoute.Get(id) };
                _mobileUnlockPads[id] = pad;
            }
            pad.Model = new SalonProximityPurchasePadModel(pad.Definition.PadId, pad.Definition.Cost);
            int paid = RestoredUnlockPayment(id, data);
            if (paid > 0) pad.Model.ApplyPayment(paid);
            pad.SpendElapsed = 0f;
            pad.WasNear = false;
            pad.InsufficientToastShown = false;
            pad.CheckpointDirty = false;
        }
    }

    private static int RestoredUnlockPayment(SalonUnlockId id, SalonProgressData data)
    {
        int cost = SalonUnlockRoute.CostOf(id);
        switch (id)
        {
            case SalonUnlockId.BlowDryer:
                return data.BlowDryerPurchased ? cost : Mathf.Clamp(data.BlowDryerPaid, 0, cost);
            case SalonUnlockId.WashStation:
                return data.WashStationPurchased ? cost : Mathf.Clamp(data.WashStationPaid, 0, cost);
            case SalonUnlockId.WaitingSeats:
                return data.WaitingSeatsPurchased ? cost : Mathf.Clamp(data.WaitingSeatsPaid, 0, cost);
            case SalonUnlockId.BlowStand:
                return data.AutoBlowPurchased ? cost : Mathf.Clamp(data.BlowStandPaid, 0, cost);
            case SalonUnlockId.ExtraSeats:
                return data.ExtraSeatsPurchased ? cost : Mathf.Clamp(data.ExtraSeatsPaid, 0, cost);
            default:
                return 0;
        }
    }

    private void WriteMobileUnlockProgress(SalonProgressData data)
    {
        if (data == null) return;
        foreach (MobileUnlockPad pad in _mobileUnlockPads.Values)
        {
            bool built = pad.Model.IsUnlocked;
            switch (pad.Id)
            {
                case SalonUnlockId.BlowDryer:
                    data.BlowDryerPurchased = built; data.BlowDryerPaid = pad.Model.Paid; break;
                case SalonUnlockId.WashStation:
                    data.WashStationPurchased = built; data.WashStationPaid = pad.Model.Paid; break;
                case SalonUnlockId.WaitingSeats:
                    data.WaitingSeatsPurchased = built;
                    data.WaitingSeatsPaid = pad.Model.Paid;
                    break;
                case SalonUnlockId.BlowStand:
                    data.BlowStandPaid = data.AutoBlowPurchased ? pad.Definition.Cost : pad.Model.Paid;
                    break;
                case SalonUnlockId.ExtraSeats:
                    data.ExtraSeatsPurchased = built;
                    data.ExtraSeatsPaid = pad.Model.Paid;
                    break;
            }
        }
    }

    /// <summary>
    /// Applies every unlock to the scene and rules: seats or standing queue,
    /// the bench and its extra capacity, the dryer stand and all pad
    /// visibility. Called on each opening, after retries and after any build.
    /// </summary>
    private void ApplyMobileUnlockPresentation()
    {
        if (!_mobileMode) return;
        if (_game != null && _mobileUnlockPads.TryGetValue(SalonUnlockId.BlowStand, out MobileUnlockPad stand) &&
            stand.Model.IsUnlocked && !_game.HasAutoBlowStand)
            _game.InstallAutoBlowStand();
        bool seats = IsMobileUnlockBuilt(SalonUnlockId.WaitingSeats);
        bool extra = IsMobileUnlockBuilt(SalonUnlockId.ExtraSeats);
        if (_waitingSeatsRoot != null) _waitingSeatsRoot.SetActive(seats);
        if (_standingQueueRoot != null) _standingQueueRoot.SetActive(!seats);
        if (_waitingBenchRoot != null) _waitingBenchRoot.SetActive(extra);
        if (_blowStandRoot != null) _blowStandRoot.SetActive(IsMobileUnlockBuilt(SalonUnlockId.BlowStand));
        ApplyMobileCoreEquipment();
        ApplyMobileSeatCapacity();
        RefreshMobileUnlockPadVisuals();
        if (_mobileControls != null) BuildMobileCollisionMap();
    }

    private void ApplyMobileSeatCapacity()
    {
        if (!_mobileMode) return;
        bool extra = IsMobileUnlockBuilt(SalonUnlockId.ExtraSeats);
        int waiting = SalonUnlockRoute.WaitingCapacity(extra);
        // Furniture supplies capacity, not an instruction to fill every seat.
        int concurrent = Mathf.Min(SalonUnlockRoute.MaxConcurrentCustomers(extra),
            DaySettings.MobileDayNumber == 1 ? 2 : DaySettings.MobileDayNumber == 2 ? 3 : 4);
        FlowSettings.WaitingCapacity = waiting;
        FlowSettings.MaxCustomers = concurrent;
        DaySettings.WaitingCapacity = waiting;
        DaySettings.MaxConcurrentCustomers = concurrent;
        if (_game != null) _game.WaitingSeatsInstalled = IsMobileUnlockBuilt(SalonUnlockId.WaitingSeats);
    }

    private void RefreshMobileUnlockPadVisuals()
    {
        UpdateMobilePurchasePadVisual();
        UpdateMobileHaircutExpansionPadVisual();
        UpdateMobileWashAnnexPadVisual();
        foreach (MobileUnlockPad pad in _mobileUnlockPads.Values) UpdateMobileUnlockPadVisual(pad);
    }

    private Vector3 WaitingSlotPosition(int slot)
    {
        if (slot < WaitingPositions.Length) return WaitingPositions[slot];
        return ExtraWaitingPositions[Mathf.Clamp(slot - WaitingPositions.Length, 0, ExtraWaitingPositions.Length - 1)];
    }

    /// <summary>
    /// Mobile waiting area before the seats are built: customers stand on
    /// painted floor spots; the sofa and table only appear once bought.
    /// </summary>
    private void BuildMobileWaitingUnlocks(Transform zone)
    {
        if (!_mobileMode || zone == null) return;
        _standingQueueRoot = new GameObject("Standing Queue Spots");
        _standingQueueRoot.transform.SetParent(zone, false);
        for (int i = 0; i < WaitingPositions.Length; i++)
        {
            Vector3 spot = new Vector3(WaitingPositions[i].x, .05f, WaitingPositions[i].z);
            Block("Queue Spot " + (i + 1), _standingQueueRoot.transform, spot, new Vector3(.9f, .03f, .9f), Cream);
            Block("Queue Spot Arrow " + (i + 1), _standingQueueRoot.transform, spot + new Vector3(.62f, 0f, 0f),
                new Vector3(.28f, .03f, .12f), Gold);
        }

        _waitingBenchRoot = new GameObject("Waiting Bench");
        Transform bench = _waitingBenchRoot.transform;
        bench.SetParent(zone, false);
        bench.localPosition = WaitingBenchPosition;
        Block("Bench Seat", bench, new Vector3(0f, .55f, -.2f), new Vector3(2.5f, .65f, 1.5f), DarkWood);
        Block("Bench Cushion", bench, new Vector3(0f, 1f, 0f), new Vector3(2.25f, .45f, 1.2f), Hex("D69A32"));
        Block("Bench Back", bench, new Vector3(0f, 1.55f, .45f), new Vector3(2.4f, 1.25f, .45f), Wood);
        FurnitureShadow(bench, "furniture-waiting-bench", Vector3.zero);
        for (int i = 0; i < ExtraWaitingPositions.Length; i++)
        {
            int slot = WaitingPositions.Length + i;
            float staggerY = (slot & 1) == 0 ? .68f : -.52f;
            float nudgeX = (slot & 1) == 0 ? -.14f : .14f;
            _waitingUiAnchors[slot] = Marker("Waiting CustomerUIAnchor " + (slot + 1), zone,
                ExtraWaitingPositions[i] + new Vector3(nudgeX, staggerY, 0f));
        }
        _waitingSeatsRoot?.SetActive(AreWaitingSeatsBuilt);
        _standingQueueRoot.SetActive(!AreWaitingSeatsBuilt);
        _waitingBenchRoot.SetActive(IsMobileUnlockBuilt(SalonUnlockId.ExtraSeats));
    }

    private void BuildMobileUnlockProps(Transform stage)
    {
        if (!_mobileMode || stage == null) return;
        _blowStandRoot = new GameObject("Blow Stand");
        Transform root = _blowStandRoot.transform;
        root.SetParent(stage, false);
        root.localPosition = new Vector3(MobileBlowStandPosition.x, 0f, MobileBlowStandPosition.z);
        Cylinder("Blow Stand Base", root, new Vector3(0f, .08f, 0f), new Vector3(.85f, .12f, .85f), Ink);
        Cylinder("Blow Stand Pole", root, new Vector3(0f, 1.05f, 0f), new Vector3(.14f, .95f, .14f), Cream);
        Block("Blow Stand Arm", root, new Vector3(0f, 1.95f, -.3f), new Vector3(.14f, .14f, .7f), Cream);
        Cylinder("Blow Stand Hood", root, new Vector3(0f, 1.9f, -.62f), new Vector3(.75f, .32f, .75f), Teal);
        Cylinder("Blow Stand Hood Rim", root, new Vector3(0f, 1.72f, -.62f), new Vector3(.8f, .06f, .8f), Gold);
        FurnitureShadow(root, "furniture-blow-stand", Vector3.zero);
        _blowStandRoot.SetActive(IsMobileUnlockBuilt(SalonUnlockId.BlowStand));

        foreach (SalonUnlockId id in MobileUnlockPadIds)
        {
            if (!_mobileUnlockPads.TryGetValue(id, out MobileUnlockPad pad)) continue;
            Vector3 position = MobileUnlockPadPosition(id);
            BuildMobileUnlockPadVisual(pad, stage, position);
        }
    }

    private void BuildMobileUnlockPadVisual(MobileUnlockPad pad, Transform stage, Vector3 position)
    {
        string name = "Expansion Pad " + pad.Id;
        pad.Root = new GameObject(name);
        Transform root = pad.Root.transform;
        root.SetParent(stage, false);
        root.localPosition = position;
        try
        {
            AssetDefinition padAsset = AssetManifestLoader.LoadFromResources().Find(pad.Definition.PadId);
            if (padAsset?.Shadow != null && padAsset.Shadow.Enabled)
                ContactShadow.Apply(root, padAsset.Shadow, padAsset.Sorting?.Order - 1 ?? -1);
        }
        catch (Exception)
        {
            // Manifest validation is a build gate; see BuildMobileSupplyPurchasePad.
        }
        Cylinder(name + " Disc", root, new Vector3(0f, .04f, 0f), new Vector3(1.55f, .08f, 1.55f), Gold);
        Block(name + " Ring Front", root, new Vector3(0f, .1f, -.78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block(name + " Ring Back", root, new Vector3(0f, .1f, .78f), new Vector3(1.55f, .08f, .1f), Cream);
        Block(name + " Ring Left", root, new Vector3(-.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        Block(name + " Ring Right", root, new Vector3(.78f, .1f, 0f), new Vector3(.1f, .08f, 1.55f), Cream);
        pad.Point = new GameObject(name + " Anchor").transform;
        pad.Point.SetParent(root, false);
        var labelObject = new GameObject(name + " Label");
        labelObject.transform.SetParent(root, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        pad.Label = labelObject.AddComponent<TextMesh>();
        pad.Label.alignment = TextAlignment.Center;
        pad.Label.anchor = TextAnchor.MiddleCenter;
        pad.Label.characterSize = .1f;
        pad.Label.fontSize = 36;
        pad.Label.font = SalonUiFactory.GetPackagedUiFont();
        labelObject.GetComponent<MeshRenderer>().sharedMaterial = pad.Label.font.material;
        pad.Label.color = Cream;
        UpdateMobileUnlockPadVisual(pad);
    }

    private void UpdateMobileUnlockPadVisual(MobileUnlockPad pad)
    {
        if (pad == null) return;
        bool visible = !pad.Model.IsUnlocked && IsMobileUnlockPadVisible(pad.Id);
        if (pad.Root != null && pad.Root.activeSelf != visible) pad.Root.SetActive(visible);
        if (pad.Label == null || !visible) return;
        pad.Label.text = pad.Definition.DisplayName + "\n" + pad.Model.Paid + "/" + pad.Model.Cost + " 金币";
        if (_camera != null)
        {
            Vector3 toCamera = pad.Label.transform.position - _camera.transform.position;
            if (toCamera.sqrMagnitude > .001f)
                pad.Label.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    private void UpdateMobileUnlockPads(float dt)
    {
        foreach (SalonUnlockId id in MobileUnlockPadIds)
            if (_mobileUnlockPads.TryGetValue(id, out MobileUnlockPad pad)) UpdateMobileUnlockPad(pad, dt);
    }

    private void UpdateMobileUnlockPad(MobileUnlockPad pad, float dt)
    {
        if (!_mobileMode || pad?.Model == null || pad.Point == null || _player == null || !CanInteractWithSalon())
            return;
        bool near = !pad.Model.IsUnlocked && IsMobileUnlockPadVisible(pad.Id) &&
                    FlatDistance(_player.position, pad.Point.position) <= MobileUnlockPadRadius;
        if (!near)
        {
            bool shouldSave = pad.WasNear && pad.CheckpointDirty;
            pad.WasNear = false;
            pad.InsufficientToastShown = false;
            pad.SpendElapsed = 0f;
            if (shouldSave) SaveMobileCheckpoint(false);
            UpdateMobileUnlockPadVisual(pad);
            return;
        }
        if (!pad.WasNear)
        {
            pad.WasNear = true;
            pad.InsufficientToastShown = false;
            ShowToast("停留投入金币 · " + pad.Definition.DisplayName);
        }
        float rate = pad.Definition.PaymentPerSecond;
        pad.SpendElapsed += Mathf.Max(0f, dt);
        int amount = pad.Model.CalculatePayment(pad.SpendElapsed, _game.Balance, rate);
        if (amount > 0)
        {
            int balanceBeforeSpend = _game.Balance;
            int customerLimitBefore = SalonPacingDirector.ActiveLimit(_mobileProgress);
            if (!_game.Payments.TrySpend(amount)) pad.SpendElapsed = 0f;
            else if (!pad.Model.ApplyPayment(amount))
            {
                _game.Payments.RestoreBalance(balanceBeforeSpend);
                pad.SpendElapsed = 0f;
            }
            else
            {
                pad.SpendElapsed = SalonProximityPurchasePadModel.RetainUnspentPaymentTime(pad.SpendElapsed, amount, rate);
                WriteMobileUnlockProgress(_mobileProgress);
                pad.CheckpointDirty = true;
                if (_coinBalanceLabel != null) _coinBalanceLabel.text = _game.Balance.ToString("N0");
                if (pad.Model.IsUnlocked)
                {
                    ProtectMobileCapacityIncrease(customerLimitBefore);
                    CompleteMobileUnlock(pad);
                }
            }
        }
        else if (_game.Balance <= 0)
        {
            pad.SpendElapsed = 0f;
            if (!pad.InsufficientToastShown)
            {
                pad.InsufficientToastShown = true;
                ShowToast("金币不足 · 先完成订单");
            }
        }
        UpdateMobileUnlockPadVisual(pad);
    }

    private void ProtectMobileCapacityIncrease(int previousLimit)
    {
        if (SalonPacingDirector.ActiveLimit(_mobileProgress) <= previousLimit) return;
        _mobileProgress.CapacityPracticeLimit = previousLimit;
        _mobileProgress.CapacityPracticeUntilPaid = _mobileProgress.PaidCustomerCount + 2;
    }

    private void CompleteMobileUnlock(MobileUnlockPad pad)
    {
        if (pad.Id == SalonUnlockId.BlowStand)
        {
            _game.InstallAutoBlowStand();
            if (_mobileProgress != null) _mobileProgress.AutoBlowPurchased = true;
        }
        CompleteMobileCoreUnlock(pad.Id);
        ApplyMobileUnlockPresentation();
        ShowToast(MobileUnlockCompletedToast(pad.Id));
        SaveMobileCheckpoint(false);
    }

    private static string MobileUnlockCompletedToast(SalonUnlockId id)
    {
        switch (id)
        {
            case SalonUnlockId.BlowDryer: return "吹风机已安装 · 下位顾客来吹发，启动后等绿圈再收尾";
            case SalonUnlockId.WashStation: return "洗头台已安装 · 送6份洗发水，长按起泡后等绿圈冲洗";
            case SalonUnlockId.WaitingSeats: return "等候座椅已安装 · 坐着等的顾客更有耐心";
            case SalonUnlockId.BlowStand: return "吹风支架已安装 · 自动吹发更快收尾";
            case SalonUnlockId.ExtraSeats: return "加座长凳已安装 · 店里能多等两位顾客";
            default: return "施工完成";
        }
    }

    private string DescribeMobileRouteForOpening(int day)
    {
        foreach (var definition in SalonUnlockRoute.Route)
            if (!IsMobileUnlockBuilt(definition.Id))
                return "下个解锁：" + definition.DisplayName + " · 还需 " +
                    (definition.Cost - MobileUnlockPaid(definition.Id)) + " 金币";
        return "全部设备已建好";
    }

    private string DescribeMobileRouteForTomorrow(int tomorrow)
        => "明日 DAY " + tomorrow + "\n" + DescribeMobileRouteForOpening(tomorrow) +
           "\n营业中站上施工点投币，建好立即生效";

    private MobilePadEvidence[] CaptureMobilePadEvidence()
    {
        var pads = new List<MobilePadEvidence>();
        foreach (SalonUnlockDefinition definition in SalonUnlockRoute.Route)
        {
            Vector3 position = MobileUnlockPadPosition(definition.Id);
            pads.Add(new MobilePadEvidence
            {
                id = definition.Id.ToString(), padId = definition.PadId, openDay = definition.OpenDay,
                cost = definition.Cost, paid = MobileUnlockPaid(definition.Id),
                unlocked = IsMobileUnlockBuilt(definition.Id),
                visible = !IsMobileUnlockBuilt(definition.Id) && IsMobileUnlockPadVisible(definition.Id),
                position = position,
                screen = _camera == null ? Vector3.zero : _camera.WorldToScreenPoint(position)
            });
        }
        return pads.ToArray();
    }

    private Vector3 MobileUnlockPadPosition(SalonUnlockId id)
    {
        switch (id)
        {
            case SalonUnlockId.HaircutChair: return MobileHaircutExpansionPadPosition;
            case SalonUnlockId.SupplyRack: return MobileSupplyPadPosition;
            case SalonUnlockId.WashAnnex: return SalonWashAnnexLayout.PadPosition;
            case SalonUnlockId.BlowDryer: return MobileBlowStandPosition;
            case SalonUnlockId.WashStation: return WashPlayerAnchorPosition;
            case SalonUnlockId.BlowStand: return MobileBlowStandPosition;
            default: return MobileWaitingSeatsPadPosition;
        }
    }

    [Serializable] private sealed class MobilePadEvidence
    {
        public string id, padId;
        public int openDay, cost, paid;
        public bool unlocked, visible;
        public Vector3 position, screen;
    }
}
