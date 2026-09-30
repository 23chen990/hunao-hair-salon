using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class FirstSessionTaskSummaryTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private SalonGameModel _game;
    private Text _summary;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("First session task summary");
        _demo = _root.AddComponent<SalonDemo>();
        _game = new SalonGameModel();
        var label = new GameObject("Tasks", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(_root.transform);
        _summary = label.GetComponent<Text>();
        Set("_game", _game);
        Set("_mobileTaskSummaryLabel", _summary);
        var progress = SalonProgressData.CreateDefault();
        progress.WashStationPurchased = true; progress.WashStationPaid = 1200;
        typeof(SalonDemo).GetMethod("CreateMobileUnlockPads", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_demo, new object[] { progress });
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void FoamReadyCallsForRinsingButAlreadyRinsedDoesNot()
    {
        var customer = Customer(ServiceType.Wash);
        customer.WashStage = WashStage.ReadyToRinse;
        customer.ShampooApplied = true;
        customer.BackgroundTask.State = BackgroundTaskState.Running;
        customer.BackgroundTask.Elapsed = _game.ServiceConfig.FoamOptimalStart;
        Refresh();
        StringAssert.Contains("待冲洗 1", _summary.text);
        customer.WashStage = WashStage.Rinsed;
        customer.ShampooApplied = false;
        Refresh();
        StringAssert.DoesNotContain("冲洗", _summary.text);
    }

    [Test]
    public void BlowFinishingIsVisibleAsSoonAsTheReturnWindowOpens()
    {
        var customer = Customer(ServiceType.Dry);
        customer.AutoBlowRunning = true;
        customer.BackgroundTask.IdealStart = 12f;
        customer.BackgroundTask.Elapsed = 11f;
        Refresh();
        StringAssert.DoesNotContain("吹发收尾", _summary.text);
        customer.BackgroundTask.Elapsed = 12f;
        Refresh();
        StringAssert.Contains("吹发收尾 1", _summary.text);
        customer.AutoBlowRunning = false;
        customer.AutoBlowSafetyStopped = true;
        Refresh();
        StringAssert.Contains("吹发收尾 1", _summary.text);
    }

    [Test]
    public void GuidedCustomerIsNotAdvertisedAsNeedingAnotherGreeting()
    {
        var customer = Customer(ServiceType.Wash);
        customer.State = CustomerState.Waiting;
        Set("_mobileGuidedCustomer", customer);
        Refresh();
        StringAssert.DoesNotContain("待接", _summary.text);
    }

    [Test]
    public void EmptyStockExplainsTheNextSupplyActionInChinese()
    {
        Set("_mobileSupplies", new SalonSupplyModel(12, 3, 6));
        Refresh();
        StringAssert.Contains("需补货", _summary.text);
        StringAssert.DoesNotContain("LOW", _summary.text);
    }

    [Test]
    public void EmptyRackDirectsPlayerToPickUpThenDeliverTheCarriedSupplies()
    {
        var supplies = new SalonSupplyModel(12, 3, 6);
        Set("_mobileSupplies", supplies);
        Refresh();
        StringAssert.Contains("右侧取货", _summary.text);
        Assert.IsTrue(supplies.TryPickUpWashKit());
        Refresh();
        StringAssert.Contains("送回洗发区", _summary.text);
        StringAssert.DoesNotContain("右侧取货", _summary.text);
        Assert.IsTrue(supplies.TryDeliverWashKit());
        Refresh();
        StringAssert.DoesNotContain("需补货", _summary.text);
    }

    [Test]
    public void ExhaustedSuppliesDoNotDirectPlayerToAnEmptySource()
    {
        Set("_mobileSupplies", new SalonSupplyModel(0, 3, 6));
        Refresh();
        StringAssert.Contains("用品已用完", _summary.text);
        StringAssert.DoesNotContain("右侧取货", _summary.text);
    }

    private CustomerModel Customer(ServiceType service)
    {
        var customer = _game.Spawn(41, new List<ServiceType> { service });
        customer.State = CustomerState.Serving;
        return customer;
    }

    private void Set(string name, object value) => typeof(SalonDemo)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_demo, value);

    private void Refresh() => typeof(SalonDemo)
        .GetMethod("UpdateMobileTaskSummary", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(_demo, null);
}
