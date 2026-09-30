using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed geometry of the second wash room grown behind the right wall
/// (product decision 2026-09-30: grow outside the shop, first area = wash).
/// Values are world metres on the same axes as the crafted room finish.
/// </summary>
public static class SalonWashAnnexLayout
{
    public const string PadId = "expansion-pad-wash-annex";
    public const string RoomAssetId = "room-annex-wash-2";
    public const int StationId = 4;

    /// <summary>Room floor edge; the old right wall stood just beyond it.</summary>
    public const float OpeningX = 10.35f;
    public const float OldWallX = 10.73f;
    /// <summary>Inner face of the low annex front wall panels.</summary>
    public const float InteriorMinZ = 1.05f;
    public const float InteriorMaxZ = 7.3f;
    /// <summary>Inner face of the annex right wall panels.</summary>
    public const float InteriorMaxX = 15.7f;
    public const float OuterWallX = 15.95f;
    public const float FrontWallZ = .83f;
    public const float BackWallZ = 7.43f;
    public const float TallWallHeight = 3.48f;
    public const float LowWallHeight = .67f;
    public const float OldWallCutMaxZ = 7.035f;

    /// <summary>
    /// Customers leave the main service lane between the second haircut chair
    /// (collision ends near x=7.1) and the corner plant (starts near x=9.0).
    /// </summary>
    public const float PortalX = 8.4f;
    public const float LaneZ = 2.3f;
    public const float ServiceLaneZ = -1.35f;
    public const float LeavingLaneZ = -.90f;

    public static readonly Vector3 WashBedPosition = new Vector3(13.2f, .35f, 4.6f);
    public static readonly Vector3 WashCustomerAnchorPosition = WashBedPosition + new Vector3(0f, 1.4f, -.6f);
    public static readonly Vector3 WashPlayerAnchorPosition = WashBedPosition + new Vector3(-1.55f, -.35f, -.55f);
    public static readonly Vector3 PadPosition = new Vector3(9.1f, .2f, 3.9f);
    public static readonly Vector3 PlantPosition = new Vector3(15.05f, .25f, 6.55f);

    /// <summary>Room plus annex floor before the body radius is removed.</summary>
    public static Rect WalkableBounds => Rect.MinMaxRect(-10.35f, -5.15f, InteriorMaxX, InteriorMaxZ);

    public static bool Contains(Vector3 worldPoint) => worldPoint.x >= OpeningX + .25f;

    /// <summary>
    /// Selects the old right wall faces in the crafted finish: the plaster
    /// wall, its oak panels and cornice. The foundation (y below zero), floor
    /// tiles (centres at x&lt;10.2) and the back wall (faces from z=7.085) are
    /// kept. The last side panel (z 7.039..7.461) stays as the rear door jamb;
    /// the panel in front of it ends at z=7.031.
    /// </summary>
    public static bool IsOldRightWallFace(Bounds face)
    {
        Vector3 c = face.center;
        return c.x >= 10.45f && c.y >= -.01f && c.z <= OldWallCutMaxZ;
    }

    /// <summary>Joystick blockers that exist only while the annex is open.</summary>
    public static void AddOpenObstacles(List<Rect> obstacles, float bodyRadius)
    {
        if (obstacles == null) return;
        // Outside ground in front of the annex's low front wall.
        obstacles.Add(Rect.MinMaxRect(OpeningX - bodyRadius, -6f, InteriorMaxX + 1f, InteriorMinZ + bodyRadius));
        // Front and rear door jambs (the old side panels at z=.80 and z=7.25).
        obstacles.Add(Rect.MinMaxRect(10.51f - bodyRadius, .589f - bodyRadius, 10.63f + bodyRadius, 1.011f + bodyRadius));
        obstacles.Add(Rect.MinMaxRect(10.51f - bodyRadius, 7.039f - bodyRadius, 10.63f + bodyRadius, 7.461f + bodyRadius));
    }
}

/// <summary>Removes whole connected face islands from an authored mesh.</summary>
public static class SalonRoomMeshCutter
{
    public static Mesh RemoveIslands(Mesh source, Func<Bounds, bool> remove, out int removedIslands)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (remove == null) throw new ArgumentNullException(nameof(remove));
        if (!source.isReadable) throw new InvalidOperationException(source.name + " must be CPU readable to cut the annex opening.");

        Vector3[] vertices = source.vertices;
        int[] parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        var submeshes = new int[source.subMeshCount][];
        for (int s = 0; s < submeshes.Length; s++)
        {
            int[] triangles = source.GetTriangles(s);
            submeshes[s] = triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Union(parent, triangles[t], triangles[t + 1]);
                Union(parent, triangles[t], triangles[t + 2]);
            }
        }

        var islandBounds = new Dictionary<int, Bounds>();
        foreach (int[] triangles in submeshes)
            foreach (int index in triangles)
            {
                int root = Find(parent, index);
                if (islandBounds.TryGetValue(root, out Bounds bounds))
                {
                    bounds.Encapsulate(vertices[index]);
                    islandBounds[root] = bounds;
                }
                else islandBounds[root] = new Bounds(vertices[index], Vector3.zero);
            }

        var removed = new HashSet<int>();
        foreach (var pair in islandBounds)
            if (remove(pair.Value)) removed.Add(pair.Key);
        removedIslands = removed.Count;

        Mesh result = UnityEngine.Object.Instantiate(source);
        result.name = source.name + " (annex opening)";
        for (int s = 0; s < submeshes.Length; s++)
        {
            int[] triangles = submeshes[s];
            var kept = new List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (removed.Contains(Find(parent, triangles[t]))) continue;
                kept.Add(triangles[t]);
                kept.Add(triangles[t + 1]);
                kept.Add(triangles[t + 2]);
            }
            result.SetTriangles(kept, s, false);
        }
        result.RecalculateBounds();
        return result;
    }

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }
        return index;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int rootA = Find(parent, a);
        int rootB = Find(parent, b);
        if (rootA != rootB) parent[rootB] = rootA;
    }
}

/// <summary>
/// Builds the annex shell with the crafted room's own materials so the new
/// room reads as the same finish: plaster, oak panels, porcelain cornice and
/// the staggered teal tile grid continued from the main floor.
/// </summary>
public static class SalonWashAnnexBuilder
{
    public const string RootName = "Wash Annex Finish";

    // Blender room() grid: tiles .724 x .365 on a .74 x .38 pitch, odd rows offset .37.
    private const float TileStartX = -10.48f;
    private const float TileStartZ = -5.48f;
    private const float TilePitchX = .74f;
    private const float TilePitchZ = .38f;
    private const float TileWidth = .724f;
    private const float TileDepth = .365f;
    private const float RoomTileLimitX = 10.5f;
    private const float PanelPitch = .43f;

    public static GameObject Build(Transform parent, Func<string, Material> material)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        var root = new GameObject(RootName);
        root.transform.SetParent(parent, false);
        var mesh = new BoxMeshBuilder();
        AddLoweredRightWall(mesh);
        AddFloor(mesh);
        AddFrontWall(mesh);
        AddRightWall(mesh);
        AddBackWall(mesh);
        AddProductLedge(mesh, SalonWashAnnexLayout.WashBedPosition.x);
        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh.Build("Wash annex shell", out string[] materialNames);
        var renderer = root.AddComponent<MeshRenderer>();
        var materials = new Material[materialNames.Length];
        for (int i = 0; i < materialNames.Length; i++)
            materials[i] = material(materialNames[i]);
        renderer.sharedMaterials = materials;
        return root;
    }

    public static IReadOnlyList<string> RequiredMaterialNames()
    {
        var mesh = new BoxMeshBuilder();
        AddLoweredRightWall(mesh);
        AddFloor(mesh);
        AddFrontWall(mesh);
        AddRightWall(mesh);
        AddBackWall(mesh);
        AddProductLedge(mesh, SalonWashAnnexLayout.WashBedPosition.x);
        mesh.Build("probe", out string[] names);
        return names;
    }

    /// <summary>Fallback palette for the procedural (washCraft=original) room.</summary>
    public static Color FallbackColor(string materialName)
    {
        if (materialName.StartsWith("Tile", StringComparison.Ordinal) &&
            int.TryParse(materialName.Substring(4), out int tile))
            return Rgb(80 + (tile - 4) * 2, 123 + (tile - 4) * 2, 115 + (tile - 4) * 2);
        if (materialName.StartsWith("Plaster", StringComparison.Ordinal) && materialName.Length > 7 &&
            int.TryParse(materialName.Substring(7), out int plaster))
            return Rgb(209 + plaster * 3, 194 + plaster * 3, 171 + plaster * 3);
        if (materialName.StartsWith("Oak", StringComparison.Ordinal) && materialName.Length == 4 &&
            int.TryParse(materialName.Substring(3), out int oak))
            return Rgb(128 + oak * 5, 92 + oak * 4, 58 + oak * 3);
        switch (materialName)
        {
            case "Plaster": return Rgb(0xd6, 0xc6, 0xae);
            case "Grout": return Rgb(0x60, 0x81, 0x76);
            case "Oak": return Rgb(0xa9, 0x7a, 0x49);
            case "OakLight": return Rgb(0xc3, 0x96, 0x60);
            case "OakDark": return Rgb(0x71, 0x53, 0x37);
            case "Porcelain": return Rgb(0xee, 0xe5, 0xcf);
            case "Brass": return Rgb(0xbd, 0xa2, 0x74);
            case "Metal": return Rgb(0x3b, 0x43, 0x42);
            case "Label": return Rgb(0xed, 0xe2, 0xc7);
            case "BottleLilac": return Rgb(0xa0, 0x92, 0xb9);
            case "BottleAmber": return Rgb(0xc9, 0x96, 0x5c);
            case "BottleBlue": return Rgb(0x72, 0x9f, 0xa1);
            case "BottlePink": return Rgb(0xc2, 0x94, 0x9d);
            default: return Color.magenta;
        }
    }

    private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    /// <summary>Old right wall, now a low cutaway in front of the opening.</summary>
    private static void AddLoweredRightWall(BoxMeshBuilder mesh)
    {
        const float x = SalonWashAnnexLayout.OldWallX;
        const float h = SalonWashAnnexLayout.LowWallHeight;
        const float end = SalonWashAnnexLayout.FrontWallZ + .12f;
        mesh.Box(new Vector3(x - .12f, 0f, -5.5f), new Vector3(x + .12f, h, end), "Plaster");
        for (int j = 0; j <= 14; j++)
        {
            float z = -5.22f + j * PanelPitch;
            mesh.Box(new Vector3(x - .22f, .015f, z - .211f), new Vector3(x - .10f, .585f, z + .211f), OakName(1, j));
        }
        mesh.Box(new Vector3(x - .23f, h - .08f, -5.6f), new Vector3(x + .23f, h + .08f, end + .1f), "Porcelain");
    }

    private static void AddFloor(BoxMeshBuilder mesh)
    {
        mesh.Box(new Vector3(10.6f, -.23f, SalonWashAnnexLayout.FrontWallZ - .17f),
            new Vector3(SalonWashAnnexLayout.OuterWallX + .17f, -.01f, SalonWashAnnexLayout.BackWallZ + .17f), "Grout");
        float wallFace = SalonWashAnnexLayout.OuterWallX - .12f;
        for (int row = 17; row <= 33; row++)
        {
            float z = TileStartZ + row * TilePitchZ;
            for (int col = 0; col < 60; col++)
            {
                float x = TileStartX + col * TilePitchX + (row % 2) * .37f;
                if (x + .72f <= RoomTileLimitX) continue;
                if (x >= wallFace) break;
                mesh.FloorQuad(x, z, Mathf.Min(x + TileWidth, wallFace), z + TileDepth, 0f,
                    "Tile" + Hash(row * 131 + col, 9));
            }
        }
    }

    private static void AddFrontWall(BoxMeshBuilder mesh)
    {
        const float z = SalonWashAnnexLayout.FrontWallZ;
        const float h = SalonWashAnnexLayout.LowWallHeight;
        float right = SalonWashAnnexLayout.OuterWallX + .12f;
        mesh.Box(new Vector3(SalonWashAnnexLayout.OldWallX - .12f, 0f, z - .12f), new Vector3(right, h, z + .12f), "Plaster");
        for (int i = 0; ; i++)
        {
            float x = 11.0f + i * PanelPitch;
            if (x + .211f > SalonWashAnnexLayout.InteriorMaxX) break;
            mesh.Box(new Vector3(x - .211f, .015f, z + .10f), new Vector3(x + .211f, .585f, z + .22f), OakName(2, i));
        }
        mesh.Box(new Vector3(SalonWashAnnexLayout.OldWallX - .23f, h - .08f, z - .23f),
            new Vector3(right + .11f, h + .08f, z + .23f), "Porcelain");
    }

    private static void AddRightWall(BoxMeshBuilder mesh)
    {
        const float x = SalonWashAnnexLayout.OuterWallX;
        const float h = SalonWashAnnexLayout.TallWallHeight;
        float front = SalonWashAnnexLayout.FrontWallZ - .12f;
        float back = SalonWashAnnexLayout.BackWallZ + .12f;
        mesh.Box(new Vector3(x - .12f, 0f, front), new Vector3(x + .12f, h, back), "Plaster");
        for (int j = 15; j <= 28; j++)
        {
            float z = -5.22f + j * PanelPitch;
            mesh.Box(new Vector3(x - .22f, 0f, z - .211f), new Vector3(x - .10f, 1.10f, z + .211f), OakName(3, j));
        }
        mesh.Box(new Vector3(x - .23f, h - .08f, front - .05f), new Vector3(x + .23f, h + .08f, back + .05f), "Porcelain");
    }

    /// <summary>Continues the back wall stack of Blender room() to the new corner.</summary>
    private static void AddBackWall(BoxMeshBuilder mesh)
    {
        const float z = SalonWashAnnexLayout.BackWallZ;
        float inner = SalonWashAnnexLayout.OuterWallX - .12f;
        mesh.Box(new Vector3(10.80f, 0f, z - .12f), new Vector3(SalonWashAnnexLayout.OuterWallX + .12f, 3.48f, z + .12f), "Plaster");
        for (int i = 50; ; i++)
        {
            float x = -10.57f + i * PanelPitch;
            if (x + .211f > SalonWashAnnexLayout.InteriorMaxX) break;
            mesh.Box(new Vector3(x - .211f, .01f, 7.205f), new Vector3(x + .211f, 1.11f, 7.325f), OakName(4, i));
        }
        for (int i = 36; ; i++)
        {
            float x = -10.48f + i * .60f;
            if (x + .2955f > inner) break;
            mesh.Box(new Vector3(x - .2955f, 1.11f, 7.2655f), new Vector3(x + .2955f, 3.29f, 7.3005f), "Plaster" + Hash(i * 17 + 3, 5));
        }
        mesh.Box(new Vector3(10.85f, 1.11f, 7.09f), new Vector3(inner, 1.23f, 7.31f), "OakLight");
        mesh.Box(new Vector3(10.85f, .03f, 7.085f), new Vector3(inner, .23f, 7.295f), "OakDark");
        mesh.Box(new Vector3(11.0f, 3.375f, 7.16f), new Vector3(SalonWashAnnexLayout.OuterWallX + .23f, 3.565f, 7.62f), "Porcelain");
    }

    /// <summary>Same ledge the crafted room places above each wash bed.</summary>
    private static void AddProductLedge(BoxMeshBuilder mesh, float x)
    {
        mesh.Box(new Vector3(x - 1.5f, 2.8f, 6.425f), new Vector3(x + 1.5f, 3.0f, 7.075f), "Oak");
        mesh.Box(new Vector3(x - 1.525f, 2.875f, 6.369f), new Vector3(x + 1.525f, 3.005f, 6.459f), "OakLight");
        for (int side = -1; side <= 1; side += 2)
            mesh.Box(new Vector3(x + side * 1.14f - .0375f, 2.57f, 6.585f),
                new Vector3(x + side * 1.14f + .0375f, 2.89f, 7.015f), "Brass");
        string[] colors = { "BottleLilac", "BottleAmber", "BottleBlue", "BottlePink" };
        for (int i = 0; i < 7; i++)
        {
            float bx = x - 1.13f + i * .36f;
            float h = .38f + .075f * (i % 3);
            mesh.Box(new Vector3(bx - .11f, 3.01f, 6.66f), new Vector3(bx + .11f, 3.01f + h * .85f, 6.84f), colors[i % 4]);
            mesh.Box(new Vector3(bx - .0725f, 3.01f + h * .27f, 6.644f), new Vector3(bx + .0725f, 3.01f + h * .62f, 6.656f), "Label");
            mesh.Box(new Vector3(bx - .04f, 3.01f + h * .85f, 6.71f), new Vector3(bx + .04f, 3.01f + h + .08f, 6.79f), "Metal");
        }
    }

    private static string OakName(int wall, int index) => "Oak" + Hash(wall * 1009 + index * 37, 7);

    private static int Hash(int value, int modulo)
    {
        unchecked
        {
            uint h = (uint)value * 2654435761u;
            h ^= h >> 13;
            return (int)(h % (uint)modulo);
        }
    }

    internal sealed class BoxMeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, List<int>> _triangles = new Dictionary<string, List<int>>();

        public void Box(Vector3 min, Vector3 max, string material)
        {
            Vector3 size = max - min;
            var x = new Vector3(size.x, 0f, 0f);
            var y = new Vector3(0f, size.y, 0f);
            var z = new Vector3(0f, 0f, size.z);
            Face(min + x, y, z, Vector3.right, material);
            Face(min, z, y, Vector3.left, material);
            Face(min + y, z, x, Vector3.up, material);
            Face(min, x, z, Vector3.down, material);
            Face(min + z, x, y, Vector3.forward, material);
            Face(min, y, x, Vector3.back, material);
        }

        public void FloorQuad(float x0, float z0, float x1, float z1, float y, string material)
        {
            Face(new Vector3(x0, y, z0), new Vector3(0f, 0f, z1 - z0), new Vector3(x1 - x0, 0f, 0f), Vector3.up, material);
        }

        public Mesh Build(string name, out string[] materials)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.subMeshCount = _order.Count;
            for (int i = 0; i < _order.Count; i++)
                mesh.SetTriangles(_triangles[_order[i]], i);
            mesh.RecalculateBounds();
            materials = _order.ToArray();
            return mesh;
        }

        // Front faces satisfy cross(u, w) == normal, matching Unity's clockwise winding.
        private void Face(Vector3 origin, Vector3 u, Vector3 w, Vector3 normal, string material)
        {
            if (!_triangles.TryGetValue(material, out List<int> triangles))
            {
                triangles = new List<int>();
                _triangles[material] = triangles;
                _order.Add(material);
            }
            int start = _vertices.Count;
            _vertices.Add(origin);
            _vertices.Add(origin + u);
            _vertices.Add(origin + u + w);
            _vertices.Add(origin + w);
            for (int i = 0; i < 4; i++) _normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
