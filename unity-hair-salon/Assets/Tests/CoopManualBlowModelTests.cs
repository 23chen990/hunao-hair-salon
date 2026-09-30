using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;

/// <summary>Player-owned manual blow-dry model behavior for local co-op.</summary>
public sealed class CoopManualBlowModelTests
{
    [Test]
    public void PlayerTwoCanStartTickAndFinishManualBlowWithoutStand()
    {
        SalonGameModel game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 2101, 1);

        Assert.IsFalse(game.HasAutoBlowStand);
        Assert.IsTrue(InvokeBool(game, "StartManualBlowForPlayer", 2, customer),
            "P2 must be able to use the handheld dryer before the automatic stand is built.");
        Assert.AreEqual(2, customer.InteractionOwnerPlayerId);
        Assert.IsTrue(customer.ManualBlowHolding);

        Assert.IsTrue(InvokeBool(game, "TickManualBlowForPlayer", 2, customer,
            game.ServiceConfig.ManualBlowGoodStart + .1f));
        BlowResult result = Invoke<BlowResult>(game, "EndManualBlowHoldForPlayer", 2, customer);

        Assert.AreEqual(BlowResult.Good, result);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(-1, customer.InteractionOwnerPlayerId);
    }

    [Test]
    public void PlayerScopedManualBlowPreventsStealingAndAllowsTwoCustomersInParallel()
    {
        SalonGameModel game = new SalonGameModel();
        CustomerModel first = SpawnServing(game, 2102, 1);
        CustomerModel second = SpawnServing(game, 2103, 2);

        Assert.IsTrue(InvokeBool(game, "StartManualBlowForPlayer", 2, first));
        Assert.IsTrue(InvokeBool(game, "StartManualBlowForPlayer", 3, second));
        Assert.IsFalse(InvokeBool(game, "StartManualBlowForPlayer", 1, first),
            "A different player must not take over an active manual blow.");
        Assert.IsFalse(InvokeBool(game, "TickManualBlowForPlayer", 1, first, .1f));
        Assert.IsFalse(InvokeBlowResult(game, "EndManualBlowHoldForPlayer", 1, first));
        Assert.IsTrue(first.ManualBlowHolding);
        Assert.AreEqual(2, first.InteractionOwnerPlayerId);

        Assert.IsTrue(InvokeBool(game, "TickManualBlowForPlayer", 2, first,
            game.ServiceConfig.ManualBlowGoodStart + .1f));
        Assert.IsTrue(InvokeBool(game, "TickManualBlowForPlayer", 3, second,
            game.ServiceConfig.ManualBlowGoodStart + .1f));
        Assert.AreEqual(BlowResult.Good, Invoke<BlowResult>(game,
            "EndManualBlowHoldForPlayer", 2, first));
        Assert.AreEqual(BlowResult.Good, Invoke<BlowResult>(game,
            "EndManualBlowHoldForPlayer", 3, second));
        Assert.AreEqual(CustomerState.Finished, first.State);
        Assert.AreEqual(CustomerState.Finished, second.State);
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { ServiceType.Dry });
        Assert.IsNotNull(customer);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }

    private static bool InvokeBool(object target, string name, params object[] args)
    {
        MethodInfo method = FindMethod(target, name);
        return (bool)method.Invoke(target, args);
    }

    private static T Invoke<T>(object target, string name, params object[] args)
    {
        MethodInfo method = FindMethod(target, name);
        return (T)method.Invoke(target, args);
    }

    private static bool InvokeBlowResult(object target, string name, params object[] args)
    {
        return Invoke<BlowResult>(target, name, args) != BlowResult.None;
    }

    private static MethodInfo FindMethod(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing player-scoped manual blow method: " + name);
        return method;
    }
}
