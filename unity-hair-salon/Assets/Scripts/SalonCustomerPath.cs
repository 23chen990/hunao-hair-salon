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
        return new[]
        {
            new Vector3(-9.55f, start.y, 1.35f),
            new Vector3(-9.55f, destination.y, 5.9f),
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
