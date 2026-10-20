using System;
using System.IO;
using UnityEngine;
using TapTapGameJam.Types;
using TapTapGameJam.Player;

namespace TapTapGameJam.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int version;
        public int dayIndex;
        public GamePhase phase;
        public int coins;
        public int nutrition;
        public int talentPoint;
        public bool hasDailySettlement;
        public int dailyCoinsEarned;
        public int dailyTalentEarned;
    }

    /// <summary>单槽阶段检查点。阶段开始时捕获，保存退出时重试写盘，不保存中途资源。</summary>
    public static class SaveManager
    {
        public const int CurrentVersion = 1;
        public static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private static SaveData _pendingLoad;
        private static SaveData _latestCheckpoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { _pendingLoad = null; _latestCheckpoint = null; }

        public static void PrepareNewGame() { _pendingLoad = null; _latestCheckpoint = null; }

        public static bool PrepareContinue()
        {
            _pendingLoad = null;
            if (!TryRead(out var data)) return false;
            _pendingLoad = data;
            _latestCheckpoint = data;
            return true;
        }

        public static SaveData ConsumePendingLoad()
        {
            var data = _pendingLoad;
            _pendingLoad = null;
            return data;
        }

        public static bool TryRead(out SaveData data)
        {
            if (TryReadFile(SavePath, out data)) return true;
            return TryReadFile(SavePath + ".bak", out data);
        }

        private static bool TryReadFile(string path, out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var candidate = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (candidate == null || candidate.version != CurrentVersion || candidate.dayIndex < 1 ||
                    !Enum.IsDefined(typeof(GamePhase), candidate.phase) || candidate.coins < 0 ||
                    candidate.nutrition < 0 || candidate.talentPoint < 0)
                    return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Debug.LogWarning($"读取存档失败：{exception.Message}");
                return false;
            }
        }

        public static bool SaveCheckpoint(GamePhaseManager phases, PlayerResources resources)
        {
            if (phases == null || resources == null || phases.DayIndex < 1) return false;
            var data = new SaveData
            {
                version = CurrentVersion, dayIndex = phases.DayIndex, phase = phases.CurrentPhase,
                coins = resources.Coins, nutrition = resources.Nutrition,
                talentPoint = resources.TalentPoint,
                hasDailySettlement = true,
                dailyCoinsEarned = phases.DailyCoinsEarned,
                dailyTalentEarned = phases.DailyTalentEarned
            };
            _latestCheckpoint = data;
            return WriteCheckpoint(data);
        }

        // Saving partial resources without the rest of the world would duplicate rewards
        // when continuing at the beginning of the phase. Persist the captured checkpoint.
        public static bool SaveLatestCheckpoint() => _latestCheckpoint != null && WriteCheckpoint(_latestCheckpoint);

        private static bool WriteCheckpoint(SaveData data)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(SavePath + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(SavePath))
                {
                    // 不让损坏的主文件覆盖最后一份可用备份。
                    if (TryReadFile(SavePath, out _))
                        File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
                    else
                        File.Replace(SavePath + ".tmp", SavePath, null);
                }
                else File.Move(SavePath + ".tmp", SavePath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError($"保存检查点失败：{exception.Message}");
                return false;
            }
        }
    }
}
