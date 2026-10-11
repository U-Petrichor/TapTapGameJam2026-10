using System;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using TapTapGameJam.MiniGame;
using UnityEngine;

public class SecurityDoor : Interactable, ITaskSpawnCondition
{
    public override string InteractionPrompt => "开门";
    [SerializeField] private Collider _blockCollider;
    [SerializeField] private Collider _detectCollider;
    [SerializeField] private Animator _animator;

    [Header("填入预计第几天开放，-1就是永远不开放")]
    [SerializeField] private int _whichDayOpen=1;
    private bool _canIOpen = false;
    private static readonly int OpenSecurityDoorHash = Animator.StringToHash("Base Layer.OpenSecurityDoor");
    public bool IsAvailable => _canIOpen;
    public string UnavailableReason => _canIOpen ? string.Empty : "门尚未开放";

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
        if (_whichDayOpen >= 0 && evt.DayCount >= _whichDayOpen)
        {
            // 是时候开门了
            _canIOpen = true;
        }
        else
        {
            _canIOpen = false;
        }
    }
    public override bool CanInteract(PlayerBasic player)
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

    public override void Interact(PlayerBasic player, InteractKey key)
    {
        // 先取消订阅
        ClearAllRegister();
        _animator.Play(OpenSecurityDoorHash);
    }
    private void UnlockTheDoor()
    {
        _blockCollider.enabled = false; // 关闭碰撞体，允许玩家通行
        _detectCollider.enabled = false; // 关闭检测碰撞体
    }
    private void LockTheDoor()
    {
        _blockCollider.enabled = true; // 打开碰撞体，禁止玩家通行
        _detectCollider.enabled = false; // 允许检测碰撞体，玩家才会显示可交互的提示
    }
    private void OpenDoorAnimationFinish(int _)
    {
        // 动画播放结束，关闭碰撞体允许玩家通行
        UnlockTheDoor();
    }
}
