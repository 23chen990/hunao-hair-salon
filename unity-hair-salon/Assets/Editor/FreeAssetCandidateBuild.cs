using System;
using System.IO;
using System.Linq;
using HairSalon.Candidates;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class FreeAssetCandidateBuild
{
    private const string ScenePath = "Assets/Scenes/FreeAssetPackCandidate.unity";
    private const string FurnitureRoot = "Assets/ThirdParty/Kenney/FurnitureKit/Models/";
    private const string CharacterRoot = "Assets/ThirdParty/Kenney/BlockyCharacters/Models/";
    private const string KayKitRoot = "Assets/ThirdParty/KayKit/FurnitureBits/Models/";
    private const int EvidenceWidth = 844;
    private const int EvidenceHeight = 390;

    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.51f, 0.53f);
        RenderSettings.fog = false;

        var root = new GameObject("Free Asset Pack Candidate");
        root.AddComponent<FreeAssetCandidateMarker>();
        CreateCamera(root.transform);
        CreateLighting(root.transform);

        Transform room = new GameObject("Actual Kenney Room Shell").transform;
        room.SetParent(root.transform, false);
        Material floorMaterial = GetOrCreateCandidateMaterial(
            "CandidateFloorTeal", new Color(0.18f, 0.49f, 0.50f));
        Material wallMaterial = GetOrCreateCandidateMaterial(
            "CandidateWallPeach", new Color(0.88f, 0.61f, 0.50f));
        CreateRoomShell(room, floorMaterial, wallMaterial);

        Transform furniture = new GameObject("Actual Kenney Furniture").transform;
        furniture.SetParent(root.transform, false);
        CreateFurniture(furniture);

        Transform characters = new GameObject("Actual Kenney Characters").transform;
        characters.SetParent(root.transform, false);
        CreateCharacters(characters);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[FreeAssetCandidate] Created independent scene: " + ScenePath);
    }

    public static void BuildWebGL()
    {
        ValidateCandidateAssets();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/WebGLFreeAssetCandidate",
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Free asset candidate WebGL build failed: " + report.summary.totalErrors);
        Debug.Log($"[FreeAssetCandidate] WebGL build succeeded ({report.summary.totalSize} bytes)");
    }

    public static void CaptureEditorEvidence()
    {
        ValidateCandidateAssets();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Camera camera = Object.FindAnyObjectByType<Camera>();
        if (camera == null)
            throw new Exception("Free asset candidate camera is missing.");

        string outputDirectory = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../Builds/PipelineEvidence/FreeAssetCandidate"));
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory,
            "actual-free-asset-packs-editor-844x390.png");
        var target = new RenderTexture(EvidenceWidth, EvidenceHeight, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(EvidenceWidth, EvidenceHeight, TextureFormat.RGB24, false);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, EvidenceWidth, EvidenceHeight), 0, 0);
            pixels.Apply();
            if (!HasVisualContent(pixels))
                throw new Exception("Free asset candidate evidence is blank or flat.");
            File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);
        }
        Debug.Log("[FreeAssetCandidate] Editor evidence captured: " + outputPath);
    }

    public static bool HasVisualContent(Texture2D texture)
    {
        if (texture == null) return false;
        Color32[] pixels = texture.GetPixels32();
        int minimum = 765;
        int maximum = 0;
        int step = Mathf.Max(1, pixels.Length / 1024);
        for (int index = 0; index < pixels.Length; index += step)
        {
            int value = pixels[index].r + pixels[index].g + pixels[index].b;
            minimum = Mathf.Min(minimum, value);
            maximum = Mathf.Max(maximum, value);
        }
        return maximum - minimum > 35;
    }

    private static void CreateCamera(Transform parent)
    {
        var cameraObject = new GameObject("Candidate Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(-8.7f, 8.6f, -10.1f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.45f, 0.25f));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3.65f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 80f;
        camera.backgroundColor = new Color(0.055f, 0.09f, 0.11f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.allowHDR = false;
        camera.allowMSAA = true;
    }

    private static void CreateLighting(Transform parent)
    {
        var lightObject = new GameObject("Soft Directional Light", typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.91f, 0.82f);
        light.intensity = 0.92f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.42f;
    }

    private static void CreateRoomShell(Transform parent, Material floorMaterial, Material wallMaterial)
    {
        for (int x = -4; x <= 4; x += 2)
        {
            for (int z = -2; z <= 2; z += 2)
            {
                InstantiateModel(FurnitureRoot + "floorFull.fbx", $"Floor {x} {z}", parent,
                    new Vector3(x, -0.08f, z), 0f, new Vector3(2.18f, 0f, 2.18f), floorMaterial);
            }
            InstantiateModel(FurnitureRoot + "wall.fbx", $"Back Wall {x}", parent,
                new Vector3(x, 0f, 3.05f), 0f, new Vector3(2.20f, 2.15f, 0f), wallMaterial);
        }

        for (int z = -2; z <= 2; z += 2)
        {
            InstantiateModel(FurnitureRoot + "wall.fbx", $"Right Wall {z}", parent,
                new Vector3(5.02f, 0f, z), 90f, new Vector3(0f, 2.15f, 2.20f), wallMaterial);
        }
    }

    private static void CreateFurniture(Transform parent)
    {
        // Back-left wash-area proxies: every visible component is an unmodified Kenney FBX.
        CreateWashProxy(parent, new Vector3(-3.25f, 0f, 1.35f), "Wash Proxy A");
        CreateWashProxy(parent, new Vector3(-1.15f, 0f, 1.35f), "Wash Proxy B");

        CreateStylingStation(parent, new Vector3(0.65f, 0f, -0.25f), "Styling Station A");
        CreateStylingStation(parent, new Vector3(2.65f, 0f, -0.25f), "Styling Station B");

        InstantiateModel(KayKitRoot + "couch_pillows.fbx", "Waiting Sofa (KayKit)", parent,
            new Vector3(-3.25f, 0f, -1.68f), 22f, new Vector3(3.1f, 1.48f, 1.32f));
        InstantiateModel(KayKitRoot + "table_low.fbx", "Waiting Coffee Table (KayKit)", parent,
            new Vector3(-1.55f, 0f, -1.78f), 22f, new Vector3(1.35f, 0.55f, 0.9f));
        InstantiateModel(KayKitRoot + "book_set.fbx", "Waiting Magazines (KayKit)", parent,
            new Vector3(-1.55f, 0.55f, -1.78f), 22f, new Vector3(0.48f, 0.16f, 0.35f));

        InstantiateModel(KayKitRoot + "cabinet_medium_decorated.fbx", "Reception Counter (KayKit)", parent,
            new Vector3(3.55f, 0f, -1.65f), -18f, new Vector3(2.25f, 1.52f, 1.12f));
        InstantiateModel(FurnitureRoot + "computerScreen.fbx", "Reception Screen", parent,
            new Vector3(3.42f, 1.31f, -1.47f), 162f, new Vector3(0.55f, 0.5f, 0.24f));

        InstantiateModel(KayKitRoot + "shelf_B_large_decorated.fbx", "Product Shelf (KayKit)", parent,
            new Vector3(4.15f, 0f, 1.78f), 180f, new Vector3(1.52f, 2.38f, 0.72f));
        InstantiateModel(KayKitRoot + "cabinet_medium_decorated.fbx", "Towel Cabinet (KayKit)", parent,
            new Vector3(2.75f, 0f, 2.12f), 180f, new Vector3(1.35f, 1.55f, 0.62f));
        InstantiateModel(KayKitRoot + "cactus_medium_A.fbx", "Corner Plant (KayKit)", parent,
            new Vector3(4.22f, 0f, 0.28f), 0f, new Vector3(1.18f, 1.95f, 1.18f));
        InstantiateModel(KayKitRoot + "cactus_small_A.fbx", "Reception Plant (KayKit)", parent,
            new Vector3(4.08f, 1.3f, -1.58f), 0f, new Vector3(0.48f, 0.62f, 0.48f));
        InstantiateModel(KayKitRoot + "rug_rectangle_stripes_A.fbx", "Styling Rug (KayKit)", parent,
            new Vector3(1.65f, 0.015f, -0.34f), 0f, new Vector3(3.65f, 0.08f, 2.15f));
        InstantiateModel(KayKitRoot + "lamp_standing.fbx", "Standing Lamp (KayKit)", parent,
            new Vector3(-4.15f, 0f, -0.25f), 0f, new Vector3(0.72f, 1.82f, 0.72f));
    }

    private static void CreateWashProxy(Transform parent, Vector3 center, string name)
    {
        var proxy = new GameObject(name + " (NOT PRODUCTION WASH STATION)").transform;
        proxy.SetParent(parent, false);
        InstantiateModel(KayKitRoot + "armchair_pillows.fbx", name + " Armchair Proxy (KayKit)", proxy,
            center + new Vector3(0f, 0f, -0.12f), 180f, new Vector3(1.45f, 1.42f, 1.52f));
        InstantiateModel(FurnitureRoot + "bathroomSink.fbx", name + " Sink", proxy,
            center + new Vector3(0f, 0f, 0.98f), 180f, new Vector3(1.42f, 1.18f, 0.88f));
        InstantiateModel(KayKitRoot + "table_small.fbx", name + " Side Table (KayKit)", proxy,
            center + new Vector3(-0.92f, 0f, 0.42f), 0f, new Vector3(0.58f, 0.72f, 0.58f));
    }

    private static void CreateStylingStation(Transform parent, Vector3 center, string name)
    {
        var station = new GameObject(name).transform;
        station.SetParent(parent, false);
        InstantiateModel(KayKitRoot + "armchair_pillows.fbx", name + " Chair (KayKit)", station,
            center, 180f, new Vector3(1.22f, 1.42f, 1.22f));
        InstantiateModel(KayKitRoot + "table_small.fbx", name + " Cabinet (KayKit)", station,
            center + new Vector3(0f, 0f, 1.18f), 0f, new Vector3(1.2f, 0.82f, 0.62f));
        InstantiateModel(FurnitureRoot + "bathroomMirror.fbx", name + " Mirror", station,
            center + new Vector3(0f, 0.86f, 1.3f), 180f, new Vector3(0.95f, 1.25f, 0.28f));
    }

    private static void CreateCharacters(Transform parent)
    {
        InstantiateCharacter(CharacterRoot + "character-f.fbx", "Hairdresser Candidate", parent,
            new Vector3(1.52f, 0f, -0.25f), 210f, 1.72f);
        InstantiateCharacter(CharacterRoot + "character-k.fbx", "Waiting Customer Candidate", parent,
            new Vector3(-3.05f, 0f, -1.08f), 34f, 1.62f);
        InstantiateCharacter(CharacterRoot + "character-a.fbx", "Reception Customer Candidate", parent,
            new Vector3(2.55f, 0f, -1.42f), 322f, 1.78f);
    }

    private static GameObject InstantiateCharacter(
        string path, string name, Transform parent, Vector3 position, float yaw, float height)
    {
        GameObject instance = InstantiateModel(path, name, parent, position, yaw,
            new Vector3(0f, height, 0f));
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(item => item.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
        if (clip != null)
            clip.SampleAnimation(instance, Mathf.Min(clip.length * 0.15f, 0.2f));
        Animator animator = instance.GetComponent<Animator>();
        if (animator != null)
            animator.enabled = false;
        AlignToGround(instance, position);
        return instance;
    }

    private static GameObject InstantiateModel(
        string path, string name, Transform parent, Vector3 position, float yaw, Vector3 targetSize,
        Material overrideMaterial = null)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new Exception("Required Kenney model is missing: " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, true);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (overrideMaterial != null)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = overrideMaterial;
        }
        FitUniformly(instance, targetSize);
        AlignToGround(instance, position);
        return instance;
    }

    private static void FitUniformly(GameObject instance, Vector3 targetSize)
    {
        Bounds bounds = CalculateBounds(instance);
        float factor = float.PositiveInfinity;
        if (targetSize.x > 0f && bounds.size.x > 0.0001f)
            factor = Mathf.Min(factor, targetSize.x / bounds.size.x);
        if (targetSize.y > 0f && bounds.size.y > 0.0001f)
            factor = Mathf.Min(factor, targetSize.y / bounds.size.y);
        if (targetSize.z > 0f && bounds.size.z > 0.0001f)
            factor = Mathf.Min(factor, targetSize.z / bounds.size.z);
        if (float.IsInfinity(factor) || factor <= 0f)
            throw new Exception("Cannot fit model with empty target size: " + instance.name);
        instance.transform.localScale *= factor;
    }

    private static void AlignToGround(GameObject instance, Vector3 target)
    {
        Bounds bounds = CalculateBounds(instance);
        instance.transform.position += new Vector3(
            target.x - bounds.center.x,
            target.y - bounds.min.y,
            target.z - bounds.center.z);
    }

    private static Bounds CalculateBounds(GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            throw new Exception("Imported model has no renderer: " + instance.name);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static Material GetOrCreateCandidateMaterial(string name, Color color)
    {
        const string folder = "Assets/ThirdParty/Kenney/CandidateMaterials";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/ThirdParty/Kenney", "CandidateMaterials");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new Exception("Unity Standard shader is unavailable for the candidate material.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.08f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ValidateCandidateAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new Exception("Free asset candidate scene is missing. Run CreateScene first.");
        if (!File.Exists("Assets/ThirdParty/Kenney/FurnitureKit/LICENSE.txt") ||
            !File.Exists("Assets/ThirdParty/Kenney/BlockyCharacters/LICENSE.txt"))
            throw new Exception("Kenney CC0 license evidence is missing.");
    }
}
