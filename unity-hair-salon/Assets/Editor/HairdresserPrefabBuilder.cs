using System.IO;
using HairSalon.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class HairdresserPrefabBuilder
{
    private const string RootFolder = "Assets/Resources/Characters";
    private const string PrefabPath = RootFolder + "/Hairdresser.prefab";
    private const string ControllerPath = RootFolder + "/Hairdresser.controller";
    private const string AnimationFolder = RootFolder + "/HairdresserAnimations";
    private const string PlaceholderFolder = RootFolder + "/HairdresserPlaceholder";
    private const string PlaceholderTexturePath = PlaceholderFolder + "/placeholder.png";
    private const string PresentationFolder = RootFolder + "/HairdresserPresentation";
    private const string ContactShadowTexturePath = PresentationFolder + "/contact-shadow.png";
    private const float VisualBaseY = 1.4422222f;

    [MenuItem("Hair Salon/Characters/Rebuild Hairdresser Framework")]
    public static void Rebuild()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(AnimationFolder);
        EnsureFolder(PlaceholderFolder);
        EnsureFolder(PresentationFolder);
        RemoveRejectedDraftAssets();

        Sprite placeholderSprite = BuildPlaceholderSprite();
        Sprite contactShadowSprite = BuildContactShadowSprite();
        AnimatorController controller = BuildAnimatorController();
        GameObject root = new GameObject("Hairdresser");
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            Hairdresser2_5DRig rig = root.AddComponent<Hairdresser2_5DRig>();
            HairdresserCharacter character = root.AddComponent<HairdresserCharacter>();

            Transform visualRoot = Node("Visual", root.transform);
            GameObject placeholder = new GameObject(
                "HairdresserSpriteVisual", typeof(SpriteRenderer), typeof(Hairdresser2DPresenter));
            placeholder.transform.SetParent(visualRoot, false);
            placeholder.transform.localPosition = new Vector3(0f, VisualBaseY, 0f);
            SpriteRenderer renderer = placeholder.GetComponent<SpriteRenderer>();
            renderer.sprite = placeholderSprite;
            renderer.sortingOrder = 20;
            ConfigureDirectionalPlaceholder(placeholder.GetComponent<Hairdresser2DPresenter>(), placeholderSprite);

            Transform grounding = Node("Grounding", root.transform);
            GameObject shadow = new GameObject("ContactShadow", typeof(SpriteRenderer));
            shadow.transform.SetParent(grounding, false);
            shadow.transform.localPosition = new Vector3(0f, .018f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpriteRenderer shadowRenderer = shadow.GetComponent<SpriteRenderer>();
            shadowRenderer.sprite = contactShadowSprite;
            shadowRenderer.sortingOrder = 2;

            Transform sockets = Node("Sockets", root.transform);
            Transform toolSocket = Node("RightHandToolSocket", sockets, new Vector3(.5f, 1.15f, 0f));

            SerializedObject serializedCharacter = new SerializedObject(character);
            serializedCharacter.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serializedCharacter.FindProperty("rightHandToolSocket").objectReferenceValue = toolSocket;
            serializedCharacter.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedRig = new SerializedObject(rig);
            serializedRig.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serializedRig.FindProperty("contactShadow").objectReferenceValue = shadowRenderer;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Hairdresser framework rebuilt at " + PrefabPath);
    }

    public static void RebuildFromCommandLine() => Rebuild();

    private static AnimatorController BuildAnimatorController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("State", AnimatorControllerParameterType.Int);
        controller.AddParameter("Direction", AnimatorControllerParameterType.Int);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        machine.name = "Hairdresser Reserved States";
        string[] states = { "Idle", "Walk", "CutHair", "DryHair", "WashHair" };
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = machine.AddState(states[i], new Vector3(250f, 35f + 65f * i));
            state.motion = BuildPlaceholderClip(states[i], i);
            if (i == 0) machine.defaultState = state;
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
            transition.duration = .08f;
            transition.AddCondition(AnimatorConditionMode.Equals, i, "State");
        }
        return controller;
    }

    private static AnimationClip BuildPlaceholderClip(string name, int stateIndex)
    {
        string path = AnimationFolder + "/" + name + ".anim";
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { name = name, frameRate = 12f };
        float duration = stateIndex == 1 ? .5f : .9f;
        float offset = stateIndex == 0 ? .015f : stateIndex == 1 ? .05f : .025f;
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, VisualBaseY),
            new Keyframe(duration * .5f, VisualBaseY + offset),
            new Keyframe(duration, VisualBaseY));
        clip.SetCurve("Visual/HairdresserSpriteVisual", typeof(Transform), "m_LocalPosition.y", curve);
        AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings { loopTime = true });
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static Sprite BuildPlaceholderSprite()
    {
        const int width = 64;
        const int height = 96;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color body = new Color(.38f, .42f, .47f, 1f);
        Color accent = new Color(.72f, .76f, .8f, 1f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        FillEllipse(pixels, width, height, 32, 69, 18, 18, accent);
        FillEllipse(pixels, width, height, 32, 41, 21, 28, body);
        FillEllipse(pixels, width, height, 20, 15, 8, 19, body);
        FillEllipse(pixels, width, height, 44, 15, 8, 19, body);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(PlaceholderTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(PlaceholderTexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 48f;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderTexturePath);
    }

    private static Sprite BuildContactShadowSprite()
    {
        const int width = 128;
        const int height = 64;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = (x + .5f - width * .5f) / (width * .5f);
            float dy = (y + .5f - height * .5f) / (height * .5f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.7f) * .34f;
            pixels[y * width + x] = new Color(.08f, .045f, .025f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(ContactShadowTexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(ContactShadowTexturePath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ContactShadowTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(ContactShadowTexturePath);
    }

    private static void FillEllipse(Color[] pixels, int width, int height,
        int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            float dx = (x - centerX) / (float)radiusX;
            float dy = (y - centerY) / (float)radiusY;
            if (dx * dx + dy * dy <= 1f) pixels[y * width + x] = color;
        }
    }

    private static void ConfigureDirectionalPlaceholder(Hairdresser2DPresenter presenter, Sprite sprite)
    {
        SerializedObject serialized = new SerializedObject(presenter);
        string[] sets =
        {
            "idle", "walk", "walkPassing", "walkOpposite",
            "cutHair", "dryHair", "washHair"
        };
        foreach (string setName in sets)
        {
            SerializedProperty directions = serialized.FindProperty(setName).FindPropertyRelative("directions");
            directions.arraySize = 8;
            for (int i = 0; i < 8; i++) directions.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Node(string name, Transform parent, Vector3 localPosition = default)
    {
        Transform node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.localPosition = localPosition;
        return node;
    }

    private static void RemoveRejectedDraftAssets()
    {
        string[] paths =
        {
            RootFolder + "/MainBarber.prefab",
            RootFolder + "/MainBarber.controller",
            RootFolder + "/MainBarberPlaceholder.controller",
            RootFolder + "/Animations",
            RootFolder + "/Materials",
            RootFolder + "/Tools",
            RootFolder + "/PlaceholderAnimations",
            RootFolder + "/PlaceholderMaterials"
        };
        foreach (string path in paths) AssetDatabase.DeleteAsset(path);
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
