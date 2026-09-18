using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonMobileNavigationTests
{
    private static Type Navigation
    {
        get
        {
            Type type = typeof(SalonDemo).Assembly.GetType("SalonMobileNavigation");
            Assert.That(type, Is.Not.Null, "手游移动必须使用可验证的碰撞与距离规则");
            return type;
        }
    }

    private static Vector3 Move(Vector3 position, Vector2 input, float distance, List<Rect> obstacles)
    {
        return (Vector3)Navigation.GetMethod("Move").Invoke(null, new object[]
        {
            position, input, Vector3.right, Vector3.forward, distance,
            Rect.MinMaxRect(-10f, -6f, 10f, 7f), obstacles
        });
    }

    [Test]
    public void LargeFrameDoesNotTunnelThroughFurniture()
    {
        Vector3 result = Move(new Vector3(-3f, .05f, 0f), Vector2.right, 8f,
            new List<Rect> { Rect.MinMaxRect(-1f, -1f, 1f, 1f) });
        Assert.That(result.x, Is.LessThanOrEqualTo(-1f));
        Assert.That(result.y, Is.EqualTo(.05f));
    }

    [Test]
    public void DiagonalInputSlidesAlongFurnitureInsteadOfSticking()
    {
        Vector3 result = Move(new Vector3(-1.1f, .05f, 0f), Vector2.one, 1f,
            new List<Rect> { Rect.MinMaxRect(-1f, -2f, 1f, 2f) });
        Assert.That(result.x, Is.LessThanOrEqualTo(-1f));
        Assert.That(result.z, Is.GreaterThan(.5f));
    }

    [Test]
    public void DiagonalSpeedIsBoundedAndRoomEdgesCannotBeCrossed()
    {
        Vector3 diagonal = Move(Vector3.zero, Vector2.one, 1f, new List<Rect>());
        Assert.That(diagonal.magnitude, Is.EqualTo(1f).Within(.001f));
        Vector3 edge = Move(new Vector3(9.9f, 0f, 6.9f), Vector2.one, 3f, new List<Rect>());
        Assert.That(edge.x, Is.LessThanOrEqualTo(10f));
        Assert.That(edge.z, Is.LessThanOrEqualTo(7f));
    }

    [Test]
    public void ReachIgnoresSeatHeightButRejectsRemoteService()
    {
        MethodInfo method = Navigation.GetMethod("CanReach");
        Assert.That(method.Invoke(null, new object[] { Vector3.zero, new Vector3(1f, 2f, 0f), 1.5f }), Is.True);
        Assert.That(method.Invoke(null, new object[] { Vector3.zero, new Vector3(4f, 0f, 0f), 1.5f }), Is.False);
    }

    [Test]
    public void WaitingAndMovingCustomersShowTheirOrderWithoutSelection()
    {
        var game = new HairSalon.SalonGameModel();
        var customer = game.Spawn(44, new List<HairSalon.ServiceType> { HairSalon.ServiceType.Cut });
        Assert.That(SalonDemo.ShouldShowCustomerStatusAtStation(customer, false, false), Is.True);
        game.Tick(1f);
        Assert.That(SalonDemo.ShouldShowCustomerStatusAtStation(customer, false, true), Is.True);
        game.Assign(customer, 1);
        Assert.That(SalonDemo.ShouldShowCustomerStatusAtStation(customer, false, false), Is.True);
    }
}
