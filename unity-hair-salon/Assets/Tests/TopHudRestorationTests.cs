using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopHudRestorationTests
{
    [Test]
    public void WebGlHasAPackagedFontFallbackInsteadOfDependingOnHostFonts()
    {
        Assert.That(SalonDemo.GetPackagedTopHudFallback(), Is.Not.Null);
    }

    [Test]
    public void RuntimeBuildsTheFiveOfficialTopHudRegionsFromPackageArt()
    {
        var root = new GameObject("Top HUD Restoration Runtime");
        var demo = root.AddComponent<SalonDemo>();

        Invoke(demo, "Start");

        Transform topHud = GameObject.Find("Top HUD").transform;
        Assert.IsNotNull(topHud.Find("Left Group/Calendar"));
        Assert.IsNotNull(topHud.Find("Left Group/Weather"));
        Assert.IsNotNull(topHud.Find("Right Group/Coins"));
        Assert.IsNotNull(topHud.Find("Right Group/Satisfaction"));
        Assert.IsNotNull(topHud.Find("Right Group/Settings"));

        foreach (Image image in topHud.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name.EndsWith("Artwork"))
                Assert.IsNotNull(image.sprite, image.gameObject.name + " should use an official sprite.");

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void CalendarCoinsAndSatisfactionRemainDynamicTextMeshProFields()
    {
        var root = new GameObject("Top HUD Dynamic Fields Runtime");
        var demo = root.AddComponent<SalonDemo>();

        Invoke(demo, "Start");

        Assert.AreEqual("周三", ((TMP_Text)Field(demo, "_weekdayLabel")).text);
        Assert.AreEqual("12", ((TMP_Text)Field(demo, "_topHudDayLabel")).text);
        var game = (HairSalon.SalonGameModel)Field(demo, "_game");
        Assert.AreEqual(game.Balance.ToString("N0"), ((TMP_Text)Field(demo, "_topHudCoinLabel")).text);
        Assert.AreEqual("90/100", ((TMP_Text)Field(demo, "_satisfactionLabel")).text);
        Assert.IsFalse(GameObject.Find("Weather").GetComponentsInChildren<TMP_Text>(true)[0].text.Contains("晴天"));

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void OfficialSettingsArtworkIsThePauseButtonTarget()
    {
        var root = new GameObject("Top HUD Settings Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        Invoke(demo, "StartBusinessDay");

        Button settings = GameObject.Find("Settings").GetComponent<Button>();
        Assert.IsNotNull(settings);
        settings.onClick.Invoke();

        Assert.IsTrue(((GameObject)Field(demo, "_pausePanel")).activeSelf);

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [Test]
    public void OfficialBackingPlatesRemainThePackageArtButRenderSemiTransparent()
    {
        var root = new GameObject("Top HUD Transparency Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        AssertBacking("Weather Artwork", "dark-panel");
        AssertBacking("Coins Artwork", "dark-panel");
        Assert.AreEqual("ReputationBar_BG",
            GameObject.Find("ReputationBar_BG").GetComponent<Image>().sprite.name);
        Assert.AreEqual("Progress_Track",
            GameObject.Find("Progress_Track").GetComponent<Image>().sprite.name);
        Assert.AreEqual("Progress_Fill",
            GameObject.Find("Progress_Fill").GetComponent<Image>().sprite.name);
        Assert.AreEqual("settings-backplate-final",
            GameObject.Find("Settings Backplate").GetComponent<Image>().sprite.name);
        Assert.AreEqual("Smile_Happy", GameObject.Find("Smile_Icon").GetComponent<Image>().sprite.name);

        UnityEngine.Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    private static void AssertBacking(string objectName, string spriteName)
    {
        Image image = GameObject.Find(objectName).GetComponent<Image>();
        Assert.AreEqual(spriteName, image.sprite.name);
        Assert.That(image.color.a, Is.InRange(.55f, .9f));
    }

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    private static void DestroyRuntimeObjects()
    {
        foreach (GameObject item in UnityEngine.Object.FindObjectsByType<GameObject>())
            if (item != null && item.name != "Top HUD Restoration Runtime" &&
                item.name != "Top HUD Dynamic Fields Runtime" && item.name != "Top HUD Settings Runtime")
                UnityEngine.Object.DestroyImmediate(item);
    }
}
