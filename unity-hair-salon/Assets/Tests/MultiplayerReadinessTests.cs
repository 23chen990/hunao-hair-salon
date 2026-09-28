using System;
using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class MultiplayerReadinessTests
{
    [Test]
    public void CoopKeyboardInputMapsMovementAndInteractionToSeparatePlayers()
    {
        Assert.AreEqual(new Vector2(0f, 1f),
            SalonCoopInputMap.ResolveKeyboardMove(1, KeyCode.W));
        Assert.AreEqual(new Vector2(1f, 0f),
            SalonCoopInputMap.ResolveKeyboardMove(1, KeyCode.D));
        Assert.AreEqual(Vector2.zero,
            SalonCoopInputMap.ResolveKeyboardMove(1, KeyCode.UpArrow),
            "P1 must not consume P2 movement keys.");

        Assert.AreEqual(new Vector2(0f, 1f),
            SalonCoopInputMap.ResolveKeyboardMove(2, KeyCode.UpArrow));
        Assert.AreEqual(new Vector2(1f, 0f),
            SalonCoopInputMap.ResolveKeyboardMove(2, KeyCode.RightArrow));
        Assert.AreEqual(Vector2.zero,
            SalonCoopInputMap.ResolveKeyboardMove(2, KeyCode.W),
            "P2 must not consume P1 movement keys.");

        Assert.IsTrue(SalonCoopInputMap.IsInteractionHeld(1, KeyCode.Space));
        Assert.IsTrue(SalonCoopInputMap.IsInteractionPressed(1, KeyCode.Space));
        Assert.IsFalse(SalonCoopInputMap.IsInteractionHeld(1, KeyCode.RightControl));
        Assert.IsFalse(SalonCoopInputMap.IsInteractionPressed(1, KeyCode.KeypadEnter));

        Assert.IsTrue(SalonCoopInputMap.IsInteractionHeld(2, KeyCode.RightControl));
        Assert.IsTrue(SalonCoopInputMap.IsInteractionPressed(2, KeyCode.RightControl));
        Assert.IsTrue(SalonCoopInputMap.IsInteractionHeld(2, KeyCode.KeypadEnter));
        Assert.IsTrue(SalonCoopInputMap.IsInteractionPressed(2, KeyCode.KeypadEnter));
        Assert.IsFalse(SalonCoopInputMap.IsInteractionHeld(2, KeyCode.Space));
        Assert.IsFalse(SalonCoopInputMap.IsInteractionPressed(2, KeyCode.Space));
    }

    [Test]
    public void DifferentCustomersCanStartParallelWashAndHaircutWithPlayerScopedOwners()
    {
        var game = new SalonGameModel();
        CustomerModel washCustomer = SpawnServing(game, 401, ServiceType.Wash, 0);
        CustomerModel haircutCustomer = SpawnServing(game, 402, ServiceType.Cut, 1);
        Assert.IsTrue(game.ConfigureHaircutOrder(haircutCustomer, SalonTool.Scissors));

        Assert.IsTrue(game.BeginWashFoamHold(1, washCustomer));
        Assert.IsTrue(game.BeginHaircutAction(
            2, haircutCustomer, SalonTool.Scissors, new HaircutConfig()));

        Assert.AreEqual(1, washCustomer.InteractionOwnerPlayerId);
        Assert.AreEqual(2, haircutCustomer.InteractionOwnerPlayerId);
        Assert.AreNotEqual(washCustomer.InteractionOwnerPlayerId,
            haircutCustomer.InteractionOwnerPlayerId);
        Assert.IsTrue(game.GetOrCreatePlayerContext(1).OwnsSelection(washCustomer));
        Assert.IsTrue(game.GetOrCreatePlayerContext(2).OwnsSelection(haircutCustomer));
        Assert.IsFalse(game.GetOrCreatePlayerContext(1).OwnsSelection(haircutCustomer));
        Assert.IsFalse(game.GetOrCreatePlayerContext(2).OwnsSelection(washCustomer));
    }

    [Test]
    public void AnotherPlayerIsRejectedWhileCustomerIsOwnedThenCanStartAfterRelease()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 403, ServiceType.Cut, 1);
        var config = new HaircutConfig();
        Assert.IsTrue(game.ConfigureHaircutOrder(
            customer, SalonTool.Scissors, SalonTool.ThinningShears));

        Assert.IsTrue(game.BeginHaircutAction(1, customer, SalonTool.Scissors, config));
        Assert.AreEqual(1, customer.InteractionOwnerPlayerId);
        Assert.IsFalse(game.BeginHaircutAction(2, customer, SalonTool.Scissors, config),
            "A second player must not mutate an active operation owned by P1.");
        Assert.AreEqual(1, customer.InteractionOwnerPlayerId);

        float perfectHold = config.GetPerfectMin(SalonTool.Scissors) + .05f;
        Assert.AreEqual(HaircutResult.None,
            game.CompleteHaircutAction(2, customer, perfectHold, false),
            "The non-owner must not complete P1's haircut.");
        Assert.IsFalse(game.EndActiveOperation(2, customer),
            "The non-owner must not release P1's reservation.");
        Assert.AreEqual(1, customer.InteractionOwnerPlayerId);
        Assert.AreNotEqual(HaircutResult.None,
            game.CompleteHaircutAction(1, customer, perfectHold, false));
        Assert.IsTrue(game.EndActiveOperation(1, customer));
        Assert.AreEqual(-1, customer.InteractionOwnerPlayerId,
            "Completing and ending the operation must release the customer owner.");

        Assert.IsTrue(game.BeginHaircutAction(
            2, customer, SalonTool.ThinningShears, config));
        Assert.AreEqual(2, customer.InteractionOwnerPlayerId);
    }

    [Test]
    public void PlayerContextsKeepToolsAndActionsIndependent()
    {
        var game = new SalonGameModel();
        CustomerModel first = SpawnServing(game, 404, ServiceType.Cut, 1);
        CustomerModel second = SpawnServing(game, 405, ServiceType.Cut, 2);
        game.SelectCustomer(1, first);
        game.SelectCustomer(2, second);

        PlayerContext playerOne = game.GetOrCreatePlayerContext(1);
        PlayerContext playerTwo = game.GetOrCreatePlayerContext(2);
        playerOne.SelectServiceAction(first, ActiveServiceAction.Shampoo, SalonTool.Shampoo);
        playerTwo.SelectServiceAction(second, ActiveServiceAction.ManualBlow, SalonTool.BlowDryer);

        Assert.AreEqual(SalonTool.Shampoo, playerOne.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.Shampoo, playerOne.ActiveAction);
        Assert.AreEqual(first.Id, playerOne.SelectedToolOwnerCustomerId);
        Assert.AreEqual(SalonTool.BlowDryer, playerTwo.SelectedTool);
        Assert.AreEqual(ActiveServiceAction.ManualBlow, playerTwo.ActiveAction);
        Assert.AreEqual(second.Id, playerTwo.SelectedToolOwnerCustomerId);
        Assert.IsTrue(playerOne.OwnsSelection(first));
        Assert.IsTrue(playerTwo.OwnsSelection(second));
        Assert.IsFalse(playerOne.OwnsSelection(second));
        Assert.IsFalse(playerTwo.OwnsSelection(first));

        playerTwo.ClearInteractionContext();
        Assert.AreEqual(SalonTool.Shampoo, playerOne.SelectedTool,
            "Clearing P2 must not clear P1's selected tool.");
        Assert.AreEqual(ActiveServiceAction.Shampoo, playerOne.ActiveAction,
            "Clearing P2 must not clear P1's active action.");
    }

    [Test]
    public void TwoPlayersCanFocusDifferentCustomersWithoutMutatingSharedCustomerState()
    {
        var game = new SalonGameModel();
        CustomerModel first = game.Spawn(101, new List<ServiceType> { ServiceType.Cut });
        CustomerModel second = game.Spawn(102, new List<ServiceType> { ServiceType.Cut });

        game.SelectCustomer(1, first);
        game.SelectCustomer(2, second);

        PlayerContext playerOne = game.GetOrCreatePlayerContext(1);
        PlayerContext playerTwo = game.GetOrCreatePlayerContext(2);
        Assert.AreEqual(first.Id, playerOne.SelectedCustomerId);
        Assert.AreEqual(second.Id, playerTwo.SelectedCustomerId);
        Assert.AreSame(first, game.SelectedCustomer, "The legacy single-player view must remain Player 1.");
        Assert.IsNull(typeof(CustomerModel).GetField("Selected"),
            "Selection is private player/UI state and must not live on the shared customer.");
    }

    [Test]
    public void DuplicateCustomerIdsAreRejectedBeforeTheyCanCorruptStationReferences()
    {
        var game = new SalonGameModel();
        Assert.IsNotNull(game.Spawn(201, new List<ServiceType> { ServiceType.Cut }));

        Assert.Throws<ArgumentException>(() =>
            game.Spawn(201, new List<ServiceType> { ServiceType.Wash }));
    }

    [Test]
    public void CustomerIdCannotBeReusedAfterTheOriginalCustomerLeavesTheSession()
    {
        var game = new SalonGameModel(
            patienceConfig: new CustomerPatienceConfig { DrainPerSecond = 100f });
        CustomerModel original = game.Spawn(301, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        original.Patience = .01f;
        game.Tick(.01f);
        game.Tick(SalonGameModel.LeavingSeconds + .01f);
        CollectionAssert.DoesNotContain(game.Customers, original);

        Assert.Throws<ArgumentException>(() =>
            game.Spawn(301, new List<ServiceType> { ServiceType.Cut }));
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, ServiceType service, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType> { service });
        Assert.IsNotNull(customer);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }
}
