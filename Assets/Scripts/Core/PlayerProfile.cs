using System;
using System.Collections.Generic;
using System.IO;
using TapTapGameJam.Tutorial;
using UnityEngine;

namespace TapTapGameJam.Core
{
    [Serializable]
    public sealed class PlayerProfileData
    {
        public int version;
        public bool hasWatchedIntro;
        // 教程步骤的“已看”集合。JsonUtility 不支持 HashSet/Dictionary，所以用 List。
        public List<string> seenTutorialSteps;
    }

    /// <summary>教程与开场视频的独立进度文件，不随新游戏清除。</summary>
    public static class PlayerProfile
    {
        public static string TutorialProgressPath =>
            Path.Combine(SaveManager.SaveDirectory, "tutorial-progress.json");

        private static string PreviousTutorialProgressPath
        {
            get
            {
#if UNITY_EDITOR
                string directory = SaveManager.TestSaveDirectory ?? Application.persistentDataPath;
#elif UNITY_STANDALONE_WIN
                // Older Windows builds stored this file beside the game's _Data directory.
                string directory = Path.GetDirectoryName(Application.dataPath) ?? Application.persistentDataPath;
#else
                string directory = Application.persistentDataPath;
#endif
                return Path.Combine(directory, "tutorial-progress.json");
            }
        }

        private static string LegacyProfilePath => Path.Combine(
#if UNITY_EDITOR
            SaveManager.TestSaveDirectory ??
#endif
            Application.persistentDataPath, "profile.json");
        private static PlayerProfileData _data;
        public static bool HasWatchedIntro => HasSeen(TutorialStepIds.IntroVideo);

        private static PlayerProfileData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = Read(TutorialProgressPath) ?? Read(TutorialProgressPath + ".bak");
                    if (_data == null)
                    {
                        // Migrate prior locations so already-seen tutorials do not replay.
                        PlayerProfileData legacy = null;
                        if (!string.Equals(PreviousTutorialProgressPath, TutorialProgressPath, StringComparison.OrdinalIgnoreCase))
                            legacy = Read(PreviousTutorialProgressPath) ??
                                Read(PreviousTutorialProgressPath + ".bak");
                        if (legacy == null)
                            legacy = Read(LegacyProfilePath) ?? Read(LegacyProfilePath + ".bak");
                        _data = legacy ?? new PlayerProfileData { version = 1 };
                        Normalize(_data);
                        if (legacy != null) Write(_data);
                    }
                    else Normalize(_data);
                }

                return _data;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _data = null;

        /// <summary>某个教程步骤 / 首次事件是否已经发生过。</summary>
        public static bool HasSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return Data.seenTutorialSteps.Contains(id);
        }

        /// <summary>记录一条“已看”，并立即原子写盘。返回是否写盘成功。</summary>
        public static bool MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            PlayerProfileData data = Data;
            if (data.seenTutorialSteps.Contains(id)) return true;

            // 先更新内存：即使写盘失败，本次运行内也不要重复弹。
            data.seenTutorialSteps.Add(id);
            return Write(data);
        }

        public static bool MarkIntroWatched()
        {
            PlayerProfileData data = Data;
            bool changed = !data.hasWatchedIntro ||
                !data.seenTutorialSteps.Contains(TutorialStepIds.IntroVideo);
            data.hasWatchedIntro = true;
            if (!data.seenTutorialSteps.Contains(TutorialStepIds.IntroVideo))
                data.seenTutorialSteps.Add(TutorialStepIds.IntroVideo);
            return changed ? Write(data) : true;
        }

        /// <summary>QA 用：清空所有“第一次”记录（包括开场视频）。</summary>
        public static bool ResetTutorialProgress()
        {
            PlayerProfileData data = new PlayerProfileData
            {
                version = 1,
                hasWatchedIntro = false,
                seenTutorialSteps = new List<string>()
            };
            return Write(data);
        }

        private static void Normalize(PlayerProfileData data)
        {
            if (data.seenTutorialSteps == null)
                data.seenTutorialSteps = new List<string>();

            // 旧版 profile 只有 hasWatchedIntro，补进统一注册表。
            if (data.hasWatchedIntro && !data.seenTutorialSteps.Contains(TutorialStepIds.IntroVideo))
                data.seenTutorialSteps.Add(TutorialStepIds.IntroVideo);
        }

        private static PlayerProfileData Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var data = JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(path));
                return data != null && data.version == 1 ? data : null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning($"读取教程进度失败：{exception.Message}");
                return null;
            }
        }

        private static bool Write(PlayerProfileData data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TutorialProgressPath));
                File.WriteAllText(TutorialProgressPath + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(TutorialProgressPath))
                    File.Replace(TutorialProgressPath + ".tmp", TutorialProgressPath,
                        Read(TutorialProgressPath) != null ? TutorialProgressPath + ".bak" : null);
                else File.Move(TutorialProgressPath + ".tmp", TutorialProgressPath);
                _data = data;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"保存教程进度失败：{exception.Message}");
                return false;
            }
        }
    }
}
