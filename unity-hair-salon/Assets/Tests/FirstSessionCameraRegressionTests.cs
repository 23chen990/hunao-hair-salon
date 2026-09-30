using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Projection regressions for the first-session mobile camera.
///
/// These tests invoke SalonDemo's real follow methods and judge the resulting
/// Camera projection. They intentionally do not duplicate the framing math.
/// The viewport is set to the two landscape evidence sizes before the camera
/// target is applied, so the assertion is about what the player can see.
/// </summary>
public sealed class FirstSessionCameraRegressionTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    // Captured from the 9/28 mobile evidence floor. The margin keeps fixtures
    // inside the legal movement area rather than testing wall contact.
    private static readonly Rect EvidenceFloor = new Rect(-10.2f, -5f, 14.8f, 12.15f);
    private const float FloorCornerMargin = .45f;
    private GameObject _root;
    private SalonDemo _demo;
    private Camera _camera;
    private Transform _player;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("First session camera regression");
        _demo = _root.AddComponent<SalonDemo>();

        GameObject cameraObject = new GameObject("Regression camera", typeof(Camera));
        cameraObject.transform.SetParent(_root.transform, false);
        _camera = cameraObject.GetComponent<Camera>();
        _camera.orthographic = true;
        _camera.nearClipPlane = .1f;
        _camera.farClipPlane = 100f;
        _camera.transform.position = new Vector3(-12f, 20f, -23f);
        _camera.transform.LookAt(new Vector3(0f, .65f, 1f));

        _player = NewTransform("P1", new Vector3(-3.6853f, .05f, 3.5001f));
        Set("_camera", _camera);
        Set("_player", _player);
        Set("_mobileMode", true);
        Set("_simple2DMode", false);
        Set("_overviewCameraPosition", _camera.transform.position);
        Set("_overviewCameraRotation", _camera.transform.rotation);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_root);
    }

    [TestCase(844, 390)]
    [TestCase(960, 540)]
    public void MobileFollowKeepsPlayerAndWashStationInTheVisibleViewport(int width, int height)
    {
        ConfigureViewport(width, height);

        Invoke("UpdateMobileCameraFollow");
        ApplyComputedCameraTarget();

        AssertVisible("single-player", _player.position);
        AssertVisible("wash station", SalonDemo.SecondaryWashBedPosition);
    }

    [TestCase(844, 390)]
    [TestCase(960, 540)]
    public void CoopFollowKeepsBothPlayersAndWashStationInTheVisibleViewport(int width, int height)
    {
        ConfigureViewport(width, height);
        Transform playerTwoTransform = NewTransform("P2", new Vector3(3f, .05f, 2.5f));
        Set("_coopMode", true);
        Set("_coopPlayerTwo", new SalonCoopPlayerState(2) { Transform = playerTwoTransform });

        Invoke("UpdateCoopCameraFollow");
        ApplyComputedCameraTarget();

        AssertVisible("co-op player one", _player.position);
        AssertVisible("co-op player two", playerTwoTransform.position);
        AssertVisible("co-op wash station", SalonDemo.SecondaryWashBedPosition);
    }

    [TestCase(844, 390)]
    [TestCase(960, 540)]
    public void MobileFollowKeepsFeetAndHeadVisibleAtEveryLegalFloorCorner(int width, int height)
    {
        ConfigureViewport(width, height);
        Set("_mobileFloor", EvidenceFloor);

        float left = EvidenceFloor.xMin + FloorCornerMargin;
        float right = EvidenceFloor.xMax - FloorCornerMargin;
        float bottom = EvidenceFloor.yMin + FloorCornerMargin;
        float top = EvidenceFloor.yMax - FloorCornerMargin;
        Vector3[] corners =
        {
            new Vector3(left, .05f, bottom),
            new Vector3(right, .05f, bottom),
            new Vector3(left, .05f, top),
            new Vector3(right, .05f, top)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            _player.position = corners[i];
            Invoke("UpdateMobileCameraFollow");
            ApplyComputedCameraTarget();
            AssertCharacterBodyVisible("single corner " + i, _player.position);
        }
    }

    [TestCase(844, 390)]
    [TestCase(960, 540)]
    public void CoopFollowKeepsFeetAndHeadsVisibleWhenPlayersUseOppositeFloorCorners(int width, int height)
    {
        ConfigureViewport(width, height);
        Set("_mobileFloor", EvidenceFloor);
        float left = EvidenceFloor.xMin + FloorCornerMargin;
        float right = EvidenceFloor.xMax - FloorCornerMargin;
        float bottom = EvidenceFloor.yMin + FloorCornerMargin;
        float top = EvidenceFloor.yMax - FloorCornerMargin;
        _player.position = new Vector3(left, .05f, bottom);
        Transform playerTwoTransform = NewTransform("P2", new Vector3(right, .05f, top));
        Set("_coopMode", true);
        Set("_coopPlayerTwo", new SalonCoopPlayerState(2) { Transform = playerTwoTransform });

        Invoke("UpdateCoopCameraFollow");
        ApplyComputedCameraTarget();
        AssertCharacterBodyVisible("co-op player one", _player.position);
        AssertCharacterBodyVisible("co-op player two", playerTwoTransform.position);
    }

    [TestCase(844, 390, false)]
    [TestCase(960, 540, false)]
    [TestCase(640, 360, false)]
    [TestCase(844, 390, true)]
    [TestCase(960, 540, true)]
    [TestCase(640, 360, true)]
    public void NearbyStylistsStayClearOfTopTasksAndBottomControlsAtRoomEdges(
        int width, int height, bool coop)
    {
        ConfigureViewport(width, height);
        Transform second = NewTransform("Nearby P2", Vector3.zero);
        Set("_coopMode", coop);
        if (coop) Set("_coopPlayerTwo", new SalonCoopPlayerState(2) { Transform = second });
        foreach (float x in new[] { EvidenceFloor.xMin + .6f, EvidenceFloor.xMax - .6f })
        foreach (float z in new[] { EvidenceFloor.yMin + .6f, EvidenceFloor.yMax - .6f })
        {
            _player.position = new Vector3(x, .05f, z);
            second.position = _player.position + Vector3.left * .5f;
            Invoke(coop ? "UpdateCoopCameraFollow" : "UpdateMobileCameraFollow");
            ApplyComputedCameraTarget();
            AssertInsideReadableArea(_player.position);
            if (coop) AssertInsideReadableArea(second.position);
        }
    }

    private void AssertInsideReadableArea(Vector3 feet)
    {
        Vector3 foot = _camera.WorldToViewportPoint(feet);
        Vector3 head = _camera.WorldToViewportPoint(feet + Vector3.up * 2f);
        Assert.That(head.y, Is.LessThanOrEqualTo(.701f),
            "The stylist's head must clear the top task strip, not merely remain onscreen.");
        Assert.That(foot.y, Is.GreaterThanOrEqualTo(.269f),
            "The stylist's feet must clear the bottom operation and hint area.");
        Assert.That(foot.x, Is.InRange(.219f, .801f),
            "The stylist must remain between the outer joystick/action overlays.");
    }

    [TestCase(844, 390)]
    [TestCase(960, 540)]
    [TestCase(640, 360)]
    public void ActualBillboardHairdresserClearsTaskStripAtBackWall(int width, int height)
    {
        ConfigureViewport(width, height);
        GameObject prefab = Resources.Load<GameObject>("Characters/Hairdresser");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab, _root.transform);
        _player = instance.transform;
        _player.position = new Vector3(4f, .05f, 6.5f);
        Set("_player", _player);
        var presenter = instance.GetComponentInChildren<HairSalon.Character.Hairdresser2DPresenter>();
        SpriteRenderer visual = presenter.GetComponent<SpriteRenderer>();
        Assert.That(visual.sprite, Is.Not.Null);
        // The production presenter faces the camera in LateUpdate. World-up
        // height alone underestimates this billboard's visible hair/head.
        visual.transform.rotation = _camera.transform.rotation;
        Invoke("UpdateMobileCameraFollow");
        ApplyComputedCameraTarget();
        Bounds bounds = visual.localBounds;
        foreach (float x in new[] { bounds.min.x, bounds.max.x })
        foreach (float y in new[] { bounds.min.y, bounds.max.y })
        {
            Vector3 screen = _camera.WorldToViewportPoint(
                visual.transform.TransformPoint(new Vector3(x, y, bounds.center.z)));
            Assert.That(screen.y, Is.LessThanOrEqualTo(.701f),
                "The rendered hairstyle, including the billboard tilt, must clear the task strip.");
            Assert.That(screen.y, Is.GreaterThanOrEqualTo(.269f));
        }
    }

    private Transform NewTransform(string name, Vector3 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(_root.transform, false);
        gameObject.transform.position = position;
        return gameObject.transform;
    }

    private void ConfigureViewport(int width, int height)
    {
        _camera.pixelRect = new Rect(0f, 0f, width, height);
        // EditMode batch runs can clamp pixelRect to the host Game view. Set
        // the projection aspect explicitly so each test exercises its named
        // landscape ratio rather than the runner window's ratio.
        _camera.aspect = (float)width / height;
        _camera.orthographicSize = 8.4f;
        Assert.That(_camera.aspect, Is.EqualTo((float)width / height).Within(.001f),
            "Regression camera must project using its actual landscape aspect.");
    }

    private void ApplyComputedCameraTarget()
    {
        Vector3 target = (Vector3)Read("_cameraPositionTarget");
        float size = (float)Read("_cameraSizeTarget");
        _camera.transform.position = target;
        _camera.transform.rotation = (Quaternion)Read("_overviewCameraRotation");
        _camera.orthographicSize = size;
    }

    private void AssertVisible(string label, Vector3 worldPoint)
    {
        Vector3 viewport = _camera.WorldToViewportPoint(worldPoint);
        Assert.That(viewport.z, Is.GreaterThan(0f),
            label + " is behind the overview camera: " + viewport);
        Assert.That(viewport.x, Is.InRange(.02f, .98f),
            label + " is outside horizontal camera bounds: " + viewport);
        Assert.That(viewport.y, Is.InRange(.02f, .98f),
            label + " is outside vertical camera bounds: " + viewport);
    }

    private void AssertCharacterBodyVisible(string label, Vector3 feet)
    {
        AssertVisible(label + " feet", feet);
        AssertVisible(label + " head", feet + Vector3.up * 2f);
    }

    private void Invoke(string methodName)
    {
        MethodInfo method = typeof(SalonDemo).GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, "Missing camera method: " + methodName);
        method.Invoke(_demo, null);
    }

    private object Read(string fieldName)
    {
        FieldInfo field = typeof(SalonDemo).GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, "Missing camera field: " + fieldName);
        return field.GetValue(_demo);
    }

    private void Set(string fieldName, object value)
    {
        FieldInfo field = typeof(SalonDemo).GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, "Missing camera field: " + fieldName);
        field.SetValue(_demo, value);
    }
}
