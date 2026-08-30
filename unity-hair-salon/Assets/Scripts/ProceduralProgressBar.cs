using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-generated progress whose fill is driven by transform width, not Image.fillAmount.
/// </summary>
public sealed class ProceduralProgressBar
{
    public GameObject Root { get; }
    public RectTransform FillRect { get; }
    public Image TrackImage { get; }
    public Image FillImage { get; }
    public float Progress { get; private set; }
    public bool IsVisible => Root != null && Root.activeSelf;
    public Color FillColor => FillImage == null ? Color.clear : FillImage.color;

    private ProceduralProgressBar(GameObject root, RectTransform fillRect, Image trackImage, Image fillImage)
    {
        Root = root;
        FillRect = fillRect;
        TrackImage = trackImage;
        FillImage = fillImage;
    }

    public static ProceduralProgressBar Create(Transform parent, string trackName, string fillName,
        Vector2 position, Vector2 size, Color trackColor)
    {
        var trackObject = new GameObject(trackName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackObject.transform.SetParent(parent, false);
        var trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(.5f, .5f);
        trackRect.pivot = new Vector2(.5f, .5f);
        trackRect.anchoredPosition = position;
        trackRect.sizeDelta = size;
        Image trackImage = trackObject.GetComponent<Image>();
        trackImage.sprite = SalonUiFactory.GetCircleSprite();
        trackImage.type = Image.Type.Simple;
        trackImage.color = trackColor;
        trackImage.raycastTarget = false;

        var fillObject = new GameObject(fillName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(trackObject.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, .5f);
        fillRect.pivot = new Vector2(0f, .5f);
        fillRect.anchoredPosition = new Vector2(2f, 0f);
        fillRect.sizeDelta = new Vector2(Mathf.Max(1f, size.x - 4f), Mathf.Max(1f, size.y - 4f));
        fillRect.localScale = new Vector3(0f, 1f, 1f);
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = SalonUiFactory.GetCircleSprite();
        fillImage.type = Image.Type.Simple;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;

        trackObject.SetActive(false);
        return new ProceduralProgressBar(trackObject, fillRect, trackImage, fillImage);
    }

    public void SetProgress(float progress, Color color)
    {
        Progress = Mathf.Clamp01(progress);
        if (FillImage != null) FillImage.color = color;
        if (FillRect != null) FillRect.localScale = new Vector3(Progress, 1f, 1f);
        if (Root != null) Root.SetActive(Progress > .001f);
    }

    public void Clear()
    {
        Progress = 0f;
        if (FillRect != null) FillRect.localScale = new Vector3(0f, 1f, 1f);
        if (Root != null) Root.SetActive(false);
    }
}
