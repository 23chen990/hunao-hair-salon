using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class DevelopmentDebugTests
{
    [TestCase("https://localhost/?debug=1", true)]
    [TestCase("https://localhost/?debug=0", false)]
    [TestCase("", false)]
    public void DebugRequest_IsExplicitlyOptIn(string url, bool expected)
    {
        Assert.That(DevelopmentDebugOverlay.IsRequested(url, new string[0]), Is.EqualTo(expected));
    }

    [Test]
    public void Snapshot_ContainsRequiredOperationalContext()
    {
        var context = new DevelopmentDebugContext
        {
            Scene = "HairSalonDemo",
            CharacterPosition = new Vector3(1.25f, 0f, -2.5f),
            CharacterState = "Walk",
            ServiceState = "BUSINESS",
            TargetStation = "Haircut Workstation 1",
            Viewport = new Vector2Int(1920, 1080),
            BuildVersion = "0.8.0-dev"
        };

        string snapshot = DevelopmentDebugSnapshot.Serialize(context);

        StringAssert.Contains("\"scene\":\"HairSalonDemo\"", snapshot);
        StringAssert.Contains("\"characterPosition\"", snapshot);
        StringAssert.Contains("\"serviceState\":\"BUSINESS\"", snapshot);
        StringAssert.Contains("\"targetStation\":\"Haircut Workstation 1\"", snapshot);
        StringAssert.Contains("\"viewport\":\"1920x1080\"", snapshot);
        StringAssert.Contains("\"buildVersion\":\"0.8.0-dev\"", snapshot);
    }

    [Test]
    public void BrowserCoreFlowReport_OnlyPassesAfterCustomerCompletesAndPaymentExists()
    {
        var report = new BrowserCoreFlowReport
        {
            StartedBusiness = true,
            SpawnedCustomer = true,
            AssignedStation = true,
            CompletedService = true,
            CustomerFinished = true,
            PaymentCreated = true
        };

        Assert.That(report.Passed, Is.True);
        report.PaymentCreated = false;
        Assert.That(report.Passed, Is.False);
    }

    [Test]
    public void DiagnosticMaterial_IsTransparentSoCollisionDoesNotHideTheScene()
    {
        Material material = DevelopmentDebugOverlay.CreateDiagnosticMaterial(
            new Color(1f, .1f, .1f, .2f));

        Assert.That(material.shader, Is.EqualTo(Resources.Load<Shader>("SalonContactShadow")));
        Assert.That(material.color.a, Is.EqualTo(.2f).Within(.001f));
        Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000));
    }
}
