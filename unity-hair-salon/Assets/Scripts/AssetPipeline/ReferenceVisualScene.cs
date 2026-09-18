using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HairSalon.AssetPipeline
{
    /// <summary>Development-only visual calibration environment. It contains no SalonDemo or service state.</summary>
    public sealed class ReferenceVisualScene : MonoBehaviour
    {
        private static readonly Color WallMain = Hex("DDB89E");
        private static readonly Color WallTrim = Hex("8B5A3D");
        private static readonly Color WallTop = Hex("F1DDC4");
        private static readonly Color Plinth = Hex("D6A17F");

        private ReferenceVisualBaseline _baseline;
        private AssetManifest _manifest;
        private Transform _world;
        private Camera _camera;
        private GameObject _player;
        private Transform _wash;
        private Transform _ordinary;
        private Transform _lowDensity;
        private Transform _targetDensity;
        private Transform _technicalGrid;
        private Canvas _overlayCanvas;
        private RawImage _overlayImage;
        private bool _built;
        private bool _debug;
        private bool _overlayEnabled;
        private bool _sideBySide;
        private float _overlayOpacity;
        private string _densityState;
        private GUIStyle _debugStyle;

        [Serializable]
        private sealed class ScreenMetricSnapshot
        {
            public float playerScreenHeightRatio;
            public float washToPlayerHeightRatio;
            public float washFootprintToVisibleFloorRatio;
            public float roomScreenWidthRatio;
            public float roomScreenHeightRatio;
            public int visibleTileColumns;
            public string densityState;
        }

        private void Start()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
                throw new InvalidOperationException("ReferenceVisualScene is development-only.");
            _debug = QueryEnabled(Application.absoluteURL, "debug");
            _overlayEnabled = QueryEnabled(Application.absoluteURL, "overlay");
            _sideBySide = QueryEnabled(Application.absoluteURL, "sideBySide");
            _densityState = QueryValue(Application.absoluteURL, "density", "target-density");
            BuildNow();
            StartCoroutine(LogReadyAfterFrame());
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            _baseline = ReferenceVisualBaselineLoader.Load();
            _manifest = AssetManifestLoader.LoadFromResources();
            if (string.IsNullOrWhiteSpace(_densityState)) _densityState = _baseline.Density.DefaultState;
            _overlayOpacity = Mathf.Clamp01(QueryFloat(
                Application.absoluteURL, "overlayOpacity", _baseline.Overlay.DefaultOpacity));

            _world = new GameObject("Reference World").transform;
            _world.SetParent(transform, false);
            BuildCameraAndLight();
            BuildGround();
            BuildWalls();
            BuildApprovedPlayer(_world, "Approved Player", _baseline.Player.Position, true);
            BuildRealAssets();
            BuildDensityStates();
            BuildOverlayCalibration();
            ApplyDensityState();
            ApplyCalibrationDisplay();
        }

        private void BuildCameraAndLight()
        {
            var cameraObject = new GameObject("Reference Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = _baseline.Camera.OrthographicSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Hex("44505A");
            Quaternion rotation = Quaternion.Euler(_baseline.Camera.PitchDegrees, -_baseline.Camera.YawDegrees, 0f);
            Vector3 forward = rotation * Vector3.forward;
            _camera.transform.position = _baseline.Camera.Target - forward * 28f;
            _camera.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            cameraObject.tag = "MainCamera";

            var key = new GameObject("Warm Reference Key", typeof(Light)).GetComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, .84f, .68f);
            key.intensity = 1.18f;
            key.transform.rotation = Quaternion.Euler(54f, -38f, 0f);
            var fill = new GameObject("Reference Fill", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional;
            fill.color = new Color(.66f, .75f, 1f);
            fill.intensity = .28f;
            fill.transform.rotation = Quaternion.Euler(58f, 140f, 0f);
        }

        private void BuildGround()
        {
            Transform ground = new GameObject("Ground").transform;
            ground.SetParent(_world, false);
            float width = _baseline.Ground.WorldWidth;
            float depth = _baseline.Ground.WorldDepth;
            Block(ground, "Warm Stone Plinth", new Vector3(0f, -.16f, 0f),
                new Vector3(width + .35f, .24f, depth + .35f), Plinth);

            var floor = new GameObject("Visual Floor", typeof(MeshFilter), typeof(MeshRenderer));
            floor.transform.SetParent(ground, false);
            floor.GetComponent<MeshFilter>().sharedMesh = CreateFloorMesh(width, depth);
            Shader shader = Resources.Load<Shader>("SalonReferenceFloor");
            if (shader == null) throw new MissingReferenceException("SalonReferenceFloor shader is missing.");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetColor("_FloorColor", _baseline.Ground.FloorColor);
            material.SetColor("_GroutColor", _baseline.Ground.GroutColor);
            material.SetVector("_TileCount", new Vector4(
                _baseline.Ground.VisibleTileColumns, _baseline.Ground.VisibleTileRows, 0f, 0f));
            material.SetFloat("_GroutWidth", .035f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;

            _technicalGrid = new GameObject("Technical Grid Debug").transform;
            _technicalGrid.SetParent(ground, false);
            BuildTechnicalGrid(_technicalGrid, width, depth, 14, 9);
            _technicalGrid.gameObject.SetActive(_debug);
        }

        private void BuildWalls()
        {
            float width = _baseline.Ground.WorldWidth;
            float depth = _baseline.Ground.WorldDepth;
            float height = _baseline.Walls.VisualHeight;
            float thickness = _baseline.Walls.VisualThickness;

            Transform visual = new GameObject("Visual Walls").transform;
            visual.SetParent(_world, false);
            Block(visual, "Back Wall Visual", new Vector3(0f, height * .5f, depth * .5f),
                new Vector3(width, height, thickness), WallMain);
            Block(visual, "Right Wall Visual", new Vector3(width * .5f, height * .5f, 0f),
                new Vector3(thickness, height, depth), WallMain);
            Block(visual, "Back Wall Lower Trim", new Vector3(0f, _baseline.Walls.LowerTrimHeight * .5f,
                    depth * .5f - .02f),
                new Vector3(width, _baseline.Walls.LowerTrimHeight, .08f), WallTrim);
            Block(visual, "Right Wall Lower Trim", new Vector3(width * .5f - .02f,
                    _baseline.Walls.LowerTrimHeight * .5f, 0f),
                new Vector3(.08f, _baseline.Walls.LowerTrimHeight, depth), WallTrim);
            Block(visual, "Back Wall Thin Cap", new Vector3(0f, height + .045f, depth * .5f),
                new Vector3(width + .12f, .09f, thickness + .08f), WallTop);
            Block(visual, "Right Wall Thin Cap", new Vector3(width * .5f, height + .045f, 0f),
                new Vector3(thickness + .08f, .09f, depth + .12f), WallTop);

            Transform collision = new GameObject("Collision Walls").transform;
            collision.SetParent(_world, false);
            CollisionWall(collision, "Back Collision Wall", new Vector3(0f,
                    _baseline.Walls.CollisionHeight * .5f, depth * .5f),
                new Vector3(width, _baseline.Walls.CollisionHeight, _baseline.Walls.CollisionThickness));
            CollisionWall(collision, "Right Collision Wall", new Vector3(width * .5f,
                    _baseline.Walls.CollisionHeight * .5f, 0f),
                new Vector3(_baseline.Walls.CollisionThickness, _baseline.Walls.CollisionHeight, depth));
        }

        private void BuildRealAssets()
        {
            AssetDefinition ordinary = RequireAsset("furniture-magazine-rack-wood");
            AssetDefinition washAsset = RequireAsset("furniture-wash-station-vintage-right-wall");
            if (washAsset.Status != "NEEDS-REVIEW")
                throw new InvalidOperationException("Wash station must remain NEEDS-REVIEW.");

            _ordinary = new GameObject("Real Ordinary Furniture").transform;
            _ordinary.SetParent(_world, false);
            _ordinary.position = new Vector3(-7.15f, 0f, -3.7f);
            CandidateAssetValidation.BuildArtwork(_ordinary, ordinary, ordinary.DefaultOrientation);

            _wash = new GameObject("Wash Station NEEDS-REVIEW").transform;
            _wash.SetParent(_world, false);
            _wash.position = new Vector3(4.2f, 0f, .35f);
            CandidateAssetValidation.BuildArtwork(_wash, washAsset, washAsset.DefaultOrientation);
        }

        private void BuildDensityStates()
        {
            _lowDensity = new GameObject("Low Density Context").transform;
            _lowDensity.SetParent(_world, false);
            _targetDensity = new GameObject("Target Density Context").transform;
            _targetDensity.SetParent(_world, false);

            AssetDefinition ordinary = RequireAsset("furniture-magazine-rack-wood");
            AssetDefinition washAsset = RequireAsset("furniture-wash-station-vintage-right-wall");
            Vector3[] washPositions =
            {
                new Vector3(-4.2f, 0f, .35f),
                new Vector3(0f, 0f, .35f)
            };
            for (int index = 0; index < washPositions.Length; index++)
                BuildRealAssetInstance(_targetDensity, washAsset, "Real Wash Density " + (index + 1), washPositions[index]);

            Vector3[] rackPositions =
            {
                new Vector3(-7.6f, 0f, 3.75f),
                new Vector3(1.8f, 0f, 3.8f),
                new Vector3(7.65f, 0f, 3.6f),
                new Vector3(7.65f, 0f, .1f),
                new Vector3(7.65f, 0f, -3.8f)
            };
            for (int index = 0; index < rackPositions.Length; index++)
                BuildRealAssetInstance(_targetDensity, ordinary, "Real Furniture Density " + (index + 1), rackPositions[index]);

            BuildApprovedPlayer(_targetDensity, "Approved Activity Player A", new Vector3(-4.5f, .05f, -1.65f), false);
            BuildApprovedPlayer(_targetDensity, "Approved Activity Player B", new Vector3(1.1f, .05f, -1.85f), false);
            BuildApprovedPlayer(_targetDensity, "Approved Activity Player C", new Vector3(5.7f, .05f, -2.7f), false);
        }

        private GameObject BuildApprovedPlayer(Transform parent, string name, Vector3 position, bool primary)
        {
            GameObject prefab = Resources.Load<GameObject>("Characters/Hairdresser");
            if (prefab == null) throw new MissingReferenceException("Approved player prefab is missing.");
            GameObject player = Instantiate(prefab, parent);
            player.name = name;
            player.transform.position = position;
            player.transform.localScale *= _baseline.Player.VisualScale;
            Transform visual = player.transform.Find("Visual/HairdresserSpriteVisual");
            if (visual == null) visual = player.transform.Find("HairdresserSpriteVisual");
            if (visual != null) visual.rotation = _camera.transform.rotation;
            if (primary) _player = player;
            return player;
        }

        private static void BuildRealAssetInstance(Transform parent, AssetDefinition asset, string name, Vector3 position)
        {
            Transform instance = new GameObject(name).transform;
            instance.SetParent(parent, false);
            instance.position = position;
            CandidateAssetValidation.BuildArtwork(instance, asset, asset.DefaultOrientation);
        }

        private void BuildOverlayCalibration()
        {
            Texture2D reference = Resources.Load<Texture2D>(_baseline.Overlay.ResourcePath);
            if (reference == null) throw new MissingReferenceException("Approved overlay reference is missing.");
            var canvasObject = new GameObject("Overlay Calibration Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _overlayCanvas = canvasObject.GetComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 2000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _baseline.Camera.Viewport;
            scaler.matchWidthOrHeight = .5f;

            var imageObject = new GameObject("Approved Reference Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _overlayImage = imageObject.GetComponent<RawImage>();
            _overlayImage.texture = reference;
            _overlayImage.raycastTarget = false;
            _overlayImage.uvRect = CoverCenterUv(reference.width, reference.height,
                _baseline.Camera.Viewport.x, _baseline.Camera.Viewport.y);
        }

        private void ApplyDensityState()
        {
            bool target = !string.Equals(_densityState, "low-density", StringComparison.OrdinalIgnoreCase);
            _densityState = target ? "target-density" : "low-density";
            _lowDensity.gameObject.SetActive(!target);
            _targetDensity.gameObject.SetActive(target);
        }

        private void ApplyCalibrationDisplay()
        {
            if (_overlayImage == null || _camera == null) return;
            _camera.rect = _sideBySide ? new Rect(.5f, 0f, .5f, 1f) : new Rect(0f, 0f, 1f, 1f);
            RectTransform rect = _overlayImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = _sideBySide ? new Vector2(.5f, 1f) : Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            float alpha = _sideBySide ? 1f : _overlayEnabled ? _overlayOpacity : 0f;
            _overlayImage.color = new Color(1f, 1f, 1f, alpha);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.O)) _overlayEnabled = !_overlayEnabled;
            if (Input.GetKeyDown(KeyCode.S)) _sideBySide = !_sideBySide;
            if (Input.GetKeyDown(KeyCode.Minus)) _overlayOpacity = Mathf.Clamp01(_overlayOpacity - .1f);
            if (Input.GetKeyDown(KeyCode.Equals)) _overlayOpacity = Mathf.Clamp01(_overlayOpacity + .1f);
            ApplyCalibrationDisplay();
        }

        private IEnumerator LogReadyAfterFrame()
        {
            yield return new WaitForEndOfFrame();
            ScreenMetricSnapshot metrics = CaptureScreenMetrics();
            string json = JsonUtility.ToJson(metrics);
            Debug.Log($"[REFERENCE_VISUAL_READY] reference={_baseline.ApprovedReferencePath} " +
                      $"camera=orthographic yaw={_baseline.Camera.YawDegrees:F1} pitch={_baseline.Camera.PitchDegrees:F1} " +
                      $"ortho={_baseline.Camera.OrthographicSize:F2} viewport={Screen.width}x{Screen.height} " +
                      $"washStatus=NEEDS-REVIEW density={_densityState}");
            Debug.Log($"[OVERLAY_CALIBRATION_READY] overlay={_overlayEnabled} overlayOpacity={_overlayOpacity:F2} " +
                      $"sideBySide={_sideBySide} density={_densityState} screenMetrics={json}");
        }

        private ScreenMetricSnapshot CaptureScreenMetrics()
        {
            Transform playerVisual = _player.transform.Find("Visual/HairdresserSpriteVisual");
            if (playerVisual == null) playerVisual = _player.transform.Find("HairdresserSpriteVisual");
            Renderer playerRenderer = playerVisual != null ? playerVisual.GetComponent<Renderer>() : null;
            Renderer washRenderer = _wash.GetComponentInChildren<MeshRenderer>(true);
            Rect player = ScreenRect(playerRenderer);
            Rect wash = ScreenRect(washRenderer);
            float roomWidth;
            float roomHeight;
            ProjectedRoomRatios(out roomWidth, out roomHeight);
            AssetDefinition washAsset = RequireAsset("furniture-wash-station-vintage-right-wall");
            float floorArea = _baseline.Ground.WorldWidth * _baseline.Ground.WorldDepth;
            float washArea = washAsset.Footprint.Size.x * washAsset.Footprint.Size.y;
            return new ScreenMetricSnapshot
            {
                playerScreenHeightRatio = player.height / Mathf.Max(1f, Screen.height),
                washToPlayerHeightRatio = wash.height / Mathf.Max(1f, player.height),
                washFootprintToVisibleFloorRatio = washArea / floorArea,
                roomScreenWidthRatio = roomWidth,
                roomScreenHeightRatio = roomHeight,
                visibleTileColumns = _baseline.Ground.VisibleTileColumns,
                densityState = _densityState
            };
        }

        private void ProjectedRoomRatios(out float widthRatio, out float heightRatio)
        {
            float halfWidth = _baseline.Ground.WorldWidth * .5f;
            float halfDepth = _baseline.Ground.WorldDepth * .5f;
            Vector3[] points =
            {
                new Vector3(-halfWidth, 0f, -halfDepth), new Vector3(halfWidth, 0f, -halfDepth),
                new Vector3(-halfWidth, 0f, halfDepth), new Vector3(halfWidth, 0f, halfDepth)
            };
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector3 point in points)
            {
                Vector3 screen = _camera.WorldToScreenPoint(point);
                minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
            }
            widthRatio = (maxX - minX) / Mathf.Max(1f, Screen.width);
            heightRatio = (maxY - minY) / Mathf.Max(1f, Screen.height);
        }

        private Rect ScreenRect(Renderer renderer)
        {
            if (renderer == null) return Rect.zero;
            Bounds bounds = renderer.localBounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 local = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Vector3 world = renderer.transform.TransformPoint(local);
                Vector3 screen = _camera.WorldToScreenPoint(world);
                minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private AssetDefinition RequireAsset(string id)
        {
            AssetDefinition asset = _manifest.Find(id);
            if (asset == null) throw new MissingReferenceException("Reference asset missing: " + id);
            return asset;
        }

        private static Mesh CreateFloorMesh(float width, float depth)
        {
            var mesh = new Mesh { name = "Reference Continuous Floor", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-width * .5f, 0f, -depth * .5f), new Vector3(width * .5f, 0f, -depth * .5f),
                new Vector3(-width * .5f, 0f, depth * .5f), new Vector3(width * .5f, 0f, depth * .5f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Rect CoverCenterUv(int sourceWidth, int sourceHeight, int viewportWidth, int viewportHeight)
        {
            float sourceAspect = sourceWidth / (float)sourceHeight;
            float viewportAspect = viewportWidth / (float)viewportHeight;
            if (viewportAspect > sourceAspect)
            {
                float height = sourceAspect / viewportAspect;
                return new Rect(0f, (1f - height) * .5f, 1f, height);
            }
            float width = viewportAspect / sourceAspect;
            return new Rect((1f - width) * .5f, 0f, width, 1f);
        }

        private static void BuildTechnicalGrid(Transform parent, float width, float depth, int columns, int rows)
        {
            Color line = new Color(.15f, .9f, 1f, .32f);
            for (int index = 0; index <= columns; index++)
            {
                float x = -width * .5f + width * index / columns;
                Block(parent, "Engineering Grid X " + index, new Vector3(x, .025f, 0f),
                    new Vector3(.025f, .025f, depth), line);
            }
            for (int index = 0; index <= rows; index++)
            {
                float z = -depth * .5f + depth * index / rows;
                Block(parent, "Engineering Grid Z " + index, new Vector3(0f, .025f, z),
                    new Vector3(width, .025f, .025f), line);
            }
        }

        private static void CollisionWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider));
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.GetComponent<BoxCollider>().size = size;
        }

        private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            Collider collider = block.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Shader shader = Resources.Load<Shader>("SalonLowPoly");
            if (shader == null) throw new MissingReferenceException("SalonLowPoly shader is missing.");
            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
            return block;
        }

        private static bool QueryEnabled(string url, string key)
            => string.Equals(QueryValue(url, key, "0"), "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(QueryValue(url, key, "0"), "true", StringComparison.OrdinalIgnoreCase);

        private static string QueryValue(string url, string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(url)) return fallback;
            int question = url.IndexOf('?');
            if (question < 0 || question == url.Length - 1) return fallback;
            string[] pairs = url.Substring(question + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(parts[1]);
            }
            return fallback;
        }

        private static float QueryFloat(string url, string key, float fallback)
            => float.TryParse(QueryValue(url, key, fallback.ToString("0.###")),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;

        private void OnGUI()
        {
            if (!_debug || _baseline == null) return;
            _debugStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12f, 12f, 440f, 174f), string.Empty);
            GUI.Label(new Rect(24f, 22f, 410f, 22f), "OVERLAY CALIBRATION — DEVELOPMENT ONLY", _debugStyle);
            GUI.Label(new Rect(24f, 47f, 410f, 22f),
                $"camera: ortho {_baseline.Camera.OrthographicSize:F2} yaw {_baseline.Camera.YawDegrees:F0}° pitch {_baseline.Camera.PitchDegrees:F0}°", _debugStyle);
            GUI.Label(new Rect(24f, 70f, 410f, 22f),
                $"target player screen ratio: {_baseline.VisualMetrics.PlayerScreenHeightRatio:F3}", _debugStyle);
            GUI.Label(new Rect(24f, 93f, 410f, 22f),
                $"density: {_densityState} | floor columns: {_baseline.Ground.VisibleTileColumns}", _debugStyle);
            GUI.Label(new Rect(24f, 116f, 410f, 22f),
                $"overlay: {(_overlayEnabled ? "on" : "off")} {_overlayOpacity:P0} | O toggle | +/- opacity | S side-by-side", _debugStyle);
            GUI.Label(new Rect(24f, 139f, 410f, 30f), _baseline.ApprovedReferencePath, _debugStyle);
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out Color color)) return Color.magenta;
            return color;
        }
    }
}
