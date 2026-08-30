using UnityEngine;

namespace HairSalon.Character
{
    /// <summary>Stable boundary used to swap the placeholder for the frozen approved character asset.</summary>
    public interface IHairdresserVisualReplacer
    {
        Transform VisualRoot { get; }
        GameObject ReplaceVisual(GameObject visualPrefab);
    }
}
