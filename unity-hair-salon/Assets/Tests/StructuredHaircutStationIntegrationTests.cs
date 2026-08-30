using System;
using System.Linq;
using System.Reflection;
using HairSalon.Character;
using HairSalon.CutStations;
using NUnit.Framework;
using UnityEngine;

public sealed class StructuredHaircutStationIntegrationTests
{
    [Test]
    public void MainHaircutZoneBuildsTwoInstancesFromOneStructuredAsset()
    {
        var ownerObject = new GameObject("Salon owner");
        var world = new GameObject("World");
        try
        {
            SalonDemo owner = ownerObject.AddComponent<SalonDemo>();
            MethodInfo build = typeof(SalonDemo).GetMethod(
                "BuildHaircutZone", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);

            build.Invoke(owner, new object[] { world.transform });

            CutStation[] stations = world.GetComponentsInChildren<CutStation>(true);
            Assert.That(stations, Has.Length.EqualTo(2));
            Assert.That(stations.Select(station => station.AssetId).Distinct().Single(),
                Is.EqualTo("cut-station-classic-poc"));
            CollectionAssert.AreEquivalent(
                new[] { CutStationOrientation.BackWall, CutStationOrientation.RightWall },
                stations.Select(station => station.Orientation));
            Assert.That(stations.All(station => station.Collision != null), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
            UnityEngine.Object.DestroyImmediate(world);
        }
    }

    [Test]
    public void CustomerServiceRouteCanPassThroughStationQueueAnchor()
    {
        MethodInfo route = typeof(SalonCustomerPath).GetMethod(
            "BuildServiceRoute",
            new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3) });
        Assert.That(route, Is.Not.Null,
            "Structured stations need their QueueAnchor in the actual approach route.");

        Vector3 start = new Vector3(-8f, 1f, -4f);
        Vector3 queue = new Vector3(2f, 1f, -1f);
        Vector3 seat = new Vector3(2f, 1f, 1f);
        var result = (Vector3[])route.Invoke(null, new object[] { start, queue, seat });

        Assert.That(result, Does.Contain(queue));
        Assert.That(result[result.Length - 1], Is.EqualTo(seat));
    }

    [Test]
    public void ServiceStartCheckRejectsAStylistWhoHasNotReachedTheWorkAnchor()
    {
        CutStation station = null;
        try
        {
            station = CutStationFactory.Create(
                CutStationDefaults.CreateBuiltinManifest(),
                "cut-station-classic-poc",
                Vector3.zero,
                CutStationOrientation.BackWall);

            CutStationAlignmentResult result = station.ValidateServiceStart(
                station.Layout.CustomerSeatAnchor,
                station.Layout.CustomerFacing,
                station.Layout.StylistWorkAnchor + Vector3.right,
                station.Layout.StylistFacing);

            Assert.That(result.IsAligned, Is.False);
            Assert.That(result.StylistPositionAligned, Is.False);
        }
        finally
        {
            if (station != null) UnityEngine.Object.DestroyImmediate(station.gameObject);
        }
    }

    [Test]
    public void HairdresserCanExplicitlyFaceTheCustomerAfterReachingTheAnchor()
    {
        MethodInfo face = typeof(HairdresserCharacter).GetMethod(
            "FaceTowards", new[] { typeof(Vector3) });
        Assert.That(face, Is.Not.Null,
            "Arrival at a work anchor must update both transform and character presentation facing.");
    }

    [Test]
    public void BuiltinStationUsesThePackagedSalonShaderInAPlayerBuild()
    {
        CutStation station = null;
        try
        {
            station = CutStationFactory.Create(CutStationDefaults.CreateBuiltinManifest(),
                "cut-station-classic-poc", Vector3.zero, CutStationOrientation.BackWall, null, true);
            Shader expected = Resources.Load<Shader>("SalonLowPoly");
            Shader expectedShadow = Resources.Load<Shader>("SalonContactShadow");

            Assert.That(expected, Is.Not.Null);
            Assert.That(expectedShadow, Is.Not.Null);
            Assert.That(station.GetComponentsInChildren<Renderer>(true)
                .All(renderer => renderer.sharedMaterial != null &&
                                 (renderer.sharedMaterial.shader == expected ||
                                  renderer.sharedMaterial.shader == expectedShadow)),
                Is.True, "Default editor materials can be stripped and render magenta in the standalone build.");
        }
        finally
        {
            if (station != null) UnityEngine.Object.DestroyImmediate(station.gameObject);
        }
    }

    [Test]
    public void StructuredCollisionCanRejectACharacterStepThroughFurniture()
    {
        MethodInfo blocked = typeof(SalonDemo).GetMethod("IsCutStationMovementBlocked",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(blocked, Is.Not.Null);

        ResolvedCutStationLayout layout = CutStationLayoutResolver.Resolve(
            CutStationDefaults.CreateBuiltinDefinition(), CutStationOrientation.BackWall, Vector3.zero);
        bool inside = (bool)blocked.Invoke(null, new object[] { layout, new Vector3(0f, 0f, .18f) });
        bool atStylistAnchor = (bool)blocked.Invoke(null,
            new object[] { layout, layout.StylistWorkAnchor });

        Assert.That(inside, Is.True);
        Assert.That(atStylistAnchor, Is.False);
    }
}
