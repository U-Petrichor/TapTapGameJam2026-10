using System.Collections.Generic;
using TapTapGameJam.Buffs;
using TapTapGameJam.Buffs.EditorTools;
using TapTapGameJam.Items;
using UnityEditor;
using UnityEngine;

namespace TapTapGameJam.Items.EditorTools
{
    /// <summary>
    /// <see cref="DressData"/> 的配表界面。
    ///
    /// 和天赋树窗口共用 <see cref="BuffKeyPicker"/>，所以两边的 key 下拉、单位提示完全一致；
    /// 数值语义也一样：百分比 0.1 = +10%，减免 0.1 = 减免 10%，Tag 不用填数值。
    /// </summary>
    [CustomEditor(typeof(DressData))]
    public sealed class DressDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var data = (DressData)target;

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_description"), new GUIContent("描述"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("提供的 Buff", EditorStyles.boldLabel);

            List<DressBuffEntry> buffs = data.MutableBuffs;

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                DressBuffEntry entry = buffs[i];
                BuffKind kind = BuffKeys.KindOf(entry.Key);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                BuffKey key = BuffKeyPicker.DrawKeyField("效果", entry.Key);
                kind = BuffKeys.KindOf(key);

                float value = entry.Value;
                if (kind == BuffKind.Flag)
                {
                    EditorGUILayout.LabelField("数值", "能力 Tag，不需要填数值");
                    value = 0f;
                }
                else
                {
                    value = EditorGUILayout.FloatField(BuffKeyPicker.UnitLabel(kind), entry.Value);
                }

                if (key != entry.Key || !Mathf.Approximately(value, entry.Value))
                {
                    Undo.RecordObject(data, "Edit Dress Buff");
                    buffs[i] = new DressBuffEntry(key, value);
                    EditorUtility.SetDirty(data);
                }

                if (GUILayout.Button("删除效果"))
                {
                    Undo.RecordObject(data, "Remove Dress Buff");
                    buffs.RemoveAt(i);
                    EditorUtility.SetDirty(data);
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("添加效果"))
            {
                Undo.RecordObject(data, "Add Dress Buff");
                buffs.Add(new DressBuffEntry(BuffKey.PlayerMoveSpeedPercent, 0.1f));
                EditorUtility.SetDirty(data);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "数值语义：百分比填小数（0.1 = +10%）；减免填正数（0.1 = 减免 10%）；" +
                "固定值填绝对值（背包档位填 1）；Tag 忽略数值。\n" +
                "这些 Buff 的来源是「装扮」，所以会被天赋「巧饰虫装 / 精工战衣」的装扮加成放大（只放大百分比）。",
                MessageType.Info);
        }
    }
}
