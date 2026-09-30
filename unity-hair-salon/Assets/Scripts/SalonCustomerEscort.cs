using System.Collections.Generic;
using UnityEngine;

/// <summary>A received customer follows the stylist's walked trail with room to stop.</summary>
public sealed class SalonCustomerEscort
{
    private const float FollowingDistance = 1.05f;
    private const float CornerClearance = .04f;
    private readonly List<Vector3> _trail = new List<Vector3>();
    private readonly List<Rect> _exitingFurniture = new List<Rect>();
    private readonly List<Rect> _blockingFurniture = new List<Rect>();
    private Vector3 _lastLeaderPoint;
    private float _speed;
    public Transform Leader { get; }

    public SalonCustomerEscort(Transform leader, Vector3 position, Rect floor, IReadOnlyList<Rect> obstacles)
    {
        Leader = leader;
        _lastLeaderPoint = OnFloor(leader.position);
        // A seated customer starts inside their own seat. Walk out of that
        // footprint once; every other item remains solid, including afterwards.
        foreach (Rect obstacle in obstacles)
            if (Contains(obstacle, position)) _exitingFurniture.Add(obstacle);
        RefreshObstacles(position, obstacles);
        JoinTrail(position, _lastLeaderPoint, floor);
    }

    public Vector3 Move(Vector3 position, float dt, Rect floor, IReadOnlyList<Rect> obstacles)
    {
        Vector3 point = OnFloor(Leader.position);
        RefreshObstacles(position, obstacles);
        if (Vector3.Distance(_lastLeaderPoint, point) >= .04f)
        {
            _trail.Add(point);
            _lastLeaderPoint = point;
        }
        if (_trail.Count == 0) JoinTrail(position, point, floor);
        else if (!ClearSegment(OnFloor(position), _trail[0], _blockingFurniture))
            JoinTrail(position, point, floor);
        if (dt <= 0f || _trail.Count == 0) return position;

        float length = 0f;
        Vector3 previous = OnFloor(position);
        foreach (Vector3 waypoint in _trail)
        {
            length += Vector3.Distance(previous, waypoint);
            previous = waypoint;
        }
        length += Vector3.Distance(previous, point);
        float available = Mathf.Max(0f, length - FollowingDistance);
        float desiredSpeed = available < .008f ? 0f : Mathf.Min(6f, available * 8f);
        _speed = Mathf.MoveTowards(_speed, desiredSpeed, 20f * dt);
        float travel = Mathf.Min(_speed * dt, available);
        Vector3 next = OnFloor(position);
        while (_trail.Count > 0 && travel > .00001f)
        {
            float distance = Vector3.Distance(next, _trail[0]);
            float step = Mathf.Min(travel, distance);
            next = Vector3.MoveTowards(next, _trail[0], step);
            travel -= step;
            if (step >= distance - .00001f) _trail.RemoveAt(0);
            else break;
        }
        next.y = Mathf.MoveTowards(position.y, .4f, 4f * dt);
        return next;
    }

    private void RefreshObstacles(Vector3 position, IReadOnlyList<Rect> obstacles)
    {
        _exitingFurniture.RemoveAll(rect => !Contains(rect, position));
        _blockingFurniture.Clear();
        foreach (Rect obstacle in obstacles)
            if (!_exitingFurniture.Contains(obstacle)) _blockingFurniture.Add(obstacle);
    }

    private void JoinTrail(Vector3 position, Vector3 destination, Rect floor)
    {
        _trail.Clear();
        _lastLeaderPoint = destination;
        Vector3 start = OnFloor(position);
        if (ClearSegment(start, destination, _blockingFurniture))
        {
            _trail.Add(destination);
            return;
        }

        // Only the initial join (or changed furniture) needs routing. Once
        // joined, keep the player's real corners rather than chasing through chairs.
        var points = new List<Vector3> { start, destination };
        foreach (Rect rect in _blockingFurniture)
            for (int corner = 0; corner < 4; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? rect.xMin - CornerClearance : rect.xMax + CornerClearance,
                    .4f, (corner & 2) == 0 ? rect.yMin - CornerClearance : rect.yMax + CornerClearance);
                if (floor.Contains(new Vector2(point.x, point.z)) &&
                    !_blockingFurniture.Exists(other => Contains(other, point))) points.Add(point);
            }
        var distance = new float[points.Count];
        var parent = new int[points.Count];
        var visited = new bool[points.Count];
        for (int i = 0; i < points.Count; i++) { distance[i] = float.PositiveInfinity; parent[i] = -1; }
        distance[0] = 0f;
        for (int pass = 0; pass < points.Count; pass++)
        {
            int nearest = -1;
            for (int i = 0; i < points.Count; i++)
                if (!visited[i] && !float.IsPositiveInfinity(distance[i]) &&
                    (nearest < 0 || distance[i] < distance[nearest])) nearest = i;
            if (nearest < 0 || nearest == 1) break;
            visited[nearest] = true;
            for (int i = 1; i < points.Count; i++)
            {
                if (visited[i]) continue;
                float candidate = distance[nearest] + Vector3.Distance(points[nearest], points[i]);
                if (candidate >= distance[i] || !ClearSegment(points[nearest], points[i], _blockingFurniture)) continue;
                distance[i] = candidate;
                parent[i] = nearest;
            }
        }
        if (parent[1] < 0) return;
        for (int i = 1; i != 0; i = parent[i]) _trail.Add(points[i]);
        _trail.Reverse();
    }

    private static bool ClearSegment(Vector3 from, Vector3 to, List<Rect> obstacles)
    {
        foreach (Rect rect in obstacles)
        {
            float enter = 0f, exit = 1f;
            if (IntersectsAxis(from.x, to.x - from.x, rect.xMin, rect.xMax, ref enter, ref exit) &&
                IntersectsAxis(from.z, to.z - from.z, rect.yMin, rect.yMax, ref enter, ref exit)) return false;
        }
        return true;
    }

    private static bool IntersectsAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < .00001f) return start >= min && start <= max;
        float first = (min - start) / delta, last = (max - start) / delta;
        enter = Mathf.Max(enter, Mathf.Min(first, last));
        exit = Mathf.Min(exit, Mathf.Max(first, last));
        return enter <= exit;
    }

    private static Vector3 OnFloor(Vector3 value) => new Vector3(value.x, .4f, value.z);
    private static bool Contains(Rect rect, Vector3 value) => rect.Contains(new Vector2(value.x, value.z));
}
