using System;
using System.Collections.Generic;
using TapTapGameJam.Cockroach;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>New and duplicated nests receive persistent identities when the scene is saved.</summary>
[InitializeOnLoad]
public static class SaveNestIdentityAuthoring
{
    static SaveNestIdentityAuthoring() => EditorSceneManager.sceneSaving += Assign;

    private static void Assign(Scene scene, string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var seen = new HashSet<string>();
        foreach (var root in scene.GetRootGameObjects())
        foreach (var nest in root.GetComponentsInChildren<CockNest>(true))
        {
            var serialized = new SerializedObject(nest);
            var id = serialized.FindProperty("_saveId");
            if (!string.IsNullOrEmpty(id.stringValue) && seen.Add(id.stringValue)) continue;
            id.stringValue = Guid.NewGuid().ToString("N");
            seen.Add(id.stringValue);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(nest);
        }
    }
}
