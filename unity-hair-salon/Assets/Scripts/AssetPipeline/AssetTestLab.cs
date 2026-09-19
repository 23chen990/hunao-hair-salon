using System;
using System.Globalization;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    public sealed class AssetTestLab : MonoBehaviour
    {
        private AssetManifest _manifest;
        private int _index;
        private string _direction = "back-wall";
        private Transform _current;
        private bool _currentUsesRealArtwork;
        private bool _currentIsModel;
        private string _diagnosticMode = "all";
        private string _viewMode = "inspect";
        private bool _showUi = true;
        private Camera _camera;
        private Transform _inspectStage;
        private Transform _contextReferences;
        private Texture2D _pixelPreviewTexture;
        private Vector2 _currentWorldSize;
        private float _contextScaleMultiplier = 1f;
        private float _currentInspectOccupancy;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        public int AssetCount => _manifest?.Assets?.Count ?? 0;
        public string CurrentAssetId => AssetCount == 0 ? string.Empty : _manifest.Assets[_index].Id;
        public string CurrentDirection => _direction;
        public string CurrentViewMode => _viewMode;
        public float CurrentInspectOccupancy => _currentInspectOccupancy;
        public float CurrentCameraOrthographicSize => _camera != null ? _camera.orthographicSize : 0f;
        public Vector2 CurrentWorldSize => _currentWorldSize;
        public float CurrentContextScaleMultiplier => _contextScaleMultiplier;
        public Texture2D PixelPreviewTexture => _pixelPreviewTexture;
        public float PixelPreviewScale => 1f;

        private void Start()
        {
            if (_manifest == null)
            {
                string diagnostics = QueryValue(Application.absoluteURL, "diagnostics");
                if (!string.IsNullOrWhiteSpace(diagnostics)) _diagnosticMode = diagnostics;
                string view = QueryValue(Application.absoluteURL, "view");
                if (IsViewMode(view)) _viewMode = view;
                if (float.TryParse(QueryValue(Application.absoluteURL, "contextScale"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float contextScale) && IsValidContextScale(contextScale))
                    _contextScaleMultiplier = contextScale;
                _showUi = QueryValue(Application.absoluteURL, "ui") != "0";
                Initialize(AssetManifestLoader.LoadFromResources());
                string requested = QueryValue(Application.absoluteURL, "assetId");
                string orientation = QueryValue(Application.absoluteURL, "orientation");
                if (!string.IsNullOrWhiteSpace(requested)) SelectAssetWithOrientation(requested, orientation);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) NextAsset();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousAsset();
            if (Input.GetKeyDown(KeyCode.Space)) ToggleDirection();
            if (Input.GetKeyDown(KeyCode.I)) SetViewMode("inspect");
            if (Input.GetKeyDown(KeyCode.C)) SetViewMode("context");
            if (Input.GetKeyDown(KeyCode.P)) SetViewMode("pixel");
        }

        public void Initialize(AssetManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            if (AssetManifestValidator.Validate(_manifest).Count > 0)
                throw new InvalidOperationException("Asset test lab cannot open an invalid manifest.");
            _index = 0;
            _direction = FirstDirection(_manifest.Assets[_index]);
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

        public bool SelectAsset(string assetId) => SelectAssetWithOrientation(assetId, null);

        public void SetDiagnosticMode(string mode)
        {
            _diagnosticMode = mode == "preview" || mode == "anchors" || mode == "areas" ? mode : "all";
            ApplyDiagnosticMode();
        }

        public bool SetViewMode(string mode)
        {
            if (!IsViewMode(mode)) return false;
            bool scaleApplicabilityChanged = (_viewMode == "context") != (mode == "context") &&
                                             !Mathf.Approximately(_contextScaleMultiplier, 1f);
            _viewMode = mode;
            if (scaleApplicabilityChanged) RebuildCurrent();
            else ApplyViewMode();
            return true;
        }

        public bool SetContextScaleMultiplier(float multiplier)
        {
            if (_viewMode != "context" || !IsValidContextScale(multiplier)) return false;
            _contextScaleMultiplier = multiplier;
            RebuildCurrent();
            return true;
        }

        private static bool IsValidContextScale(float multiplier)
            => !float.IsNaN(multiplier) && !float.IsInfinity(multiplier) && multiplier >= .25f && multiplier <= 2f;

        private static bool IsViewMode(string mode)
            => mode == "inspect" || mode == "context" || mode == "pixel";

        private bool SelectAssetWithOrientation(string assetId, string orientation)
        {
            if (AssetCount == 0 || string.IsNullOrWhiteSpace(assetId)) return false;
            int requestedIndex = _manifest.Assets.FindIndex(asset => asset != null &&
                string.Equals(asset.Id, assetId, StringComparison.Ordinal));
            if (requestedIndex < 0) return false;
            _index = requestedIndex;
            AssetDefinition asset = _manifest.Assets[_index];
            _direction = !string.IsNullOrWhiteSpace(orientation) && asset.Directions.Contains(orientation)
                ? orientation : FirstDirection(asset);
            RebuildCurrent();
            return true;
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
            _inspectStage = new GameObject("Inspect Stage").transform;
            _inspectStage.SetParent(environment, false);
            GameObject floor = Primitive(_inspectStage, "Floor", PrimitiveType.Cube, new Vector3(0f, -.12f, .6f),
                new Vector3(7.5f, .2f, 4.2f), new Color(.91f, .87f, .78f));
            floor.GetComponent<Collider>().enabled = false;

            _contextReferences = new GameObject("Context References").transform;
            _contextReferences.SetParent(environment, false);
            BuildContextReferences(_contextReferences);

            var cameraObject = new GameObject("Asset Lab Camera", typeof(Camera));
            cameraObject.transform.SetParent(environment, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 3.8f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(.11f, .13f, .14f);
            cameraObject.tag = "MainCamera";

            var lightObject = new GameObject("Key Light", typeof(Light));
            lightObject.transform.SetParent(environment, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
        }

        private static void BuildContextReferences(Transform parent)
        {
            for (int x = -4; x <= 4; x++)
            for (int z = -2; z <= 2; z++)
            {
                Color tile = ((x + z) & 1) == 0 ? new Color(.10f, .55f, .55f) : new Color(.09f, .50f, .52f);
                GameObject target = Primitive(parent, $"Grid Tile {x} {z}", PrimitiveType.Cube,
                    new Vector3(x, -.08f, z), new Vector3(.97f, .12f, .97f), tile);
                target.GetComponent<Collider>().enabled = false;
            }
            GameObject prefab = Resources.Load<GameObject>("Characters/Hairdresser");
            if (prefab == null) throw new MissingReferenceException("Context scale reference is missing: Characters/Hairdresser");
            GameObject player = Instantiate(prefab, parent);
            player.name = "Scale Reference Hairdresser";
            player.transform.localPosition = new Vector3(-2.15f, .05f, -.25f);
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
            _currentWorldSize = asset.WorldSize * (_viewMode == "context" ? _contextScaleMultiplier : 1f);
            _pixelPreviewTexture = null;
            _currentIsModel = asset.ImportProfile == "authored-3d";
            _current = new GameObject("Current Asset").transform;
            _current.SetParent(transform, false);
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(_current, false);
            _currentUsesRealArtwork = BuildRealArtwork(visual, asset, _direction, _currentWorldSize);
            if (!_currentUsesRealArtwork)
            {
                visual.localRotation = Quaternion.Euler(0f, _direction == "right-wall" ? 90f : 0f, 0f);
                BuildRecognizablePreview(visual, asset);
            }
            if (asset.Shadow != null && asset.Shadow.UsesProceduralShadow)
                ContactShadow.Apply(_current, asset.Shadow, asset.Sorting.Order - 1);
            BuildDiagnostics(_current, asset);
            ApplyDiagnosticMode();
            ApplyViewMode();
            string runtimeSize = _pixelPreviewTexture != null ? $"{_pixelPreviewTexture.width}x{_pixelPreviewTexture.height}" : "procedural";
            Debug.Log($"[ASSET_LAB_READY] id={asset.Id} direction={_direction} status={asset.Status} realArtwork={_currentUsesRealArtwork} view={_viewMode} contextScale={_contextScaleMultiplier:F3} runtimeTexture={runtimeSize} worldSize={_currentWorldSize.x:F3}x{_currentWorldSize.y:F3}");
        }

        private void ApplyViewMode()
        {
            if (_camera == null || _current == null) return;
            bool pixel = _viewMode == "pixel";
            _camera.enabled = !pixel;
            _current.gameObject.SetActive(!pixel);
            if (_inspectStage != null) _inspectStage.gameObject.SetActive(_viewMode == "inspect");
            if (_contextReferences != null) _contextReferences.gameObject.SetActive(_viewMode == "context");
            if (pixel) return;

            if (_viewMode == "context")
            {
                _current.position = new Vector3(1.45f, 0f, 0f);
                _camera.transform.position = new Vector3(0f, 9f, -9.5f);
                _camera.transform.LookAt(new Vector3(0f, .8f, .4f));
                _camera.orthographicSize = 4.25f;
                _currentInspectOccupancy = 0f;
            }
            else
            {
                _current.position = Vector3.zero;
                float centerY = _currentWorldSize.y * .5f;
                _camera.transform.position = new Vector3(0f, centerY + .75f, -10f);
                _camera.transform.LookAt(new Vector3(0f, centerY, 0f));
                float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
                _camera.orthographicSize = CalculateInspectOrthographicSize(_currentWorldSize, aspect, .60f);
                _currentInspectOccupancy = CalculateOccupancy(_currentWorldSize, aspect, _camera.orthographicSize);
            }
            if (_currentIsModel && _viewMode != "context")
            {
                _viewMode = "inspect"; // Pixel inspection applies only to raster artwork.
                Renderer[] parts = _current.Find("Visual/Model").GetComponentsInChildren<Renderer>();
                Bounds bounds = parts[0].bounds;
                foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
                _camera.transform.position = bounds.center + new Vector3(-6f, 6f, -8f);
                _camera.transform.LookAt(bounds.center);
                float halfHeight = 0f;
                float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(corner, _camera.transform.up)),
                        Mathf.Abs(Vector3.Dot(corner, _camera.transform.right)) / aspect);
                }
                _camera.orthographicSize = Mathf.Max(.5f, halfHeight / .66f);
                _currentInspectOccupancy = .66f;
            }
            FaceArtworkToCamera();
        }

        public static float CalculateInspectOrthographicSize(Vector2 worldSize, float aspect, float fill)
        {
            float safeAspect = Mathf.Max(.25f, aspect);
            float safeFill = Mathf.Clamp(fill, .45f, .70f);
            float vertical = worldSize.y / (2f * safeFill);
            float horizontal = worldSize.x / (2f * safeAspect * safeFill);
            return Mathf.Max(.5f, vertical, horizontal);
        }

        private static float CalculateOccupancy(Vector2 worldSize, float aspect, float orthographicSize)
        {
            float vertical = worldSize.y / (2f * orthographicSize);
            float horizontal = worldSize.x / (2f * orthographicSize * Mathf.Max(.25f, aspect));
            return Mathf.Max(vertical, horizontal);
        }

        private void FaceArtworkToCamera()
        {
            Transform artwork = _current?.Find("Visual/Artwork");
            if (artwork != null && _camera != null) artwork.rotation = _camera.transform.rotation;
        }

        private void ApplyDiagnosticMode()
        {
            if (_current == null) return;
            bool preview = _diagnosticMode == "preview";
            bool anchors = _diagnosticMode == "anchors";
            bool areas = _diagnosticMode == "areas";
            SetChild("Pivot", !preview);
            SetChild("Anchors", anchors || _diagnosticMode == "all");
            SetChild("Footprint", areas || _diagnosticMode == "all");
            SetChild("Collision", areas || _diagnosticMode == "all");
            SetChild("Shadow Range", areas || _diagnosticMode == "all");
            SetChild("Sorting Baseline", !preview);
        }

        private void SetChild(string name, bool active)
        {
            Transform child = _current.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static string QueryValue(string url, string key)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)) return string.Empty;
            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart + 1 >= url.Length) return string.Empty;
            string[] pairs = url.Substring(queryStart + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(parts[1]);
            }
            return string.Empty;
        }

        private bool BuildRealArtwork(Transform parent, AssetDefinition asset, string direction, Vector2 worldSize)
        {
            string resourcePath = asset.ResourceFor(direction);
            if (string.IsNullOrWhiteSpace(resourcePath) || resourcePath.StartsWith("builtin://", StringComparison.Ordinal)) return false;
            if (asset.ImportProfile == "authored-3d")
            {
                var model = Resources.Load<GameObject>(resourcePath);
                if (model == null) throw new MissingReferenceException("Authored model is missing: " + resourcePath);
                var instance = Instantiate(model, parent);
                instance.name = "Model";
                instance.transform.localRotation = Quaternion.Euler(0f, direction == "right-wall" ? 90f : 0f, 0f);
                WashCraftIntegration.ApplyLighting();
                return true;
            }
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) throw new MissingReferenceException($"Asset artwork is missing: {resourcePath}");
            _pixelPreviewTexture = texture;
            float width = worldSize.x > .01f ? worldSize.x : Mathf.Max(.5f, asset.Footprint.Size.x);
            float pixelsPerUnit = texture.width / width;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), asset.Pivot, pixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            sprite.name = asset.Id + " [" + direction + "]";
            var artwork = new GameObject("Artwork", typeof(SpriteRenderer));
            artwork.transform.SetParent(parent, false);
            artwork.transform.localPosition = new Vector3(0f, 0f, asset.Sorting?.DepthOffset ?? 0f);
            SpriteRenderer renderer = artwork.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = string.IsNullOrWhiteSpace(asset.Sorting?.Layer) ? "Default" : asset.Sorting.Layer;
            renderer.sortingOrder = asset.Sorting?.Order ?? 0;
            return true;
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
            Transform shadow = new GameObject("Shadow Range").transform;
            shadow.SetParent(parent, false);
            if (asset.Shadow != null)
                DiagnosticArea(shadow, new AssetArea { Center = asset.Shadow.Offset, Size = asset.Shadow.Size },
                    new Color(.72f, .42f, 1f, .18f), .015f);
            Transform baseline = new GameObject("Sorting Baseline").transform;
            baseline.SetParent(parent, false);
            Primitive(baseline, "Baseline", PrimitiveType.Cube, new Vector3(0f, .035f, 0f),
                new Vector3(Mathf.Max(.6f, asset.Footprint.Size.x), .025f, .055f), new Color(1f, .82f, .1f, .75f));
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
            if (_viewMode == "pixel" && _pixelPreviewTexture != null) DrawPixelPreview(_pixelPreviewTexture);
            if (!_showUi) return;
            _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(.92f, .94f, .94f) } };
            float panelY = Mathf.Max(8f, Screen.height - 118f);
            GUI.Box(new Rect(12f, panelY, Mathf.Min(820f, Screen.width - 24f), 106f), string.Empty);
            GUI.Label(new Rect(24f, panelY + 7f, 220f, 26f), "ASSET TEST LAB", _titleStyle);
            AssetDefinition asset = _manifest.Assets[_index];
            GUI.Label(new Rect(24f, panelY + 31f, 420f, 21f), $"{_index + 1}/{AssetCount}  {asset.Id}", _bodyStyle);
            string shadowMode = string.IsNullOrWhiteSpace(asset.Shadow?.Mode)
                ? (asset.Shadow?.Enabled == true ? "procedural" : "none") : asset.Shadow.Mode;
            GUI.Label(new Rect(24f, panelY + 52f, 500f, 20f),
                $"view: {_viewMode}  status: {asset.Status}  direction: {_direction}  shadow: {shadowMode}  world: {_currentWorldSize.x:F2}×{_currentWorldSize.y:F2}  scale: {_contextScaleMultiplier:F2}×", _bodyStyle);
            if (GUI.Button(new Rect(24f, panelY + 75f, 70f, 25f), "PREV")) PreviousAsset();
            if (GUI.Button(new Rect(100f, panelY + 75f, 70f, 25f), "NEXT")) NextAsset();
            if (GUI.Button(new Rect(180f, panelY + 75f, 80f, 25f), "INSPECT")) SetViewMode("inspect");
            if (GUI.Button(new Rect(266f, panelY + 75f, 80f, 25f), "CONTEXT")) SetViewMode("context");
            GUI.enabled = !_currentIsModel;
            if (GUI.Button(new Rect(352f, panelY + 75f, 80f, 25f), "100% PIXEL")) SetViewMode("pixel");
            GUI.enabled = true;
            GUI.enabled = asset.Directions.Contains("back-wall") && asset.Directions.Contains("right-wall");
            if (GUI.Button(new Rect(438f, panelY + 75f, 150f, 25f), "BACK / RIGHT WALL")) ToggleDirection();
            GUI.enabled = true;
        }

        private static void DrawPixelPreview(Texture2D texture)
        {
            Color previous = GUI.color;
            for (int y = 0; y < Screen.height; y += 32)
            for (int x = 0; x < Screen.width; x += 32)
            {
                GUI.color = ((x / 32 + y / 32) & 1) == 0 ? new Color(.72f, .72f, .72f) : new Color(.48f, .48f, .48f);
                GUI.DrawTexture(new Rect(x, y, 32f, 32f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            float xOffset = (Screen.width - texture.width) * .5f;
            float yOffset = (Screen.height - texture.height) * .5f;
            GUI.DrawTexture(new Rect(xOffset, yOffset, texture.width, texture.height), texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }
    }
}
