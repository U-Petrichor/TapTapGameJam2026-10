using System;
using TapTapGameJam.Events;
using TapTapGameJam.Types;
using UnityEngine;

namespace TapTapGameJam.Player
{
    /// <summary>携带真实世界实例时，由物品负责恢复碰撞、落地和自身数据。</summary>
    public interface IPlayerCarryObject
    {
        void PlaceAt(Vector3 groundPosition);
    }

    /// <summary>
    /// 人形玩家的携带状态。与夜晚 PlayerBag / HandItem 数据完全无关。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCarryController : MonoBehaviour
    {
        public enum CarryType
        {
            None,
            File,
            CockroachNest
        }

        [field: SerializeField] public CarryType CurrentType { get; private set; } = CarryType.None;
        public UnityEngine.Object CurrentObject { get; private set; }
        public GameObject HeldVisual { get; private set; }
        public bool IsCarry => CurrentType != CarryType.None;

        [Header("人形手持挂点")]
        [Tooltip("可指定双手之间的挂点；留空时使用人形 Animator 的右手骨骼")]
        [SerializeField] private Transform _carryAnchor;
        [SerializeField, Min(0f)] private float _dropForwardDistance = 1.2f;

        public Transform GetCarryAnchor()
        {
            if (_carryAnchor != null) return _carryAnchor;
            var animation = GetComponent<PlayerAnimationManager>();
            var animator = animation != null ? animation.Animator : null;
            if (animator != null && animator.isHuman)
                return animator.GetBoneTransform(HumanBodyBones.RightHand);
            return null;
        }

        public Vector3 EvaluateDropPosition()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            return transform.position + forward * _dropForwardDistance;
        }

        public event Action<CarryType> EventCarryChanged;
        public event Action<GameObject> EventHeldVisualClearRequested;
        public event Action<UnityEngine.Object, Vector3> EventNestNightDropRequested;

        private void OnEnable()
        {
            EventPipeline.Subscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        private void OnDisable()
        {
            EventPipeline.UnSubscribe<GamePhaseChangedEvent>(HandlePhaseChanged);
        }

        public bool BeginCarry(CarryType type, UnityEngine.Object carryObject = null, GameObject heldVisual = null)
        {
            if (type == CarryType.None || IsCarry)
            {
                return false;
            }

            CurrentType = type;
            CurrentObject = carryObject;
            HeldVisual = heldVisual;
            EventCarryChanged?.Invoke(CurrentType);
            return true;
        }

        public void SetHeldVisual(GameObject heldVisual)
        {
            HeldVisual = heldVisual;
        }

        public void EndCarry(bool clearHeldVisual = true)
        {
            if (!IsCarry && CurrentObject == null && HeldVisual == null)
            {
                return;
            }

            // 巢穴是原世界实例，结束携带时必须恢复落地，不能 SetActive(false)。
            if (CurrentObject is IPlayerCarryObject worldItem)
            {
                worldItem.PlaceAt(EvaluateDropPosition());
                if (IsSameObject(HeldVisual, CurrentObject)) HeldVisual = null;
            }

            if (clearHeldVisual)
            {
                ClearHeldVisual();
            }

            CurrentType = CarryType.None;
            CurrentObject = null;
            HeldVisual = null;
            EventCarryChanged?.Invoke(CurrentType);
        }

        private void HandlePhaseChanged(GamePhaseChangedEvent evt)
        {
            if (evt.NewPhase != GamePhase.Night || !IsCarry)
            {
                return;
            }

            switch (CurrentType)
            {
                case CarryType.File:
                    EndCarry();
                    break;

                case CarryType.CockroachNest:
                    UnityEngine.Object nest = CurrentObject;
                    Vector3 dropPosition = transform.position;
                    EventNestNightDropRequested?.Invoke(nest, dropPosition);
                    // 预留系统已接管时，不再重复放置；否则使用通用的世界对象兜底逻辑。
                    if (IsCarry && CurrentObject == nest)
                    {
                        if (nest is IPlayerCarryObject worldItem)
                            worldItem.PlaceAt(dropPosition);
                        else
                            TryDropObjectAt(nest, dropPosition);
                    }
                    if (HeldVisual != null && !IsSameObject(HeldVisual, nest))
                    {
                        ClearHeldVisual();
                    }
                    // 已放置在夜晚切换的位置，避免 EndCarry 再放到玩家前方。
                    CurrentObject = null;
                    EndCarry(false);
                    break;
            }
        }

        private void ClearHeldVisual()
        {
            GameObject visual = HeldVisual;
            HeldVisual = null;
            if (visual == null)
            {
                return;
            }

            visual.SetActive(false);
            EventHeldVisualClearRequested?.Invoke(visual);
        }

        private static bool IsSameObject(GameObject visual, UnityEngine.Object carryObject)
        {
            if (visual == null || carryObject == null)
            {
                return false;
            }

            if (carryObject is GameObject gameObject)
            {
                return visual == gameObject;
            }

            if (carryObject is Component component)
            {
                return visual == component.gameObject;
            }

            return false;
        }

        private static bool TryDropObjectAt(UnityEngine.Object carryObject, Vector3 position)
        {
            Transform target = null;
            if (carryObject is GameObject gameObject)
            {
                target = gameObject.transform;
            }
            else if (carryObject is Component component)
            {
                target = component.transform;
            }

            if (target == null)
            {
                return false;
            }

            target.SetParent(null, true);
            target.position = position;
            target.gameObject.SetActive(true);
            return true;
        }
    }
}
