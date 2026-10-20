using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 广播播放器（单例）。按 <see cref="BroadcastKind"/> 播一段视频，播完自动把自己藏起来。
    ///
    /// 用法：
    /// <code>
    /// BroadcastUI.Broadcast(BroadcastKind.ReturnNest);   // 播完自动隐藏；没配这条会在 Console 报警
    /// BroadcastUI.Stop();                                // 手动打断
    /// if (BroadcastUI.IsPlaying) { ... }                 // 有广播在播
    /// </code>
    ///
    /// 【新广播打断旧的】<see cref="Play"/> 一开始就先把上一条停掉再起新的，
    /// 所以连发多条时永远是最后一条说了算。
    ///
    /// 【为什么组件所在的物体不能被 SetActive(false)】
    /// <see cref="VideoPlayer"/> 就挂在这个物体上（见 <see cref="RequireComponent"/>），
    /// 物体一关，VideoPlayer 停了、单例也查不到了（<c>FindFirstObjectByType</c> 找不到未激活的物体）。
    /// 所以「隐藏」只关**画面**：优先关 <see cref="_viewRoot"/> 那个子物体（推荐做法，符合项目的
    /// UI 约定：作者在 Hierarchy 里摆好两套状态，代码只开关分组），
    /// 没配就退化成开关 <see cref="_display"/> 这个 RawImage ——
    /// <c>BroadcastPanel.prefab</c> 就是后者（RawImage 和 VideoPlayer 在同一个物体上）。
    ///
    /// 【预制体】Assets/Prefabs/UI/BroadcastPanel.prefab 已经接好了：
    /// VideoPlayer + RawImage + 本组件的引用都在，映射表也配满了 5 条。
    /// 把它拖进场景里挂在某个 Canvas 下面即可（它是一个 576×324 的 UI 面板）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class BroadcastUI : MonoBehaviour
    {
        /// <summary>一条「广播种类 → 视频」的映射。Unity 不能序列化 Dictionary，所以用 List 配、运行时转字典。</summary>
        [Serializable]
        public struct BroadcastEntry
        {
            [Tooltip("广播种类")]
            public BroadcastKind kind;

            [Tooltip("对应的视频资源")]
            public VideoClip clip;
        }

        private static BroadcastUI _instance;

        /// <summary>场景里的那一个。没有玩家/场景没搭时返回 null，不会自动创建。</summary>
        public static BroadcastUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BroadcastUI>();
                }

                return _instance;
            }
        }

        /// <summary>当前有没有广播在播。</summary>
        public static bool IsPlaying => _instance != null && _instance._playing;

        #region Inspector

        [Header("视频")]
        [Tooltip("播视频用的 VideoPlayer。留空则取自己身上的（推荐留空）")]
        [SerializeField] private VideoPlayer _videoPlayer;

        [Tooltip("广播种类 → 视频。同一种类配了多条时以第一条为准")]
        [SerializeField] private List<BroadcastEntry> _clips = new List<BroadcastEntry>();

        [Header("显示")]
        [Tooltip("显示画面的 RawImage。留空则从子物体里找")]
        [SerializeField] private RawImage _display;

        [Tooltip("整块广播画面的根物体，播完把它关掉。留空则用 RawImage 自己所在的物体")]
        [SerializeField] private GameObject _viewRoot;

        [Tooltip("视频渲染目标（建议用 Assets/UI/播报特效/R.renderTexture）。" +
                 "留空则用 VideoPlayer 上已配的；都没有就按视频尺寸自动建一个")]
        [SerializeField] private RenderTexture _renderTexture;

        [Header("行为")]
        [Tooltip("播完自动隐藏")]
        [SerializeField] private bool _hideWhenFinished = true;

        [Tooltip("播放期间隐藏鼠标指针，恢复时还原成播放前的状态")]
        [SerializeField] private bool _hideCursorWhilePlaying;

        [Tooltip("准备超时（秒）。视频一直准备不好就当它失败，收场，避免画面卡在广播上")]
        [SerializeField, Min(1f)] private float _prepareTimeout = 8f;

        #endregion

        private readonly Dictionary<BroadcastKind, VideoClip> _map =
            new Dictionary<BroadcastKind, VideoClip>();

        /// <summary>「场景里没有 BroadcastUI」只报一次，否则会刷屏</summary>
        private static bool _warnedNoInstance;

        private bool _mapBuilt;
        private Coroutine _playback;
        private bool _playing;
        private bool _finished;

        /// <summary>RenderTexture 是自己建的话，销毁时自己收</summary>
        private bool _ownsRenderTexture;

        private bool _cursorRestoreVisible = true;

        #region 生命周期

        private void Awake()
        {
            _instance = this;

            if (_videoPlayer == null)
            {
                _videoPlayer = GetComponent<VideoPlayer>();
            }

            if (_display == null)
            {
                // true：连未激活的子物体一起找 —— ViewRoot 初始就是关着的
                _display = GetComponentInChildren<RawImage>(true);
            }

            if (_viewRoot == null && _display != null && _display.gameObject != gameObject)
            {
                _viewRoot = _display.gameObject;
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.playOnAwake = false;
                _videoPlayer.isLooping = false;
                _videoPlayer.Stop();

                // 视频带声音时，VideoPlayer 的 audioOutputMode 要指到 AudioSource；
                // 没配的话是"有画面没声音"，很难一眼看出来
                if (_videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource &&
                    GetComponent<AudioSource>() == null)
                {
                    Debug.LogWarning(
                        $"[BroadcastUI:{name}]：VideoPlayer 的音频输出指向 AudioSource，但身上没有 AudioSource，" +
                        "视频会没有声音。", this);
                }
            }
            else
            {
                Debug.LogError($"[BroadcastUI:{name}]：身上没有 VideoPlayer，广播播不了。", this);
            }

            if (_viewRoot == null && _display == null)
            {
                Debug.LogWarning(
                    $"[BroadcastUI:{name}]：既没有配 _viewRoot 也没有 _display，" +
                    "播完没法隐藏画面。把整个广播画面的根物体拖到 _viewRoot 上。", this);
            }

            BuildMap();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            StopInternal(false);

            if (_ownsRenderTexture && _renderTexture != null)
            {
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        private void OnDisable()
        {
            // 组件被关掉时别把光标留在隐藏状态
            RestoreCursor();
        }

        #endregion

        #region 对外接口

        /// <summary>
        /// 播一条广播。会把正在播的那条打断。
        /// 返回是否真的开始播（没配这条的视频、没配 VideoPlayer 都会返回 false 并打 warning）。
        /// </summary>
        public static bool Broadcast(BroadcastKind kind)
        {
            BroadcastUI ui = Instance;
            if (ui == null)
            {
                if (!_warnedNoInstance)
                {
                    _warnedNoInstance = true;
                    Debug.LogWarning(
                        $"[BroadcastUI]：场景里没有 BroadcastUI，广播 {kind} 被忽略（本局只提示这一次）。");
                }

                return false;
            }

            return ui.Play(kind);
        }

        /// <summary>立刻停止当前广播并隐藏画面。</summary>
        public static void Stop()
        {
            BroadcastUI ui = Instance;
            if (ui != null)
            {
                ui.StopInternal(true);
            }
        }

        /// <summary>播一条广播。返回是否真的开始播。</summary>
        public bool Play(BroadcastKind kind)
        {
            EnsureMap();

            if (!_map.TryGetValue(kind, out VideoClip clip) || clip == null)
            {
                Debug.LogWarning(
                    $"[BroadcastUI]：没有配置广播 {kind} 的视频。已配置的有：{DescribeKeys()}", this);
                return false;
            }

            if (_videoPlayer == null)
            {
                Debug.LogError($"[BroadcastUI:{name}]：身上没有 VideoPlayer，广播播不了。", this);
                return false;
            }

            // 新广播打断旧的：先停干净，但**不要**隐藏画面（马上要播新的）
            StopInternal(false);

            _playback = StartCoroutine(PlayRoutine(clip));
            return true;
        }

        #endregion

        #region 播放

        private IEnumerator PlayRoutine(VideoClip clip)
        {
            _playing = true;
            _finished = false;

            SetVisible(true);
            ApplyCursorHidden(true);
            ApplyRenderTarget(clip);

            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = clip;
            _videoPlayer.isLooping = false;
            // 用不受 timeScale 影响的时间轴：暂停游戏时广播照样播
            _videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;

            _videoPlayer.loopPointReached += HandleLoopPointReached;
            _videoPlayer.errorReceived += HandleError;
            _videoPlayer.Prepare();

            float startTime = Time.realtimeSinceStartup;
            // 准备 + 播放 的总预算：准备超时 + 视频本身的长度（再留一倍余量）
            float budget = _prepareTimeout + Mathf.Max(1f, (float)clip.length * 2f);
            bool started = false;

            while (!_finished)
            {
                if (!started && _videoPlayer.isPrepared)
                {
                    _videoPlayer.Play();
                    started = true;
                }

                if (Time.realtimeSinceStartup - startTime > budget)
                {
                    Debug.LogWarning(
                        $"[BroadcastUI]：「{clip.name}」播放超时（{budget:F1}s 还没结束），强制收场。", this);
                    break;
                }

                yield return null;
            }

            FinishPlayback();
        }

        /// <summary>收场：解事件、停视频、恢复光标、按配置隐藏画面。</summary>
        private void FinishPlayback()
        {
            _playback = null;

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleError;
                _videoPlayer.Stop();
            }

            _playing = false;
            RestoreCursor();

            if (_hideWhenFinished)
            {
                SetVisible(false);
            }
        }

        /// <summary>打断 / 停止。hide = 是否顺手把画面也关掉（打断后马上要播新的，就别关）。</summary>
        private void StopInternal(bool hide)
        {
            if (_playback != null)
            {
                StopCoroutine(_playback);
                _playback = null;
            }

            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= HandleLoopPointReached;
                _videoPlayer.errorReceived -= HandleError;
                _videoPlayer.Stop();
            }

            _playing = false;
            _finished = true;
            RestoreCursor();

            if (hide)
            {
                SetVisible(false);
            }
        }

        private void HandleLoopPointReached(VideoPlayer player) => _finished = true;

        private void HandleError(VideoPlayer player, string message)
        {
            Debug.LogWarning($"[BroadcastUI]：视频播放出错：{message}", this);
            _finished = true;
        }

        #endregion

        #region 渲染目标

        /// <summary>
        /// 把视频渲到 RawImage 上。
        /// 优先用 Inspector 配的 RenderTexture，其次用 VideoPlayer 上已经配好的，
        /// 都没有才按视频尺寸现建一张（这种会由自己销毁）。
        /// </summary>
        private void ApplyRenderTarget(VideoClip clip)
        {
            if (_videoPlayer == null)
            {
                return;
            }

            if (_renderTexture == null)
            {
                _renderTexture = _videoPlayer.targetTexture;
            }

            if (_renderTexture == null)
            {
                int width = clip != null && clip.width > 0 ? (int)clip.width : 1920;
                int height = clip != null && clip.height > 0 ? (int)clip.height : 1080;

                _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "BroadcastRT",
                };
                _renderTexture.Create();
                _ownsRenderTexture = true;
            }

            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;

            if (_display != null)
            {
                _display.texture = _renderTexture;
            }
        }

        #endregion

        #region 显示 / 光标

        /// <summary>
        /// 只开关画面，<b>不动本物体</b>：VideoPlayer 在这个物体上，
        /// 物体一关视频就停了，单例也查不到了。
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (_viewRoot != null && _viewRoot != gameObject)
            {
                if (_viewRoot.activeSelf != visible)
                {
                    _viewRoot.SetActive(visible);
                }
            }

            if (_display != null && _display.enabled != visible)
            {
                _display.enabled = visible;
            }
        }

        private void ApplyCursorHidden(bool hidden)
        {
            if (!_hideCursorWhilePlaying)
            {
                return;
            }

            if (hidden)
            {
                _cursorRestoreVisible = Cursor.visible;
                Cursor.visible = false;
            }
        }

        private void RestoreCursor()
        {
            if (!_hideCursorWhilePlaying)
            {
                return;
            }

            Cursor.visible = _cursorRestoreVisible;
        }

        #endregion

        #region 映射表

        private void BuildMap()
        {
            _map.Clear();

            if (_clips == null)
            {
                _mapBuilt = true;
                return;
            }

            for (int i = 0; i < _clips.Count; i++)
            {
                BroadcastEntry entry = _clips[i];

                if (_map.ContainsKey(entry.kind))
                {
                    // 同一种类配了多条：以第一条为准，但要让人看见配错了
                    Debug.LogWarning($"[BroadcastUI]：广播 {entry.kind} 重复配置了，后面的那条被忽略。", this);
                    continue;
                }

                _map[entry.kind] = entry.clip;
            }

            _mapBuilt = true;
        }

        private void EnsureMap()
        {
            if (!_mapBuilt)
            {
                BuildMap();
            }
        }

        private string DescribeKeys()
        {
            if (_map.Count == 0)
            {
                return "（一条都没配）";
            }

            string text = string.Empty;
            foreach (KeyValuePair<BroadcastKind, VideoClip> pair in _map)
            {
                if (text.Length > 0)
                {
                    text += "、";
                }

                text += pair.Value != null ? pair.Key.ToString() : pair.Key + "(视频为空)";
            }

            return text;
        }

        #endregion

        #region 调试

        [ContextMenu("测试：播放列表里的第一条")]
        private void DebugPlayFirst()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BroadcastUI]：播放要在 Play 模式里测。");
                return;
            }

            if (_clips == null || _clips.Count == 0)
            {
                Debug.LogWarning("[BroadcastUI]：列表是空的，没得播。", this);
                return;
            }

            Play(_clips[0].kind);
        }

        [ContextMenu("测试：停止广播")]
        private void DebugStop()
        {
            StopInternal(true);
        }

        #endregion
    }
}
