using TapTapGameJam.Tutorial;
using UnityEditor;
using UnityEngine;

namespace TapTapGameJam.EditorTools
{
    /// <summary>
    /// 首次导入时生成可编辑的教程步骤资产；已有资产不会被覆盖。
    /// </summary>
    public static class TutorialSequenceAssetCreator
    {
        private const string AssetPath = "Assets/Resources/Tutorial/TutorialSequence.asset";

        [InitializeOnLoadMethod]
        private static void EnsureOnLoad()
        {
            EditorApplication.delayCall += () => EnsureAsset(false);
        }

        [MenuItem("Tools/Tutorial/Create or Reset Tutorial Sequence")]
        private static void CreateOrReset()
        {
            EnsureAsset(true);
        }

        private static void EnsureAsset(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            TutorialSequence existing = AssetDatabase.LoadAssetAtPath<TutorialSequence>(AssetPath);
            if (existing != null && !overwrite) return;
            if (existing != null && overwrite &&
                !EditorUtility.DisplayDialog("Tutorial Sequence",
                    "覆盖现有教程步骤？所有手工修改的文案都会丢失。", "覆盖", "取消"))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Tutorial"))
                AssetDatabase.CreateFolder("Assets/Resources", "Tutorial");

            if (existing != null) AssetDatabase.DeleteAsset(AssetPath);

            TutorialSequence sequence = ScriptableObject.CreateInstance<TutorialSequence>();
            sequence.SetSteps(TutorialDefaultContent.CreateSteps());
            AssetDatabase.CreateAsset(sequence, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Tutorial] 已生成教程步骤资产：{AssetPath}");
        }
    }
}
