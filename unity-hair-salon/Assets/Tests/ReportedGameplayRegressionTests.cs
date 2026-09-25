using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HairSalon;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Regression checks for the two reported mobile gameplay failures.
/// These tests intentionally exercise the SalonDemo integration boundary:
/// the model can have the right lifecycle while the view/target hand-off is
/// still cleared or locked by the presentation layer.
/// </summary>
public sealed class ReportedGameplayRegressionTests
{
    [Test]
    public void ResultTransitionDoesNotRemoveLeavingCustomerBeforeExited()
    {
        var root = new GameObject("Reported exit timing regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            day.PrepareDay(1);
            day.StartBusiness();

            CustomerModel first = game.Spawn(1, new[] { ServiceType.Cut });
            first.State = CustomerState.Leaving;
            first.StateElapsed = .25f;

            var viewRoot = new GameObject("顾客 1 视图");
            viewRoot.transform.SetParent(root.transform, false);
            var view = viewRoot.AddComponent<SalonCustomerView>();
            view.Customer = first;
            List<SalonCustomerView> views = Field<List<SalonCustomerView>>(demo, "_customerViews");
            views.Add(view);

            SetField(demo, "_game", game);
            SetField(demo, "_dayController", day);
            // The destructive result callback is shared by desktop and mobile;
            // keep the harness focused on the lifecycle boundary so it does not
            // require constructing the complete mobile HUD.
            SetField(demo, "_mobileMode", false);

            // This is the integration boundary that used to call
            // ForceCloseRemainingCustomers/ClearCustomerViews while the first
            // customer was still on the visible Leaving route.
            Invoke(demo, "HandleDayStateChanged", DayState.Result);

            Assert.AreEqual(CustomerState.Leaving, first.State,
                "A result callback must not turn a visible Leaving customer into Exited.");
            Assert.AreEqual(1, views.Count,
                "The customer view must remain registered until the model reaches Exited.");
            Assert.IsTrue(viewRoot.activeSelf,
                "The first customer must stay visible during the Leaving animation.");

            first.State = CustomerState.Exited;
            Assert.AreEqual(CustomerState.Exited, first.State);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void DeferredResultFinalizesAfterLastLeavingCustomerExits()
    {
        var root = new GameObject("Reported deferred result regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            day.PrepareDay(1);
            day.StartBusiness();
            day.ForceResult();

            CustomerModel customer = game.Spawn(1, new[] { ServiceType.Cut });
            customer.State = CustomerState.Leaving;
            var viewObject = new GameObject("Customer exit view");
            viewObject.transform.SetParent(root.transform, false);
            var view = viewObject.AddComponent<SalonCustomerView>();
            view.Customer = customer;
            List<SalonCustomerView> views = Field<List<SalonCustomerView>>(demo, "_customerViews");
            views.Add(view);

            var resultPanel = new GameObject("Result panel");
            resultPanel.transform.SetParent(root.transform, false);
            resultPanel.SetActive(false);
            SetField(demo, "_game", game);
            SetField(demo, "_dayController", day);
            SetField(demo, "_mobileMode", false);
            SetField(demo, "_resultPanel", resultPanel);

            Invoke(demo, "HandleDayStateChanged", DayState.Result);
            Assert.IsFalse(resultPanel.activeSelf,
                "The result panel must wait while the leaving customer is still visible.");
            Assert.IsFalse(game.FirstDayCompleteForShop,
                "Day settlement must not complete before the customer reaches Exited.");

            customer.State = CustomerState.Exited;
            LogAssert.Expect(LogType.Error,
                new Regex("Customer exit view: Destroy may not be called from edit mode!"));
            Invoke(demo, "UpdateCustomerViews");

            Assert.AreEqual(0, views.Count,
                "The exited customer's view must be removed from the presentation list.");
            Assert.IsTrue(resultPanel.activeSelf,
                "A Result callback deferred during Leaving must be completed after the last customer exits.");
            Assert.IsTrue(game.FirstDayCompleteForShop,
                "Deferred result settlement must complete the day after the exit route finishes.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void WaitingSecondCustomerCanBeGreetedAfterFirstGuidedCustomerStartsLeaving()
    {
        var root = new GameObject("Reported second customer regression");
        try
        {
            var demo = root.AddComponent<SalonDemo>();
            var game = new SalonGameModel();
            var day = new BusinessDayController(SalonMobileDayConfig.CreateForDay(1));
            day.PrepareDay(1);
            day.StartBusiness();

            CustomerModel first = game.Spawn(1, new[] { ServiceType.Cut });
            CustomerModel second = game.Spawn(2, new[] { ServiceType.Cut });
            game.Tick(SalonGameModel.EnteringSeconds + .01f);
            first.State = CustomerState.Leaving;
            first.Station = -1;
            second.State = CustomerState.Waiting;

            SetField(demo, "_game", game);
            SetField(demo, "_dayController", day);
            SetField(demo, "_mobileMode", true);
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            player.transform.position = Vector3.zero;
            SetField(demo, "_player", player.transform);

            var firstView = AddCustomerView(root.transform, first, new Vector3(4f, 0f, 0f));
            var secondView = AddCustomerView(root.transform, second, new Vector3(.5f, 0f, 0f));
            SetReachedDestination(firstView, true);
            SetReachedDestination(secondView, true);
            List<SalonCustomerView> views = Field<List<SalonCustomerView>>(demo, "_customerViews");
            views.Add(firstView);
            views.Add(secondView);

            var stationAnchor = new GameObject("Free haircut anchor");
            stationAnchor.transform.SetParent(root.transform, false);
            stationAnchor.transform.position = Vector3.zero;
            Dictionary<int, Transform> anchors = Field<Dictionary<int, Transform>>(demo, "_playerServiceAnchors");
            anchors[1] = stationAnchor.transform;

            // Simulate the stale hand-off reference left by the first customer
            // while its view is still leaving. It must not lock the waiting
            // second customer out of the normal Greet -> Guide flow.
            SetField(demo, "_mobileGuidedCustomer", first);
            object target = Invoke(demo, "FindMobileTarget");
            CustomerModel targetCustomer = (CustomerModel)target.GetType().GetField("Customer").GetValue(target);
            object action = target.GetType().GetField("Action").GetValue(target);
            bool available = (bool)target.GetType().GetField("Available").GetValue(target);

            Assert.AreEqual(second.Id, targetCustomer == null ? -1 : targetCustomer.Id,
                "A Leaving customer cannot remain the mobile target after its hand-off is over.");
            Assert.AreEqual("Greet", action.ToString(),
                "The second waiting customer must expose the normal greeting action.");
            Assert.IsTrue(available, "The second customer must be interactable at its waiting position.");

            Invoke(demo, "ExecuteMobileTarget", target);
            Assert.AreSame(second, Field<CustomerModel>(demo, "_mobileGuidedCustomer"),
                "Executing the target must start reception for customer two.");
            Assert.IsTrue(second.HasServiceEngaged,
                "Greeting customer two must engage its hand-off timer.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static SalonCustomerView AddCustomerView(Transform parent, CustomerModel customer, Vector3 position)
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
