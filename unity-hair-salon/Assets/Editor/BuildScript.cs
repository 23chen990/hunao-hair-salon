using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using HairSalon.AssetPipeline;

public static class BuildScript
{
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("SalonDemoRuntime");
        root.AddComponent<SalonDemo>();
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
        foreach (AssetDefinition asset in manifest.Assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Id))
                throw new System.Exception("Manifest contains an asset without a stable Id.");
        }
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
