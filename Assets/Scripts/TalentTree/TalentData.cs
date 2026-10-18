using System.Collections.Generic;
using UnityEngine;

namespace TalentTree
{
    /// <summary>单个天赋的静态定义。玩家当前等级由 PlayerTalentTree 保存。</summary>
    [CreateAssetMenu(menuName = "Talent Tree/Talent", fileName = "Talent")]
    public sealed class TalentData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _talentType;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;
        [SerializeField] private List<TalentLevelData> _levels = new();

        public string Id => _id;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string TalentType => _talentType;
        public string Description => _description;
        public Sprite Icon => _icon;
        public IReadOnlyList<TalentLevelData> Levels => _levels;
        public int MaxLevel => _levels?.Count ?? 0;

        public TalentLevelData GetLevelData(int level)
        {
            if (_levels == null || level < 1 || level > _levels.Count) return null;
            return _levels[level - 1];
        }

        public void SetIdentity(string id, string displayName)
        {
            _id = id;
            _displayName = displayName;
            SyncObjectName();
        }

        private void OnValidate() => SyncObjectName();

        public void SetTalentType(string talentType) => _talentType = talentType;
        public void SetDescription(string description) => _description = description;

        private void SyncObjectName()
        {
            // Unity's ObjectField uses ScriptableObject.name for sub-assets. Keep it
            // populated so talent references can be identified in editor pickers.
            string objectName = !string.IsNullOrWhiteSpace(_displayName) ? _displayName : _id;
            if (string.IsNullOrWhiteSpace(objectName)) objectName = "Talent";
            if (name != objectName) name = objectName;
        }

        public List<TalentLevelData> MutableLevels => _levels ??= new List<TalentLevelData>();

        public TalentLevelData AddLevel()
        {
            TalentLevelData level = new();
            MutableLevels.Add(level);
            return level;
        }
    }
}
