using UnityEngine;

namespace HairSalon.CutStations
{
    public readonly struct CutStationAlignmentResult
    {
        public CutStationAlignmentResult(bool customerPosition, bool customerFacing, bool stylistPosition, bool stylistFacing)
        {
            CustomerPositionAligned = customerPosition;
            CustomerFacingAligned = customerFacing;
            StylistPositionAligned = stylistPosition;
            StylistFacingAligned = stylistFacing;
        }

        public bool CustomerPositionAligned { get; }
        public bool CustomerFacingAligned { get; }
        public bool StylistPositionAligned { get; }
        public bool StylistFacingAligned { get; }
        public bool IsAligned => CustomerPositionAligned && CustomerFacingAligned && StylistPositionAligned && StylistFacingAligned;
    }

    public static class CutStationAlignment
    {
        public static CutStationAlignmentResult ValidateServiceStart(
            ResolvedCutStationLayout layout,
            Vector3 customerPosition,
            Vector3 customerFacing,
            Vector3 stylistPosition,
            Vector3 stylistFacing,
            float positionTolerance = .12f,
            float minimumFacingDot = .95f)
        {
            if (layout == null) return new CutStationAlignmentResult(false, false, false, false);
            return new CutStationAlignmentResult(
                HorizontalDistance(customerPosition, layout.CustomerSeatAnchor) <= positionTolerance,
                FacingMatches(customerFacing, layout.CustomerFacing, minimumFacingDot),
                HorizontalDistance(stylistPosition, layout.StylistWorkAnchor) <= positionTolerance,
                FacingMatches(stylistFacing, layout.StylistFacing, minimumFacingDot));
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
            => Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));

        private static bool FacingMatches(Vector3 actual, Vector3 expected, float minimumDot)
        {
            actual.y = 0f;
            expected.y = 0f;
            return actual.sqrMagnitude > .001f && expected.sqrMagnitude > .001f &&
                   Vector3.Dot(actual.normalized, expected.normalized) >= minimumDot;
        }
    }
}
