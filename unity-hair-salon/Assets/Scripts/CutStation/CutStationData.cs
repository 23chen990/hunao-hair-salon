using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon.CutStations
{
    public enum CutStationVisualType
    {
        BuiltinProcedural = 0,
        Sprite2D = 1,
        PrefabOrModel = 2
    }

    public enum CutStationOrientation
    {
        BackWall = 0,
        RightWall = 1
    }

    [Serializable]
    public sealed class CutStationVisual
    {
        public CutStationVisualType Type;
        public string ResourcePath;
        public Vector3 DefaultSize = Vector3.one;
        public float DefaultScale = 1f;
    }

    [Serializable]
    public sealed class CutStationRect
    {
        public Vector2 Center;
        public Vector2 Size = Vector2.one;
    }

    [Serializable]
    public sealed class CutStationAnchors
    {
        public Vector3 CustomerSeat;
        public Vector3 CustomerFacing = Vector3.forward;
        public Vector3 StylistWork;
        public Vector3 StylistFacing = Vector3.forward;
        public Vector3 Queue;
        public Vector3 Tool;
        public Vector3 ServiceVfx;
    }

    [Serializable]
    public sealed class CutStationSorting
    {
        public string SortingLayer = "Default";
        public int SortingOrder;
        public float DepthOffset;
    }

    [Serializable]
    public sealed class CutStationDebugDisplay
    {
        public bool ShowFootprint = true;
        public bool ShowCollision = true;
        public bool ShowAnchors = true;
        public float MarkerSize = .18f;
        public Color FootprintColor = new Color(.1f, .75f, 1f, .3f);
        public Color CollisionColor = new Color(1f, .2f, .2f, .35f);
        public Color AnchorColor = new Color(.25f, 1f, .35f, .9f);
    }

    [Serializable]
    public sealed class CutStationDefinition
    {
        public string AssetId;
        public string Type = "CutStation";
        public CutStationVisual Visual;
        public CutStationRect Footprint;
        public CutStationRect Collision;
        public CutStationAnchors Anchors;
        public List<CutStationOrientation> LegalOrientations = new List<CutStationOrientation>();
        public CutStationSorting Sorting;
        public CutStationDebugDisplay Debug;
    }

    [Serializable]
    public sealed class CutStationManifest
    {
        public List<CutStationDefinition> Stations = new List<CutStationDefinition>();

        public CutStationDefinition Find(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId) || Stations == null) return null;
            return Stations.Find(station => station != null && station.AssetId == assetId);
        }
    }

    public static class CutStationDefaults
    {
        public static CutStationManifest CreateBuiltinManifest()
        {
            return new CutStationManifest
            {
                Stations = new List<CutStationDefinition> { CreateBuiltinDefinition() }
            };
        }

        public static CutStationDefinition CreateBuiltinDefinition()
        {
            return new CutStationDefinition
            {
                AssetId = "cut-station-classic-poc",
                Type = "CutStation",
                Visual = new CutStationVisual
                {
                    Type = CutStationVisualType.BuiltinProcedural,
                    ResourcePath = CutStationBuiltinVisuals.ClassicPocPath,
                    DefaultSize = new Vector3(2.4f, 2.65f, 1.75f),
                    DefaultScale = 1f
                },
                Footprint = new CutStationRect
                {
                    Center = new Vector2(0f, -.45f),
                    Size = new Vector2(2.45f, 2.65f)
                },
                Collision = new CutStationRect
                {
                    Center = new Vector2(0f, .18f),
                    Size = new Vector2(1.8f, 1.08f)
                },
                Anchors = new CutStationAnchors
                {
                    CustomerSeat = new Vector3(0f, .52f, -.42f),
                    CustomerFacing = Vector3.forward,
                    StylistWork = new Vector3(0f, 0f, -1.4f),
                    StylistFacing = Vector3.forward,
                    Queue = new Vector3(2.65f, 0f, -1.15f),
                    Tool = new Vector3(1.08f, 1.02f, -.2f),
                    ServiceVfx = new Vector3(0f, 2.05f, -.42f)
                },
                LegalOrientations = new List<CutStationOrientation>
                {
                    CutStationOrientation.BackWall,
                    CutStationOrientation.RightWall
                },
                Sorting = new CutStationSorting
                {
                    SortingLayer = "Default",
                    SortingOrder = 5,
                    DepthOffset = 0f
                },
                Debug = new CutStationDebugDisplay()
            };
        }
    }

    public static class CutStationBuiltinVisuals
    {
        public const string ClassicPocPath = "builtin://cut-station/classic-poc";

        public static bool Exists(string resourcePath)
            => string.Equals(resourcePath, ClassicPocPath, StringComparison.Ordinal);
    }
}
