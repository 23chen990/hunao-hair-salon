using System.Collections;
using System.Collections.Generic;
using HairSalon;
using HairSalon.AssetPipeline;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class SalonDemo
{
    private SalonCheckoutModel _mobileCheckout;
    private Transform _mobileSourceRoot, _mobileRackRoot;
    private Transform _cashierPlayerPoint, _cashierQueuePoint;
    private TextMesh _cashierLabel;
    private string _mobileTeachingOrder;
    private SalonPacingDirector _mobilePacing = new SalonPacingDirector();
    private bool _restoringSupply;
    private float _supplySaveElapsed;
    private bool _supplyDirty;
    private TextMesh _mobileGuideArrow;
    private string _mobileTutorialHint;
    private readonly Dictionary<int, SalonCustomerEscort> _escortTrails = new Dictionary<int, SalonCustomerEscort>();

    private void UpdateMobileFoamHoldInput()
    {
        if (_mobileWorkingView != null && _mobileWorkingService == ServiceType.Wash)
            _mobileWorkingView.Customer.TimedActionPaused = !_mobileControls.InteractionHeld;
        if (_coopPlayerTwo?.WorkingView != null && _coopPlayerTwo.WorkingService == ServiceType.Wash)
            _coopPlayerTwo.WorkingView.Customer.TimedActionPaused = !_coopPlayerTwo.Controls.InteractionHeld;
    }

    private bool TryMoveMobileFollower(SalonCustomerView view, float dt)
    {
        if (!_mobileMode) return false;
        Transform leader = view.Customer == _mobileGuidedCustomer ? _player :
            view.Customer == _coopPlayerTwo?.GuidedCustomer ? _coopPlayerTwo.Transform : null;
        if (leader == null || view.Customer.State != CustomerState.Waiting)
        {
            _escortTrails.Remove(view.Customer.Id);
            return false;
        }
        if (!_escortTrails.TryGetValue(view.Customer.Id, out var trail) || trail.Leader != leader)
        {
            trail = new SalonCustomerEscort(leader, view.transform.position, _mobileFloor, _mobileObstacles);
            _escortTrails[view.Customer.Id] = trail;
        }
        view.FollowAt(trail.Move(view.transform.position, dt, _mobileFloor, _mobileObstacles));
        return true;
    }

    private void SeatMobileCustomer(CustomerModel customer, int station)
    {
        var view = FindCustomerView(customer);
        if (view == null || !_customerSeatAnchors.TryGetValue(station, out var seat)) return;
        Vector3 position = seat.position;
        if (_game.Workstations[station].Type != WorkstationType.Wash) position.y = .4f;
        view.SeatDirectly(position);
        _escortTrails.Remove(customer.Id);
        _game.ConfirmStationArrival(customer);
    }

    private void UpdateMobileComedy(SalonCustomerView view)
    {
        if (view.Comedy == null) return;
        var c = view.Customer;
        var mood = c.State == CustomerState.Leaving && c.Emotion == CustomerEmotion.Angry
            ? CustomerMoodStage.Leaving : c.State == CustomerState.Waiting
                ? CustomerMoodModel.FromPatience(c.PatienceProgress) : CustomerMoodStage.Calm;
        view.Comedy.SetMood(mood);
        if (!view.ComedyOvercutShown && c.LastHaircutRating == HaircutServiceRating.Failed)
        {
            view.ComedyOvercutShown = true;
            view.Comedy.Play(CustomerComedyReactionKind.Overcut);
        }
        if (!view.ComedyFoamShown && c.CurrentNeed == ServiceType.Wash &&
            c.BackgroundTask.Elapsed > ServiceSettings.FoamMinorLateThreshold)
        {
            view.ComedyFoamShown = true;
            view.Comedy.Play(CustomerComedyReactionKind.FoamTooLong);
        }
        if (!view.ComedyBlowShown && (c.AutoBlowSafetyStopped || c.LastBlowResult == BlowResult.Moderate))
        {
            view.ComedyBlowShown = true;
            view.Comedy.Play(CustomerComedyReactionKind.BlowTooLong);
        }
        if (!view.ComedyHappyShown && c.State == CustomerState.Finished && c.ServiceResult != CustomerServiceResult.Failed)
        {
            view.ComedyHappyShown = true;
            view.Comedy.Play(CustomerComedyReactionKind.Satisfied);
        }
        view.Comedy.RenderFrame(_camera);
    }

    private void ApplyMobileCoreEquipment()
    {
        if (_game == null) return;
        _game.RequireCheckout = true;
        _game.ExperienceProfile.ServiceDelayPatienceMultiplier = 3.2f;
        PatienceSettings.ImpatientAtPatience = 60f;
        PatienceSettings.AngryAtPatience = 30f;
        bool wash = IsMobileUnlockBuilt(SalonUnlockId.WashStation);
        if (_game.Workstations[0].IsUsable != wash) _game.SetWorkstationAvailability(0, wash);
        ApplyMobileWorkstationPresentation();
        if (_mobileSourceRoot != null) _mobileSourceRoot.gameObject.SetActive(wash && _mobileProgress.SupplyRackIntroduced);
        if (_mobileRackRoot != null) _mobileRackRoot.gameObject.SetActive(wash && _mobileProgress.SupplyRackIntroduced);
    }

    private void RestoreMobileCoreStock()
    {
        if (_mobileSupplies == null || _mobileProgress == null) return;
        _restoringSupply = true;
        var p = _mobileProgress;
        if (!p.SupplyStockInitialized)
        {
            p.SourceStock = MobileSupplySourceStock;
            p.RackStock = IsMobileUnlockBuilt(SalonUnlockId.WashStation) ? MobileSupplyRackCapacity : 0;
            p.CarriedStock = 0;
            p.SupplyStockInitialized = true;
        }
        // Replenish the crate at the next opening, preserving stock in the shop.
        _mobileSupplies.RestoreStock(_mobileRestoring ? p.SourceStock : MobileSupplySourceStock,
            p.CarriedStock, p.RackStock);
        _restoringSupply = false;
        _supplyDirty = false;
        ApplyMobileCoreEquipment();
    }

    private void CaptureMobileCoreStock(SalonProgressData data)
    {
        if (_mobileSupplies == null) return;
        data.SourceStock = _mobileSupplies.SourceWashKits;
        data.CarriedStock = _mobileSupplies.CarriedWashKits;
        data.RackStock = _mobileSupplies.WashRackWashKits;
        data.SupplyStockInitialized = true;
    }

    private void CompleteMobileCoreUnlock(SalonUnlockId id)
    {
        WriteMobileUnlockProgress(_mobileProgress);
        _mobileTeachingOrder = SalonServiceMenu.TeachingOrderAfter(id);
        if (id == SalonUnlockId.WashStation)
            _mobileSupplies.RestoreStock(MobileSupplySourceStock, 0, MobileSupplyRackCapacity);
    }

    private string PickMobileAvailableOrder(string picked)
    {
        return SalonPacingDirector.SelectOrder(_game, _mobileProgress, _nextCustomerId,
            _mobileTeachingOrder, _mobileSupplies?.WashRackWashKits ?? 0,
            _dayController.BusinessRemainingTime);
    }

    private bool MobileHasUsefulManagementWork()
    {
        if (_mobileProgress == null || _game == null) return false;
        if (_mobileProgress.SupplyRackIntroduced && _mobileSupplies != null &&
            _mobileSupplies.WashRackWashKits <= 2 &&
            (_mobileSupplies.CarriedWashKits > 0 || _mobileSupplies.SourceWashKits > 0)) return true;
        foreach (var definition in SalonUnlockRoute.Route)
            if (!IsMobileUnlockBuilt(definition.Id))
                return _game.Balance >= definition.Cost - MobileUnlockPaid(definition.Id);
        return false;
    }

    private void CheckMobileRestockIntroduction()
    {
        if (_restoringSupply || _mobileProgress == null || _mobileSupplies == null) return;
        _supplyDirty = true;
        if (IsMobileUnlockBuilt(SalonUnlockId.WashStation) && !_mobileProgress.SupplyRackIntroduced &&
            _mobileSupplies.WashRackWashKits <= 2)
        {
            _mobileProgress.SupplyRackIntroduced = true;
            ApplyMobileCoreEquipment();
            if (_mobileControls != null) BuildMobileCollisionMap();
            ShowToast("洗发水快用完了 · 到补货箱拿货，再送到洗头台旁的架子", 5f);
        }
    }

    private void BuildMobileCashier(Transform zone)
    {
        if (!_mobileMode) return;
        var asset = AssetManifestLoader.LoadFromResources().Find("furniture-cashier-counter");
        Vector3 counter = new Vector3(7.2f, .05f, -3.9f);
        foreach (var anchor in asset.InteractionAnchors)
        {
            if (anchor.Id == "cashier-player")
                _cashierPlayerPoint = Marker("Cashier Player Anchor", zone, counter + anchor.Position);
            if (anchor.Id == "checkout-queue")
                _cashierQueuePoint = Marker("Checkout Queue Anchor", zone, counter + anchor.Position);
        }
        if (_cashierPlayerPoint == null) return;
        Cylinder("Cashier Stand Here", zone, _cashierPlayerPoint.position,
            new Vector3(1.35f, .035f, 1.35f), Teal);
        var label = new GameObject("Checkout Label");
        label.transform.SetParent(zone, false);
        label.transform.position = _cashierPlayerPoint.position + Vector3.up * 1.2f;
        _cashierLabel = label.AddComponent<TextMesh>();
        _cashierLabel.font = SalonUiFactory.GetPackagedUiFont();
        _cashierLabel.GetComponent<MeshRenderer>().sharedMaterial = _cashierLabel.font.material;
        _cashierLabel.fontSize = 36;
        _cashierLabel.characterSize = .065f;
        _cashierLabel.anchor = TextAnchor.MiddleCenter;
        _cashierLabel.alignment = TextAlignment.Center;
        _cashierLabel.color = Cream;
        _cashierLabel.text = "收银 · 站此结账";
    }

    private void ResetMobileCheckout()
    {
        _escortTrails.Clear();
        _mobilePacing = new SalonPacingDirector();
        _mobileCheckout = new SalonCheckoutModel(queueCapacity: SalonUnlockRoute.MaxConcurrentCustomers(true));
        _mobileCheckout.Paid += payment =>
        {
            PaymentDropModel drop = FindLatestPayment(payment.CustomerId);
            if (!_game.Payments.BeginCollection(drop.Id) || !_game.Payments.CompleteCollection(drop.Id)) return;
            _dayController?.Stats.RecordPaymentCollected(drop);
            var served = _game.Customers.Find(c => c.Id == payment.CustomerId);
            if (served != null && served.IsComplete && served.ServiceResult != CustomerServiceResult.Failed)
            {
                _mobileProgress.PaidCustomerCount++;
                if (served.Needs.Contains(ServiceType.Dry)) _mobileProgress.PaidDryOrderCount++;
                if (served.Needs.Contains(ServiceType.Wash)) _mobileProgress.PaidWashOrderCount++;
            }
            _game.FinishCheckout(payment.CustomerId, true);
            if (_coinBalanceLabel != null) _coinBalanceLabel.text = _game.Balance.ToString("N0");
            ShowToast("收款 +" + payment.Amount);
            if (_cashierPlayerPoint != null && Application.isPlaying)
                StartCoroutine(AnimateCheckoutCoin());
            SaveMobileCheckpoint(false);
        };
        _mobileCheckout.Abandoned += payment =>
        {
            var customer = _game.Customers.Find(c => c.Id == payment.CustomerId);
            if (customer != null) FindCustomerView(customer)?.Comedy?.Play(CustomerComedyReactionKind.CheckoutTooLong);
            _game.FinishCheckout(payment.CustomerId, false);
            if (_dayController != null) _dayController.Stats.CheckoutAbandoned++;
            ShowToast("收银等太久 · 顾客没付钱就走了，口碑下降");
        };
    }

    private Vector3 MobileCheckoutSlot(int customerId)
    {
        var bill = _mobileCheckout?.Find(customerId);
        Vector3 head = _cashierQueuePoint != null ? _cashierQueuePoint.position : new Vector3(3.5f, .4f, -2.1f);
        head.y = .4f;
        return head + Vector3.left * (Mathf.Max(0, bill?.QueueIndex ?? 0) * .85f);
    }

    private void UpdateMobileCoreFlow(float dt)
    {
        if (_mobileCheckout == null) ResetMobileCheckout();
        bool atCashier = _cashierPlayerPoint != null &&
            (FlatDistance(_player.position, _cashierPlayerPoint.position) <= 1f ||
             (_coopPlayerTwo?.Transform != null && FlatDistance(_coopPlayerTwo.Transform.position,
                 _cashierPlayerPoint.position) <= 1f));
        _mobileCheckout.Tick(dt, atCashier);
        UpdateMobileTutorial();
        if (_cashierLabel != null)
        {
            _cashierLabel.text = _mobileCheckout.Queue.Count > 0
                ? "收银 " + _mobileCheckout.Queue.Count + " 人 · 站此结账" : "收银 · 站此结账";
            _cashierLabel.transform.rotation = _camera.transform.rotation;
        }
        if (_supplyDirty && (_supplySaveElapsed += dt) >= 1f)
        {
            _supplySaveElapsed = 0f;
            _supplyDirty = false;
            SaveMobileCheckpoint(false);
        }
    }

    private void UpdateMobileTutorial()
    {
        if (_mobileProgress == null || _camera == null || _player == null) return;
        Vector3? target = null;
        string title = string.Empty;
        _mobileTutorialHint = null;
        if (_mobileGuidedCustomer != null)
        {
            foreach (var station in _game.Workstations)
                if (station.IsUsable && !station.Occupied &&
                    SalonGameModel.IsCompatibleStation(_mobileGuidedCustomer.CurrentNeed, station.Type) &&
                    _playerServiceAnchors.TryGetValue(station.Id, out var anchor))
                { target = anchor.position; title = "带顾客到这里"; break; }
        }
        else if (_mobileCheckout.HasPendingCustomers && _cashierPlayerPoint != null)
        {
            target = _cashierPlayerPoint.position; title = "收银";
            _mobileTutorialHint = "顾客在等结账 · 站到收银台绿色圆圈，自动收钱";
        }
        else if (_mobileProgress.SupplyRackIntroduced && _mobileSupplies.WashRackWashKits <= 2 &&
            (_mobileSupplies.CarriedWashKits > 0 || _mobileSupplies.SourceWashKits > 0))
        {
            bool carrying = _mobileSupplies.CarriedWashKits > 0;
            target = carrying ? _mobileWashRackPoint.position : _mobileSupplySourcePoint.position;
            title = carrying ? "把洗发水送到这里" : "取洗发水";
            _mobileTutorialHint = carrying ? "靠近洗头台旁的架子自动放货" : "洗发水不足 · 靠近补货箱自动拿货，再送回架子";
        }
        else
        {
            foreach (var pad in _mobileUnlockPads.Values)
                if (IsMobileUnlockPadVisible(pad.Id) && _game.Balance >= pad.Model.RemainingCost && pad.Point != null)
                {
                    target = pad.Point.position; title = "解锁" + pad.Definition.DisplayName;
                    _mobileTutorialHint = SalonUnlockRoute.LearningPurpose(pad.Id) + " · 闲时去金色施工点解锁";
                    break;
                }
        }
        if (_mobileGuideArrow == null && target.HasValue)
        {
            var arrow = new GameObject("Mobile Teaching Arrow");
            arrow.transform.SetParent(transform, false);
            _mobileGuideArrow = arrow.AddComponent<TextMesh>();
            _mobileGuideArrow.font = SalonUiFactory.GetPackagedUiFont();
            arrow.GetComponent<MeshRenderer>().sharedMaterial = _mobileGuideArrow.font.material;
            _mobileGuideArrow.fontSize = 40; _mobileGuideArrow.characterSize = .055f;
            _mobileGuideArrow.anchor = TextAnchor.MiddleCenter;
            _mobileGuideArrow.alignment = TextAlignment.Center;
            _mobileGuideArrow.color = Gold;
        }
        if (_mobileGuideArrow == null) return;
        _mobileGuideArrow.gameObject.SetActive(target.HasValue && CanInteractWithSalon());
        if (!target.HasValue) return;
        _mobileGuideArrow.text = title + "\n▼";
        _mobileGuideArrow.transform.position = target.Value + Vector3.up * (2.5f + Mathf.Sin(Time.time * 4f) * .12f);
        _mobileGuideArrow.transform.rotation = _camera.transform.rotation;
    }

    private IEnumerator AnimateCheckoutCoin()
    {
        if (_coinBalanceLabel == null || _camera == null) yield break;
        var coin = new GameObject("Checkout Coin", typeof(RectTransform), typeof(Image));
        coin.transform.SetParent(_coinBalanceLabel.canvas.transform, false);
        Image icon = coin.GetComponent<Image>();
        icon.sprite = SalonUiFactory.GetCircleSprite();
        icon.color = Gold;
        icon.raycastTarget = false;
        icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
        Vector3 start = _camera.WorldToScreenPoint(_cashierPlayerPoint.position + Vector3.up * 2f);
        for (float t = 0f; t < .55f; t += Time.unscaledDeltaTime)
        {
            coin.transform.position = Vector3.Lerp(start, _coinBalanceLabel.transform.position, t / .55f);
            yield return null;
        }
        Destroy(coin);
    }
}
