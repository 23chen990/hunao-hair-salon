using HairSalon;
using UnityEngine;
using UnityEngine.UI;

public sealed class HaircutDemandBubbleView : MonoBehaviour
{
    private sealed class StepVisual
    {
        public Image Artwork;
        public ProceduralProgressBar Progress;
        public Image Completion;
        public float LogicalProgress;
    }

    private readonly System.Collections.Generic.List<StepVisual> _steps =
        new System.Collections.Generic.List<StepVisual>();
    private HaircutServiceModel _service;
    private Image _frame;
    private bool _highlightVisible;

    public int StepCount => _steps.Count;
    public int ActiveStepIndex => _service == null ? -1 :
        Mathf.Min(_service.CurrentStepIndex, _steps.Count - 1);

    public void Initialize(HaircutServiceModel service, Camera sceneCamera)
    {
        if (_service != null || service == null) return;
        _service = service;
        Build(sceneCamera);
        Refresh();
    }

    public void Refresh()
    {
        if (_service == null) return;
        _highlightVisible = _service.State == HaircutServiceState.Active;
        if (_frame != null)
            _frame.sprite = DemandBubbleArtwork.Frame(_steps.Count, _highlightVisible);

        for (int i = 0; i < _steps.Count; i++)
        {
            bool complete = _service.IsStepComplete(i) ||
                            _service.State == HaircutServiceState.Completed;
            bool active = _service.State == HaircutServiceState.Active && i == _service.CurrentStepIndex;
            StepVisual step = _steps[i];
            step.Artwork.sprite = DemandBubbleArtwork.Tool(_service.RequiredTools[i]);
            step.Artwork.color = complete ? new Color(.46f, .46f, .46f, .52f) : Color.white;
            step.Completion.gameObject.SetActive(complete);
            if (!active)
            {
                step.LogicalProgress = 0f;
                step.Progress.Clear();
            }
            else if (step.LogicalProgress <= .001f) step.Progress.Clear();
        }
    }

    public void SetHoldProgress(float progress)
    {
        if (_service == null || _service.State != HaircutServiceState.Active) return;
        for (int i = 0; i < _steps.Count; i++)
        {
            float value = i == _service.CurrentStepIndex ? Mathf.Clamp01(progress) : 0f;
            _steps[i].LogicalProgress = value;
            if (value > .001f) _steps[i].Progress.SetProgress(value, Color.white);
            else _steps[i].Progress.Clear();
        }
    }

    public void SetHoldColor(Color color)
    {
        if (ActiveStepIndex >= 0 && _steps[ActiveStepIndex].LogicalProgress > .001f)
            _steps[ActiveStepIndex].Progress.SetProgress(_steps[ActiveStepIndex].LogicalProgress, color);
    }

    public void ClearHoldProgress()
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            _steps[i].LogicalProgress = 0f;
            _steps[i].Progress.Clear();
        }
    }

    public void SetPatience(float progress) { }

    public float GetStepProgress(int index) =>
        index >= 0 && index < _steps.Count ? _steps[index].LogicalProgress : 0f;

    public int GetProgressSegmentCount(int index) => 0;

    public int GetVisibleProgressSegments(int index)
    {
        return 0;
    }

    public bool IsCurrentHighlightVisible(int index) =>
        index >= 0 && index < _steps.Count && _highlightVisible && index == ActiveStepIndex;

    public bool IsCompletionBadgeVisible(int index) =>
        index >= 0 && index < _steps.Count && _steps[index].Completion.gameObject.activeSelf;

    private void Build(Camera sceneCamera)
    {
        int count = _service.RequiredTools.Count;
        var canvasObject = new GameObject("剪发需求气泡 V1", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        if (sceneCamera != null) canvasObject.transform.rotation = sceneCamera.transform.rotation;
        canvasObject.transform.localScale = Vector3.one * .0115f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 22;

        Vector2 bubbleSize = count == 1 ? new Vector2(132f, 148f) : new Vector2(248f, 142f);
        canvasObject.GetComponent<RectTransform>().sizeDelta = bubbleSize;
        _frame = DemandBubbleArtwork.Image("Demand Bubble Frame", canvasObject.transform,
            Vector2.zero, bubbleSize, DemandBubbleArtwork.Frame(count, true), false);

        float spacing = count == 1 ? 0f : 108f;
        float startX = -(count - 1) * spacing * .5f;
        float iconSize = count == 1 ? 82f : 76f;
        for (int i = 0; i < count; i++)
        {
            float x = startX + i * spacing;
            Image artwork = DemandBubbleArtwork.Image("Tool Artwork " + (i + 1),
                canvasObject.transform, new Vector2(x, 9f), new Vector2(iconSize, iconSize),
                DemandBubbleArtwork.Tool(_service.RequiredTools[i]));
            ProceduralProgressBar progress = ProceduralProgressBar.Create(canvasObject.transform,
                "Haircut Action Progress Track " + (i + 1),
                "Haircut Action Progress Fill " + (i + 1), new Vector2(x, -29f),
                new Vector2(count == 1 ? 82f : 76f, 12f), new Color(.08f, .1f, .12f, .82f));
            Image completion = DemandBubbleArtwork.Image("Completion Badge " + (i + 1),
                canvasObject.transform, new Vector2(x + 33f, -22f), new Vector2(38f, 38f),
                DemandBubbleArtwork.CompletionBadge);
            completion.gameObject.SetActive(false);

            _steps.Add(new StepVisual { Artwork = artwork, Progress = progress, Completion = completion });

            if (i < count - 1)
                DemandBubbleArtwork.Plus("Plus " + (i + 1), canvasObject.transform,
                    new Vector2(x + spacing * .5f, 9f), 28);
        }
        SalonUiFactory.MakeClickThrough(canvasObject);
    }

    public static string ToolSymbol(SalonTool tool)
    {
        if (tool == SalonTool.Scissors) return "✂";
        if (tool == SalonTool.ThinningShears) return "≋";
        if (tool == SalonTool.BlowDryer) return "♨";
        if (tool == SalonTool.Shampoo) return "≈";
        return "▣";
    }
}
