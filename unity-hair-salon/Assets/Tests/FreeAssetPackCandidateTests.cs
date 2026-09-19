using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FreeAssetPackCandidateTests
{
    private const string FurnitureRoot = "Assets/ThirdParty/Kenney/FurnitureKit";
    private const string CharacterRoot = "Assets/ThirdParty/Kenney/BlockyCharacters";
    private const string KayKitRoot = "Assets/ThirdParty/KayKit/FurnitureBits";

    [Test]
    public void Candidate_UsesImportedKenneyModelsWithLocalCc0Evidence()
    {
        string[] requiredModels =
        {
            FurnitureRoot + "/Models/loungeDesignSofa.fbx",
            FurnitureRoot + "/Models/chairModernCushion.fbx",
            FurnitureRoot + "/Models/bathroomSink.fbx",
            FurnitureRoot + "/Models/bathroomMirror.fbx",
            FurnitureRoot + "/Models/kitchenBar.fbx",
            FurnitureRoot + "/Models/bookcaseOpen.fbx",
            FurnitureRoot + "/Models/pottedPlant.fbx",
            CharacterRoot + "/Models/character-a.fbx",
            CharacterRoot + "/Models/character-f.fbx",
            CharacterRoot + "/Models/character-k.fbx",
            KayKitRoot + "/Models/armchair_pillows.fbx",
            KayKitRoot + "/Models/couch_pillows.fbx",
            KayKitRoot + "/Models/cabinet_medium_decorated.fbx",
            KayKitRoot + "/Models/shelf_B_large_decorated.fbx",
            KayKitRoot + "/Models/table_low.fbx"
        };

        foreach (string path in requiredModels)
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null, path);

        AssertCc0License(FurnitureRoot + "/LICENSE.txt");
        AssertCc0License(CharacterRoot + "/LICENSE.txt");
        AssertCc0License(KayKitRoot + "/LICENSE.txt");
        Assert.That(File.ReadAllText("Assets/ThirdParty/Kenney/SOURCES.md"),
            Does.Contain("e67652d0932cee41683f74711c03d3e192a2af9979ef8e6b237711f5482d46b0"));
        Assert.That(File.ReadAllText("Assets/ThirdParty/Kenney/SOURCES.md"),
            Does.Contain("5e123859aa0c1598342b600c6db197024a1d63eb9ec531398b310725f589887e"));
        Assert.That(File.ReadAllText("Assets/ThirdParty/KayKit/SOURCES.md"),
            Does.Contain("e6d75f34c5545486b5a8f45f86209dbd49092f3e77ae23011bdb87edab89e7e4"));
    }

    [Test]
    public void Candidate_HasIndependentSceneRuntimeMarkerAndBuildEntry()
    {
        Type marker = Type.GetType("HairSalon.Candidates.FreeAssetCandidateMarker, HairSalon.Runtime");
        Type editorBuild = Type.GetType("FreeAssetCandidateBuild, Assembly-CSharp-Editor");

        Assert.That(marker, Is.Not.Null);
        Assert.That(editorBuild?.GetMethod("CreateScene"), Is.Not.Null);
        Assert.That(editorBuild?.GetMethod("BuildWebGL"), Is.Not.Null);
        Assert.That(editorBuild?.GetMethod("CaptureEditorEvidence"), Is.Not.Null);
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(
            "Assets/Scenes/FreeAssetPackCandidate.unity"), Is.Not.Null);
    }

    [Test]
    public void Candidate_FramesACompactTealSalonInsteadOfAnEmptyDefaultAssetRoom()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/FreeAssetPackCandidate.unity", OpenSceneMode.Single);
        GameObject root = GameObject.Find("Free Asset Pack Candidate");
        Assert.That(root, Is.Not.Null);

        Camera camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
        Assert.That(camera, Is.Not.Null);
        Assert.That(camera.orthographic, Is.True);
        Assert.That(camera.orthographicSize, Is.LessThanOrEqualTo(4.5f));

        Transform room = root.transform.Find("Actual Kenney Room Shell");
        Transform furniture = root.transform.Find("Actual Kenney Furniture");
        Transform characters = root.transform.Find("Actual Kenney Characters");
        Assert.That(room, Is.Not.Null);
        Assert.That(furniture, Is.Not.Null);
        Assert.That(characters, Is.Not.Null);
        Assert.That(furniture.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(18));
        int kayKitInstances = furniture.GetComponentsInChildren<Transform>(true).Count(item =>
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
            return source != null && AssetDatabase.GetAssetPath(source).StartsWith(KayKitRoot);
        });
        Assert.That(kayKitInstances, Is.GreaterThanOrEqualTo(8),
            "KayKit must be the primary visible furniture family, not merely downloaded beside the scene.");
        Assert.That(characters.childCount, Is.EqualTo(3));
        Assert.That(characters.GetComponentsInChildren<Renderer>(true).Length,
            Is.GreaterThanOrEqualTo(3));

        Renderer floor = room.GetComponentsInChildren<Renderer>(true)
            .First(item => item.gameObject.name.Contains("Floor"));
        int floorTileCount = room.GetComponentsInChildren<Renderer>(true)
            .Count(item => item.gameObject.name.Contains("Floor"));
        Assert.That(floorTileCount, Is.EqualTo(15),
            "The review frame should be a compact five-by-three salon slice, not a distant full-room test grid.");
        Color floorColor = floor.sharedMaterial.color;
        Assert.That(floorColor.g, Is.GreaterThan(floorColor.r),
            "The presentation floor should use the teal candidate material, not the bright default FBX material.");
    }

    private static void AssertCc0License(string path)
    {
        Assert.That(File.Exists(path), Is.True, path);
        string license = File.ReadAllText(path);
        Assert.That(license, Does.Contain("Creative Commons Zero"));
        Assert.That(license, Does.Contain("commercial"));
    }
}
