using System;
using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Interactable;
using TapTapGameJam.Player;
using UnityEngine;

public class SecurityDoor : MonoBehaviour,IInteractable
{
    public string InteractionPrompt => "开门";
    private Dictionary<PlayerInteractionManager, HashSet<Collider>> _registered = new Dictionary<PlayerInteractionManager, HashSet<Collider>>();
    private bool _isOpen = false;
    [SerializeField] private Collider _blockCollider;
    [SerializeField] private Collider _detectCollider;
    [SerializeField] private Animator _animator;

    [Header("填入预计第几天开放，-1就是永远不开放")]
    [SerializeField] private int _whichDayOpen=1;
    private bool _canIOpen = false;
    private static readonly int OpenSecurityDoorHash = Animator.StringToHash("Base Layer.OpenSecurityDoor");
    public event Action EventIOpen;

    private void Awake()
    {
        if (_blockCollider == null)
        {
            Debug.LogError($"[SecurityDoor]:没有配置BlockCollider");
        }
        if (_detectCollider == null)
        {
            Debug.LogError($"[SecurityDoor]:没有配置DetectCollider");
        }
        if (_animator == null)
        {
            Debug.LogError($"[SecurityDoor]:没有配置Animator");
        }
    }
    private void OnEnable()
    {
        EventPipeline.Subscribe<EventNewDay>(TimeToUnlock);
    }
    private void OnDisable()
    {
        EventPipeline.UnSubscribe<EventNewDay>(TimeToUnlock);
     }
    private void TimeToUnlock(EventNewDay evt)
    {
        if (evt.DayCount >= _whichDayOpen)
        {
            // 是时候开门了
            _canIOpen = true;
            // 还要发送事件给被锁在里面的任务点，这样接收到消息之后就会开门了
            EventIOpen?.Invoke();
        }
        else
        {
            _canIOpen = false;
        }
    }
    public bool CanInteract(PlayerBasic player)
    {
        if (_canIOpen)
        {
            // 如果当前门可以进入
            return true;
        }
        if (true)
        {
            // 这里写玩家有通行证的情况
        }
        return false;
        
    }

    public void Interact(PlayerBasic player)
    {
        // 先取消订阅
        foreach(var x in _registered)
        {
            x.Key.Unregister(this);
        }
        _registered.Clear();
        _animator.Play(OpenSecurityDoorHash);
    }
    private void UnlockTheDoor()
    {
        _isOpen = true;
        _blockCollider.enabled = false; // 关闭碰撞体，允许玩家通行
        _detectCollider.enabled = false; // 关闭检测碰撞体
    }
    private void LockTheDoor()
    {
        _isOpen = false;
        _blockCollider.enabled = true; // 打开碰撞体，禁止玩家通行
        _detectCollider.enabled = false; // 允许检测碰撞体，玩家才会显示可交互的提示
    }
    private void OpenDoorAnimationFinish(int _)
    {
        // 动画播放结束，关闭碰撞体允许玩家通行
        UnlockTheDoor();
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
