using System.Collections.Generic;
using HairSalon;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class SalonDemo
{
    private bool _startupSelectionRequired;
    private bool _startupSelectionComplete;
    private SalonPlayMode _startupPlayMode = SalonPlayMode.SinglePlayer;
    private int _mobileSaveSlotId = 1;
    private int _startupSelectedSlotId = 1;
    private Transform _startupSafeArea;
    private GameObject _startupPanel;
    private GameObject _startupModePanel;
    private GameObject _startupSavePanel;
    private Text _startupTitleLabel;
    private Text _startupHintLabel;
    private Text _startupSaveHintLabel;
    private Button _startupSingleButton;
    private Button _startupTwoButton;
    private Button _startupBackButton;
    private Text[] _startupSlotLabels = new Text[SalonSaveSlots.Count];
    private Button[] _startupSlotButtons = new Button[SalonSaveSlots.Count];
    private Button _startupConfirmButton;

    private bool IsStartupSelectionBlocking => _startupSelectionRequired && !_startupSelectionComplete;

    private void BuildStartupSelectionPanel(Transform parent)
    {
        if (!_mobileMode || parent == null) return;
        _startupSafeArea = parent;
        _startupPanel = UiPanel("Startup Selection Overlay", parent, new Color(.10f, .08f, .07f, .82f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Transform modeSurface = BuildModalSurface(_startupPanel.transform, "Startup Mode Card", "Startup Mode Surface",
            new Vector2(760f, 520f), Hex("FFF0D7"));
        _startupModePanel = modeSurface.parent.gameObject;
        _startupTitleLabel = UiLabel("选择模式", modeSurface, 48, DarkWood, TextAnchor.MiddleCenter,
            new Vector2(0f, 145f), new Vector2(640f, 78f));
        _startupHintLabel = UiLabel("同屏合作 · 两人同店", modeSurface, 24, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 82f), new Vector2(620f, 48f));
        _startupSingleButton = UiButton("单人\nP1 操作", modeSurface, new Vector2(.5f, .5f), new Vector2(-185f, -65f),
            new Vector2(280f, 132f), Teal, () => SelectStartupMode(SalonPlayMode.SinglePlayer));
        _startupTwoButton = UiButton("两人\nP1 + P2", modeSurface, new Vector2(.5f, .5f), new Vector2(185f, -65f),
            new Vector2(280f, 132f), Purple, () => SelectStartupMode(SalonPlayMode.TwoPlayer));
        UiLabel("模式可选，进度相同", modeSurface, 20, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, -175f), new Vector2(620f, 40f));

        Transform saveSurface = BuildModalSurface(_startupPanel.transform, "Startup Save Card", "Startup Save Surface",
            new Vector2(980f, 610f), Hex("FFF0D7"));
        _startupSavePanel = saveSurface.parent.gameObject;
        UiLabel("选择进度", saveSurface, 48, DarkWood, TextAnchor.MiddleCenter,
            new Vector2(0f, 218f), new Vector2(760f, 74f));
        _startupSaveHintLabel = UiLabel("选择进度后开始营业", saveSurface, 24, Ink,
            TextAnchor.MiddleCenter, new Vector2(0f, 160f), new Vector2(820f, 42f));
        for (int i = 0; i < SalonSaveSlots.Count; i++)
        {
            int slotId = i + 1;
            Button button = UiButton("", saveSurface, new Vector2(.5f, .5f),
                new Vector2(-300f + i * 300f, 15f), new Vector2(270f, 180f),
                i == 0 ? Teal : (i == 1 ? Purple : Coral), () => SelectStartupSaveSlot(slotId));
            _startupSlotButtons[i] = button;
            _startupSlotLabels[i] = button.GetComponentInChildren<Text>();
        }
        _startupBackButton = UiButton("上一步", saveSurface, new Vector2(.5f, .5f), new Vector2(-190f, -205f),
            new Vector2(250f, 64f), DarkWood, ShowStartupModeSelection);
        _startupConfirmButton = UiButton("开始营业", saveSurface, new Vector2(.5f, .5f),
            new Vector2(185f, -205f), new Vector2(300f, 64f), Teal, ConfirmStartupSelection);

        _startupModePanel.SetActive(false);
        _startupSavePanel.SetActive(false);
        _startupPanel.SetActive(false);
    }

    private void ShowStartupModeSelection()
    {
        if (_startupPanel == null) return;
        _startupPanel.SetActive(true);
        _startupModePanel.SetActive(true);
        _startupSavePanel.SetActive(false);
        if (_startupTitleLabel != null) _startupTitleLabel.text = "选择模式";
        if (_startupHintLabel != null) _startupHintLabel.text = "同屏合作 · 两人同店";
        _mobileControls?.SetVisible(false);
        _coopPlayerTwo?.Controls?.SetVisible(false);
        if (_coopPlayerTwo?.Transform != null) _coopPlayerTwo.Transform.gameObject.SetActive(false);
        if (EventSystem.current != null && _startupSingleButton != null)
            EventSystem.current.SetSelectedGameObject(_startupSingleButton.gameObject);
    }

    private void SelectStartupMode(SalonPlayMode mode)
    {
        if (!IsStartupSelectionBlocking) return;
        _startupPlayMode = mode;
        SetRuntimeCoopMode(mode == SalonPlayMode.TwoPlayer);
        if (_startupPanel != null) _startupPanel.SetActive(true);
        if (_startupModePanel != null) _startupModePanel.SetActive(false);
        if (_startupSavePanel != null) _startupSavePanel.SetActive(true);
        RefreshStartupSlotCards();
        if (EventSystem.current != null && _startupSlotButtons.Length > 0 && _startupSlotButtons[0] != null)
            EventSystem.current.SetSelectedGameObject(_startupSlotButtons[0].gameObject);
    }

    private void SetStartupButtonsVisible(bool visible)
    {
        if (_startupPanel == null) return;
        _startupPanel.SetActive(visible);
        if (!visible)
        {
            _startupModePanel?.SetActive(false);
            _startupSavePanel?.SetActive(false);
        }
    }

    private void RefreshStartupSlotCards()
    {
        if (_startupSavePanel == null) return;
        List<SalonSaveSlotSummary> summaries = SalonSaveSlots.ReadSummaries(new PlayerPrefsSalonProgressStorage());
        _startupSelectedSlotId = SalonSaveSlots.NormalizeSlot(_mobileSaveSlotId);
        for (int i = 0; i < summaries.Count && i < _startupSlotLabels.Length; i++)
        {
            SalonSaveSlotSummary summary = summaries[i];
            if (_startupSlotLabels[i] != null)
                _startupSlotLabels[i].text = "进度 " + summary.SlotId + "\n" + summary.DisplayStatus;
            if (_startupSlotButtons[i] != null)
            {
                _startupSlotButtons[i].interactable = true;
                Image image = _startupSlotButtons[i].GetComponent<Image>();
                if (image != null)
                    image.color = summary.SlotId == _startupSelectedSlotId
                        ? Color.Lerp(Teal, Color.white, .12f) : (i == 0 ? Teal : (i == 1 ? Purple : Coral));
            }
        }
        if (_startupConfirmButton != null) _startupConfirmButton.interactable = true;
        if (_startupSaveHintLabel != null)
            _startupSaveHintLabel.text = _startupPlayMode == SalonPlayMode.TwoPlayer
                ? "两人营业和单人使用同一经营进度" : "单人营业和两人使用同一经营进度";
    }

    private void SelectStartupSaveSlot(int slotId)
    {
        if (!IsStartupSelectionBlocking) return;
        _startupSelectedSlotId = SalonSaveSlots.NormalizeSlot(slotId);
        for (int i = 0; i < _startupSlotButtons.Length; i++)
        {
            if (_startupSlotButtons[i] == null) continue;
            Image image = _startupSlotButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = (i + 1) == _startupSelectedSlotId
                    ? Color.Lerp(Teal, Color.white, .12f) : (i == 0 ? Teal : (i == 1 ? Purple : Coral));
        }
        if (_startupConfirmButton != null) _startupConfirmButton.interactable = true;
    }

    private void ConfirmStartupSelection()
    {
        if (!IsStartupSelectionBlocking || _dayController == null) return;
        if (_dayController.State == DayState.PreOpen && _startupSelectedSlotId != _mobileSaveSlotId)
            SwitchMobileSaveSlot(_startupSelectedSlotId);
        _mobileSaveSlotId = SalonSaveSlots.NormalizeSlot(_startupSelectedSlotId);
        PlayerPrefs.SetInt(SalonSaveSlots.SelectedSlotKey, _mobileSaveSlotId);
        PlayerPrefs.Save();
        _startupSelectionComplete = true;
        SetStartupButtonsVisible(false);
        HandleDayStateChanged(_dayController.State);
    }

    private void SetRuntimeCoopMode(bool enabled)
    {
        if (!_mobileMode) return;
        if (!enabled)
        {
            _coopMode = false;
            if (_mobileControls != null)
            {
                RectTransform root = _mobileControls.transform as RectTransform;
                if (root != null)
                {
                    root.anchorMin = Vector2.zero;
                    root.anchorMax = Vector2.one;
                    root.offsetMin = Vector2.zero;
                    root.offsetMax = Vector2.zero;
                }
                _mobileControls.SetVisible(false);
            }
            _coopPlayerTwo?.Controls?.SetVisible(false);
            if (_coopPlayerTwo?.Transform != null) _coopPlayerTwo.Transform.gameObject.SetActive(false);
            return;
        }

        _coopMode = true;
        if (_coopPlayerTwo == null || _coopPlayerTwo.Transform == null)
        {
            Transform salon = GameObject.Find("Fixed Salon Map")?.transform;
            BuildCoopPlayerTwoAvatar(salon);
        }
        if (_coopPlayerTwo != null && _coopPlayerTwo.Controls == null)
            BuildCoopControls(_startupSafeArea);
        else
        {
            _mobileControls?.ConfigureForPlayer(1, true);
            _coopPlayerTwo?.Controls?.ConfigureForPlayer(2, true);
        }
        if (_coopPlayerTwo?.Transform != null) _coopPlayerTwo.Transform.gameObject.SetActive(true);
        _mobileControls?.SetVisible(false);
        _coopPlayerTwo?.Controls?.SetVisible(false);
    }

    private void SwitchMobileSaveSlot(int slotId)
    {
        if (!_mobileMode || _dayController == null || _dayController.State != DayState.PreOpen) return;
        int normalized = SalonSaveSlots.NormalizeSlot(slotId);
        if (normalized == _mobileSaveSlotId) return;

        ClearCustomerViews();
        EndMobileWork();
        _mobileGuidedCustomer = null;
        _mobileResumeGuidedCustomer = null;
        _game.ResetForNextDay();
        _mobileSaveSlotId = normalized;
        _mobileSaves = new PlayerPrefsSalonProgressRepository(_mobileSaveSlotId);
        _mobileProgress = _mobileSaves.Load() ?? SalonProgressData.CreateDefault();
        SalonMobileDayConfig.ApplyForDay(DaySettings, _mobileProgress.DayNumber);
        FlowSettings.WaitingCapacity = SalonMobileDayConfig.WaitingCapacity;
        FlowSettings.MaxCustomers = SalonMobileDayConfig.MaxConcurrentCustomers;
        ServiceSettings = SalonMobileDayConfig.CreateServiceConfig();
        PatienceSettings.DrainPerSecond = SalonMobileDayConfig.PatienceDrainPerSecond(_mobileProgress.DayNumber);
        _mobileSupplyPad = new SalonProximityPurchasePadModel("expansion-pad-wash-rack", MobileSupplyPadCost);
        _mobileHaircutExpansionPad = new SalonProximityPurchasePadModel(
            "expansion-pad-haircut-2", SalonProgressData.HaircutExpansionCost);
        RestoreMobileSupplyPadProgress(_mobileProgress);
        RestoreMobileHaircutExpansionProgress(_mobileProgress);
        CreateMobileWashAnnexPad(_mobileProgress);
        CreateMobileUnlockPads(_mobileProgress);
        _game.ConfigureWorkstationAvailability(_mobileProgress.HaircutExpansionPurchased, IsMobileWashAnnexUnlocked, false);
        _game.RestorePersistentState(_mobileProgress.Balance, _mobileProgress.AutoBlowPurchased,
            _mobileProgress.FirstDayComplete);
        _dayController.RestoreReputation(_mobileProgress.ReputationStars);
        _satisfaction.BeginDay(_mobileProgress.ShopSatisfaction);
        RestoreMobileGame();
        ApplyMobileWorkstationPresentation();
        if (_mobileExpansionBarrier != null) _mobileExpansionBarrier.SetActive(!_mobileHaircutExpansionPad.IsUnlocked);
        ApplyMobileWashAnnexPresentation();
        if (_mobileSupplyPad.IsUnlocked) BuildExpandedWashRackVisual();
        else if (_mobileExpandedRackBuilt) RemoveExpandedWashRackVisual();
        UpdateMobilePurchasePadVisual();
        UpdateMobileHaircutExpansionPadVisual();
        _coinBalanceLabel.text = _game.Balance.ToString("N0");
        RefreshMobileDayPresentation();
    }
}
