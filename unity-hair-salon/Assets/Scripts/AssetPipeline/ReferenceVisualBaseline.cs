using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    [Serializable]
    public sealed class ReferenceVisualBaseline
    {
        public int SchemaVersion;
        public string ApprovedReferencePath;
        public ReferenceVisualCamera Camera;
        public ReferenceVisualGround Ground;
        public ReferenceVisualWalls Walls;
        public ReferenceVisualPlayer Player;
        public ReferenceVisualShadow Shadow;
        public ReferenceVisualDensity Density;
        public ReferenceVisualOverlay Overlay;
        public ReferenceVisualMetrics VisualMetrics;
        public List<ReferenceVisualCategory> Categories = new List<ReferenceVisualCategory>();

        public ReferenceVisualCategory FindCategory(string id)
            => Categories?.Find(item => item != null && item.Id == id);
    }

    [Serializable] public sealed class ReferenceVisualCamera
    {
        public string Projection;
        public float YawDegrees;
        public float PitchDegrees;
        public float OrthographicSize;
        public Vector2Int Viewport;
        public Vector3 Target;
    }

    [Serializable] public sealed class ReferenceVisualGround
    {
        public string VisualMode;
        public float WorldWidth;
        public float WorldDepth;
        public int VisibleTileColumns;
        public int VisibleTileRows;
        public float BaseHeight;
        public Color FloorColor;
        public Color GroutColor;
    }

    [Serializable] public sealed class ReferenceVisualWalls
    {
        public float VisualHeight;
        public float VisualThickness;
        public float CollisionHeight;
        public float CollisionThickness;
        public float LowerTrimHeight;
    }

    [Serializable] public sealed class ReferenceVisualPlayer
    {
        public float WorldHeight;
        public float VisualScale;
        public Vector3 Position;
        public string ReferenceCharacterDescription;
    }

    [Serializable] public sealed class ReferenceVisualShadow
    {
        public Vector2 Direction;
        public float Softness;
        public float Opacity;
    }

    [Serializable] public sealed class ReferenceVisualDensity
    {
        public string DefaultState;
        public float LowDensityFloorOccupancy;
        public float TargetDensityFloorOccupancy;
        public float TargetFloorCoverage;
        public float TargetObjectsPer100Tiles;
    }

    [Serializable] public sealed class ReferenceVisualOverlay
    {
        public string ResourcePath;
        public string SourceSha256;
        public string FitMode;
        public float DefaultOpacity;
    }

    [Serializable] public sealed class ReferencePixelBounds
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable] public sealed class ReferenceVisualMetrics
    {
        public Vector2Int SourceImageSize;
        public ReferencePixelBounds RoomBounds;
        public ReferencePixelBounds StandingPlayerBounds;
        public float PlayerScreenHeightRatio;
        public float RoomScreenWidthRatio;
        public float RoomScreenHeightRatio;
        public float WashToPlayerHeightRatio;
        public float WashFootprintToVisibleFloorRatio;
    }

    [Serializable] public sealed class ReferenceVisualCategory
    {
        public string Id;
        public string ReferenceAssetId;
        public string Status;
        public Vector3 ReferenceWorldSize;
        public Vector2 ReferenceFootprint;
        public float ExpectedPlayerRelativeHeight;
        public Vector2 ExpectedTileOccupancy;
        public Vector2 InteractionClearance;
    }

    public static class ReferenceVisualBaselineLoader
    {
        public const string ResourcePath = "AssetPipeline/reference-visual-baseline";

        public static ReferenceVisualBaseline Load()
        {
            TextAsset text = Resources.Load<TextAsset>(ResourcePath);
            if (text == null) throw new InvalidOperationException("Reference visual baseline is missing.");
            ReferenceVisualBaseline baseline = JsonUtility.FromJson<ReferenceVisualBaseline>(text.text);
            if (baseline == null || baseline.SchemaVersion != 2 || baseline.Camera == null ||
                baseline.Ground == null || baseline.Walls == null || baseline.Player == null ||
                baseline.Shadow == null || baseline.Density == null || baseline.Overlay == null ||
                baseline.VisualMetrics == null || baseline.Categories == null)
                throw new InvalidOperationException("Reference visual baseline is incomplete.");
            return baseline;
        }
    }
}
