using System;
using System.Collections.Generic;
using System.IO;
using HairSalon.Character;
using UnityEditor;
using UnityEngine;

public static class HairdresserSpriteSheetImporter
{
    private const string ArtRoot = "Assets/Art/Characters/Hairdresser";
    private const string SourceRoot = ArtRoot + "/SourceSheets";
    private const string ProcessedRoot = ArtRoot + "/ProcessedSheets";
    private const string SpriteRoot = ArtRoot + "/Sprites";
    private const string PrefabPath = "Assets/Resources/Characters/Hairdresser.prefab";
    private const int OutputWidth = 384;
    private const int OutputHeight = 512;

    private static readonly Sheet[] Sheets =
    {
        new Sheet("Idle", "hairdresser-idle-8dir-v1.png", false, "idle"),
        new Sheet("Walk", "hairdresser-walk-8dir-v1.png", true, "walk"),
        new Sheet("WalkPassing", "hairdresser-walk-passing-8dir-v1.png", true, "walkPassing"),
        new Sheet("WalkOpposite", "hairdresser-walk-opposite-8dir-v1.png", true, "walkOpposite"),
        new Sheet("CutHair", "hairdresser-cuthair-8dir-v1.png", true, "cutHair"),
        new Sheet("DryHair", "hairdresser-dryhair-8dir-v1.png", true, "dryHair"),
        new Sheet("WashHair", "hairdresser-washhair-8dir-v1.png", true, "washHair")
    };

    private static readonly string[] DirectionNames =
    {
        "North", "NorthEast", "East", "SouthEast",
        "South", "SouthWest", "West", "NorthWest"
    };

    [MenuItem("Hair Salon/Characters/Import Approved Hairdresser Sprite Sheets")]
    public static void Import()
    {
        ValidateSources();
        HairdresserPrefabBuilder.Rebuild();

        foreach (Sheet sheet in Sheets) ImportSheet(sheet);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureSpriteImporters();
        BindPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("Imported and bound " + (Sheets.Length * 8) +
            " approved Hairdresser sprites to " + PrefabPath);
    }

    public static void ImportFromCommandLine() => Import();

    private static void ImportSheet(Sheet sheet)
    {
        string sourcePath = SourceRoot + "/" + sheet.FileName;
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(sourcePath), false))
            throw new InvalidDataException("Could not decode " + sourcePath);

        string outputFolder = SpriteRoot + "/" + sheet.StateName;
        Directory.CreateDirectory(outputFolder);
        Directory.CreateDirectory(ProcessedRoot);
        Texture2D atlas = new Texture2D(OutputWidth * 4, OutputHeight * 2,
            TextureFormat.RGBA32, false);
        try
        {
            for (int direction = 0; direction < 8; direction++)
            {
                Texture2D cell = ExtractDirection(source, direction);
                try
                {
                    Color[] pixels = cell.GetPixels();
                    if (sheet.ChromaKey) RemoveGreenScreen(pixels);
                    else RemoveConnectedNeutralBackground(pixels, cell.width, cell.height);
                    cell.SetPixels(pixels);
                    cell.Apply(false, false);

                    Texture2D normalized = Letterbox(cell, OutputWidth, OutputHeight);
                    try
                    {
                        int atlasColumn = direction < 4 ? direction : direction - 4;
                        int atlasRow = direction < 4 ? 0 : 1;
                        atlas.SetPixels(atlasColumn * OutputWidth, atlasRow * OutputHeight,
                            OutputWidth, OutputHeight, normalized.GetPixels());
                        string outputPath = outputFolder + "/" + DirectionNames[direction] + ".png";
                        File.WriteAllBytes(outputPath, normalized.EncodeToPNG());
                    }
                    finally { UnityEngine.Object.DestroyImmediate(normalized); }
                }
                finally { UnityEngine.Object.DestroyImmediate(cell); }
            }

            atlas.Apply(false, false);
            File.WriteAllBytes(ProcessedRoot + "/hairdresser-" + sheet.StateName.ToLowerInvariant() +
                "-8dir.png", atlas.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(atlas);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static Texture2D ExtractDirection(Texture2D source, int direction)
    {
        int column = direction < 4 ? direction : direction - 4;
        int rowFromBottom = direction < 4 ? 0 : 1;
        int x0 = Mathf.RoundToInt(column * source.width / 4f);
        int x1 = Mathf.RoundToInt((column + 1) * source.width / 4f);
        int y0 = Mathf.RoundToInt(rowFromBottom * source.height / 2f);
        int y1 = Mathf.RoundToInt((rowFromBottom + 1) * source.height / 2f);
        Texture2D result = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGBA32, false);
        result.SetPixels(source.GetPixels(x0, y0, x1 - x0, y1 - y0));
        result.Apply(false, false);
        return result;
    }

    // Removes a generated green screen by green dominance. A narrow feather keeps
    // anti-aliased character edges while hard-clearing the noisy painted backdrop.
    private static void RemoveGreenScreen(Color[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            float other = Mathf.Max(c.r, c.b);
            float greenExcess = c.g - other;
            if (c.g > .35f && greenExcess >= .40f)
            {
                pixels[i] = Color.clear;
                continue;
            }
            if (c.g > .35f && greenExcess > .10f)
            {
                c.a = 1f - Mathf.SmoothStep(.10f, .40f, greenExcess);
                c.g = Mathf.Min(c.g, other);
                if (c.a <= .02f) c = Color.clear;
            }
            else c.a = 1f;
            pixels[i] = c;
        }
    }

    // The approved Idle source has a baked neutral checker. Flooding only from the
    // boundary removes that connected backdrop without erasing the white shirt.
    private static void RemoveConnectedNeutralBackground(Color[] pixels, int width, int height)
    {
        bool[] removed = new bool[pixels.Length];
        Queue<int> pending = new Queue<int>();
        for (int x = 0; x < width; x++)
        {
            EnqueueBackground(x, 0, pixels, removed, width, height, pending);
            EnqueueBackground(x, height - 1, pixels, removed, width, height, pending);
        }
        for (int y = 0; y < height; y++)
        {
            EnqueueBackground(0, y, pixels, removed, width, height, pending);
            EnqueueBackground(width - 1, y, pixels, removed, width, height, pending);
        }

        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            int x = index % width;
            int y = index / width;
            EnqueueBackground(x - 1, y, pixels, removed, width, height, pending);
            EnqueueBackground(x + 1, y, pixels, removed, width, height, pending);
            EnqueueBackground(x, y - 1, pixels, removed, width, height, pending);
            EnqueueBackground(x, y + 1, pixels, removed, width, height, pending);
        }

        for (int i = 0; i < pixels.Length; i++)
            if (removed[i]) pixels[i] = Color.clear;
    }

    private static void EnqueueBackground(int x, int y, Color[] pixels, bool[] removed,
        int width, int height, Queue<int> pending)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int index = y * width + x;
        if (removed[index] || !IsNeutralBackdrop(pixels[index])) return;
        removed[index] = true;
        pending.Enqueue(index);
    }

    private static bool IsNeutralBackdrop(Color c)
    {
        float maximum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float minimum = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return maximum >= .55f && maximum - minimum <= .13f;
    }

    private static Texture2D Letterbox(Texture2D source, int width, int height)
    {
        Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] outputPixels = new Color[width * height];
        float scale = Mathf.Min(width / (float)source.width, height / (float)source.height);
        int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
        int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
        int offsetX = (width - scaledWidth) / 2;
        int offsetY = (height - scaledHeight) / 2;

        for (int y = 0; y < scaledHeight; y++)
        for (int x = 0; x < scaledWidth; x++)
        {
            float u = (x + .5f) / scaledWidth;
            float v = (y + .5f) / scaledHeight;
            outputPixels[(y + offsetY) * width + x + offsetX] = SamplePremultiplied(source, u, v);
        }

        output.SetPixels(outputPixels);
        output.Apply(false, false);
        return output;
    }

    private static Color SamplePremultiplied(Texture2D source, float u, float v)
    {
        float px = Mathf.Clamp(u * source.width - .5f, 0f, source.width - 1f);
        float py = Mathf.Clamp(v * source.height - .5f, 0f, source.height - 1f);
        int x0 = Mathf.FloorToInt(px);
        int y0 = Mathf.FloorToInt(py);
        int x1 = Mathf.Min(x0 + 1, source.width - 1);
        int y1 = Mathf.Min(y0 + 1, source.height - 1);
        float tx = px - x0;
        float ty = py - y0;
        Color a = Premultiply(source.GetPixel(x0, y0));
        Color b = Premultiply(source.GetPixel(x1, y0));
        Color c = Premultiply(source.GetPixel(x0, y1));
        Color d = Premultiply(source.GetPixel(x1, y1));
        Color value = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
        if (value.a > .001f)
        {
            value.r /= value.a;
            value.g /= value.a;
            value.b /= value.a;
        }
        return value;
    }

    private static Color Premultiply(Color color)
    {
        color.r *= color.a;
        color.g *= color.a;
        color.b *= color.a;
        return color;
    }

    private static void ConfigureSpriteImporters()
    {
        foreach (Sheet sheet in Sheets)
        for (int direction = 0; direction < 8; direction++)
        {
            string path = SpritePath(sheet, direction);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 180f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void BindPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("Visual/HairdresserSpriteVisual");
            if (visual == null) throw new MissingReferenceException("Hairdresser visual slot is missing.");
            Hairdresser2DPresenter presenter = visual.GetComponent<Hairdresser2DPresenter>();
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            SerializedObject serialized = new SerializedObject(presenter);
            foreach (Sheet sheet in Sheets)
            {
                SerializedProperty directions = serialized.FindProperty(sheet.PresenterField)
                    .FindPropertyRelative("directions");
                directions.arraySize = 8;
                for (int direction = 0; direction < 8; direction++)
                    directions.GetArrayElementAtIndex(direction).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(sheet, direction));
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(Sheets[0], 4));
            renderer.sortingOrder = 20;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static string SpritePath(Sheet sheet, int direction) =>
        SpriteRoot + "/" + sheet.StateName + "/" + DirectionNames[direction] + ".png";

    private static void ValidateSources()
    {
        foreach (Sheet sheet in Sheets)
        {
            string path = SourceRoot + "/" + sheet.FileName;
            if (!File.Exists(path)) throw new FileNotFoundException("Missing Hairdresser source sheet", path);
        }
    }

    private sealed class Sheet
    {
        public readonly string StateName;
        public readonly string FileName;
        public readonly bool ChromaKey;
        public readonly string PresenterField;

        public Sheet(string stateName, string fileName, bool chromaKey, string presenterField)
        {
            StateName = stateName;
            FileName = fileName;
            ChromaKey = chromaKey;
            PresenterField = presenterField;
        }
    }
}
