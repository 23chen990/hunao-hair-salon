using UnityEngine;

namespace HairSalon.Candidates
{
    public sealed class FreeAssetCandidateMarker : MonoBehaviour
    {
        private const string ReadyMarker =
            "[FREE_ASSET_CANDIDATE_READY] source=KayKit Furniture Bits 1.0 FREE + Kenney Furniture Kit 2.0 + Blocky Characters 2.0 " +
            "license=CC0 furnitureModels=20 characterModels=3 washStation=proxy scene=FreeAssetPackCandidate";

        private void Start()
        {
            Debug.Log(ReadyMarker);
        }

        private void OnGUI()
        {
            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;
            GUI.color = new Color(0.05f, 0.08f, 0.09f, 0.88f);
            GUI.Box(new Rect(14f, 14f, 318f, 68f), GUIContent.none);
            GUI.color = Color.white;
            GUI.contentColor = new Color(0.96f, 0.94f, 0.86f, 1f);
            GUI.Label(new Rect(28f, 23f, 290f, 54f),
                "ACTUAL FREE 3D ASSET TEST\nKayKit + Kenney CC0 · Unity candidate scene\nWash area: sink + armchair proxy");
            GUI.color = previousColor;
            GUI.contentColor = previousContentColor;
        }
    }
}
