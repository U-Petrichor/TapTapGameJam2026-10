using UnityEngine;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using TapTapGameJam.UI;

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
        private bool _doorRevealPlaying;
        private bool _tutorialOpen;

        /// <summary>
        /// 结局流程进行中。它不算"完全暂停"：时间流速压到
        /// <see cref="GameEndingDirector.EndingTimeScale"/>（0.1 倍速），
        /// 画面上还看得出被踩 / 被喷 / 成片消杀，但已经不是可玩状态。
        /// </summary>
        private bool _endingActive;

        // 任意一个暂停原因存在，游戏就保持暂停。
        // "有没有 UI 面板开着"不再自己记一份：去问 UIManager —— 它才知道受监控容器的开/关，
        // 这样仓库、以后的商店也一起算数，不用在暂停这边一个个订阅。
        public bool IsPaused =>
            IntroVideoTransition.IsTransitioning || _escOpen || UIManager.Instance.AnyContainerOpen ||
            _doorRevealPlaying || _tutorialOpen;

        public bool IsEnding => _endingActive;

        /// <summary>
        /// 进入 / 退出结局流程。优先级高于其它暂停原因：
        /// 结局已经开始，就不该因为某个面板还开着而被冻回 0。
        /// </summary>
        public void SetGameEnding(bool active)
        {
            _endingActive = active;
            RefreshPause();
        }

        public void SetDoorRevealPlaying(bool playing)
        {
            _doorRevealPlaying = playing;
            RefreshPause();
        }

        public void SetTutorialOpen(bool open)
        {
            _tutorialOpen = open;
            RefreshPause();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            EventPipeline.Subscribe<ESCMenuEvent>(
                HandleESCMenu
            );

            // 容器开关状态变了只是"触发器"，真正的状态在 RefreshPause 里问 UIManager
            EventPipeline.Subscribe<EventContainerStateChanged>(
                HandleContainerStateChanged
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

            EventPipeline.UnSubscribe<EventContainerStateChanged>(
                HandleContainerStateChanged
            );
        }

        private void HandleESCMenu(ESCMenuEvent evt)
        {
            _escOpen = evt.IsOpen;
            RefreshPause();
        }

        private void HandleContainerStateChanged(EventContainerStateChanged evt)
        {
            RefreshPause();
        }

        public void RefreshPause()
        {
            // 结局期间统一压到 0.1 倍速（不彻底冻死）；其余情况正常游戏是 1、暂停是 0。
            Time.timeScale = _endingActive
                ? GameEndingDirector.EndingTimeScale
                : IsPaused ? 0f : 1f;
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
