using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class AssetTestLabTests
{
    [Test]
    public void Initialize_CanDisplayEveryManifestAssetWithDiagnostics()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            AssetManifest manifest = AssetManifestLoader.LoadFromResources();

            lab.Initialize(manifest);

            Assert.That(lab.AssetCount, Is.EqualTo(manifest.Assets.Count));
            Assert.That(lab.CurrentAssetId, Is.EqualTo(manifest.Assets[0].Id));
            Assert.That(root.transform.Find("Current Asset/Pivot"), Is.Not.Null);
            Assert.That(root.transform.Find("Current Asset/Footprint"), Is.Not.Null);
            Assert.That(root.transform.Find("Current Asset/Collision"), Is.Not.Null);
            Assert.That(root.transform.Find("Current Asset/Anchors"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ToggleDirection_QuarterTurnsAssetsThatSupportBothWalls()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());

            lab.ToggleDirection();

            Assert.That(lab.CurrentDirection, Is.EqualTo("right-wall"));
            Assert.That(root.transform.Find("Current Asset/Visual").localEulerAngles.y,
                Is.EqualTo(90f).Within(.1f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NextAsset_EventuallyVisitsEveryStableId()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            AssetManifest manifest = AssetManifestLoader.LoadFromResources();
            lab.Initialize(manifest);
            var visited = new System.Collections.Generic.HashSet<string> { lab.CurrentAssetId };

            for (int i = 1; i < manifest.Assets.Count; i++)
            {
                lab.NextAsset();
                visited.Add(lab.CurrentAssetId);
            }

            Assert.That(visited.Count, Is.EqualTo(manifest.Assets.Count));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
