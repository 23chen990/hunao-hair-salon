using System.Collections.Generic;
using HairSalon;
using UnityEngine;
using UnityEngine.UI;

internal static class DemandBubbleArtwork
{
    private const string Root = "DemandBubble/";
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    public static Sprite Frame(int count, bool current)
    {
        if (count <= 1) return Load(current ? "bubble-single-current" : "bubble-single");
        return Load(current ? "bubble-wide-current" : "bubble-wide", current);
    }

    public static Sprite ProgressRing => Load("progress-ring");
    public static Sprite CompletionBadge => Load("completion-badge");

    public static Sprite Tool(SalonTool tool)
    {
        if (tool == SalonTool.Scissors) return Load("tool-scissors");
        if (tool == SalonTool.BlowDryer) return Load("tool-dryer");
        return Load("tool-brush");
    }

    public static Sprite Service(ServiceType service)
    {
        if (service == ServiceType.Cut) return Load("tool-scissors");
        if (service == ServiceType.Dry) return Load("tool-dryer");
        return Load("tool-brush");
    }

    public static Image Image(string name, Transform parent, Vector2 position, Vector2 size,
        Sprite sprite, bool preserveAspect = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    public static Text Plus(string name, Transform parent, Vector2 position, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(36f, 60f);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.18f, .16f, .13f, 1f);
        text.text = "+";
        text.raycastTarget = false;
        return text;
    }

    private static Sprite Load(string name, bool cropWideCurrent = false)
    {
        string key = cropWideCurrent ? name + "#crop" : name;
        if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Sprite imported = Resources.Load<Sprite>(Root + name);
        if (imported != null && !cropWideCurrent) return Cache[key] = imported;

        Texture2D texture = Resources.Load<Texture2D>(Root + name);
        if (texture == null) return null;
        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        if (cropWideCurrent && texture.width == 1254 && texture.height == 1254)
            rect = new Rect(0f, 220f, 1254f, 850f);
        Sprite sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect);
        sprite.name = name;
        Cache[key] = sprite;
        return sprite;
    }
}
