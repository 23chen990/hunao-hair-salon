using System.Collections.Generic;
using HairSalon.CutStations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public sealed class CutStationPipelineTests
{
    private sealed class FakeResources : ICutStationResourceResolver
    {
        private readonly HashSet<string> _paths;

        public FakeResources(params string[] paths)
        {
            _paths = new HashSet<string>(paths);
        }

        public bool Exists(CutStationVisualType type, string resourcePath)
        {
            return type == CutStationVisualType.BuiltinProcedural || _paths.Contains(resourcePath);
        }
    }

    [Test]
    public void BuiltinDefinition_ContainsCompleteReplaceableStationContract()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();

        Assert.That(definition.AssetId, Is.EqualTo("cut-station-classic-poc"));
        Assert.That(definition.Visual.Type, Is.EqualTo(CutStationVisualType.BuiltinProcedural));
        Assert.That(definition.Visual.ResourcePath, Is.EqualTo("builtin://cut-station/classic-poc"));
        Assert.That(definition.Visual.DefaultSize.x, Is.GreaterThan(0f));
        Assert.That(definition.Visual.DefaultScale, Is.GreaterThan(0f));
        Assert.That(definition.Footprint.Size.x, Is.GreaterThan(0f));
        Assert.That(definition.Collision.Size.y, Is.GreaterThan(0f));
        Assert.That(definition.Anchors, Is.Not.Null);
        Assert.That(definition.Sorting, Is.Not.Null);
        Assert.That(definition.Debug, Is.Not.Null);
        CollectionAssert.AreEquivalent(
            new[] { CutStationOrientation.BackWall, CutStationOrientation.RightWall },
            definition.LegalOrientations);
    }

    [Test]
    public void Resolve_RightWall_UsesSingleQuarterTurnRuleForEveryAnchorAndFacing()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        ResolvedCutStationLayout back = CutStationLayoutResolver.Resolve(
            definition, CutStationOrientation.BackWall, new Vector3(10f, 0f, 20f));
        ResolvedCutStationLayout right = CutStationLayoutResolver.Resolve(
            definition, CutStationOrientation.RightWall, new Vector3(10f, 0f, 20f));

        Vector3 localSeat = back.CustomerSeatAnchor - back.Origin;
        Vector3 expectedSeat = new Vector3(localSeat.z, localSeat.y, -localSeat.x) + right.Origin;
        Assert.That(right.CustomerSeatAnchor, Is.EqualTo(expectedSeat).Using(Vector3ComparerWithEqualsOperator.Instance));
        Assert.That(right.CustomerFacing, Is.EqualTo(Vector3.right).Using(Vector3ComparerWithEqualsOperator.Instance));
        Assert.That(right.StylistFacing, Is.EqualTo(Vector3.right).Using(Vector3ComparerWithEqualsOperator.Instance));
        Assert.That(right.Footprint.Size, Is.EqualTo(new Vector2(back.Footprint.Size.y, back.Footprint.Size.x)));
        Assert.That(right.Orientation, Is.EqualTo(CutStationOrientation.RightWall));
    }

    [Test]
    public void ChangingVisualAsset_DoesNotChangeServiceAnchors()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        ResolvedCutStationLayout before = CutStationLayoutResolver.Resolve(definition, CutStationOrientation.BackWall);
        definition.Visual = new CutStationVisual
        {
            Type = CutStationVisualType.PrefabOrModel,
            ResourcePath = "CutStations/FormalChairV2",
            DefaultSize = new Vector3(2.5f, 3f, 2f),
            DefaultScale = 0.85f
        };
        ResolvedCutStationLayout after = CutStationLayoutResolver.Resolve(definition, CutStationOrientation.BackWall);

        Assert.That(after.CustomerSeatAnchor, Is.EqualTo(before.CustomerSeatAnchor));
        Assert.That(after.StylistWorkAnchor, Is.EqualTo(before.StylistWorkAnchor));
        Assert.That(after.QueueAnchor, Is.EqualTo(before.QueueAnchor));
    }

    [Test]
    public void ValidateManifest_RejectsDuplicateIdsAndMissingResource()
    {
        CutStationDefinition first = CutStationDefaults.CreateBuiltinDefinition();
        CutStationDefinition duplicate = CutStationDefaults.CreateBuiltinDefinition();
        duplicate.Visual = new CutStationVisual
        {
            Type = CutStationVisualType.Sprite2D,
            ResourcePath = "CutStations/MissingSprite",
            DefaultSize = Vector3.one,
            DefaultScale = 1f
        };
        var manifest = new CutStationManifest { Stations = new List<CutStationDefinition> { first, duplicate } };

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateManifest(
            manifest, new FakeResources());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.DuplicateAssetId));
        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.ResourceNotFound));
    }

    [Test]
    public void ValidateDefinition_RejectsUnregisteredBuiltinVisualPath()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.Visual.ResourcePath = "builtin://cut-station/typo";

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateDefinition(
            definition, new UnityCutStationResourceResolver());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.ResourceNotFound));
    }

    [Test]
    public void ValidateDefinition_RejectsMissingAnchorsInvalidNumbersAndInvalidFootprint()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.Anchors = null;
        definition.Footprint.Size = new Vector2(0f, float.NaN);
        definition.Visual.DefaultScale = float.PositiveInfinity;

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateDefinition(
            definition, new FakeResources());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.MissingRequiredAnchors));
        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.InvalidFootprint));
        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.NonFiniteNumber));
    }

    [Test]
    public void ValidateDefinition_RejectsIndividuallyOmittedAnchorDecodedAsZero()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.Anchors.Queue = Vector3.zero;

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateDefinition(
            definition, new FakeResources());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.MissingRequiredAnchors));
    }

    [Test]
    public void Resolve_RejectsOrientationNotDeclaredByDefinition()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.LegalOrientations = new List<CutStationOrientation> { CutStationOrientation.BackWall };

        Assert.That(
            () => CutStationLayoutResolver.Resolve(definition, CutStationOrientation.RightWall),
            Throws.ArgumentException);
    }

    [Test]
    public void ValidateDefinition_RejectsUnknownOrientationValue()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.LegalOrientations.Add((CutStationOrientation)999);

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateDefinition(
            definition, new FakeResources());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.IllegalOrientation));
    }

    [Test]
    public void ValidateDefinition_RejectsInteractionAnchorInsideCollisionAndCharacterOverlap()
    {
        CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
        definition.Anchors.CustomerSeat = Vector3.zero;
        definition.Anchors.StylistWork = new Vector3(.1f, 0f, .1f);

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateDefinition(
            definition, new FakeResources());

        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.AnchorInsideCollision));
        Assert.That(issues, Has.Some.Matches<CutStationValidationIssue>(x => x.Code == CutStationValidationCode.CharacterAnchorsOverlap));
    }

    [Test]
    public void ValidateServiceAlignment_RequiresBothCharactersAtAnchorAndFacing()
    {
        ResolvedCutStationLayout layout = CutStationLayoutResolver.Resolve(
            CutStationDefaults.CreateBuiltinDefinition(), CutStationOrientation.BackWall);

        CutStationAlignmentResult misaligned = CutStationAlignment.ValidateServiceStart(
            layout,
            layout.CustomerSeatAnchor + Vector3.right,
            layout.CustomerFacing,
            layout.StylistWorkAnchor,
            -layout.StylistFacing);
        CutStationAlignmentResult aligned = CutStationAlignment.ValidateServiceStart(
            layout,
            layout.CustomerSeatAnchor,
            layout.CustomerFacing,
            layout.StylistWorkAnchor,
            layout.StylistFacing);

        Assert.That(misaligned.IsAligned, Is.False);
        Assert.That(misaligned.CustomerPositionAligned, Is.False);
        Assert.That(misaligned.StylistFacingAligned, Is.False);
        Assert.That(aligned.IsAligned, Is.True);
    }

    [Test]
    public void BuiltinManifest_PassesAllStaticValidation()
    {
        CutStationManifest manifest = CutStationDefaults.CreateBuiltinManifest();

        IReadOnlyList<CutStationValidationIssue> issues = CutStationValidator.ValidateManifest(
            manifest, new FakeResources());

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public void Factory_CreatesQueryableStationFromIdPositionAndOrientation()
    {
        CutStation station = null;
        try
        {
            Vector3 origin = new Vector3(-2.1f, .2f, .2f);
            station = CutStationFactory.Create(
                CutStationDefaults.CreateBuiltinManifest(),
                "cut-station-classic-poc",
                origin,
                CutStationOrientation.RightWall);

            Assert.That(station.AssetId, Is.EqualTo("cut-station-classic-poc"));
            Assert.That(station.Orientation, Is.EqualTo(CutStationOrientation.RightWall));
            Assert.That(station.Layout.Origin, Is.EqualTo(origin));
            Assert.That(station.CustomerSeatAnchor.position, Is.EqualTo(station.Layout.CustomerSeatAnchor));
            Assert.That(station.StylistWorkAnchor.position, Is.EqualTo(station.Layout.StylistWorkAnchor));
            Assert.That(station.QueueAnchor.position, Is.EqualTo(station.Layout.QueueAnchor));
            Assert.That(station.ToolAnchor.position, Is.EqualTo(station.Layout.ToolAnchor));
            Assert.That(station.ServiceVfxAnchor.position, Is.EqualTo(station.Layout.ServiceVfxAnchor));
            Assert.That(station.Collision, Is.Not.Null);
            Assert.That(station.SortingOrder, Is.EqualTo(station.Layout.Sorting.SortingOrder));
            Assert.That(station.transform.Find("Visual"), Is.Not.Null);
            Assert.That(station.transform.Find("Visual").GetComponentsInChildren<Renderer>(),
                Is.All.Matches<Renderer>(renderer => renderer.sortingOrder == station.SortingOrder));
        }
        finally
        {
            if (station != null) Object.DestroyImmediate(station.gameObject);
        }
    }

    [Test]
    public void ResourcesManifest_LoadsAndPassesValidation()
    {
        CutStationManifest manifest = CutStationManifestLoader.LoadFromResources();

        Assert.That(manifest.Find("cut-station-classic-poc"), Is.Not.Null);
        Assert.That(CutStationValidator.ValidateManifest(manifest, new UnityCutStationResourceResolver()), Is.Empty);
    }
}
