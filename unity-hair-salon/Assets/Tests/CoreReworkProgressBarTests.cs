using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class CoreReworkProgressBarTests
{
    [Test]
    public void HaircutBarIsFullExactlyWhenReleasingBecomesPerfect()
    {
        var config = new HaircutConfig();
        float min = config.GetPerfectMin(SalonTool.Scissors);
        float max = config.GetPerfectMax(SalonTool.Scissors);

        Assert.Less(ServiceProgressDisplay.HaircutFill(min - .05f, min), 1f);
        Assert.AreEqual(ServiceProgressDisplay.Phase.Working,
            ServiceProgressDisplay.HaircutPhase(min - .05f, min, max));
        Assert.AreEqual(1f, ServiceProgressDisplay.HaircutFill(min, min), .0001f,
            "A full bar must mean release now, not keep holding until the overcut.");
        Assert.AreEqual(ServiceProgressDisplay.Phase.Ready,
            ServiceProgressDisplay.HaircutPhase(min, min, max));
        Assert.AreEqual(ServiceProgressDisplay.Phase.Late,
            ServiceProgressDisplay.HaircutPhase(max - .05f, min, max),
            "The end of the perfect window warns before the overcut.");
        Assert.AreEqual(ServiceProgressDisplay.Phase.Failing,
            ServiceProgressDisplay.HaircutPhase(max + .05f, min, max));
    }

    [Test]
    public void FoamBarIsFullExactlyWhenTheModelAllowsRinsing()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        CustomerModel customer = SpawnServing(game, 7101, new[] { ServiceType.Wash }, 0);
        Assert.IsTrue(game.BeginWashFoamHold(customer));
        game.Tick(game.ServiceConfig.ShampooDuration + .01f);
        Assert.IsTrue(game.IsWashFoamWaitRunning(customer));

        TickUntil(game, () => game.IsWashFoamReadyToRinse(customer), 60f, out float beforeReady);
        Assert.Less(beforeReady, 1f, "The bar is not full while rinsing is still refused.");
        Assert.AreEqual(1f, ServiceProgressDisplay.BackgroundFill(customer.BackgroundTask), .0001f);
        Assert.AreEqual(ServiceProgressDisplay.Phase.Ready,
            ServiceProgressDisplay.BackgroundPhase(customer.BackgroundTask));

        game.Tick(customer.BackgroundTask.IdealEnd - customer.BackgroundTask.Elapsed + .05f);
        Assert.AreEqual(ServiceProgressDisplay.Phase.Late,
            ServiceProgressDisplay.BackgroundPhase(customer.BackgroundTask));
    }

    [Test]
    public void DryBarIsFullExactlyWhenTheModelAllowsFinishing()
    {
        var game = new SalonGameModel(serviceConfig: SalonMobileDayConfig.CreateServiceConfig());
        CustomerModel customer = SpawnServing(game, 7102, new[] { ServiceType.Dry }, 1);
        game.InstallAutoBlowStand();
        Assert.IsTrue(game.StartAutoBlow(customer));

        TickUntil(game, () => customer.BackgroundTask.Elapsed >= customer.BackgroundTask.IdealStart,
            60f, out float beforeReady);
        Assert.Less(beforeReady, 1f,
            "The old bar used a 10 second scale and filled before the 12.75 second ready time.");
        Assert.AreEqual(1f, ServiceProgressDisplay.BackgroundFill(customer.BackgroundTask), .0001f);
        Assert.AreEqual(ServiceProgressDisplay.Phase.Ready,
            ServiceProgressDisplay.BackgroundPhase(customer.BackgroundTask));
        Assert.AreEqual(BlowResult.Good, game.FinishAutoBlow(customer),
            "A full green dry bar must be finishable immediately.");
    }

    [Test]
    public void OnlyLateOrFailingBarsBlink()
    {
        foreach (ServiceProgressDisplay.Phase phase in new[]
                 { ServiceProgressDisplay.Phase.Working, ServiceProgressDisplay.Phase.Ready })
            Assert.AreEqual(ServiceProgressDisplay.ColorFor(phase),
                ServiceProgressDisplay.ColorFor(phase, 1.234f));
        Assert.AreNotEqual(ServiceProgressDisplay.ColorFor(ServiceProgressDisplay.Phase.Late),
            ServiceProgressDisplay.ColorFor(ServiceProgressDisplay.Phase.Late, .26f));
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id, ServiceType[] needs, int station)
    {
        CustomerModel customer = game.Spawn(id, new List<ServiceType>(needs));
        Assert.IsNotNull(customer);
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, station));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }

    private static void TickUntil(SalonGameModel game, System.Func<bool> ready, float limit,
        out float lastFillBeforeReady)
    {
        const float step = .05f;
        lastFillBeforeReady = 0f;
        CustomerModel customer = null;
        foreach (CustomerModel candidate in game.Customers)
            if (candidate.BackgroundTask.State == BackgroundTaskState.Running) customer = candidate;
        Assert.IsNotNull(customer);
        for (float t = 0f; t < limit; t += step)
        {
            if (ready()) return;
            lastFillBeforeReady = ServiceProgressDisplay.BackgroundFill(customer.BackgroundTask);
            game.Tick(step);
        }
        Assert.Fail("The background task never became ready.");
    }
}
