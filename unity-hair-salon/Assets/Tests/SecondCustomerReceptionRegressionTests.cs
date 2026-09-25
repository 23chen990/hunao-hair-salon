using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Exercises the real first-day second order (O002: wash, then blow-dry)
/// through the mobile target picker and workstation hand-off.
/// </summary>
public sealed class SecondCustomerReceptionRegressionTests
{
    [Test]
    public void GuidedWashCustomerExplainsNextStepAndCanBeAssigned()
    {
        var root = new GameObject("Second customer wash reception regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            day.PrepareDay(1);
            day.StartBusiness();

            CustomerModel first = game.Spawn(0, new List<ServiceType>(SalonOrderCatalog.Get("O001")));
            CustomerModel second = game.Spawn(1, new List<ServiceType>(SalonOrderCatalog.Get("O002")));
            Assert.AreEqual(ServiceType.Wash, second.CurrentNeed,
                "The deterministic second order must start with washing.");

            first.State = CustomerState.Leaving;
            first.Station = -1;
            second.State = CustomerState.Waiting;

            SetField(demo, "_game", game);
            SetField(demo, "_dayController", day);
            SetField(demo, "_mobileMode", true);

            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            // This is the measured browser position after reaching customer 2
            // in the queue, beside but outside the waiting-furniture collider.
            player.transform.position = new Vector3(-7.735f, .05f, -3.379f);
            SetField(demo, "_player", player.transform);

            SalonCustomerView firstView = AddCustomerView(root.transform, first,
                new Vector3(-9f, .4f, -4.3f));
            SalonCustomerView secondView = AddCustomerView(root.transform, second,
                new Vector3(-7.75f, .4f, -4.3f));
            SetReachedDestination(firstView, true);
            SetReachedDestination(secondView, true);
            List<SalonCustomerView> views = Field<List<SalonCustomerView>>(demo, "_customerViews");
            views.Add(firstView);
            views.Add(secondView);

            var washAnchor = new GameObject("Primary wash service anchor");
            washAnchor.transform.SetParent(root.transform, false);
            washAnchor.transform.position = SalonDemo.WashPlayerAnchorPosition;
            Dictionary<int, Transform> anchors = Field<Dictionary<int, Transform>>(demo,
                "_playerServiceAnchors");
            anchors[0] = washAnchor.transform;

            // A Leaving customer's old guided reference must not beat the
            // actual second waiting order at the queue position.
            SetField(demo, "_mobileGuidedCustomer", first);
            object greet = Invoke(demo, "FindMobileTarget");
            AssertTarget(greet, second, "Greet", true);
            Invoke(demo, "ExecuteMobileTarget", greet);
            Assert.AreSame(second, Field<CustomerModel>(demo, "_mobileGuidedCustomer"));
            Assert.IsTrue(second.HasServiceEngaged,
                "Greeting customer 2 must stop the ordinary waiting timer.");

            // The real touch run showed the confusing dead-looking state here:
            // the player is still at the queue, so no service anchor is in range.
            // Keep the guided customer as the disabled action target and explain
            // that customer 2 is already received and name the compatible station.
            object escorted = Invoke(demo, "FindMobileTarget");
            Assert.AreSame(second, escorted.GetType().GetField("Customer").GetValue(escorted),
                "A successful Greet must not fall back to a targetless '靠近顾客' state.");
            Assert.AreEqual("Assign", escorted.GetType().GetField("Action").GetValue(escorted).ToString(),
                "The next mobile action must remain the guided customer's station assignment.");
            Assert.IsFalse((bool)escorted.GetType().GetField("Available").GetValue(escorted),
                "Assignment remains disabled until the player reaches a compatible station anchor.");
            string hint = (string)escorted.GetType().GetField("Hint").GetValue(escorted);
            StringAssert.Contains("已接待", hint,
                "After a successful greet, the disabled generic '靠近顾客' state must not hide reception.");
            StringAssert.Contains("洗发工位", hint,
                "The player needs an explicit next step for the real O002 wash order.");

            // Mirror the real route's reachable position near the wash anchor.
            player.transform.position = new Vector3(-8.647f, .05f, 2.636f);
            object assign = Invoke(demo, "FindMobileTarget");
            AssertTarget(assign, second, "Assign", true);
            Assert.AreEqual(0, (int)assign.GetType().GetField("Station").GetValue(assign),
                "The second order must target the compatible, free wash station.");

            Invoke(demo, "ExecuteMobileTarget", assign);
            Assert.AreEqual(CustomerState.MovingToStation, second.State);
            Assert.AreEqual(0, second.Station);
            Assert.IsTrue(second.HasServiceEngaged,
                "Assigning after greeting must preserve the received state.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void GuidedWashCustomerCanBeAssignedToWrongHaircutStationWithFeedback()
    {
        var root = new GameObject("Second customer wrong station freedom regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            CustomerModel washCustomer = game.Spawn(1,
                new List<ServiceType> { ServiceType.Wash, ServiceType.Dry });
            washCustomer.State = CustomerState.Waiting;
            Assert.IsTrue(game.EngageCustomerForHandoff(washCustomer));

            SetField(demo, "_game", game);
            SetField(demo, "_mobileMode", true);
            var player = new GameObject("Player beside wrong haircut station");
            player.transform.SetParent(root.transform, false);
            player.transform.position = Vector3.zero;
            SetField(demo, "_player", player.transform);
            SetField(demo, "_mobileGuidedCustomer", washCustomer);

            // The only compatible Wash anchor is deliberately far away. The
            // player has reached an available Haircut station instead.
            var washAnchor = new GameObject("Wash service anchor");
            washAnchor.transform.SetParent(root.transform, false);
            washAnchor.transform.position = new Vector3(5f, 0f, 0f);
            var haircutAnchor = new GameObject("Haircut service anchor");
            haircutAnchor.transform.SetParent(root.transform, false);
            haircutAnchor.transform.position = Vector3.zero;
            Dictionary<int, Transform> anchors = Field<Dictionary<int, Transform>>(
                demo, "_playerServiceAnchors");
            anchors[0] = washAnchor.transform;
            anchors[1] = haircutAnchor.transform;

            object wrongStationTarget = Invoke(demo, "FindMobileTarget");
            Assert.AreSame(washCustomer,
                wrongStationTarget.GetType().GetField("Customer").GetValue(wrongStationTarget));
            Assert.AreEqual("Assign",
                wrongStationTarget.GetType().GetField("Action").GetValue(wrongStationTarget).ToString());
            Assert.AreEqual(1, (int)wrongStationTarget.GetType().GetField("Station")
                    .GetValue(wrongStationTarget),
                "The reached, available haircut station must remain a valid mistake the player can attempt.");
            Assert.IsTrue((bool)wrongStationTarget.GetType().GetField("Available")
                    .GetValue(wrongStationTarget),
                "Being at the wrong service station must not silently disable the action.");

            Assert.AreSame(washCustomer, Field<CustomerModel>(demo, "_mobileGuidedCustomer"),
                "The hand-off target must remain selected until the attempted assignment is handled.");
            Invoke(demo, "ExecuteMobileTarget", wrongStationTarget);

            Assert.AreEqual(CustomerState.MovingToStation, washCustomer.State,
                "The intentional wrong-station attempt is allowed to proceed.");
            Assert.AreEqual(1, washCustomer.Station);
            Assert.AreEqual(1, washCustomer.WrongStationCount,
                "The customer must register the wrong-station consequence.");
            Assert.AreEqual(CustomerReactionKind.Confused, washCustomer.ReactionKind,
                "The customer reaction makes the wrong assignment visible to the player.");
            Assert.IsNull(Field<CustomerModel>(demo, "_mobileGuidedCustomer"),
                "The hand-off should clear only after the attempted assignment was accepted by the model.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static SalonCustomerView AddCustomerView(Transform parent, CustomerModel customer,
        Vector3 position)
    {
        var viewObject = new GameObject("Customer " + customer.Id);
        viewObject.transform.SetParent(parent, false);
        viewObject.transform.position = position;
        var view = viewObject.AddComponent<SalonCustomerView>();
        view.Customer = customer;
        return view;
    }

    private static void SetReachedDestination(SalonCustomerView view, bool reached)
    {
        typeof(SalonCustomerView).GetProperty("IsAtMovementDestination",
            BindingFlags.Instance | BindingFlags.Public).SetValue(view, reached);
    }

    private static void AssertTarget(object target, CustomerModel customer, string action, bool available)
    {
        Assert.AreSame(customer, target.GetType().GetField("Customer").GetValue(target));
        Assert.AreEqual(action, target.GetType().GetField("Action").GetValue(target).ToString());
        Assert.AreEqual(available, (bool)target.GetType().GetField("Available").GetValue(target));
    }

    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, name);
        field.SetValue(target, value);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, name);
        return method.Invoke(target, args);
    }
}
