using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ValidateSelectedToonCityAssets
{
    [MenuItem("Tools/ToonCity/Validate Selected Assets")]
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths = new[] { "Roads/Road_1A", "Vegetation/Tree_1A", "Props/Trash_Can_1A", "Roads/Streetlight_1A" };
        var report = "";
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonCity_Selected/Prefabs/" + path + ".prefab");
            if (!prefab) throw new Exception("Missing prefab: " + path);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new Exception("No renderer: " + path);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (!filter.sharedMesh) throw new Exception("Missing mesh: " + path);
            foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
            {
                if (!material || !material.shader || material.shader.name != "Universal Render Pipeline/Lit")
                    throw new Exception("Invalid material: " + path);
                if (!material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
                    throw new Exception("Unsupported shader: " + path);
                if (!material.GetTexture("_BaseMap")) throw new Exception("Missing Base Map: " + path);
            }
            foreach (var dependency in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true))
                if (dependency.StartsWith("Assets/Toon City/", StringComparison.Ordinal))
                    throw new Exception("Original pack dependency: " + dependency);
            report += path + ": mesh, URP/Lit, texture and independent dependencies OK\n";
        }
        File.WriteAllText("Assets/ToonCity_Selected/Validation.txt", report);
        Debug.Log(report);
    }
}
