using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class AssetTestLabTests
{
    [Test]
    public void InspectMode_AutomaticallyFramesDifferentAssetsAtAReadableOccupancy()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());
            System.Type type = typeof(AssetTestLab);
            System.Reflection.MethodInfo setViewMode = type.GetMethod("SetViewMode");
            System.Reflection.PropertyInfo currentMode = type.GetProperty("CurrentViewMode");
            System.Reflection.PropertyInfo occupancy = type.GetProperty("CurrentInspectOccupancy");
            System.Reflection.PropertyInfo cameraSize = type.GetProperty("CurrentCameraOrthographicSize");
            Assert.That(setViewMode, Is.Not.Null);
            Assert.That(currentMode, Is.Not.Null);
            Assert.That(occupancy, Is.Not.Null);
            Assert.That(cameraSize, Is.Not.Null);

            Assert.That((bool)setViewMode.Invoke(lab, new object[] { "inspect" }), Is.True);
            Assert.That(lab.SelectAsset("furniture-magazine-rack-wood"), Is.True);
            Assert.That((string)currentMode.GetValue(lab), Is.EqualTo("inspect"));
            Assert.That((float)occupancy.GetValue(lab), Is.InRange(.45f, .70f));
            float rackCameraSize = (float)cameraSize.GetValue(lab);

            Assert.That(lab.SelectAsset("furniture-wash-station-vintage-right-wall"), Is.True);
            Assert.That((float)occupancy.GetValue(lab), Is.InRange(.45f, .70f));
            Assert.That((float)cameraSize.GetValue(lab), Is.GreaterThan(rackCameraSize));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ContextMode_UsesManifestWorldSizeAndShowsGameScaleReferences()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            AssetManifest manifest = AssetManifestLoader.LoadFromResources();
            lab.Initialize(manifest);
            Assert.That(lab.SelectAsset("furniture-magazine-rack-wood"), Is.True);
            System.Reflection.MethodInfo setViewMode = typeof(AssetTestLab).GetMethod("SetViewMode");
            System.Reflection.PropertyInfo currentMode = typeof(AssetTestLab).GetProperty("CurrentViewMode");
            System.Reflection.PropertyInfo currentSize = typeof(AssetTestLab).GetProperty("CurrentWorldSize");
            System.Reflection.FieldInfo desiredSize = typeof(AssetDefinition).GetField("DesiredWorldSize");
            Assert.That(setViewMode, Is.Not.Null);
            Assert.That(currentMode, Is.Not.Null);
            Assert.That(currentSize, Is.Not.Null);
            Assert.That(desiredSize, Is.Not.Null);

            Assert.That((bool)setViewMode.Invoke(lab, new object[] { "context" }), Is.True);

            AssetDefinition asset = manifest.Find("furniture-magazine-rack-wood");
            Assert.That((string)currentMode.GetValue(lab), Is.EqualTo("context"));
            Assert.That((Vector2)currentSize.GetValue(lab), Is.EqualTo((Vector2)desiredSize.GetValue(asset)));
            Assert.That(root.transform.Find("Lab Environment/Context References/Scale Reference Hairdresser"), Is.Not.Null);
            Assert.That(root.transform.Find("Lab Environment/Context References/Grid Tile 0 0"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ContextScaleComparison_ChangesOnlyArtworkWorldSizeAndKeepsCameraAndReferencesFixed()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            AssetManifest manifest = AssetManifestLoader.LoadFromResources();
            lab.Initialize(manifest);
            Assert.That(lab.SelectAsset("furniture-wash-station-vintage-right-wall"), Is.True);
            Assert.That(lab.SetViewMode("context"), Is.True);
            Camera camera = root.GetComponentInChildren<Camera>();
            Transform character = root.transform.Find(
                "Lab Environment/Context References/Scale Reference Hairdresser");
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            Vector3 characterPosition = character.position;
            Vector2 baseline = manifest.Find("furniture-wash-station-vintage-right-wall").WorldSize;

            System.Reflection.MethodInfo setScale = typeof(AssetTestLab).GetMethod("SetContextScaleMultiplier");
            System.Reflection.PropertyInfo multiplier = typeof(AssetTestLab).GetProperty("CurrentContextScaleMultiplier");
            Assert.That(setScale, Is.Not.Null);
            Assert.That(multiplier, Is.Not.Null);
            Assert.That((bool)setScale.Invoke(lab, new object[] { .8f }), Is.True);

            Assert.That((float)multiplier.GetValue(lab), Is.EqualTo(.8f).Within(.001f));
            Assert.That(lab.CurrentWorldSize.x, Is.EqualTo(baseline.x * .8f).Within(.001f));
            Assert.That(lab.CurrentWorldSize.y, Is.EqualTo(baseline.y * .8f).Within(.001f));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Quaternion.Angle(camera.transform.rotation, cameraRotation), Is.LessThan(.001f));
            Assert.That(character.position, Is.EqualTo(characterPosition));
            Assert.That(root.transform.Find("Lab Environment/Context References/Grid Tile 0 0").localScale,
                Is.EqualTo(new Vector3(.97f, .12f, .97f)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PixelMode_ExposesTheRuntimeTextureAtOneTexturePixelPerScreenPixel()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());
            Assert.That(lab.SelectAsset("furniture-wash-station-vintage-right-wall"), Is.True);
            System.Reflection.MethodInfo setViewMode = typeof(AssetTestLab).GetMethod("SetViewMode");
            System.Reflection.PropertyInfo pixelTexture = typeof(AssetTestLab).GetProperty("PixelPreviewTexture");
            System.Reflection.PropertyInfo pixelScale = typeof(AssetTestLab).GetProperty("PixelPreviewScale");
            Assert.That(setViewMode, Is.Not.Null);
            Assert.That(pixelTexture, Is.Not.Null);
            Assert.That(pixelScale, Is.Not.Null);

            Assert.That((bool)setViewMode.Invoke(lab, new object[] { "pixel" }), Is.True);

            Texture2D texture = (Texture2D)pixelTexture.GetValue(lab);
            Assert.That(texture, Is.Not.Null);
            Assert.That((float)pixelScale.GetValue(lab), Is.EqualTo(1f));
            Assert.That(texture.width, Is.EqualTo(1254));
            Assert.That(texture.height, Is.EqualTo(1254));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

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

    [Test]
    public void Initialize_RightWallOnlyAssetUsesItsRealDirectionAndTextureWithoutRotatingTheArtwork()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var asset = new AssetDefinition
            {
                Id = "furniture-real-png",
                Type = "furniture",
                ResourcePath = "Characters/HairdresserPlaceholder/placeholder",
                Status = "candidate",
                Directions = new System.Collections.Generic.List<string> { "right-wall" },
                Pivot = new Vector2(.5f, 0f),
                DesiredWorldSize = new Vector2(2f, 2f),
                ScalePolicy = "footprint-isometric",
                ImportProfile = "production-2.5d-rendered",
                Footprint = new AssetArea { Center = Vector2.zero, Size = new Vector2(2f, 2f) },
                Collision = new AssetArea { Center = Vector2.zero, Size = new Vector2(1.8f, 1.8f) },
                Shadow = new AssetShadow { Enabled = false },
                InteractionAnchors = new System.Collections.Generic.List<AssetAnchor>(),
                RequiredAnchors = new System.Collections.Generic.List<string>(),
                Sorting = new AssetSorting { Layer = "Default", Order = 3 },
                CandidateValidation = new AssetCandidateValidation
                {
                    Role = "ordinary",
                    WorldPosition = Vector3.zero,
                    HideObjectNames = new System.Collections.Generic.List<string>()
                }
            };
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(new AssetManifest
            {
                Assets = new System.Collections.Generic.List<AssetDefinition> { asset }
            });

            Assert.That(lab.CurrentDirection, Is.EqualTo("right-wall"));
            Transform visual = root.transform.Find("Current Asset/Visual");
            Assert.That(visual.localEulerAngles, Is.EqualTo(Vector3.zero));
            SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.texture.name, Is.EqualTo("placeholder"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SelectAsset_OpensTheRequestedCandidateInsteadOfTheFirstManifestEntry()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            AssetManifest manifest = AssetManifestLoader.LoadFromResources();
            lab.Initialize(manifest);
            System.Reflection.MethodInfo select = typeof(AssetTestLab).GetMethod("SelectAsset");

            Assert.That(select, Is.Not.Null, "Asset lab needs a stable asset-id selection API for browser evidence.");
            select.Invoke(lab, new object[] { manifest.Assets[manifest.Assets.Count - 1].Id });
            Assert.That(lab.CurrentAssetId, Is.EqualTo(manifest.Assets[manifest.Assets.Count - 1].Id));
            Assert.That(lab.CurrentDirection, Is.EqualTo(manifest.Assets[manifest.Assets.Count - 1].Directions[0]));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Initialize_UsesAReadableOrthographicInspectionCamera()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());
            Camera camera = root.GetComponentInChildren<Camera>();

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(camera.orthographicSize, Is.LessThanOrEqualTo(4.25f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DiagnosticModes_SeparateArtworkAnchorsAndCollisionEvidence()
    {
        var root = new GameObject("Asset Lab");
        try
        {
            var lab = root.AddComponent<AssetTestLab>();
            lab.Initialize(AssetManifestLoader.LoadFromResources());
            System.Reflection.MethodInfo setMode = typeof(AssetTestLab).GetMethod("SetDiagnosticMode");
            Assert.That(setMode, Is.Not.Null);

            setMode.Invoke(lab, new object[] { "anchors" });
            Assert.That(root.transform.Find("Current Asset/Anchors").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("Current Asset/Footprint").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Current Asset/Collision").gameObject.activeSelf, Is.False);

            setMode.Invoke(lab, new object[] { "areas" });
            Assert.That(root.transform.Find("Current Asset/Anchors").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Current Asset/Footprint").gameObject.activeSelf, Is.True);
            Assert.That(root.transform.Find("Current Asset/Collision").gameObject.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
