using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ProductionTextureImportPolicyTests
{
    [Test]
    public void BuildScript_ExposesDeterministicEditorGameViewEvidenceCapture()
    {
        System.Type buildScript = System.Type.GetType("BuildScript, Assembly-CSharp-Editor");
        Assert.That(buildScript?.GetMethod("CaptureAssetLabEditorEvidence"), Is.Not.Null);
    }

    [Test]
    public void EditorEvidenceCapture_RejectsAFlatGraphicsLessFrame()
    {
        System.Type buildScript = System.Type.GetType("BuildScript, Assembly-CSharp-Editor");
        System.Reflection.MethodInfo hasContent = buildScript?.GetMethod("HasVisualContent");
        Assert.That(hasContent, Is.Not.Null);
        var solid = new Texture2D(8, 8, TextureFormat.RGB24, false);
        var varied = new Texture2D(8, 8, TextureFormat.RGB24, false);
        try
        {
            Color[] solidPixels = new Color[64];
            Color[] variedPixels = new Color[64];
            for (int i = 0; i < 64; i++)
            {
                solidPixels[i] = Color.gray;
                variedPixels[i] = (i & 1) == 0 ? Color.black : Color.white;
            }
            solid.SetPixels(solidPixels); solid.Apply();
            varied.SetPixels(variedPixels); varied.Apply();
            Assert.That((bool)hasContent.Invoke(null, new object[] { solid }), Is.False);
            Assert.That((bool)hasContent.Invoke(null, new object[] { varied }), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(solid);
            Object.DestroyImmediate(varied);
        }
    }

    [TestCase("Assets/Resources/Imported/furniture-magazine-rack-wood.png", 1211, 1299)]
    [TestCase("Assets/Resources/Imported/furniture-wash-station-vintage-right-wall.png", 1254, 1254)]
    public void ProductionRenderedAssets_KeepSourcePixelsAndUseTheTransparent2D5Profile(
        string path, int sourceWidth, int sourceHeight)
    {
        System.Type policy = System.Type.GetType("Production2D5TexturePolicy, Assembly-CSharp-Editor");
        Assert.That(policy, Is.Not.Null, "2.5D production texture policy must exist in the Editor assembly.");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
        Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
        Assert.That(importer.alphaIsTransparency, Is.True);
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
        Assert.That(importer.anisoLevel, Is.EqualTo(1));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
        Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        Assert.That(importer.sRGBTexture, Is.True);

        TextureImporterPlatformSettings webgl = importer.GetPlatformTextureSettings("WebGL");
        Assert.That(webgl.overridden, Is.True);
        Assert.That(webgl.maxTextureSize, Is.EqualTo(2048));
        Assert.That(webgl.format, Is.EqualTo(TextureImporterFormat.RGBA32));
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.That(texture.width, Is.EqualTo(sourceWidth));
        Assert.That(texture.height, Is.EqualTo(sourceHeight));
    }
}
