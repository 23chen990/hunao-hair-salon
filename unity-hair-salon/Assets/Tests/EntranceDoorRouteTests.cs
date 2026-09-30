using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EntranceDoorRouteTests
{
    [Test]
    public void EnteringRouteCrossesTheDoorFromOutside()
    {
        Vector3 destination = new Vector3(-9f, 1.05f, -4.3f);
        Vector3[] route = SalonCustomerPath.BuildEnteringRoute(SalonEntranceDoor.OutsideSpawn, destination);

        Assert.That(route, Does.Contain(SalonEntranceDoor.DoorOutside));
        Assert.That(route, Does.Contain(SalonEntranceDoor.DoorInside));
        Assert.Less(route[0].x, -10.9f);
        Assert.That(route[route.Length - 1], Is.EqualTo(destination));
        Assert.That(route[1].z, Is.InRange(SalonEntranceDoor.OpeningMinZ,
            SalonEntranceDoor.OpeningMaxZ));
    }

    [Test]
    public void LeavingRouteReachesExteriorAfterDoor()
    {
        Vector3[] route = SalonCustomerPath.BuildLeavingRoute(
            new Vector3(-2.1f, 1.05f, .2f), SalonDemoFallbackExit());

        Assert.That(route[route.Length - 3], Is.EqualTo(SalonEntranceDoor.DoorInside));
        Assert.That(route[route.Length - 2], Is.EqualTo(SalonEntranceDoor.DoorOutside));
        Assert.That(route[route.Length - 1], Is.EqualTo(SalonEntranceDoor.OutsideExit));
        Assert.Less(route[route.Length - 1].x, -10.9f);
    }

    [Test]
    public void ServiceRouteUsesQueueAnchorWithoutPassingDestinationFirst()
    {
        Vector3 queue = new Vector3(2.65f, 1f, -1.15f);
        Vector3 seat = new Vector3(-2.1f, 1f, .2f);
        Vector3[] route = SalonCustomerPath.BuildServiceRoute(
            new Vector3(-8f, 1f, -4f), queue, seat);

        Assert.That(route, Does.Contain(queue));
        Assert.That(route[route.Length - 1], Is.EqualTo(seat));
        int queueIndex = System.Array.IndexOf(route, queue);
        int seatIndex = System.Array.IndexOf(route, seat);
        Assert.Less(queueIndex, seatIndex);
        Assert.That(route[1].x, Is.EqualTo(queue.x).Within(.001f));
    }

    [Test]
    public void CheckoutRouteEndsAtRequestedSlotAndAvoidsCounterSide()
    {
        Vector3 slot = new Vector3(3.9f, 1.05f, -2.2f);
        Vector3[] route = SalonCustomerPath.BuildCheckoutRoute(
            new Vector3(6.2f, 1.05f, .2f), slot);
        Assert.That(route[route.Length - 1], Is.EqualTo(slot));
        foreach (Vector3 point in route)
            Assert.IsFalse(point.x > 4.55f && point.x < 9.85f &&
                           point.z > -5.0f && point.z < -2.8f,
                "Checkout route entered the counter footprint: " + point);
    }

    [Test]
    public void LeftWallSelectorKeepsFloorAndBackWall()
    {
        Assert.IsTrue(SalonEntranceDoor.IsOldLeftWallFace(
            new Bounds(new Vector3(-10.7f, .4f, -2f), new Vector3(.2f, .8f, .5f))));
        Assert.IsFalse(SalonEntranceDoor.IsOldLeftWallFace(
            new Bounds(new Vector3(-2f, -.2f, -2f), new Vector3(10f, .2f, 10f))));
        Assert.IsFalse(SalonEntranceDoor.IsOldLeftWallFace(
            new Bounds(new Vector3(-2f, 1f, 7.3f), new Vector3(10f, 2f, .3f))));
    }

    [Test]
    public void PlayerWalkableBoundsStopBeforeTheExteriorDoorPoint()
    {
        Assert.That(SalonWashAnnexLayout.WalkableBounds.xMin, Is.GreaterThanOrEqualTo(-10.35f));
        Assert.That(SalonEntranceDoor.DoorInside.x,
            Is.GreaterThan(SalonWashAnnexLayout.WalkableBounds.xMin));
        Assert.That(SalonEntranceDoor.DoorOutside.x,
            Is.LessThan(SalonWashAnnexLayout.WalkableBounds.xMin));
    }

    [Test]
    public void QueueWalkoutsLeaveStraightThroughTheDoorWithoutADetour()
    {
        foreach (float x in new[] { -9f, -7.75f, -6.5f, -5.25f, -4f, -2.75f })
        {
            Vector3 start = new Vector3(x, 1.05f, -4.3f);
            Vector3[] route = SalonCustomerPath.BuildLeavingRoute(start, SalonEntranceDoor.OutsideExit);
            Assert.AreEqual(SalonEntranceDoor.DoorInside, route[0],
                "A queue walkout must not walk up behind the chairs first: " + start);
            Assert.AreEqual(SalonEntranceDoor.OutsideExit, route[route.Length - 1]);
        }
    }

    [Test]
    public void LeavingRoutesAvoidTheWaitingSofaCoffeeTableAndDoorPlant()
    {
        var obstacles = new Dictionary<string, Rect>
        {
            { "waiting sofa", Rect.MinMaxRect(-9.9f, -5.3f, -4.7f, -3.625f) },
            { "coffee table", Rect.MinMaxRect(-8.5f, -2.95f, -5.3f, -1.75f) },
            { "door plant", Rect.MinMaxRect(-9.925f, -2.825f, -9.075f, -1.975f) },
            { "cashier counter", Rect.MinMaxRect(4.55f, -5.0f, 9.85f, -2.8f) }
        };
        var starts = new[]
        {
            new Vector3(-2.1f, 1.05f, -.22f),
            new Vector3(6.2f, 1.05f, .2f),
            new Vector3(3.9f, 1.05f, -2.2f),
            SalonWashAnnexLayout.WashCustomerAnchorPosition
        };
        foreach (Vector3 start in starts)
        {
            Vector3[] route = SalonCustomerPath.BuildLeavingRoute(start, SalonEntranceDoor.OutsideExit);
            Vector3 from = start;
            foreach (Vector3 to in route)
            {
                for (int i = 0; i <= 20; i++)
                {
                    Vector3 p = Vector3.Lerp(from, to, i / 20f);
                    foreach (KeyValuePair<string, Rect> obstacle in obstacles)
                        Assert.IsFalse(obstacle.Value.Contains(new Vector2(p.x, p.z)),
                            "Leaving from " + start + " crosses the " + obstacle.Key + " at " + p);
                }
                from = to;
            }
        }
    }

    [Test]
    public void RebuiltLeftWallKeepsTheCraftedLowCutawayAwayFromTheDoorFrame()
    {
        var parent = new GameObject("Door test parent");
        try
        {
            GameObject door = SalonEntranceDoor.Build(parent.transform, _ => null);
            Mesh mesh = door.GetComponent<MeshFilter>().sharedMesh;
            float frameReach = SalonEntranceDoor.OpeningWidth * .5f + .3f;
            foreach (Vector3 vertex in mesh.vertices)
            {
                if (Mathf.Abs(vertex.z - SalonEntranceDoor.DoorZ) <= frameReach) continue;
                Assert.LessOrEqual(vertex.y, SalonEntranceDoor.WallHeight + .081f,
                    "Only the door frame may rise above the crafted cutaway wall: " + vertex);
            }
            Assert.Greater(mesh.bounds.max.y, 2f, "The door frame must read as a door in the overview camera.");
        }
        finally
        {
            Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void DoorIsRegisteredInTheManifestWithCustomerAnchors()
    {
        HairSalon.AssetPipeline.AssetManifest manifest = HairSalon.AssetPipeline.AssetManifestLoader.LoadFromResources();
        Assert.IsEmpty(HairSalon.AssetPipeline.AssetManifestValidator.Validate(manifest));
        HairSalon.AssetPipeline.AssetDefinition door = manifest.Find(SalonEntranceDoor.RoomAssetId);
        Assert.IsNotNull(door);
        Assert.That(door.RequiredAnchors, Does.Contain("customer-inside"));
        Assert.That(door.RequiredAnchors, Does.Contain("customer-outside"));
    }

    private static Vector3 SalonDemoFallbackExit()
    {
        return new Vector3(-10.8f, 1.05f, 5.9f);
    }
}
