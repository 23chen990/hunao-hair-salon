using System.Collections.Generic;
using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;
using UnityEngine.UI;

public sealed class OrderDemandBubbleView : MonoBehaviour
{
    private sealed class StepVisual
    {
        public Image Artwork;
        public ProceduralProgressBar Progress;
        public Image Completion;
        public Color TrackColor;
        public float LogicalProgress;
    }

    private readonly List<StepVisual> _steps = new List<StepVisual>();
    private CustomerModel _customer;
    private Image _frame;
    private GameObject _cleanupRoot;
    private Text _cleanupText;
    private GameObject _backgroundWaitRoot;
    private Text _backgroundWaitText;
    private ProceduralProgressBar _backgroundWaitFill;
    private float _backgroundWaitProgress;

    public int StepCount => _steps.Count;
    public int CustomerId => _customer == null ? -1 : _customer.Id;
    public int ActiveStepIndex => GetActiveRequirementIndex();
    public bool CleanupVisible => _cleanupRoot != null && _cleanupRoot.activeSelf;
    public string CleanupText => _cleanupText == null ? string.Empty : _cleanupText.text;
    public bool BackgroundWaitVisible => _backgroundWaitRoot != null && _backgroundWaitRoot.activeSelf;
    public string BackgroundWaitText => _backgroundWaitText == null ? string.Empty : _backgroundWaitText.text;
    public float BackgroundWaitProgress => _backgroundWaitProgress;

    public void Initialize(CustomerModel customer, Camera sceneCamera)
    {
        if (_customer != null || customer == null) return;
        _customer = customer;
        Build(sceneCamera);
        Refresh();
    }

    public void Refresh()
    {
        if (_customer == null) return;
        bool hasCurrent = !_customer.IsComplete && ActiveStepIndex >= 0;
        if (_frame != null) _frame.sprite = DemandBubbleArtwork.Frame(_steps.Count, hasCurrent);

        for (int i = 0; i < _steps.Count; i++)
        {
            bool complete = i < _customer.CompletedStepCount;
            bool active = hasCurrent && i == ActiveStepIndex;
            StepVisual step = _steps[i];
            step.Artwork.sprite = StepArtwork(i);
            step.Artwork.color = complete ? new Color(.46f, .46f, .46f, .52f) : Color.white;
            step.Completion.gameObject.SetActive(complete);
            step.TrackColor = SalonPalette.Track;
            if (!active)
            {
                step.LogicalProgress = 0f;
                step.Progress.Clear();
            }
        }

        bool cleanup = !_customer.ExitReady && _customer.ExitBlockReason != ExitBlockReason.None &&
            _customer.ExitBlockReason != ExitBlockReason.RequirementsIncomplete &&
            _customer.ExitBlockReason != ExitBlockReason.Moving;
        if (_cleanupRoot != null) _cleanupRoot.SetActive(cleanup);
        if (cleanup && _cleanupText != null) _cleanupText.text = CleanupLabel(_customer.ExitBlockReason);
    }

    public bool IsStepChecked(int index) =>
        index >= 0 && index < _steps.Count && _steps[index].Completion.gameObject.activeSelf;

    public float GetServiceProgress(int index) =>
        index >= 0 && index < _steps.Count ? _steps[index].LogicalProgress : 0f;

    public int GetVisibleProgressSegmentCount(int index) =>
        index >= 0 && index < _steps.Count
            ? Mathf.Clamp(Mathf.CeilToInt(_steps[index].LogicalProgress * 5f - .0001f), 0, 5)
            : 0;

    public Color GetProgressTint(int index) =>
        index >= 0 && index < _steps.Count ? _steps[index].Progress.FillColor : Color.clear;

    public Color GetTrackColor(int index) =>
        index >= 0 && index < _steps.Count ? _steps[index].TrackColor : Color.clear;

    public bool IsServiceProgressVisible(int index) =>
        index >= 0 && index < _steps.Count && _steps[index].Progress.IsVisible;

    public void ShowServiceProgressReady()
    {
        ClearServiceProgress();
    }

    public void SetServiceProgress(float progress, Color color)
    {
        SetActiveProgress(progress, color);
    }

    public bool SetServiceProgress(ServiceType actionService, float progress, Color color)
    {
        if (_customer == null || _customer.IsComplete ||
            _customer.CurrentNeed != actionService)
        {
            ClearServiceProgress();
            return false;
        }
        SetActiveProgress(progress, color);
        return true;
    }

    public void SetBackgroundWaitProgress(float progress, Color color)
    {
        SetActiveProgress(progress, color);
    }

    public void SetProcessingWaitProgress(string label, float progress, Color color)
    {
        _backgroundWaitProgress = Mathf.Clamp01(progress);
        if (_backgroundWaitRoot != null) _backgroundWaitRoot.SetActive(true);
        if (_backgroundWaitText != null) _backgroundWaitText.text = label;
        _backgroundWaitFill?.SetProgress(_backgroundWaitProgress, color);
    }

    public void ClearBackgroundWaitProgress()
    {
        _backgroundWaitProgress = 0f;
        if (_backgroundWaitRoot != null) _backgroundWaitRoot.SetActive(false);
        _backgroundWaitFill?.Clear();
    }

    public string GetServiceCountdownText(int index) => string.Empty;

    public void ClearServiceProgress()
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            _steps[i].LogicalProgress = 0f;
            _steps[i].Progress.Clear();
        }
    }

    private void SetActiveProgress(float progress, Color color)
    {
        int active = ActiveStepIndex;
        for (int i = 0; i < _steps.Count; i++)
        {
            float value = i == active ? Mathf.Clamp01(progress) : 0f;
            StepVisual step = _steps[i];
            step.LogicalProgress = value;
            if (value > .001f) step.Progress.SetProgress(value, color);
            else step.Progress.Clear();
        }
    }

    private int GetActiveRequirementIndex()
    {
        if (_customer == null || _customer.IsComplete || _customer.Needs == null) return -1;
        int complete = Mathf.Clamp(_customer.CompletedStepCount, 0, _customer.Needs.Count);
        return Mathf.Clamp(complete, 0, _steps.Count - 1);
    }

    private Sprite StepArtwork(int index)
    {
        if (_customer == null || index < 0 || index >= _customer.Needs.Count) return null;
        if (_customer.Needs[index] == ServiceType.Cut && index == ActiveStepIndex &&
            _customer.HaircutService != null &&
            _customer.HaircutService.State == HaircutServiceState.Active)
            return DemandBubbleArtwork.Tool(_customer.HaircutService.CurrentRequiredTool);
        return DemandBubbleArtwork.Service(_customer.Needs[index]);
    }

    private void Build(Camera sceneCamera)
    {
        int count = Mathf.Clamp(_customer.Needs.Count, 1, 3);
        var canvasObject = new GameObject("完整订单需求气泡", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        if (sceneCamera != null) canvasObject.transform.rotation = sceneCamera.transform.rotation;
        canvasObject.transform.localScale = Vector3.one * .0115f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 22;

        float width = count == 1 ? 132f : count == 2 ? 248f : 330f;
        Vector2 bubbleSize = new Vector2(width, count == 1 ? 148f : 142f);
        canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 190f);
        _frame = DemandBubbleArtwork.Image("Demand Bubble Frame", canvasObject.transform,
            new Vector2(0f, 18f), bubbleSize, DemandBubbleArtwork.Frame(count, true), false);

        float spacing = count == 1 ? 0f : count == 2 ? 108f : 94f;
        float startX = -(count - 1) * spacing * .5f;
        float iconSize = count == 3 ? 68f : 76f;
        for (int i = 0; i < count; i++)
        {
            float x = startX + i * spacing;
            Image artwork = DemandBubbleArtwork.Image("Tool Artwork " + (i + 1),
                canvasObject.transform, new Vector2(x, 27f), new Vector2(iconSize, iconSize),
                DemandBubbleArtwork.Service(_customer.Needs[i]));
            ProceduralProgressBar progress = ProceduralProgressBar.Create(canvasObject.transform,
                "Action Progress Track " + (i + 1), "Action Progress Fill " + (i + 1),
                new Vector2(x, -27f), new Vector2(count == 3 ? 66f : 78f, 12f),
                new Color(.08f, .1f, .12f, .82f));
            Image completion = DemandBubbleArtwork.Image("Completion Badge " + (i + 1),
                canvasObject.transform, new Vector2(x + 31f, -2f), new Vector2(36f, 36f),
                DemandBubbleArtwork.CompletionBadge);
            completion.gameObject.SetActive(false);
            _steps.Add(new StepVisual
            {
                Artwork = artwork,
                Progress = progress,
                Completion = completion,
                TrackColor = SalonPalette.Track
            });

            if (i < count - 1)
                DemandBubbleArtwork.Plus("Plus " + (i + 1), canvasObject.transform,
                    new Vector2(x + spacing * .5f, 27f), count == 3 ? 25 : 28);
        }

        _cleanupText = SalonUiFactory.CreateLabel(string.Empty, canvasObject.transform,
            SalonPalette.Ink, new Color(1f, .91f, .7f, .96f));
        _cleanupRoot = SalonUiFactory.DirectChildRoot(_cleanupText.transform, canvasObject.transform).gameObject;
        var cleanupRect = _cleanupRoot.GetComponent<RectTransform>();
        cleanupRect.anchoredPosition = new Vector2(0f, -68f);
        cleanupRect.sizeDelta = new Vector2(230f, 40f);
        _cleanupText.font = SalonUiFactory.GetPackagedUiFont();
        _cleanupText.fontSize = 18;
        _cleanupText.alignment = TextAnchor.MiddleCenter;
        _cleanupRoot.SetActive(false);

        _backgroundWaitText = SalonUiFactory.CreateLabel(string.Empty, canvasObject.transform,
            Color.white, new Color(.12f, .14f, .18f, .88f));
        _backgroundWaitRoot = SalonUiFactory.DirectChildRoot(
            _backgroundWaitText.transform, canvasObject.transform).gameObject;
        RectTransform waitRect = _backgroundWaitRoot.GetComponent<RectTransform>();
        waitRect.anchoredPosition = new Vector2(0f, -70f);
        waitRect.sizeDelta = new Vector2(154f, 34f);
        _backgroundWaitText.font = SalonUiFactory.GetPackagedUiFont();
        _backgroundWaitText.fontSize = 16;
        _backgroundWaitText.alignment = TextAnchor.MiddleCenter;
        _backgroundWaitFill = ProceduralProgressBar.Create(_backgroundWaitRoot.transform,
            "Background Wait Track", "Background Wait Fill", new Vector2(0f, -14f),
            new Vector2(132f, 8f), new Color(.04f, .06f, .08f, .8f));
        _backgroundWaitRoot.SetActive(false);
        SalonUiFactory.MakeClickThrough(canvasObject);
    }

    private static string CleanupLabel(ExitBlockReason reason)
    {
        if (reason == ExitBlockReason.FoamRemaining) return "🫧 泡沫残留";
        if (reason == ExitBlockReason.ShampooResidue) return "🧴 洗发残留";
        if (reason == ExitBlockReason.TowelWrapped) return "🧻 毛巾未拆";
        if (reason == ExitBlockReason.WetHair) return "💧 湿发未处理";
        if (reason == ExitBlockReason.Moving) return "🚶 仍在移动";
        if (reason == ExitBlockReason.ActiveAction) return "🌀 操作进行中";
        return "✓ CLEANUP · 订单完成 · 仍需收尾";
    }
}
