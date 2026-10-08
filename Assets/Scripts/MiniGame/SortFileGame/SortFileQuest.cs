using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class SortFileQuest : SinglePointQuest
    {
        [SerializeField] private SortFile _sortFile;
        protected override PanelTaskPoint Point => _sortFile;
        private void Awake()
        {
            if (_sortFile == null)
            {
                _sortFile = GetComponent<SortFile>();
            }
        }
        protected override PanelMiniGameInstance CreateInstance() => new SortFileInstance(_sortFile, _rewardCoins, _rewardTalentPoints);
    }
}