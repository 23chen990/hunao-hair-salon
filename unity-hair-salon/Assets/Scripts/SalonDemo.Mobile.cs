using System;
using System.Collections.Generic;
using HairSalon;
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
    private readonly List<Rect> _mobileObstacles = new List<Rect>();
    private Rect _mobileFloor;
    private Text _mobileGoalLabel;
    private GameObject _mobileGoalRoot;
    private Button _mobileRetryButton;
    private Button _mobileCancelGuideButton;
    private Image _mobileProgressFill;
    private string _mobileActionLabel = "靠近顾客";
    private bool _mobileActionAvailable;
    private float _mobileEvidenceAt;
    private bool _mobileRestoring;

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
        // A visible grace window gives a player time to run to a seated customer.
        PatienceSettings.DrainPerSecond = 1.15f;
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
    }

    private void BuildMobileCollisionMap()
    {
        const float bodyRadius = .15f;
        _mobileObstacles.Clear();
        foreach (var obstacle in FindObjectsByType<SalonFurnitureObstacle>(FindObjectsInactive.Include))
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
        data.FirstDayComplete = _game.FirstDayCompleteForShop;
        data.ReputationStars = _dayController.Reputation.CurrentStars;
        data.ShopSatisfaction = _satisfaction.CurrentSatisfaction;
        data.DaySettled = settled;
        return data;
    }

    private void SaveMobileCheckpoint(bool settled)
    {
        if (!_mobileMode || _mobileRestoring || _mobileSaves == null) return;
        _mobileProgress = CaptureMobileProgress(settled);
        if (!_mobileSaves.Save(_mobileProgress))
            ShowToast("本次进度无法保存，请保留当前页面");
        if (!settled) _mobileOpening = _mobileProgress.Clone();
    }

    private void HandleMobileDayState(DayState state)
    {
        if (!_mobileMode) return;
        _mobileControls?.ResetInput();
        if (state == DayState.PreOpen)
        {
            SalonMobileDayConfig.ApplyForDay(DaySettings, _dayController.DayNumber);
            _nextCustomerId = 0;
            _satisfaction.BeginDay(_satisfaction.CurrentSatisfaction);
            _mobileGuidedCustomer = null;
            EndMobileWork();
            if (_player != null) _player.position = new Vector3(0f, .05f, -3.2f);
            if (!_mobileRestoring) SaveMobileCheckpoint(false);
        }
        if (state == DayState.Result)
        {
            EndMobileWork();
            _mobileGuidedCustomer = null;
            bool passed = _dayController.Stats.CompletedOrders >= DaySettings.TargetOrders;
            _resultTitleLabel.text = "DAY " + _dayController.DayNumber + (passed ? "  目标达成！" : "  差一点，再试一次");
            _resultSummaryLabel.text = "完成订单   " + _dayController.Stats.CompletedOrders + " / " + DaySettings.TargetOrders +
                "\n\n" + (passed ? "忙碌的一天完成了，去升级设备吧！" : "优先处理快失去耐心的顾客，\n吹发运行时去接待下一位。") +
                "\n\n订单收入   " + _dayController.Stats.OrderIncome +
                "\n小费收入   " + _dayController.Stats.TipIncome +
                "\n离店顾客   " + _game.AngryLeaves +
                "\n\n" + (passed ? "进度已保存" : "重试会恢复今天开店前的资金和设备");
            _resultContinueButton.gameObject.SetActive(passed);
            _mobileRetryButton.gameObject.SetActive(!passed);
            if (passed)
            {
                _mobileProgress.TutorialCompleted = true;
                int index = _mobileProgress.CompletedDays.IndexOf(_dayController.DayNumber);
                if (index < 0)
                {
                    _mobileProgress.CompletedDays.Add(_dayController.DayNumber);
                    _mobileProgress.BestCompletedOrders.Add(_dayController.Stats.CompletedOrders);
                }
                else _mobileProgress.BestCompletedOrders[index] = Mathf.Max(
                    _mobileProgress.BestCompletedOrders[index], _dayController.Stats.CompletedOrders);
                SaveMobileCheckpoint(true);
            }
            else if (_mobileOpening != null) _mobileSaves.Save(_mobileOpening);
        }
        RefreshMobileDayPresentation();
    }

    private void RefreshMobileDayPresentation()
    {
        if (!_mobileMode || _dayController == null) return;
        if (_dayController.State == DayState.PreOpen && _preOpenInfoLabel != null)
        {
            _preOpenInfoLabel.fontSize = 24;
            _preOpenInfoLabel.text = "2 分钟 · 完成 " + DaySettings.TargetOrders + " 单\n左摇杆移动 · 右按钮就近操作\n接待 → 安排工位 → 服务，吹发可离开";
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
            _mobileGoalLabel.text = "目标 " + _dayController.Stats.CompletedOrders + "/" + DaySettings.TargetOrders + " 单  ·  " + phase;
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
        ClearDayPaymentPickups();
        _game.ResetForNextDay();
        _game.RestorePersistentState(checkpoint.Balance, checkpoint.AutoBlowPurchased, checkpoint.FirstDayComplete);
        _dayController.RestoreReputation(checkpoint.ReputationStars);
        _satisfaction.BeginDay(checkpoint.ShopSatisfaction);
        _mobileProgress = checkpoint;
        _dayController.PrepareDay(checkpoint.DayNumber);
        _mobileRestoring = false;
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        SaveMobileCheckpoint(false);
        ApplyOverview(true);
    }

    private void UpdateMobilePlay(float dt)
    {
        if (_mobileControls == null) return;
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
        if (_mobileGuidedCustomer != null &&
            (_mobileGuidedCustomer.State == CustomerState.Leaving || _mobileGuidedCustomer.State == CustomerState.Exited))
            _mobileGuidedCustomer = null;
        MobileTarget target = FindMobileTarget();
        _mobileActionLabel = target.Label;
        _mobileActionAvailable = target.Available;
        _mobileControls.SetInteraction(target.Label, target.Available);
        _mobileControls.SetHint(target.Hint);
        foreach (var pair in _selectionPlates)
            pair.Value.SetActive(target.Available && target.Station == pair.Key && target.Action != MobileAction.Greet);
        if (_mobileControls.ConsumeInteractionPressed() && target.Available) ExecuteMobileTarget(target);
        CollectNearbyMobilePayments();
    }

    private MobileTarget FindMobileTarget()
    {
        var target = new MobileTarget { Station = -1, Label = "靠近顾客", Hint = "移动到等候顾客身边，点右侧按钮接待" };
        if (_mobileGuidedCustomer != null)
        {
            target.Customer = _mobileGuidedCustomer;
            target.Label = "前往工位";
            string service = MobileServiceName(_mobileGuidedCustomer.CurrentNeed);
            target.Hint = "已接待 " + (_mobileGuidedCustomer.Id + 1) + " 号 · 前往空闲" + service + "工位安排";
            float best = 1.5f;
            foreach (var pair in _playerServiceAnchors)
            {
                if (_game.IsStationOccupied(pair.Key) || !SalonGameModel.IsCompatibleStation(
                    _mobileGuidedCustomer.CurrentNeed, _game.Workstations[pair.Key].Type)) continue;
                float distance = FlatDistance(_player.position, pair.Value.position);
                if (distance > best) continue;
                best = distance;
                target.Station = pair.Key;
                target.Action = MobileAction.Assign;
                target.Label = "安排" + service;
                target.Available = true;
            }
            return target;
        }
        float nearest = 1.8f;
        foreach (var view in _customerViews)
        {
            if (view == null || view.Customer == null) continue;
            CustomerModel customer = view.Customer;
            if (customer.State != CustomerState.Waiting && customer.State != CustomerState.Serving) continue;
            bool waiting = customer.State == CustomerState.Waiting;
            Vector3 anchor = !waiting && _playerServiceAnchors.TryGetValue(customer.Station, out var work)
                ? work.position : view.transform.position;
            float distance = FlatDistance(_player.position, anchor);
            if (distance > nearest || (!waiting && distance > 1.45f)) continue;
            nearest = distance;
            target.Customer = customer;
            target.Station = customer.Station;
            target.Available = view.IsAtMovementDestination;
            if (!view.IsAtMovementDestination)
            {
                target.Label = "顾客入座中";
                target.Hint = "等顾客到达座位后即可操作";
                continue;
            }
            target.Hint = (customer.Id + 1) + " 号 · " + MobileServiceName(customer.CurrentNeed);
            if (waiting)
            {
                target.Action = MobileAction.Greet;
                target.Label = "接待 " + (customer.Id + 1) + " 号";
                continue;
            }
            if (!SalonGameModel.IsCompatibleStation(customer.CurrentNeed, _game.Workstations[customer.Station].Type))
            {
                target.Action = MobileAction.Guide;
                target.Label = "转移顾客";
                target.Hint += " · 带到下一工位";
            }
            else if (customer.CurrentNeed == ServiceType.Wash)
            {
                if (customer.ActiveServiceAction == ActiveServiceAction.Shampoo)
                {
                    target.Action = MobileAction.Wash;
                    target.Available = false;
                    target.Label = "洗发中";
                    target.Hint += " · 正在打泡沫";
                }
                else if (_game.IsWashFoamWaitRunning(customer))
                {
                    // 后台泡沫等待：这是玩家可以离开、之后必须回来处理的节奏点。
                    target.Action = MobileAction.RinseWash;
                    bool ready = _game.IsWashFoamReadyToRinse(customer);
                    target.Available = ready;
                    target.Label = ready ? "冲洗" : "泡沫中";
                    target.Hint += ready ? " · 回来冲洗收尾" : " · 可以先照顾其他顾客";
                }
                else
                {
                    target.Action = MobileAction.Wash;
                    target.Label = "洗发";
                    target.Hint += " · 点一下，然后可以先走开";
                }
            }
            else if (customer.CurrentNeed == ServiceType.Cut)
            {
                target.Action = MobileAction.Cut;
                target.Label = "剪发";
                target.Hint += " · 点一下，短时完成";
            }
            else if (customer.CurrentNeed == ServiceType.Dry)
            {
                bool running = customer.AutoBlowRunning || customer.AutoBlowSafetyStopped;
                bool ready = running && customer.BackgroundTask.Elapsed >= customer.BackgroundTask.IdealStart;
                target.Action = running ? MobileAction.FinishDry : MobileAction.StartDry;
                target.Available = !running || ready;
                target.Label = !running ? "启动吹发" : ready ? "吹发收尾" : "吹发运行中";
                target.Hint += running ? ready ? " · 回来收尾即可完成" : " · 可以先照顾其他顾客" : " · 启动后可离开";
            }
        }
        return target;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    private static string MobileServiceName(ServiceType service)
        => service == ServiceType.Wash ? "洗发" : service == ServiceType.Cut ? "剪发" : "吹发";

    private void ExecuteMobileTarget(MobileTarget target)
    {
        CustomerModel customer = target.Customer;
        if (customer == null) return;
        _game.SelectCustomer(customer);
        if (target.Action == MobileAction.Greet || target.Action == MobileAction.Guide)
        {
            _mobileGuidedCustomer = customer;
            ShowToast("接待成功 · 去空闲" + MobileServiceName(customer.CurrentNeed) + "工位");
        }
        else if (target.Action == MobileAction.Assign)
        {
            if (_game.Assign(customer, target.Station))
            {
                _mobileGuidedCustomer = null;
                ShowToast("顾客正在入座");
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
            _mobileWorkingTool = customer.HaircutService == null ? SalonTool.Scissors : customer.HaircutService.CurrentRequiredTool;
            bool began = wash
                ? _game.BeginWashFoamHold(customer)
                : _game.BeginHaircutAction(customer, _mobileWorkingTool, HaircutSettings);
            if (!began) { ShowToast("顾客尚未准备好"); return; }
            _mobileWorkingView = view;
            _mobileWorkingService = wash ? ServiceType.Wash : ServiceType.Cut;
            _mobileWorkElapsed = 0f;
            _mobileWorkDuration = wash
                ? ServiceSettings.ShampooDuration
                : _game.HaircutHoldDurationFor(_mobileWorkingTool, HaircutSettings);
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
        _mobileWorkElapsed += dt;
        float progress = Mathf.Clamp01(_mobileWorkElapsed / Mathf.Max(.1f, _mobileWorkDuration));
        _mobileControls.SetInteraction(MobileServiceName(_mobileWorkingService) + "中", false);
        _mobileControls.SetHint("正在" + MobileServiceName(_mobileWorkingService) + " · " + Mathf.RoundToInt(progress * 100f) + "%");
        _mobileProgressFill.transform.parent.gameObject.SetActive(true);
        _mobileProgressFill.fillAmount = progress;
        view.ActionProgress?.SetProgressForService(_mobileWorkingService, "", progress, Teal);
        if (_mobileWorkingService == ServiceType.Cut)
            view.HaircutFeedback?.TickHold(_mobileWorkElapsed, progress);
        if (_mobileWorkElapsed < _mobileWorkDuration) return;
        if (_mobileWorkingService == ServiceType.Cut)
        {
            HaircutResult result = _game.CompleteHaircutAction(view.Customer, _mobileWorkDuration, false);
            _game.EndActiveOperation(view.Customer);
            view.HaircutFeedback?.Stop(result);
            view.ApplyHairStage(view.Customer.HairStage);
        }
        view.OrderDemand?.Refresh();
        ShowToast(_mobileWorkingService == ServiceType.Wash
            ? "泡沫已打好 · 可以先去照顾别人，回来冲洗"
            : MobileServiceName(_mobileWorkingService) + "完成" + (view.Customer.IsComplete ? "！" : " · 继续下一项"));
        EndMobileWork();
    }

    private void EndMobileWork()
    {
        if (_mobileWorkingView != null) _mobileWorkingView.ActionProgress?.ClearProgress();
        _mobileWorkingView = null;
        _mobileWorkElapsed = 0f;
        _playerCharacter?.EndService();
        if (_mobileProgressFill != null) _mobileProgressFill.transform.parent.gameObject.SetActive(false);
    }

    private void CollectMobilePaymentsAtClose()
    {
        foreach (var drop in _game.Payments.Drops)
        {
            if (drop.State == PaymentDropState.Collected) continue;
            _game.Payments.BeginCollection(drop.Id);
            if (_game.Payments.CompleteCollection(drop.Id)) _dayController.Stats.RecordPaymentCollected(drop);
        }
        if (_coinBalanceLabel != null) _coinBalanceLabel.text = _game.Balance.ToString("N0");
    }

    private void CollectNearbyMobilePayments()
    {
        foreach (var pile in FindObjectsByType<CoinPileView>())
        {
            if (pile.Drop != null && _playerServiceAnchors.TryGetValue(pile.Drop.WorkstationId, out var anchor) &&
                SalonMobileNavigation.CanReach(_player.position, anchor.position, 1.8f)) HandleCoinPileClick(pile);
        }
    }

    // Read-only evidence for real browser input checks; absent from release builds.
    private void EmitMobileEvidence()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_mobileMode || !Application.absoluteURL.Contains("mobileEvidence=1") ||
            Time.unscaledTime < _mobileEvidenceAt || _player == null) return;
        _mobileEvidenceAt = Time.unscaledTime + .3f;
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
        public int day, completed, target, balance, guided, working, satisfaction;
        public bool paused, purchased, available;
        public string state, action;
        public float progress;
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
        public int id, station, step;
        public string state, need;
        public Vector3 position, screen;
        public bool arrived, autoRunning, autoStopped, complete;
        public float patience, autoElapsed, autoReady;
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
