using System.Collections.Generic;
using HairSalon.CutStations;
using UnityEngine;

/// <summary>Visibility route around the existing, resolved station collision rectangles.</summary>
public static class SalonPlayerRoute
{
    public static Vector3[] Build(Vector3 start, Vector3 destination, IReadOnlyList<ResolvedCutStationLayout> layouts)
    {
        destination.y = start.y;
        var obstacles = new List<Rect>();
        foreach (var layout in layouts)
        {
            if (layout == null) continue;
            Vector2 center = new Vector2(layout.Origin.x, layout.Origin.z) + layout.Collision.Center;
            Vector2 half = layout.Collision.Size * .5f + Vector2.one * .12f;
            obstacles.Add(Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y));
        }
        var nodes = new List<Vector3> { start, destination };
        foreach (Rect rect in obstacles)
        {
            nodes.Add(new Vector3(rect.xMin, start.y, rect.yMin));
            nodes.Add(new Vector3(rect.xMin, start.y, rect.yMax));
            nodes.Add(new Vector3(rect.xMax, start.y, rect.yMin));
            nodes.Add(new Vector3(rect.xMax, start.y, rect.yMax));
        }
        int n = nodes.Count;
        var costs = new float[n];
        var previous = new int[n];
        var visited = new bool[n];
        for (int i = 0; i < n; i++) { costs[i] = float.PositiveInfinity; previous[i] = -1; }
        costs[0] = 0f;
        for (int iteration = 0; iteration < n; iteration++)
        {
            int current = -1;
            for (int i = 0; i < n; i++)
                if (!visited[i] && (current < 0 || costs[i] < costs[current])) current = i;
            if (current < 0 || float.IsPositiveInfinity(costs[current])) break;
            if (current == 1) break;
            visited[current] = true;
            for (int next = 0; next < n; next++)
            {
                if (visited[next] || next == current || !Visible(nodes[current], nodes[next], obstacles)) continue;
                float cost = costs[current] + Vector3.Distance(nodes[current], nodes[next]);
                if (cost >= costs[next]) continue;
                costs[next] = cost;
                previous[next] = current;
            }
        }
        if (previous[1] < 0) return new Vector3[0];
        var result = new List<Vector3>();
        for (int node = 1; node != 0; node = previous[node]) result.Add(nodes[node]);
        result.Reverse();
        return result.ToArray();
    }

    private static bool Visible(Vector3 start, Vector3 end, List<Rect> obstacles)
    {
        foreach (Rect rect in obstacles)
        {
            float near = 0f, far = 1f;
            if (Clip(start.x, end.x - start.x, rect.xMin, rect.xMax, ref near, ref far) &&
                Clip(start.z, end.z - start.z, rect.yMin, rect.yMax, ref near, ref far) &&
                far - near > .00001f) return false;
        }
        return true;
    }

    private static bool Clip(float origin, float delta, float minimum, float maximum, ref float near, ref float far)
    {
        if (Mathf.Abs(delta) < .00001f) return origin > minimum + .00001f && origin < maximum - .00001f;
        float a = (minimum - origin) / delta;
        float b = (maximum - origin) / delta;
        near = Mathf.Max(near, Mathf.Min(a, b));
        far = Mathf.Min(far, Mathf.Max(a, b));
        return near < far - .00001f;
    }
}
