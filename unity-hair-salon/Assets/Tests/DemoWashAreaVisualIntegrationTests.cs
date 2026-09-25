using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DemoWashAreaVisualIntegrationTests
{
    private static Type RuntimeType => Type.GetType(
        "HairSalon.AssetPipeline.DemoWashAreaVisualIntegration, HairSalon.Runtime");

    [Test]
    public void RuntimeExistsAndParsesOnlyExplicitDevelopmentEvidenceModes()
    {
        Assert.That(RuntimeType, Is.Not.Null);
        MethodInfo parse = RuntimeType.GetMethod("ParseMode", BindingFlags.Public | BindingFlags.Static);
        Assert.That(parse, Is.Not.Null);
        Assert.That(parse.Invoke(null, new object[] { "https://localhost/?washAreaVisual=before" }),
            Is.EqualTo("before"));
        Assert.That(parse.Invoke(null, new object[] { "https://localhost/?washAreaVisual=after" }),
            Is.EqualTo("after"));
        Assert.That(parse.Invoke(null, new object[] { "https://localhost/?washAreaVisual=context" }),
            Is.EqualTo("context"));
        Assert.That(parse.Invoke(null, new object[] { "https://localhost/" }), Is.EqualTo(string.Empty));
    }

    [Test]
    public void PlaceholderVisualReplacementPreservesCollidersAndServiceAnchors()
    {
        Assert.That(RuntimeType, Is.Not.Null);
        MethodInfo disable = RuntimeType.GetMethod(
            "DisablePlaceholderVisuals", BindingFlags.Public | BindingFlags.Static);
        Assert.That(disable, Is.Not.Null);
        var station = new GameObject("Wash Workstation 1");
        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "Wash Base";
        placeholder.transform.SetParent(station.transform, false);
        var anchor = new GameObject("PlayerServiceAnchor");
        anchor.transform.SetParent(station.transform, false);
        try
        {
            int count = (int)disable.Invoke(null, new object[] { station.transform });
            Assert.That(count, Is.EqualTo(1));
            Assert.That(placeholder.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(placeholder.GetComponent<Collider>().enabled, Is.True,
                "The formal service collision must survive a visual-only replacement.");
            Assert.That(station.transform.Find("PlayerServiceAnchor"), Is.SameAs(anchor.transform));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(station);
        }
    }

    [Test]
    public void FormalMainSceneDeclaresTheVisualIntegrationComponent()
    {
        Assert.That(RuntimeType, Is.Not.Null);
        string scene = System.IO.File.ReadAllText("Assets/Scenes/HairSalonDemo.unity");
        string scriptPath = UnityEditor.AssetDatabase.GetAssetPath(
            UnityEditor.MonoScript.FromMonoBehaviour(
                (MonoBehaviour)new GameObject("probe").AddComponent(RuntimeType)));
        try
        {
            string guid = UnityEditor.AssetDatabase.AssetPathToGUID(scriptPath);
            Assert.That(scene, Does.Contain(guid));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(GameObject.Find("probe"));
        }
    }
}
