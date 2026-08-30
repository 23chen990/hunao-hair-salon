using System;
using HairSalon.AssetPipeline;
using UnityEngine;

namespace HairSalon.CutStations
{
    internal static class CutStationVisualFactory
    {
        public static void Build(Transform stationRoot, CutStationDefinition definition, ResolvedCutStationLayout layout)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(stationRoot, false);
            visual.localRotation = Quaternion.Euler(0f, layout.YawDegrees, 0f);
            visual.localPosition = Vector3.forward * (definition.Sorting?.DepthOffset ?? 0f);
            visual.localScale = Vector3.one * definition.Visual.DefaultScale;

            switch (definition.Visual.Type)
            {
                case CutStationVisualType.BuiltinProcedural:
                    BuildProcedural(visual, definition.Sorting);
                    break;
                case CutStationVisualType.Sprite2D:
                    BuildSprite(visual, definition);
                    break;
                case CutStationVisualType.PrefabOrModel:
                    BuildPrefab(visual, definition);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            try
            {
                AssetDefinition common = AssetManifestLoader.LoadFromResources()
                    .Find("furniture-cut-station-classic");
                if (common?.Shadow != null)
                    ContactShadow.Apply(stationRoot, common.Shadow, (definition.Sorting?.SortingOrder ?? 0) - 1);
            }
            catch (InvalidOperationException)
            {
                // The dedicated cut-station manifest remains usable in isolated unit tests.
            }
        }

        private static void BuildProcedural(Transform root, CutStationSorting sorting)
        {
            Primitive(root, "Chair Base", PrimitiveType.Cylinder, new Vector3(0f, .12f, -.05f), new Vector3(.78f, .12f, .78f), new Color(.16f, .18f, .2f), sorting);
            Primitive(root, "Seat", PrimitiveType.Cube, new Vector3(0f, .55f, -.38f), new Vector3(1.08f, .22f, .92f), new Color(.11f, .5f, .58f), sorting);
            Primitive(root, "Chair Back", PrimitiveType.Cube, new Vector3(0f, 1.18f, .02f), new Vector3(1.1f, 1.15f, .2f), new Color(.08f, .38f, .47f), sorting);
            Primitive(root, "Mirror", PrimitiveType.Cube, new Vector3(0f, 1.65f, .68f), new Vector3(1.42f, 1.42f, .08f), new Color(.65f, .85f, .9f), sorting);
            Primitive(root, "Tool Shelf", PrimitiveType.Cube, new Vector3(0f, .86f, .55f), new Vector3(1.85f, .12f, .46f), new Color(.55f, .32f, .2f), sorting);
        }

        private static void BuildSprite(Transform root, CutStationDefinition definition)
        {
            Sprite sprite = Resources.Load<Sprite>(definition.Visual.ResourcePath);
            var renderer = root.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = definition.Sorting.SortingLayer;
            renderer.sortingOrder = definition.Sorting.SortingOrder;
        }

        private static void BuildPrefab(Transform root, CutStationDefinition definition)
        {
            GameObject prefab = Resources.Load<GameObject>(definition.Visual.ResourcePath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, root, false);
            instance.name = "Model";
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerName = definition.Sorting.SortingLayer;
                renderer.sortingOrder = definition.Sorting.SortingOrder;
            }
        }

        private static void Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, CutStationSorting sorting)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = primitive.GetComponent<Renderer>();
            renderer.sortingLayerName = sorting.SortingLayer;
            renderer.sortingOrder = sorting.SortingOrder;
            Shader shader = Resources.Load<Shader>("SalonLowPoly");
            if (shader == null)
                throw new MissingReferenceException("SalonLowPoly shader is required by the built-in cut station visual.");
            var material = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
        }
    }
}
