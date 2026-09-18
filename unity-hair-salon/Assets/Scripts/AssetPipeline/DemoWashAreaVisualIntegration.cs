using System;
using System.Collections;
using System.Collections.Generic;
using HairSalon.Character;
using UnityEngine;

namespace HairSalon.AssetPipeline
{
    /// <summary>
    /// Development-only visual slice inside the real HairSalonDemo scene. It replaces renderers only;
    /// the formal station identity, collision and service anchors remain owned by SalonDemo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoWashAreaVisualIntegration : MonoBehaviour
    {
        public const string WashAssetId = "furniture-wash-station-vintage-right-wall";
        public const string DecorationAssetId = "furniture-magazine-rack-wood";
        public const float IntegratedWashVisualScale = .68f;

        private static readonly HashSet<string> WashPlaceholderNames = new HashSet<string>
        {
            "Wash Base", "Wash Bed", "Basin", "Contact Shadow"
        };

        private string _mode = string.Empty;
        private Camera _camera;
        private Transform _player;
        private Quaternion _cameraRotation;
        private bool _evidenceActive;
        private float _cameraSize;

        public static string ParseMode(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart == url.Length - 1) return string.Empty;
            string[] pairs = url.Substring(queryStart + 1).Split('&');
            foreach (string pair in pairs)
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length != 2 || !string.Equals(parts[0], "washAreaVisual",
                        StringComparison.OrdinalIgnoreCase)) continue;
                string value = Uri.UnescapeDataString(parts[1]).ToLowerInvariant();
                return value == "before" || value == "after" || value == "context"
                    ? value
                    : string.Empty;
            }
            return string.Empty;
        }

        public static int DisablePlaceholderVisuals(Transform station)
        {
            if (station == null) return 0;
            int disabled = 0;
            foreach (Renderer renderer in station.GetComponentsInChildren<Renderer>(true))
            {
                if (!WashPlaceholderNames.Contains(renderer.gameObject.name) || !renderer.enabled) continue;
                renderer.enabled = false;
                disabled++;
            }
            return disabled;
        }

        private IEnumerator Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _mode = ParseMode(Application.absoluteURL);
            if (string.IsNullOrEmpty(_mode)) yield break;
            if (!Debug.isDebugBuild && !Application.isEditor)
                throw new InvalidOperationException("Wash-area visual evidence is development-only.");

            yield return null;
            _camera = Camera.main;
            _player = GameObject.Find("主控理发师")?.transform;
            GameObject stationObject = GameObject.Find("Wash Workstation 1");
            if (_camera == null || _player == null || stationObject == null)
                throw new MissingReferenceException("Formal Demo wash-area evidence dependencies are missing.");

            Transform station = stationObject.transform;
            int colliderCountBefore = EnabledColliderCount(station);
            int anchorCountBefore = ServiceAnchorCount(station);
            int hiddenRenderers = 0;
            bool realWash = false;
            bool realDecoration = false;
            if (_mode == "after" || _mode == "context")
            {
                AssetManifest manifest = AssetManifestLoader.LoadFromResources();
                AssetDefinition wash = RequireAsset(manifest, WashAssetId, "right-wall");
                AssetDefinition decoration = RequireAsset(manifest, DecorationAssetId, "free");
                hiddenRenderers = DisablePlaceholderVisuals(station);
                realWash = BuildWashArtwork(station, wash);
                realDecoration = BuildDecorationArtwork(decoration);
            }

            _cameraRotation = _camera.transform.rotation;
            _cameraSize = _mode == "context" ? 3.65f : 5.2f;
            _evidenceActive = true;
            ApplyEvidencePose();
            yield return null;

            bool collidersPreserved = EnabledColliderCount(station) == colliderCountBefore;
            bool anchorsPreserved = ServiceAnchorCount(station) == anchorCountBefore && anchorCountBefore == 3;
            Renderer playerRenderer = _player.GetComponentInChildren<SpriteRenderer>();
            float playerRatio = ScreenHeightRatio(playerRenderer, _camera);
            float stationRatio = ScreenHeightRatio(station.GetComponentsInChildren<Renderer>(true), _camera);
            Debug.Log("[WASH_AREA_VISUAL_READY] mode=" + _mode +
                      " scene=HairSalonDemo projection=orthographic cameraChanged=False" +
                      " realWash=" + realWash + " realDecoration=" + realDecoration +
                      " orientation=right-wall mirrored=False rotated=False negativeScale=False" +
                      " hiddenPlaceholderRenderers=" + hiddenRenderers +
                      " collidersPreserved=" + collidersPreserved +
                      " anchorsPreserved=" + anchorsPreserved +
                      " playerScreenHeight=" + playerRatio.ToString("F3") +
                      " stationScreenHeight=" + stationRatio.ToString("F3") +
                      " stationToPlayer=" + (playerRatio > 0f ? stationRatio / playerRatio : 0f).ToString("F3"));
#endif
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_evidenceActive) ApplyEvidencePose();
#endif
        }

        private void ApplyEvidencePose()
        {
            Vector3 target = new Vector3(-7.25f, 1.05f, 4.35f);
            const float cameraDistance = 27.2f;
            _camera.transform.rotation = _cameraRotation;
            _camera.transform.position = target - (_cameraRotation * Vector3.forward) * cameraDistance;
            _camera.orthographic = true;
            _camera.orthographicSize = _cameraSize;
            _player.position = SalonDemo.WashPlayerAnchorPosition;
            HairdresserCharacter character = _player.GetComponent<HairdresserCharacter>();
            if (character != null) character.FaceTowards(Vector3.right);
        }

        private static AssetDefinition RequireAsset(AssetManifest manifest, string assetId, string orientation)
        {
            AssetDefinition asset = manifest?.Find(assetId);
            if (asset == null) throw new MissingReferenceException("Manifest asset is missing: " + assetId);
            if (asset.DefaultOrientation != orientation || asset.Directions == null ||
                asset.Directions.Count != 1 || asset.Directions[0] != orientation)
                throw new InvalidOperationException("Visual integration orientation contract failed: " + assetId);
            if (asset.Shadow == null || asset.Shadow.Mode != "baked" || asset.Shadow.UsesProceduralShadow)
                throw new InvalidOperationException("Visual integration requires one baked shadow only: " + assetId);
            return asset;
        }

        private static bool BuildWashArtwork(Transform station, AssetDefinition asset)
        {
            var visual = new GameObject("Demo Wash Visual Integration [right-wall]").transform;
            visual.SetParent(station, false);
            visual.position = new Vector3(SalonDemo.PrimaryWashBedPosition.x, .03f,
                SalonDemo.PrimaryWashBedPosition.z);
            visual.localScale = Vector3.one * IntegratedWashVisualScale;
            CandidateAssetValidation.BuildArtwork(visual, asset, asset.DefaultOrientation);
            return visual.GetComponentInChildren<MeshRenderer>() != null &&
                   visual.lossyScale.x > 0f && visual.lossyScale.y > 0f && visual.lossyScale.z > 0f;
        }

        private static bool BuildDecorationArtwork(AssetDefinition asset)
        {
            Vector3 position = new Vector3(-9.45f, .02f, 5.7f);
            HideNearbyVisuals(position, 1.05f, "Plant Pot", "Faceted Leaf", "Contact Shadow");
            Transform parent = GameObject.Find("Fixed Salon Map")?.transform;
            var visual = new GameObject("Demo Wash Decoration [magazine-rack]").transform;
            visual.SetParent(parent, false);
            visual.position = position;
            visual.localScale = Vector3.one * .86f;
            CandidateAssetValidation.BuildArtwork(visual, asset, asset.DefaultOrientation);
            return visual.GetComponentInChildren<MeshRenderer>() != null;
        }

        private static void HideNearbyVisuals(Vector3 origin, float radius, params string[] names)
        {
            var allowed = new HashSet<string>(names);
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!allowed.Contains(renderer.gameObject.name)) continue;
                Vector2 delta = new Vector2(renderer.transform.position.x - origin.x,
                    renderer.transform.position.z - origin.z);
                if (delta.magnitude <= radius) renderer.enabled = false;
            }
        }

        private static int EnabledColliderCount(Transform root)
        {
            int count = 0;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) count++;
            return count;
        }

        private static int ServiceAnchorCount(Transform station)
        {
            if (station == null) return 0;
            int count = 0;
            if (station.Find("CustomerSeatAnchor") != null) count++;
            if (station.Find("PlayerServiceAnchor") != null) count++;
            if (station.Find("CustomerUIAnchor") != null) count++;
            return count;
        }

        private static float ScreenHeightRatio(Renderer renderer, Camera camera)
            => renderer == null ? 0f : ScreenHeightRatio(new[] { renderer }, camera);

        private static float ScreenHeightRatio(IEnumerable<Renderer> renderers, Camera camera)
        {
            if (camera == null || Screen.height <= 0) return 0f;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    float screenY = camera.WorldToScreenPoint(corner).y;
                    minimum = Mathf.Min(minimum, screenY);
                    maximum = Mathf.Max(maximum, screenY);
                }
            }
            return float.IsInfinity(minimum) ? 0f : Mathf.Max(0f, maximum - minimum) / Screen.height;
        }
    }
}
