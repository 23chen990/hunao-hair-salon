using System;
using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;

public sealed class TrafficRhythmReputationTests
{
    private sealed class Simulation
    {
        public float NonZeroQueueRatio;
        public float LongestQuietWindow;
        public int PeakQueue;
        public int Completed;
        public int UnservedAtClose;
    }

    [Test]
    public void MobileWavesLeaveRecoveryGapsAndStopAdmittingBeforeClose()
    {
        Simulation before = Run(DayConfig.CreateDefault(), 3f);
        Simulation after = Run(SalonMobileDayConfig.CreateForDay(1), 3f);

        Assert.Greater(after.NonZeroQueueRatio, .75f);
        Assert.Less(after.NonZeroQueueRatio, .9f,
            "首日客流不能整场持续有队列，必须保留可感知的喘息段。");
        Assert.GreaterOrEqual(after.LongestQuietWindow, 8f,
            "清空一批工作后应保留至少一个真实恢复窗口。");
        Assert.GreaterOrEqual(after.PeakQueue, 1);
        Assert.LessOrEqual(after.PeakQueue, 2,
            "首日队列峰值不能超过两位活跃顾客的软上限。");
        Assert.GreaterOrEqual(after.Completed, 3);
        Assert.LessOrEqual(after.UnservedAtClose, SalonMobileDayConfig.WaitingCapacity);
    }

    [Test]
    public void OneStarStillLeavesEnoughCapacityForEveryMobileTarget()
    {
        for (int day = 1; day <= 3; day++)
        {
            Simulation result = Run(SalonMobileDayConfig.CreateForDay(day), 1f);
            Assert.GreaterOrEqual(result.Completed, SalonMobileDayConfig.TargetOrdersForDay(day),
                "1星第" + day + "天仍应完成目标");
        }
    }

    [Test]
    public void ReputationConfigDoesNotPenalizeCustomersLeftAtClose()
    {
        ShopReputationConfig config = SalonMobileDayConfig.CreateReputationConfig();
        var stats = new DayStats(1);
        stats.RecordUnservedAtClose();
        var model = new ShopReputationModel(config);
        model.ApplyDayResult(stats);
        Assert.AreEqual(0f, stats.ReputationDelta, .0001f);
    }

    [Test]
    public void ADayThatMeetsTheTargetWithOneWalkoutDoesNotDrainReputation()
    {
        // Real-touch target-met days: plain completions (no order type can
        // reach Happy), one walkout, and one or two services cut off at close.
        var model = new ShopReputationModel(SalonMobileDayConfig.CreateReputationConfig());
        for (int incomplete = 1; incomplete <= 2; incomplete++)
        {
            var stats = new DayStats(incomplete)
            {
                CompletedOrders = 4, NormalCustomers = 4,
                AbandonedBeforeService = 1, IncompleteAtClose = incomplete
            };
            model.ApplyDayResult(stats);
            Assert.GreaterOrEqual(stats.ReputationDelta, 0f,
                incomplete + " service(s) unfinished at close drained a target-met day.");
        }

        var clean = new DayStats(3) { CompletedOrders = 4, NormalCustomers = 4 };
        model.ApplyDayResult(clean);
        Assert.Greater(clean.ReputationDelta, 0f, "Clean service must slowly raise reputation.");

        var bad = new DayStats(4) { CompletedOrders = 1, NormalCustomers = 1, AbandonedBeforeService = 4 };
        model.ApplyDayResult(bad);
        Assert.AreEqual(-.2f, bad.ReputationDelta, .0001f, "Losses stay capped at 0.2 per day.");
    }

    [Test]
    public void ReputationTextUsesTheConfiguredTrafficMultiplier()
    {
        Assert.AreEqual(.8f, SalonMobileDayConfig.ReputationTrafficMultiplier(1f), .0001f);
        Assert.AreEqual(1.25f, SalonMobileDayConfig.ReputationTrafficMultiplier(5f), .0001f);
        StringAssert.Contains("3.0 星 → 3.1 星", SalonMobileDayConfig.DescribeReputationChange(3f, 3.1f));
        StringAssert.Contains("+1%", SalonMobileDayConfig.DescribeReputationForOpening(3.1f));
    }

    private static Simulation Run(DayConfig config, float stars)
    {
        if (!config.IsMobileProfile)
        {
            config.BusinessDuration = 180f;
            config.MinSpawnInterval = 6f;
            config.MaxSpawnInterval = 9f;
            config.MaxConcurrentCustomers = 6;
            config.WaitingCapacity = 4;
            config.OverloadWaitingThreshold = 3;
            config.OverloadSlowdownMultiplier = 2.4f;
        }
        var director = new CustomerTrafficDirector(config);
        var result = new Simulation();
        float cooldown = 0f;
        float serviceRemaining = 0f;
        int queue = 0;
        int total = 0;
        int quiet = 0;
        int quietLongest = 0;
        const float step = .25f;
        for (int tick = 0; tick < 720; tick++)
        {
            float progress = tick * step / 180f;
            if (serviceRemaining > 0f) serviceRemaining -= step;
            if (serviceRemaining <= 0f && queue > 0)
            {
                queue--;
                result.Completed++;
                serviceRemaining = 28f;
            }
            cooldown -= step;
            if (cooldown <= 0f)
            {
                TrafficDecision decision = director.Evaluate(progress,
                    new TrafficSnapshot(queue, queue, serviceRemaining > 0f ? 1 : 0, 0, 1),
                    .5f, stars);
                if (decision.ShouldSpawn)
                {
                    queue++;
                    total++;
                    cooldown = decision.SpawnInterval;
                }
                else cooldown = .5f;
            }
            result.PeakQueue = Math.Max(result.PeakQueue, queue);
            if (queue > 0)
            {
                result.NonZeroQueueRatio += step;
                quiet = 0;
            }
            else
            {
                quiet++;
                quietLongest = Math.Max(quietLongest, quiet);
            }
            result.LongestQuietWindow = Math.Max(result.LongestQuietWindow, quiet * step);
        }
        result.NonZeroQueueRatio /= 180f;
        result.UnservedAtClose = queue;
        return result;
    }
}
