using System;
using UnityEngine;

namespace HairSalon.CutStations
{
    public static class CutStationManifestLoader
    {
        public const string DefaultResourcePath = "CutStations/cut-stations";

        public static CutStationManifest LoadFromResources(string resourcePath = DefaultResourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) throw new InvalidOperationException($"Cut station manifest not found at Resources/{resourcePath}.json");
            CutStationManifest manifest = JsonUtility.FromJson<CutStationManifest>(asset.text);
            if (manifest == null) throw new InvalidOperationException($"Cut station manifest at {resourcePath} is invalid JSON.");
            return manifest;
        }
    }
}
