using TapTapGameJam.Audio;
using TapTapGameJam.Events;
using TapTapGameJam.MiniGame;
using TapTapGameJam.NPC;
using TapTapGameJam.Player;
using UnityEngine;

public class SecurityDoor : MonoBehaviour, ITaskSpawnCondition
{
    [SerializeField] private Collider _blockCollider;
    [SerializeField] private Collider _detectCollider;
    [SerializeField] private Animator _animator;
    [Header("自动开关门")]
    [Min(0.01f)] [SerializeField] private float _doorAnimationDuration = 0.4166667f;
    [Tooltip("勾选后只由门禁卡授权，不按日期开放。")]
    [SerializeField] private bool _requiresAccessCard;
    [Header("预计第几天开放，-1 表示不按日期开放")]
    [SerializeField] private int _whichDayOpen = 1;
    [Header("开放特写（留空时使用阻挡碰撞体中心）")]
    [SerializeField] private Transform _revealFocus;
    [Tooltip("可选的世界机位，特殊门可用它绕开墙体遮挡。")]
    [SerializeField] private Transform _revealCameraPose;
    [Tooltip("0 使用运镜管理器的默认距离。")]
    [SerializeField, Min(0f)] private float _revealDistance;
    private static readonly int OpenSecurityDoorHash = Animator.StringToHash("Base Layer.OpenSecurityDoor");
    private bool _canIOpen;
    private bool _doorOpen;
    private bool _isAnimating;
    private bool _opening;
    private bool _openOnceRequested;
    private float _animationNormalizedTime;

    public bool IsAvailable => _canIOpen;
    public Vector3 RevealFocus => _revealFocus != null ? _revealFocus.position
        : _blockCollider != null ? _blockCollider.bounds.center : transform.position;
    public Transform RevealCameraPose => _revealCameraPose;
    public float RevealDistance => _revealDistance;
    public string UnavailableReason => _canIOpen ? string.Empty : "门尚未开放";

    private void Awake()
    {
        if (_blockCollider == null) Debug.LogError("[SecurityDoor] 没有配置 BlockCollider", this);
        if (_detectCollider == null) Debug.LogError("[SecurityDoor] 没有配置 DetectCollider", this);
        if (_animator == null) Debug.LogError("[SecurityDoor] 没有配置 Animator", this);
        if (_blockCollider != null) _blockCollider.enabled = true;
        if (_detectCollider != null) _detectCollider.enabled = true;
        SampleDoorPose(0f);
    }
    private void OnEnable() => EventPipeline.Subscribe<EventNewDay>(TimeToUnlock);
    private void OnDisable() => EventPipeline.UnSubscribe<EventNewDay>(TimeToUnlock);
    private void Update()
    {
        UpdatePresence();
        UpdateDoorAnimation();
    }
    private void TimeToUnlock(EventNewDay evt)
    {
        if (_requiresAccessCard) return;
        bool wasAvailable = _canIOpen;
        _canIOpen = _whichDayOpen >= 0 && evt.DayCount >= _whichDayOpen;
        RefreshBlocker();
        if (_canIOpen && !wasAvailable)
            EventPipeline.Publish(new EventSecurityDoorAvailable(this));
    }
    /// <summary>获得本局永久通行权限，并立即完整开门一次，不触发日期开放特写。</summary>
    public void GrantAccessCard()
    {
        if (!_requiresAccessCard || _canIOpen) return;
        _canIOpen = true;
        _openOnceRequested = true;
        StartDoorAnimation(true);
    }
    private void UpdatePresence()
    {
        if (_detectCollider == null || !_detectCollider.enabled) return;
        Bounds bounds = _detectCollider.bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents,
            Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        bool shouldOpen = _openOnceRequested;
        foreach (Collider hit in hits)
        {
            if (hit != null && (hit.GetComponentInParent<NPC>() != null ||
                (_canIOpen && hit.GetComponentInParent<PlayerBasic>() != null)))
            {
                shouldOpen = true;
                break;
            }
        }
        StartDoorAnimation(shouldOpen);
    }
    private void StartDoorAnimation(bool opening)
    {
        if (_animator == null) return;
        if (!_isAnimating && _doorOpen == opening)
        {
            if (opening) _openOnceRequested = false;
            RefreshBlocker();
            return;
        }
        if (_isAnimating && _opening == opening) return;
        if (!_isAnimating) _animationNormalizedTime = _doorOpen ? 1f : 0f;
        Sfx.At(opening ? SoundId.Door : SoundId.DoorClose, transform.position, this);
        _opening = opening;
        _isAnimating = true;
        RefreshBlocker();
        SampleDoorPose(_animationNormalizedTime);
    }
    private void UpdateDoorAnimation()
    {
        if (!_isAnimating || _animator == null) return;
        float delta = Time.deltaTime / Mathf.Max(0.01f, _doorAnimationDuration);
        _animationNormalizedTime = Mathf.Clamp01(_animationNormalizedTime + (_opening ? delta : -delta));
        SampleDoorPose(_animationNormalizedTime);
        if ((_opening && _animationNormalizedTime >= 1f) || (!_opening && _animationNormalizedTime <= 0f))
            FinishDoorAnimation();
    }
    private void SampleDoorPose(float normalizedTime)
    {
        if (_animator == null || !_animator.isActiveAndEnabled) return;
        _animator.speed = 0f;
        _animator.Play(OpenSecurityDoorHash, 0, Mathf.Clamp01(normalizedTime));
        _animator.Update(0f);
    }
    private void FinishDoorAnimation()
    {
        if (!_isAnimating) return;
        _isAnimating = false;
        _doorOpen = _opening;
        _animationNormalizedTime = _doorOpen ? 1f : 0f;
        if (_doorOpen) _openOnceRequested = false;
        SampleDoorPose(_animationNormalizedTime);
        RefreshBlocker();
    }
    private void RefreshBlocker()
    {
        // NPC 可临时打开锁住的门，但不会因此获得玩家通行权限。
        if (_blockCollider != null) _blockCollider.enabled = !(_canIOpen && _doorOpen && !_isAnimating);
    }
    private void OpenDoorAnimationFinish(int _)
    {
        // 兼容旧动画事件；完成逻辑在 Animator.Update 返回后执行，避免回调重入。
    }
}
