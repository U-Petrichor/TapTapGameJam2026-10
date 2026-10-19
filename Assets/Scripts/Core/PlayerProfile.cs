using System;
using System.IO;
using UnityEngine;

namespace TapTapGameJam.Core
{
    [Serializable]
    public sealed class PlayerProfileData
    {
        public int version;
        public bool hasWatchedIntro;
    }

    /// <summary>跨周目保留的玩家记录，不随新游戏清除。</summary>
    public static class PlayerProfile
    {
        public static string ProfilePath => Path.Combine(Application.persistentDataPath, "profile.json");
        private static PlayerProfileData _data;
        public static bool HasWatchedIntro => Data.hasWatchedIntro;

        private static PlayerProfileData Data => _data ??= Read(ProfilePath) ??
            Read(ProfilePath + ".bak") ?? new PlayerProfileData { version = 1 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _data = null;

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

        public static bool MarkIntroWatched()
        {
            if (Data.hasWatchedIntro) return true;
            var updated = new PlayerProfileData { version = 1, hasWatchedIntro = true };
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(ProfilePath + ".tmp", JsonUtility.ToJson(updated, true));
                if (File.Exists(ProfilePath))
                    File.Replace(ProfilePath + ".tmp", ProfilePath,
                        Read(ProfilePath) != null ? ProfilePath + ".bak" : null);
                else File.Move(ProfilePath + ".tmp", ProfilePath);
                _data = updated;
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
