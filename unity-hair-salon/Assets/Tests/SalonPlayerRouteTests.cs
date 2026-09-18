using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon.CutStations;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonPlayerRouteTests
{
    private static readonly Vector3 Start = new Vector3(0f, .05f, -3.2f);
    private static ResolvedCutStationLayout[] Layouts()
    {
        var definition = CutStationDefaults.CreateBuiltinDefinition();
        return new[] {
            CutStationLayoutResolver.Resolve(definition, CutStationOrientation.BackWall, new Vector3(-2.1f, .2f, .2f)),
            CutStationLayoutResolver.Resolve(definition, CutStationOrientation.RightWall, new Vector3(2.2f, .2f, .2f))
        };
    }
    private static Vector3[] Plan(Vector3 start, Vector3 target, ResolvedCutStationLayout[] layouts)
    {
        Type type = Type.GetType("SalonPlayerRoute, HairSalon.Runtime");
        Assert.That(type, Is.Not.Null, "The stylist needs a route around the central station instead of retrying the same blocked step.");
        return (Vector3[])type.GetMethod("Build", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { start, target, layouts });
    }
    [TestCase(false)]
    [TestCase(true)]
    public void StylistReachesEitherWashAnchorWithoutCrossingCentralStationCollisions(bool second)
    {
        var layouts = Layouts();
        Vector3 destination = second ? SalonDemo.SecondaryWashPlayerAnchorPosition : SalonDemo.WashPlayerAnchorPosition;
        destination.y = Start.y;
        var route = Plan(Start, destination, layouts);
        Assert.That(route.Length, Is.GreaterThan(0));
        Vector3 position = Start;
        foreach (Vector3 waypoint in route)
        {
            for (int step = 0; step < 400 && Vector3.Distance(position, waypoint) > .001f; step++)
            {
                position = Vector3.MoveTowards(position, waypoint, .1f);
                foreach (var layout in layouts)
                    Assert.That(SalonDemo.IsCutStationMovementBlocked(layout, position), Is.False, "Route crosses the existing collision.");
            }
        }
        Assert.That(Vector3.Distance(position, destination), Is.LessThan(.02f));
    }
    [Test]
    public void FreeMovementRemainsDirect()
    {
        var end = new Vector3(5f, Start.y, -3.2f);
        Assert.That(Plan(Start, end, Layouts()), Is.EqualTo(new[] { end }));
    }
}
