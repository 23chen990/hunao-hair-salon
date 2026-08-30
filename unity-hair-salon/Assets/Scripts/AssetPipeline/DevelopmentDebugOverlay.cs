using System;
using HairSalon.Character;
using HairSalon.CutStations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HairSalon.AssetPipeline
{
    public sealed class DevelopmentDebugOverlay : MonoBehaviour
    {
        private SalonDemo _salon;
        private HairdresserCharacter _character;
        private bool _visible;
        private Transform _visuals;
        private string _copyFeedback = string.Empty;
        private GUIStyle _style;

        public static bool IsRequested(string url, string[] arguments)
        {
            if (!string.IsNullOrEmpty(url) && url.IndexOf("debug=1", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (arguments == null) return false;
            foreach (string argument in arguments)
                if (string.Equals(argument, "--debug", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void Initialize(SalonDemo salon)
        {
            _salon = salon;
            _character = FindAnyObjectByType<HairdresserCharacter>();
            _visible = true;
            RebuildVisuals();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                _visible = !_visible;
                if (_visuals != null) _visuals.gameObject.SetActive(_visible);
            }
        }

        private void RebuildVisuals()
        {
            if (_visuals != null) Destroy(_visuals.gameObject);
            _visuals = new GameObject("DEV DEBUG VISUALS").transform;
            _visuals.SetParent(transform, false);
            foreach (CutStation station in FindObjectsByType<CutStation>())
            {
                CutStationDebugView.Build(station.transform,
                    AssetManifestToCutStationDebug(station), station.Layout);
                Transform built = station.transform.Find("DEV ONLY - CutStation Debug");
                if (built != null) built.SetParent(_visuals, true);
            }
            foreach (BoxCollider collider in FindObjectsByType<BoxCollider>())
            {
                if (!collider.enabled || collider.GetComponent<CutStation>() != null) continue;
                GameObject area = GameObject.CreatePrimitive(PrimitiveType.Cube);
                area.name = "Collider " + collider.name;
                area.transform.SetParent(_visuals, false);
                area.transform.position = collider.transform.TransformPoint(collider.center);
                area.transform.rotation = collider.transform.rotation;
                area.transform.localScale = Vector3.Scale(collider.size, collider.transform.lossyScale);
                Collider own = area.GetComponent<Collider>();
                if (own != null) Destroy(own);
                Renderer renderer = area.GetComponent<Renderer>();
                renderer.sharedMaterial = CreateDiagnosticMaterial(new Color(1f, .12f, .12f, .16f));
            }
        }

        private static CutStationDefinition AssetManifestToCutStationDebug(CutStation station)
        {
            CutStationDefinition definition = CutStationDefaults.CreateBuiltinDefinition();
            definition.Debug.FootprintColor = new Color(.05f, .65f, 1f, .24f);
            definition.Debug.CollisionColor = new Color(1f, .12f, .12f, .3f);
            definition.Debug.AnchorColor = new Color(.2f, 1f, .35f, .9f);
            return definition;
        }

        public static Material CreateDiagnosticMaterial(Color color)
        {
            Shader shader = Resources.Load<Shader>("SalonContactShadow");
            if (shader == null) throw new MissingReferenceException("SalonContactShadow shader is required for debug overlays.");
            return new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
        }

        private DevelopmentDebugContext Context()
        {
            _character ??= FindAnyObjectByType<HairdresserCharacter>();
            if (_salon != null) return _salon.CreateDevelopmentDebugContext();
            return new DevelopmentDebugContext
            {
                Scene = SceneManager.GetActiveScene().name,
                CharacterPosition = _character == null ? Vector3.zero : _character.transform.position,
                CharacterState = _character == null ? "missing" : _character.CurrentState.ToString(),
                ServiceState = "unknown",
                TargetStation = "none",
                Viewport = new Vector2Int(Screen.width, Screen.height),
                BuildVersion = Application.version
            };
        }

        private void OnGUI()
        {
            if (!_visible) return;
            DevelopmentDebugContext context = Context();
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Color.white } };
            GUI.Box(new Rect(Screen.width - 410f, 18f, 392f, 255f), string.Empty);
            float x = Screen.width - 392f;
            GUI.Label(new Rect(x, 30f, 360f, 24f), "DEVELOPMENT DEBUG  [F3]", _style);
            GUI.Label(new Rect(x, 58f, 360f, 24f), $"scene: {context.Scene}", _style);
            GUI.Label(new Rect(x, 84f, 360f, 24f), $"character: {context.CharacterState}  pos: {context.CharacterPosition:F2}", _style);
            GUI.Label(new Rect(x, 110f, 360f, 24f), $"service: {context.ServiceState}", _style);
            GUI.Label(new Rect(x, 136f, 360f, 24f), $"target: {context.TargetStation}", _style);
            GUI.Label(new Rect(x, 162f, 360f, 24f), $"viewport: {context.Viewport.x}x{context.Viewport.y}  build: {context.BuildVersion}", _style);
            GUI.Label(new Rect(x, 188f, 360f, 24f), "blue=footprint red=collision green=anchors; z=depth", _style);
            if (GUI.Button(new Rect(x, 218f, 210f, 38f), "COPY DEBUG STATE"))
            {
                GUIUtility.systemCopyBuffer = DevelopmentDebugSnapshot.Serialize(context);
                _copyFeedback = "copied";
            }
            GUI.Label(new Rect(x + 220f, 224f, 110f, 24f), _copyFeedback, _style);
        }
    }
}
