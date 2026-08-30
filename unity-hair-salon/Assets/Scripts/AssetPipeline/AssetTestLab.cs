using System;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    public sealed class AssetTestLab : MonoBehaviour
    {
        private AssetManifest _manifest;
        private int _index;
        private string _direction = "back-wall";
        private Transform _current;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public int AssetCount => _manifest?.Assets?.Count ?? 0;
        public string CurrentAssetId => AssetCount == 0 ? string.Empty : _manifest.Assets[_index].Id;
        public string CurrentDirection => _direction;

        private void Start()
        {
            if (_manifest == null) Initialize(AssetManifestLoader.LoadFromResources());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) NextAsset();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousAsset();
            if (Input.GetKeyDown(KeyCode.Space)) ToggleDirection();
        }

        public void Initialize(AssetManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            if (AssetManifestValidator.Validate(_manifest).Count > 0)
                throw new InvalidOperationException("Asset test lab cannot open an invalid manifest.");
            _index = 0;
            _direction = "back-wall";
            BuildEnvironment();
            RebuildCurrent();
        }

        public void NextAsset()
        {
            if (AssetCount == 0) return;
            _index = (_index + 1) % AssetCount;
            _direction = FirstDirection(_manifest.Assets[_index]);
            RebuildCurrent();
        }

        public void PreviousAsset()
        {
            if (AssetCount == 0) return;
            _index = (_index - 1 + AssetCount) % AssetCount;
            _direction = FirstDirection(_manifest.Assets[_index]);
            RebuildCurrent();
        }

        public void ToggleDirection()
        {
            if (AssetCount == 0) return;
            AssetDefinition asset = _manifest.Assets[_index];
            if (!asset.Directions.Contains("back-wall") || !asset.Directions.Contains("right-wall")) return;
            _direction = _direction == "back-wall" ? "right-wall" : "back-wall";
            RebuildCurrent();
        }

        private void BuildEnvironment()
        {
            if (transform.Find("Lab Environment") != null) return;
            var environment = new GameObject("Lab Environment").transform;
            environment.SetParent(transform, false);
            GameObject floor = Primitive(environment, "Floor", PrimitiveType.Cube, new Vector3(0f, -.12f, 0f),
                new Vector3(12f, .2f, 10f), new Color(.91f, .87f, .78f));
            floor.GetComponent<Collider>().enabled = false;
            GameObject gridX = Primitive(environment, "Back Wall", PrimitiveType.Cube, new Vector3(0f, 2.5f, 4f),
                new Vector3(12f, 5f, .12f), new Color(.82f, .9f, .88f));
            gridX.GetComponent<Collider>().enabled = false;
            GameObject gridZ = Primitive(environment, "Right Wall", PrimitiveType.Cube, new Vector3(5.8f, 2.5f, 0f),
                new Vector3(.12f, 5f, 8f), new Color(.88f, .83f, .75f));
            gridZ.GetComponent<Collider>().enabled = false;

            var cameraObject = new GameObject("Asset Lab Camera", typeof(Camera));
            cameraObject.transform.SetParent(environment, false);
            cameraObject.transform.position = new Vector3(7.5f, 6.5f, -9.5f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(-7.5f, -4.2f, 9.5f), Vector3.up);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 38f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.11f, .13f, .14f);
            cameraObject.tag = "MainCamera";

            var lightObject = new GameObject("Key Light", typeof(Light));
            lightObject.transform.SetParent(environment, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
        }

        private void RebuildCurrent()
        {
            if (_current != null)
            {
                if (Application.isPlaying) Destroy(_current.gameObject);
                else DestroyImmediate(_current.gameObject);
            }
            if (AssetCount == 0) return;
            AssetDefinition asset = _manifest.Assets[_index];
            _current = new GameObject("Current Asset").transform;
            _current.SetParent(transform, false);
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(_current, false);
            visual.localRotation = Quaternion.Euler(0f, _direction == "right-wall" ? 90f : 0f, 0f);
            BuildRecognizablePreview(visual, asset);
            ContactShadow.Apply(_current, asset.Shadow, asset.Sorting.Order - 1);
            BuildDiagnostics(_current, asset);
        }

        private static void BuildRecognizablePreview(Transform parent, AssetDefinition asset)
        {
            Vector2 footprint = asset.Footprint.Size;
            Color accent = ColorFor(asset.Id);
            float width = Mathf.Clamp(footprint.x * .72f, .7f, 4.2f);
            float depth = Mathf.Clamp(footprint.y * .62f, .55f, 2.6f);
            if (asset.Id.Contains("plant"))
            {
                Primitive(parent, "Pot", PrimitiveType.Cylinder, new Vector3(0f, .35f, 0f), new Vector3(.7f, .35f, .7f), new Color(.55f, .3f, .18f));
                Primitive(parent, "Leaves", PrimitiveType.Sphere, new Vector3(0f, 1.25f, 0f), new Vector3(1.15f, 1.55f, 1.15f), accent);
                return;
            }
            if (asset.Id.Contains("shelf"))
            {
                Primitive(parent, "Shelf", PrimitiveType.Cube, new Vector3(0f, 1.4f, .2f), new Vector3(width, .22f, depth), accent);
                for (int i = -1; i <= 1; i++)
                    Primitive(parent, "Bottle " + i, PrimitiveType.Cylinder, new Vector3(i * .65f, 1.9f, .15f), new Vector3(.25f, .55f, .25f), i == 0 ? Color.magenta : Color.cyan);
                return;
            }
            if (asset.Id.Contains("sofa"))
            {
                Primitive(parent, "Seat", PrimitiveType.Cube, new Vector3(0f, .55f, 0f), new Vector3(width, .6f, depth), accent);
                Primitive(parent, "Back", PrimitiveType.Cube, new Vector3(0f, 1.25f, depth * .38f), new Vector3(width, 1.05f, .25f), ColorFor("wood"));
                return;
            }
            if (asset.Id.Contains("counter"))
            {
                Primitive(parent, "Counter", PrimitiveType.Cube, new Vector3(0f, .9f, 0f), new Vector3(width, 1.8f, depth), ColorFor("wood"));
                Primitive(parent, "Counter Top", PrimitiveType.Cube, new Vector3(0f, 1.9f, 0f), new Vector3(width + .25f, .22f, depth + .2f), Color.white);
                Primitive(parent, "Register", PrimitiveType.Cube, new Vector3(-.55f, 2.3f, -.2f), new Vector3(1.0f, .7f, .7f), accent);
                return;
            }
            Primitive(parent, "Base", PrimitiveType.Cylinder, new Vector3(0f, .18f, 0f), new Vector3(width * .42f, .18f, depth * .42f), new Color(.18f, .2f, .22f));
            Primitive(parent, "Seat", PrimitiveType.Cube, new Vector3(0f, .65f, -.15f), new Vector3(Mathf.Min(width, 1.4f), .3f, Mathf.Min(depth, 1.2f)), accent);
            Primitive(parent, "Back", PrimitiveType.Cube, new Vector3(0f, 1.35f, .25f), new Vector3(Mathf.Min(width, 1.5f), 1.25f, .24f), accent * .8f);
            if (asset.Id.Contains("cut-station"))
                Primitive(parent, "Mirror", PrimitiveType.Cube, new Vector3(0f, 1.85f, 1.05f), new Vector3(1.55f, 1.55f, .09f), new Color(.65f, .85f, .9f));
            if (asset.Id.Contains("wash"))
                Primitive(parent, "Basin", PrimitiveType.Cylinder, new Vector3(0f, 1.15f, .9f), new Vector3(1.25f, .24f, 1.25f), Color.white);
            if (asset.Id.Contains("perm"))
                Primitive(parent, "Processor", PrimitiveType.Sphere, new Vector3(0f, 2.15f, .15f), new Vector3(1.5f, .65f, 1.5f), new Color(.65f, .4f, .75f));
        }

        private static void BuildDiagnostics(Transform parent, AssetDefinition asset)
        {
            Transform pivot = new GameObject("Pivot").transform;
            pivot.SetParent(parent, false);
            Primitive(pivot, "Pivot Marker", PrimitiveType.Sphere, new Vector3(0f, .08f, 0f), Vector3.one * .18f, Color.yellow);
            Transform footprint = new GameObject("Footprint").transform;
            footprint.SetParent(parent, false);
            DiagnosticArea(footprint, asset.Footprint, new Color(.05f, .65f, 1f, .28f), .025f);
            Transform collision = new GameObject("Collision").transform;
            collision.SetParent(parent, false);
            DiagnosticArea(collision, asset.Collision, new Color(1f, .15f, .15f, .34f), .055f);
            Transform anchors = new GameObject("Anchors").transform;
            anchors.SetParent(parent, false);
            foreach (AssetAnchor anchor in asset.InteractionAnchors)
                Primitive(anchors, anchor.Id, PrimitiveType.Sphere, anchor.Position, Vector3.one * .16f, new Color(.2f, 1f, .35f));
        }

        private static void DiagnosticArea(Transform parent, AssetArea area, Color color, float y)
        {
            GameObject target = Primitive(parent, "Area", PrimitiveType.Cube,
                new Vector3(area.Center.x, y, area.Center.y), new Vector3(area.Size.x, .04f, area.Size.y), color);
            target.GetComponent<Collider>().enabled = false;
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject target = GameObject.CreatePrimitive(type);
            target.name = name;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            target.transform.localScale = scale;
            Renderer renderer = target.GetComponent<Renderer>();
            Shader shader = color.a < .999f
                ? Resources.Load<Shader>("SalonContactShadow")
                : Resources.Load<Shader>("SalonLowPoly") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            return target;
        }

        private static Color ColorFor(string id)
        {
            if (id.Contains("wood") || id.Contains("counter") || id.Contains("sofa")) return new Color(.48f, .27f, .16f);
            if (id.Contains("wash")) return new Color(.1f, .58f, .64f);
            if (id.Contains("perm")) return new Color(.63f, .37f, .72f);
            if (id.Contains("plant")) return new Color(.26f, .68f, .34f);
            return new Color(.12f, .53f, .62f);
        }

        private static string FirstDirection(AssetDefinition asset)
            => asset.Directions != null && asset.Directions.Count > 0 ? asset.Directions[0] : "free";

        private void OnGUI()
        {
            if (AssetCount == 0) return;
            _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = new Color(.92f, .94f, .94f) } };
            GUI.Box(new Rect(18f, 18f, 470f, 184f), string.Empty);
            GUI.Label(new Rect(36f, 30f, 430f, 34f), "ASSET TEST LAB", _titleStyle);
            AssetDefinition asset = _manifest.Assets[_index];
            GUI.Label(new Rect(36f, 68f, 430f, 24f), $"{_index + 1}/{AssetCount}  {asset.Id}", _bodyStyle);
            GUI.Label(new Rect(36f, 94f, 430f, 24f), $"status: {asset.Status}   direction: {_direction}", _bodyStyle);
            GUI.Label(new Rect(36f, 120f, 430f, 24f), "yellow=pivot  blue=footprint  red=collision  green=anchors", _bodyStyle);
            if (GUI.Button(new Rect(36f, 154f, 100f, 32f), "PREV")) PreviousAsset();
            if (GUI.Button(new Rect(144f, 154f, 100f, 32f), "NEXT")) NextAsset();
            if (GUI.Button(new Rect(252f, 154f, 190f, 32f), "BACK / RIGHT WALL")) ToggleDirection();
        }
    }
}
