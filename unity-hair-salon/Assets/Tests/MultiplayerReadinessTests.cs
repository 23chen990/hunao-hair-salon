using System;
using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class MultiplayerReadinessTests
{
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
}
