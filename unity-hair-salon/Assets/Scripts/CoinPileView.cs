using System;
using System.Collections;
using HairSalon;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CoinPileView : MonoBehaviour, IPointerClickHandler
{
    public const int UnifiedVisualCoinCount = 6;
    private static readonly Color CoinGold = new Color(1f, .66f, .05f);
    private static readonly Color CoinEdge = new Color(.82f, .42f, .02f);
    private Vector3 _restScale = Vector3.one;
    private bool _collecting;

    public PaymentDropModel Drop { get; private set; }
    public int VisualCoinCount { get; private set; }
    public event Action<CoinPileView> Clicked;

    public void Initialize(PaymentDropModel drop)
    {
        Drop = drop ?? throw new ArgumentNullException(nameof(drop));
        VisualCoinCount = UnifiedVisualCoinCount;
        gameObject.name = "待拾取金币";
        BuildPile();
        _restScale = transform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_collecting && Drop != null && Drop.State == PaymentDropState.Pending)
            Clicked?.Invoke(this);
    }

    public void PlayCollection(Canvas hudCanvas, RectTransform target, Camera sceneCamera, Action completed)
    {
        if (_collecting) return;
        _collecting = true;
        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        StartCoroutine(CollectRoutine(hudCanvas, target, sceneCamera, completed));
    }

    private void Update()
    {
        if (_collecting) return;
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * .045f;
        transform.localScale = _restScale * pulse;
        transform.Rotate(Vector3.up, 22f * Time.unscaledDeltaTime, Space.World);
    }

    private void BuildPile()
    {
        var collider = gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, .28f, 0f);
        collider.size = new Vector3(1.35f, .7f, 1.1f);

        for (int i = 0; i < VisualCoinCount; i++)
        {
            int layer = i / 4;
            float angle = i * 2.39996f;
            float radius = layer == 0 ? .3f : .16f + (i % 3) * .1f;
            var coin = SalonPrimitiveFactory.CreateCylinder(
                transform,
                new Vector3(Mathf.Cos(angle) * radius, .08f + layer * .11f, Mathf.Sin(angle) * radius),
                new Vector3(.28f, .055f, .28f),
                i % 3 == 0 ? CoinEdge : CoinGold);
            coin.name = "Low Poly Coin " + i;
            Collider childCollider = coin.GetComponent<Collider>();
            if (childCollider != null) childCollider.enabled = false;
        }

    }

    private IEnumerator CollectRoutine(Canvas hudCanvas, RectTransform target, Camera sceneCamera, Action completed)
    {
        Vector3 start = sceneCamera == null ? Vector3.zero : sceneCamera.WorldToScreenPoint(transform.position + Vector3.up * .4f);
        Vector3 end = target == null ? new Vector3(Screen.width * .5f, Screen.height - 60f) :
            RectTransformUtility.WorldToScreenPoint(null, target.position);
        const int flyingCoins = 5;
        var tokens = new RectTransform[flyingCoins];
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = new GameObject("Flying Coin", typeof(RectTransform), typeof(Image));
            token.transform.SetParent(hudCanvas.transform, false);
            var rect = token.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.position = start;
            var image = token.GetComponent<Image>();
            image.sprite = SalonUiFactory.GetCircleSprite();
            image.color = CoinGold;
            image.raycastTarget = false;
            tokens[i] = rect;
        }

        float duration = .62f;
        float elapsed = 0f;
        while (elapsed < duration + .06f * (tokens.Length - 1))
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < tokens.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * .06f) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 position = Vector3.Lerp(start, end, eased);
                position.y += Mathf.Sin(t * Mathf.PI) * (110f + i * 9f);
                tokens[i].position = position;
                tokens[i].localScale = Vector3.one * Mathf.Lerp(1f, .45f, eased);
            }
            transform.localScale = Vector3.Lerp(_restScale, Vector3.zero, Mathf.Clamp01(elapsed / .24f));
            yield return null;
        }

        for (int i = 0; i < tokens.Length; i++) Destroy(tokens[i].gameObject);
        completed?.Invoke();
        Destroy(gameObject);
    }
}
