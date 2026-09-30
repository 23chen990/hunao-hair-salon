using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using HairSalon.AssetPipeline;
using HairSalon.CutStations;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Second wash room grown behind the right wall (basic-issue decision
/// 2026-09-30, map option B, first area = wash room).
/// </summary>
public sealed class WashAnnexExpansionTests
{
    private const float BodyRadius = .15f;
    private static readonly Vector3 WaitingSeat = new Vector3(-6.2f, 1.05f, -2.4f);
    private static readonly Vector3 MainRoomSeat = new Vector3(-2.1f, 1.05f, .6f);
    private static readonly Vector3 Exit = new Vector3(-10.8f, 1.05f, 5.9f);

    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;

    [TearDown]
    public void TearDown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        GameObject map = GameObject.Find("Fixed Salon Map");
        if (map != null) Object.DestroyImmediate(map);
    }

    [Test]
    public void AnnexProgressUsesItsOwnLedgerAndRequiresTheFullPayment()
    {
        var data = SalonProgressData.CreateDefault();
        data.WashAnnexExpansionPaid = 240;
        Assert.IsTrue(data.TryValidate(out _));
        SalonProgressData clone = data.Clone();
        Assert.AreEqual(240, clone.WashAnnexExpansionPaid);
        Assert.IsFalse(clone.WashAnnexExpansionPurchased);

        data.WashAnnexExpansionPurchased = true;
        Assert.IsFalse(data.TryValidate(out _), "A purchased annex must carry the full 2000 coins.");
        data.WashAnnexExpansionPaid = SalonProgressData.WashAnnexExpansionCost;
        Assert.IsTrue(data.TryValidate(out _));
        data.WashAnnexExpansionPaid = SalonProgressData.WashAnnexExpansionCost + 1;
        Assert.IsFalse(data.TryValidate(out _));
    }

    [Test]
    public void LegacyProgressJsonKeepsTheAnnexClosed()
    {
        var legacy = JsonUtility.FromJson<SalonProgressData>(
            "{\"SchemaVersion\":1,\"DayNumber\":3,\"Balance\":700,\"HaircutExpansionPurchased\":true," +
            "\"HaircutExpansionPaid\":180,\"ShopSatisfaction\":90,\"ReputationStars\":3}");
        Assert.IsFalse(legacy.WashAnnexExpansionPurchased);
        Assert.AreEqual(0, legacy.WashAnnexExpansionPaid);
        Assert.IsFalse(legacy.TryValidate(out _), "The repository migrates version-one saves.");
    }

    [Test]
    public void CustomersEnterTransferAndLeaveTheAnnexOnlyThroughTheOpening()
    {
        Vector3 annex = SalonWashAnnexLayout.WashCustomerAnchorPosition;
        Vector3[] toAnnex = SalonCustomerPath.BuildServiceRoute(WaitingSeat, annex);
        Assert.AreEqual(annex, toAnnex[toAnnex.Length - 1]);
        AssertRouteUsesOpening(WaitingSeat, toAnnex);
        AssertRouteUsesOpening(annex, SalonCustomerPath.BuildServiceRoute(annex, MainRoomSeat));
        Vector3[] leaving = SalonCustomerPath.BuildLeavingRoute(annex, Exit);
        Assert.AreEqual(SalonEntranceDoor.OutsideExit, leaving[leaving.Length - 1]);
        AssertRouteUsesOpening(annex, leaving);
    }

    [Test]
    public void AnnexPortalLaneClearsTheSecondHaircutChairAndCornerPlant()
    {
        CutStationManifest manifest = CutStationManifestLoader.LoadFromResources();
        ResolvedCutStationLayout chair = CutStationLayoutResolver.Resolve(
            manifest.Find("cut-station-classic-poc"), CutStationOrientation.RightWall, new Vector3(6.2f, .2f, .2f));
        AssetDefinition plant = AssetManifestLoader.LoadFromResources().Find("prop-potted-plant");
        Vector2 plantHalf = plant.Collision.Size * .5f;
        Rect plantRect = Rect.MinMaxRect(9.4f - plantHalf.x, -.9f - plantHalf.y, 9.4f + plantHalf.x, -.9f + plantHalf.y);
        Vector3 annex = SalonWashAnnexLayout.WashCustomerAnchorPosition;
        foreach (Vector3[] route in new[]
                 {
                     SalonCustomerPath.BuildServiceRoute(WaitingSeat, annex),
                     SalonCustomerPath.BuildServiceRoute(annex, MainRoomSeat),
                     SalonCustomerPath.BuildLeavingRoute(annex, Exit)
                 })
        {
            ForEachSample(route[0], route, point =>
            {
                Assert.IsFalse(SalonDemo.IsCutStationMovementBlocked(chair, point),
                    "The annex lane must not be rolled back by the second haircut chair: " + point);
                Assert.IsFalse(plantRect.Contains(new Vector2(point.x, point.z)),
                    "The annex lane must not walk through the corner plant: " + point);
            });
        }
    }

    [Test]
    public void MainRoomRoutesKeepTheirApprovedLanes()
    {
        Vector3[] service = SalonCustomerPath.BuildServiceRoute(WaitingSeat, MainRoomSeat);
        Assert.AreEqual(4, service.Length);
        Assert.AreEqual(-1.35f, service[0].z, 1e-4f);
        Assert.AreEqual(-1.35f, service[1].z, 1e-4f);
        Vector3[] leaving = SalonCustomerPath.BuildLeavingRoute(new Vector3(6.2f, 1.05f, .2f), Exit);
        Assert.AreEqual(-.9f, leaving[0].z, 1e-4f);
        Assert.AreEqual(SalonEntranceDoor.OutsideExit, leaving[leaving.Length - 1]);
    }

    [Test]
    public void CutterRemovesOnlySelectedIslandsAndKeepsSubmeshes()
    {
        var mesh = new Mesh();
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0),
            new Vector3(5, 0, 0), new Vector3(5, 0, 1), new Vector3(6, 0, 1), new Vector3(6, 0, 0)
        });
        mesh.subMeshCount = 2;
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 1);

        Mesh cut = SalonRoomMeshCutter.RemoveIslands(mesh, bounds => bounds.center.x > 3f, out int removed);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(2, cut.subMeshCount, "Material slots must stay aligned with the renderer.");
        Assert.AreEqual(6, cut.GetTriangles(0).Length);
        Assert.AreEqual(0, cut.GetTriangles(1).Length);
        Assert.AreEqual(6, mesh.GetTriangles(1).Length, "The source mesh is kept for closing the annex again.");
    }

    [Test]
    public void CraftedRoomCutOpensTheRightWallButKeepsFloorBackWallAndRearJamb()
    {
        GameObject prefab = Resources.Load<GameObject>("Models/WashCraft/room-finish");
        Assert.IsNotNull(prefab);
        Mesh source = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
        Assert.IsTrue(source.isReadable, "room-finish.fbx must import as CPU readable for the runtime cut.");

        Mesh cut = SalonRoomMeshCutter.RemoveIslands(source, SalonWashAnnexLayout.IsOldRightWallFace, out int removed);

        Assert.Greater(removed, 500, "The whole old right wall (plaster, 29 panels, cornice) must be removed.");
        List<Bounds> before = Faces(source);
        List<Bounds> after = Faces(cut);
        foreach (Bounds face in after)
            Assert.IsFalse(SalonWashAnnexLayout.IsOldRightWallFace(face), "Old wall face survived: " + face);
        // Starts just behind the cut line: panel 28's end cap (z=7.031) is
        // removed with its panel, panel 29's front face (z=7.039) is not.
        var rearJamb = new Bounds();
        rearJamb.SetMinMax(new Vector3(10.5f, 0f, SalonWashAnnexLayout.OldWallCutMaxZ + .001f),
            new Vector3(10.64f, 1.12f, 7.47f));
        Assert.AreEqual(CountInside(before, rearJamb), CountInside(after, rearJamb),
            "The last side panel stays intact as the rear door jamb.");
        Assert.Greater(CountInside(after, rearJamb), 0);
        Assert.AreEqual(CountWhere(before, f => f.center.y < -.01f), CountWhere(after, f => f.center.y < -.01f),
            "The foundation is not part of the cut.");
        Assert.AreEqual(CountWhere(before, f => f.center.z > 7.08f), CountWhere(after, f => f.center.z > 7.08f),
            "The back wall stack is not part of the cut.");
        Assert.AreEqual(CountWhere(before, f => f.center.x < 10.2f), CountWhere(after, f => f.center.x < 10.2f),
            "Room floor tiles and furniture are not part of the cut.");
    }

    [Test]
    public void AnnexShellUsesOnlyTheCraftedRoomMaterials()
    {
        GameObject prefab = Resources.Load<GameObject>("Models/WashCraft/room-finish");
        var names = new HashSet<string>();
        foreach (Material material in prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials)
            names.Add(material.name);
        foreach (string required in SalonWashAnnexBuilder.RequiredMaterialNames())
            Assert.IsTrue(names.Contains(required), "Annex material is not part of the crafted finish: " + required);
    }

    [Test]
    public void AnnexShellLeavesTheWalkableInteriorClear()
    {
        _root = new GameObject("Annex shell probe");
        GameObject shell = SalonWashAnnexBuilder.Build(_root.transform, name => null);
        Mesh mesh = shell.GetComponent<MeshFilter>().sharedMesh;
        // The main room's own skirting starts at z=7.085, so the annex uses
        // the same back limit rather than the floor clamp at z=7.15.
        Rect interior = Rect.MinMaxRect(SalonWashAnnexLayout.OpeningX + BodyRadius, SalonWashAnnexLayout.InteriorMinZ + BodyRadius,
            SalonWashAnnexLayout.InteriorMaxX - BodyRadius, 7.08f);
        foreach (Bounds face in Faces(mesh))
        {
            bool bodyHeight = face.max.y > .05f && face.min.y < 2.4f;
            bool overInterior = face.max.x > interior.xMin && face.min.x < interior.xMax &&
                                face.max.z > interior.yMin && face.min.z < interior.yMax;
            Assert.IsFalse(bodyHeight && overInterior, "Annex shell geometry blocks the walkable interior: " + face);
        }

        // Crafted overview camera: (-12,20,-23) looking at (0,.65,1), size 8.35, 16:9.
        Quaternion overview = Quaternion.LookRotation(new Vector3(0f, .65f, 1f) - new Vector3(-12f, 20f, -23f));
        Vector3 right = overview * Vector3.right;
        float centre = Vector3.Dot(right, new Vector3(0f, .65f, 1f));
        float halfWidth = 8.35f * 16f / 9f;
        foreach (Vector3 p in mesh.vertices)
            Assert.LessOrEqual(Mathf.Abs(Vector3.Dot(right, p) - centre), halfWidth,
                "The annex must stay inside the crafted 16:9 overview: " + p);
    }

    [Test]
    public void JoystickEntersTheAnnexThroughTheOpeningAndReachesTheWashAnchor()
    {
        var obstacles = new List<Rect>();
        SalonWashAnnexLayout.AddOpenObstacles(obstacles, BodyRadius);
        AssetDefinition wash = AssetManifestLoader.LoadFromResources().Find("furniture-wash-station");
        Vector3 bed = SalonWashAnnexLayout.WashBedPosition;
        Vector2 center = new Vector2(bed.x + wash.Collision.Center.x, bed.z + wash.Collision.Center.y);
        Vector2 half = wash.Collision.Size * .5f + Vector2.one * BodyRadius;
        obstacles.Add(Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y));
        Rect walkable = SalonWashAnnexLayout.WalkableBounds;
        Rect floor = new Rect(walkable.xMin + BodyRadius, walkable.yMin + BodyRadius,
            walkable.width - BodyRadius * 2f, walkable.height - BodyRadius * 2f);

        Vector3 anchor = SalonWashAnnexLayout.WashPlayerAnchorPosition;
        Assert.IsTrue(floor.Contains(new Vector2(anchor.x, anchor.z)));
        foreach (Rect obstacle in obstacles)
            Assert.IsFalse(obstacle.Contains(new Vector2(anchor.x, anchor.z)), "Wash anchor is blocked by " + obstacle);

        Vector3 through = Walk(new Vector3(8.8f, 0f, anchor.z), Vector2.right, 2.85f, floor, obstacles);
        Assert.AreEqual(anchor.x, through.x, .1f, "Walking right at the bed row must pass the opening to the anchor.");
        Vector3 front = Walk(new Vector3(8.8f, 0f, 0f), Vector2.right, 4f, floor, obstacles);
        Assert.LessOrEqual(front.x, SalonWashAnnexLayout.OpeningX - BodyRadius + .01f,
            "The low front segment of the old wall still blocks the room edge.");
        Vector3 inside = Walk(new Vector3(12f, 0f, 2.3f), Vector2.down, 3f, floor, obstacles);
        Assert.GreaterOrEqual(inside.z, SalonWashAnnexLayout.InteriorMinZ + BodyRadius - .01f,
            "The annex front wall keeps the stylist inside.");
        Vector3 back = Walk(new Vector3(9.9f, 0f, 7f), Vector2.right, 3f, floor, obstacles);
        Assert.Less(back.x, 10.5f, "The rear door jamb blocks the back corner.");
    }

    [Test]
    public void AnnexPadStaysHiddenBeforeItsDayOpens()
    {
        CreateDemo(900);
        Invoke("UpdateMobileWashAnnexPad", 1f);
        Assert.IsFalse(Get<GameObject>("_mobileWashAnnexPadRoot").activeSelf);
        Assert.AreEqual(0, Get<SalonProximityPurchasePadModel>("_mobileWashAnnexPad").Paid);
        Assert.AreEqual(900, _game.Balance);
    }

    [Test]
    public void AnnexPadChargesThenOpensStationFourAndRecordsProgress()
    {
        CreateDemo(3900, 3);
        var progress = Get<SalonProgressData>("_mobileProgress");
        progress.BlowDryerPurchased = true; progress.BlowDryerPaid = 600;
        progress.WashStationPurchased = true; progress.WashStationPaid = 1200;
        progress.AutoBlowPurchased = true; progress.BlowStandPaid = 3000;
        progress.WaitingSeatsPurchased = true;
        progress.WaitingSeatsPaid = SalonProgressData.WaitingSeatsCost;
        Invoke("CreateMobileUnlockPads", progress);
        Get<SalonProximityPurchasePadModel>("_mobileSupplyPad").ApplyPayment(
            SalonProgressData.SupplyRackExpansionCost);
        Invoke("UpdateMobileWashAnnexPadVisual");
        Assert.IsTrue(Get<GameObject>("_mobileWashAnnexPadRoot").activeSelf, "Building the preceding equipment reveals the annex pad.");

        Invoke("UpdateMobileWashAnnexPad", 1f);
        var pad = Get<SalonProximityPurchasePadModel>("_mobileWashAnnexPad");
        Assert.AreEqual(450, pad.Paid);
        Assert.AreEqual(3450, _game.Balance);
        Assert.IsFalse(_game.Workstations[SalonWashAnnexLayout.StationId].IsUsable);

        for (int i = 0; i < 7; i++) Invoke("UpdateMobileWashAnnexPad", 1f);

        Assert.IsTrue(pad.IsUnlocked);
        Assert.AreEqual(300, _game.Balance);
        Assert.IsTrue(_game.Workstations[SalonWashAnnexLayout.StationId].IsUsable);
        progress = Get<SalonProgressData>("_mobileProgress");
        Assert.IsTrue(progress.WashAnnexExpansionPurchased);
        Assert.AreEqual(3600, progress.WashAnnexExpansionPaid);
        Assert.IsFalse(Get<GameObject>("_mobileWashAnnexPadRoot").activeSelf);
    }

    [Test]
    public void OpeningTheAnnexCutsTheWallAndAClosedCheckpointRestoresIt()
    {
        CreateDemo(0);
        var map = new GameObject("Fixed Salon Map");
        GameObject finish = Object.Instantiate(Resources.Load<GameObject>("Models/WashCraft/room-finish"), map.transform);
        finish.name = "Crafted Room Finish";
        MeshFilter filter = finish.GetComponentInChildren<MeshFilter>();
        Mesh closed = filter.sharedMesh;
        Get<SalonProximityPurchasePadModel>("_mobileWashAnnexPad").ApplyPayment(SalonProgressData.WashAnnexExpansionCost);

        Invoke("ApplyMobileWashAnnexPresentation");

        Assert.AreNotSame(closed, filter.sharedMesh);
        Assert.Less(filter.sharedMesh.triangles.Length, closed.triangles.Length);
        GameObject annex = Get<GameObject>("_mobileWashAnnexRoot");
        Assert.IsTrue(annex.activeSelf);
        foreach (Material material in annex.GetComponent<MeshRenderer>().sharedMaterials)
            Assert.AreNotEqual(Color.magenta, material.color, material.name + " fell back outside the crafted palette.");

        Invoke("CreateMobileWashAnnexPad", SalonProgressData.CreateDefault());
        Invoke("ApplyMobileWashAnnexPresentation");

        Assert.AreSame(closed, filter.sharedMesh, "Retry to a pre-purchase checkpoint closes the wall again.");
        Assert.IsFalse(annex.activeSelf);
    }

    [Test]
    public void GuidedWashCustomerIsSentToTheAnnexWhenTheFirstBedIsBusy()
    {
        CreateDemo(0);
        _game.ConfigureWorkstationAvailability(true, true, false);
        var anchors = Get<Dictionary<int, Transform>>("_playerServiceAnchors");
        anchors[0] = NewTransform("Wash 1 anchor", SalonDemo.WashPlayerAnchorPosition);
        anchors[SalonWashAnnexLayout.StationId] =
            NewTransform("Annex anchor", SalonWashAnnexLayout.WashPlayerAnchorPosition);
        CustomerModel first = _game.Spawn(9401, new[] { ServiceType.Wash });
        CustomerModel second = _game.Spawn(9402, new[] { ServiceType.Wash });
        _game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(_game.Assign(first, 0));
        Assert.IsTrue(_game.EngageCustomerForHandoff(second));
        Set("_mobileGuidedCustomer", second);

        object target = Invoke("FindMobileTarget");

        int station = (int)target.GetType().GetField("Station").GetValue(target);
        Assert.AreEqual(SalonWashAnnexLayout.StationId, station,
            "With bed 1 busy, the guided wash customer goes to the second wash room.");
    }

    private void CreateDemo(int balance, int day = 2)
    {
        _root = new GameObject("Wash annex regression");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        _game.RestorePersistentState(balance, false, true);
        _game.ConfigureWorkstationAvailability(true, false, false);
        var dayController = new BusinessDayController(SalonMobileDayConfig.CreateForDay(day));
        dayController.PrepareDay(day);
        dayController.StartBusiness();
        Set("_mobileMode", true);
        Set("_game", _game);
        Set("_dayController", dayController);
        Set("_mobileProgress", SalonProgressData.CreateDefault());
        var haircut = new SalonProximityPurchasePadModel("expansion-pad-haircut-2", SalonProgressData.HaircutExpansionCost);
        haircut.ApplyPayment(haircut.Cost);
        Set("_mobileHaircutExpansionPad", haircut);
        Set("_mobileSupplyPad",
            new SalonProximityPurchasePadModel("expansion-pad-wash-rack", SalonProgressData.SupplyRackExpansionCost));
        Invoke("CreateMobileWashAnnexPad", SalonProgressData.CreateDefault());
        Set("_player", NewTransform("Annex player", SalonWashAnnexLayout.PadPosition));
        Transform stage = NewTransform("Annex stage", Vector3.zero);
        Invoke("BuildMobileWashAnnexPad", stage);
    }

    private static void AssertRouteUsesOpening(Vector3 start, Vector3[] route)
    {
        ForEachSample(start, route, point =>
        {
            if (point.x <= SalonWashAnnexLayout.OpeningX) return;
            Assert.LessOrEqual(point.x, SalonWashAnnexLayout.InteriorMaxX, "Route leaves the annex: " + point);
            Assert.That(point.z, Is.InRange(SalonWashAnnexLayout.InteriorMinZ, SalonWashAnnexLayout.InteriorMaxZ),
                "Route passes outside the annex walls: " + point);
            if (point.x < SalonWashAnnexLayout.OldWallX + .12f)
                Assert.That(point.z, Is.InRange(1.2f, 6.85f), "Route clips a door jamb: " + point);
        });
    }

    private static void ForEachSample(Vector3 start, Vector3[] route, System.Action<Vector3> check)
    {
        Vector3 previous = start;
        foreach (Vector3 point in route)
        {
            for (int s = 0; s <= 50; s++) check(Vector3.Lerp(previous, point, s / 50f));
            previous = point;
        }
    }

    private static Vector3 Walk(Vector3 start, Vector2 input, float distance, Rect floor, List<Rect> obstacles)
        => SalonMobileNavigation.Move(start, input, Vector3.right, Vector3.forward, distance, floor, obstacles);

    /// <summary>Bounds of every vertex-connected island (one per crafted face).</summary>
    private static List<Bounds> Faces(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] parent = new int[vertices.Length];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int i)
        {
            while (parent[i] != i) i = parent[i] = parent[parent[i]];
            return i;
        }
        var triangles = new List<int>();
        for (int s = 0; s < mesh.subMeshCount; s++) triangles.AddRange(mesh.GetTriangles(s));
        for (int t = 0; t < triangles.Count; t += 3)
            for (int k = 1; k < 3; k++)
            {
                int a = Find(triangles[t]);
                int b = Find(triangles[t + k]);
                if (a != b) parent[b] = a;
            }
        var bounds = new Dictionary<int, Bounds>();
        foreach (int index in triangles)
        {
            int root = Find(index);
            if (bounds.TryGetValue(root, out Bounds b)) { b.Encapsulate(vertices[index]); bounds[root] = b; }
            else bounds[root] = new Bounds(vertices[index], Vector3.zero);
        }
        return new List<Bounds>(bounds.Values);
    }

    private static int CountInside(List<Bounds> faces, Bounds region)
        => CountWhere(faces, face => region.Contains(face.center));

    private static int CountWhere(List<Bounds> faces, System.Func<Bounds, bool> predicate)
    {
        int count = 0;
        foreach (Bounds face in faces) if (predicate(face)) count++;
        return count;
    }

    private Transform NewTransform(string name, Vector3 position)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(_root.transform, false);
        gameObject.transform.position = position;
        return gameObject.transform;
    }

    private T Get<T>(string name)
        => (T)typeof(SalonDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);

    private void Set(string name, object value)
        => typeof(SalonDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);

    private object Invoke(string name, params object[] args)
    {
        MethodInfo method = typeof(SalonDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing SalonDemo method " + name);
        return method.Invoke(_demo, args);
    }
}
