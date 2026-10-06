using System.Collections.Generic;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using System;
using UnityEngine;
using TapTapGameJam.Events;

namespace TapTapGameJam.MiniGame
{
    public class FileSource : MonoBehaviour, IInteractable
    {
        public string InteractionPrompt => "接取文件";
        [SerializeField] private Collider _detectCollider;
        [SerializeField] private GameObject _filePrefab;
        private GameObject _spawnedFile;
        public bool IsFree = true;

        [Header("对于一开始在空旷区域的生成点，要置为true")]
        [SerializeField] public bool IsOpen = false;
        [Header("如果被某个门锁住了，要拖入哪一个门锁住了这里")]
        [SerializeField] private SecurityDoor _securityDoor;
        public bool CanUse => IsFree & IsOpen;
        public event Action EventFileSourceFinish;

        private Dictionary<PlayerInteractionManager, HashSet<Collider>> _registered = new Dictionary<PlayerInteractionManager, HashSet<Collider>>();

        private void Start()
        {
            _detectCollider.enabled = false;
        }
        private void OnEnable()
        {
            if (_securityDoor != null)
            {
                _securityDoor.EventIOpen += TimeToUnlock;
            }
        }
        private void OnDisable()
        {
            if (_securityDoor != null)
            {
                _securityDoor.EventIOpen -= TimeToUnlock;
            }
        }
        private void TimeToUnlock()
        {
            IsOpen = true;
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
                _spawnedFile = Instantiate(_filePrefab, transform, true);
            }
            else
            {
                Debug.LogError($"[FileSource]:没有给定文件预制体");
            }
        }
        public bool CanInteract(PlayerBasic player)
        {
            return true;
        }

        public void Interact(PlayerBasic player)
        {
            // 先取消订阅
            foreach (var x in _registered)
            {
                x.Key.Unregister(this);
            }
            _registered.Clear();
            // 接取文件的部分结束了，关闭检测器

            _detectCollider.enabled = false;
            IsFree = true;// 任务结束，重新变为空闲状态
            EventFileSourceFinish?.Invoke();
        }
        public void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 玩家进入范围
                PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
                if (!_registered.TryGetValue(interactionManager, out var colliders))
                {
                    // 尝试获取值失败→这是第一个collider
                    colliders = new HashSet<Collider>();
                    _registered.Add(interactionManager, colliders);
                    interactionManager.Register(this);
                }
                colliders.Add(other);
            }
        }

        public void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                // 玩家离开范围
                PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
                if (_registered.TryGetValue(interactionManager, out var colliders))
                {
                    colliders.Remove(other);
                    if (colliders.Count == 0)
                    {
                        // 说明是最后一个，那么就取消注册并且删除这个键
                        interactionManager.Unregister(this);
                        _registered.Remove(interactionManager);
                    }
                }
            }
        }
    }

}