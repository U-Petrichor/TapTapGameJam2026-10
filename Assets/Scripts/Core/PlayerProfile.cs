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

    /// <summary>跨周目保留的玩家记录，不随新游戏清除。</summary>
    public static class PlayerProfile
    {
        public static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");
        private static PlayerProfileData _data;
        public static bool HasWatchedIntro => HasSeen(TutorialStepIds.IntroVideo);

        private static PlayerProfileData Data
        {
            get
            {
                if (_data == null)
                {
                    _data = Read(ProfilePath) ?? Read(ProfilePath + ".bak") ??
                        new PlayerProfileData { version = 1 };
                    Normalize(_data);
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
                Debug.LogWarning($"读取玩家全局存档失败：{exception.Message}");
                return null;
            }
        }

        private static bool Write(PlayerProfileData data)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(ProfilePath + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(ProfilePath))
                    File.Replace(ProfilePath + ".tmp", ProfilePath,
                        Read(ProfilePath) != null ? ProfilePath + ".bak" : null);
                else File.Move(ProfilePath + ".tmp", ProfilePath);
                _data = data;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"保存玩家全局存档失败：{exception.Message}");
                return false;
            }
        }
    }
}
