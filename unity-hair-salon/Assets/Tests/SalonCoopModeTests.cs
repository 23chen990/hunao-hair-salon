using System;
using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RED-phase contracts for the local same-device co-op layer.
/// Reflection keeps the first run meaningful while the new public gateway is absent.
/// </summary>
public sealed class SalonCoopModeTests
{
    [Test]
    public void MultiplayerEntryIsOptInAndSupportsBrowserOrEditorLaunch()
    {
        Assert.IsTrue(SalonDemo.IsLocalCoopRequested(
            "http://localhost/WebGLDemo/?multiplayer=1", Array.Empty<string>()));
        Assert.IsTrue(SalonDemo.IsLocalCoopRequested(
            string.Empty, new[] { "-salonMultiplayer" }));
        Assert.IsFalse(SalonDemo.IsLocalCoopRequested(
            "http://localhost/WebGLDemo/", Array.Empty<string>()));
    }

    [Test]
    public void KeyboardMappingKeepsPlayerOneAndPlayerTwoKeysIndependent()
    {
        Type type = RequireType("SalonCoopInputMap");
        MethodInfo move = RequireMethod(type, "ResolveKeyboardMove", typeof(int), typeof(KeyCode));
        MethodInfo held = RequireMethod(type, "IsInteractionHeld", typeof(int), typeof(KeyCode));

        Assert.AreEqual(Vector2.up, (Vector2)move.Invoke(null, new object[] { 1, KeyCode.W }));
        Assert.AreEqual(Vector2.zero, (Vector2)move.Invoke(null, new object[] { 1, KeyCode.UpArrow }));
        Assert.AreEqual(Vector2.up, (Vector2)move.Invoke(null, new object[] { 2, KeyCode.UpArrow }));
        Assert.AreEqual(Vector2.zero, (Vector2)move.Invoke(null, new object[] { 2, KeyCode.W }));
        Assert.IsTrue((bool)held.Invoke(null, new object[] { 1, KeyCode.Space }));
        Assert.IsFalse((bool)held.Invoke(null, new object[] { 1, KeyCode.RightControl }));
        Assert.IsTrue((bool)held.Invoke(null, new object[] { 2, KeyCode.RightControl }));
        Assert.IsTrue((bool)held.Invoke(null, new object[] { 2, KeyCode.KeypadEnter }));
        Assert.IsFalse((bool)held.Invoke(null, new object[] { 2, KeyCode.Space }));
    }

    [Test]
    public void DifferentCustomersCanRunForegroundOperationsForDifferentPlayers()
    {
        SalonGameModel game = CreateServingHaircutPair(out CustomerModel first, out CustomerModel second);
        HaircutConfig config = new HaircutConfig();

        Assert.IsTrue(game.BeginHaircutAction(1, first, SalonTool.Scissors, config));
        Assert.IsTrue(game.BeginHaircutAction(2, second, SalonTool.Scissors, config));
        Assert.AreEqual(1, first.InteractionOwnerPlayerId);
        Assert.AreEqual(2, second.InteractionOwnerPlayerId);
        Assert.AreNotEqual(first.InteractionOwnerPlayerId, second.InteractionOwnerPlayerId);
    }

    [Test]
    public void SameCustomerCannotBeOperatedByBothPlayersAndOwnershipReleases()
    {
        SalonGameModel game = CreateServingHaircutPair(out CustomerModel first, out _);
        HaircutConfig config = new HaircutConfig();

        Assert.IsTrue(game.BeginHaircutAction(1, first, SalonTool.Scissors, config));
        Assert.IsFalse(game.BeginHaircutAction(2, first, SalonTool.Scissors, config));
        Assert.AreEqual(1, first.InteractionOwnerPlayerId);

        game.CompleteHaircutAction(1, first, 0f, true);
        game.EndActiveOperation(1, first);
        Assert.AreEqual(-1, first.InteractionOwnerPlayerId);
        Assert.IsTrue(game.BeginHaircutAction(2, first, SalonTool.Scissors, config));
        Assert.AreEqual(2, first.InteractionOwnerPlayerId);
    }

    [Test]
    public void PlayerContextsKeepToolAndActionSelectionIndependent()
    {
        SalonGameModel game = new SalonGameModel();
        CustomerModel first = game.Spawn(701, new List<ServiceType> { ServiceType.Cut });
        CustomerModel second = game.Spawn(702, new List<ServiceType> { ServiceType.Cut });
        PlayerContext playerOne = game.GetOrCreatePlayerContext(1);
        PlayerContext playerTwo = game.GetOrCreatePlayerContext(2);

        game.SelectCustomer(1, first);
        game.SelectCustomer(2, second);
        playerOne.SelectServiceAction(first, ActiveServiceAction.None, SalonTool.Scissors);
        playerTwo.SelectServiceAction(second, ActiveServiceAction.ManualBlow, SalonTool.BlowDryer);

        Assert.AreEqual(SalonTool.Scissors, playerOne.SelectedTool);
        Assert.AreEqual(SalonTool.BlowDryer, playerTwo.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.None, playerOne.ActiveAction);
        Assert.AreEqual(ActiveServiceAction.ManualBlow, playerTwo.ActiveAction);
        Assert.AreEqual(first.Id, playerOne.SelectedToolOwnerCustomerId);
        Assert.AreEqual(second.Id, playerTwo.SelectedToolOwnerCustomerId);
    }

    private static SalonGameModel CreateServingHaircutPair(
        out CustomerModel first, out CustomerModel second)
    {
        SalonGameModel game = new SalonGameModel();
        first = game.Spawn(711, new List<ServiceType> { ServiceType.Cut });
        second = game.Spawn(712, new List<ServiceType> { ServiceType.Cut });
        Assert.IsTrue(game.ConfigureHaircutOrder(first, SalonTool.Scissors));
        Assert.IsTrue(game.ConfigureHaircutOrder(second, SalonTool.Scissors));
        game.Tick(SalonGameModel.EnteringSeconds + .05f);
        Assert.IsTrue(game.Assign(first, 1));
        Assert.IsTrue(game.Assign(second, 2));
        game.Tick(SalonGameModel.EnteringSeconds + SalonGameModel.MovingToStationSeconds + .05f);
        Assert.AreEqual(CustomerState.Serving, first.State);
        Assert.AreEqual(CustomerState.Serving, second.State);
        return game;
    }

    private static Type RequireType(string name)
    {
        Type type = Type.GetType(name + ", HairSalon.Runtime");
        if (type == null)
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                type = assembly.GetType(name) ?? type;
        Assert.That(type, Is.Not.Null, "Missing new co-op type: " + name);
        return type;
    }

    private static MethodInfo RequireMethod(Type type, string name, params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static,
            null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, "Missing co-op input method: " + name);
        return method;
    }
}
