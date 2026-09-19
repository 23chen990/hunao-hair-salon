using System.Collections.Generic;
using HairSalon;
using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class AssetPipelineManifestTests
{
    private sealed class FakeResolver : IAssetResourceResolver
    {
        private readonly HashSet<string> _paths = new HashSet<string>();

        public FakeResolver(params string[] paths)
        {
            foreach (string path in paths) _paths.Add(path);
        }

        public bool Exists(string resourcePath)
            => resourcePath.StartsWith("builtin://") || _paths.Contains(resourcePath);
    }

    [Test]
    public void PackagedManifest_HasStableIdsAndPassesValidation()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();

        Assert.That(manifest.Assets.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(manifest.Find("furniture-cut-station-classic"), Is.Not.Null);
        Assert.That(AssetManifestValidator.Validate(manifest), Is.Empty);
    }

    [Test]
    public void Validator_ReportsDuplicateIdIllegalDirectionInvalidFootprintAndMissingAnchor()
    {
        AssetDefinition first = ValidAsset("furniture-test");
        AssetDefinition duplicate = ValidAsset("furniture-test");
        duplicate.Directions.Add("ceiling");
        duplicate.Footprint.Size = new Vector2(0f, -1f);
        duplicate.RequiredAnchors.Add("stylist-work");
        var manifest = new AssetManifest
        {
            Assets = new List<AssetDefinition> { first, duplicate }
        };

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            manifest, new FakeResolver("Imported/test"));

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.DuplicateAssetId));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.IllegalDirection));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidFootprint));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.MissingRequiredAnchor));
    }

    [Test]
    public void Validator_ReportsMissingResourceAndInvalidPivot()
    {
        AssetDefinition asset = ValidAsset("furniture-test");
        asset.ResourcePath = "Imported/missing";
        asset.Pivot = new Vector2(1.2f, -.1f);

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } }, new FakeResolver());

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.ResourceNotFound));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidPivot));
    }

    [Test]
    public void CandidateSingleDirectionAndBakedShadow_AreRepresentedByTheContract()
    {
        Assert.That(typeof(AssetDefinition).GetField("DisplayName"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("Category"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("DefaultOrientation"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("SourceHash"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("Visuals"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("VisualSize"), Is.Null,
            "DesiredWorldSize must be the only runtime visual scale source.");
        Assert.That(typeof(AssetDefinition).GetField("CandidateValidation"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("DesiredWorldSize"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("ScalePolicy"), Is.Not.Null);
        Assert.That(typeof(AssetDefinition).GetField("ImportProfile"), Is.Not.Null);
        Assert.That(typeof(AssetShadow).GetField("Mode"), Is.Not.Null);

        AssetDefinition asset = ValidAsset("furniture-candidate-right-wall");
        asset.Status = "candidate";
        asset.Directions = new List<string> { "right-wall" };
        asset.DefaultOrientation = "right-wall";
        asset.CandidateValidation = new AssetCandidateValidation
        {
            Role = "service-station",
            ServiceType = "wash",
            StationId = 0,
            TargetObjectName = "Wash Workstation 1",
            WorldPosition = new Vector3(9f, 0f, -.3f),
            HideObjectNames = new List<string>(),
            HideRadius = 0f
        };
        asset.DesiredWorldSize = new Vector2(4f, 4f);
        asset.ScalePolicy = "footprint-isometric";
        asset.ImportProfile = "production-2.5d-rendered";
        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } }, new FakeResolver("Imported/test"));

        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidStatus));
        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.IllegalDirection));
        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidCandidateValidation));
        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidWorldScale));
    }

    [Test]
    public void Validator_RejectsCandidateWithoutSceneValidationData()
    {
        AssetDefinition asset = ValidAsset("furniture-candidate-missing-placement");
        asset.Status = "candidate";

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } }, new FakeResolver("Imported/test"));

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x =>
            x.Code == AssetValidationCode.InvalidCandidateValidation));
    }

    [Test]
    public void Validator_RejectsCandidateWithoutOneAuthoritativeWorldScalePolicy()
    {
        AssetDefinition asset = ValidAsset("furniture-candidate-missing-scale");
        asset.Status = "candidate";
        asset.CandidateValidation = new AssetCandidateValidation
        {
            Role = "ordinary", WorldPosition = Vector3.zero, HideObjectNames = new List<string>()
        };

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } }, new FakeResolver("Imported/test"));

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidWorldScale));
    }

    [Test]
    public void NeedsReviewAsset_RemainsAValidCandidateWithAllProductionChecksEnabled()
    {
        AssetDefinition asset = ValidAsset("furniture-needs-review");
        asset.Status = "NEEDS-REVIEW";
        asset.DesiredWorldSize = new Vector2(4f, 4f);
        asset.ScalePolicy = "footprint-isometric";
        asset.ImportProfile = "production-2.5d-rendered";
        asset.CandidateValidation = new AssetCandidateValidation
        {
            Role = "service-station",
            ServiceType = "wash",
            StationId = 0,
            TargetObjectName = "Wash Workstation 1",
            WorldPosition = new Vector3(9f, 0f, -.3f),
            HideObjectNames = new List<string>()
        };

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } },
            new FakeResolver("Imported/test"));

        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidStatus));
        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidCandidateValidation));
        Assert.That(issues, Has.None.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidWorldScale));
    }

    [Test]
    public void CandidateValidationScene_HasADevelopmentOnlyRuntimeAndBuildEntry()
    {
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Type editorBuild = System.Type.GetType("BuildScript, Assembly-CSharp-Editor");
        System.Reflection.MethodInfo build = editorBuild?.GetMethod("BuildWebGLCandidate");

        Assert.That(runtime, Is.Not.Null);
        Assert.That(build, Is.Not.Null);
    }

    [Test]
    public void CandidateRegression_DoesNotConfigureHaircutOrderWhileCurrentNeedIsWash()
    {
        var model = new SalonGameModel();
        CustomerModel customer = model.Spawn(88201, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo shouldConfigure = runtime?.GetMethod("ShouldConfigureHaircutOrder");

        Assert.That(shouldConfigure, Is.Not.Null);
        Assert.That((bool)shouldConfigure.Invoke(null, new object[] { customer }), Is.False);
    }

    [Test]
    public void CandidateRegression_RemovesCleanTowelBeforeTransferringToHaircut()
    {
        var model = new SalonGameModel();
        CustomerModel customer = model.Spawn(88202, new List<ServiceType> { ServiceType.Wash, ServiceType.Cut });
        model.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.That(model.Assign(customer, 0), Is.True);
        model.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        CompleteWash(model, customer, WashAction.Shower, model.ServiceConfig.RinseDuration);
        CompleteWash(model, customer, WashAction.Shampoo, model.ServiceConfig.ShampooDuration);
        CompleteWash(model, customer, WashAction.Shower, model.ServiceConfig.RinseDuration);
        Assert.That(model.PerformQuickAction(customer, ActiveServiceAction.WrapTowel),
            Is.EqualTo(ServiceActionResult.QuickActionCompleted));
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo prepare = runtime?.GetMethod("PrepareHaircutTransfer");

        Assert.That(prepare, Is.Not.Null);
        Assert.That((bool)prepare.Invoke(null, new object[] { model, customer }), Is.True);
        Assert.That(model.GetServicePhysicalSnapshot(customer).IsTowelWrapped, Is.False);
        Assert.That(customer.CurrentNeed, Is.EqualTo(ServiceType.Cut));
    }

    [Test]
    public void CandidateRegression_CompletesExistingDryStepBeforeExpectingPayment()
    {
        var model = new SalonGameModel();
        CustomerModel customer = model.Spawn(88203, new List<ServiceType> { ServiceType.Dry });
        model.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.That(model.Assign(customer, 1), Is.True);
        model.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        model.SelectCustomer(customer);
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo completeDry = runtime?.GetMethod("CompleteDryStep");

        Assert.That(completeDry, Is.Not.Null);
        Assert.That((bool)completeDry.Invoke(null, new object[] { model, customer }), Is.True);
        Assert.That(customer.IsComplete, Is.True);
        Assert.That(customer.State, Is.EqualTo(CustomerState.Finished));
        Assert.That(model.Payments.Drops.Count, Is.EqualTo(1));
    }

    [Test]
    public void CandidateWorldArtwork_HasAlphaClippedDepthForFrontAndBackCharacterOcclusion()
    {
        Shader shader = Resources.Load<Shader>("SalonCandidateArtwork");

        Assert.That(shader, Is.Not.Null);
        Assert.That(shader.name, Is.EqualTo("HairSalon/CandidateArtwork"));
    }

    [Test]
    public void CandidateWorldArtwork_FlattensDepthToItsPivotSoWallsCannotSliceTheBillboard()
    {
        Shader shader = Resources.Load<Shader>("SalonCandidateArtwork");
        string path = UnityEditor.AssetDatabase.GetAssetPath(shader);
        string source = System.IO.File.ReadAllText(path);

        Assert.That(source, Does.Contain("originClip"));
        Assert.That(source, Does.Contain("output.vertex.z = originClip.z * output.vertex.w / originClip.w"));
    }

    [Test]
    public void CandidateWorldArtwork_DoesNotDependOnWebGLStrippedMeshCollider()
    {
        AssetDefinition asset = AssetManifestLoader.LoadFromResources().Find(
            "furniture-wash-station-vintage-right-wall");
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo build = runtime?.GetMethod("BuildArtwork");
        var root = new GameObject("Candidate artwork test");
        try
        {
            Assert.That(build, Is.Not.Null);
            build.Invoke(null, new object[] { root.transform, asset, "right-wall" });
            Assert.That(root.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<Collider>(), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CandidateWorldArtwork_UsesManifestWorldSizeAndFacesTheGameCamera()
    {
        AssetDefinition asset = AssetManifestLoader.LoadFromResources().Find(
            "furniture-wash-station-vintage-right-wall");
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo build = runtime?.GetMethod("BuildArtwork");
        Camera[] existingCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        string[] existingTags = new string[existingCameras.Length];
        for (int i = 0; i < existingCameras.Length; i++)
        {
            existingTags[i] = existingCameras[i].tag;
            existingCameras[i].tag = "Untagged";
        }
        var cameraObject = new GameObject("Candidate artwork camera", typeof(Camera));
        var root = new GameObject("Candidate billboard test");
        try
        {
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(43f, 0f, 0f);
            build.Invoke(null, new object[] { root.transform, asset, "right-wall" });
            Transform artwork = root.transform.Find("Artwork [unaltered right-wall]");

            Assert.That(artwork, Is.Not.Null);
            Assert.That(artwork.localScale.x, Is.EqualTo(asset.DesiredWorldSize.x).Within(.001f));
            Assert.That(artwork.localScale.y, Is.EqualTo(asset.DesiredWorldSize.y).Within(.001f));
            Assert.That(Quaternion.Angle(artwork.rotation, cameraObject.transform.rotation), Is.LessThan(.1f));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            for (int i = 0; i < existingCameras.Length; i++)
                if (existingCameras[i] != null) existingCameras[i].tag = existingTags[i];
        }
    }

    [Test]
    public void CandidateWorldArtwork_MeshOriginIsTheManifestPivotAndDepthBaseline()
    {
        AssetDefinition asset = AssetManifestLoader.LoadFromResources().Find(
            "furniture-magazine-rack-wood");
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.MethodInfo build = runtime?.GetMethod("BuildArtwork");
        Camera[] existingCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        string[] existingTags = new string[existingCameras.Length];
        for (int i = 0; i < existingCameras.Length; i++)
        {
            existingTags[i] = existingCameras[i].tag;
            existingCameras[i].tag = "Untagged";
        }
        var cameraObject = new GameObject("Pivot depth camera", typeof(Camera));
        var root = new GameObject("Pivot depth root");
        try
        {
            cameraObject.tag = "MainCamera";
            cameraObject.transform.rotation = Quaternion.Euler(43f, 0f, 0f);
            root.transform.position = new Vector3(3f, 0f, 6f);
            build.Invoke(null, new object[] { root.transform, asset, "free" });
            Transform artwork = root.transform.Find("Artwork [unaltered free]");
            Mesh mesh = artwork.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(Vector3.Distance(artwork.position, root.transform.position), Is.LessThan(.001f));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(mesh.bounds.min.x, Is.EqualTo(-.5f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
            for (int i = 0; i < existingCameras.Length; i++)
                if (existingCameras[i] != null) existingCameras[i].tag = existingTags[i];
        }
    }

    [Test]
    public void OrdinaryCandidate_BaselineStaysInFrontOfTheBackWallOcclusionPlane()
    {
        AssetDefinition asset = AssetManifestLoader.LoadFromResources().Find(
            "furniture-magazine-rack-wood");

        Assert.That(asset, Is.Not.Null);
        Assert.That(asset.CandidateValidation, Is.Not.Null);
        Assert.That(asset.CandidateValidation.WorldPosition.z, Is.LessThanOrEqualTo(5.75f),
            "The camera-facing artwork uses its floor pivot as the depth baseline; placing that " +
            "baseline closer to the back wall lets the opaque wall hide almost the entire PNG.");
    }

    [Test]
    public void CandidateAlignmentEvidence_WaitsForTheStylistToReachTheReboundAnchor()
    {
        System.Type runtime = System.Type.GetType(
            "HairSalon.AssetPipeline.CandidateAssetValidation, HairSalon.Runtime");
        System.Reflection.FieldInfo settle = runtime?.GetField("AlignmentSettleSeconds");

        Assert.That(settle, Is.Not.Null);
        Assert.That((float)settle.GetRawConstantValue(), Is.GreaterThanOrEqualTo(4f));
    }

    private static void CompleteWash(SalonGameModel model, CustomerModel customer, WashAction action, float duration)
    {
        Assert.That(model.BeginWashAction(customer, action), Is.True);
        Assert.That(model.TickActiveServiceAction(customer, duration), Is.True);
    }

    private static AssetDefinition ValidAsset(string id)
    {
        return new AssetDefinition
        {
            Id = id,
            Type = "furniture",
            ResourcePath = "Imported/test",
            Status = "approved",
            Directions = new List<string> { "back-wall", "right-wall" },
            Pivot = new Vector2(.5f, .1f),
            Footprint = new AssetArea { Center = Vector2.zero, Size = new Vector2(2f, 2f) },
            Collision = new AssetArea { Center = Vector2.zero, Size = Vector2.one },
            Shadow = new AssetShadow { Enabled = true, Size = new Vector2(1.5f, .8f), Opacity = .28f, Softness = .7f },
            InteractionAnchors = new List<AssetAnchor>
            {
                new AssetAnchor { Id = "customer-seat", Position = new Vector3(0f, 0f, -.8f), Facing = Vector3.forward }
            },
            RequiredAnchors = new List<string> { "customer-seat" },
            Sorting = new AssetSorting { Layer = "Default", Order = 5, DepthOffset = 0f }
        };
    }
}
