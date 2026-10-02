using UnityEngine;
using TapTapGameJam.Events;
using TapTapGameJam.Types;

namespace TapTapGameJam.Core
{
    public class GamePauseManager : MonoBehaviour
    {
        public static GamePauseManager Instance
        {
            get;
            private set;
        }

        private bool _escOpen;
        private bool _backpackOpen;

        // 任意一个暂停原因存在，游戏就保持暂停。
        public bool IsPaused =>
            _escOpen || _backpackOpen;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<ESCMenuEvent>(
                HandleESCMenu
            );

            EventPipeline.Subscribe<BackpackEvent>(
                HandleBackpack
            );
        }

        private void Start()
        {
            RefreshPause();
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<ESCMenuEvent>(
                HandleESCMenu
            );

            EventPipeline.UnSubscribe<BackpackEvent>(
                HandleBackpack
            );
        }

        private void HandleESCMenu(ESCMenuEvent evt)
        {
            _escOpen = evt.IsOpen;
            RefreshPause();
        }

        private void HandleBackpack(BackpackEvent evt)
        {
            _backpackOpen = evt.IsOpen;
            RefreshPause();
        }

        private void RefreshPause()
        {
            // 本例正常游戏速度为 1，没有慢动作。
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;

            // 离开游戏场景时恢复正常时间。
            Time.timeScale = 1f;
        }
    }
}