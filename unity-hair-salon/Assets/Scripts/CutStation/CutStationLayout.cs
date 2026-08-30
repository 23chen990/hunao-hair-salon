using System;
using UnityEngine;

namespace HairSalon.CutStations
{
    public readonly struct ResolvedCutStationRect
    {
        public ResolvedCutStationRect(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
        }

        public Vector2 Center { get; }
        public Vector2 Size { get; }
    }

    public sealed class ResolvedCutStationLayout
    {
        public string AssetId { get; internal set; }
        public Vector3 Origin { get; internal set; }
        public CutStationOrientation Orientation { get; internal set; }
        public float YawDegrees { get; internal set; }
        public ResolvedCutStationRect Footprint { get; internal set; }
        public ResolvedCutStationRect Collision { get; internal set; }
        public Vector3 CustomerSeatAnchor { get; internal set; }
        public Vector3 CustomerFacing { get; internal set; }
        public Vector3 StylistWorkAnchor { get; internal set; }
        public Vector3 StylistFacing { get; internal set; }
        public Vector3 QueueAnchor { get; internal set; }
        public Vector3 ToolAnchor { get; internal set; }
        public Vector3 ServiceVfxAnchor { get; internal set; }
        public CutStationSorting Sorting { get; internal set; }
    }

    public static class CutStationLayoutResolver
    {
        public static ResolvedCutStationLayout Resolve(
            CutStationDefinition definition,
            CutStationOrientation orientation,
            Vector3 origin = default)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Anchors == null) throw new ArgumentException("Cut station anchors are required.", nameof(definition));
            if (definition.Footprint == null || definition.Collision == null)
                throw new ArgumentException("Cut station footprint and collision are required.", nameof(definition));
            if (definition.LegalOrientations == null || !definition.LegalOrientations.Contains(orientation))
                throw new ArgumentException($"Orientation {orientation} is not legal for {definition.AssetId}.", nameof(orientation));

            bool quarterTurn = orientation == CutStationOrientation.RightWall;
            return new ResolvedCutStationLayout
            {
                AssetId = definition.AssetId,
                Origin = origin,
                Orientation = orientation,
                YawDegrees = quarterTurn ? 90f : 0f,
                Footprint = ResolveRect(definition.Footprint, quarterTurn),
                Collision = ResolveRect(definition.Collision, quarterTurn),
                CustomerSeatAnchor = origin + Rotate(definition.Anchors.CustomerSeat, quarterTurn),
                CustomerFacing = Rotate(definition.Anchors.CustomerFacing, quarterTurn).normalized,
                StylistWorkAnchor = origin + Rotate(definition.Anchors.StylistWork, quarterTurn),
                StylistFacing = Rotate(definition.Anchors.StylistFacing, quarterTurn).normalized,
                QueueAnchor = origin + Rotate(definition.Anchors.Queue, quarterTurn),
                ToolAnchor = origin + Rotate(definition.Anchors.Tool, quarterTurn),
                ServiceVfxAnchor = origin + Rotate(definition.Anchors.ServiceVfx, quarterTurn),
                Sorting = definition.Sorting
            };
        }

        private static ResolvedCutStationRect ResolveRect(CutStationRect rect, bool quarterTurn)
        {
            Vector3 center = Rotate(new Vector3(rect.Center.x, 0f, rect.Center.y), quarterTurn);
            Vector2 size = quarterTurn ? new Vector2(rect.Size.y, rect.Size.x) : rect.Size;
            return new ResolvedCutStationRect(new Vector2(center.x, center.z), size);
        }

        internal static Vector3 Rotate(Vector3 value, bool quarterTurn)
        {
            return quarterTurn ? new Vector3(value.z, value.y, -value.x) : value;
        }
    }
}
