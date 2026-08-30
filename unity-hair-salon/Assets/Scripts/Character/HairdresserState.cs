using UnityEngine;

namespace HairSalon.Character
{
    public enum HairdresserDirection
    {
        North = 0,
        NorthEast = 1,
        East = 2,
        SouthEast = 3,
        South = 4,
        SouthWest = 5,
        West = 6,
        NorthWest = 7
    }

    public enum HairdresserAnimationState
    {
        Idle = 0,
        Walk = 1,
        CutHair = 2,
        DryHair = 3,
        WashHair = 4
    }

    public static class HairdresserDirectionResolver
    {
        public static HairdresserDirection FromVector(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f) return HairdresserDirection.North;
            float clockwiseDegrees = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            int sector = Mathf.RoundToInt(clockwiseDegrees / 45f);
            return (HairdresserDirection)((sector % 8 + 8) % 8);
        }
    }
}
