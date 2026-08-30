using UnityEngine;

namespace HairSalon.CutStations
{
    public sealed class CutStation : MonoBehaviour
    {
        public string AssetId => Layout?.AssetId;
        public CutStationOrientation Orientation => Layout == null ? CutStationOrientation.BackWall : Layout.Orientation;
        public ResolvedCutStationLayout Layout { get; internal set; }
        public Transform CustomerSeatAnchor { get; internal set; }
        public Transform StylistWorkAnchor { get; internal set; }
        public Transform QueueAnchor { get; internal set; }
        public Transform ToolAnchor { get; internal set; }
        public Transform ServiceVfxAnchor { get; internal set; }
        public BoxCollider Collision { get; internal set; }
        public int SortingOrder => Layout?.Sorting?.SortingOrder ?? 0;

        public CutStationAlignmentResult ValidateServiceStart(
            Vector3 customerPosition, Vector3 customerFacing, Vector3 stylistPosition, Vector3 stylistFacing,
            float positionTolerance = .12f, float minimumFacingDot = .95f)
        {
            return CutStationAlignment.ValidateServiceStart(
                Layout, customerPosition, customerFacing, stylistPosition, stylistFacing,
                positionTolerance, minimumFacingDot);
        }
    }
}
