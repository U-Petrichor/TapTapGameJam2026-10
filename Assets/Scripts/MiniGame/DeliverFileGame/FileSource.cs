using System.Collections.Generic;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using System;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class FileSource : Interactable
    {
        public override string InteractionPrompt => "接取文件";
        [SerializeField] private Collider _detectCollider;
        [SerializeField] private GameObject _filePrefab;
        private GameObject _spawnedFile;
        public bool IsFree = true;

        [Tooltip("额外的任务生成条件组件。每个组件都需要实现 ITaskSpawnCondition。")]
        [SerializeField] private List<MonoBehaviour> _spawnConditionObjects = new();
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
        public event Action EventFileSourceFinish;

        private void Awake()
        {
            _detectCollider.enabled = false;
        }
        public void StartTask()
        {
            // 并且生成文件
            SpawnPrefab();
            // 任务开始之后激活检测器
            _detectCollider.enabled = true;       
        }
        private void SpawnPrefab()
        {
            if (_filePrefab != null)
            {
                _spawnedFile = Instantiate(_filePrefab, transform, false);
                _spawnedFile.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogError($"[FileSource]:没有给定文件预制体");
            }
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
            EventFileSourceFinish?.Invoke();
        }

        public void ResetTask()
        {
            // 清理点位不发送完成事件，取消任务时也可以安全调用。
            ClearAllRegister();
            // 接取文件的部分结束了，关闭检测器

            if (_detectCollider != null) _detectCollider.enabled = false;
            IsFree = true;// 任务结束，重新变为空闲状态
            if (_spawnedFile != null)
            {
                Destroy(_spawnedFile);
                _spawnedFile = null;
            }
        }
    }

}
