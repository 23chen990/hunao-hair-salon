using System;
using System.Collections.Generic;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class Phase5ConcurrentFlowTests
{
    [Test]
    public void WaitingSlotsCompactForwardWhenTheHeadLeaves()
    {
        var registry = new CustomerWaitingSlotRegistry();
        var a = new CustomerModel { Id = 1 };
        var b = new CustomerModel { Id = 2 };
        var c = new CustomerModel { Id = 3 };
        Assert.AreEqual(0, registry.Reserve(a, 4));
        Assert.AreEqual(1, registry.Reserve(b, 4));
        Assert.AreEqual(2, registry.Reserve(c, 4));

        registry.Release(a);

        Assert.AreEqual(0, registry.Reserve(b, 4));
        Assert.AreEqual(1, registry.Reserve(c, 4));
    }

    [Test]
    public void PlayerCanChooseALaterCustomerWhileWaitingSeatsRemainFifo()
    {
        var game = new SalonGameModel();
        CustomerModel first = game.Spawn(10, CutOrder());
        CustomerModel second = game.Spawn(11, CutOrder());
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.Assign(second, 1));
        Assert.AreEqual(0, game.GetWaitingSlot(first));
        Assert.IsTrue(game.Assign(first, 2));
    }

    [Test]
    public void AssignmentImmediatelyMarksTheStationAsCustomerEnRoute()
    {
        Assert.IsTrue(Enum.IsDefined(typeof(WorkstationState), "CustomerEnRoute"));
        var game = new SalonGameModel();
        CustomerModel customer = game.Spawn(12, CutOrder());
        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.IsTrue(game.Assign(customer, 1));
        Assert.AreEqual("CustomerEnRoute", game.Workstations[1].State.ToString());
        Assert.AreEqual(customer.Id, game.Workstations[1].CurrentCustomerId);
    }

    [Test]
    public void FormalServiceResumesPatiencePressureAfterTheDelayGrace()
    {
        var game = new SalonGameModel();
        CustomerModel customer = SpawnServing(game, 13);
        customer.Patience = 20f;
        Assert.IsTrue(game.BeginActiveOperation(customer));
        Assert.IsTrue(game.EndActiveOperation(customer));

        game.Tick(5f);
        game.Tick(100f);

        Assert.AreEqual(CustomerState.Leaving, customer.State);
        Assert.AreEqual(0f, customer.Patience, .001f);
        Assert.AreEqual(1, game.AngryLeaves);
        Assert.IsTrue(customer.HasServiceEngaged);

        game.Tick(SalonGameModel.LeavingSeconds + .01f);
        Assert.AreEqual(CustomerState.Exited, customer.State);
    }

    [Test]
    public void FlowTuningIsCentralizedAndInjectedIntoTheModel()
    {
        Type flowType = typeof(SalonGameModel).Assembly.GetType("HairSalon.SalonFlowConfig");
        Assert.IsNotNull(flowType);
        Assert.IsNotNull(flowType.GetField("MinSpawnInterval"));
        Assert.IsNotNull(flowType.GetField("MaxSpawnInterval"));
        Assert.IsNotNull(flowType.GetField("MaxCustomers"));
        Assert.IsNotNull(flowType.GetField("WaitingCapacity"));
        Assert.IsNotNull(typeof(SalonGameModel).GetProperty("FlowConfig"));

        var flow = new SalonFlowConfig { MaxCustomers = 3, WaitingCapacity = 2 };
        var game = new SalonGameModel(flowConfig: flow);
        Assert.IsNotNull(game.Spawn(30, CutOrder()));
        Assert.IsNotNull(game.Spawn(31, CutOrder()));
        Assert.IsNull(game.Spawn(32, CutOrder()));
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.AreEqual(2, game.AutoAssignWaitingCustomers());
        Assert.IsNotNull(game.Spawn(32, CutOrder()),
            "Spawning must resume after the waiting queue has legal capacity again.");
    }

    [Test]
    public void WaitingEmotionThresholdsAreCentralizedInPatienceConfig()
    {
        Type config = typeof(CustomerPatienceConfig);
        Assert.IsNotNull(config.GetField("InitialPatience"));
        Assert.IsNotNull(config.GetField("ImpatientAtPatience"));
        Assert.IsNotNull(config.GetField("AngryAtPatience"));
    }

    [Test]
    public void WaitingQueueIsExposedAsTheSingleOrderedSource()
    {
        var game = new SalonGameModel();
        CustomerModel first = game.Spawn(40, CutOrder());
        CustomerModel second = game.Spawn(41, CutOrder());
        Assert.AreEqual(0, game.GetWaitingSlot(first));
        Assert.AreEqual(1, game.GetWaitingSlot(second));
        CollectionAssert.AreEqual(new[] { first, second }, game.WaitingQueue);

        game.Tick(SalonGameModel.EnteringSeconds + .01f);

        Assert.AreEqual(2, game.AutoAssignWaitingCustomers());
        Assert.AreEqual(0, game.WaitingQueue.Count);
        Assert.AreNotEqual(first.Station, second.Station);
        Assert.AreEqual(CustomerState.MovingToStation, first.State);
        Assert.AreEqual(CustomerState.MovingToStation, second.State);
    }

    [Test]
    public void ImpatientHeadLeavingCompactsTheModelQueueWithoutPayment()
    {
        var game = new SalonGameModel(
            patienceConfig: new CustomerPatienceConfig { DrainPerSecond = 10f });
        CustomerModel first = game.Spawn(50, CutOrder());
        CustomerModel second = game.Spawn(51, CutOrder());
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        first.Patience = .1f;

        game.Tick(.02f);

        Assert.AreEqual(CustomerState.Leaving, first.State);
        Assert.AreEqual(0, game.GetWaitingSlot(second));
        CollectionAssert.AreEqual(new[] { second }, game.WaitingQueue);
        Assert.AreEqual(0, game.Payments.Drops.Count);
    }

    [Test]
    public void UndercutBlocksTheStationWhileOtherCustomersKeepLosingPatience()
    {
        var game = new SalonGameModel();
        CustomerModel active = game.Spawn(20, CutOrder());
        CustomerModel waiting = game.Spawn(21, CutOrder());
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(active, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        game.ConfigureHaircutOrder(active, SalonTool.Scissors);
        float waitingBefore = waiting.Patience;

        Assert.IsTrue(game.BeginActiveOperation(active));
        Assert.IsTrue(game.ApplyHaircutResult(active, SalonTool.Scissors, HaircutResult.Undercut, new HaircutConfig()));
        Assert.IsTrue(game.EndActiveOperation(active));
        game.Tick(10f);

        Assert.AreEqual(CustomerState.Serving, active.State);
        Assert.AreEqual(1, active.Station);
        Assert.AreEqual(active.Id, game.Workstations[1].CurrentCustomerId);
        Assert.AreEqual(WorkstationState.Rework, game.Workstations[1].State);
        Assert.Less(waiting.Patience, waitingBefore);
    }

    [Test]
    public void EmotionSlotUsesOneMutuallyExclusiveVisualKind()
    {
        var root = new GameObject("Phase 5 Emotion Test");
        var cameraRoot = new GameObject("Camera", typeof(Camera));
        var customer = new CustomerModel();
        var view = root.AddComponent<CustomerEmotionView>();
        view.Initialize(customer, cameraRoot.GetComponent<Camera>());

        customer.Emotion = CustomerEmotion.Impatient;
        view.Refresh();
        Assert.AreEqual("BlueSweat", view.VisualKind);
        customer.Emotion = CustomerEmotion.Angry;
        view.Refresh();
        Assert.AreEqual("RedAnger", view.VisualKind);
        customer.Emotion = CustomerEmotion.Happy;
        view.Refresh();
        Assert.AreEqual("GreenHappy", view.VisualKind);

        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(cameraRoot);
    }

    [Test]
    public void FiveMinuteConcurrentSoakDoesNotDeadlockQueuesOrStations()
    {
        var game = new SalonGameModel(
            flowConfig: new SalonFlowConfig { MaxCustomers = 5, WaitingCapacity = 4 },
            patienceConfig: new CustomerPatienceConfig { DrainPerSecond = 2.4f });
        var retried = new HashSet<int>();
        int nextId = 1000;

        for (int frame = 0; frame < 600; frame++)
        {
            if ((frame & 3) == 0)
            {
                CustomerModel spawned = game.Spawn(nextId, CutOrder());
                if (spawned != null) nextId++;
            }
            game.Tick(.5f);
            game.AutoAssignWaitingCustomers();

            var snapshot = new List<CustomerModel>(game.Customers);
            foreach (CustomerModel customer in snapshot)
            {
                if (customer.State != CustomerState.Serving) continue;
                Assert.IsTrue(game.BeginActiveOperation(customer));
                if ((customer.Id & 3) == 0 && retried.Add(customer.Id))
                {
                    Assert.IsTrue(game.ApplyHaircutResult(
                        customer, SalonTool.Scissors, HaircutResult.Undercut, new HaircutConfig()));
                }
                else
                {
                    Assert.IsTrue(game.ApplyHaircutResult(
                        customer, SalonTool.Scissors, HaircutResult.Perfect, new HaircutConfig()));
                }
                game.EndActiveOperation(customer);
            }

            Assert.LessOrEqual(game.Customers.Count, game.FlowConfig.MaxCustomers);
            Assert.LessOrEqual(game.WaitingQueue.Count, game.FlowConfig.WaitingCapacity);
            for (int slot = 0; slot < game.WaitingQueue.Count; slot++)
                Assert.AreEqual(slot, game.GetWaitingSlot(game.WaitingQueue[slot]));
            Assert.AreNotEqual(
                game.Workstations[1].CurrentCustomerId >= 0 ? game.Workstations[1].CurrentCustomerId : -10,
                game.Workstations[2].CurrentCustomerId >= 0 ? game.Workstations[2].CurrentCustomerId : -20);
        }

        Assert.AreEqual(300f, game.WorldElapsed, .001f);
        Assert.Greater(game.Served + game.AngryLeaves, 20);
    }

    private static List<ServiceType> CutOrder()
    {
        return new List<ServiceType> { ServiceType.Cut };
    }

    private static CustomerModel SpawnServing(SalonGameModel game, int id)
    {
        CustomerModel customer = game.Spawn(id, CutOrder());
        game.Tick(SalonGameModel.EnteringSeconds + .01f);
        Assert.IsTrue(game.Assign(customer, 1));
        game.Tick(SalonGameModel.MovingToStationSeconds + .01f);
        Assert.AreEqual(CustomerState.Serving, customer.State);
        return customer;
    }
}
