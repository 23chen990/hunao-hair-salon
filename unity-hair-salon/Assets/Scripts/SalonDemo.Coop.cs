using System;
using HairSalon;
using HairSalon.Character;
using UnityEngine;

public sealed partial class SalonDemo
{
    private bool _coopMode;
    private SalonCoopPlayerState _coopPlayerTwo;
    private float _coopEvidenceAt;
    private bool _coopReadyEvidenceLogged;
    private bool _coopFlowEvidenceLogged;

    /// <summary>
    /// Local same-device co-op is an opt-in runtime mode. It deliberately has
    /// no network or account dependency, so the normal demo URL remains the
    /// approved single-player experience.
    /// </summary>
    public static bool IsLocalCoopRequested(string absoluteUrl, string[] args)
    {
        string url = (absoluteUrl ?? string.Empty).ToLowerInvariant();
        if (url.Contains("?multiplayer=1") || url.Contains("&multiplayer=1") ||
            url.Contains("?multiplayer=true") || url.Contains("&multiplayer=true"))
            return true;
        if (args == null) return false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;
            if (string.Equals(arg, "-salonMultiplayer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--salonMultiplayer", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void BuildCoopPlayerTwoAvatar(Transform salon)
    {
        if (!_coopMode || salon == null) return;
        GameObject prefab = Resources.Load<GameObject>("Characters/Hairdresser");
        if (prefab == null) return;
        GameObject instance = Instantiate(prefab, salon);
        instance.name = "P2 理发师";
        instance.transform.position = new Vector3(1.8f, .05f, -3.2f);
        HairdresserCharacter character = instance.GetComponent<HairdresserCharacter>();
        _coopPlayerTwo = new SalonCoopPlayerState(2)
        {
            Transform = instance.transform,
            Character = character
        };
        Cylinder("P2 Marker", instance.transform, new Vector3(0f, -.02f, 0f),
            new Vector3(.72f, .025f, .72f), Purple);
    }

    private void BuildCoopControls(Transform safeParent)
    {
        if (!_coopMode || safeParent == null) return;
        if (_coopPlayerTwo == null)
            _coopPlayerTwo = new SalonCoopPlayerState(2);
        _mobileControls?.ConfigureForPlayer(1, true);
        _coopPlayerTwo.Controls = SalonMobileControls.Create(safeParent);
        _coopPlayerTwo.Controls.ConfigureForPlayer(2, true);
        _coopPlayerTwo.Controls.SetHint("P2 ↑↓←→ 移动 · Ctrl / Enter 操作");
    }

    private void HandleCoopDayState(DayState state)
    {
        if (!_coopMode || _coopPlayerTwo == null) return;
        _coopPlayerTwo.Controls?.ResetInput();
        if (state == DayState.PreOpen || state == DayState.Result)
        {
            EndCoopWork();
            _coopPlayerTwo.GuidedCustomer = null;
            if (_coopPlayerTwo.Transform != null)
                _coopPlayerTwo.Transform.position = new Vector3(1.8f, .05f, -3.2f);
            _game?.ClearFocus(2);
        }
    }

    private void RefreshCoopDayPresentation(bool playable)
    {
        if (!_coopMode || _coopPlayerTwo == null) return;
        _coopPlayerTwo.Controls?.SetVisible(playable);
        if (!playable) _coopPlayerTwo.Controls?.ResetInput();
    }

    private void SuspendCoopInput()
    {
        if (!_coopMode || _coopPlayerTwo == null) return;
        if (_coopPlayerTwo.IsWorking && _coopPlayerTwo.WorkingService == ServiceType.Cut)
            _coopPlayerTwo.HaircutSuspended = true;
        _coopPlayerTwo.Controls?.ResetInput();
    }

    private void UpdateCoopPlayerTwo(float dt)
    {
        SalonCoopPlayerState player = _coopPlayerTwo;
        if (!_coopMode || player == null || player.Controls == null || player.Transform == null) return;
        if (player.IsWorking)
        {
            TickCoopWork(player, dt);
            return;
        }

        Vector3 before = player.Transform.position;
        Vector3 cameraRight = _simple2DMode ? Vector3.right : _camera.transform.right;
        Vector3 cameraForward = _simple2DMode ? Vector3.forward : _camera.transform.forward;
        Vector3 next = SalonMobileNavigation.Move(before, player.Controls.Move,
            cameraRight, cameraForward, 5.5f * dt, _mobileFloor, _mobileObstacles);
        if (player.Character != null)
        {
            player.Character.Move(next - before, dt);
            player.Transform.position = next;
        }
        else player.Transform.position = next;

        // P1 owns the shared supply/purchase clocks during the normal loop.
        // If P1 is in a foreground action its legacy loop returns early, so
        // P2 keeps those shared clocks alive exactly once here.
        if (_mobileWorkingView != null)
        {
            UpdateMobileSupplyLoop(dt);
            UpdateMobilePurchasePad(dt);
            UpdateMobileHaircutExpansionPad(dt);
        }
        MobileTarget target = FindCoopMobileTarget(player);
        player.ActionLabel = target.Label;
        player.ActionAvailable = target.Available;
        player.Controls.SetInteraction(target.Label, target.Available);
        player.Controls.SetHint(target.Hint);
        if (player.Controls.ConsumeInteractionPressed() && target.Available)
            ExecuteCoopMobileTarget(player, target);
        UpdateCoopCameraFollow();
    }

    private MobileTarget FindCoopMobileTarget(SalonCoopPlayerState player)
    {
        MobileTarget fallback = new MobileTarget
        {
            Action = MobileAction.None,
            Station = -1,
            Label = "靠近顾客",
            Hint = "P2 靠近顾客 · Ctrl 操作",
            Available = false
        };
        if (_game == null || player == null || player.Transform == null) return fallback;

        if (player.GuidedCustomer != null &&
            player.GuidedCustomer.State != CustomerState.Leaving &&
            player.GuidedCustomer.State != CustomerState.Exited)
        {
            CustomerModel guidedCustomer = player.GuidedCustomer;
            MobileTarget guided = new MobileTarget
            {
                Action = MobileAction.Assign,
                Customer = guidedCustomer,
                Station = -1,
                Label = "前往工位",
                Hint = "P2 已接待 " + (guidedCustomer.Id + 1) + " 号 · 前往空闲工位"
            };
            float nearest = float.PositiveInfinity;
            foreach (var pair in _playerServiceAnchors)
            {
                if (pair.Key < 0 || pair.Key >= _game.Workstations.Count ||
                    !_game.Workstations[pair.Key].IsUsable || _game.IsStationOccupied(pair.Key)) continue;
                float distance = FlatDistance(player.Transform.position, pair.Value.position);
                if (distance < nearest)
                {
                    nearest = distance;
                    guided.Station = pair.Key;
                }
            }
            if (guided.Station >= 0 && nearest <= 1.6f)
            {
                WorkstationType stationType = _game.Workstations[guided.Station].Type;
                guided.Available = true;
                guided.Label = SalonGameModel.IsCompatibleStation(guidedCustomer.CurrentNeed, stationType)
                    ? "安排" + MobileServiceName(guidedCustomer.CurrentNeed)
                    : "安排" + MobileWorkstationName(stationType);
            }
            return guided;
        }

        float nearestDistance = float.PositiveInfinity;
        foreach (SalonCustomerView view in _customerViews)
        {
            if (view == null || view.Customer == null) continue;
            CustomerModel customer = view.Customer;
            if (customer.State != CustomerState.Waiting && customer.State != CustomerState.Serving) continue;
            if (customer.InteractionOwnerPlayerId > 0 && customer.InteractionOwnerPlayerId != player.PlayerId)
                continue;
            bool waiting = customer.State == CustomerState.Waiting;
            Vector3 anchor = view.transform.position;
            if (!waiting && customer.Station >= 0 &&
                _playerServiceAnchors.TryGetValue(customer.Station, out Transform serviceAnchor))
                anchor = serviceAnchor.position;
            float distance = FlatDistance(player.Transform.position, anchor);
            if (distance > (waiting ? 1.9f : 1.55f) || distance >= nearestDistance) continue;
            MobileTarget candidate = new MobileTarget
            {
                Customer = customer,
                Station = customer.Station,
                Available = view.IsAtMovementDestination || waiting,
                Hint = "P2 · " + (customer.Id + 1) + " 号 · " + MobileServiceName(customer.CurrentNeed)
            };
            if (waiting)
            {
                candidate.Action = MobileAction.Greet;
                candidate.Label = "接待 " + (customer.Id + 1) + " 号";
            }
            else if (!view.IsAtMovementDestination)
            {
                candidate.Label = "顾客入座中";
                candidate.Hint = "P2 等顾客到达座位后操作";
            }
            else if (customer.CurrentNeed == ServiceType.Wash)
            {
                if (customer.ActiveServiceAction == ActiveServiceAction.Shampoo)
                {
                    candidate.Action = MobileAction.Wash;
                    candidate.Available = false;
                    candidate.Label = "洗发中";
                }
                else if (_game.IsWashFoamWaitRunning(customer))
                {
                    candidate.Action = MobileAction.RinseWash;
                    candidate.Available = _game.IsWashFoamReadyToRinse(customer);
                    candidate.Label = candidate.Available ? "冲洗" : "泡沫中";
                }
                else
                {
                    candidate.Action = MobileAction.Wash;
                    candidate.Label = "洗发";
                }
            }
            else if (customer.CurrentNeed == ServiceType.Cut)
            {
                candidate.Action = MobileAction.Cut;
                candidate.Label = "剪发";
                candidate.Hint += " · 按住 Ctrl 到绿色区间松手";
            }
            else if (customer.CurrentNeed == ServiceType.Dry)
            {
                bool running = customer.AutoBlowRunning || customer.AutoBlowSafetyStopped;
                bool ready = running && customer.BackgroundTask.Elapsed >= customer.BackgroundTask.IdealStart;
                candidate.Action = running ? MobileAction.FinishDry : MobileAction.StartDry;
                candidate.Available = !running || ready;
                candidate.Label = !running ? "启动吹发" : ready ? "吹发收尾" : "吹发运行中";
            }
            nearestDistance = distance;
            fallback = candidate;
        }
        return fallback;
    }

    private void ExecuteCoopMobileTarget(SalonCoopPlayerState player, MobileTarget target)
    {
        CustomerModel customer = target.Customer;
        if (player == null || customer == null || _game == null) return;
        _game.SelectCustomer(player.PlayerId, customer);
        if (target.Action == MobileAction.Greet)
        {
            if (!_game.EngageCustomerForHandoff(customer))
            {
                ShowToast("顾客正在离店或已被接待");
                return;
            }
            player.GuidedCustomer = customer;
            ShowToast("P2 接待成功 · 去空闲" + MobileServiceName(customer.CurrentNeed) + "工位");
        }
        else if (target.Action == MobileAction.Assign)
        {
            if (customer.CurrentNeed == ServiceType.Cut && customer.HaircutService == null)
                _game.ConfigureHaircutOrder(customer,
                    SalonMobileDayConfig.GetHaircutToolsForSpawn(DaySettings.MobileDayNumber, customer.Id));
            if (_game.Assign(customer, target.Station))
            {
                player.GuidedCustomer = null;
                ShowToast("P2 已安排顾客入座");
            }
        }
        else if (target.Action == MobileAction.RinseWash)
        {
            if (_game.FinishWashRinse(player.PlayerId, customer))
                ShowToast("P2 冲洗完成");
        }
        else if (target.Action == MobileAction.StartDry)
        {
            if (_game.StartAutoBlow(customer)) ShowToast("P2 吹发已启动");
        }
        else if (target.Action == MobileAction.FinishDry)
        {
            BlowResult result = _game.FinishAutoBlow(customer);
            if (result != BlowResult.Undone) ShowToast("P2 吹发收尾完成");
        }
        else if (target.Action == MobileAction.Wash || target.Action == MobileAction.Cut)
        {
            SalonCustomerView view = FindCustomerView(customer);
            if (view == null || !view.IsAtMovementDestination) return;
            bool wash = target.Action == MobileAction.Wash;
            if (wash && _mobileSupplies != null && !_mobileSupplies.CanStartWash)
            {
                ShowToast("洗发用品用完了 · 去洗发区补");
                return;
            }
            player.WorkingTool = customer.HaircutService == null
                ? SalonTool.Scissors : customer.HaircutService.CurrentRequiredTool;
            bool began = wash
                ? _game.BeginWashFoamHold(player.PlayerId, customer)
                : _game.BeginHaircutAction(player.PlayerId, customer, player.WorkingTool, HaircutSettings);
            if (!began)
            {
                ShowToast("P2 顾客尚未准备好");
                return;
            }
            if (wash && _mobileSupplies != null && !_mobileSupplies.TryConsumeWashKit())
            {
                _game.CancelActiveServiceAction(customer);
                ShowToast("洗发用品不足 · 请先补");
                return;
            }
            player.WorkingView = view;
            player.WorkingService = wash ? ServiceType.Wash : ServiceType.Cut;
            player.WorkElapsed = 0f;
            player.WorkDuration = wash ? ServiceSettings.ShampooDuration :
                HaircutSettings.GetPerfectMax(player.WorkingTool);
            player.HaircutSuspended = false;
            player.Character?.FaceTowards(view.transform.position - player.Transform.position);
            player.Character?.BeginService(wash ? HairdresserAnimationState.WashHair : HairdresserAnimationState.CutHair);
            if (!wash) view.HaircutFeedback?.BeginHold(player.WorkingTool);
        }
    }

    private void TickCoopWork(SalonCoopPlayerState player, float dt)
    {
        SalonCustomerView view = player.WorkingView;
        if (view == null || view.Customer == null ||
            view.Customer.State == CustomerState.Leaving || view.Customer.State == CustomerState.Exited)
        {
            EndCoopWork();
            return;
        }
        if (player.WorkingService == ServiceType.Cut)
        {
            bool held = player.Controls != null && player.Controls.InteractionHeld;
            if (player.HaircutSuspended && !held)
            {
                player.Controls.SetInteraction("继续剪发", true);
                player.Controls.SetHint("P2 按住继续剪发 · 绿色时松手");
                return;
            }
            if (held)
            {
                player.HaircutSuspended = false;
                player.WorkElapsed += Mathf.Max(0f, dt);
            }
            if (player.WorkElapsed <= 0f && !held)
            {
                player.Controls.SetInteraction("按住剪发", true);
                player.Controls.SetHint("P2 按住 Ctrl 开始剪发");
                return;
            }
            float perfectMin = Mathf.Max(.01f, HaircutSettings.GetPerfectMin(player.WorkingTool));
            float perfectMax = HaircutSettings.GetPerfectMax(player.WorkingTool);
            float progress = Mathf.Clamp01(player.WorkElapsed / perfectMax);
            bool ready = player.WorkElapsed >= perfectMin;
            player.Controls.SetInteraction(ready ? "松手完成" : "按住剪发", true);
            player.Controls.SetHint(ready ? "P2 现在松手 · 剪发完成" : "P2 正在剪发 · 绿色时松手");
            view.ActionProgress?.SetProgressForService(ServiceType.Cut, "", progress,
                ready ? SalonPalette.Success : SalonPalette.Warning);
            view.HaircutFeedback?.TickHold(player.WorkElapsed, progress);
            if (held && player.WorkElapsed <= perfectMax) return;
            HaircutResult result = _game.CompleteHaircutAction(player.PlayerId, view.Customer,
                player.WorkElapsed, false);
            _game.EndActiveOperation(player.PlayerId, view.Customer);
            view.HaircutFeedback?.Stop(result);
            view.ApplyHairStage(view.Customer.HairStage);
            view.OrderDemand?.Refresh();
            ShowToast(result == HaircutResult.Perfect ? "P2 剪发完成" : "P2 剪发需要再试一次");
            EndCoopWork();
            return;
        }

        player.WorkElapsed += Mathf.Max(0f, dt);
        float washProgress = Mathf.Clamp01(player.WorkElapsed / Mathf.Max(.1f, player.WorkDuration));
        player.Controls.SetInteraction("洗发中", false);
        player.Controls.SetHint("P2 正在洗发 · " + Mathf.RoundToInt(washProgress * 100f) + "%");
        view.ActionProgress?.SetProgressForService(ServiceType.Wash, "", washProgress, Teal);
        if (player.WorkElapsed >= player.WorkDuration)
        {
            view.OrderDemand?.Refresh();
            ShowToast("P2 泡沫已打好");
            EndCoopWork();
        }
    }

    private void EndCoopWork()
    {
        SalonCoopPlayerState player = _coopPlayerTwo;
        if (player == null) return;
        CustomerModel customer = player.WorkingView == null ? null : player.WorkingView.Customer;
        if (customer != null && customer.InteractionOwnerPlayerId == player.PlayerId)
            _game.EndActiveOperation(player.PlayerId, customer);
        if (player.WorkingView != null)
            player.WorkingView.ActionProgress?.ClearProgress();
        player.Character?.EndService();
        _game?.GetOrCreatePlayerContext(player.PlayerId).ClearInteractionContext();
        player.ResetInteraction();
    }

    private void UpdateCoopCameraFollow()
    {
        if (!_coopMode || _simple2DMode || _camera == null || _player == null ||
            _coopPlayerTwo == null || _coopPlayerTwo.Transform == null) return;
        bool expanded = _mobileHaircutExpansionPad != null && _mobileHaircutExpansionPad.IsUnlocked;
        Rect bounds = expanded ? MobileSalonBounds : MobileZoneABounds;
        Vector3 p1 = _player.position;
        Vector3 p2 = _coopPlayerTwo.Transform.position;
        float distanceX = Mathf.Abs(p1.x - p2.x);
        float distanceZ = Mathf.Abs(p1.z - p2.z);
        float aspect = Mathf.Max(.5f, (float)Screen.width / Mathf.Max(1f, Screen.height));
        float requiredSize = Mathf.Max(6f, distanceZ * .5f + 2.2f,
            (distanceX * .5f + 2.2f) / aspect);
        // The approved overview is the farthest framing available. If the
        // players spread farther apart the camera still remains inside the
        // original salon bounds rather than exposing space outside the room.
        float cameraSize = Mathf.Min(requiredSize, 8.35f);
        float halfWidth = cameraSize * aspect;
        float midX = (p1.x + p2.x) * .5f;
        float midZ = (p1.z + p2.z) * .5f;
        float centerX = bounds.width <= halfWidth * 2f ? bounds.center.x :
            Mathf.Clamp(midX, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
        float centerZ = bounds.height <= cameraSize * 2f ? bounds.center.y :
            Mathf.Clamp(midZ, bounds.yMin + cameraSize, bounds.yMax - cameraSize);
        float zOffset = _overviewCameraPosition.z - 1.4f;
        _cameraPositionTarget = new Vector3(centerX, _overviewCameraPosition.y, centerZ + zOffset);
        _cameraSizeTarget = cameraSize;
    }

    private void EmitCoopEvidence()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!_coopMode || !Application.absoluteURL.Contains("multiplayerEvidence=1") ||
            _coopPlayerTwo == null || _coopPlayerTwo.Transform == null ||
            Time.unscaledTime < _coopEvidenceAt) return;
        _coopEvidenceAt = Time.unscaledTime + .3f;
        if (!_coopReadyEvidenceLogged)
        {
            _coopReadyEvidenceLogged = true;
            Debug.Log("[MULTIPLAYER_READY] local=true p1=1 p2=2 controls=split-touch-keyboard");
        }
        if (!_coopFlowEvidenceLogged && _mobileControls != null && _coopPlayerTwo.Controls != null)
        {
            _coopFlowEvidenceLogged = true;
            Debug.Log("[MULTIPLAYER_FLOW_PASS] independent-controls=true contexts=" +
                (_game == null ? 0 : _game.PlayerContexts.Count));
        }
        Debug.Log("[MULTIPLAYER_STATE] p1=" + (_player == null ? "null" : _player.position.ToString("F2")) +
            " p2=" + _coopPlayerTwo.Transform.position.ToString("F2") +
            " p2Working=" + (_coopPlayerTwo.WorkingView == null ? -1 : _coopPlayerTwo.WorkingView.Customer.Id));
#endif
    }
}
