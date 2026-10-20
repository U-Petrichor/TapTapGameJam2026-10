using System.Collections.Generic;
using TapTapGameJam.Core;
using TapTapGameJam.Items;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Prefab GUIDs survive renaming/moving; no runtime AssetDatabase dependency.</summary>
[InitializeOnLoad]
public sealed class SaveItemCatalogBuilder : IPreprocessBuildWithReport
{
    private const string CatalogPath = "Assets/Resources/SaveItemCatalog.asset";
    public int callbackOrder => 0;

    static SaveItemCatalogBuilder()
    {
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Rebuild(); };
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.ExitingEditMode) Rebuild();
        };
    }

    public void OnPreprocessBuild(BuildReport report) => Rebuild();

    [MenuItem("Tools/Save/Rebuild Item Catalog")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<SaveItemCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SaveItemCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var entries = new List<SaveItemCatalog.Entry>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        foreach (var guid in guids)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            // Owned items are instantiated by their PickableItem component.
            var items = prefab.GetComponentsInChildren<PickableItem>(true);
            foreach (var item in items)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item, out string assetGuid, out long localId)) continue;
                string id = assetGuid + ":" + localId;
                var serialized = new SerializedObject(item);
                var property = serialized.FindProperty("_saveDefinitionId");
                if (property.stringValue != id)
                {
                    property.stringValue = id;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                }
                entries.Add(new SaveItemCatalog.Entry { id = id, prefab = item });
            }
        }
        bool changed = catalog.entries.Count != entries.Count;
        for (int i = 0; !changed && i < entries.Count; i++)
            changed = catalog.entries[i].id != entries[i].id || catalog.entries[i].prefab != entries[i].prefab;
        if (changed) { catalog.entries = entries; EditorUtility.SetDirty(catalog); }
        AssetDatabase.SaveAssets();
    }
}
