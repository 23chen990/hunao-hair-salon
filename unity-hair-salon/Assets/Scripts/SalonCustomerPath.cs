using UnityEngine;

/// <summary>Small fixed-lane waypoint policy for the sealed phase-4 salon map.</summary>
public static class SalonCustomerPath
{
    public static Vector3[] BuildEnteringRoute(Vector3 start, Vector3 destination)
    {
        return new[]
        {
            new Vector3(-9.65f, start.y, -4.75f),
            destination
        };
    }

    public static Vector3[] BuildServiceRoute(Vector3 start, Vector3 destination)
    {
        return BuildServiceRoute(start, destination, destination);
    }

    public static Vector3[] BuildServiceRoute(Vector3 start, Vector3 queueAnchor, Vector3 destination)
    {
        return new[]
        {
            new Vector3(start.x, start.y, -1.35f),
            new Vector3(destination.x, destination.y, -1.35f),
            queueAnchor,
            destination
        };
    }

    public static Vector3[] BuildLeavingRoute(Vector3 start, Vector3 destination)
    {
        // Leave both formal haircut seats through the low lane. The old
        // diagonal to x=-9.55 crossed the other chair and then climbed through
        // the exit plant, so the view's movement guard rolled the customer
        // back every frame. The right-wall seat must descend in place before
        // moving left; its collision footprint extends to z=-.70.
        float lowLaneZ = start.x > 0f ? -.90f : -.45f;
        return new[]
        {
            new Vector3(start.x, start.y, lowLaneZ),
            new Vector3(-10.0f, start.y, lowLaneZ),
            new Vector3(-10.0f, destination.y, 4.85f),
            destination
        };
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
