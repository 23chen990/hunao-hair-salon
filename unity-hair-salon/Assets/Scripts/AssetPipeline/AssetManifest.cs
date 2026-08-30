using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    [Serializable]
    public sealed class AssetArea
    {
        public Vector2 Center;
        public Vector2 Size = Vector2.one;
    }

    [Serializable]
    public sealed class AssetShadow
    {
        public bool Enabled = true;
        public Vector2 Size = new Vector2(1.5f, .75f);
        public Vector2 Offset;
        [Range(0f, 1f)] public float Opacity = .28f;
        [Range(0f, 1f)] public float Softness = .7f;
    }

    [Serializable]
    public sealed class AssetAnchor
    {
        public string Id;
        public Vector3 Position;
        public Vector3 Facing = Vector3.forward;
    }

    [Serializable]
    public sealed class AssetSorting
    {
        public string Layer = "Default";
        public int Order;
        public float DepthOffset;
        public bool SortByWorldZ = true;
    }

    [Serializable]
    public sealed class AssetDefinition
    {
        public string Id;
        public string Type;
        public string ResourcePath;
        public string Status;
        public List<string> Directions = new List<string>();
        public Vector2 Pivot = new Vector2(.5f, 0f);
        public AssetArea Footprint;
        public AssetArea Collision;
        public AssetShadow Shadow;
        public List<AssetAnchor> InteractionAnchors = new List<AssetAnchor>();
        public List<string> RequiredAnchors = new List<string>();
        public AssetSorting Sorting;
    }

    [Serializable]
    public sealed class AssetManifest
    {
        public int SchemaVersion = 1;
        public string BuildVersion = "0.8.0-pipeline";
        public List<AssetDefinition> Assets = new List<AssetDefinition>();

        public AssetDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || Assets == null) return null;
            return Assets.Find(asset => asset != null && string.Equals(asset.Id, id, StringComparison.Ordinal));
        }
    }
}
