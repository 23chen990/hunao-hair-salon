using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class CustomerEscortRegressionTests
{
    private GameObject _root;
    private SalonDemo _demo;
    private Transform _leader;
    private SalonCustomerView _follower;
    private List<Rect> _obstacles;
    private const float Frame = 1f / 60f;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Customer escort regression");
        _demo = _root.AddComponent<SalonDemo>();
        _leader = new GameObject("Leader").transform;
        _leader.SetParent(_root.transform);
        _follower = new GameObject("Follower").AddComponent<SalonCustomerView>();
        _follower.transform.SetParent(_root.transform);
        var game = new SalonGameModel();
        _follower.Customer = game.Spawn(17, new[] { ServiceType.Cut });
        _follower.Customer.State = CustomerState.Waiting;
        Set("_mobileMode", true);
        Set("_game", game);
        Set("_player", _leader);
        Set("_mobileGuidedCustomer", _follower.Customer);
        Set("_mobileFloor", Rect.MinMaxRect(-20f, -20f, 20f, 20f));
        _obstacles = (List<Rect>)Field("_mobileObstacles").GetValue(_demo);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void WalkingCustomerKeepsPersonalSpaceBehindLeader()
    {
        _follower.transform.position = new Vector3(-1.1f, .4f, 0f);
        for (int i = 0; i < 180; i++)
        {
            _leader.position += Vector3.right * (5.5f * Frame);
            Step();
            if (i < 30) continue;
            float gap = _leader.position.x - _follower.transform.position.x;
            Assert.That(gap, Is.InRange(.9f, 2.2f),
                "A walking customer should trail the stylist with visible personal space.");
        }
    }

    [Test]
    public void SlowWalkingAndStoppingDoNotMakeCustomerSprintInShortBursts()
    {
        _follower.transform.position = new Vector3(-1.1f, .4f, 0f);
        for (int i = 0; i < 240; i++)
        {
            Vector3 before = _follower.transform.position;
            _leader.position += Vector3.right * (.3f * Frame);
            Step();
            Assert.That(FlatDistance(before, _follower.transform.position), Is.LessThan(.025f),
                "A slow leader needs a slow follower, not repeated full-speed catch-up steps.");
        }
        for (int i = 0; i < 180; i++) Step();
        Vector3 settled = _follower.transform.position;
        for (int i = 0; i < 120; i++) Step();
        Assert.That(FlatDistance(settled, _follower.transform.position), Is.LessThan(.015f));
        Assert.That(FlatDistance(_leader.position, settled), Is.InRange(.95f, 1.3f));
    }

    [Test]
    public void ReselectingCustomerDropsThePreviousEscortRoute()
    {
        _leader.position = Vector3.right * 3f;
        Step(0f);
        _leader.position = Vector3.right * 6f;
        Step(0f);
        Set("_mobileGuidedCustomer", null);
        Assert.IsFalse(Step());
        _leader.position = Vector3.left * 2f;
        Set("_mobileGuidedCustomer", _follower.Customer);
        Step(.1f);
        Assert.That(_follower.transform.position.x, Is.LessThan(0f),
            "Reception must start towards the current stylist, never an old trail.");
    }

    [Test]
    public void GettingUpFromFurnitureMovesTowardsLeaderWithoutTeleporting()
    {
        _obstacles.Add(Rect.MinMaxRect(-2f, -.75f, 2f, .75f));
        _leader.position = new Vector3(0f, .05f, 2f);
        _follower.transform.position = new Vector3(0f, .4f, 0f);
        Vector3 before = _follower.transform.position;
        Step();
        Assert.That(FlatDistance(before, _follower.transform.position), Is.LessThan(.11f),
            "Standing up must not snap the customer to a randomly chosen furniture edge.");
        Assert.That(_follower.transform.position.z, Is.GreaterThan(0f));
        for (int i = 0; i < 240; i++) Step();
        Assert.IsFalse(_obstacles[0].Contains(new Vector2(_follower.transform.position.x, _follower.transform.position.z)));
        Assert.That(FlatDistance(_leader.position, _follower.transform.position), Is.InRange(.95f, 1.3f));
    }

    [Test]
    public void CustomerCanJoinLeaderAroundFurnitureInsteadOfGettingStuck()
    {
        _obstacles.Add(Rect.MinMaxRect(-.4f, -.7f, .4f, .7f));
        _leader.position = new Vector3(1f, .05f, 0f);
        _follower.transform.position = new Vector3(-1f, .4f, 0f);
        for (int i = 0; i < 480; i++)
        {
            Step();
            Assert.IsFalse(_obstacles[0].Contains(new Vector2(_follower.transform.position.x, _follower.transform.position.z)));
        }
        Assert.That(FlatDistance(_leader.position, _follower.transform.position), Is.LessThan(1.3f),
            "A customer greeted across a furniture corner must find the nearby free aisle.");
    }

    [Test]
    public void FollowingTurnsUseTheWalkedAisleAndSettleAfterLeaderStops()
    {
        _obstacles.Add(Rect.MinMaxRect(-1f, -1f, 1f, 1f));
        _leader.position = new Vector3(-2f, .05f, -1.5f);
        _follower.transform.position = new Vector3(-3.1f, .4f, -1.5f);
        foreach (Vector3 destination in new[] { new Vector3(1.5f, .05f, -1.5f), new Vector3(1.5f, .05f, 2f) })
            while (FlatDistance(_leader.position, destination) > .001f)
            {
                _leader.position = Vector3.MoveTowards(_leader.position, destination, 3f * Frame);
                Step();
                Assert.IsFalse(_obstacles[0].Contains(new Vector2(_follower.transform.position.x, _follower.transform.position.z)));
                Assert.That(FlatDistance(_leader.position, _follower.transform.position), Is.LessThan(2.2f));
            }
        for (int i = 0; i < 180; i++) Step();
        Assert.That(_follower.transform.position.x, Is.EqualTo(1.5f).Within(.05f));
        Assert.That(_leader.position.z - _follower.transform.position.z, Is.InRange(1f, 1.15f));
    }

    [Test]
    public void ChangingLeaderStartsANewRouteEvenWithoutAnUnselectedFrame()
    {
        _leader.position = Vector3.right * 3f;
        Step(0f);
        Transform nextLeader = new GameObject("Other stylist").transform;
        nextLeader.SetParent(_root.transform);
        nextLeader.position = Vector3.left * 2f;
        Set("_player", nextLeader);
        Step(.1f);
        Assert.That(_follower.transform.position.x, Is.LessThan(0f));
    }

    private bool Step(float dt = Frame) => (bool)typeof(SalonDemo).GetMethod("TryMoveMobileFollower",
        BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_demo, new object[] { _follower, dt });
    private void Set(string name, object value) => Field(name).SetValue(_demo, value);
    private static FieldInfo Field(string name) => typeof(SalonDemo).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
