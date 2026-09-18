using System.Collections.Generic;
using HairSalon;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only queue strip for the mobile HUD.
///
/// The in-world demand bubbles are useful next to a workstation, but they are
/// too small and can overlap while several customers are waiting. This view
/// keeps the first few customers visible in a fixed safe-area column instead.
/// It deliberately owns no game state and only reads the models passed to
/// <see cref="Refresh"/>.
/// </summary>
public sealed class SalonMobileQueueView : MonoBehaviour
{
    public const int MaxVisibleCustomers = 4;
    public const float CardWidth = 310f;
    public const float CardHeight = 82f;
    public const float CardSpacing = 8f;

    private static readonly Color CardCream = new Color32(249, 226, 189, 250);
    private static readonly Color CardBrown = new Color32(86, 52, 35, 255);
    private static readonly Color Ink = new Color32(59, 48, 38, 255);
    private static readonly Color BarTrack = new Color32(77, 52, 39, 100);
    private static readonly Color PatienceGood = new Color32(63, 143, 136, 255);
    private static readonly Color PatienceWarning = new Color32(227, 159, 74, 255);
    private static readonly Color PatienceDanger = new Color32(204, 87, 70, 255);

    private readonly List<QueueCard> _cards = new List<QueueCard>(MaxVisibleCustomers);
    private RectTransform _rect;
    private bool _built;
    private bool _visibleRequested = true;
    private int _visibleCardCount;

    /// <summary>How many Entering/Waiting customers are currently shown.</summary>
    public int VisibleCardCount => _visibleCardCount;

    /// <summary>Whether the queue currently has visible cards.</summary>
    public bool IsVisible => this != null && gameObject != null && gameObject.activeSelf;

    /// <summary>
    /// Creates the queue under an existing HUD safe-area transform. Reusing an
    /// existing child keeps repeated HUD setup calls from creating duplicates.
    /// </summary>
    public static SalonMobileQueueView Create(Transform safeParent)
    {
        SalonMobileQueueView existing = null;
        if (safeParent != null)
        {
            existing = safeParent.GetComponent<SalonMobileQueueView>() ??
                safeParent.GetComponentInChildren<SalonMobileQueueView>(true);
        }

        if (existing != null)
        {
            existing.EnsureBuilt();
            return existing;
        }

        GameObject root = new GameObject("Mobile Customer Queue", typeof(RectTransform));
        if (safeParent != null)
            root.transform.SetParent(safeParent, false);

        SalonMobileQueueView view = root.AddComponent<SalonMobileQueueView>();
        view.EnsureBuilt();
        return view;
    }

    /// <summary>
    /// Updates the cards from the current model order. Only Entering and
    /// Waiting customers are eligible; the source list is never changed.
    /// </summary>
    public void Refresh(IReadOnlyList<CustomerModel> customers)
    {
        EnsureBuilt();

        int count = 0;
        if (customers != null)
        {
            for (int i = 0; i < customers.Count && count < MaxVisibleCustomers; i++)
            {
                CustomerModel customer = customers[i];
                if (customer == null || !IsQueueCustomer(customer))
                    continue;

                _cards[count].Refresh(customer);
                count++;
            }
        }

        for (int i = 0; i < _cards.Count; i++)
            _cards[i].SetVisible(i < count);

        _visibleCardCount = count;
        ApplyVisibility();
    }

    /// <summary>
    /// Controls the whole strip. Refreshing later will not re-enable a strip
    /// that was explicitly hidden by a modal or a non-playable day state.
    /// </summary>
    public void SetVisible(bool visible)
    {
        _visibleRequested = visible;
        ApplyVisibility();
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built)
            return;

        _rect = transform as RectTransform;
        if (_rect == null)
            return;

        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.anchoredPosition = new Vector2(24f, -225f);
        _rect.sizeDelta = new Vector2(CardWidth,
            MaxVisibleCustomers * CardHeight + (MaxVisibleCustomers - 1) * CardSpacing);

        for (int i = 0; i < MaxVisibleCustomers; i++)
            _cards.Add(new QueueCard(transform, i));

        _built = true;
        SalonUiFactory.MakeClickThrough(gameObject);
    }

    private void ApplyVisibility()
    {
        if (gameObject == null)
            return;

        bool shouldBeVisible = _visibleRequested && _visibleCardCount > 0;
        if (gameObject.activeSelf != shouldBeVisible)
            gameObject.SetActive(shouldBeVisible);
    }

    private static bool IsQueueCustomer(CustomerModel customer)
    {
        return customer.State == CustomerState.Entering || customer.State == CustomerState.Waiting;
    }

    private sealed class QueueCard
    {
        private readonly GameObject _root;
        private readonly Text _customerLabel;
        private readonly Text _patienceValue;
        private readonly Image[] _orderIcons = new Image[3];
        private readonly Image _patienceTrack;
        private readonly Image _patienceFill;

        internal QueueCard(Transform parent, int index)
        {
            _root = new GameObject("Mobile Queue Card " + (index + 1),
                typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(parent, false);

            RectTransform cardRect = _root.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 1f);
            cardRect.anchorMax = new Vector2(0f, 1f);
            cardRect.pivot = new Vector2(0f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -index * (CardHeight + CardSpacing));
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);

            Image background = _root.GetComponent<Image>();
            background.sprite = SalonUiFactory.GetRoundedPanelSprite();
            background.type = Image.Type.Sliced;
            background.color = CardCream;
            background.raycastTarget = false;
            Outline outline = _root.AddComponent<Outline>();
            outline.effectColor = CardBrown;
            outline.effectDistance = new Vector2(0f, -3f);
            outline.useGraphicAlpha = true;

            _customerLabel = CreateLabel("Customer Number", _root.transform,
                new Vector2(-118f, 9f), new Vector2(60f, 48f), 28, TextAnchor.MiddleCenter);

            float[] iconPositions = { -53f, -6f, 41f };
            for (int i = 0; i < _orderIcons.Length; i++)
            {
                _orderIcons[i] = DemandBubbleArtwork.Image("Order Icon " + (i + 1),
                    _root.transform, new Vector2(iconPositions[i], 8f), new Vector2(40f, 40f), null);
                _orderIcons[i].gameObject.SetActive(false);
            }

            _patienceValue = CreateLabel("Patience Value", _root.transform,
                new Vector2(111f, 9f), new Vector2(70f, 48f), 26, TextAnchor.MiddleCenter);

            _patienceTrack = CreateBar("Patience Track", _root.transform,
                new Vector2(0f, -24f), new Vector2(282f, 12f), BarTrack);
            _patienceFill = CreateBar("Patience Fill", _patienceTrack.transform,
                new Vector2(2f, 0f), new Vector2(278f, 8f), PatienceGood);
            RectTransform fillRect = _patienceFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, .5f);
            fillRect.anchorMax = new Vector2(0f, .5f);
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.localScale = new Vector3(0f, 1f, 1f);
        }

        internal void Refresh(CustomerModel customer)
        {
            _customerLabel.text = (customer.Id + 1) + "号";

            float patience = customer == null ? 0f : Mathf.Clamp01(customer.PatienceProgress);
            _patienceValue.text = Mathf.RoundToInt(patience * 100f) + "%";
            _patienceFill.color = PatienceColor(patience);
            _patienceFill.rectTransform.localScale = new Vector3(patience, 1f, 1f);
            _patienceTrack.color = BarTrack;

            for (int i = 0; i < _orderIcons.Length; i++)
            {
                bool active = customer.Needs != null && i < customer.Needs.Count;
                _orderIcons[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                _orderIcons[i].sprite = DemandBubbleArtwork.Service(customer.Needs[i]);
                _orderIcons[i].color = i < customer.CompletedStepCount
                    ? new Color(1f, 1f, 1f, .38f)
                    : Color.white;
            }
        }

        internal void SetVisible(bool visible)
        {
            if (_root.activeSelf != visible)
                _root.SetActive(visible);
        }

        private static Text CreateLabel(string name, Transform parent, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text label = labelObject.GetComponent<Text>();
            label.font = SalonUiFactory.GetPackagedUiFont();
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Ink;
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateBar(string name, Transform parent, Vector2 position,
            Vector2 size, Color color)
        {
            GameObject barObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(parent, false);
            RectTransform rect = barObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image bar = barObject.GetComponent<Image>();
            bar.sprite = SalonUiFactory.GetRoundedPanelSprite();
            bar.type = Image.Type.Sliced;
            bar.color = color;
            bar.raycastTarget = false;
            return bar;
        }

        private static Color PatienceColor(float patience)
        {
            if (patience <= .25f) return PatienceDanger;
            if (patience <= .55f) return PatienceWarning;
            return PatienceGood;
        }
    }
}
