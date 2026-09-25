using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Behaviour contract for the mobile controls that will be created by
/// SalonMobileControls.Create. Reflection keeps this test suite compilable in
/// the red phase before the runtime component exists.
/// </summary>
public sealed class SalonMobileControlsTests
{
    private GameObject _root;
    private EventSystem _eventSystem;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "SalonMobileControlsTests Root",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        GameObject eventSystemObject = new GameObject(
            "SalonMobileControlsTests EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        _eventSystem = eventSystemObject.GetComponent<EventSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_eventSystem != null)
            UnityEngine.Object.DestroyImmediate(_eventSystem.gameObject);
        if (_root != null)
            UnityEngine.Object.DestroyImmediate(_root);
    }

    [Test]
    public void PublicContractUsesNamedActionsAndExposesTheRequiredMobileApi()
    {
        Type type = RequireControlsType();
        MethodInfo create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.That(create, Is.Not.Null, "Create(Transform safeParent) is the only construction entry point.");
        Assert.That(create.GetParameters(), Has.Length.EqualTo(1));
        Assert.That(create.GetParameters()[0].ParameterType, Is.EqualTo(typeof(Transform)));

        AssertProperty(type, "Move", typeof(Vector2));
        AssertProperty(type, "InteractionHeld", typeof(bool));
        AssertMethod(type, "ConsumeInteractionPressed", typeof(bool));
        AssertMethod(type, "ResetInput", typeof(void));
        AssertMethod(type, "SetVisible", typeof(void), typeof(bool));
        AssertMethod(type, "SetInteraction", typeof(void), typeof(string), typeof(bool));
        AssertMethod(type, "SetHint", typeof(void), typeof(string));
    }

    [Test]
    public void CreateBuildsReferenceScaledControlsInTheTwoLowerCorners()
    {
        Component controls = CreateControls();
        Transform joystick = FindChild(controls.transform, "MobileJoystick");
        Transform interaction = FindChild(controls.transform, "MobileInteractionButton");
        Assert.That(joystick, Is.Not.Null, "The movement action needs a dedicated left joystick hit area.");
        Assert.That(interaction, Is.Not.Null, "The interaction action needs a dedicated right button hit area.");

        CanvasScaler scaler = controls.GetComponentInParent<CanvasScaler>();
        Assert.That(scaler, Is.Not.Null);
        Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

        RectTransform joystickRect = joystick as RectTransform;
        RectTransform interactionRect = interaction as RectTransform;
        Assert.That(joystickRect, Is.Not.Null);
        Assert.That(interactionRect, Is.Not.Null);
        Assert.That(joystickRect.rect.width, Is.GreaterThanOrEqualTo(120f),
            "The joystick must remain at least 44 physical pixels at the 844x390 target size.");
        Assert.That(joystickRect.rect.height, Is.GreaterThanOrEqualTo(120f));
        Assert.That(interactionRect.rect.width, Is.GreaterThanOrEqualTo(120f),
            "The interaction button must remain at least 44 physical pixels at the 844x390 target size.");
        Assert.That(interactionRect.rect.height, Is.GreaterThanOrEqualTo(120f));

        Assert.That(joystickRect.anchorMin.x, Is.LessThan(.2f));
        Assert.That(joystickRect.anchorMax.x, Is.LessThan(.35f));
        Assert.That(joystickRect.anchorMin.y, Is.LessThan(.25f));
        Assert.That(joystickRect.anchorMax.y, Is.LessThan(.5f));
        Assert.That(interactionRect.anchorMin.x, Is.GreaterThan(.65f));
        Assert.That(interactionRect.anchorMax.x, Is.GreaterThan(.8f));
        Assert.That(interactionRect.anchorMin.y, Is.LessThan(.25f));
        Assert.That(interactionRect.anchorMax.y, Is.LessThan(.5f));
    }

    [Test]
    public void JoystickUsesARadialDeadzoneAndClampsDiagonalMagnitude()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        Assert.That(joystick, Is.Not.Null, "The joystick hit area must receive pointer events.");

        Vector2 center = new Vector2(170f, 150f);
        SendPointer(joystick, "OnPointerDown", 31, center);
        SendPointer(joystick, "OnDrag", 31, center + new Vector2(3f, 4f));
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero),
            "Small radial drift inside the deadzone must not move the stylist.");

        SendPointer(joystick, "OnDrag", 31, center + new Vector2(1000f, 1000f));
        Vector2 diagonal = ReadProperty<Vector2>(controls, "Move");
        Assert.That(diagonal.magnitude, Is.LessThanOrEqualTo(1.001f));
        Assert.That(diagonal.x, Is.GreaterThan(.6f));
        Assert.That(diagonal.y, Is.GreaterThan(.6f));

        SendPointer(joystick, "OnPointerUp", 31, center);
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void JoystickAndInteractionRemainIndependentAcrossPointerIds()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        Component interaction = FindEventComponent(FindChild(controls.transform, "MobileInteractionButton"),
            typeof(IPointerDownHandler));
        Assert.That(joystick, Is.Not.Null);
        Assert.That(interaction, Is.Not.Null);

        Vector2 joystickCenter = new Vector2(170f, 150f);
        SendPointer(joystick, "OnPointerDown", 41, joystickCenter);
        SendPointer(joystick, "OnDrag", 41, joystickCenter + Vector2.right * 240f);
        Vector2 movementWhileHolding = ReadProperty<Vector2>(controls, "Move");
        SendPointer(interaction, "OnPointerDown", 42, new Vector2(670f, 150f));

        Assert.That(ReadProperty<bool>(controls, "InteractionHeld"), Is.True);
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(movementWhileHolding));

        SendPointer(interaction, "OnPointerUp", 42, new Vector2(670f, 150f));
        Assert.That(ReadProperty<bool>(controls, "InteractionHeld"), Is.False);
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(movementWhileHolding));

        SendPointer(joystick, "OnPointerUp", 41, joystickCenter);
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void InteractionPressIsAnEdgeThatIsConsumedOnce()
    {
        Component controls = CreateControls();
        Component interaction = FindEventComponent(FindChild(controls.transform, "MobileInteractionButton"),
            typeof(IPointerDownHandler));
        Assert.That(interaction, Is.Not.Null);

        SendPointer(interaction, "OnPointerDown", 51, new Vector2(670f, 150f));
        Assert.That(Invoke<bool>(controls, "ConsumeInteractionPressed"), Is.True);
        Assert.That(Invoke<bool>(controls, "ConsumeInteractionPressed"), Is.False,
            "A held button must not trigger a second discrete action until pressed again.");
        SendPointer(interaction, "OnPointerUp", 51, new Vector2(670f, 150f));

        SendPointer(interaction, "OnPointerDown", 52, new Vector2(670f, 150f));
        Assert.That(Invoke<bool>(controls, "ConsumeInteractionPressed"), Is.True);
    }

    [Test]
    public void ResetInputClearsMovementHoldAndPendingPress()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        Component interaction = FindEventComponent(FindChild(controls.transform, "MobileInteractionButton"),
            typeof(IPointerDownHandler));
        Assert.That(joystick, Is.Not.Null);
        Assert.That(interaction, Is.Not.Null);

        Vector2 joystickCenter = new Vector2(170f, 150f);
        SendPointer(joystick, "OnPointerDown", 61, joystickCenter);
        SendPointer(joystick, "OnDrag", 61, joystickCenter + Vector2.up * 240f);
        SendPointer(interaction, "OnPointerDown", 62, new Vector2(670f, 150f));
        InvokeVoid(controls, "ResetInput");

        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
        Assert.That(ReadProperty<bool>(controls, "InteractionHeld"), Is.False);
        Assert.That(Invoke<bool>(controls, "ConsumeInteractionPressed"), Is.False);
    }

    [Test]
    public void DisablingControlsClearsActiveInputAndReenablingStartsNeutral()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        Assert.That(joystick, Is.Not.Null);

        Vector2 center = new Vector2(170f, 150f);
        SendPointer(joystick, "OnPointerDown", 71, center);
        SendPointer(joystick, "OnDrag", 71, center + Vector2.right * 240f);
        Behaviour behaviour = controls as Behaviour;
        Assert.That(behaviour, Is.Not.Null, "SalonMobileControls must be a Behaviour so disabling it clears active touch state.");
        InvokeVoid(controls, "OnDisable");
        behaviour.enabled = false;
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
        Assert.That(ReadProperty<bool>(controls, "InteractionHeld"), Is.False);
        behaviour.enabled = true;
        InvokeVoid(controls, "OnEnable");
        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void JoystickKnobFollowsFilteredMoveAndReturnsToCenterOnRelease()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        RectTransform knob = FindChild(controls.transform, "JoystickKnob") as RectTransform;
        Assert.That(joystick, Is.Not.Null);
        Assert.That(knob, Is.Not.Null);

        Vector2 center = new Vector2(170f, 150f);
        Assert.That(knob.anchoredPosition, Is.EqualTo(Vector2.zero));
        SendPointer(joystick, "OnPointerDown", 75, center);
        SendPointer(joystick, "OnDrag", 75, center + Vector2.right * 240f);
        Assert.That(knob.anchoredPosition.x, Is.GreaterThan(0f),
            "The visual knob must follow the normalized movement action.");
        Assert.That(knob.anchoredPosition.y, Is.EqualTo(0f).Within(.001f));

        SendPointer(joystick, "OnPointerUp", 75, center);
        Assert.That(knob.anchoredPosition, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void PauseClearsActiveInputEvenWhenTheCanvasRemainsVisible()
    {
        Component controls = CreateControls();
        Component joystick = FindEventComponent(FindChild(controls.transform, "MobileJoystick"),
            typeof(IPointerDownHandler));
        Assert.That(joystick, Is.Not.Null);

        Vector2 center = new Vector2(170f, 150f);
        SendPointer(joystick, "OnPointerDown", 81, center);
        SendPointer(joystick, "OnDrag", 81, center + Vector2.right * 240f);
        MethodInfo pause = controls.GetType().GetMethod("OnApplicationPause",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(pause, Is.Not.Null, "Pause handling must be explicit so a suspended touch cannot stick.");
        pause.Invoke(controls, new object[] { true });

        Assert.That(ReadProperty<Vector2>(controls, "Move"), Is.EqualTo(Vector2.zero));
        Assert.That(ReadProperty<bool>(controls, "InteractionHeld"), Is.False);
    }

    [Test]
    public void InteractionAvailabilityAndHintAreReflectedInTheUi()
    {
        Component controls = CreateControls();
        Transform interaction = FindChild(controls.transform, "MobileInteractionButton");
        Transform hint = FindChild(controls.transform, "MobileHint");
        Assert.That(interaction, Is.Not.Null);
        Assert.That(hint, Is.Not.Null);

        Button button = interaction.GetComponent<Button>();
        Assert.That(button, Is.Not.Null, "The operation control must expose an explicit availability state.");
        InvokeVoid(controls, "SetInteraction", "开始操作", false);
        Assert.That(button.interactable, Is.False);
        Assert.That(FindText(interaction), Is.EqualTo("开始操作"));

        InvokeVoid(controls, "SetInteraction", "操作", true);
        Assert.That(button.interactable, Is.True);
        Assert.That(FindText(interaction), Is.EqualTo("操作"));

        InvokeVoid(controls, "SetHint", "靠近工位后点击");
        Assert.That(FindText(hint), Is.EqualTo("靠近工位后点击"));
    }

    [Test]
    public void VisibilityCanBeToggledWithoutDestroyingTheActionSurface()
    {
        Component controls = CreateControls();
        GameObject controlsObject = controls.gameObject;
        Transform joystick = FindChild(controls.transform, "MobileJoystick");
        Transform interaction = FindChild(controls.transform, "MobileInteractionButton");
        InvokeVoid(controls, "SetVisible", false);
        Assert.That(controlsObject.activeSelf, Is.False,
            "Hidden mobile controls must stop intercepting taps while a menu is open.");
        InvokeVoid(controls, "SetVisible", true);
        Assert.That(controlsObject.activeSelf, Is.True);
        Assert.That(FindChild(controls.transform, "MobileJoystick"), Is.EqualTo(joystick));
        Assert.That(FindChild(controls.transform, "MobileInteractionButton"), Is.EqualTo(interaction));
    }

    private Component CreateControls()
    {
        Type type = RequireControlsType();
        MethodInfo create = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.That(create, Is.Not.Null);
        object result = create.Invoke(null, new object[] { _root.transform });
        Component component = result as Component;
        Assert.That(component, Is.Not.Null, "Create must return the runtime SalonMobileControls component.");
        return component;
    }

    private static Type RequireControlsType()
    {
        Type type = Type.GetType("SalonMobileControls, HairSalon.Runtime");
        if (type == null)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("SalonMobileControls");
                if (type != null)
                    break;
            }
        }

        Assert.That(type, Is.Not.Null,
            "SalonMobileControls is missing. This is the intentional red phase for the mobile input contract.");
        return type;
    }

    private static void AssertProperty(Type type, string name, Type valueType)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"Missing public property {name}.");
        Assert.That(property.PropertyType, Is.EqualTo(valueType));
        Assert.That(property.CanRead, Is.True);
    }

    private static void AssertMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null, $"Missing public method {name}.");
        Assert.That(method.ReturnType, Is.EqualTo(returnType));
        ParameterInfo[] parameters = method.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(parameterTypes.Length));
        for (int i = 0; i < parameterTypes.Length; i++)
            Assert.That(parameters[i].ParameterType, Is.EqualTo(parameterTypes[i]));
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Component FindEventComponent(Transform root, Type eventInterface)
    {
        if (root == null)
            return null;
        foreach (Component component in root.GetComponents<Component>())
        {
            if (component != null && eventInterface.IsAssignableFrom(component.GetType()))
                return component;
        }
        return null;
    }

    private void SendPointer(Component target, string methodName, int pointerId, Vector2 position)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing pointer callback {methodName} on {target.GetType().Name}.");
        PointerEventData data = new PointerEventData(_eventSystem)
        {
            pointerId = pointerId,
            position = position,
            pressPosition = position,
            button = PointerEventData.InputButton.Left
        };
        method.Invoke(target, new object[] { data });
    }

    private static T ReadProperty<T>(Component target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target, null);
    }

    private static T Invoke<T>(Component target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (T)method.Invoke(target, null);
    }

    private static void InvokeVoid(Component target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        method.Invoke(target, arguments);
    }

    private static string FindText(Transform root)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component is Text legacyText)
                return legacyText.text;
            if (component is TMPro.TMP_Text tmpText)
                return tmpText.text;
        }
        return null;
    }
}
