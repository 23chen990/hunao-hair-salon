using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    public enum AssetValidationCode
    {
        MissingAssetId,
        InvalidAssetId,
        DuplicateAssetId,
        InvalidType,
        MissingResourcePath,
        ResourceNotFound,
        InvalidStatus,
        IllegalDirection,
        InvalidPivot,
        InvalidFootprint,
        InvalidCollision,
        InvalidShadow,
        DuplicateAnchor,
        MissingRequiredAnchor,
        InvalidAnchor,
        MissingSorting
    }

    public readonly struct AssetValidationIssue
    {
        public AssetValidationIssue(AssetValidationCode code, string assetId, string message)
        {
            Code = code;
            AssetId = assetId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public AssetValidationCode Code { get; }
        public string AssetId { get; }
        public string Message { get; }
        public override string ToString() => $"[{Code}] {AssetId}: {Message}";
    }

    public interface IAssetResourceResolver
    {
        bool Exists(string resourcePath);
    }

    public sealed class UnityAssetResourceResolver : IAssetResourceResolver
    {
        public bool Exists(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return false;
            if (resourcePath.StartsWith("builtin://", StringComparison.Ordinal)) return true;
            return Resources.Load<UnityEngine.Object>(resourcePath) != null;
        }
    }

    public static class AssetManifestValidator
    {
        private static readonly Regex StableId = new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
        private static readonly HashSet<string> Types = new HashSet<string>(StringComparer.Ordinal)
            { "furniture", "character", "ui-sprite", "effect", "prop" };
        private static readonly HashSet<string> Statuses = new HashSet<string>(StringComparer.Ordinal)
            { "draft", "review", "approved", "deprecated" };
        private static readonly HashSet<string> Directions = new HashSet<string>(StringComparer.Ordinal)
            { "back-wall", "right-wall", "free" };

        public static IReadOnlyList<AssetValidationIssue> Validate(
            AssetManifest manifest, IAssetResourceResolver resolver = null)
        {
            var issues = new List<AssetValidationIssue>();
            if (manifest == null || manifest.Assets == null)
            {
                issues.Add(new AssetValidationIssue(AssetValidationCode.MissingAssetId, string.Empty, "Manifest or Assets list is missing."));
                return issues;
            }

            resolver ??= new UnityAssetResourceResolver();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AssetDefinition asset in manifest.Assets)
            {
                if (asset == null)
                {
                    issues.Add(new AssetValidationIssue(AssetValidationCode.MissingAssetId, string.Empty, "Asset entry is null."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(asset.Id)) Add(issues, AssetValidationCode.MissingAssetId, asset, "Stable asset Id is required.");
                else
                {
                    if (!StableId.IsMatch(asset.Id)) Add(issues, AssetValidationCode.InvalidAssetId, asset, "Id must use lowercase kebab-case.");
                    if (!ids.Add(asset.Id)) Add(issues, AssetValidationCode.DuplicateAssetId, asset, "Asset Id must be unique.");
                }
                if (!Types.Contains(asset.Type ?? string.Empty)) Add(issues, AssetValidationCode.InvalidType, asset, $"Unsupported type: {asset.Type}");
                if (string.IsNullOrWhiteSpace(asset.ResourcePath)) Add(issues, AssetValidationCode.MissingResourcePath, asset, "Resource path is required.");
                else if (!resolver.Exists(asset.ResourcePath)) Add(issues, AssetValidationCode.ResourceNotFound, asset, $"Resource not found: {asset.ResourcePath}");
                if (!Statuses.Contains(asset.Status ?? string.Empty)) Add(issues, AssetValidationCode.InvalidStatus, asset, $"Unsupported status: {asset.Status}");
                ValidateDirections(asset, issues);
                if (!Finite(asset.Pivot) || asset.Pivot.x < 0f || asset.Pivot.x > 1f || asset.Pivot.y < 0f || asset.Pivot.y > 1f)
                    Add(issues, AssetValidationCode.InvalidPivot, asset, "Pivot must be normalized between 0 and 1.");
                ValidateArea(asset, asset.Footprint, AssetValidationCode.InvalidFootprint, "Footprint", issues);
                ValidateArea(asset, asset.Collision, AssetValidationCode.InvalidCollision, "Collision", issues);
                ValidateShadow(asset, issues);
                ValidateAnchors(asset, issues);
                if (asset.Sorting == null || string.IsNullOrWhiteSpace(asset.Sorting.Layer) || !Finite(asset.Sorting.DepthOffset))
                    Add(issues, AssetValidationCode.MissingSorting, asset, "Sorting layer/order/depth information is required.");
            }
            return issues;
        }

        private static void ValidateDirections(AssetDefinition asset, List<AssetValidationIssue> issues)
        {
            if (asset.Directions == null || asset.Directions.Count == 0)
            {
                Add(issues, AssetValidationCode.IllegalDirection, asset, "At least one supported direction is required.");
                return;
            }
            foreach (string direction in asset.Directions)
                if (!Directions.Contains(direction ?? string.Empty))
                    Add(issues, AssetValidationCode.IllegalDirection, asset, $"Illegal direction: {direction}");
        }

        private static void ValidateArea(AssetDefinition asset, AssetArea area, AssetValidationCode code, string label, List<AssetValidationIssue> issues)
        {
            if (area == null || !Finite(area.Center) || !Finite(area.Size) || area.Size.x <= 0f || area.Size.y <= 0f)
                Add(issues, code, asset, $"{label} center must be finite and size must be positive.");
        }

        private static void ValidateShadow(AssetDefinition asset, List<AssetValidationIssue> issues)
        {
            if (asset.Shadow == null || !asset.Shadow.Enabled) return;
            if (!Finite(asset.Shadow.Size) || !Finite(asset.Shadow.Offset) ||
                asset.Shadow.Size.x <= 0f || asset.Shadow.Size.y <= 0f ||
                !Finite(asset.Shadow.Opacity) || asset.Shadow.Opacity < 0f || asset.Shadow.Opacity > 1f ||
                !Finite(asset.Shadow.Softness) || asset.Shadow.Softness < 0f || asset.Shadow.Softness > 1f)
                Add(issues, AssetValidationCode.InvalidShadow, asset, "Shadow size, offset, opacity or softness is invalid.");
        }

        private static void ValidateAnchors(AssetDefinition asset, List<AssetValidationIssue> issues)
        {
            var configured = new HashSet<string>(StringComparer.Ordinal);
            if (asset.InteractionAnchors != null)
            {
                foreach (AssetAnchor anchor in asset.InteractionAnchors)
                {
                    if (anchor == null || string.IsNullOrWhiteSpace(anchor.Id) || !Finite(anchor.Position) ||
                        !Finite(anchor.Facing) || anchor.Facing.sqrMagnitude < .001f)
                    {
                        Add(issues, AssetValidationCode.InvalidAnchor, asset, "Every anchor needs an Id, finite position and non-zero facing.");
                        continue;
                    }
                    if (!configured.Add(anchor.Id)) Add(issues, AssetValidationCode.DuplicateAnchor, asset, $"Duplicate anchor: {anchor.Id}");
                }
            }
            if (asset.RequiredAnchors == null) return;
            foreach (string required in asset.RequiredAnchors)
                if (string.IsNullOrWhiteSpace(required) || !configured.Contains(required))
                    Add(issues, AssetValidationCode.MissingRequiredAnchor, asset, $"Missing required anchor: {required}");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static void Add(List<AssetValidationIssue> issues, AssetValidationCode code, AssetDefinition asset, string message)
            => issues.Add(new AssetValidationIssue(code, asset?.Id, message));
    }
}
