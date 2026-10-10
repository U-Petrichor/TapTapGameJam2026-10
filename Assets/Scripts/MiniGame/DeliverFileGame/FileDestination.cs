using System.Collections.Generic;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using System;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class FileDestination : Interactable
    {
        public override string InteractionPrompt => "交出文件";
        [SerializeField] private Collider _detectCollider;
        [Tooltip("任务生成前必须满足的条件组件。每个组件都需要实现 ITaskSpawnCondition。")]
        [SerializeField] private List<MonoBehaviour> _spawnConditionObjects = new();
        public bool IsFree = true;

        public bool IsAvailable => TaskSpawnConditionUtility.TryEvaluate(_spawnConditionObjects, out _);
        public string UnavailableReason
        {
            get
            {
                TaskSpawnConditionUtility.TryEvaluate(_spawnConditionObjects, out var reason);
                return reason;
            }
        }
        public bool CanUse => IsFree && IsAvailable;

        public event Action<PlayerBasic> EventFileDestinationFinish;

        private void Awake()
        {
            _detectCollider.enabled = false;
        }
        public void StartTask()
        {
            // 任务开始之后激活检测器
            _detectCollider.enabled = true;
        }
        public override bool CanInteract(PlayerBasic player)
        {
            return player != null && isActiveAndEnabled && !IsFree &&
                _detectCollider != null && _detectCollider.enabled;
        }

        public override void Interact(PlayerBasic player)
        {
            if (!CanInteract(player)) return;
            ResetTask();
            EventFileDestinationFinish?.Invoke(player);
        }

        public void ResetTask()
        {
            ClearAllRegister();
            // 接取文件的部分结束了，关闭检测器

            if (_detectCollider != null) _detectCollider.enabled = false;
            IsFree = true;// 任务结束，重新变为空闲状态

        }
    }

}
