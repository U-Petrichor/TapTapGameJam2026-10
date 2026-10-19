using TapTapGameJam.UI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ArcLiquidProgress))]
public class ArcLiquidProgressEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("拖动 Progress 查看填充。Demo Loop 可自动往返演示；正式接入游戏时关闭，通过 SetProgress(0~1) 更新。编辑模式也会播放波浪。", MessageType.Info);
        DrawDefaultInspector();
        EditorGUILayout.BeginHorizontal();
        foreach (float value in new[] { 0f, .25f, .55f, 1f })
            if (GUILayout.Button(value.ToString("P0")))
            {
                Undo.RecordObject(target, "Preview liquid progress");
                var liquid = (ArcLiquidProgress)target;
                liquid.demoLoop = false;
                liquid.SetProgress(value);
                EditorUtility.SetDirty(target);
            }
        EditorGUILayout.EndHorizontal();
    }
}
