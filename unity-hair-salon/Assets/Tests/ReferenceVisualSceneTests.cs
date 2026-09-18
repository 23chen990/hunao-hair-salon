using System;
using System.Linq;
using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class ReferenceVisualSceneTests
{
    [Serializable]
    private sealed class BaselineFile
    {
        public int SchemaVersion;
        public string ApprovedReferencePath;
        public ReferenceCamera Camera;
        public ReferenceGround Ground;
        public ReferenceWalls Walls;
        public ReferencePlayer Player;
        public ReferenceOverlay Overlay;
        public ReferenceVisualMetrics VisualMetrics;
        public ReferenceDensity Density;
        public ReferenceCategory[] Categories;
    }

    [Serializable] private sealed class ReferenceCamera
    {
        public string Projection;
        public float YawDegrees;
        public float PitchDegrees;
        public float OrthographicSize;
        public Vector2Int Viewport;
    }

    [Serializable] private sealed class ReferenceGround
    {
        public string VisualMode;
        public int VisibleTileColumns;
        public int VisibleTileRows;
    }
    [Serializable] private sealed class ReferenceWalls
    {
        public float VisualHeight;
        public float VisualThickness;
        public float CollisionHeight;
        public float CollisionThickness;
    }
    [Serializable] private sealed class ReferencePlayer
    {
        public string ReferenceCharacterDescription;
        public float VisualScale;
    }
    [Serializable] private sealed class ReferenceOverlay
    {
        public string ResourcePath;
        public string SourceSha256;
        public string FitMode;
        public float DefaultOpacity;
    }
    [Serializable] private sealed class PixelBounds
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }
    [Serializable] private sealed class ReferenceVisualMetrics
    {
        public Vector2Int SourceImageSize;
        public PixelBounds RoomBounds;
        public PixelBounds StandingPlayerBounds;
        public float PlayerScreenHeightRatio;
        public float RoomScreenWidthRatio;
        public float RoomScreenHeightRatio;
        public float WashToPlayerHeightRatio;
        public float WashFootprintToVisibleFloorRatio;
    }
    [Serializable] private sealed class ReferenceDensity
    {
        public string DefaultState;
        public float LowDensityFloorOccupancy;
        public float TargetDensityFloorOccupancy;
    }
    [Serializable] private sealed class ReferenceCategory
    {
        public string Id;
        public string Status;
        public Vector3 ReferenceWorldSize;
        public Vector2 ReferenceFootprint;
        public float ExpectedPlayerRelativeHeight;
        public Vector2 ExpectedTileOccupancy;
    }

    [Test]
    public void Baseline_LocksApprovedReferenceCameraGroundPlayerAndCategoryRelationships()
    {
        TextAsset text = Resources.Load<TextAsset>("AssetPipeline/reference-visual-baseline");
        Assert.That(text, Is.Not.Null);
        BaselineFile baseline = JsonUtility.FromJson<BaselineFile>(text.text);

        Assert.That(baseline.SchemaVersion, Is.EqualTo(2));
        Assert.That(baseline.ApprovedReferencePath,
            Is.EqualTo("Docs/VisualReferences/salon-overview-visual-reference.png"));
        Assert.That(baseline.Camera.Projection, Is.EqualTo("orthographic"));
        Assert.That(baseline.Camera.YawDegrees, Is.InRange(20f, 50f));
        Assert.That(baseline.Camera.PitchDegrees, Is.InRange(20f, 40f));
        Assert.That(baseline.Camera.Viewport, Is.EqualTo(new Vector2Int(844, 390)));
        Assert.That(baseline.Ground.VisualMode, Is.EqualTo("continuous-reference-floor"));
        Assert.That(baseline.Ground.VisibleTileColumns, Is.GreaterThanOrEqualTo(45));
        Assert.That(baseline.Ground.VisibleTileRows, Is.GreaterThanOrEqualTo(25));
        Assert.That(baseline.Walls.VisualHeight, Is.LessThan(baseline.Walls.CollisionHeight));
        Assert.That(baseline.Walls.VisualThickness, Is.LessThan(baseline.Walls.CollisionThickness));
        Assert.That(baseline.Player.ReferenceCharacterDescription, Does.Contain("standing stylist"));
        Assert.That(baseline.Player.VisualScale, Is.GreaterThan(1f));
        Assert.That(baseline.Overlay.ResourcePath,
            Is.EqualTo("VisualReferences/approved-salon-effect-reference"));
        Assert.That(baseline.Overlay.SourceSha256,
            Is.EqualTo("b358fd3f38b6aac7400357623c01af24e8c27e919b44e0cc4b8820e8cc1265af"));
        Assert.That(baseline.Overlay.FitMode, Is.EqualTo("cover-center"));
        Assert.That(baseline.Overlay.DefaultOpacity, Is.InRange(0f, 1f));

        Assert.That(baseline.VisualMetrics.SourceImageSize, Is.EqualTo(new Vector2Int(1672, 941)));
        Assert.That(baseline.VisualMetrics.RoomBounds.width, Is.GreaterThan(1500));
        Assert.That(baseline.VisualMetrics.StandingPlayerBounds.height, Is.InRange(230, 300));
        Assert.That(baseline.VisualMetrics.PlayerScreenHeightRatio, Is.InRange(.28f, .38f));
        Assert.That(baseline.VisualMetrics.RoomScreenWidthRatio, Is.InRange(.9f, 1f));
        Assert.That(baseline.VisualMetrics.RoomScreenHeightRatio, Is.InRange(.75f, 1f));
        Assert.That(baseline.VisualMetrics.WashToPlayerHeightRatio, Is.InRange(.8f, 1.2f));
        Assert.That(baseline.VisualMetrics.WashFootprintToVisibleFloorRatio, Is.GreaterThan(0f));
        Assert.That(baseline.Density.DefaultState, Is.EqualTo("target-density"));
        Assert.That(baseline.Density.LowDensityFloorOccupancy,
            Is.LessThan(baseline.Density.TargetDensityFloorOccupancy));

        ReferenceCategory furniture = baseline.Categories.Single(item => item.Id == "standard-furniture");
        ReferenceCategory station = baseline.Categories.Single(item => item.Id == "service-station");
        ReferenceCategory wash = baseline.Categories.Single(item => item.Id == "wash-station");
        Assert.That(furniture.Status, Is.EqualTo("visual-calibration-candidate"));
        Assert.That(furniture.Id, Is.EqualTo("standard-furniture"));
        Assert.That(furniture.ReferenceWorldSize.x, Is.GreaterThan(0f));
        Assert.That(furniture.ExpectedPlayerRelativeHeight, Is.InRange(.4f, .8f));
        Assert.That(station.Status, Is.EqualTo("engineering-debug-only"));
        Assert.That(station.ReferenceFootprint.x, Is.GreaterThan(1f));
        Assert.That(station.ExpectedTileOccupancy.x, Is.GreaterThan(0f));
        Assert.That(station.ExpectedTileOccupancy.y, Is.GreaterThan(0f));
        Assert.That(wash.Status, Is.EqualTo("NEEDS-REVIEW"));
        Assert.That(wash.ReferenceWorldSize, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ReferenceScene_HasAnIndependentRuntimeSceneAndWebGLBuildEntry()
    {
        Type runtime = Type.GetType("HairSalon.AssetPipeline.ReferenceVisualScene, HairSalon.Runtime");
        Type editorBuild = Type.GetType("BuildScript, Assembly-CSharp-Editor");

        Assert.That(runtime, Is.Not.Null);
        Assert.That(editorBuild?.GetMethod("BuildWebGLReferenceVisual"), Is.Not.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ReferenceVisualScene.unity"), Is.Not.Null);
    }

    [Test]
    public void ReferenceScene_BuildsTheMinimumApprovalWorldWithoutSalonGameplay()
    {
        Type runtime = Type.GetType("HairSalon.AssetPipeline.ReferenceVisualScene, HairSalon.Runtime");
        Assert.That(runtime, Is.Not.Null);
        var root = new GameObject("Reference Visual Scene Test");
        try
        {
            Component component = root.AddComponent(runtime);
            var build = runtime.GetMethod("BuildNow");
            Assert.That(build, Is.Not.Null);
            build.Invoke(component, null);

            Assert.That(root.transform.Find("Reference World/Ground"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Ground/Visual Floor"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Ground/Technical Grid Debug"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Ground/Technical Grid Debug").gameObject.activeSelf, Is.False);
            Assert.That(root.transform.Find("Reference World/Visual Walls"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Collision Walls"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Approved Player"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Real Ordinary Furniture"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Wash Station NEEDS-REVIEW"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Low Density Context"), Is.Not.Null);
            Assert.That(root.transform.Find("Reference World/Target Density Context"), Is.Not.Null);
            Assert.That(root.transform.Find("Overlay Calibration Canvas/Approved Reference Overlay"), Is.Not.Null);
            Assert.That(root.transform.Find("Overlay Calibration Canvas/Approved Reference Overlay").GetComponent<RawImage>(), Is.Not.Null);
            Assert.That(root.GetComponent<SalonDemo>(), Is.Null,
                "Reference Scene is an approval environment, not another gameplay scene.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
