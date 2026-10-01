using UnityEngine;
using TapTapGameJam.Types;
using TMPro.EditorUtilities;
using TapTapGameJam.Events;
using System.Threading.Tasks.Sources;

namespace TapTapGameJam.Core
{
    public class GamePhaseManager : MonoBehaviour
    {

        private const float DAY_DURATION = 60f; // 白天持续时间，单位秒
        private const float NIGHT_DURATION = 60f; // 夜晚持续时间，单位秒


        [field: SerializeField] public GamePhase CurrentPhase { get; private set; } = GamePhase.Day;
        public float RemainingTime = 0f; // 当前阶段剩余时间
        public int DayIndex = 1; // 当前是第几天，从1开始计数
        

        private void Start()
        {
            // TODO 暂时在这里初始化阶段为白天，后续要统一管理游戏进程
            EnterPhase(GamePhase.Day);
        }
        private void Update()
        {
            if(CurrentPhase == GamePhase.Shopping)
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
            if (CurrentPhase == GamePhase.Day)
            {
                EnterPhase(GamePhase.Shopping);
            }
            else if (CurrentPhase == GamePhase.Night)
            {
                DayIndex++;
                EnterPhase(GamePhase.Day);
            }
        }

        /// <summary>
        /// 只需要传入接下来要进入的阶段，方法会自动处理剩余时间和事件触发
        /// </summary>
        /// <param name="phase"></param>
        public void EnterPhase(GamePhase phase)
        {
            RemainingTime = phase switch
            {
                GamePhase.Day => DAY_DURATION,
                GamePhase.Night => NIGHT_DURATION,
                _ => 0f
            };
            // -------------------------------------------- ↓当前阶段  ↓传入的新阶段 ↓自动更新的日期
            EventPipeline.Publish(new GamePhaseChangedEvent(phase, DayIndex));
        }
        /// <summary>
        /// 购物时点击按钮主动结束，触发阶段切换事件
        /// </summary>
        public void FinishShopping()
        {
            if(CurrentPhase != GamePhase.Shopping)
            {
                Debug.LogError($"[GamePhaseManager] 当前阶段不是购物阶段，无法结束购物");
                return;
            }
            EnterPhase(GamePhase.Night);
        }
    }
}