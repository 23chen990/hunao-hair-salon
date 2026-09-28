using UnityEngine;

/// <summary>
/// Keyboard mapping for the local two-player salon mode.
///
/// The mapping is deliberately pure so it can be checked without creating a
/// scene or reading Unity's live input state. Player 1 uses the left-hand
/// WASD/Space cluster and player 2 uses arrows/Right Control (or keypad
/// Enter). The legacy single-player surface keeps its combined mapping in
/// <see cref="SalonMobileControls"/>.
/// </summary>
public static class SalonCoopInputMap
{
    public static Vector2 ResolveKeyboardMove(int playerId, KeyCode key)
    {
        bool playerTwo = playerId == 2;
        if (!playerTwo)
        {
            if (key == KeyCode.W) return Vector2.up;
            if (key == KeyCode.A) return Vector2.left;
            if (key == KeyCode.S) return Vector2.down;
            if (key == KeyCode.D) return Vector2.right;
            return Vector2.zero;
        }

        if (key == KeyCode.UpArrow) return Vector2.up;
        if (key == KeyCode.LeftArrow) return Vector2.left;
        if (key == KeyCode.DownArrow) return Vector2.down;
        if (key == KeyCode.RightArrow) return Vector2.right;
        return Vector2.zero;
    }

    public static bool IsInteractionHeld(int playerId, KeyCode key)
    {
        return playerId == 2
            ? key == KeyCode.RightControl || key == KeyCode.KeypadEnter
            : key == KeyCode.Space;
    }

    /// <summary>
    /// Returns whether a key belongs to the player's interaction action. The
    /// live component applies GetKeyDown for the actual edge; keeping this
    /// pure helper parallel to IsInteractionHeld makes input contracts easy
    /// to test and reuse in browser/dev evidence.
    /// </summary>
    public static bool IsInteractionPressed(int playerId, KeyCode key)
    {
        return IsInteractionHeld(playerId, key);
    }
}
