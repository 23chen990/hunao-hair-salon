using System.Collections.Generic;
using System.Reflection;
using HairSalon;
using NUnit.Framework;
using UnityEngine;

public sealed class SalonSupplyVisualRegressionTests
{
    private GameObject _root;
    private SalonDemo _demo;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Supply visual regression");
        _demo = _root.AddComponent<SalonDemo>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [Test]
    public void SharedRackInventoryIsShownExactlyOnceAcrossOneOrTwoRacks()
    {
        for (int inventory = 0; inventory <= 6; inventory++)
        {
            AssertRackDisplay(inventory, expanded: false,
                expectedPrimary: inventory, expectedExpanded: 0);
            AssertRackDisplay(inventory, expanded: true,
                expectedPrimary: (inventory + 1) / 2, expectedExpanded: inventory / 2);
        }
    }

    [Test]
    public void RollingBackToAnUnpurchasedOpeningRemovesExpandedRackVisualAndAnchor()
    {
        var expandedRoot = new GameObject("Expanded rack under test");
        expandedRoot.transform.SetParent(_root.transform, false);
        var expandedAnchor = new GameObject("Expanded anchor under test").transform;
        expandedAnchor.SetParent(expandedRoot.transform, false);
        SetField("_mobileExpandedRackRoot", expandedRoot);
        SetField("_mobileExpandedRackShadowAnchor", expandedAnchor);
        SetField("_mobileExpandedWashRackPoint", expandedAnchor);
        SetField("_mobileExpandedRackBuilt", true);

        Invoke("RemoveExpandedWashRackVisual");

        Assert.IsTrue(expandedRoot == null || !expandedRoot.activeSelf);
        Assert.IsFalse((bool)typeof(SalonDemo).GetField(
            "_mobileExpandedRackBuilt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo));
        Assert.IsNull(typeof(SalonDemo).GetField(
            "_mobileExpandedWashRackPoint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo));
        Assert.IsNull(typeof(SalonDemo).GetField(
            "_mobileExpandedRackShadowAnchor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo));
    }

    private void AssertRackDisplay(int inventory, bool expanded, int expectedPrimary, int expectedExpanded)
    {
        var supply = new SalonSupplyModel(12, 3, 6);
        for (int i = 0; i < inventory; i++)
        {
            Assert.IsTrue(supply.TryPickUpWashKit());
            Assert.IsTrue(supply.TryDeliverWashKit());
        }

        SetField("_mobileSupplies", supply);
        List<GameObject> primary = CreateSlots("Primary", 6, "_mobileRackItems");
        List<GameObject> secondary = CreateSlots("Expanded", expanded ? 6 : 0,
            "_mobileExpandedRackItems");
        Invoke("UpdateMobileSupplyVisuals");

        int activePrimary = CountActive(primary);
        int activeSecondary = CountActive(secondary);
        Assert.AreEqual(expectedPrimary, activePrimary,
            $"inventory={inventory}, expanded={expanded}, primary");
        Assert.AreEqual(expectedExpanded, activeSecondary,
            $"inventory={inventory}, expanded={expanded}, expanded");
        Assert.AreEqual(inventory, activePrimary + activeSecondary,
            $"inventory={inventory}, expanded={expanded}, shared inventory must not duplicate");
    }

    private List<GameObject> CreateSlots(string prefix, int count, string fieldName)
    {
        var slots = (List<GameObject>)typeof(SalonDemo).GetField(
            fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_demo);
        slots.Clear();
        var result = new List<GameObject>();
        for (int i = 0; i < count; i++)
        {
            var item = new GameObject(prefix + " " + i);
            item.transform.SetParent(_root.transform, false);
            slots.Add(item);
            result.Add(item);
        }
        return result;
    }

    private static int CountActive(List<GameObject> items)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
            if (items[i].activeSelf) count++;
        return count;
    }

    private void SetField(string name, object value)
    {
        typeof(SalonDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_demo, value);
    }

    private void Invoke(string name)
    {
        typeof(SalonDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_demo, null);
    }
}
