using System;
using System.Reflection;
using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class WashCraftIntegrationTests
{
    private static Type RuntimeType => Type.GetType("HairSalon.AssetPipeline.WashCraftIntegration, HairSalon.Runtime");

    [Test]
    public void WashModelHasRealGeometryAndMatchesTheExistingStationEnvelope()
    {
        var model = Resources.Load<GameObject>("Models/WashCraft/wash-station");
        Assert.That(model, Is.Not.Null, "The authored Blender furniture must be available in the playable build.");
        var instance = UnityEngine.Object.Instantiate(model);
        try
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.x, Is.InRange(1.8f, 2.3f));
            Assert.That(bounds.size.z, Is.InRange(2.7f, 3.3f));
            Assert.That(bounds.min.y, Is.InRange(-.05f, .05f));
            Assert.That(bounds.size.y, Is.InRange(1.5f, 2.3f));
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    [Test]
    public void ReplacementPreservesExistingServiceAnchorAndColliderIdentity()
    {
        Assert.That(RuntimeType, Is.Not.Null);
        var station = new GameObject("Wash Workstation 1");
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Wash Base";
        cube.transform.SetParent(station.transform);
        var anchor = new GameObject("CustomerSeatAnchor").transform;
        anchor.SetParent(station.transform);
        anchor.position = SalonDemo.WashCustomerAnchorPosition;
        var collider = cube.GetComponent<Collider>();
        try
        {
            var method = RuntimeType.GetMethod("ReplaceStationVisual", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { station.transform, new Vector3(-7.2f, 0f, 4.6f) });
            Assert.That(anchor.position, Is.EqualTo(SalonDemo.WashCustomerAnchorPosition));
            Assert.That(cube.GetComponent<Collider>(), Is.SameAs(collider));
            Assert.That(collider.enabled, Is.True);
            Assert.That(cube.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(station.transform.Find("Wash Craft Visual"), Is.Not.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(station); }
    }

    [Test]
    public void NewWorldAssetsHaveValidatedFootprintsAndServiceAnchors()
    {
        var manifest = AssetManifestLoader.LoadFromResources();
        var asset = manifest.Assets.Find(item => item.Id == "furniture-wash-station-crafted");
        Assert.That(asset, Is.Not.Null);
        Assert.That(AssetManifestValidator.Validate(manifest), Is.Empty);
        Assert.That(asset.RequiredAnchors, Does.Contain("customer-seat"));
        Assert.That(asset.RequiredAnchors, Does.Contain("stylist-work"));
        Assert.That(asset.Shadow.Mode, Is.EqualTo("procedural"));
        Assert.That(asset.Status, Is.EqualTo("review"), "Technical completion cannot grant product approval.");
    }
    [Test]
    public void AssetLabCanDisplayAuthoredModelWithoutTreatingItAsAPng()
    {
        var root = new GameObject("Craft Lab Test");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());
            Assert.That(lab.SelectAsset("furniture-wash-station-crafted"), Is.True);
            Assert.That(root.transform.Find("Current Asset/Visual/Model"), Is.Not.Null);
            Assert.That(lab.PixelPreviewTexture, Is.Null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void InvalidAuthoredModelDimensionsAreRejectedBeforeIntegration()
    {
        var manifest = AssetManifestLoader.LoadFromResources();
        var asset = manifest.Assets.Find(a => a.Id == "furniture-wash-station-crafted");
        Assert.That(asset, Is.Not.Null);
        asset.DesiredWorldSize = new Vector2(-1f, 0f);
        Assert.That(AssetManifestValidator.Validate(manifest),
            Has.Some.Matches<AssetValidationIssue>(issue => issue.Code == AssetValidationCode.InvalidWorldScale));
    }

    [Test]
    public void ImportedWashShelvesStayOnTheApprovedLeftSideOfTheRoom()
    {
        var instance = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Models/WashCraft/room-finish"));
        try
        {
            float totalX = 0f;
            int count = 0;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                var materials = filter.GetComponent<Renderer>().sharedMaterials;
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                for (int submesh = 0; submesh < materials.Length; submesh++)
                    if (materials[submesh].name.Contains("Bottle"))
                        foreach (int index in mesh.GetTriangles(submesh))
                        { totalX += filter.transform.TransformPoint(vertices[index]).x; count++; }
            }
            Assert.That(count, Is.GreaterThan(0));
            Assert.That(totalX / count, Is.LessThan(-3f), "FBX handedness must not mirror the approved wash-area decoration.");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

}
