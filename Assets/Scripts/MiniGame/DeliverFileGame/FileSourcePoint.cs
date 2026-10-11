using System.Collections.Generic;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using System;
using UnityEngine;

namespace TapTapGameJam.MiniGame
{
    public class FileSourcePoint : Interactable
    {
        public override string InteractionPrompt => "接取文件";
        [SerializeField] private Collider _detectCollider;
        [SerializeField] private GameObject _filePrefab;
        private GameObject _spawnedFile;

        [Header("文件手持姿态")]
        [SerializeField] private Vector3 _carryLocalPosition = new Vector3(-0.0621f, 0.028f, -0.1047f);
        [SerializeField] private Vector3 _carryLocalEulerAngles = new Vector3(-105.601f, -687.739f, 483.525f);
        [SerializeField] private Vector3 _carryLocalScale = new Vector3(0.3f, 0.3f, 0.3f);
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
        public event Action<PlayerBasic> EventFileSourceFinish;

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
                Debug.LogError($"[FileSourcePoint]:没有给定文件预制体");
            }
        }
        public override bool CanInteract(PlayerBasic player)
        {
            return player != null && player.CompareTag("Player") && player.CarryController != null &&
                !player.IsCarry && isActiveAndEnabled && !IsFree &&
                _detectCollider != null && _detectCollider.enabled;
        }

        public override void Interact(PlayerBasic player, InteractKey key)
        {
            if (!CanInteract(player)) return;

            GameObject file = _spawnedFile;
            Transform anchor = player.CarryController.GetCarryAnchor();
            if (file == null || anchor == null) return;
            if (!player.CarryController.BeginCarry(PlayerCarryController.CarryType.File, file, file)) return;

            file.transform.SetParent(anchor, true);
            file.transform.localPosition = _carryLocalPosition;
            file.transform.localRotation = Quaternion.Euler(_carryLocalEulerAngles);
            file.transform.localScale = _carryLocalScale;

            Collider[] colliders = file.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders) collider.enabled = false;

            ClearAllRegister();
            if (_detectCollider != null) _detectCollider.enabled = false;
            IsFree = true;
            _spawnedFile = null;
            EventFileSourceFinish?.Invoke(player);
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
