using HairSalon;
using NUnit.Framework;
using System.Collections.Generic;

public class HaircutInteractionTests
{
    private static CustomerModel AssignedCutCustomer(SalonGameModel game)
    {
        var customer = game.Spawn(20, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        return customer;
    }

    [Test]
    public void HoldCannotStartUntilAHaircutToolIsSelected()
    {
        var interaction = new HaircutInteraction(new HaircutConfig());

        Assert.IsFalse(interaction.BeginHold());
        interaction.SelectTool(SalonTool.Shampoo);
        Assert.IsFalse(interaction.BeginHold());
        interaction.SelectTool(SalonTool.Scissors);

        Assert.IsTrue(interaction.BeginHold());
        Assert.AreEqual(HaircutInteractionState.Holding, interaction.State);
    }

    [TestCase(SalonTool.Scissors)]
    [TestCase(SalonTool.ThinningShears)]
    [TestCase(SalonTool.Clippers)]
    public void EveryHaircutToolUsesTheSameLongPressStateMachine(SalonTool tool)
    {
        var interaction = new HaircutInteraction(new HaircutConfig());

        interaction.SelectTool(tool);

        Assert.IsTrue(interaction.BeginHold());
        Assert.AreEqual(HaircutInteractionState.Holding, interaction.State);
        Assert.AreEqual(tool, interaction.SelectedTool);
    }

    [Test]
    public void ClippersReachDangerThresholdFasterThanScissors()
    {
        var config = new HaircutConfig
        {
            PerfectMax = 2.5f,
            ClippersPerfectMax = 1.45f
        };
        var scissors = new HaircutInteraction(config);
        scissors.SelectTool(SalonTool.Scissors);
        scissors.BeginHold();
        var clippers = new HaircutInteraction(config);
        clippers.SelectTool(SalonTool.Clippers);
        clippers.BeginHold();

        Assert.AreEqual(HaircutResult.None, scissors.Tick(1.5f));
        Assert.AreEqual(HaircutResult.Overcut, clippers.Tick(1.5f));
    }

    [TestCase(0.4f, HaircutResult.Undercut)]
    [TestCase(1.499f, HaircutResult.Undercut)]
    [TestCase(1.5f, HaircutResult.Perfect)]
    [TestCase(2.5f, HaircutResult.Perfect)]
    public void ReleasingHoldUsesConfiguredSafeWindow(float duration, HaircutResult expected)
    {
        var interaction = new HaircutInteraction(new HaircutConfig { PerfectMin = 1.5f, PerfectMax = 2.5f });
        interaction.SelectTool(SalonTool.Scissors);
        Assert.IsTrue(interaction.BeginHold());
        interaction.Tick(duration);

        Assert.AreEqual(expected, interaction.ReleaseHold());
        Assert.AreEqual(HaircutInteractionState.Result, interaction.State);
    }

    [Test]
    public void CrossingPerfectMaximumImmediatelyTriggersOvercutOnce()
    {
        var interaction = new HaircutInteraction(new HaircutConfig { PerfectMin = 1.5f, PerfectMax = 2.5f });
        int overcutEvents = 0;
        interaction.ResultResolved += result => { if (result == HaircutResult.Overcut) overcutEvents++; };
        interaction.SelectTool(SalonTool.Scissors);
        interaction.BeginHold();

        interaction.Tick(2.501f);
        interaction.Tick(3f);

        Assert.AreEqual(HaircutInteractionState.Result, interaction.State);
        Assert.AreEqual(HaircutResult.Overcut, interaction.LastResult);
        Assert.AreEqual(1, overcutEvents);
        Assert.AreEqual(HaircutResult.None, interaction.ReleaseHold());
    }

    [TestCase(SalonTool.Scissors)]
    [TestCase(SalonTool.ThinningShears)]
    [TestCase(SalonTool.Clippers)]
    public void ProgressBarIsFullWhenTheSelectedToolFirstBecomesCompletable(SalonTool tool)
    {
        var config = new HaircutConfig();
        var interaction = new HaircutInteraction(config);
        interaction.SelectTool(tool);
        Assert.IsTrue(interaction.BeginHold());

        interaction.Tick(config.GetPerfectMin(tool));

        Assert.AreEqual(1f, interaction.HoldProgress, .001f,
            "The requirement progress must reach 100% at the normal completion threshold, not at the bald/overcut limit.");
        Assert.AreEqual(HaircutResult.Perfect, interaction.ReleaseHold());
    }

    [Test]
    public void CancelReturnsToToolSelectedWithoutResolvingAResult()
    {
        var interaction = new HaircutInteraction(new HaircutConfig());
        int resultEvents = 0;
        interaction.ResultResolved += _ => resultEvents++;
        interaction.SelectTool(SalonTool.Scissors);
        interaction.BeginHold();
        interaction.Tick(1f);

        interaction.CancelHold();

        Assert.AreEqual(HaircutInteractionState.ToolSelected, interaction.State);
        Assert.AreEqual(0f, interaction.HoldTime);
        Assert.AreEqual(HaircutResult.None, interaction.LastResult);
        Assert.AreEqual(0, resultEvents);
    }

    [Test]
    public void UndercutPenalizesButKeepsCustomerAtStationForRetry()
    {
        var game = new SalonGameModel();
        var customer = AssignedCutCustomer(game);
        var config = new HaircutConfig { UndercutPenalty = 10f };
        float satisfactionBefore = customer.Satisfaction;

        Assert.IsTrue(game.ApplyHaircutResult(customer, HaircutResult.Undercut, config));

        Assert.AreEqual(satisfactionBefore - 10f, customer.Satisfaction);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(1, customer.Station);
        Assert.AreEqual(HairStage.Trimmed, customer.HairStage);
        Assert.AreEqual(CustomerEmotion.Impatient, customer.Emotion);
        Assert.AreEqual(0, game.Served);
    }

    [Test]
    public void PerfectUsesExistingServiceCompletionFlow()
    {
        var game = new SalonGameModel();
        var customer = AssignedCutCustomer(game);

        Assert.IsTrue(game.ApplyHaircutResult(customer, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(HairStage.Complete, customer.HairStage);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, game.Served);
        Assert.AreEqual(1, customer.Station);
        Assert.AreEqual(WorkstationState.Completed, game.Workstations[1].State);
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion);
    }

    [Test]
    public void OvercutAppliesConfiguredPenaltyAndRaisesAccidentEvent()
    {
        var game = new SalonGameModel();
        var customer = AssignedCutCustomer(game);
        var config = new HaircutConfig { OvercutPenalty = 40f };
        int accidentCustomerId = -1;
        game.HaircutOvercut += affected => accidentCustomerId = affected.Id;

        Assert.IsTrue(game.ApplyHaircutResult(customer, HaircutResult.Overcut, config));

        Assert.LessOrEqual(customer.Satisfaction, 30f);
        Assert.AreEqual(AccidentSeverity.Major, customer.AccidentSeverity);
        Assert.AreEqual(HairStage.Overcut, customer.HairStage);
        Assert.AreEqual(CustomerEmotion.Angry, customer.Emotion);
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(1, customer.Station);
        Assert.AreEqual(WorkstationState.Completed, game.Workstations[1].State);
        Assert.AreEqual(HaircutServiceRating.Failed, customer.LastHaircutRating);
        Assert.AreEqual(0, game.Payments.Drops.Count);
        Assert.AreEqual(1, game.Mistakes);
        Assert.AreEqual(customer.Id, accidentCustomerId);

        game.Tick(SalonGameModel.FinishedFeedbackSeconds + .01f);
        Assert.AreEqual(-1, customer.Station);
        Assert.AreEqual(WorkstationState.Available, game.Workstations[1].State);
    }

    [Test]
    public void TwoStepHaircutOnlyCompletesAfterSecondPerfectAttempt()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(20, new List<ServiceType> { ServiceType.Cut });
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors, SalonTool.ThinningShears);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(SalonTool.ThinningShears, customer.HaircutService.CurrentRequiredTool);
        Assert.AreEqual(0, game.Payments.Drops.Count);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.ThinningShears, HaircutResult.Perfect, new HaircutConfig()));
        Assert.AreEqual(CustomerState.Finished, customer.State);
        Assert.AreEqual(HaircutServiceRating.Perfect, customer.LastHaircutRating);
        Assert.AreEqual(300, game.Payments.Drops[0].BaseReward);
        Assert.AreEqual(0, game.Payments.Drops[0].TipReward,
            "A clean order still needs to reach the configurable happy threshold before earning a tip.");
        Assert.AreEqual(game.Payments.Drops[0].BaseReward + game.Payments.Drops[0].TipReward,
            game.Payments.Drops[0].Amount);
    }

    [Test]
    public void CompletingOnlyTheFirstStepDoesNotMarkTheCustomerHappy()
    {
        var game = new SalonGameModel();
        var customer = game.Spawn(24, new List<ServiceType> { ServiceType.Cut });
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors, SalonTool.ThinningShears);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));

        Assert.AreEqual(HaircutServiceState.Active, customer.HaircutService.State);
        Assert.AreEqual(CustomerEmotion.Calm, customer.Emotion,
            "A correct intermediate step is not yet a HappyCompletion.");
        Assert.AreEqual(CustomerServiceResult.None, customer.ServiceResult);
    }

    [Test]
    public void RecoveredHaircutCreatesMediumPayment()
    {
        var game = new SalonGameModel();
        var customer = AssignedCutCustomer(game);
        var config = new HaircutConfig();

        game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Undercut, config);
        game.ApplyHaircutResult(customer, SalonTool.Scissors, HaircutResult.Perfect, config);

        Assert.AreEqual(HaircutServiceRating.Recovered, customer.LastHaircutRating);
        Assert.AreEqual(120, game.Payments.Drops[0].Amount);
    }

    [Test]
    public void WrongToolAtHoldStartIsExtraServiceWithoutCreatingPayment()
    {
        var game = new SalonGameModel();
        var customer = AssignedCutCustomer(game);

        Assert.IsTrue(game.ApplyHaircutResult(customer, SalonTool.Clippers, HaircutResult.WrongTool, new HaircutConfig()));

        Assert.AreEqual(CustomerState.Serving, customer.State);
        Assert.AreEqual(HaircutServiceRating.None, customer.LastHaircutRating);
        Assert.AreEqual(HaircutServiceState.Active, customer.HaircutService.State);
        Assert.AreEqual(1, customer.ExtraServiceCount);
        Assert.AreEqual(0, game.Payments.Drops.Count);
        Assert.AreEqual(1, game.Mistakes);
    }

    [Test]
    public void ResultCanResetForAFreshNonAccumulatingRetry()
    {
        var interaction = new HaircutInteraction(new HaircutConfig());
        interaction.SelectTool(SalonTool.Scissors);
        interaction.BeginHold();
        interaction.Tick(.5f);
        Assert.AreEqual(HaircutResult.Undercut, interaction.ReleaseHold());

        interaction.PrepareRetry();
        Assert.IsTrue(interaction.BeginHold());

        Assert.AreEqual(0f, interaction.HoldTime);
        Assert.AreEqual(HaircutResult.None, interaction.LastResult);
    }

    [Test]
    public void WorldTimeAndOtherCustomersContinueDuringHaircutHold()
    {
        var game = new SalonGameModel();
        var focused = game.Spawn(30, new List<ServiceType> { ServiceType.Cut });
        var other = game.Spawn(31, new List<ServiceType> { ServiceType.Cut });
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        game.Assign(focused, 1);
        game.Assign(other, 2);
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.SelectCustomer(focused);
        var interaction = new HaircutInteraction(new HaircutConfig());
        interaction.SelectTool(SalonTool.Scissors);
        interaction.BeginHold();
        float patienceBefore = other.Patience;
        float worldBefore = game.WorldElapsed;
        float worldAdvance = game.PatienceConfig.ServiceArrivalGraceSeconds + 1f;

        interaction.Tick(1f);
        game.Tick(worldAdvance);

        Assert.AreEqual(worldBefore + worldAdvance, game.WorldElapsed, .001f);
        Assert.Less(other.Patience, patienceBefore);
        Assert.AreEqual(HaircutInteractionState.Holding, interaction.State);
    }
}
