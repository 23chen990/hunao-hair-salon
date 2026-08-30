using HairSalon;
using HairSalon.ServiceArchitecture;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DemandBubbleVisualRestorationTests
{
    [Test]
    public void CustomerAlreadyAtAStationKeepsDemandAndProgressVisibleWithoutSelection()
    {
        MethodInfo policy = typeof(SalonDemo).GetMethod("ShouldShowCustomerStatusAtStation",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(policy);
        var customer = new CustomerModel
        {
            State = CustomerState.Serving,
            Station = 1,
            Needs = new List<ServiceType> { ServiceType.Dye }
        };

        Assert.IsTrue((bool)policy.Invoke(null, new object[] { customer, false, true }),
            "A seated customer must keep overhead demand/progress visible even while the player serves elsewhere.");
        customer.State = CustomerState.Waiting;
        customer.Station = -1;
        Assert.IsFalse((bool)policy.Invoke(null, new object[] { customer, false, true }));
        Assert.IsTrue((bool)policy.Invoke(null, new object[] { customer, true, true }),
            "Explicit selection must still reveal a waiting customer's demand.");
    }

    [Test]
    public void DyeFailureVisualUsesMultipleHairColors()
    {
        var root = new GameObject("Rainbow hair customer");
        for (int i = 0; i < 4; i++)
        {
            var hair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hair.name = "Faceted Hair " + i;
            hair.transform.SetParent(root.transform, false);
            hair.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = Color.black
            };
        }
        var view = root.AddComponent<SalonCustomerView>();
        view.InitializeHair(Color.black);
        MethodInfo applyFailure = typeof(SalonCustomerView).GetMethod("ApplyDyeFailureVisual");
        Assert.IsNotNull(applyFailure);

        applyFailure.Invoke(view, new object[] { true });

        var colors = new HashSet<Color>();
        foreach (Transform child in root.transform)
            if (child.name.StartsWith("Faceted Hair"))
                colors.Add(child.GetComponent<Renderer>().sharedMaterial.color);
        Assert.GreaterOrEqual(colors.Count, 3, "Failed dye must visibly turn the hair multicolored.");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void RuntimeCustomerFactoryKeepsDemandUiAvailableForSelection()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/SalonDemo.cs"));

        StringAssert.Contains(
            "AddComponent<CustomerUIRootView>()", source,
            "The customer factory must retain an instance-owned UI root for selected customers.");
        StringAssert.Contains(
            "AddComponent<OrderDemandBubbleView>()", source,
            "Demand data must have a view that can be revealed when its customer is selected.");
        StringAssert.Contains(
            "AddComponent<ActionProgressView>()", source,
            "The selected customer's current requirement must retain progress feedback.");
        StringAssert.Contains(
            "AddComponent<CustomerEmotionView>()", source,
            "The selected customer's state slot must remain available.");
    }

    [Test]
    public void CustomerDemandUiIsHiddenUntilItsCustomerIsSelected()
    {
        var owner = new GameObject("Selectable customer");
        var anchor = new GameObject("Customer UI anchor").transform;
        var ui = owner.AddComponent<CustomerUIRootView>();
        ui.Initialize(new CustomerModel { Id = 31, Needs = new List<ServiceType> { ServiceType.Dye } });
        PropertyInfo visible = typeof(CustomerUIRootView).GetProperty("IsVisible");
        MethodInfo setSelected = typeof(CustomerUIRootView).GetMethod("SetSelected");

        Assert.IsNotNull(visible, "The selection-aware UI root must expose its current visibility for verification.");
        Assert.IsNotNull(setSelected, "The customer UI root needs an explicit selected/unselected state.");

        ui.SetAnchor(anchor);
        Assert.IsFalse((bool)visible.GetValue(ui),
            "Unselected customers must not show always-on overhead demand UI.");

        setSelected.Invoke(ui, new object[] { true });
        Assert.IsTrue((bool)visible.GetValue(ui),
            "Selecting the customer must reveal its demand state above its head.");

        setSelected.Invoke(ui, new object[] { false });
        Assert.IsFalse((bool)visible.GetValue(ui),
            "Changing selection must hide the previous customer's overhead UI.");

        Object.DestroyImmediate(owner);
        Object.DestroyImmediate(anchor.gameObject);
    }

    [Test]
    public void HaircutDemandUsesTheProvidedLowPolyFrameAndToolArtwork()
    {
        var root = new GameObject("Demand visual test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var view = root.AddComponent<HaircutDemandBubbleView>();
        view.Initialize(new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears),
            cameraObject.GetComponent<Camera>());

        Transform frame = FindDescendant(root.transform, "Demand Bubble Frame");
        Assert.IsNotNull(frame, "The restored UI needs the low-poly speech-bubble plate.");
        Assert.IsNotNull(frame.GetComponent<Image>().sprite);

        Transform firstArtwork = FindDescendant(root.transform, "Tool Artwork 1");
        Transform secondArtwork = FindDescendant(root.transform, "Tool Artwork 2");
        Assert.IsNotNull(firstArtwork);
        Assert.IsNotNull(secondArtwork);
        Assert.IsNotNull(firstArtwork.GetComponent<Image>().sprite);
        Assert.IsNotNull(secondArtwork.GetComponent<Image>().sprite);

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void CurrentAndCompletedRequirementsUseReferenceStateArtwork()
    {
        var root = new GameObject("Demand state test");
        var cameraObject = new GameObject("Camera", typeof(Camera));
        var service = new HaircutServiceModel(SalonTool.Scissors, SalonTool.ThinningShears);
        var view = root.AddComponent<HaircutDemandBubbleView>();
        view.Initialize(service, cameraObject.GetComponent<Camera>());

        MethodInfo highlight = typeof(HaircutDemandBubbleView).GetMethod("IsCurrentHighlightVisible");
        MethodInfo completion = typeof(HaircutDemandBubbleView).GetMethod("IsCompletionBadgeVisible");
        Assert.IsNotNull(highlight);
        Assert.IsNotNull(completion);
        Assert.IsTrue((bool)highlight.Invoke(view, new object[] { 0 }));
        Assert.IsFalse((bool)completion.Invoke(view, new object[] { 0 }));

        service.ApplyAttempt(SalonTool.Scissors, HaircutResult.Perfect);
        view.Refresh();

        Assert.IsFalse((bool)highlight.Invoke(view, new object[] { 0 }));
        Assert.IsTrue((bool)completion.Invoke(view, new object[] { 0 }));
        Assert.IsTrue((bool)highlight.Invoke(view, new object[] { 1 }));

        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void RuntimeOrderBubbleUsesOneContinuousWidePlateAndImageIcons()
    {
        var root = new GameObject("Order visual test");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Cut, ServiceType.Dry }
        };
        var view = root.AddComponent<OrderDemandBubbleView>();
        view.Initialize(customer, null);

        Transform frame = FindDescendant(root.transform, "Demand Bubble Frame");
        Assert.IsNotNull(frame);
        Assert.IsNotNull(frame.GetComponent<Image>().sprite);
        Assert.IsNotNull(FindDescendant(root.transform, "Tool Artwork 1"));
        Assert.IsNotNull(FindDescendant(root.transform, "Plus 1"));

        Object.DestroyImmediate(root);
    }

    [Test]
    public void ActionProgressIsRenderedOnTheCurrentRequirementInsteadOfBehindTheCustomer()
    {
        var requirementRoot = new GameObject("Requirement Root");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Cut, ServiceType.Dry }
        };
        var bubble = requirementRoot.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);

        var actionRoot = new GameObject("Action Progress Container");
        actionRoot.transform.SetParent(requirementRoot.transform, false);
        var progress = actionRoot.AddComponent<ActionProgressView>();
        var cameraObject = new GameObject("Camera", typeof(Camera));
        progress.Initialize(cameraObject.GetComponent<Camera>());
        progress.SetProgressForService(
            ServiceType.Cut, "剪发中...", .5f, SalonPalette.Warning);

        Assert.IsNull(FindDescendant(requirementRoot.transform, "动作进度"),
            "The action view must not create a second world-space disc behind the customer's head.");
        Assert.AreEqual(.5f, bubble.GetServiceProgress(0), .001f,
            "The current requirement icon owns the action progress ring.");

        Object.DestroyImmediate(requirementRoot);
        Object.DestroyImmediate(cameraObject);
    }

    [Test]
    public void ActionProgressBindsWhileUnselectedCustomerUiStartsHidden()
    {
        var hiddenUiRoot = new GameObject("Hidden customer UI");
        var requirementRoot = new GameObject("RequirementContainer");
        requirementRoot.transform.SetParent(hiddenUiRoot.transform, false);
        hiddenUiRoot.SetActive(false);
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Dye }
        };
        var bubble = requirementRoot.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        var progress = requirementRoot.AddComponent<ActionProgressView>();

        progress.Initialize(null);
        progress.SetProgressForService(
            ServiceType.Dye, "◆", .6f, SalonPalette.Warning);

        Assert.AreEqual(.6f, bubble.GetServiceProgress(0), .001f,
            "Progress must bind even though unselected customer UI is inactive during construction.");
        Object.DestroyImmediate(hiddenUiRoot);
    }

    [Test]
    public void SelectingDyeBrushKeepsProgressHiddenUntilThePlayerStartsHolding()
    {
        var root = new GameObject("Selected dye progress");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Dye }
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        MethodInfo showReady = typeof(OrderDemandBubbleView).GetMethod("ShowServiceProgressReady");
        MethodInfo isVisible = typeof(OrderDemandBubbleView).GetMethod("IsServiceProgressVisible");
        Assert.IsNotNull(showReady);
        Assert.IsNotNull(isVisible);

        showReady.Invoke(bubble, null);

        Assert.IsFalse((bool)isVisible.Invoke(bubble, new object[] { 0 }),
            "Selecting a tool only arms the interaction; progress belongs to the actual hold action.");
        Assert.AreEqual(0f, bubble.GetServiceProgress(0), .001f,
            "Selecting a tool must not fake action progress.");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void IncompleteOrderNeverShowsMovingAsACleanupRequirement()
    {
        var root = new GameObject("Moving customer demand");
        var customer = new CustomerModel
        {
            State = CustomerState.MovingToStation,
            Station = 1,
            Needs = new List<ServiceType> { ServiceType.Dye },
            OrderRequirementsCompleted = false,
            ExitBlockReason = ExitBlockReason.Moving
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);

        Assert.IsFalse(bubble.CleanupVisible,
            "Still moving is a transient travel state, not a cleanup instruction above an unfinished customer.");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void ArmedBlowAndDyeToolsSurviveUnrelatedCustomerRefreshes()
    {
        MethodInfo armed = typeof(SalonDemo).GetMethod("HasArmedToolSelection",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(armed,
            "The toolbar refresh path must be able to distinguish an armed tool from an idle toolbar.");

        Assert.IsTrue((bool)armed.Invoke(null, new object[]
        {
            4, ActiveServiceAction.ManualBlow, HaircutInteractionState.Idle
        }), "A selected blower must not be reset by a customer state refresh.");
        Assert.IsTrue((bool)armed.Invoke(null, new object[]
        {
            5, ActiveServiceAction.ApplyDye, HaircutInteractionState.Idle
        }), "A selected dye brush must not become unclickable because another state event rebuilt the toolbar.");
        Assert.IsFalse((bool)armed.Invoke(null, new object[]
        {
            -1, ActiveServiceAction.None, HaircutInteractionState.Idle
        }));
    }

    [Test]
    public void RequirementProgressUsesACodeGeneratedContinuousWidthBar()
    {
        var root = new GameObject("Generated progress test");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Cut }
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        bubble.SetServiceProgress(.73f, SalonPalette.Warning);

        Transform track = FindDescendant(root.transform, "Action Progress Track 1");
        Transform fill = FindDescendant(root.transform, "Action Progress Fill 1");
        Assert.IsNotNull(track, "Action progress must be generated as its own horizontal track.");
        Assert.IsNotNull(fill, "Action progress must have a separately generated fill rectangle.");
        Assert.AreEqual(Image.Type.Simple, fill.GetComponent<Image>().type,
            "The generated bar must not use Unity Image.Type.Filled/fillAmount.");
        Assert.AreEqual(.73f, fill.localScale.x, .001f,
            "The fill width must follow continuous logical progress instead of stopping at five ring segments.");
        Assert.IsNull(FindDescendant(root.transform, "Current Step Progress 1"),
            "The old radial UI fill must be removed.");

        bubble.SetServiceProgress(1f, SalonPalette.Success);
        Assert.AreEqual(1f, fill.localScale.x, .001f,
            "A completed hold must visibly reach the end of the generated bar.");

        Object.DestroyImmediate(root);
    }

    [Test]
    public void ArmingAToolClearsAnyStaleActionProgressBeforeTheHoldStarts()
    {
        var root = new GameObject("Tool selection progress reset");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Cut }
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        var progress = root.AddComponent<ActionProgressView>();
        progress.Initialize(null);
        progress.SetProgressForService(
            ServiceType.Cut, "✂", .46f, SalonPalette.Warning);

        MethodInfo armTool = typeof(ActionProgressView).GetMethod("ArmTool");
        Assert.IsNotNull(armTool, "Tool selection needs one explicit reset path for every progress visual.");
        armTool.Invoke(progress, null);

        Assert.IsFalse(bubble.IsServiceProgressVisible(0),
            "Selecting scissors must not show action progress before pointer hold begins.");
        Assert.AreEqual(0f, bubble.GetServiceProgress(0), .001f);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void WrongServiceActionNeverFillsTheCurrentDyeRequirement()
    {
        var root = new GameObject("Wrong scissors must not become dye progress");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Dye }
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        var progress = root.AddComponent<ActionProgressView>();
        progress.Initialize(null);

        progress.SetProgressForService(ServiceType.Cut, "✂", .65f, SalonPalette.Warning);

        Assert.AreEqual(0f, bubble.GetServiceProgress(0), .001f,
            "Scissor hold is a Cut action and must never be drawn as Dye progress.");
        Assert.IsFalse(bubble.IsServiceProgressVisible(0));

        progress.SetProgressForService(ServiceType.Dye, "◆", .65f, SalonPalette.Warning);

        Assert.AreEqual(.65f, bubble.GetServiceProgress(0), .001f,
            "Only a Dye action may fill the active Dye requirement.");
        Assert.IsTrue(bubble.IsServiceProgressVisible(0));
        Object.DestroyImmediate(root);
    }

    [Test]
    public void ActionProgressTargetsTheMatchingRequirementInsteadOfAlwaysUsingTheFirstSlot()
    {
        var root = new GameObject("Progress follows explicit service");
        var customer = new CustomerModel
        {
            Needs = new List<ServiceType> { ServiceType.Dye, ServiceType.Dry },
            Step = 1
        };
        var bubble = root.AddComponent<OrderDemandBubbleView>();
        bubble.Initialize(customer, null);
        var progress = root.AddComponent<ActionProgressView>();
        progress.Initialize(null);

        progress.SetProgressForService(ServiceType.Dry, "≋", .4f, SalonPalette.Warning);

        Assert.AreEqual(0f, bubble.GetServiceProgress(0), .001f);
        Assert.AreEqual(.4f, bubble.GetServiceProgress(1), .001f,
            "Blow progress belongs to the Dry step, never to the completed Dye step.");
        Object.DestroyImmediate(root);
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform nested = FindDescendant(child, name);
            if (nested != null) return nested;
        }
        return null;
    }
}
