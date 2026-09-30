using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Layout contract for the second-pass local co-op touch controls.
///
/// The controls use the 1920x1080 UI reference space. At the narrow
/// 640x360 acceptance viewport one reference unit is one third of a physical
/// pixel, so every action surface below must retain at least 132 reference
/// units in each dimension to preserve the 44px touch target.
/// </summary>
public sealed class SecondPassControlsTests
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float PlayerSplit = ReferenceWidth * .5f;
    private const float NarrowViewportScale = 640f / ReferenceWidth;
    private const float MinimumTouchPixels = 44f;
    private const float MinimumTouchReferenceUnits = MinimumTouchPixels / NarrowViewportScale;

    private GameObject _root;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject(
            "Second Pass Controls Root",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        scaler.enabled = false;

        // Give the fixture a deterministic reference-space canvas.  The
        // runtime CanvasScaler may later scale this rect to a real viewport,
        // but layout assertions should include every parent anchor first.
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(.5f, .5f);
        rootRect.pivot = new Vector2(.5f, .5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    [Test]
    public void SplitControlsStayInsideTheirOwnHalfAndDoNotOverlap()
    {
        SalonMobileControls playerOne = SalonMobileControls.Create(_root.transform);
        playerOne.ConfigureForPlayer(1, true);
        SalonMobileControls playerTwo = SalonMobileControls.Create(_root.transform);
        playerTwo.ConfigureForPlayer(2, true);

        Rect p1Joystick = ReferenceRect(FindRect(playerOne, "MobileJoystick"));
        Rect p1Action = ReferenceRect(FindRect(playerOne, "MobileInteractionButton"));
        Rect p1Hint = ReferenceRect(FindRect(playerOne, "MobileHint"));
        Rect p2Joystick = ReferenceRect(FindRect(playerTwo, "MobileJoystick"));
        Rect p2Action = ReferenceRect(FindRect(playerTwo, "MobileInteractionButton"));
        Rect p2Hint = ReferenceRect(FindRect(playerTwo, "MobileHint"));

        AssertInsideHalf(p1Joystick, 0f, "P1 joystick");
        AssertInsideHalf(p1Action, 0f, "P1 action");
        AssertInsideHalf(p1Hint, 0f, "P1 hint");
        AssertInsideHalf(p2Joystick, PlayerSplit, "P2 joystick");
        AssertInsideHalf(p2Action, PlayerSplit, "P2 action");
        AssertInsideHalf(p2Hint, PlayerSplit, "P2 hint");
        Assert.That(p1Action.yMax, Is.LessThanOrEqualTo(240.01f),
            "P1 action must leave the lower waiting area visible at 640x360.");
        Assert.That(p2Action.yMax, Is.LessThanOrEqualTo(240.01f),
            "P2 action must leave the lower waiting area visible at 640x360.");

        Assert.That(p1Joystick.Overlaps(p1Action), Is.False,
            "P1 movement and action hit surfaces must leave a reachable gap.");
        Assert.That(p2Joystick.Overlaps(p2Action), Is.False,
            "P2 movement and action hit surfaces must leave a reachable gap.");
        Assert.That(p1Joystick.Overlaps(p1Hint), Is.False,
            "P1 hint must not cover the movement surface.");
        Assert.That(p1Action.Overlaps(p1Hint), Is.False,
            "P1 hint must not cover the action surface.");
        Assert.That(p2Joystick.Overlaps(p2Hint), Is.False,
            "P2 hint must not cover the movement surface.");
        Assert.That(p2Action.Overlaps(p2Hint), Is.False,
            "P2 hint must not cover the action surface.");
        Assert.That(p1Joystick.Overlaps(p2Joystick), Is.False,
            "The two players must not receive the same movement touch area.");
        Assert.That(p1Action.Overlaps(p2Action), Is.False,
            "The two players must not receive the same action touch area.");
    }

    [Test]
    public void SplitControlsKeepNarrowViewportTouchTargetsAndReadableLabels()
    {
        SalonMobileControls playerOne = SalonMobileControls.Create(_root.transform);
        playerOne.ConfigureForPlayer(1, true);
        SalonMobileControls playerTwo = SalonMobileControls.Create(_root.transform);
        playerTwo.ConfigureForPlayer(2, true);

        AssertTouchTarget(playerOne, "MobileJoystick", "P1 joystick");
        AssertTouchTarget(playerOne, "MobileInteractionButton", "P1 action");
        AssertTouchTarget(playerTwo, "MobileJoystick", "P2 joystick");
        AssertTouchTarget(playerTwo, "MobileInteractionButton", "P2 action");

        Text p1ActionLabel = FindText(playerOne, "InteractionLabel");
        Text p2ActionLabel = FindText(playerTwo, "InteractionLabel");
        Assert.That(p1ActionLabel, Is.Not.Null);
        Assert.That(p2ActionLabel, Is.Not.Null);
        Assert.That(p1ActionLabel.fontSize * NarrowViewportScale, Is.GreaterThanOrEqualTo(14f),
            "P1 action text must remain legible at the narrow target viewport.");
        Assert.That(p2ActionLabel.fontSize * NarrowViewportScale, Is.GreaterThanOrEqualTo(14f),
            "P2 action text must remain legible at the narrow target viewport.");

        Text p1HintLabel = FindText(playerOne, "MobileHintLabel");
        Text p2HintLabel = FindText(playerTwo, "MobileHintLabel");
        Assert.That(p1HintLabel, Is.Not.Null);
        Assert.That(p2HintLabel, Is.Not.Null);
        Assert.That(p1HintLabel.fontSize * NarrowViewportScale, Is.GreaterThanOrEqualTo(9f),
            "P1 contextual hint must remain readable when it is shown.");
        Assert.That(p2HintLabel.fontSize * NarrowViewportScale, Is.GreaterThanOrEqualTo(9f),
            "P2 contextual hint must remain readable when it is shown.");
    }

    private static void AssertTouchTarget(SalonMobileControls controls, string name, string label)
    {
        RectTransform rect = FindRect(controls, name);
        Assert.That(rect, Is.Not.Null, label + " must exist.");
        Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(MinimumTouchReferenceUnits),
            label + " must remain at least 44 physical pixels wide at 640x360.");
        Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(MinimumTouchReferenceUnits),
            label + " must remain at least 44 physical pixels high at 640x360.");
    }

    private static void AssertInsideHalf(Rect rect, float halfStart, string label)
    {
        const float tolerance = .01f;
        Assert.That(rect.xMin + tolerance, Is.GreaterThanOrEqualTo(halfStart),
            label + " must not cross the left edge of its player's half.");
        Assert.That(rect.xMax - tolerance, Is.LessThanOrEqualTo(halfStart + PlayerSplit),
            label + " must not cross the right edge of its player's half.");
        Assert.That(rect.yMin + tolerance, Is.GreaterThanOrEqualTo(0f),
            label + " must stay above the bottom safe edge.");
        Assert.That(rect.yMax - tolerance, Is.LessThanOrEqualTo(ReferenceHeight),
            label + " must stay below the top safe edge.");
    }

    private static RectTransform FindRect(SalonMobileControls controls, string name)
    {
        Transform found = FindChild(controls == null ? null : controls.transform, name);
        return found as RectTransform;
    }

    private static Text FindText(SalonMobileControls controls, string name)
    {
        RectTransform rect = FindRect(controls, name);
        return rect == null ? null : rect.GetComponent<Text>();
    }

    private Rect ReferenceRect(RectTransform rect)
    {
        Assert.That(rect, Is.Not.Null);
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 lowerLeft = _root.transform.InverseTransformPoint(corners[0]);
        Vector3 upperRight = _root.transform.InverseTransformPoint(corners[2]);
        Vector3 origin = new Vector3(ReferenceWidth * .5f, ReferenceHeight * .5f, 0f);
        lowerLeft += origin;
        upperRight += origin;
        return Rect.MinMaxRect(lowerLeft.x, lowerLeft.y, upperRight.x, upperRight.y);
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
}
