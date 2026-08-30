using System;
using System.Collections.Generic;
using UnityEngine;

namespace HairSalon.CutStations
{
    public enum CutStationValidationCode
    {
        MissingAssetId,
        DuplicateAssetId,
        MissingVisual,
        MissingResourcePath,
        ResourceNotFound,
        MissingRequiredAnchors,
        NonFiniteNumber,
        InvalidVisualSize,
        InvalidFootprint,
        InvalidCollision,
        IllegalOrientation,
        AnchorInsideCollision,
        CharacterAnchorsOverlap,
        MissingSorting,
        MissingDebugDisplay
    }

    public readonly struct CutStationValidationIssue
    {
        public CutStationValidationIssue(CutStationValidationCode code, string assetId, string message)
        {
            Code = code;
            AssetId = assetId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CutStationValidationCode Code { get; }
        public string AssetId { get; }
        public string Message { get; }
        public override string ToString() => $"[{Code}] {AssetId}: {Message}";
    }

    public interface ICutStationResourceResolver
    {
        bool Exists(CutStationVisualType type, string resourcePath);
    }

    public sealed class UnityCutStationResourceResolver : ICutStationResourceResolver
    {
        public bool Exists(CutStationVisualType type, string resourcePath)
        {
            if (type == CutStationVisualType.BuiltinProcedural)
                return CutStationBuiltinVisuals.Exists(resourcePath);
            return !string.IsNullOrWhiteSpace(resourcePath) && Resources.Load<UnityEngine.Object>(resourcePath) != null;
        }
    }

    public static class CutStationValidator
    {
        private const float MinimumCharacterSeparation = .55f;

        public static IReadOnlyList<CutStationValidationIssue> ValidateManifest(
            CutStationManifest manifest,
            ICutStationResourceResolver resources = null)
        {
            var issues = new List<CutStationValidationIssue>();
            if (manifest == null || manifest.Stations == null) return issues;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CutStationDefinition definition in manifest.Stations)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.AssetId) && !ids.Add(definition.AssetId))
                    Add(issues, CutStationValidationCode.DuplicateAssetId, definition, "AssetId must be unique within the manifest.");
                issues.AddRange(ValidateDefinition(definition, resources));
            }
            return issues;
        }

        public static IReadOnlyList<CutStationValidationIssue> ValidateDefinition(
            CutStationDefinition definition,
            ICutStationResourceResolver resources = null)
        {
            var issues = new List<CutStationValidationIssue>();
            if (definition == null)
            {
                issues.Add(new CutStationValidationIssue(CutStationValidationCode.MissingAssetId, string.Empty, "Station definition is null."));
                return issues;
            }

            if (string.IsNullOrWhiteSpace(definition.AssetId)) Add(issues, CutStationValidationCode.MissingAssetId, definition, "AssetId is required.");
            ValidateVisual(definition, resources ?? new UnityCutStationResourceResolver(), issues);
            ValidateRect(definition, definition.Footprint, true, issues);
            ValidateRect(definition, definition.Collision, false, issues);
            ValidateOrientations(definition, issues);
            ValidateAnchors(definition, issues);
            if (definition.Sorting == null) Add(issues, CutStationValidationCode.MissingSorting, definition, "Sorting/depth rules are required.");
            else if (!Finite(definition.Sorting.DepthOffset)) Add(issues, CutStationValidationCode.NonFiniteNumber, definition, "Sorting depth must be finite.");
            if (definition.Debug == null) Add(issues, CutStationValidationCode.MissingDebugDisplay, definition, "Debug display rules are required.");
            else if (!Finite(definition.Debug.MarkerSize) || definition.Debug.MarkerSize <= 0f)
                Add(issues, CutStationValidationCode.NonFiniteNumber, definition, "Debug marker size must be finite and positive.");
            return issues;
        }

        private static void ValidateVisual(CutStationDefinition definition, ICutStationResourceResolver resources, List<CutStationValidationIssue> issues)
        {
            CutStationVisual visual = definition.Visual;
            if (visual == null)
            {
                Add(issues, CutStationValidationCode.MissingVisual, definition, "Visual/model configuration is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(visual.ResourcePath)) Add(issues, CutStationValidationCode.MissingResourcePath, definition, "Visual resource path is required.");
            else if (!resources.Exists(visual.Type, visual.ResourcePath)) Add(issues, CutStationValidationCode.ResourceNotFound, definition, $"Resource not found: {visual.ResourcePath}");
            if (!Finite(visual.DefaultSize) || !Finite(visual.DefaultScale))
                Add(issues, CutStationValidationCode.NonFiniteNumber, definition, "Visual size and scale must be finite.");
            if (visual.DefaultSize.x <= 0f || visual.DefaultSize.y <= 0f || visual.DefaultSize.z <= 0f || visual.DefaultScale <= 0f)
                Add(issues, CutStationValidationCode.InvalidVisualSize, definition, "Visual size and scale must be positive.");
        }

        private static void ValidateRect(CutStationDefinition definition, CutStationRect rect, bool footprint, List<CutStationValidationIssue> issues)
        {
            CutStationValidationCode code = footprint ? CutStationValidationCode.InvalidFootprint : CutStationValidationCode.InvalidCollision;
            if (rect == null)
            {
                Add(issues, code, definition, footprint ? "Footprint is required." : "Collision is required.");
                return;
            }
            if (!Finite(rect.Center) || !Finite(rect.Size)) Add(issues, CutStationValidationCode.NonFiniteNumber, definition, "Footprint/collision values must be finite.");
            if (!Finite(rect.Size) || rect.Size.x <= 0f || rect.Size.y <= 0f) Add(issues, code, definition, "Footprint/collision size must be positive.");
        }

        private static void ValidateOrientations(CutStationDefinition definition, List<CutStationValidationIssue> issues)
        {
            if (definition.LegalOrientations == null || definition.LegalOrientations.Count == 0)
            {
                Add(issues, CutStationValidationCode.IllegalOrientation, definition, "At least one legal orientation is required.");
                return;
            }
            foreach (CutStationOrientation orientation in definition.LegalOrientations)
            {
                if (orientation != CutStationOrientation.BackWall && orientation != CutStationOrientation.RightWall)
                    Add(issues, CutStationValidationCode.IllegalOrientation, definition, $"Unknown orientation value: {(int)orientation}.");
            }
        }

        private static void ValidateAnchors(CutStationDefinition definition, List<CutStationValidationIssue> issues)
        {
            CutStationAnchors anchors = definition.Anchors;
            if (anchors == null)
            {
                Add(issues, CutStationValidationCode.MissingRequiredAnchors, definition, "Customer, stylist, queue, tool and VFX anchors are required.");
                return;
            }
            if (!Finite(anchors.CustomerSeat) || !Finite(anchors.CustomerFacing) ||
                !Finite(anchors.StylistWork) || !Finite(anchors.StylistFacing) ||
                !Finite(anchors.Queue) || !Finite(anchors.Tool) || !Finite(anchors.ServiceVfx))
                Add(issues, CutStationValidationCode.NonFiniteNumber, definition, "All anchors and facings must contain finite coordinates.");
            if (anchors.CustomerFacing.sqrMagnitude < .001f || anchors.StylistFacing.sqrMagnitude < .001f)
                Add(issues, CutStationValidationCode.MissingRequiredAnchors, definition, "Customer and stylist facing vectors must be non-zero.");
            if (anchors.CustomerSeat.sqrMagnitude < .000001f || anchors.StylistWork.sqrMagnitude < .000001f ||
                anchors.Queue.sqrMagnitude < .000001f || anchors.Tool.sqrMagnitude < .000001f ||
                anchors.ServiceVfx.sqrMagnitude < .000001f)
                Add(issues, CutStationValidationCode.MissingRequiredAnchors, definition,
                    "Every required position anchor must be explicitly configured and non-zero.");

            if (definition.Collision != null &&
                (Inside(definition.Collision, anchors.CustomerSeat) || Inside(definition.Collision, anchors.StylistWork) ||
                 Inside(definition.Collision, anchors.Queue) || Inside(definition.Collision, anchors.Tool)))
                Add(issues, CutStationValidationCode.AnchorInsideCollision, definition, "A character/tool interaction anchor lies inside station collision.");

            Vector2 customer = new Vector2(anchors.CustomerSeat.x, anchors.CustomerSeat.z);
            Vector2 stylist = new Vector2(anchors.StylistWork.x, anchors.StylistWork.z);
            if (Vector2.Distance(customer, stylist) < MinimumCharacterSeparation)
                Add(issues, CutStationValidationCode.CharacterAnchorsOverlap, definition, "Customer and stylist anchors overlap.");
        }

        private static bool Inside(CutStationRect rect, Vector3 point)
        {
            Vector2 half = rect.Size * .5f;
            return point.x > rect.Center.x - half.x && point.x < rect.Center.x + half.x &&
                   point.z > rect.Center.y - half.y && point.z < rect.Center.y + half.y;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static void Add(List<CutStationValidationIssue> issues, CutStationValidationCode code, CutStationDefinition definition, string message)
            => issues.Add(new CutStationValidationIssue(code, definition?.AssetId, message));
    }
}
