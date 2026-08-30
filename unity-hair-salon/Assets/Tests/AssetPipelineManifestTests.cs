using System.Collections.Generic;
using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class AssetPipelineManifestTests
{
    private sealed class FakeResolver : IAssetResourceResolver
    {
        private readonly HashSet<string> _paths = new HashSet<string>();

        public FakeResolver(params string[] paths)
        {
            foreach (string path in paths) _paths.Add(path);
        }

        public bool Exists(string resourcePath)
            => resourcePath.StartsWith("builtin://") || _paths.Contains(resourcePath);
    }

    [Test]
    public void PackagedManifest_HasStableIdsAndPassesValidation()
    {
        AssetManifest manifest = AssetManifestLoader.LoadFromResources();

        Assert.That(manifest.Assets.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(manifest.Find("furniture-cut-station-classic"), Is.Not.Null);
        Assert.That(AssetManifestValidator.Validate(manifest), Is.Empty);
    }

    [Test]
    public void Validator_ReportsDuplicateIdIllegalDirectionInvalidFootprintAndMissingAnchor()
    {
        AssetDefinition first = ValidAsset("furniture-test");
        AssetDefinition duplicate = ValidAsset("furniture-test");
        duplicate.Directions.Add("ceiling");
        duplicate.Footprint.Size = new Vector2(0f, -1f);
        duplicate.RequiredAnchors.Add("stylist-work");
        var manifest = new AssetManifest
        {
            Assets = new List<AssetDefinition> { first, duplicate }
        };

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            manifest, new FakeResolver("Imported/test"));

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.DuplicateAssetId));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.IllegalDirection));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidFootprint));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.MissingRequiredAnchor));
    }

    [Test]
    public void Validator_ReportsMissingResourceAndInvalidPivot()
    {
        AssetDefinition asset = ValidAsset("furniture-test");
        asset.ResourcePath = "Imported/missing";
        asset.Pivot = new Vector2(1.2f, -.1f);

        IReadOnlyList<AssetValidationIssue> issues = AssetManifestValidator.Validate(
            new AssetManifest { Assets = new List<AssetDefinition> { asset } }, new FakeResolver());

        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.ResourceNotFound));
        Assert.That(issues, Has.Some.Matches<AssetValidationIssue>(x => x.Code == AssetValidationCode.InvalidPivot));
    }

    private static AssetDefinition ValidAsset(string id)
    {
        return new AssetDefinition
        {
            Id = id,
            Type = "furniture",
            ResourcePath = "Imported/test",
            Status = "approved",
            Directions = new List<string> { "back-wall", "right-wall" },
            Pivot = new Vector2(.5f, .1f),
            Footprint = new AssetArea { Center = Vector2.zero, Size = new Vector2(2f, 2f) },
            Collision = new AssetArea { Center = Vector2.zero, Size = Vector2.one },
            Shadow = new AssetShadow { Enabled = true, Size = new Vector2(1.5f, .8f), Opacity = .28f, Softness = .7f },
            InteractionAnchors = new List<AssetAnchor>
            {
                new AssetAnchor { Id = "customer-seat", Position = new Vector3(0f, 0f, -.8f), Facing = Vector3.forward }
            },
            RequiredAnchors = new List<string> { "customer-seat" },
            Sorting = new AssetSorting { Layer = "Default", Order = 5, DepthOffset = 0f }
        };
    }
}
