using System;
using System.Collections.Generic;
using UnityEngine;

namespace TapTapGameJam.Tutorial
{
    public enum TutorialSpeaker
    {
        Self,
        Boss,
        System
    }

    public enum TutorialPresentation
    {
        Modal,
        HighlightOnly
    }

    public enum TutorialHighlightTarget
    {
        None,
        Coins,
        Countdown
    }

    [Serializable]
    public sealed class TutorialStep
    {
        [Tooltip("持久化 ID，发布后不要改名")]
        public string id;
        [Tooltip("信号名，或 after:上一步ID")]
        public string trigger;
        [Tooltip("这些步骤看过后，本步骤才允许出现")]
        public List<string> requires = new List<string>();
        public TutorialSpeaker speaker = TutorialSpeaker.Self;
        [TextArea(3, 10)] public string text;
        public TutorialPresentation presentation = TutorialPresentation.Modal;
        public TutorialHighlightTarget highlight = TutorialHighlightTarget.None;
        [Tooltip("Modal 步骤是否暂停游戏")]
        public bool pauseGame = true;
        [Tooltip("HighlightOnly 步骤的自动结束时间；0 表示用默认值")]
        [Min(0f)] public float autoCloseSeconds;
        [Tooltip("弹出瞬间就记录已看，不等玩家关闭")]
        public bool markOnShow;
        [Tooltip("关闭后额外发出的信号")]
        public string emitOnClose;
    }

    [CreateAssetMenu(menuName = "TapTapGameJam/Tutorial Sequence", fileName = "TutorialSequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

        public IReadOnlyList<TutorialStep> Steps => steps;

        public void SetSteps(IEnumerable<TutorialStep> source)
        {
            steps = source != null ? new List<TutorialStep>(source) : new List<TutorialStep>();
        }

        public bool TryGet(string id, out TutorialStep step)
        {
            if (!string.IsNullOrEmpty(id))
            {
                foreach (TutorialStep item in steps)
                {
                    if (item != null && item.id == id)
                    {
                        step = item;
                        return true;
                    }
                }
            }

            step = null;
            return false;
        }
    }
}
