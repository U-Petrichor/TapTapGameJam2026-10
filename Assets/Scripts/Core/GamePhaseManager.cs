using System;
using System.Collections;
using TapTapGameJam.Player;
using UnityEngine;
using TapTapGameJam.Types;
using TapTapGameJam.Events;

namespace TapTapGameJam.Core
{
    public class GamePhaseManager : MonoBehaviour
    {

        [SerializeField] private const float DAY_DURATION = 90f; // 白天持续时间，单位秒
        [SerializeField] private const float NIGHT_DURATION = 330f; // 夜晚持续时间，单位秒
        public bool TimeCouldWork = false;

        [field: SerializeField] public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;
        public float RemainingTime = 0f; // 当前阶段剩余时间
        public int DayIndex = 0; // 已开始的白天数；进入首日后为 1，与门的开放日期一致。
        [Tooltip("每次进入白天或夜晚时，玩家回到的世界坐标。")]
        [SerializeField] private Vector3 _phaseStartPosition = new Vector3(32.51f, 0f, 20.76f);
        private PlayerResources _saveResources;
        private bool _initialized;
        
        public float DayProcess => CurrentPhase == GamePhase.Day ? RemainingTime / DAY_DURATION : -1.0f;
        public float NightProcess => CurrentPhase == GamePhase.Night ? RemainingTime / NIGHT_DURATION : -1.0f;

        private void OnEnable()
        {
            EventPipeline.Subscribe<ShoppingFinishedEvent>(HandleShoppingFinished);
            EventPipeline.Subscribe<NightTransitionCompletedEvent>(HandleNightReady);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<ShoppingFinishedEvent>(HandleShoppingFinished);
            EventPipeline.UnSubscribe<NightTransitionCompletedEvent>(HandleNightReady);
        }

        private void HandleShoppingFinished(ShoppingFinishedEvent evt)
        {
            if (CurrentPhase == GamePhase.Shopping) EnterPhase(GamePhase.Night);
        }

        private void HandleNightReady(NightTransitionCompletedEvent evt)
        {
            if (CurrentPhase == GamePhase.Night) TimeCouldWork = true;
        }

        private IEnumerator Start()
        {
            TimeCouldWork = false;
            // 等场景中各组件 Start 完成，防止初始化覆盖恢复的数据。
            yield return null;
            _saveResources = FindFirstObjectByType<PlayerBasic>()?.PlayerResources;
            if (_saveResources == null) _saveResources = FindFirstObjectByType<PlayerResources>();
            var data = SaveManager.ConsumePendingLoad();
            if (data != null && _saveResources != null)
            {
                DayIndex = data.dayIndex;
                CurrentPhase = data.phase;
                RemainingTime = data.phase == GamePhase.Day ? DAY_DURATION :
                    data.phase == GamePhase.Night ? NIGHT_DURATION : 0f;
                TimeCouldWork = data.phase != GamePhase.Shopping;
                _saveResources.RestoreResources(data.coins, data.nutrition, data.talentPoint);
                ResetPlayerPosition(CurrentPhase);
                // 重建当天的场景条件，不调用 EnterPhase，因此不会增加天数。
                EventPipeline.Publish(new EventNewDay(DayIndex));
                EventPipeline.Publish(new GamePhaseChangedEvent(CurrentPhase));
                _initialized = true;
            }
            else
            {
                _initialized = true;
                EnterPhase(GamePhase.Day);
            }
        }
        private void Update()
        {
            if(!_initialized || TimeCouldWork == false)
            {
                return;
            }
            if (CurrentPhase == GamePhase.Shopping)
            {
                // 如果是购物阶段，就没有倒计时
                return;
            }
            // 防止倒计时为负数
            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);

            
            if (RemainingTime>0f)
            {
                // 游戏还没结束就不触发阶段切换事件
                return;
            }

            // 阶段结束，进入下一个阶段
            NextPhase();
        }

        public void NextPhase()
        {
            EnterPhase(CurrentPhase switch
            {
                GamePhase.Day => GamePhase.Shopping,
                GamePhase.Shopping => GamePhase.Night,
                _ => GamePhase.Day
            });
        }
        
        /// <summary>
        /// 只需要传入接下来要进入的阶段，方法会自动处理剩余时间和事件触发
        /// </summary>
        /// <param name="phase"></param>
        public void EnterPhase(GamePhase phase)
        {
            Debug.Log($"<color=#00ff00>进入了新的阶段{phase}”</color>");
            switch (phase)
            {
                case GamePhase.Day:
                {
                    DayIndex++;
                    TimeCouldWork = true;
                    RemainingTime = DAY_DURATION;
                    break;
                }
                case GamePhase.Shopping:
                {
                    TimeCouldWork = false;
                    break;
                }
                case GamePhase.Night:
                {
                    TimeCouldWork = true;
                    RemainingTime = NIGHT_DURATION;
                    break;
                }
                default:
                {
                    Debug.LogError($"出现意料之外的阶段");
                    break;
                }
            }
            CurrentPhase = phase;
            ResetPlayerPosition(phase);
            EventPipeline.Publish(new GamePhaseChangedEvent(phase));
            if (phase == GamePhase.Day)
                EventPipeline.Publish(new EventNewDay(DayIndex));
            if (_initialized) SaveManager.SaveCheckpoint(this, _saveResources);
        }

        private void ResetPlayerPosition(GamePhase phase)
        {
            if (phase != GamePhase.Day && phase != GamePhase.Night) return;

            PlayerBasic player = GameManager.Player;
            if (player == null) player = FindFirstObjectByType<PlayerBasic>();
            if (player == null) return;

            if (player.PlayerMovement != null)
            {
                player.PlayerMovement.SetMoveInput(Vector2.zero);
                player.PlayerMovement.IsSprinting = false;
            }

            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = _phaseStartPosition;
            }
            player.transform.position = _phaseStartPosition;
            Physics.SyncTransforms();
        }
        /// <summary>
        /// 购物时点击按钮主动结束，触发阶段切换事件
        /// </summary>
    }
}
