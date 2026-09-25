using System.Collections.Generic;
using System.Reflection;
using HairSalon.CutStations;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class CustomerExitRouteRegressionTests
{
    private const float CustomerWalkingSpeed = 5.2f;

    [Test]
    public void LeavingRouteFromFormalBlowWorkstationAvoidsBothHaircutStationCollisions()
    {
        var ownerObject = new GameObject("Salon owner");
        var world = new GameObject("World");
        try
        {
            SalonDemo owner = ownerObject.AddComponent<SalonDemo>();
            MethodInfo build = typeof(SalonDemo).GetMethod(
                "BuildHaircutZone", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null, "The formal demo haircut zone builder must remain available.");
            build.Invoke(owner, new object[] { world.transform });

            CutStation[] stations = world.GetComponentsInChildren<CutStation>(true);
            Assert.That(stations, Has.Length.EqualTo(2), "The formal demo uses two haircut stations.");
            CutStation rightWallStation = null;
            foreach (CutStation station in stations)
                if (station.Orientation == CutStationOrientation.RightWall)
                {
                    rightWallStation = station;
                    break;
                }
            Assert.That(rightWallStation, Is.Not.Null,
                "The formal demo must expose the right-wall station used for Day 1 blow-drying.");

            Vector3 start = rightWallStation.CustomerSeatAnchor.position;
            Vector3 exit = ReadPrivateStaticVector3(typeof(SalonDemo), "ExitPosition");
            Vector3[] route = SalonCustomerPath.BuildLeavingRoute(start, exit);
            var violations = new List<string>();
            Vector3 segmentStart = start;
            for (int waypoint = 0; waypoint <= route.Length; waypoint++)
            {
                Vector3 segmentEnd = waypoint == route.Length ? exit : route[waypoint];
                for (int stationIndex = 0; stationIndex < stations.Length; stationIndex++)
                {
                    CutStation station = stations[stationIndex];
                    if (SegmentIntersectsRect(segmentStart, segmentEnd,
                        station.Layout.Collision, station.Layout.Origin))
                    {
                        string stationSide = station.Orientation == CutStationOrientation.RightWall
                            ? "right-wall"
                            : "back-wall";
                        violations.Add($"route segment {waypoint + 1} crosses the {stationSide} haircut station collision");
                    }
                }
                segmentStart = segmentEnd;
            }

            Assert.That(violations, Is.Empty,
                "Leaving route from the formal Day 1 blow workstation: " + string.Join("; ", violations));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(world);
        }
    }

    [Test]
    public void LeavingRoutesFromBothFormalHaircutSeatsStayWithinTimeBudgetAndAvoidFurniture()
    {
        var ownerObject = new GameObject("Salon owner");
        var world = new GameObject("World");
        try
        {
            SalonDemo owner = ownerObject.AddComponent<SalonDemo>();
            MethodInfo build = typeof(SalonDemo).GetMethod(
                "BuildHaircutZone", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null, "The formal demo haircut zone builder must remain available.");
            build.Invoke(owner, new object[] { world.transform });
            MethodInfo buildFurniture = typeof(SalonDemo).GetMethod(
                "BuildShelvesAndPlants", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildFurniture, Is.Not.Null, "The formal demo furniture builder must remain available.");
            buildFurniture.Invoke(owner, new object[] { world.transform });

            CutStation[] stations = world.GetComponentsInChildren<CutStation>(true);
            Assert.That(stations, Has.Length.EqualTo(2), "The formal demo uses two haircut stations.");
            Vector3 exit = ReadPrivateStaticVector3(typeof(SalonDemo), "ExitPosition");
            SalonFurnitureObstacle exitPlant = null;
            foreach (SalonFurnitureObstacle obstacle in world.GetComponentsInChildren<SalonFurnitureObstacle>(true))
                if (obstacle.AssetId == "prop-potted-plant" &&
                    Vector2.Distance(new Vector2(obstacle.transform.position.x, obstacle.transform.position.z),
                        new Vector2(exit.x, exit.z)) < 2f)
                {
                    exitPlant = obstacle;
                    break;
                }
            Assert.That(exitPlant, Is.Not.Null, "The exit plant collision must come from the formal demo furniture builder.");
            Rect plantBounds = exitPlant.WorldBounds(0f);

            var violations = new List<string>();
            for (int stationIndex = 0; stationIndex < stations.Length; stationIndex++)
            {
                CutStation currentStation = stations[stationIndex];
                Vector3 start = currentStation.CustomerSeatAnchor.position;
                Vector3[] route = SalonCustomerPath.BuildLeavingRoute(start, exit);
                float routeLength = HorizontalDistance(start, route[0]);
                for (int waypoint = 1; waypoint < route.Length; waypoint++)
                    routeLength += HorizontalDistance(route[waypoint - 1], route[waypoint]);
                routeLength += HorizontalDistance(route[route.Length - 1], exit);
                float timeBudgetDistance = CustomerWalkingSpeed * SalonGameModel.LeavingSeconds;
                if (routeLength > timeBudgetDistance + .001f)
                    violations.Add($"station {stationIndex + 1} route is {routeLength:F2} units, over the {timeBudgetDistance:F2}-unit leaving budget");

                Vector3 segmentStart = start;
                for (int waypoint = 0; waypoint <= route.Length; waypoint++)
                {
                    Vector3 segmentEnd = waypoint == route.Length ? exit : route[waypoint];
                    for (int otherIndex = 0; otherIndex < stations.Length; otherIndex++)
                    {
                        if (otherIndex == stationIndex) continue;
                        if (SegmentIntersectsRect(segmentStart, segmentEnd,
                            stations[otherIndex].Layout.Collision, stations[otherIndex].Layout.Origin))
                            violations.Add($"station {stationIndex + 1} route segment {waypoint + 1} crosses haircut station {otherIndex + 1} collision");
                    }

                    if (SegmentIntersectsBounds(segmentStart, segmentEnd, plantBounds))
                        violations.Add($"station {stationIndex + 1} route segment {waypoint + 1} crosses the exit plant collision");
                    segmentStart = segmentEnd;
                }
            }

            Assert.That(violations, Is.Empty,
                "Leaving route regression: " + string.Join("; ", violations));
        }
        finally
        {
            Object.DestroyImmediate(ownerObject);
            Object.DestroyImmediate(world);
        }
    }

    private static Vector3 ReadPrivateStaticVector3(System.Type declaringType, string fieldName)
    {
        FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected demo exit destination field " + fieldName + ".");
        return (Vector3)field.GetValue(null);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static bool SegmentIntersectsRect(
        Vector3 segmentStart,
        Vector3 segmentEnd,
        ResolvedCutStationRect rect,
        Vector3 origin = default)
    {
        float minX = origin.x + rect.Center.x - rect.Size.x * .5f;
        float maxX = origin.x + rect.Center.x + rect.Size.x * .5f;
        float minZ = origin.z + rect.Center.y - rect.Size.y * .5f;
        float maxZ = origin.z + rect.Center.y + rect.Size.y * .5f;
        float tMin = 0f;
        float tMax = 1f;
        return ClipAxis(segmentStart.x, segmentEnd.x - segmentStart.x, minX, maxX, ref tMin, ref tMax) &&
               ClipAxis(segmentStart.z, segmentEnd.z - segmentStart.z, minZ, maxZ, ref tMin, ref tMax) &&
               tMin <= tMax;
    }

    private static bool SegmentIntersectsBounds(Vector3 segmentStart, Vector3 segmentEnd, Rect bounds)
    {
        var rect = new ResolvedCutStationRect(
            new Vector2(bounds.center.x, bounds.center.y),
            new Vector2(bounds.width, bounds.height));
        return SegmentIntersectsRect(segmentStart, segmentEnd, rect);
    }

    private static bool ClipAxis(float start, float delta, float min, float max, ref float tMin, ref float tMax)
    {
        if (Mathf.Abs(delta) < .0001f) return start >= min && start <= max;
        float first = (min - start) / delta;
        float second = (max - start) / delta;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }
        tMin = Mathf.Max(tMin, first);
        tMax = Mathf.Min(tMax, second);
        return tMin <= tMax;
    }
}
