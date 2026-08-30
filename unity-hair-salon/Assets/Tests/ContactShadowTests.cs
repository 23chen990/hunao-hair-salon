using HairSalon.AssetPipeline;
using NUnit.Framework;
using UnityEngine;

public sealed class ContactShadowTests
{
    [Test]
    public void Apply_UsesManifestSizeOffsetAndOpacityWithoutFurnitureArtwork()
    {
        var root = new GameObject("Furniture");
        try
        {
            var config = new AssetShadow
            {
                Enabled = true,
                Size = new Vector2(2.4f, 1.1f),
                Offset = new Vector2(.3f, -.2f),
                Opacity = .32f,
                Softness = .75f
            };

            ContactShadow shadow = ContactShadow.Apply(root.transform, config, 3);

            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.transform.localPosition.x, Is.EqualTo(.3f).Within(.001f));
            Assert.That(shadow.transform.localPosition.z, Is.EqualTo(-.2f).Within(.001f));
            Assert.That(shadow.Size, Is.EqualTo(config.Size));
            Assert.That(shadow.Opacity, Is.EqualTo(.32f).Within(.001f));
            Assert.That(shadow.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(shadow.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(3));
            Assert.That(shadow.GetComponent<SpriteRenderer>().sharedMaterial.shader,
                Is.EqualTo(Resources.Load<Shader>("SalonContactShadow")));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Apply_DisabledShadowCreatesNothing()
    {
        var root = new GameObject("Furniture");
        try
        {
            Assert.That(ContactShadow.Apply(root.transform, new AssetShadow { Enabled = false }), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
