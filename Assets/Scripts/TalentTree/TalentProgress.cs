using System;
using System.Collections.Generic;
using UnityEngine;

namespace TalentTree
{
    [Serializable]
    public sealed class TalentProgress : ISerializationCallbackReceiver
    {
        [Serializable]
        private sealed class Entry
        {
            public TalentData Talent;
            public int Level;
        }

        [SerializeField] private List<Entry> _entries = new();
        [NonSerialized] private Dictionary<TalentData, int> _levels = new();

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            RebuildCache();
        }

        // 重新读取
        public void RebuildCache()
        {
            _levels ??= new Dictionary<TalentData, int>();
            _levels.Clear();
            _entries ??= new List<Entry>();
            foreach (Entry entry in _entries)
            {
                // 如果List中已经记录了此天赋，并且等级大于0，则写入字典
                if (entry?.Talent != null && entry.Level > 0)
                    _levels[entry.Talent] = entry.Level;
            }
        }

        public int GetLevel(TalentData talent)
        {
            if (talent == null) return 0;
            if (_levels == null) RebuildCache();
            return _levels.TryGetValue(talent, out int level) ? level : 0;
        }

        /// <summary>
        /// 传入天赋与此天赋的目标level
        /// </summary>
        /// <param name="talent">传入的天赋内容</param>
        /// <param name="level">此天赋的目标level</param>
        public void SetLevel(TalentData talent, int level)
        {
            if (talent == null) return;
            if (_levels == null)
            {
                // 如果当前没有字典，需要重新读取已序列化的List
                RebuildCache();
            }
            level = Math.Clamp(level, 0, talent.MaxLevel);
            _levels[talent] = level;

            Entry entry = _entries.Find(item => item.Talent == talent);
            if (entry == null)
            {
                if (level > 0) 
                    _entries.Add(new Entry { Talent = talent, Level = level });
            }
            else if (level == 0)
            {
                _entries.Remove(entry);
            }
            else
            {
                entry.Level = level;
            }
        }

        public IEnumerable<(TalentData Talent, int Level)> Enumerate()
        {
            foreach (Entry entry in _entries)
            {
                if (entry?.Talent != null && entry.Level > 0)
                    yield return (entry.Talent, entry.Level);
            }
        }
    }
}
