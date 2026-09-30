using HairSalon;
using UnityEngine;

/// <summary>
/// One reading rule for every timed service bar: the bar fills while the work
/// is not ready and is full exactly when the player should act (release the
/// cut, rinse the foam, finish the blow-dry). After that only the colour
/// changes, so a full bar never means "keep waiting".
/// </summary>
public static class ServiceProgressDisplay
{
    public enum Phase
    {
        Working,
        Ready,
        Late,
        Failing
    }

    public static readonly Color LateColor = new Color32(240, 122, 46, 255);

    // Share of the perfect haircut window after which the bar warns that the
    // next moment is an overcut.
    private const float HaircutLateShare = .6f;

    public static float HaircutFill(float held, float perfectMin)
    {
        if (perfectMin <= 0f) return 1f;
        return Mathf.Clamp01(held / perfectMin);
    }

    public static Phase HaircutPhase(float held, float perfectMin, float perfectMax)
    {
        if (held < perfectMin) return Phase.Working;
        if (held > perfectMax) return Phase.Failing;
        float window = perfectMax - perfectMin;
        return window > 0f && held >= perfectMin + window * HaircutLateShare ? Phase.Late : Phase.Ready;
    }

    public static float BackgroundFill(BackgroundTaskModel task)
    {
        if (task == null) return 0f;
        if (task.IdealStart <= 0f) return 1f;
        return Mathf.Clamp01(task.Elapsed / task.IdealStart);
    }

    public static Phase BackgroundPhase(BackgroundTaskModel task)
    {
        if (task == null || task.Elapsed < task.IdealStart) return Phase.Working;
        if (task.Elapsed <= task.IdealEnd) return Phase.Ready;
        return task.Elapsed < task.DangerAt ? Phase.Late : Phase.Failing;
    }

    public static Color ColorFor(Phase phase)
    {
        if (phase == Phase.Working) return SalonPalette.Warning;
        if (phase == Phase.Ready) return SalonPalette.Success;
        return phase == Phase.Late ? LateColor : SalonPalette.Danger;
    }

    /// <summary>Late and failing bars blink so they read as "come back now" on a small screen.</summary>
    public static Color ColorFor(Phase phase, float time)
    {
        Color color = ColorFor(phase);
        if (phase != Phase.Late && phase != Phase.Failing) return color;
        float speed = phase == Phase.Failing ? 9f : 6f;
        float pulse = .5f + .5f * Mathf.Sin(time * speed);
        return Color.Lerp(color, Color.white, pulse * .35f);
    }
}
