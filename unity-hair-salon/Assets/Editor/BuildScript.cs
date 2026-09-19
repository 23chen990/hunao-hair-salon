using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using HairSalon.AssetPipeline;

public static class BuildScript
{
    private const int EvidenceWidth = 844;
    private const int EvidenceHeight = 390;
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("SalonDemoRuntime");
        root.AddComponent<SalonDemo>();
        root.AddComponent<DemoWashAreaVisualIntegration>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HairSalonDemo.unity");
        AssetDatabase.SaveAssets();
    }

    public static void BuildMac()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/HairSalonDemo.unity" },
            locationPathName = "Builds/HairSalonDemo.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Build failed: " + report.summary.totalErrors + " errors");
    }

    public static void CreateAssetTestScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Asset Test Lab", typeof(AssetTestLab));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/AssetTestLab.unity");
        var scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/HairSalonDemo.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AssetTestLab.unity", true)
        };
        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
    }

    public static void CreateCandidateValidationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("SalonCandidateRuntime");
        root.AddComponent<SalonDemo>();
        root.AddComponent<CandidateAssetValidation>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/CandidateAssetValidation.unity");
        AssetDatabase.SaveAssets();
    }

    public static void CreateReferenceVisualScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("ReferenceVisualRuntime", typeof(ReferenceVisualScene));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ReferenceVisualScene.unity");
        AssetDatabase.SaveAssets();
    }

    public static void ValidatePipeline()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();
        var issues = AssetManifestValidator.Validate(manifest);
        if (issues.Count > 0)
            throw new System.Exception("Asset manifest validation failed: " + issues[0]);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/HairSalonDemo.unity") == null)
            throw new System.Exception("Startup scene is missing: Assets/Scenes/HairSalonDemo.unity");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/AssetTestLab.unity") == null)
            throw new System.Exception("Asset test scene is missing: Assets/Scenes/AssetTestLab.unity");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/CandidateAssetValidation.unity") == null)
            throw new System.Exception("Candidate validation scene is missing: Assets/Scenes/CandidateAssetValidation.unity");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/ReferenceVisualScene.unity") == null)
            throw new System.Exception("Reference visual scene is missing: Assets/Scenes/ReferenceVisualScene.unity");
        if (AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/AssetPipeline/reference-visual-baseline.json") == null)
            throw new System.Exception("Reference visual baseline is missing.");
        foreach (AssetDefinition asset in manifest.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Id))
                throw new System.Exception("Manifest contains an asset without a stable Id.");
        }
        var textureIssues = Production2D5TexturePolicy.ValidateAll();
        if (textureIssues.Count > 0)
            throw new System.Exception("Production 2.5D texture policy validation failed: " + textureIssues[0]);
        Debug.Log($"[Pipeline] Startup, resources and manifest checks passed for {manifest.Assets.Count} assets.");
    }

    public static void BuildWebGLDemo()
    {
        ValidatePipeline();
        BuildWebGL("Builds/WebGLDemo", "Assets/Scenes/HairSalonDemo.unity");
    }

    public static void BuildWebGLAssetLab()
    {
        ValidatePipeline();
        BuildWebGL("Builds/WebGLAssetLab", "Assets/Scenes/AssetTestLab.unity");
    }

    public static void BuildWebGLAssetLabLegacyImportAB()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();
        var issues = AssetManifestValidator.Validate(manifest);
        if (issues.Count > 0) throw new System.Exception("A/B manifest validation failed: " + issues[0]);
        BuildWebGL("Builds/WebGLAssetLabABLegacy", "Assets/Scenes/AssetTestLab.unity");
    }

    public static void BuildWebGLCandidate()
    {
        ValidatePipeline();
        BuildWebGL("Builds/WebGLCandidate", "Assets/Scenes/CandidateAssetValidation.unity");
    }

    public static void BuildWebGLReferenceVisual()
    {
        ValidatePipeline();
        BuildWebGL("Builds/WebGLReferenceVisual", "Assets/Scenes/ReferenceVisualScene.unity");
    }

    public static void CaptureAssetLabEditorEvidence()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/AssetTestLab.unity", OpenSceneMode.Single);
        AssetTestLab lab = Object.FindFirstObjectByType<AssetTestLab>();
        if (lab == null) throw new System.Exception("AssetTestLab component is missing.");
        lab.Initialize(AssetManifestLoader.LoadFromResources());
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/PipelineEvidence/Round2_1"));
        Directory.CreateDirectory(output);
        CaptureAssetLab(lab, "furniture-magazine-rack-wood", "inspect", output);
        CaptureAssetLab(lab, "furniture-magazine-rack-wood", "context", output);
        CaptureAssetLab(lab, "furniture-wash-station-vintage-right-wall", "inspect", output);
        CaptureAssetLab(lab, "furniture-wash-station-vintage-right-wall", "context", output);
        Debug.Log("[Pipeline] Unity Editor Game View evidence captured: " + output);
    }

    private static void CaptureAssetLab(AssetTestLab lab, string assetId, string view, string output)
    {
        if (!lab.SelectAsset(assetId) || !lab.SetViewMode(view))
            throw new System.Exception("Cannot prepare editor evidence for " + assetId + " / " + view);
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null) throw new System.Exception("Asset Lab camera is missing.");
        var target = new RenderTexture(EvidenceWidth, EvidenceHeight, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(EvidenceWidth, EvidenceHeight, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, EvidenceWidth, EvidenceHeight), 0, 0);
            pixels.Apply();
            if (!HasVisualContent(pixels))
                throw new System.Exception("Unity Editor evidence frame is flat; run capture with a graphics device (do not use -nographics).");
            string path = Path.Combine(output, $"editor-gameview-{assetId}-{view}-844x390.png");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
    }

    public static bool HasVisualContent(Texture2D texture)
    {
        if (texture == null) return false;
        Color32[] pixels = texture.GetPixels32();
        int minimum = 765;
        int maximum = 0;
        int step = Mathf.Max(1, pixels.Length / 512);
        for (int index = 0; index < pixels.Length; index += step)
        {
            int value = pixels[index].r + pixels[index].g + pixels[index].b;
            minimum = Mathf.Min(minimum, value);
            maximum = Mathf.Max(maximum, value);
        }
        return maximum - minimum > 20;
    }

    private static void BuildWebGL(string output, string scene)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception($"WebGL build failed for {scene}: {report.summary.totalErrors} errors");
        Debug.Log($"[Pipeline] WebGL build succeeded: {output} ({report.summary.totalSize} bytes)");
    }
}
