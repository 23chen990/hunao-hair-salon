using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HairSalon.AssetPipeline
{
    /// <summary>Authored render layer. Existing station roots, colliders, anchors and gameplay own interaction.</summary>
    public static class WashCraftIntegration
    {
        public const string WashId = "furniture-wash-station-crafted";
        public const string RoomId = "prop-salon-room-crafted";
        public static bool IsEnabled
        {
            get
            {
                if (SceneManager.GetActiveScene().name != "HairSalonDemo") return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Application.absoluteURL.Contains("washCraft=before") ||
                    !string.IsNullOrEmpty(DemoWashAreaVisualIntegration.ParseMode(Application.absoluteURL))) return false;
#endif
                // The approved playable default is the first crafted slice requested by the
                // product owner. `washCraft=original` remains an explicit rollback aid.
                return !(Application.absoluteURL ?? string.Empty).Contains("washCraft=original");
            }
        }

        public static void Apply(Transform room)
        {
            if (!IsEnabled || room.Find("Crafted Room Finish") != null) return;
            var manifest = AssetManifestLoader.LoadFromResources();
            var issues = AssetManifestValidator.Validate(manifest);
            if (issues.Count > 0) throw new InvalidOperationException("Craft scene rejected invalid assets: " + issues[0]);
            var definition = manifest.Assets.Find(a => a.Id == RoomId);
            if (definition == null) throw new MissingReferenceException(RoomId);
            // Hide renderers only. The original floor click targets and boundary colliders survive.
            foreach (Renderer r in room.GetComponentsInChildren<Renderer>())
            {
                string n = r.name;
                bool shell = n == "Floor Tile" || n == "Back Wall" || n == "Back Wall Cream" ||
                             n == "Left Wall" || n == "Right Wall" || n == "Wall Panel";
                bool washShelf = (n == "Shelf" || n == "Bottle") && r.transform.position.x < -2f && r.transform.position.z > 6f;
                bool washPlant = (n == "Plant Pot" || n == "Faceted Leaf") &&
                                 Vector3.Distance(r.transform.position, new Vector3(-9.4f, 1f, 5.7f)) < 2f;
                if (shell || washShelf || washPlant) r.enabled = false;
            }
            var finish = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(definition.ResourcePath), room);
            finish.name = "Crafted Room Finish";
            finish.transform.localPosition = Vector3.zero;
            for (int i = 1; i <= 2; i++)
            {
                var station = room.Find("洗头区/Wash Workstation " + i);
                if (station == null) throw new MissingReferenceException("Wash Workstation " + i);
                var baseRenderer = station.Find("Wash Base");
                Vector3 origin = baseRenderer.position;
                origin.y = 0f;
                Collider[] beforeColliders = station.GetComponentsInChildren<Collider>(true);
                Transform[] anchors = { station.Find("CustomerSeatAnchor"), station.Find("PlayerServiceAnchor"), station.Find("CustomerUIAnchor") };
                Vector3[] positions = { anchors[0].position, anchors[1].position, anchors[2].position };
                ReplaceStationVisual(station, origin);
                Collider[] afterColliders = station.GetComponentsInChildren<Collider>(true);
                if (beforeColliders.Length != afterColliders.Length)
                    throw new InvalidOperationException("Craft visual changed station collision count.");
                for (int c = 0; c < beforeColliders.Length; c++)
                    if (beforeColliders[c] != afterColliders[c]) throw new InvalidOperationException("Craft visual replaced an existing collider.");
                for (int a = 0; a < anchors.Length; a++)
                    if (anchors[a].position != positions[a]) throw new InvalidOperationException("Craft visual moved a service anchor.");
            }
            ApplyLighting();
            Debug.Log("[WASH_CRAFT_READY] scene=HairSalonDemo stations=2 authoredModels=True collidersPreserved=True anchorsPreserved=True");
        }

        public static void ReplaceStationVisual(Transform station, Vector3 origin)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            if (station.Find("Wash Craft Visual") != null) return;
            var manifest = AssetManifestLoader.LoadFromResources();
            var definition = manifest.Assets.Find(a => a.Id == WashId);
            if (definition == null) throw new MissingReferenceException(WashId);
            var prefab = Resources.Load<GameObject>(definition.ResourcePath);
            if (prefab == null) throw new MissingReferenceException(definition.ResourcePath);
            DemoWashAreaVisualIntegration.DisablePlaceholderVisuals(station);
            var visual = UnityEngine.Object.Instantiate(prefab, station);
            visual.name = "Wash Craft Visual";
            visual.transform.position = origin;
            visual.transform.rotation = Quaternion.identity;
            ContactShadow.Apply(visual.transform, definition.Shadow);
        }

        public static void ConfigureCamera(Camera camera)
        {
            if (!IsEnabled) return;
            camera.transform.position = new Vector3(-12f, 20f, -23f);
            camera.transform.LookAt(new Vector3(0f, .65f, 1f));
            camera.orthographicSize = 8.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.27f, .31f, .31f);
        }

        public static void ApplyLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .59f, .59f);
            RenderSettings.ambientEquatorColor = new Color(.40f, .43f, .40f);
            RenderSettings.ambientGroundColor = new Color(.30f, .31f, .27f);
            var sun = GameObject.Find("Warm Key Light")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, .93f, .82f);
                sun.intensity = .85f;
                sun.shadowStrength = .32f;
                sun.shadowBias = .025f;
                sun.shadowNormalBias = .15f;
                sun.shadows = LightShadows.Soft;
            }
            var fill = GameObject.Find("Soft Fill")?.GetComponent<Light>();
            if (fill != null) fill.intensity = .15f;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 65f;
            QualitySettings.antiAliasing = 4;
        }
    }
}
