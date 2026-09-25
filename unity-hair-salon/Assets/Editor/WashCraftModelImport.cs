using UnityEditor;
using UnityEngine;

// Scope is deliberately limited to our original Blender-authored assets.
public sealed class WashCraftModelImport : AssetPostprocessor
{
    private bool IsCraft => assetPath.StartsWith("Assets/Resources/Models/WashCraft/");
    private void OnPreprocessModel()
    {
        if (!IsCraft) return;
        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.addCollider = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }
    private Material OnAssignMaterialModel(Material source, Renderer renderer)
    {
        if (!IsCraft) return null;
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Resources/SalonCraftLit.shader");
        if (shader == null) throw new System.InvalidOperationException("Crafted furniture shader is missing.");
        var result = new Material(shader) { name = source.name, color = source.color };
        bool metal = source.name.Contains("Chrome") || source.name.Contains("Brass");
        result.SetFloat("_Metallic", metal ? .55f : 0f);
        result.SetFloat("_Glossiness", metal ? .60f : source.name.Contains("Porcelain") ? .32f : .12f);
        return result;
    }
}
