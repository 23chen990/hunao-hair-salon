using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The small, named-action input surface used by the mobile salon loop.
///
/// The game reads actions from this component instead of reading pointer or
/// keyboard state directly. Touch handlers are private implementation details,
/// which keeps the integration point usable by both the mobile build and the
/// desktop test fallback.
/// </summary>
public sealed class SalonMobileControls : MonoBehaviour
{
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    private static readonly Color WarmBrown = new Color32(84, 51, 34, 244);
    private static readonly Color DarkBrown = new Color32(54, 31, 22, 250);
    private static readonly Color Cream = new Color32(242, 215, 182, 255);
    private static readonly Color Teal = new Color32(63, 143, 136, 248);
    private static readonly Color TealDark = new Color32(37, 99, 95, 255);

    private readonly Dictionary<int, Vector2> _joystickOrigins = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, Vector2> _joystickMoves = new Dictionary<int, Vector2>();
    private readonly HashSet<int> _interactionPointerIds = new HashSet<int>();

    private RectTransform _joystickRect;
    private RectTransform _joystickKnobRect;
    private RectTransform _interactionRect;
    private JoystickPointerSurface _joystickSurface;
    private InteractionPointerSurface _interactionSurface;
    private Button _interactionButton;
    private Text _interactionLabel;
    private Text _hintLabel;
    private CanvasGroup _canvasGroup;
    private int _activeJoystickPointerId = int.MinValue;
    private Vector2 _move;
    private bool _interactionPressed;
    private bool _keyboardInteractionHeld;
    private bool _interactionAvailable = true;

    /// <summary>Normalized movement action, including the radial deadzone.</summary>
    public Vector2 Move => _move;

    /// <summary>Whether the operation action is currently held.</summary>
    public bool InteractionHeld => _interactionPointerIds.Count > 0 || _keyboardInteractionHeld;

    /// <summary>
    /// Consumes the one-frame edge of the operation action.
    /// </summary>
    public bool ConsumeInteractionPressed()
    {
        if (!_interactionPressed)
            return false;
        _interactionPressed = false;
        return true;
    }

    /// <summary>
    /// Builds the controls below an existing safe-area Canvas or RectTransform.
    /// No artwork asset is required; all surfaces use the salon's procedural UI
    /// sprites and the existing packaged UI font.
    /// </summary>
    public static SalonMobileControls Create(Transform safeParent)
    {
        Transform parent = safeParent;
        if (parent == null)
        {
            GameObject canvasObject = new GameObject(
                "Salon Mobile Controls Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            ConfigureScaler(scaler);
            parent = canvasObject.transform;
        }

        GameObject rootObject = new GameObject(
            "Salon Mobile Controls",
            typeof(RectTransform),
            typeof(CanvasGroup));
        rootObject.transform.SetParent(parent, false);

        Canvas parentCanvas = rootObject.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = rootObject.AddComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            parentCanvas.sortingOrder = 60;
            ConfigureScaler(rootObject.AddComponent<CanvasScaler>());
            rootObject.AddComponent<GraphicRaycaster>();
        }
        else if (rootObject.GetComponentInParent<CanvasScaler>() == null)
        {
            ConfigureScaler(rootObject.AddComponent<CanvasScaler>());
        }

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        SalonMobileControls controls = rootObject.AddComponent<SalonMobileControls>();
        controls._canvasGroup = rootObject.GetComponent<CanvasGroup>();
        controls.BuildUi();
        return controls;
    }

    /// <summary>
    /// Clears all held and pending actions. Call this when the game pauses,
    /// opens a modal, or otherwise stops accepting gameplay input.
    /// </summary>
    public void ResetInput()
    {
        _joystickOrigins.Clear();
        _joystickMoves.Clear();
        _interactionPointerIds.Clear();
        _activeJoystickPointerId = int.MinValue;
        _move = Vector2.zero;
        SetJoystickKnobPosition(Vector2.zero);
        _interactionPressed = false;
        _keyboardInteractionHeld = false;
    }

    /// <summary>
    /// Shows or hides the complete touch surface. Hiding also releases any
    /// pointer captured before a menu or pause overlay opened.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!visible)
            ResetInput();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    /// <summary>
    /// Updates the operation label and whether the operation can be pressed.
    /// </summary>
    public void SetInteraction(string label, bool available)
    {
        _interactionAvailable = available;
        if (_interactionButton != null)
            _interactionButton.interactable = available;
        if (_interactionLabel != null)
            _interactionLabel.text = string.IsNullOrEmpty(label) ? "操作" : label;

        if (!available)
        {
            _interactionPointerIds.Clear();
            _interactionPressed = false;
        }
    }

    /// <summary>Sets the short contextual hint below the central toolbar.</summary>
    public void SetHint(string hint)
    {
        if (_hintLabel == null)
            return;
        _hintLabel.text = hint ?? string.Empty;
        _hintLabel.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(hint));
    }

    private static void ConfigureScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
    }

    private void BuildUi()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        CreateJoystick(root);
        CreateInteractionButton(root);
        CreateHint(root);
        SetInteraction("操作", true);
        SetHint(string.Empty);
    }

    private void CreateJoystick(RectTransform root)
    {
        GameObject joystickObject = new GameObject(
            "MobileJoystick",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        joystickObject.transform.SetParent(root, false);
        _joystickRect = joystickObject.GetComponent<RectTransform>();
        _joystickRect.anchorMin = Vector2.zero;
        _joystickRect.anchorMax = Vector2.zero;
        _joystickRect.pivot = Vector2.zero;
        _joystickRect.anchoredPosition = new Vector2(100f, 92f);
        _joystickRect.sizeDelta = new Vector2(300f, 300f);

        Image backing = joystickObject.GetComponent<Image>();
        backing.sprite = SalonUiFactory.GetCircleSprite();
        backing.color = new Color(0.33f, 0.20f, 0.14f, .88f);
        backing.raycastTarget = true;

        // Add the custom event surface before any Selectable so direct test
        // dispatch and normal EventSystem dispatch both reach the action layer.
        _joystickSurface = joystickObject.AddComponent<JoystickPointerSurface>();
        _joystickSurface.Owner = this;

        GameObject ringObject = new GameObject(
            "JoystickRing",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        ringObject.transform.SetParent(joystickObject.transform, false);
        RectTransform ringRect = ringObject.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = new Vector2(12f, 12f);
        ringRect.offsetMax = new Vector2(-12f, -12f);
        Image ring = ringObject.GetComponent<Image>();
        ring.sprite = SalonUiFactory.GetCircleSprite();
        ring.color = new Color(0.95f, 0.82f, 0.65f, .16f);
        ring.raycastTarget = false;

        GameObject knobObject = new GameObject(
            "JoystickKnob",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        knobObject.transform.SetParent(joystickObject.transform, false);
        RectTransform knobRect = knobObject.GetComponent<RectTransform>();
        _joystickKnobRect = knobRect;
        knobRect.anchorMin = knobRect.anchorMax = new Vector2(.5f, .5f);
        knobRect.anchoredPosition = Vector2.zero;
        knobRect.sizeDelta = new Vector2(112f, 112f);
        Image knob = knobObject.GetComponent<Image>();
        knob.sprite = SalonUiFactory.GetCircleSprite();
        knob.color = Teal;
        knob.raycastTarget = false;
    }

    private void CreateInteractionButton(RectTransform root)
    {
        GameObject buttonObject = new GameObject(
            "MobileInteractionButton",
            typeof(RectTransform),
            typeof(CanvasRenderer));
        buttonObject.transform.SetParent(root, false);
        _interactionRect = buttonObject.GetComponent<RectTransform>();
        _interactionRect.anchorMin = new Vector2(1f, 0f);
        _interactionRect.anchorMax = new Vector2(1f, 0f);
        _interactionRect.pivot = new Vector2(1f, 0f);
        _interactionRect.anchoredPosition = new Vector2(-100f, 104f);
        _interactionRect.sizeDelta = new Vector2(320f, 184f);

        // Keep this component ahead of Button's Selectable pointer handlers so
        // pointer IDs are tracked by our action surface as well as by uGUI.
        _interactionSurface = buttonObject.AddComponent<InteractionPointerSurface>();
        _interactionSurface.Owner = this;

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = SalonUiFactory.GetRoundedPanelSprite();
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = TealDark;
        buttonImage.raycastTarget = true;

        _interactionButton = buttonObject.AddComponent<Button>();
        _interactionButton.targetGraphic = buttonImage;
        ColorBlock colors = _interactionButton.colors;
        colors.normalColor = TealDark;
        colors.highlightedColor = Teal;
        colors.pressedColor = new Color32(29, 76, 73, 255);
        colors.selectedColor = Teal;
        colors.disabledColor = new Color32(88, 82, 75, 175);
        colors.fadeDuration = .08f;
        _interactionButton.colors = colors;

        GameObject labelObject = new GameObject(
            "InteractionLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);
        _interactionLabel = labelObject.GetComponent<Text>();
        ConfigureLabel(_interactionLabel, "操作", 34, Color.white, TextAnchor.MiddleCenter);
    }

    private void CreateHint(RectTransform root)
    {
        GameObject hintObject = new GameObject(
            "MobileHint",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        hintObject.transform.SetParent(root, false);
        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f, 0f);
        hintRect.pivot = new Vector2(.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 22f);
        hintRect.sizeDelta = new Vector2(650f, 54f);
        Image backing = hintObject.GetComponent<Image>();
        backing.sprite = SalonUiFactory.GetRoundedPanelSprite();
        backing.type = Image.Type.Sliced;
        backing.color = new Color(0.17f, 0.12f, 0.10f, .88f);
        backing.raycastTarget = false;

        GameObject textObject = new GameObject(
            "MobileHintLabel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(hintObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);
        _hintLabel = textObject.GetComponent<Text>();
        ConfigureLabel(_hintLabel, string.Empty, 23, Cream, TextAnchor.MiddleCenter);
    }

    private static void ConfigureLabel(Text label, string text, int size, Color color, TextAnchor alignment)
    {
        if (label == null)
            return;
        label.text = text;
        label.font = SalonUiFactory.GetPackagedUiFont();
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(12, size / 2);
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;
    }

    private void Update()
    {
        Vector2 keyboardMove = ReadKeyboardMove();
        if (_joystickOrigins.Count == 0)
            _move = keyboardMove;

        _keyboardInteractionHeld = Input.GetKey(KeyCode.Space);
        if (_keyboardInteractionHeld && Input.GetKeyDown(KeyCode.Space))
            _interactionPressed = true;
    }

    private static Vector2 ReadKeyboardMove()
    {
        float horizontal = 0f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
        Vector2 input = new Vector2(horizontal, vertical);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void OnEnable() => ResetInput();

    private void OnDisable() => ResetInput();

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            ResetInput();
    }

    private void HandleJoystickDown(PointerEventData eventData)
    {
        if (eventData == null || _joystickRect == null)
            return;
        Vector2 local = ToJoystickLocal(eventData);
        _joystickOrigins[eventData.pointerId] = local;
        _joystickMoves[eventData.pointerId] = Vector2.zero;
        _activeJoystickPointerId = eventData.pointerId;
        _move = Vector2.zero;
        SetJoystickKnobPosition(Vector2.zero);
    }

    private void HandleJoystickDrag(PointerEventData eventData)
    {
        if (eventData == null || !_joystickOrigins.ContainsKey(eventData.pointerId))
            return;

        Vector2 local = ToJoystickLocal(eventData);
        Vector2 delta = local - _joystickOrigins[eventData.pointerId];
        float radius = GetJoystickRadius();
        Vector2 raw = radius <= Mathf.Epsilon ? Vector2.zero : delta / radius;
        Vector2 filtered = ApplyRadialDeadzone(raw, .16f);
        _joystickMoves[eventData.pointerId] = filtered;
        if (_activeJoystickPointerId == eventData.pointerId)
        {
            _move = filtered;
            SetJoystickKnobPosition(filtered);
        }
    }

    private void HandleJoystickUp(PointerEventData eventData)
    {
        if (eventData == null)
            return;
        int pointerId = eventData.pointerId;
        _joystickOrigins.Remove(pointerId);
        _joystickMoves.Remove(pointerId);
        if (_activeJoystickPointerId != pointerId)
            return;

        _activeJoystickPointerId = int.MinValue;
        _move = Vector2.zero;
        SetJoystickKnobPosition(Vector2.zero);
        foreach (KeyValuePair<int, Vector2> remaining in _joystickMoves)
        {
            _activeJoystickPointerId = remaining.Key;
            _move = remaining.Value;
            SetJoystickKnobPosition(_move);
            break;
        }
    }

    private void HandleInteractionDown(PointerEventData eventData)
    {
        if (eventData == null || !_interactionAvailable)
            return;
        if (_interactionPointerIds.Add(eventData.pointerId))
            _interactionPressed = true;
    }

    private void HandleInteractionUp(PointerEventData eventData)
    {
        if (eventData == null)
            return;
        _interactionPointerIds.Remove(eventData.pointerId);
    }

    private Vector2 ToJoystickLocal(PointerEventData eventData)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _joystickRect, eventData.position, eventData.pressEventCamera, out local))
            return local;
        return eventData.position;
    }

    private float GetJoystickRadius()
    {
        if (_joystickRect == null)
            return 120f;
        float radius = Mathf.Min(_joystickRect.rect.width, _joystickRect.rect.height) * .5f;
        return radius > Mathf.Epsilon ? radius : 120f;
    }

    private void SetJoystickKnobPosition(Vector2 action)
    {
        if (_joystickKnobRect == null)
            return;
        float knobRadius = Mathf.Min(_joystickKnobRect.rect.width, _joystickKnobRect.rect.height) * .5f;
        float travel = Mathf.Max(0f, GetJoystickRadius() - knobRadius - 6f);
        _joystickKnobRect.anchoredPosition = action * travel;
    }

    private static Vector2 ApplyRadialDeadzone(Vector2 raw, float deadzone)
    {
        float magnitude = raw.magnitude;
        if (magnitude <= deadzone)
            return Vector2.zero;
        float scaled = Mathf.Clamp01((magnitude - deadzone) / Mathf.Max(.0001f, 1f - deadzone));
        return raw / magnitude * scaled;
    }

    private sealed class JoystickPointerSurface : MonoBehaviour, IPointerDownHandler, IDragHandler,
        IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        public SalonMobileControls Owner;

        public void OnPointerDown(PointerEventData eventData) => Owner?.HandleJoystickDown(eventData);

        public void OnDrag(PointerEventData eventData) => Owner?.HandleJoystickDrag(eventData);

        public void OnPointerUp(PointerEventData eventData) => Owner?.HandleJoystickUp(eventData);

        public void OnPointerExit(PointerEventData eventData) => Owner?.HandleJoystickUp(eventData);

        public void OnCancel(BaseEventData eventData)
        {
            if (eventData is PointerEventData pointerEvent)
                Owner?.HandleJoystickUp(pointerEvent);
        }
    }

    private sealed class InteractionPointerSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        public SalonMobileControls Owner;

        public void OnPointerDown(PointerEventData eventData) => Owner?.HandleInteractionDown(eventData);

        public void OnPointerUp(PointerEventData eventData) => Owner?.HandleInteractionUp(eventData);

        public void OnPointerExit(PointerEventData eventData) => Owner?.HandleInteractionUp(eventData);

        public void OnCancel(BaseEventData eventData)
        {
            if (eventData is PointerEventData pointerEvent)
                Owner?.HandleInteractionUp(pointerEvent);
        }
    }
}
