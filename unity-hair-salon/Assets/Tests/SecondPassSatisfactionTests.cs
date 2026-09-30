using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

/// <summary>
/// Second pass regression coverage for the day-end satisfaction boundary.
/// These tests wire the same CustomerChanged -> satisfaction path as SalonDemo
/// so the result describes the current integration, not an isolated model call.
/// </summary>
public sealed class SecondPassSatisfactionTests
{
    [Test]
    public void ClosingUnservedEnteringAndWaitingCustomersDoesNotCreateFailureOrPenalty()
    {
        SalonGameModel game = NewGame();
        ShopSatisfactionModel satisfaction = AttachSatisfaction(game);
        CustomerModel waiting = game.Spawn(4101, new List<ServiceType> { ServiceType.Cut });

        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, waiting.State);
        waiting.TotalWaitSeconds = 30f;

        CustomerModel entering = game.Spawn(4102, new List<ServiceType> { ServiceType.Cut });
        Assert.AreEqual(CustomerState.Entering, entering.State);
        Assert.Greater(waiting.Patience, 0f);
        Assert.Greater(entering.Patience, 0f);

        game.ForceCloseRemainingCustomers(new DayStats(1));

        Assert.AreEqual(CustomerServiceResult.None, waiting.ServiceResult,
            "A patient who was never received should leave as a normal closing departure.");
        Assert.AreEqual(CustomerServiceResult.None, entering.ServiceResult,
            "An entering patient should not become a failed order merely because the day ended.");
        Assert.AreEqual(90, satisfaction.CurrentSatisfaction,
            "Unserved patients with remaining patience must not lower shop satisfaction.");
    }

    [Test]
    public void ClosingAnEngagedIncompleteCustomerStillAppliesExistingPenalty()
    {
        SalonGameModel game = NewGame();
        ShopSatisfactionModel satisfaction = AttachSatisfaction(game);
        CustomerModel customer = SpawnServing(game, 4111, new[] { ServiceType.Cut }, 1);
        customer.HasServiceEngaged = true;
        customer.TotalWaitSeconds = 20f;

        game.ForceCloseRemainingCustomers(new DayStats(1));

        Assert.AreEqual(CustomerServiceResult.SevereUnhappyCompletion, customer.ServiceResult);
        Assert.AreEqual(86, satisfaction.CurrentSatisfaction,
            "An engaged incomplete order still carries the configured long-wait consequence.");
    }

    [Test]
    public void RealPatienceExhaustionStillLeavesAsFailedAndLowersSatisfaction()
    {
        SalonGameModel game = NewGame();
        ShopSatisfactionModel satisfaction = AttachSatisfaction(game);
        CustomerModel customer = game.Spawn(4121, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        customer.Patience = 0f;

        game.Tick(.01f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.AreEqual(CustomerServiceResult.Failed, customer.ServiceResult);
        Assert.AreEqual(82, satisfaction.CurrentSatisfaction,
            "A genuine patience-zero departure remains a failed service outcome.");
    }

    [Test]
    public void RepeatedForceCloseDoesNotRepeatAnExistingFailurePenalty()
    {
        SalonGameModel game = NewGame();
        ShopSatisfactionModel satisfaction = AttachSatisfaction(game);
        CustomerModel customer = SpawnServing(game, 4131, new[] { ServiceType.Cut }, 1);
        customer.HasServiceEngaged = true;
        customer.TotalWaitSeconds = 20f;

        DayStats stats = new DayStats(1);
        game.ForceCloseRemainingCustomers(stats);
        int afterFirstClose = satisfaction.CurrentSatisfaction;

        game.ForceCloseRemainingCustomers(stats);

        Assert.AreEqual(86, afterFirstClose);
        Assert.AreEqual(afterFirstClose, satisfaction.CurrentSatisfaction,
            "A second close pass must not settle the same customer again.");
        Assert.AreEqual(1, stats.IncompleteAtClose);
    }

    [Test]
    public void ClosingAnAlreadyFailedLeavingCustomerPreservesTheExistingFailure()
    {
        SalonGameModel game = NewGame();
        ShopSatisfactionModel satisfaction = AttachSatisfaction(game);
        CustomerModel customer = game.Spawn(4141, new List<ServiceType> { ServiceType.Cut });
        customer.State = CustomerState.Leaving;
        customer.HasServiceEngaged = true;
        customer.ServiceResult = CustomerServiceResult.Failed;
        customer.ServiceFeedback = CustomerServiceFeedback.Dissatisfied;
        customer.Emotion = CustomerEmotion.Angry;
        customer.TotalWaitSeconds = 20f;

        game.ForceCloseRemainingCustomers(new DayStats(1));

        Assert.AreEqual(CustomerServiceResult.Failed, customer.ServiceResult,
            "Closing cleanup must not overwrite a real failure that already entered the exit route.");
        Assert.AreEqual(78, satisfaction.CurrentSatisfaction,
            "The existing failed order keeps its configured failure and long-wait consequences.");
    }

    private static SalonGameModel NewGame()
    {
        return new SalonGameModel(flowConfig: new SalonFlowConfig
        {
            MaxCustomers = 8,
            WaitingCapacity = 8,
            InitialCustomerCount = 0,
            BusinessDuration = 180f
        });
    }

    private static ShopSatisfactionModel AttachSatisfaction(SalonGameModel game)
    {
        ShopSatisfactionModel satisfaction = new ShopSatisfactionModel();
        game.CustomerChanged += customer => satisfaction.TrySettleCustomer(customer);
        return satisfaction;
    }

    private static CustomerModel SpawnServing(
        SalonGameModel game, int id, IReadOnlyList<ServiceType> needs, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType>(needs));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(CustomerState.Waiting, customer.State);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }
}
