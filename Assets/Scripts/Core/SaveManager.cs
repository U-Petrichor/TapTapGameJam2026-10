using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using TapTapGameJam.Types;
using TapTapGameJam.Player;

namespace TapTapGameJam.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public List<TalentSaveEntry> talents;
        public List<ItemSaveEntry> items;
        public List<NestSaveEntry> nests;
        public float sharedNutrition;
        public bool hasFullCheckpoint;
        public bool hasCollectedAccessCard;
        public int version;
        public int dayIndex;
        public GamePhase phase;
        public int coins;
        public int nutrition;
        public int talentPoint;
        public bool hasDailySettlement;
        public int dailyCoinsEarned;
        public int dailyTalentEarned;
        public int dailyCoinsGift;
        public int dailyTalentGift;
    }

    /// <summary>单槽阶段检查点。阶段开始时捕获，保存退出时重试写盘，不保存中途资源。</summary>
    public static class SaveManager
    {
        public const int CurrentVersion = 3;
#if UNITY_EDITOR
        // Integration checks use a separate directory and never touch the player's save.
        public static string TestSaveDirectory;
#endif
        public static string SaveDirectory
        {
            get
            {
#if UNITY_EDITOR
                return TestSaveDirectory ??
                    Path.Combine(Application.persistentDataPath, "Save");
#elif UNITY_STANDALONE_WIN
                string gameDirectory = Path.GetDirectoryName(Application.dataPath) ??
                    Application.persistentDataPath;
                return Path.Combine(gameDirectory, "Save");
#else
                return Path.Combine(Application.persistentDataPath, "Save");
#endif
            }
        }
        public static string SavePath => Path.Combine(SaveDirectory, "save.json");
        private static string LegacySavePath
        {
            get
            {
#if UNITY_EDITOR
                if (TestSaveDirectory != null) return SavePath;
#endif
                return Path.Combine(Application.persistentDataPath, "save.json");
            }
        }
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
            if (TryReadFile(SavePath + ".bak", out data)) return true;

            // Migrate existing installs from persistentDataPath into the exported game's Save folder.
            if (!string.Equals(LegacySavePath, SavePath, StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadFile(LegacySavePath, out data) ||
                    TryReadFile(LegacySavePath + ".bak", out data))
                {
                    WriteCheckpoint(data);
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadFile(string path, out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var candidate = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (candidate == null || (candidate.version < 1 || candidate.version > CurrentVersion) || candidate.dayIndex < 1 ||
                    !Enum.IsDefined(typeof(GamePhase), candidate.phase) || candidate.coins < 0 ||
                    candidate.nutrition < 0 || candidate.talentPoint < 0 || candidate.dailyCoinsGift < 0 ||
                    candidate.dailyTalentGift < 0 || !CheckpointState.Validate(candidate))
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
                coins = resources.Coins, nutrition = Mathf.FloorToInt(resources.Nutrition),
                talentPoint = resources.TalentPoint,
                hasDailySettlement = true,
                dailyCoinsEarned = phases.DailyCoinsEarned,
                dailyTalentEarned = phases.DailyTalentEarned,
                dailyCoinsGift = phases.DailyCoinsGiftAmount,
                dailyTalentGift = phases.DailyTalentGiftAmount
            };
            try { CheckpointState.Capture(data, resources); }
            catch (Exception exception)
            {
                Debug.LogError($"捕获完整检查点失败，保留上一份存档：{exception}");
                return false;
            }
            if (!CheckpointState.Validate(data)) { Debug.LogError("检查点数据无效，保留上一份存档。"); return false; }
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
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
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
