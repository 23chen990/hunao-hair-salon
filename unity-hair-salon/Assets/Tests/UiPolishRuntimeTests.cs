using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class UiPolishRuntimeTests
{
    [Test]
    public void ServiceToolbarUsesAPermanentWoodAndCreamTrayHierarchy()
    {
        var root = new GameObject("UI Polish Toolbar Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        GameObject toolbar = FindAny("Workstation Tools");
        Assert.IsNotNull(toolbar);
        Assert.IsNotNull(toolbar.transform.Find("Tool Tray Surface"));
        Assert.IsNotNull(toolbar.transform.Find("Tool Status Ribbon"));
        Assert.IsNotNull(toolbar.transform.Find("Tool Buttons"));
        Assert.AreEqual(Image.Type.Sliced, toolbar.GetComponent<Image>().type);
        Assert.IsNotNull(toolbar.GetComponent<Outline>());

        DestroyRuntimeObjects();
    }

    [Test]
    public void ExistingFullScreenFlowsShareTheSameReadableModalCardLanguage()
    {
        var root = new GameObject("UI Polish Modal Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        AssertCard("Day Start Overlay", "Opening Card", "Opening Surface");
        AssertCard("Pause Overlay", "Pause Card", "Pause Surface");
        AssertCard("Day Result", "Result Card", "Result Surface");
        AssertCard("Equipment Shop", "Shop Card", "Shop Surface");

        Assert.That(FindAny("Button").GetComponent<RectTransform>().rect.height,
            Is.GreaterThanOrEqualTo(58f));
        Assert.AreEqual(Image.Type.Sliced, FindAny("Button").GetComponent<Image>().type);

        DestroyRuntimeObjects();
    }

    [Test]
    public void FormalHudStillUsesSafeAreaAndLandscapeScalePolicy()
    {
        var root = new GameObject("UI Polish Safe Area Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        CanvasScaler scaler = GameObject.Find("Salon HUD").GetComponent<CanvasScaler>();
        Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
        Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
        Assert.IsNotNull(GameObject.Find("Safe Area").GetComponent<SalonSafeArea>());
        Assert.IsNotNull(GameObject.Find("Top HUD"));

        DestroyRuntimeObjects();
    }

    [Test]
    public void WebGlUiUsesAPackagedChineseFontInsteadOfAHostFont()
    {
        Font packaged = Resources.Load<Font>("Fonts/NotoSansSC-UI");
        Assert.IsNotNull(packaged, "The release must package its Chinese UI glyphs.");

        var root = new GameObject("UI Polish Font Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        Text openingLabel = FindAny("Opening Surface").GetComponentInChildren<Text>(true);
        Assert.AreSame(packaged, openingLabel.font);

        DestroyRuntimeObjects();
    }

    private static void AssertCard(string overlayName, string cardName, string surfaceName)
    {
        Transform overlay = FindAny(overlayName)?.transform;
        Assert.IsNotNull(overlay, overlayName);
        Transform card = overlay.Find(cardName);
        Assert.IsNotNull(card, cardName);
        Assert.AreEqual(Image.Type.Sliced, card.GetComponent<Image>().type);
        Assert.IsNotNull(card.Find(surfaceName), surfaceName);
    }

    private static GameObject FindAny(string name)
    {
        foreach (Transform item in Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item.name == name)
                return item.gameObject;
        return null;
    }

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    private static void DestroyRuntimeObjects()
    {
        foreach (GameObject item in Object.FindObjectsByType<GameObject>())
            if (item != null)
                Object.DestroyImmediate(item);
    }
}
