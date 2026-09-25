using HairSalon;
using HairSalon.ServiceArchitecture;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A compact, always-readable customer state strip. The order bubble keeps the approved
/// service artwork; this separate strip carries patience and the one action the player should
/// understand next.
/// </summary>
public sealed class CustomerUrgencyView : MonoBehaviour
{
    private CustomerModel _customer;
    private Camera _sceneCamera;
    private GameObject _statusRoot;
    private Text _statusLabel;
    private Text _patienceLabel;
    private Image _patienceTrack;
    private Image _patienceFill;

    public CustomerModel Customer => _customer;
    public bool IsVisible => _statusRoot != null && _statusRoot.activeSelf;
    public string StatusText => _statusLabel == null ? string.Empty : _statusLabel.text;
    public string PatienceText => _patienceLabel == null ? string.Empty : _patienceLabel.text;
    public float PatienceProgress => _customer == null ? 0f : _customer.PatienceProgress;

    public void Initialize(CustomerModel customer, Camera sceneCamera)
    {
        if (_customer != null || customer == null) return;
        _customer = customer;
        _sceneCamera = sceneCamera;
        Build();
        Refresh();
    }

    public void SetSceneCamera(Camera sceneCamera)
    {
        _sceneCamera = sceneCamera;
        FaceCamera();
    }

    public void Refresh()
    {
        if (_customer == null || _statusRoot == null) return;

        bool stillInSalon = _customer.State != CustomerState.Leaving &&
            _customer.State != CustomerState.Exited;
        _statusRoot.SetActive(stillInSalon);
        if (!stillInSalon) return;

        float patience = _customer.PatienceProgress;
        _statusLabel.text = StatusLabel(_customer);
        // The bar carries the patience meaning. Keeping only the percentage here
        // leaves room for the state label at the target mobile viewport.
        _patienceLabel.text = Mathf.RoundToInt(patience * 100f) + "%";
        _patienceTrack.color = PatienceTrackColor(patience);
        _patienceFill.color = PatienceFillColor(patience);
        _patienceFill.rectTransform.localScale = new Vector3(patience, 1f, 1f);
        FaceCamera();
    }

    private void LateUpdate()
    {
        // Customer state changes are model-driven and may happen while the player is serving
        // another seat. Keeping this view self-refreshing makes the resident strip independent
        // from selection-only toolbar refreshes.
        Refresh();
    }

    private void Build()
    {
        var canvasObject = new GameObject("Customer Urgency Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 2.22f, 0f);
        // The previous 15px label at .009 world scale rendered at roughly 3px
        // on the 844x390 review build. Increase the glyph budget while keeping
        // the panel narrow enough for adjacent wash stations.
        canvasObject.transform.localScale = Vector3.one * .014f;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 23;
        canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 72f);

        _statusRoot = new GameObject("Customer Urgency Status", typeof(RectTransform), typeof(Image));
        _statusRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = _statusRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
        panelRect.pivot = new Vector2(.5f, .5f);
        panelRect.sizeDelta = new Vector2(150f, 64f);
        Image panel = _statusRoot.GetComponent<Image>();
        panel.sprite = SalonUiFactory.GetRoundedPanelSprite();
        panel.type = Image.Type.Sliced;
        panel.color = new Color(.96f, .88f, .71f, .96f);
        panel.raycastTarget = false;

        _statusLabel = Label("Status Label", _statusRoot.transform, SalonPalette.Ink);
        RectTransform statusRect = _statusLabel.rectTransform;
        statusRect.anchorMin = new Vector2(0f, .5f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.offsetMin = new Vector2(7f, 0f);
        statusRect.offsetMax = new Vector2(-7f, -2f);
        _statusLabel.fontSize = 30;
        _statusLabel.fontStyle = FontStyle.Bold;
        _statusLabel.alignment = TextAnchor.MiddleCenter;
        _statusLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _statusLabel.verticalOverflow = VerticalWrapMode.Overflow;

        _patienceLabel = Label("Patience Label", _statusRoot.transform, SalonPalette.Ink);
        RectTransform patienceRect = _patienceLabel.rectTransform;
        patienceRect.anchorMin = patienceRect.anchorMax = new Vector2(.5f, .5f);
        patienceRect.pivot = new Vector2(.5f, .5f);
        patienceRect.anchoredPosition = new Vector2(56f, -21f);
        patienceRect.sizeDelta = new Vector2(36f, 26f);
        _patienceLabel.fontSize = 24;
        _patienceLabel.fontStyle = FontStyle.Bold;
        _patienceLabel.alignment = TextAnchor.MiddleCenter;
        _patienceLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        _patienceLabel.verticalOverflow = VerticalWrapMode.Overflow;

        _patienceTrack = Bar("Patience Track", _statusRoot.transform,
            new Vector2(-10f, -21f), new Vector2(98f, 8f), new Color(.18f, .2f, .22f, .52f));
        _patienceFill = Bar("Patience Fill", _patienceTrack.transform,
            new Vector2(0f, 0f), new Vector2(94f, 4f), SalonPalette.Success);
        RectTransform fillRect = _patienceFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, .5f);
        fillRect.anchorMax = new Vector2(0f, .5f);
        fillRect.pivot = new Vector2(0f, .5f);
        fillRect.anchoredPosition = new Vector2(2f, 0f);
        fillRect.localScale = new Vector3(0f, 1f, 1f);

        SalonUiFactory.MakeClickThrough(canvasObject);
    }

    private void FaceCamera()
    {
        if (_sceneCamera == null) return;
        Transform canvas = _statusRoot == null ? null : _statusRoot.transform.parent;
        if (canvas != null) canvas.rotation = _sceneCamera.transform.rotation;
    }

    private static Text Label(string name, Transform parent, Color color)
    {
        var objectRoot = new GameObject(name, typeof(RectTransform), typeof(Text));
        objectRoot.transform.SetParent(parent, false);
        Text label = objectRoot.GetComponent<Text>();
        label.font = SalonUiFactory.GetPackagedUiFont();
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var objectRoot = new GameObject(name, typeof(RectTransform), typeof(Image));
        objectRoot.transform.SetParent(parent, false);
        RectTransform rect = objectRoot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = objectRoot.GetComponent<Image>();
        image.sprite = SalonUiFactory.GetRoundedPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static string StatusLabel(CustomerModel customer)
    {
        if (customer.AutoBlowSafetyStopped || customer.BlowStage == BlowStage.SafetyStopped)
            return "超时收尾";
        if (customer.State != CustomerState.Finished && customer.Patience <= .01f)
            return "超时";
        if (customer.IsComplete && (customer.ExitReady || (customer.State == CustomerState.Finished &&
            customer.ExitBlockReason == ExitBlockReason.None)))
            return "可离开";
        if (IsForegroundServiceActive(customer))
            return "服务中";
        if (customer.AutoBlowRunning || customer.BlowStage == BlowStage.AutoRunning)
            return IsAtBackgroundIdealStart(customer) ? "待收尾" : "后台运行";
        if (customer.IsProcessing)
            return customer.IsDyeCleanupReady ? "待收尾" : "后台运行";
        if (IsBackgroundRunning(customer))
            return IsAtBackgroundIdealStart(customer) ? "待收尾" : "后台运行";
        if (customer.IsDyeCleanupReady || (customer.IsComplete && IsCleanupBlocked(customer.ExitBlockReason)))
            return "待收尾";
        if (customer.State == CustomerState.Serving)
            return customer.CurrentNeed == ServiceType.Wash ? "待洗发" :
                customer.CurrentNeed == ServiceType.Cut ? "待剪发" : "待吹发";
        if (customer.State == CustomerState.MovingToStation)
            return "前往工位";
        if (customer.State == CustomerState.Entering)
            return "进店中";
        if (customer.State == CustomerState.Finished)
            return "已完成";
        return "等待中";
    }

    private static bool IsBackgroundRunning(CustomerModel customer)
    {
        return customer.BackgroundTask != null &&
            customer.BackgroundTask.State == BackgroundTaskState.Running;
    }

    private static bool IsAtBackgroundIdealStart(CustomerModel customer)
    {
        if (customer == null || customer.BackgroundTask == null ||
            customer.BackgroundTask.State != BackgroundTaskState.Running)
            return false;
        return customer.BackgroundTask.Elapsed >= Mathf.Max(0f, customer.BackgroundTask.IdealStart);
    }

    private static bool IsForegroundServiceActive(CustomerModel customer)
    {
        if (customer == null) return false;
        if (customer.ActiveServiceAction != ActiveServiceAction.None || customer.ManualBlowHolding)
            return true;
        if (customer.ServiceExecution != null &&
            customer.ServiceExecution.State == ServiceExecutionState.Executing)
            return true;
        return customer.ServicePhase == ServiceStepPhase.Active;
    }

    private static bool IsCleanupBlocked(ExitBlockReason reason)
    {
        return reason == ExitBlockReason.FoamRemaining ||
            reason == ExitBlockReason.ShampooResidue ||
            reason == ExitBlockReason.TowelWrapped ||
            reason == ExitBlockReason.WetHair;
    }

    private static Color PatienceTrackColor(float progress)
    {
        return progress <= .25f ? new Color(.35f, .12f, .1f, .68f) :
            progress <= .55f ? new Color(.43f, .31f, .1f, .64f) :
            new Color(.18f, .2f, .22f, .52f);
    }

    private static Color PatienceFillColor(float progress)
    {
        return progress <= .25f ? SalonPalette.Danger :
            progress <= .55f ? SalonPalette.Warning : SalonPalette.Success;
    }
}
