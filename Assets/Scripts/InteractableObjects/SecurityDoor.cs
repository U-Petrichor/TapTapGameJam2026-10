using TapTapGameJam.Audio;
using System;
using System.Collections.Generic;
using TapTapGameJam.Events;
using TapTapGameJam.Interactables;
using TapTapGameJam.MiniGame;
using TapTapGameJam.NPC;
using TapTapGameJam.Player;
using UnityEngine;

public class SecurityDoor : Interactable, ITaskSpawnCondition
{
    public override string InteractionPrompt => "开门";
    [SerializeField] private Collider _blockCollider;
    [SerializeField] private Collider _detectCollider;
    [SerializeField] private Animator _animator;

    [Header("NPC 自动开关门")]
    [Tooltip("开门动画的时长；当前 OpenSecurityDoor 动画约为 0.4167 秒")]
    [Min(0.01f)] [SerializeField] private float _doorAnimationDuration = 0.4166667f;

    [Header("填入预计第几天开放，-1就是永远不开放")]
    [SerializeField] private int _whichDayOpen=1;
    [Header("开放特写（留空时使用阻挡碰撞体中心）")]
    [SerializeField] private Transform _revealFocus;
    [Tooltip("可选的世界机位，特殊门可用它绕开墙体遮挡。")]
    [SerializeField] private Transform _revealCameraPose;
    [Tooltip("0 使用运镜管理器的默认距离。")]
    [SerializeField, Min(0f)] private float _revealDistance;
    private bool _canIOpen = false;
    private static readonly int OpenSecurityDoorHash = Animator.StringToHash("Base Layer.OpenSecurityDoor");

    private bool _playerUnlocked;
    private bool _doorOpen;
    private bool _isAnimating;
    private bool _opening;
    private bool _unlockPlayerAfterOpen;
    private float _animationNormalizedTime;

    public bool IsAvailable => _canIOpen;
    public Vector3 RevealFocus => _revealFocus != null ? _revealFocus.position
        : _blockCollider != null ? _blockCollider.bounds.center : transform.position;
    public Transform RevealCameraPose => _revealCameraPose;
    public float RevealDistance => _revealDistance;
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

        if (_blockCollider != null) _blockCollider.enabled = true;
        if (_detectCollider != null) _detectCollider.enabled = true;
        SampleDoorPose(0f);
    }
    private void OnEnable()
    {
        EventPipeline.Subscribe<EventNewDay>(TimeToUnlock);
    }
    private void OnDisable()
    {
        EventPipeline.UnSubscribe<EventNewDay>(TimeToUnlock);
        ClearAllRegister();
     }

    private void Update()
    {
        UpdateNpcPresence();
        UpdateDoorAnimation();
    }
    private void TimeToUnlock(EventNewDay evt)
    {
        bool wasAvailable = _canIOpen;
        _canIOpen = _whichDayOpen >= 0 && evt.DayCount >= _whichDayOpen;
        if (_canIOpen && !wasAvailable)
            EventPipeline.Publish(new EventSecurityDoorAvailable(this));
    }
    public override bool CanInteract(PlayerBasic player)
    {
        return _canIOpen && !_playerUnlocked;
    }

    public override void Interact(PlayerBasic player, InteractKey key)
    {
        if (!_canIOpen) return;

        ClearAllRegister();
        _unlockPlayerAfterOpen = true;

        if (_doorOpen && !_isAnimating)
        {
            UnlockTheDoor();
        }
        else
        {
            StartDoorAnimation(true, true);
        }
    }

    /// <summary>NPC 靠近时只打开视觉动画，玩家阻挡碰撞体仍然保持启用。</summary>
    private void OpenForNpc()
    {
        if (_playerUnlocked) return;
        StartDoorAnimation(true, false);
    }

    /// <summary>所有 NPC 离开后倒放开门动画。</summary>
    private void CloseForNpc()
    {
        if (_playerUnlocked || !_doorOpen) return;
        StartDoorAnimation(false, false);
    }

    private void UpdateNpcPresence()
    {
        if (_playerUnlocked || _detectCollider == null || !_detectCollider.enabled) return;

        Bounds bounds = _detectCollider.bounds;
        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        bool npcNearby = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i].GetComponentInParent<NPC>() != null)
            {
                npcNearby = true;
                break;
            }
        }

        if (npcNearby)
        {
            OpenForNpc();
        }
        else if (_doorOpen && !_isAnimating)
        {
            CloseForNpc();
        }
    }

    private void StartDoorAnimation(bool opening, bool unlockPlayerWhenFinished)
    {
        if (_animator == null) return;

        // NPC presence is sampled every frame. A door already at the requested
        // endpoint must not restart its animation or publish another sound.
        if (!_isAnimating && _doorOpen == opening)
        {
            if (opening && unlockPlayerWhenFinished) UnlockTheDoor();
            return;
        }

        if (_isAnimating && _opening == opening)
        {
            _unlockPlayerAfterOpen |= unlockPlayerWhenFinished;
            return;
        }

        bool wasAnimating = _isAnimating;
        float currentTime = _animationNormalizedTime;
        if (!wasAnimating)
        {
            currentTime = _doorOpen ? 1f : 0f;
        }

        Sfx.At(opening ? SoundId.Door : SoundId.DoorClose, transform.position, this);
        _opening = opening;
        _unlockPlayerAfterOpen = unlockPlayerWhenFinished || _unlockPlayerAfterOpen;
        _animationNormalizedTime = Mathf.Clamp01(currentTime);
        _isAnimating = true;

        // 从当前姿态继续，开门中途遇到 NPC 离开/进入时不会跳帧。
        SampleDoorPose(_animationNormalizedTime);
    }

    private void UpdateDoorAnimation()
    {
        if (!_isAnimating || _animator == null) return;

        float delta = Time.deltaTime / Mathf.Max(0.01f, _doorAnimationDuration);
        _animationNormalizedTime = Mathf.Clamp01(
            _animationNormalizedTime + ( _opening ? delta : -delta));
        SampleDoorPose(_animationNormalizedTime);

        if ((_opening && _animationNormalizedTime >= 1f) ||
            (!_opening && _animationNormalizedTime <= 0f))
        {
            FinishDoorAnimation();
        }
    }

    private void SampleDoorPose(float normalizedTime)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        // 不依赖 Animator.speed = -1（Unity 对负速率有限制），逐帧采样同一动画的正/反方向。
        _animator.speed = 0f;
        _animator.Play(OpenSecurityDoorHash, 0, Mathf.Clamp01(normalizedTime));
        _animator.Update(0f);
    }

    private void FinishDoorAnimation()
    {
        if (!_isAnimating) return;

        bool opened = _opening;
        _isAnimating = false;
        _doorOpen = opened;
        _animationNormalizedTime = opened ? 1f : 0f;
        SampleDoorPose(_animationNormalizedTime);

        if (opened && _unlockPlayerAfterOpen)
        {
            UnlockTheDoor();
        }
        else
        {
            // NPC 自动开门/关门不会改变玩家的通行权限。
            if (_blockCollider != null) _blockCollider.enabled = true;
            if (_detectCollider != null) _detectCollider.enabled = true;
        }

        _unlockPlayerAfterOpen = false;
    }
    private void UnlockTheDoor()
    {
        _playerUnlocked = true;
        _doorOpen = true;
        _isAnimating = false;
        _blockCollider.enabled = false; // 关闭碰撞体，允许玩家通行
        _detectCollider.enabled = false; // 关闭检测碰撞体
    }
    private void LockTheDoor()
    {
        _playerUnlocked = false;
        _doorOpen = false;
        _isAnimating = false;
        _unlockPlayerAfterOpen = false;
        _blockCollider.enabled = true; // 打开碰撞体，禁止玩家通行
        _detectCollider.enabled = true; // 继续检测玩家与 NPC
        SampleDoorPose(0f);
    }
    private void OpenDoorAnimationFinish(int _)
    {
        // 兼容旧动画事件。完成逻辑由 UpdateDoorAnimation 在采样返回后执行，
        // 避免在 Animator.Update 的回调中再次采样 Animator。
    }
    private void OnTriggerStay(Collider other)
    {
        // 玩家可能在门开放前就已站在检测范围内（运镜期间物理暂停）。
        // 开放后补注册，不要求玩家先退到触发器外再走回来。
        OnTriggerEnter(other);
    }
    public override void OnTriggerEnter(Collider other)
    {
        if (!_canIOpen || _playerUnlocked || _unlockPlayerAfterOpen)
        {
            // 未开放、正在解锁或已解锁的门不再加入交互候选。
            return;
        }
        if (other.CompareTag("Player"))
        {
            // 玩家进入范围
            PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
            if (interactionManager == null) return;
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
    /// <summary>
    /// 检测离开的单位Tag是否为Player，是则取消注册自身
    /// </summary>
    public override void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 玩家离开范围
            PlayerInteractionManager interactionManager = other.GetComponentInParent<PlayerInteractionManager>();
            if (interactionManager == null) return;
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
