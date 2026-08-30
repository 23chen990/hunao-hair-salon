using System;
using UnityEngine;

namespace HairSalon.CutStations
{
    public static class CutStationFactory
    {
        public static CutStation Create(
            CutStationManifest manifest,
            string assetId,
            Vector3 worldPosition,
            CutStationOrientation orientation,
            Transform parent = null,
            bool showDebug = false)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            CutStationDefinition definition = manifest.Find(assetId);
            if (definition == null) throw new ArgumentException($"Unknown cut station AssetId: {assetId}", nameof(assetId));
            var issues = CutStationValidator.ValidateDefinition(definition);
            if (issues.Count > 0) throw new InvalidOperationException("Invalid cut station definition: " + issues[0]);

            ResolvedCutStationLayout layout = CutStationLayoutResolver.Resolve(definition, orientation, worldPosition);
            var root = new GameObject($"CutStation [{assetId}] ({orientation})");
            if (parent != null) root.transform.SetParent(parent, true);
            root.transform.position = worldPosition;
            var station = root.AddComponent<CutStation>();
            station.Layout = layout;
            station.CustomerSeatAnchor = Anchor(root.transform, "CustomerSeatAnchor", layout.CustomerSeatAnchor, layout.CustomerFacing);
            station.StylistWorkAnchor = Anchor(root.transform, "StylistWorkAnchor", layout.StylistWorkAnchor, layout.StylistFacing);
            station.QueueAnchor = Anchor(root.transform, "QueueAnchor", layout.QueueAnchor, Vector3.forward);
            station.ToolAnchor = Anchor(root.transform, "ToolAnchor", layout.ToolAnchor, Vector3.forward);
            station.ServiceVfxAnchor = Anchor(root.transform, "ServiceVFXAnchor", layout.ServiceVfxAnchor, Vector3.forward);
            station.Collision = BuildCollision(root, layout);
            CutStationVisualFactory.Build(root.transform, definition, layout);
            if (showDebug) CutStationDebugView.Build(root.transform, definition, layout);
            return station;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 worldPosition, Vector3 facing)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, true);
            anchor.position = worldPosition;
            if (facing.sqrMagnitude > .001f) anchor.rotation = Quaternion.LookRotation(facing, Vector3.up);
            return anchor;
        }

        private static BoxCollider BuildCollision(GameObject root, ResolvedCutStationLayout layout)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(layout.Collision.Center.x, .6f, layout.Collision.Center.y);
            collider.size = new Vector3(layout.Collision.Size.x, 1.2f, layout.Collision.Size.y);
            return collider;
        }
    }
}
