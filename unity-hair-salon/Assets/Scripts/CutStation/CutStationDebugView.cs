using UnityEngine;

namespace HairSalon.CutStations
{
    internal static class CutStationDebugView
    {
        public static void Build(Transform root, CutStationDefinition definition, ResolvedCutStationLayout layout)
        {
            var debug = new GameObject("DEV ONLY - CutStation Debug").transform;
            debug.SetParent(root, false);
            if (definition.Debug.ShowFootprint)
                Area(debug, "Footprint", layout.Footprint, definition.Debug.FootprintColor);
            if (definition.Debug.ShowCollision)
                Area(debug, "Collision", layout.Collision, definition.Debug.CollisionColor);
            if (!definition.Debug.ShowAnchors) return;
            Marker(debug, "Customer Seat", layout.CustomerSeatAnchor - layout.Origin, definition.Debug);
            Marker(debug, "Stylist Work", layout.StylistWorkAnchor - layout.Origin, definition.Debug);
            Marker(debug, "Queue", layout.QueueAnchor - layout.Origin, definition.Debug);
            Marker(debug, "Tool", layout.ToolAnchor - layout.Origin, definition.Debug);
            Marker(debug, "Service VFX", layout.ServiceVfxAnchor - layout.Origin, definition.Debug);
        }

        private static void Area(Transform parent, string name, ResolvedCutStationRect rect, Color color)
        {
            GameObject area = GameObject.CreatePrimitive(PrimitiveType.Cube);
            area.name = name;
            area.transform.SetParent(parent, false);
            area.transform.localPosition = new Vector3(rect.Center.x, .025f, rect.Center.y);
            area.transform.localScale = new Vector3(rect.Size.x, .05f, rect.Size.y);
            RemoveCollider(area);
            ApplyMaterial(area.GetComponent<Renderer>(), color);
        }

        private static void Marker(Transform parent, string name, Vector3 position, CutStationDebugDisplay config)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = Vector3.one * config.MarkerSize;
            RemoveCollider(marker);
            ApplyMaterial(marker.GetComponent<Renderer>(), config.AnchorColor);
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
            else UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void ApplyMaterial(Renderer renderer, Color color)
        {
            Shader shader = Resources.Load<Shader>("SalonLowPoly");
            if (shader == null)
                throw new MissingReferenceException("SalonLowPoly shader is required by cut station debug rendering.");
            renderer.sharedMaterial = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }
    }
}
