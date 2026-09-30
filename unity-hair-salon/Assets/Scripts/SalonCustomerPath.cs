using System.Collections.Generic;
using UnityEngine;

/// <summary>Small fixed-lane waypoint policy for the sealed phase-4 salon map.</summary>
public static class SalonCustomerPath
{
    public static Vector3[] BuildEnteringRoute(Vector3 start, Vector3 destination)
    {
        return new[]
        {
            new Vector3(start.x, start.y, SalonEntranceDoor.DoorZ),
            SalonEntranceDoor.OutsidePoint(start.y),
            SalonEntranceDoor.InsidePoint(start.y),
            destination
        };
    }

    public static Vector3[] BuildServiceRoute(Vector3 start, Vector3 destination)
    {
        return BuildServiceRoute(start, destination, destination);
    }

    public static Vector3[] BuildServiceRoute(Vector3 start, Vector3 queueAnchor, Vector3 destination)
    {
        bool fromAnnex = SalonWashAnnexLayout.Contains(start);
        bool toAnnex = SalonWashAnnexLayout.Contains(destination);
        if (!fromAnnex && !toAnnex)
        {
            return new[]
            {
                new Vector3(start.x, start.y, SalonWashAnnexLayout.ServiceLaneZ),
                new Vector3(queueAnchor.x, destination.y, SalonWashAnnexLayout.ServiceLaneZ),
                queueAnchor,
                destination
            };
        }

        // The annex is reached only through the opening in the old right wall.
        var route = new List<Vector3>(8);
        if (fromAnnex) AddAnnexExit(route, start, SalonWashAnnexLayout.ServiceLaneZ);
        else route.Add(new Vector3(start.x, start.y, SalonWashAnnexLayout.ServiceLaneZ));
        if (toAnnex)
        {
            float y = destination.y;
            route.Add(new Vector3(SalonWashAnnexLayout.PortalX, y, SalonWashAnnexLayout.ServiceLaneZ));
            route.Add(new Vector3(SalonWashAnnexLayout.PortalX, y, SalonWashAnnexLayout.LaneZ));
            route.Add(new Vector3(destination.x, y, SalonWashAnnexLayout.LaneZ));
        }
        else route.Add(new Vector3(queueAnchor.x, destination.y, SalonWashAnnexLayout.ServiceLaneZ));
        route.Add(queueAnchor);
        route.Add(destination);
        return route.ToArray();
    }

    public static Vector3[] BuildLeavingRoute(Vector3 start, Vector3 destination)
    {
        var route = new List<Vector3>(10);
        bool inQueueRow = IsInQueueRow(start);
        if (SalonWashAnnexLayout.Contains(start))
        {
            AddAnnexExit(route, start, SalonWashAnnexLayout.LeavingLaneZ);
            route.Add(new Vector3(TableBypassX, start.y, SalonWashAnnexLayout.LeavingLaneZ));
            route.Add(new Vector3(TableBypassX, start.y, FrontLaneZ));
        }
        else if (IsInFrontAisle(start))
        {
            if (!inQueueRow) route.Add(new Vector3(start.x, start.y, FrontLaneZ));
        }
        else
        {
            // The low lane clears both chair collision footprints; the bypass
            // then passes east of the coffee table before turning to the door.
            float lowLaneZ = start.x > 0f ? SalonWashAnnexLayout.LeavingLaneZ : -.45f;
            route.Add(new Vector3(start.x, start.y, lowLaneZ));
            route.Add(new Vector3(TableBypassX, start.y, lowLaneZ));
            route.Add(new Vector3(TableBypassX, start.y, FrontLaneZ));
        }
        if (!inQueueRow) route.Add(new Vector3(SalonEntranceDoor.DoorInside.x, start.y, FrontLaneZ));
        route.Add(SalonEntranceDoor.InsidePoint(start.y));
        route.Add(SalonEntranceDoor.OutsidePoint(start.y));
        route.Add(SalonEntranceDoor.OutsideExitPoint(destination.y));
        return route.ToArray();
    }

    public static Vector3[] BuildCheckoutRoute(Vector3 start, Vector3 slot)
    {
        var exit = BuildLeavingRoute(start, SalonEntranceDoor.OutsideExitPoint(slot.y));
        var route = new List<Vector3>();
        for (int i = 0; i < exit.Length - 4; i++) route.Add(exit[i]);
        route.Add(new Vector3(slot.x, slot.y, FrontLaneZ));
        route.Add(slot);
        return route.ToArray();
    }

    // The waiting sofa (x -9.9..-4.7, z -5.3..-3.625), coffee table
    // (x -8.5..-5.3, z -2.95..-1.75) and the plant beside the door leave one
    // clear west-east lane between the sofa back and the table.
    public const float FrontLaneZ = -3.29f;
    public const float TableBypassX = -4.85f;
    public const float QueueRowMaxZ = -3.7f;
    // South of both chairs and west of the cashier counter.
    public const float FrontAisleMaxZ = -1.6f;
    public const float FrontAisleMaxX = 4.55f;

    public static bool IsInFrontAisle(Vector3 point)
        => point.z <= FrontAisleMaxZ && point.x <= FrontAisleMaxX;

    /// <summary>Queue spots and seats sit on the door's own row and walk straight out.</summary>
    public static bool IsInQueueRow(Vector3 point)
        => point.z <= QueueRowMaxZ && point.x <= FrontAisleMaxX;

    private static void AddAnnexExit(List<Vector3> route, Vector3 start, float mainLaneZ)
    {
        route.Add(new Vector3(start.x, start.y, SalonWashAnnexLayout.LaneZ));
        route.Add(new Vector3(SalonWashAnnexLayout.PortalX, start.y, SalonWashAnnexLayout.LaneZ));
        route.Add(new Vector3(SalonWashAnnexLayout.PortalX, start.y, mainLaneZ));
    }
}

public static class CoinPickupLayout
{
    public static Vector3 OffsetFor(int dropId)
    {
        int index = Mathf.Max(0, dropId - 1);
        float angle = index * 137.5f * Mathf.Deg2Rad;
        float radius = .32f + (index % 3) * .14f;
        return new Vector3(Mathf.Cos(angle) * radius, .08f, -1.05f + Mathf.Sin(angle) * radius);
    }
}
