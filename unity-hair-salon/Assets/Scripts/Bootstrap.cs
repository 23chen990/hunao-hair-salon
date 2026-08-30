using UnityEngine;

public sealed class Bootstrap : MonoBehaviour
{
    private void Awake()
    {
        if (FindAnyObjectByType<SalonDemo>() == null)
            new GameObject("SalonDemo", typeof(SalonDemo));
    }
}
