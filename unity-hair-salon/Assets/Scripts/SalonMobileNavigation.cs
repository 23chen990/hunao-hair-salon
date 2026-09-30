using System.Collections.Generic;
using HairSalon.AssetPipeline;
using UnityEngine;

/// <summary>Planar joystick motion, with small collision steps and wall sliding.</summary>
public static class SalonMobileNavigation
{
    public static bool CanReach(Vector3 player, Vector3 anchor, float radius)
    {
        return new Vector2(player.x - anchor.x, player.z - anchor.z).sqrMagnitude <= radius * radius;
    }

    public static Vector3 Move(Vector3 position, Vector2 input, Vector3 cameraRight,
        Vector3 cameraForward, float distance, Rect floor, IReadOnlyList<Rect> obstacles)
    {
        input = Vector2.ClampMagnitude(input, 1f);
        cameraRight.y = cameraForward.y = 0f;
        Vector3 delta = (cameraRight.normalized * input.x + cameraForward.normalized * input.y)
                        * Mathf.Max(0f, distance);
        int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / .08f));
        delta /= steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 next = position + new Vector3(delta.x, 0f, 0f);
            next.x = Mathf.Clamp(next.x, floor.xMin, floor.xMax);
            if (!Blocked(next, obstacles)) position = next;
            next = position + new Vector3(0f, 0f, delta.z);
            next.z = Mathf.Clamp(next.z, floor.yMin, floor.yMax);
            if (!Blocked(next, obstacles)) position = next;
        }
        return position;
    }

    /// <summary>
    /// Returns the nearest free floor point when new furniture appears on top
    /// of a player; Move never leaves a blocked start on its own.
    /// </summary>
    public static Vector3 ResolveOverlap(Vector3 position, Rect floor, IReadOnlyList<Rect> obstacles)
    {
        if (!Blocked(position, obstacles)) return position;
        const float ringStep = .1f;
        const int directions = 16;
        for (int ring = 1; ring <= 40; ring++)
        {
            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                Vector3 candidate = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * ringStep);
                candidate.x = Mathf.Clamp(candidate.x, floor.xMin, floor.xMax);
                candidate.z = Mathf.Clamp(candidate.z, floor.yMin, floor.yMax);
                if (!Blocked(candidate, obstacles)) return candidate;
            }
        }
        return position;
    }

    private static bool Blocked(Vector3 point, IReadOnlyList<Rect> obstacles)
    {
        for (int i = 0; i < obstacles.Count; i++)
            if (obstacles[i].Contains(new Vector2(point.x, point.z))) return true;
        return false;
    }
}

/// <summary>Shares an existing manifest collision with joystick navigation.</summary>
public sealed class SalonFurnitureObstacle : MonoBehaviour
{
    public string AssetId { get; private set; }
    private AssetArea _collision;
    private AssetArea _footprint;

    public void Initialize(AssetDefinition asset)
    {
        AssetId = asset.Id;
        _collision = asset.Collision;
        _footprint = asset.Footprint;
    }

    public Rect WorldBounds(float radius)
    {
        Vector3 center = transform.TransformPoint(new Vector3(_collision.Center.x, 0f, _collision.Center.y));
        Vector2 half = _collision.Size * .5f + Vector2.one * radius;
        return Rect.MinMaxRect(center.x - half.x, center.z - half.y, center.x + half.x, center.z + half.y);
    }

    public float DistanceToFootprint(Vector3 worldPoint)
    {
        AssetArea footprint = _footprint ?? _collision;
        Vector3 center = transform.TransformPoint(new Vector3(footprint.Center.x, 0f, footprint.Center.y));
        Vector2 half = footprint.Size * .5f;
        float dx = Mathf.Max(0f, Mathf.Abs(worldPoint.x - center.x) - half.x);
        float dz = Mathf.Max(0f, Mathf.Abs(worldPoint.z - center.z) - half.y);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
