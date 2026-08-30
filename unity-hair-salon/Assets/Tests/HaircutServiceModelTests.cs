using HairSalon;
using NUnit.Framework;

public class HaircutServiceModelTests
{
    [Test]
    public void OrderAcceptsOneOrTwoHaircutStepsOnly()
    {
        Assert.DoesNotThrow(() => new HaircutServiceModel(SalonTool.Scissors));
        Assert.DoesNotThrow(() => new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears));
        Assert.Throws<System.ArgumentException>(() => new HaircutServiceModel());
        Assert.Throws<System.ArgumentException>(() => new HaircutServiceModel(
            SalonTool.Scissors, SalonTool.ThinningShears, SalonTool.Clippers));
        Assert.Throws<System.ArgumentException>(() => new HaircutServiceModel(SalonTool.Shampoo));
    }

    [Test]
    public void FirstPerfectStepActivatesSecondWithoutCompletingService()
    {
        var service = new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears);

        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Perfect);

        Assert.AreEqual(1, service.CurrentStepIndex);
        Assert.AreEqual(SalonTool.ThinningShears, service.CurrentRequiredTool);
        Assert.AreEqual(HaircutServiceState.Active, service.State);
        Assert.AreEqual(HaircutServiceRating.None, service.Rating);
    }

    [Test]
    public void AllStepsFirstTryProducesPerfectRating()
    {
        var service = new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears);

        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Perfect);
        service.ApplyAttempt(SalonTool.ThinningShears, HaircutResult.Perfect);

        Assert.AreEqual(HaircutServiceState.Completed, service.State);
        Assert.AreEqual(HaircutServiceRating.Perfect, service.Rating);
    }

    [Test]
    public void UndercutThenSuccessProducesRecoveredRating()
    {
        var service = new HaircutServiceModel(SalonTool.Scissors);

        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Undercut);
        Assert.AreEqual(0, service.CurrentStepIndex);
        Assert.IsTrue(service.HadUndercut);
        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Perfect);

        Assert.AreEqual(HaircutServiceState.Completed, service.State);
        Assert.AreEqual(HaircutServiceRating.Recovered, service.Rating);
    }

    [TestCase(HaircutResult.Overcut)]
    [TestCase(HaircutResult.WrongTool)]
    public void FatalAttemptEndsServiceAsFailed(HaircutResult result)
    {
        var service = new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears);

        service.ApplyAttempt(SalonTool.Scissors, result);

        Assert.AreEqual(HaircutServiceState.Failed, service.State);
        Assert.AreEqual(HaircutServiceRating.Failed, service.Rating);
    }

    [Test]
    public void StartingWithWrongToolFailsWithoutWaitingForTimingResult()
    {
        var service = new HaircutServiceModel(SalonTool.Scissors);

        HaircutResult result = service.BeginAttempt(SalonTool.Clippers);

        Assert.AreEqual(HaircutResult.WrongTool, result);
        Assert.AreEqual(HaircutServiceState.Failed, service.State);
    }
}
