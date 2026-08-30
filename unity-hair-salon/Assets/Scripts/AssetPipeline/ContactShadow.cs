using UnityEngine;

namespace HairSalon.AssetPipeline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ContactShadow : MonoBehaviour
    {
        private static Sprite _sharedSprite;

        public Vector2 Size { get; private set; }
        public float Opacity { get; private set; }

        public static ContactShadow Apply(Transform parent, AssetShadow config, int sortingOrder = 0)
        {
            if (parent == null || config == null || !config.Enabled) return null;
            var target = new GameObject("Contact Shadow");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = new Vector3(config.Offset.x, .018f, config.Offset.y);
            target.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.transform.localScale = new Vector3(config.Size.x, config.Size.y, 1f);
            var shadow = target.AddComponent<ContactShadow>();
            shadow.Size = config.Size;
            shadow.Opacity = config.Opacity;
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = SharedSprite(config.Softness);
            renderer.color = new Color(0f, 0f, 0f, config.Opacity);
            renderer.sortingOrder = sortingOrder;
            Shader shader = Resources.Load<Shader>("SalonContactShadow");
            if (shader == null) throw new MissingReferenceException("SalonContactShadow shader is required.");
            renderer.sharedMaterial = new Material(shader)
            {
                color = Color.black,
                hideFlags = HideFlags.DontSave
            };
            return shadow;
        }

        private static Sprite SharedSprite(float softness)
        {
            if (_sharedSprite != null) return _sharedSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Generated Soft Contact Shadow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            float exponent = Mathf.Lerp(.8f, 2.5f, Mathf.Clamp01(softness));
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f) / size * 2f - 1f;
                float ny = (y + .5f) / size * 2f - 1f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), exponent);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            _sharedSprite.name = "Generated Soft Contact Shadow";
            _sharedSprite.hideFlags = HideFlags.HideAndDontSave;
            return _sharedSprite;
        }
    }
}
