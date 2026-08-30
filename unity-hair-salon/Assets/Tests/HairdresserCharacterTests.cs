using HairSalon.Character;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class HairdresserCharacterTests
{
    [TestCase(0f, 1f, HairdresserDirection.North)]
    [TestCase(1f, 1f, HairdresserDirection.NorthEast)]
    [TestCase(1f, 0f, HairdresserDirection.East)]
    [TestCase(1f, -1f, HairdresserDirection.SouthEast)]
    [TestCase(0f, -1f, HairdresserDirection.South)]
    [TestCase(-1f, -1f, HairdresserDirection.SouthWest)]
    [TestCase(-1f, 0f, HairdresserDirection.West)]
    [TestCase(-1f, 1f, HairdresserDirection.NorthWest)]
    public void ResolverQuantizesEightDirections(float x, float y, HairdresserDirection expected)
    {
        Assert.AreEqual(expected, HairdresserDirectionResolver.FromVector(new Vector2(x, y)));
    }

    [Test]
    public void MovementDrivesIdleAndWalkWhilePreservingLastFacing()
    {
        GameObject root = new GameObject("Hairdresser Test", typeof(Animator));
        try
        {
            HairdresserCharacter character = root.AddComponent<HairdresserCharacter>();
            character.Move(new Vector3(1f, 0f, 1f), .1f);
            Assert.AreEqual(HairdresserAnimationState.Walk, character.CurrentState);
            Assert.AreEqual(HairdresserDirection.NorthEast, character.FacingDirection);

            character.Move(Vector3.zero, .1f);
            Assert.AreEqual(HairdresserAnimationState.Idle, character.CurrentState);
            Assert.AreEqual(HairdresserDirection.NorthEast, character.FacingDirection);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(HairdresserAnimationState.CutHair)]
    [TestCase(HairdresserAnimationState.DryHair)]
    [TestCase(HairdresserAnimationState.WashHair)]
    public void ServiceStatesAreReserved(HairdresserAnimationState state)
    {
        GameObject root = new GameObject("Hairdresser Test", typeof(Animator));
        try
        {
            HairdresserCharacter character = root.AddComponent<HairdresserCharacter>();
            character.BeginService(state);
            Assert.AreEqual(state, character.CurrentState);
            character.EndService();
            Assert.AreEqual(HairdresserAnimationState.Idle, character.CurrentState);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void PrefabExposesStableVisualAndToolReplacementContracts()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/Hairdresser.prefab");

        Assert.IsNotNull(prefab);
        HairdresserCharacter character = prefab.GetComponent<HairdresserCharacter>();
        Assert.IsNotNull(character);
        Assert.IsInstanceOf<IHairdresserVisualReplacer>(character);
        Assert.IsNotNull(prefab.GetComponent<Animator>());
        Assert.IsNotNull(prefab.GetComponent<Animator>().runtimeAnimatorController);
        Transform visual = prefab.transform.Find("Visual/HairdresserSpriteVisual");
        Assert.IsNotNull(visual);
        Assert.IsNotNull(visual.GetComponent<SpriteRenderer>());
        Assert.IsNotNull(visual.GetComponent<Hairdresser2DPresenter>());
        Assert.IsNotNull(prefab.transform.Find("Sockets/RightHandToolSocket"));
    }

    [Test]
    public void ApprovedAtlasBindsEveryStateAndDirection()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/Hairdresser.prefab");
        Transform visual = prefab == null
            ? null
            : prefab.transform.Find("Visual/HairdresserSpriteVisual");
        Assert.IsNotNull(visual);

        Hairdresser2DPresenter presenter = visual.GetComponent<Hairdresser2DPresenter>();
        SerializedObject serialized = new SerializedObject(presenter);
        string[] sets = { "idle", "walk", "cutHair", "dryHair", "washHair" };
        foreach (string setName in sets)
        {
            SerializedProperty directions = serialized.FindProperty(setName)
                .FindPropertyRelative("directions");
            Assert.AreEqual(8, directions.arraySize, setName);
            for (int direction = 0; direction < 8; direction++)
                Assert.IsNotNull(directions.GetArrayElementAtIndex(direction).objectReferenceValue,
                    setName + " direction " + direction);
        }
    }

    [Test]
    public void WalkCycleUsesContactPassingOppositePassingRhythm()
    {
        MethodInfo resolver = typeof(Hairdresser2DPresenter).GetMethod(
            "ResolveWalkFrame", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(resolver, "Walk frame resolver is missing");
        Assert.AreEqual(0, resolver.Invoke(null, new object[] { 0f }));
        Assert.AreEqual(1, resolver.Invoke(null, new object[] { .25f }));
        Assert.AreEqual(2, resolver.Invoke(null, new object[] { .5f }));
        Assert.AreEqual(1, resolver.Invoke(null, new object[] { .75f }));
    }

    [Test]
    public void ProceduralGaitAlternatesWeightAndReturnsToRest()
    {
        MethodInfo evaluator = typeof(Hairdresser2DPresenter).GetMethod(
            "EvaluateGaitOffset", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(evaluator, "Procedural gait evaluator is missing");
        Vector3 rest = (Vector3)evaluator.Invoke(null, new object[] { .25f, 0f });
        Vector3 left = (Vector3)evaluator.Invoke(null, new object[] { .25f, 1f });
        Vector3 right = (Vector3)evaluator.Invoke(null, new object[] { .75f, 1f });
        Assert.AreEqual(Vector3.zero, rest);
        Assert.Greater(left.x, 0f);
        Assert.Less(right.x, 0f);
        Assert.Greater(left.y, 0f);
        Assert.Greater(right.y, 0f);
    }

    [Test]
    public void WalkTransitionAndOppositeFramesAreBoundForEightDirections()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/Hairdresser.prefab");
        Hairdresser2DPresenter presenter = prefab.transform
            .Find("Visual/HairdresserSpriteVisual").GetComponent<Hairdresser2DPresenter>();
        SerializedObject serialized = new SerializedObject(presenter);
        foreach (string setName in new[] { "walkPassing", "walkOpposite" })
        {
            SerializedProperty set = serialized.FindProperty(setName);
            Assert.IsNotNull(set, setName + " is missing");
            SerializedProperty directions = set.FindPropertyRelative("directions");
            Assert.AreEqual(8, directions.arraySize);
            for (int direction = 0; direction < 8; direction++)
                Assert.IsNotNull(directions.GetArrayElementAtIndex(direction).objectReferenceValue,
                    setName + " direction " + direction);
        }
    }

    [Test]
    public void DirectionIsResolvedRelativeToTheViewPlane()
    {
        System.Type rigType = System.Type.GetType(
            "HairSalon.Character.Hairdresser2_5DRig, HairSalon.Runtime");
        Assert.IsNotNull(rigType, "2.5D rig is missing");
        MethodInfo resolver = rigType.GetMethod(
            "ResolveCameraRelativeDirection", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(resolver);
        Vector3 cameraRight = Vector3.forward;
        Vector3 cameraForward = Vector3.left;
        Assert.AreEqual(HairdresserDirection.East,
            resolver.Invoke(null, new object[] { Vector3.forward, cameraRight, cameraForward }));
        Assert.AreEqual(HairdresserDirection.North,
            resolver.Invoke(null, new object[] { Vector3.left, cameraRight, cameraForward }));
    }

    [Test]
    public void ApprovedSpriteFeetAreAnchoredAtThePrefabGroundPlane()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/Hairdresser.prefab");
        Transform visual = prefab.transform.Find("Visual/HairdresserSpriteVisual");
        Sprite sprite = visual.GetComponent<SpriteRenderer>().sprite;
        float lowestCanvasPoint = visual.localPosition.y + sprite.bounds.min.y;
        Assert.That(lowestCanvasPoint, Is.InRange(0f, .04f));
    }

    [Test]
    public void PrefabHasIndependentGroundContactShadow()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/Hairdresser.prefab");
        System.Type rigType = System.Type.GetType(
            "HairSalon.Character.Hairdresser2_5DRig, HairSalon.Runtime");
        Component rig = rigType == null ? null : prefab.GetComponent(rigType);
        Transform shadow = prefab.transform.Find("Grounding/ContactShadow");
        Assert.IsNotNull(rig);
        Assert.IsNotNull(shadow);
        Assert.IsNotNull(shadow.GetComponent<SpriteRenderer>());
        Assert.AreNotSame(prefab.transform.Find("Visual"), shadow.parent);
        Assert.Less(shadow.localPosition.y, .05f);
    }

    [Test]
    public void AnimatorControllerReservesAllRequiredStates()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Resources/Characters/Hairdresser.controller");
        Assert.IsNotNull(controller);
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        CollectionAssert.IsSubsetOf(
            new[] { "Idle", "Walk", "CutHair", "DryHair", "WashHair" },
            System.Array.ConvertAll(states, entry => entry.state.name));
    }

    [Test]
    public void TwoPointFiveDPresenterReceivesDirectionAndAnimationState()
    {
        GameObject visual = new GameObject("Visual", typeof(SpriteRenderer));
        try
        {
            Hairdresser2DPresenter presenter = visual.AddComponent<Hairdresser2DPresenter>();
            presenter.Apply(HairdresserAnimationState.Walk, HairdresserDirection.West);
            Assert.AreEqual(HairdresserAnimationState.Walk, presenter.CurrentState);
            Assert.AreEqual(HairdresserDirection.West, presenter.CurrentDirection);
        }
        finally { Object.DestroyImmediate(visual); }
    }

    [Test]
    public void VisualReplacementOnlyChangesTheVisualSlot()
    {
        GameObject root = new GameObject("Hairdresser", typeof(Animator));
        GameObject visualSlot = new GameObject("Visual");
        visualSlot.transform.SetParent(root.transform, false);
        GameObject oldVisual = new GameObject("PlaceholderModel");
        oldVisual.transform.SetParent(visualSlot.transform, false);
        GameObject sockets = new GameObject("Sockets");
        sockets.transform.SetParent(root.transform, false);
        GameObject socket = new GameObject("RightHandToolSocket");
        socket.transform.SetParent(sockets.transform, false);
        GameObject replacementPrefab = new GameObject("ApprovedVisual");
        try
        {
            HairdresserCharacter character = root.AddComponent<HairdresserCharacter>();
            GameObject replacement = character.ReplaceVisual(replacementPrefab);

            Assert.AreEqual(1, visualSlot.transform.childCount);
            Assert.AreEqual("ApprovedVisual", replacement.name);
            Assert.AreSame(visualSlot.transform, replacement.transform.parent);
            Assert.IsNotNull(root.transform.Find("Sockets/RightHandToolSocket"));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(replacementPrefab);
        }
    }
}
