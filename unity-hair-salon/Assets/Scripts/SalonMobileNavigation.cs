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

    public void Initialize(AssetDefinition asset)
    {
        AssetId = asset.Id;
        _collision = asset.Collision;
    }

    public Rect WorldBounds(float radius)
    {
        Vector3 center = transform.TransformPoint(new Vector3(_collision.Center.x, 0f, _collision.Center.y));
        Vector2 half = _collision.Size * .5f + Vector2.one * radius;
        return Rect.MinMaxRect(center.x - half.x, center.z - half.y, center.x + half.x, center.z + half.y);
    }
}
