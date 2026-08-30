using System;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    public static class AssetManifestLoader
    {
        public const string DefaultResourcePath = "AssetPipeline/asset-manifest";

        public static AssetManifest LoadFromResources(string resourcePath = DefaultResourcePath)
        {
            TextAsset source = Resources.Load<TextAsset>(resourcePath);
            if (source == null)
                throw new InvalidOperationException($"Asset manifest not found at Resources/{resourcePath}.json");
            AssetManifest manifest = JsonUtility.FromJson<AssetManifest>(source.text);
            if (manifest == null || manifest.Assets == null)
                throw new InvalidOperationException($"Asset manifest at {resourcePath} is invalid JSON.");
            return manifest;
        }
    }
}
