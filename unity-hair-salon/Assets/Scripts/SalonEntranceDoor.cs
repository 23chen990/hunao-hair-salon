using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared geometry and customer waypoints for the main left-wall door.</summary>
public static class SalonEntranceDoor
{
    public const string RoomAssetId = "room-entrance-door-left";
    public const float WallX = -10.73f;
    public const float DoorZ = -4.15f;
    public const float OpeningMinZ = -4.95f;
    public const float OpeningMaxZ = -3.35f;
    public const float OpeningWidth = OpeningMaxZ - OpeningMinZ;
    public const float DoorHeight = 2.3f;
    public const float OutsideX = -11.35f;
    public const float OutsideSpawnX = -13.0f;
    public const float OutsideExitX = -13.25f;
    // Crafted left cutaway wall (tools/blender/build_wash_craft.py): it stays
    // low so the overview camera can see the queue and wash area behind it.
    public const float WallHeight = .67f;
    public const float WallThickness = .24f;
    public const float WallMinZ = -5.5f;
    public const float WallMaxZ = 7.5f;
    public const float CorniceMinZ = -5.6f;
    public const float CorniceMaxZ = 7.6f;
    // Procedural fallback wall (washCraft=original) is split around the same opening.
    public const float FallbackWallMinZ = -5.85f;
    public const float FallbackWallMaxZ = 7.85f;

    public static readonly Vector3 DoorInside = new Vector3(-10.15f, 1.05f, DoorZ);
    public static readonly Vector3 DoorOutside = new Vector3(OutsideX, 1.05f, DoorZ);
    public static readonly Vector3 OutsideSpawn = new Vector3(OutsideSpawnX, 1.05f, DoorZ);
    public static readonly Vector3 OutsideExit = new Vector3(OutsideExitX, 1.05f, DoorZ);

    public static Vector3 InsidePoint(float y) => new Vector3(DoorInside.x, y, DoorZ);
    public static Vector3 OutsidePoint(float y) => new Vector3(DoorOutside.x, y, DoorZ);
    public static Vector3 OutsideExitPoint(float y) => new Vector3(OutsideExit.x, y, DoorZ);

    /// <summary>
    /// Selects the authored left side wall, oak skirting and cornice, while
    /// leaving floor, foundation and rear wall islands untouched.
    /// </summary>
    public static bool IsOldLeftWallFace(Bounds face)
    {
        Vector3 c = face.center;
        return c.x <= -10.45f && c.y >= -.01f && c.z <= 7.035f;
    }

    public static IReadOnlyList<string> RequiredMaterialNames()
    {
        var mesh = new SalonWashAnnexBuilder.BoxMeshBuilder();
        AddLeftWallWithDoor(mesh);
        AddDoorGround(mesh);
        mesh.Build("probe", out string[] names);
        return names;
    }

    /// <summary>Builds the rebuilt left wall, visible door frame and exterior mat.</summary>
    public static GameObject Build(Transform parent, Func<string, Material> material)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        var root = new GameObject("Salon Entrance Door");
        root.transform.SetParent(parent, false);
        var mesh = new SalonWashAnnexBuilder.BoxMeshBuilder();
        AddLeftWallWithDoor(mesh);
        AddDoorGround(mesh);
        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh.Build("Left wall with entrance door", out string[] names);
        var renderer = root.AddComponent<MeshRenderer>();
        var materials = new Material[names.Length];
        for (int i = 0; i < names.Length; i++) materials[i] = material(names[i]);
        renderer.sharedMaterials = materials;
        return root;
    }

    private static void AddLeftWallWithDoor(SalonWashAnnexBuilder.BoxMeshBuilder mesh)
    {
        const float half = WallThickness * .5f;
        mesh.Box(new Vector3(WallX - half, 0f, WallMinZ),
            new Vector3(WallX + half, WallHeight, OpeningMinZ), "Plaster");
        mesh.Box(new Vector3(WallX - half, 0f, OpeningMaxZ),
            new Vector3(WallX + half, WallHeight, WallMaxZ), "Plaster");

        // Inside oak panels keep the crafted 0.43 m rhythm; the ones that
        // would cross the opening are dropped.
        for (int j = 0; j < 30; j++)
        {
            float z = -5.22f + j * .43f;
            if (z + .211f > OpeningMinZ && z - .211f < OpeningMaxZ) continue;
            mesh.Box(new Vector3(WallX + .10f, .015f, z - .211f),
                new Vector3(WallX + .22f, .585f, z + .211f), "Oak" + (j % 7));
        }
        mesh.Box(new Vector3(WallX - .23f, WallHeight - .08f, CorniceMinZ),
            new Vector3(WallX + .23f, WallHeight + .08f, OpeningMinZ), "Porcelain");
        mesh.Box(new Vector3(WallX - .23f, WallHeight - .08f, OpeningMaxZ),
            new Vector3(WallX + .23f, WallHeight + .08f, CorniceMaxZ), "Porcelain");

        // A contrasting frame makes the opening legible in the overview camera
        // without introducing a new art asset.
        mesh.Box(new Vector3(WallX - .2f, 0f, OpeningMinZ - .14f),
            new Vector3(WallX + .2f, DoorHeight, OpeningMinZ), "OakDark");
        mesh.Box(new Vector3(WallX - .2f, 0f, OpeningMaxZ),
            new Vector3(WallX + .2f, DoorHeight, OpeningMaxZ + .14f), "OakDark");
        mesh.Box(new Vector3(WallX - .24f, DoorHeight - .12f, OpeningMinZ - .2f),
            new Vector3(WallX + .24f, DoorHeight + .08f, OpeningMaxZ + .2f), "OakLight");
    }

    private static void AddDoorGround(SalonWashAnnexBuilder.BoxMeshBuilder mesh)
    {
        mesh.Box(new Vector3(OutsideExitX - .15f, -.04f, OpeningMinZ - .35f),
            new Vector3(WallX + .06f, .015f, OpeningMaxZ + .35f), "Grout");
        mesh.Box(new Vector3(OutsideX - .20f, .005f, OpeningMinZ - .25f),
            new Vector3(DoorInside.x + .08f, .035f, OpeningMaxZ + .25f), "Porcelain");
    }
}
