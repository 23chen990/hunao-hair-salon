using System;
using System.Collections.Generic;
using HairSalon.AssetPipeline;
using UnityEditor;
using UnityEngine;

public sealed class Production2D5TexturePolicy : AssetPostprocessor
{
    public const string ProfileId = "production-2.5d-rendered";
    private static bool _suspendAutomaticPolicy;

    private void OnPreprocessTexture()
    {
        if (_suspendAutomaticPolicy || !IsManagedAsset(assetPath)) return;
        Apply((TextureImporter)assetImporter);
    }

    public static void ApplyAllBatch()
    {
        ApplyAll();
        AssetDatabase.SaveAssets();
        Debug.Log("[TexturePolicy] Production 2.5D rendered asset profile applied.");
    }

    public static void ApplyLegacyABBatch()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();
        _suspendAutomaticPolicy = true;
        try
        {
            foreach (AssetDefinition asset in manifest.Assets)
            {
                if (asset?.ImportProfile != ProfileId) continue;
                var importer = AssetImporter.GetAtPath(AssetPath(asset)) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Texture importer is missing: " + AssetPath(asset));
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 1;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.ToNearest;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "WebGL", overridden = false, maxTextureSize = 2048,
                    format = TextureImporterFormat.Automatic, compressionQuality = 50
                });
                importer.SaveAndReimport();
            }
        }
        finally { _suspendAutomaticPolicy = false; }
        Debug.Log("[TexturePolicy] Legacy A/B import settings applied temporarily.");
    }

    public static void ApplyAll()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();
        foreach (AssetDefinition asset in manifest.Assets)
        {
            if (asset?.ImportProfile != ProfileId) continue;
            string path = AssetPath(asset);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Texture importer is missing: " + path);
            Apply(importer);
            importer.SaveAndReimport();
        }
    }

    public static IReadOnlyList<string> ValidateAll()
    {
        var issues = new List<string>();
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();
        foreach (AssetDefinition asset in manifest.Assets)
        {
            if (asset?.ImportProfile != ProfileId) continue;
            string path = AssetPath(asset);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                issues.Add("missing importer: " + path);
                continue;
            }
            TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
            if (importer.textureType != TextureImporterType.Default ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency || importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Bilinear || importer.anisoLevel != 1 ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.maxTextureSize != 2048 || importer.npotScale != TextureImporterNPOTScale.None ||
                !importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Clamp ||
                !webgl.overridden || webgl.maxTextureSize != 2048 || webgl.format != TextureImporterFormat.RGBA32)
                issues.Add("profile mismatch: " + path);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || asset.Source == null || texture.width != asset.Source.Width || texture.height != asset.Source.Height)
                issues.Add("runtime pixel size differs from source: " + path);
        }
        return issues;
    }

    private static bool IsManagedAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/Resources/Imported/", StringComparison.Ordinal))
            return false;
        AssetManifest manifest;
        try { manifest = AssetManifestLoader.LoadFromResources(); }
        catch { return false; }
        string resourcePath = path.Substring("Assets/Resources/".Length);
        int extension = resourcePath.LastIndexOf('.');
        if (extension >= 0) resourcePath = resourcePath.Substring(0, extension);
        return manifest.Assets.Exists(asset => asset != null && asset.ImportProfile == ProfileId &&
            string.Equals(asset.ResourcePath, resourcePath, StringComparison.Ordinal));
    }

    private static string AssetPath(AssetDefinition asset)
        => "Assets/Resources/" + asset.ResourcePath + ".png";

    private static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.streamingMipmaps = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.isReadable = false;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "WebGL",
            overridden = true,
            maxTextureSize = 2048,
            resizeAlgorithm = TextureResizeAlgorithm.Mitchell,
            format = TextureImporterFormat.RGBA32,
            compressionQuality = 100,
            crunchedCompression = false,
            allowsAlphaSplitting = false
        });
    }
}
