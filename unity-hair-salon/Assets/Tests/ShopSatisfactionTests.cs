using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class ShopSatisfactionTests
{
    [TestCase(100, SatisfactionMood.Happy, "100/100", 1f)]
    [TestCase(80, SatisfactionMood.Happy, "80/100", .8f)]
    [TestCase(79, SatisfactionMood.Neutral, "79/100", .79f)]
    [TestCase(40, SatisfactionMood.Neutral, "40/100", .4f)]
    [TestCase(39, SatisfactionMood.Sad, "39/100", .39f)]
    [TestCase(0, SatisfactionMood.Sad, "0/100", 0f)]
    [TestCase(120, SatisfactionMood.Happy, "100/100", 1f)]
    [TestCase(-15, SatisfactionMood.Sad, "0/100", 0f)]
    public void SetCurrent_ClampsAndBuildsOneConsistentHudSnapshot(
        int input, SatisfactionMood expectedMood, string expectedText, float expectedFill)
    {
        var model = new ShopSatisfactionModel();

        model.SetCurrent(input);

        Assert.AreEqual(expectedText, model.ValueText);
        Assert.AreEqual(expectedMood, model.Mood);
        Assert.AreEqual(expectedFill, model.NormalizedProgress, .0001f);
    }

    [Test]
    public void Changed_FiresOnlyWhenTheClampedValueActuallyChanges()
    {
        var model = new ShopSatisfactionModel();
        var values = new List<int>();
        model.Changed += snapshot => values.Add(snapshot.Value);

        model.SetCurrent(120);
        model.SetCurrent(100);
        model.SetCurrent(73);

        CollectionAssert.AreEqual(new[] { 100, 73 }, values);
    }

    [Test]
    public void SettleCustomer_IsIdempotentAndUsesExistingServiceOutcome()
    {
        var config = new ShopSatisfactionConfig
        {
            InitialValue = 50,
            CorrectServiceDelta = 2,
            FastErrorFreeBonus = 1,
            LongWaitPenalty = 4,
            WrongServicePenalty = 8,
            DisasterPenalty = 15
        };
        var model = new ShopSatisfactionModel(config);
        var customer = new CustomerModel
        {
            Id = 42,
            State = CustomerState.Leaving,
            ServiceResult = CustomerServiceResult.HappyCompletion,
            TotalWaitSeconds = 2f,
            WrongStationCount = 0,
            AccidentSeverity = AccidentSeverity.None
        };

        Assert.IsTrue(model.TrySettleCustomer(customer));
        Assert.AreEqual(53, model.CurrentSatisfaction);
        Assert.IsFalse(model.TrySettleCustomer(customer));
        Assert.AreEqual(53, model.CurrentSatisfaction);
    }

    [Test]
    public void SettleCustomer_CombinesConfiguredWaitWrongServiceAndDisasterPenalties()
    {
        var config = new ShopSatisfactionConfig
        {
            InitialValue = 90,
            CorrectServiceDelta = 2,
            FastErrorFreeBonus = 1,
            LongWaitPenalty = 4,
            WrongServicePenalty = 8,
            DisasterPenalty = 15,
            LongWaitSeconds = 12f
        };
        var model = new ShopSatisfactionModel(config);
        var customer = new CustomerModel
        {
            Id = 9,
            State = CustomerState.Leaving,
            ServiceResult = CustomerServiceResult.SevereUnhappyCompletion,
            TotalWaitSeconds = 20f,
            WrongStationCount = 1,
            AccidentSeverity = AccidentSeverity.Major
        };

        Assert.IsTrue(model.TrySettleCustomer(customer));

        Assert.AreEqual(63, model.CurrentSatisfaction);
    }

    [Test]
    public void SettleCustomer_WaitsForACompletedOrLeavingCustomer()
    {
        var model = new ShopSatisfactionModel();
        var customer = new CustomerModel
        {
            Id = 7,
            State = CustomerState.Serving,
            ServiceResult = CustomerServiceResult.NormalCompletion
        };

        Assert.IsFalse(model.TrySettleCustomer(customer));
        Assert.AreEqual(90, model.CurrentSatisfaction);
    }

    [Test]
    public void SettleCustomer_WaitsUntilFinishedFeedbackTransitionsToLeaving()
    {
        var model = new ShopSatisfactionModel();
        var customer = new CustomerModel
        {
            Id = 8,
            State = CustomerState.Finished,
            ServiceResult = CustomerServiceResult.HappyCompletion
        };

        Assert.IsFalse(model.TrySettleCustomer(customer));
        Assert.AreEqual(90, model.CurrentSatisfaction);

        customer.State = CustomerState.Leaving;
        Assert.IsTrue(model.TrySettleCustomer(customer));
        Assert.AreEqual(93, model.CurrentSatisfaction);
    }
}
