using System.Reflection;
using HairSalon;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReputationBarRuntimeTests
{
    [Test]
    public void RuntimeBuildsTheRequiredReputationBarLayersAndUsesACroppedFill()
    {
        var root = new GameObject("Reputation Bar Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");

        Assert.IsNotNull(GameObject.Find("ReputationBar_BG")?.GetComponent<Image>()?.sprite);
        Assert.IsNotNull(GameObject.Find("Smile_Icon")?.GetComponent<Image>()?.sprite);
        Assert.IsNotNull(GameObject.Find("Progress_Track")?.GetComponent<Image>()?.sprite);
        Image fill = GameObject.Find("Progress_Fill")?.GetComponent<Image>();
        Assert.IsNotNull(fill?.sprite);
        Assert.AreEqual(Image.Type.Filled, fill.type);
        Assert.AreEqual(Image.FillMethod.Horizontal, fill.fillMethod);
        Assert.AreEqual(0, fill.fillOrigin);
        Assert.AreEqual(.9f, fill.fillAmount, .0001f);
        Assert.AreEqual("90/100", GameObject.Find("ValueText").GetComponent<TMP_Text>().text);

        Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    [TestCase(100, "Smile_Happy", 1f, "100/100")]
    [TestCase(80, "Smile_Happy", .8f, "80/100")]
    [TestCase(79, "Smile_Neutral", .79f, "79/100")]
    [TestCase(40, "Smile_Neutral", .4f, "40/100")]
    [TestCase(39, "Smile_Sad", .39f, "39/100")]
    [TestCase(0, "Smile_Sad", 0f, "0/100")]
    public void ChangingSatisfactionUpdatesTextFillAndMoodWithoutMovingTheSmile(
        int value, string expectedSprite, float expectedFill, string expectedText)
    {
        var root = new GameObject("Reputation Bar State Runtime");
        var demo = root.AddComponent<SalonDemo>();
        Invoke(demo, "Start");
        var model = (ShopSatisfactionModel)Field(demo, "_satisfaction");
        RectTransform smile = GameObject.Find("Smile_Icon").GetComponent<RectTransform>();
        Vector2 position = smile.anchoredPosition;
        Vector2 size = smile.sizeDelta;
        Vector2 anchorMin = smile.anchorMin;
        Vector2 anchorMax = smile.anchorMax;

        model.SetCurrent(value);

        Assert.AreEqual(expectedText, GameObject.Find("ValueText").GetComponent<TMP_Text>().text);
        Assert.AreEqual(expectedFill, GameObject.Find("Progress_Fill").GetComponent<Image>().fillAmount, .0001f);
        Assert.AreEqual(expectedSprite, GameObject.Find("Smile_Icon").GetComponent<Image>().sprite.name);
        Assert.AreEqual(position, smile.anchoredPosition);
        Assert.AreEqual(size, smile.sizeDelta);
        Assert.AreEqual(anchorMin, smile.anchorMin);
        Assert.AreEqual(anchorMax, smile.anchorMax);

        Object.DestroyImmediate(root);
        DestroyRuntimeObjects();
    }

    private static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    private static void DestroyRuntimeObjects()
    {
        foreach (GameObject item in Object.FindObjectsByType<GameObject>())
            if (item != null && item.name != "Reputation Bar Runtime" &&
                item.name != "Reputation Bar State Runtime")
                Object.DestroyImmediate(item);
    }
}
