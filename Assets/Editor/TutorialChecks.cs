using System.Collections.Generic;
using System.IO;
using TapTapGameJam.Core;
using TapTapGameJam.Tutorial;
using UnityEditor;
using UnityEngine;

namespace TapTapGameJam.EditorTools
{
    /// <summary>教程步骤的编辑器校验与 QA 入口。</summary>
    public static class TutorialChecks
    {
        private const string AssetPath = "Assets/Resources/Tutorial/TutorialSequence.asset";

        [MenuItem("Tools/Tutorial/Validate Sequence")]
        private static void ValidateSequence()
        {
            TutorialSequence sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(AssetPath);
            if (sequence == null)
            {
                Debug.LogError($"[Tutorial] 找不到步骤资产：{AssetPath}");
                return;
            }

            var ids = new HashSet<string>();
            var errors = new List<string>();

            foreach (TutorialStep step in sequence.Steps)
            {
                if (step == null)
                {
                    errors.Add("存在空步骤");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.id))
                    errors.Add("存在空 ID");
                else if (!ids.Add(step.id))
                    errors.Add($"重复 ID：{step.id}");

                if (string.IsNullOrWhiteSpace(step.trigger))
                    errors.Add($"{step.id} 没有 trigger");

                if (step.presentation == TutorialPresentation.Modal &&
                    string.IsNullOrWhiteSpace(step.text))
                    errors.Add($"{step.id} 是 Modal 但没有文字");

                if (step.presentation == TutorialPresentation.HighlightOnly &&
                    step.highlight == TutorialHighlightTarget.None)
                    errors.Add($"{step.id} 是高亮步骤但没有目标");
            }

            foreach (TutorialStep step in sequence.Steps)
            {
                if (step == null) continue;

                if (!string.IsNullOrEmpty(step.trigger) &&
                    step.trigger.StartsWith("after:"))
                {
                    string referenced = step.trigger.Substring("after:".Length);
                    if (!ids.Contains(referenced))
                        errors.Add($"{step.id} 的 after 引用不存在：{referenced}");
                }

                if (step.requires == null) continue;
                foreach (string required in step.requires)
                {
                    if (!string.IsNullOrEmpty(required) && !ids.Contains(required))
                        errors.Add($"{step.id} 的 requires 不存在：{required}");
                }
            }

            if (errors.Count == 0)
                Debug.Log($"[Tutorial] 校验通过，共 {sequence.Steps.Count} 步。");
            else
                Debug.LogError($"[Tutorial] 校验失败：\n- {string.Join("\n- ", errors)}");
        }

        [MenuItem("Tools/Tutorial/Print Seen Steps")]
        private static void PrintSeenSteps()
        {
            TutorialSequence sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(AssetPath);
            if (sequence == null)
            {
                Debug.LogError($"[Tutorial] 找不到步骤资产：{AssetPath}");
                return;
            }

            foreach (TutorialStep step in sequence.Steps)
            {
                if (step == null) continue;
                Debug.Log($"[Tutorial] {step.id}: {(PlayerProfile.HasSeen(step.id) ? "已看" : "未看")}");
            }
        }

        [MenuItem("Tools/Tutorial/Reset Tutorial Progress")]
        private static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("Tutorial",
                    "清除本安装的所有“第一次”记录（包括开场视频）？", "重置", "取消"))
                return;

            TutorialDirector director = Application.isPlaying
                ? Object.FindFirstObjectByType<TutorialDirector>()
                : null;
            bool ok;
            if (director != null)
            {
                director.ResetProgress();
                ok = true;
            }
            else
            {
                ok = PlayerProfile.ResetTutorialProgress();
                TapTapGameJam.Player.MoveState.ResetFirstMoveTracking();
            }

            if (ok)
                Debug.Log("[Tutorial] 已重置教程进度。Play Mode 下建议重新进入 GameScene 以重建第一天流程。");
            else
                Debug.LogError("[Tutorial] 重置失败。");
        }

        [MenuItem("Tools/Tutorial/Delete Save")]
        private static void DeleteSave()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Delete Save",
                    "请先停止 Play Mode，再删除存档。", "确定");
                return;
            }

            string savePath = SaveManager.SavePath;
            if (!EditorUtility.DisplayDialog("Delete Save",
                    "删除当前安装的游戏存档及其备份？此操作无法撤销。\n\n" + savePath,
                    "删除", "取消"))
                return;

            try
            {
                string[] paths = { savePath, savePath + ".bak", savePath + ".tmp" };
                int deleted = 0;
                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    File.Delete(path);
                    deleted++;
                }

                Debug.Log(deleted > 0
                    ? $"[Save] 已删除 {deleted} 个存档文件。"
                    : "[Save] 没有找到存档文件。");
                EditorUtility.DisplayDialog("Delete Save",
                    deleted > 0 ? "存档及备份已删除。" : "没有找到存档文件。", "确定");
            }
            catch (IOException exception)
            {
                Debug.LogError($"[Save] 删除存档失败：{exception.Message}");
                EditorUtility.DisplayDialog("Delete Save", "删除存档失败，请查看 Console。", "确定");
            }
            catch (System.UnauthorizedAccessException exception)
            {
                Debug.LogError($"[Save] 删除存档失败：{exception.Message}");
                EditorUtility.DisplayDialog("Delete Save", "删除存档失败，请查看 Console。", "确定");
            }
        }
    }
}
