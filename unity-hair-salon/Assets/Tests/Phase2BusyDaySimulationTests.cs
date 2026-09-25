using System;
using System.Collections.Generic;
using System.Text;
using HairSalon;
using NUnit.Framework;

/// <summary>
/// Phase 2 客流压力的无头验收：用真实的 BusinessDayController + CustomerTrafficDirector +
/// SalonGameModel 跑完一整个营业日，并由一个"称职玩家"策略驱动，测量真实并发。
///
/// 目的不是模拟完美操作，而是回答两个问题：
///   1. 现有 Director 参数能不能自然产生 2 → 3 → 4 的并发爬升；
///   2. Day 1 的目标订单在一个称职玩家手里能不能达成。
///
/// 注意：模型层没有玩家位置，模拟不证明实际触控体验；完整操作由 Chromium 回归检查。
/// 压力测试单独禁用目标收客门槛，达标与结算测试使用真实目标。
/// </summary>
public sealed class Phase2BusyDaySimulationTests
{
    private const float Step = .25f;

    private sealed class Report
    {
        public int PeakActive;
        public int PeakPendingActions;
        public float SecondsAtTwo;
        public float SecondsAtThree;
        public float SecondsAtFour;
        public float FirstAtTwo = -1f;
        public float FirstAtThree = -1f;
        public float FirstAtFour = -1f;
        public int Spawned;
        public int CompletedOrders;
        public int Abandoned;
        public int CollectedPayments;
        public float DayLength;
        public string Trace = "";
    }

    // ------------------------------------------------------------------ 验收

    [Test]
    public void UnfinishedMobileDayClimbsFromTwoToThreeAndPeaksAtFour()
    {
        Report report = RunDay(1, pressureOnly: true);

        TestContext.Out.WriteLine(report.Trace);

        Assert.GreaterOrEqual(report.PeakActive, 4,
            "A competent player must still see four customers in the salon at peak. " +
            "Trace: " + report.Trace);
        Assert.GreaterOrEqual(report.SecondsAtThree, 15f,
            "Three simultaneous customers must hold for a meaningful stretch, not appear for one frame.");
        Assert.GreaterOrEqual(report.SecondsAtTwo, 40f,
            "Two simultaneous customers must be the normal state, not a spike.");
        Assert.Greater(report.FirstAtTwo, 0f, "Pressure must start early in the day.");
        Assert.Less(report.FirstAtTwo, report.FirstAtThree, "Pressure must climb 2 -> 3.");
        Assert.Less(report.FirstAtThree, report.FirstAtFour, "Pressure must climb 3 -> 4.");
    }

    [Test]
    public void ACompetentPlayerStillHasSeveralThingsPendingAtThePeak()
    {
        Report report = RunDay(1, pressureOnly: true);
        Assert.GreaterOrEqual(report.PeakPendingActions, 3,
            "At the busiest moment the player must have at least three separate things waiting. " +
            "Trace: " + report.Trace);
    }

    [Test]
    public void DayOneTargetIsAchievableWithoutPerfectPlay()
    {
        Report report = RunDay(1);
        Assert.GreaterOrEqual(report.CompletedOrders, SalonMobileDayConfig.TargetOrdersForDay(1),
            "Day 1 target must be reachable by a competent player. " + report.Trace);
        Assert.GreaterOrEqual(report.CollectedPayments, report.CompletedOrders,
            "Every completed order must be collectable.");
    }

    [Test]
    public void TheDirectorEasesOffInsteadOfPunishingAnOverloadedPlayer()
    {
        // 同样的场景，但玩家完全不作为：Director 必须放慢客流，而不是继续堆人。
        Report idle = RunDay(1, playerActs: false);
        Assert.LessOrEqual(idle.Spawned, 12,
            "A player who does nothing must not be buried under an unbounded queue. " + idle.Trace);
    }

    // ------------------------------------------------------------------ 模拟器

    private static Report RunDay(int dayNumber, bool playerActs = true, bool pressureOnly = false)
    {
        var game = new SalonGameModel(
            serviceConfig: SalonMobileDayConfig.CreateServiceConfig(),
            flowConfig: new SalonFlowConfig
            {
                MaxCustomers = 8,
                WaitingCapacity = 4,
                InitialCustomerCount = 0
            });
        var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(dayNumber));
        day.StartDay(dayNumber);
        // Test the existing traffic curve independently from the new early
        // goal settlement; a completed goal correctly stops this curve early.
        if (pressureOnly) day.Config.TargetOrders = int.MaxValue;
        day.StartBusiness();
        var director = new CustomerTrafficDirector(day.Config);
        var haircut = new HaircutConfig();
        // DayStats 由 CustomerChanged 事件驱动（真实运行时由 SalonDemo 挂钩），
        // 模拟里必须自己接上，否则 CompletedOrders 永远是 0。
        game.CustomerChanged += c => day.Stats.RecordCustomerSnapshot(c);

        var report = new Report();
        var trace = new StringBuilder();
        float spawnCooldown = 0f;
        int spawnIndex = 0;
        int nextId = 4100;
        float elapsed = 0f;
        CustomerModel cutting = null;
        float cutRemaining = 0f;
        uint seed = 20260919u;
        int sampleCounter = 0;

        while (day.State != DayState.Result && elapsed < 400f)
        {
            // 1. 玩家手上的剪发继续占用玩家。
            if (cutting != null)
            {
                cutRemaining -= Step;
                if (cutRemaining <= 0f)
                {
                    game.CompleteHaircutAction(cutting,
                        game.HaircutHoldDurationFor(SalonTool.Scissors, haircut), false);
                    game.EndActiveOperation(cutting);
                    cutting = null;
                }
            }
            else if (playerActs && !game.PlayerBusy)
            {
                PerformOnePlayerAction(game, day, haircut, ref cutting, ref cutRemaining);
            }

            // 2. 客流：与 SalonDemo.MaintainCustomerFlow 同口径。
            if (day.CanSpawnCustomers)
            {
                spawnCooldown = Math.Max(0f, spawnCooldown - Step);
                if (spawnCooldown <= 0f)
                {
                    seed = seed * 1664525u + 1013904223u;
                    float roll = (seed % 10000u) / 10000f;
                    TrafficDecision decision = director.Evaluate(
                        day.BusinessProgress, BuildSnapshot(game), roll, day.Reputation.CurrentStars);
                    if (decision.ShouldSpawn && TrySpawn(game, day, nextId, spawnIndex))
                    {
                        nextId++;
                        spawnIndex++;
                        spawnCooldown = decision.SpawnInterval;
                    }
                    else
                    {
                        spawnCooldown = decision.IsOverloaded ? 1.25f : .6f;
                    }
                }
            }

            // 3. 世界推进。
            game.Tick(Step);
            day.Tick(Step, CountActive(game));
            elapsed += Step;

            // 4. 记录。
            int active = CountActive(game);
            int pending = CountPendingActions(game);
            report.PeakActive = Math.Max(report.PeakActive, active);
            report.PeakPendingActions = Math.Max(report.PeakPendingActions, pending);
            Bucket(report, active, elapsed);

            if (++sampleCounter % 16 == 0)
                trace.AppendLine(string.Format(
                    "t={0,5:0}s active={1} pending={2} done={3} gone={4} phase={5} | {6}",
                    elapsed, active, pending, day.Stats.CompletedOrders,
                    day.Stats.AbandonedBeforeService, day.CurrentPressurePhase,
                    StateBreakdown(game)));
        }

        // The Demo collects earned payments on entry to Result, including the
        // last customer's drop when the day ends without another gameplay tick.
        foreach (PaymentDropModel drop in game.Payments.Drops)
        {
            game.Payments.BeginCollection(drop.Id);
            if (game.Payments.CompleteCollection(drop.Id)) day.Stats.RecordPaymentCollected(drop);
        }
        report.Spawned = day.Stats.SpawnedCustomers;
        report.CompletedOrders = day.Stats.CompletedOrders;
        report.Abandoned = day.Stats.AbandonedBeforeService;
        report.DayLength = elapsed;
        report.CollectedPayments = CountCollectedPayments(game, day);
        report.Trace = trace.ToString() +
            string.Format("[summary] spawned={0} completed={1} abandoned={2} unserved={3} incomplete={4} drops={5} collected={6}",
                report.Spawned, report.CompletedOrders, report.Abandoned,
                day.Stats.UnservedAtClose, day.Stats.IncompleteAtClose,
                game.Payments.Drops.Count, report.CollectedPayments);
        return report;
    }

    private static string StateBreakdown(SalonGameModel game)
    {
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            string key = c.State + "/" + c.CurrentNeed +
                         (game.IsWashFoamWaitRunning(c) ? "(foam)" : "") +
                         (c.AutoBlowRunning ? "(blow)" : "");
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        var parts = new List<string>();
        foreach (var kv in counts) parts.Add(kv.Key + "x" + kv.Value);
        return string.Join(" ", parts.ToArray());
    }

    private static void Bucket(Report report, int active, float elapsed)
    {
        if (active >= 2)
        {
            report.SecondsAtTwo += Step;
            if (report.FirstAtTwo < 0f) report.FirstAtTwo = elapsed;
        }
        if (active >= 3)
        {
            report.SecondsAtThree += Step;
            if (report.FirstAtThree < 0f) report.FirstAtThree = elapsed;
        }
        if (active >= 4)
        {
            report.SecondsAtFour += Step;
            if (report.FirstAtFour < 0f) report.FirstAtFour = elapsed;
        }
    }

    // ------------------------------------------------------------ 玩家策略

    private static void PerformOnePlayerAction(SalonGameModel game, BusinessDayController day,
        HaircutConfig haircut, ref CustomerModel cutting, ref float cutRemaining)
    {
        // a. 泡沫已经可以冲洗 —— 回来了就得收尾。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (game.IsWashFoamReadyToRinse(c)) { game.FinishWashRinse(c); return; }
        }

        // b. 吹发进入理想窗口。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.AutoBlowRunning && c.BackgroundTask.Elapsed >= c.BackgroundTask.IdealStart)
            {
                game.FinishAutoBlow(c);
                return;
            }
        }

        // c. 收钱。
        for (int i = 0; i < game.Payments.Drops.Count; i++)
        {
            PaymentDropModel drop = game.Payments.Drops[i];
            if (game.Payments.BeginCollection(drop.Id))
            {
                game.Payments.CompleteCollection(drop.Id);
                day.Stats.RecordPaymentCollected(drop);
                return;
            }
        }

        // d. 等候顾客分配到空闲且对口的工位。
        for (int i = 0; i < game.WaitingQueue.Count; i++)
        {
            CustomerModel c = game.WaitingQueue[i];
            int station = FindFreeStation(game, c.CurrentNeed);
            if (station >= 0) { game.Assign(c, station); return; }
        }

        // e. 已到站但工位不对口 —— 转工位。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State != CustomerState.Serving || c.Station < 0) continue;
            if (SalonGameModel.IsCompatibleStation(c.CurrentNeed, c.Station)) continue;
            int station = FindFreeStation(game, c.CurrentNeed);
            if (station >= 0) { game.Assign(c, station); return; }
        }

        // f. 开始洗头。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State == CustomerState.Serving && c.CurrentNeed == ServiceType.Wash &&
                !c.ShampooApplied && !game.IsWashFoamWaitRunning(c))
            {
                if (game.BeginWashFoamHold(c)) return;
            }
        }

        // g. 开始剪发 —— 这是占用玩家的主动任务。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State != CustomerState.Serving || c.CurrentNeed != ServiceType.Cut) continue;
            if (c.Station < 0 || !SalonGameModel.IsCompatibleStation(ServiceType.Cut, c.Station)) continue;
            game.SelectCustomer(c);
            if (!game.BeginHaircutAction(c, SalonTool.Scissors, haircut)) continue;
            cutting = c;
            cutRemaining = game.HaircutHoldDurationFor(SalonTool.Scissors, haircut);
            return;
        }

        // h. 启动吹发后台。
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State != CustomerState.Serving || c.CurrentNeed != ServiceType.Dry) continue;
            if (c.AutoBlowRunning) continue;
            if (game.StartAutoBlow(c)) return;
        }
    }

    // ---------------------------------------------------------------- 统计

    private static int CountActive(SalonGameModel game)
    {
        int active = 0;
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerState s = game.Customers[i].State;
            if (s == CustomerState.Exited || s == CustomerState.Finished || s == CustomerState.Leaving)
                continue;
            active++;
        }
        return active;
    }

    /// <summary>此刻真正等着玩家动手的事情数量（后台任务在等待期内不算）。</summary>
    private static int CountPendingActions(SalonGameModel game)
    {
        int pending = 0;
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State == CustomerState.Waiting || c.State == CustomerState.Entering)
            {
                pending++;
                continue;
            }
            if (c.State != CustomerState.Serving) continue;

            bool wrongStation = c.Station < 0 || !SalonGameModel.IsCompatibleStation(c.CurrentNeed, c.Station);
            if (wrongStation) { pending++; continue; }

            if (c.CurrentNeed == ServiceType.Wash)
            {
                if (game.IsWashFoamReadyToRinse(c)) pending++;
                else if (!c.ShampooApplied && !game.IsWashFoamWaitRunning(c)) pending++;
            }
            else if (c.CurrentNeed == ServiceType.Cut)
            {
                if (c.ActiveServiceAction == ActiveServiceAction.None &&
                    c.AttentionState != CustomerAttentionState.ActiveOperation) pending++;
            }
            else if (c.CurrentNeed == ServiceType.Dry)
            {
                if (!c.AutoBlowRunning) pending++;
                else if (c.BackgroundTask.Elapsed >= c.BackgroundTask.IdealStart) pending++;
            }
        }
        for (int i = 0; i < game.Payments.Drops.Count; i++)
            if (game.Payments.Drops[i].State == PaymentDropState.Pending) pending++;
        return pending;
    }

    private static int CountCollectedPayments(SalonGameModel game, BusinessDayController day)
    {
        int collected = 0;
        for (int i = 0; i < game.Payments.Drops.Count; i++)
            if (game.Payments.Drops[i].State == PaymentDropState.Collected) collected++;
        return collected;
    }

    // ---------------------------------------------------------------- 辅助

    private static bool TrySpawn(SalonGameModel game, BusinessDayController day, int id, int spawnIndex)
    {
        string orderId = SalonMobileDayConfig.PickOrderForSpawn(
            day.Config, spawnIndex, day.BusinessProgress);
        var needs = new List<ServiceType>(SalonOrderCatalog.Get(orderId));
        CustomerModel customer = game.Spawn(id, needs);
        if (customer == null) return false;
        game.ConfigureHaircutOrder(customer, SalonTool.Scissors);
        day.Stats.RecordSpawn(customer);
        return true;
    }

    private static int FindFreeStation(SalonGameModel game, ServiceType need)
    {
        for (int i = 0; i < game.Workstations.Count; i++)
        {
            if (game.Workstations[i].Occupied) continue;
            if (!SalonGameModel.IsCompatibleStation(need, game.Workstations[i].Type)) continue;
            return i;
        }
        return -1;
    }

    private static TrafficSnapshot BuildSnapshot(SalonGameModel game)
    {
        int active = 0, waiting = 0, occupied = 0, angry = 0;
        for (int i = 0; i < game.Customers.Count; i++)
        {
            CustomerModel c = game.Customers[i];
            if (c.State == CustomerState.Exited) continue;
            active++;
            if (c.State == CustomerState.Entering || c.State == CustomerState.Waiting) waiting++;
            if (c.Emotion == CustomerEmotion.Angry) angry++;
        }
        for (int i = 0; i < game.Workstations.Count; i++)
            if (game.Workstations[i].Occupied) occupied++;
        return new TrafficSnapshot(active, waiting, occupied, angry, 4);
    }
}
