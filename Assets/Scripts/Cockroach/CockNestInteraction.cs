using System.Collections.Generic;
using TapTapGameJam.Interactables;
using TapTapGameJam.Player;
using UnityEngine;

namespace TapTapGameJam.Cockroach
{
    /// <summary>搬运同一个巢穴实例，保持蟑螂列表、数量、指令和孵化进度。</summary>
    public partial class CockNest : IPlayerCarryObject
    {
        [Header("巢穴交互")]
        [SerializeField, Min(0.1f)] private float _interactionRadius = 2f;
        [Tooltip("巢穴挂到玩家手部挂点后的局部位置")]
        [SerializeField] private Vector3 _carryLocalPosition;
        [SerializeField] private Vector3 _carryLocalEulerAngles;
        [Tooltip("落地射线允许命中的地面层")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private PlayerBasic _carrier;
        private PlayerInteractionManager _carrierInteraction;
        private Collider[] _carryColliders;
        private bool[] _colliderEnabled;
        private Rigidbody _carryBody;
        private bool _bodyKinematic;
        private Quaternion _groundRotation;
        private Vector3 _groundScale;
        private float _pivotAboveGround;
        private Canvas[] _carryCanvases;
        private bool[] _canvasEnabled;
        private bool _ringWasActive;
        private bool _wasActive;
        private readonly Dictionary<PlayerInteractionManager, HashSet<Collider>> _interactionPlayers = new();

        public bool IsCarried => _carrier != null;
        public override string InteractionPrompt => IsCarried ? "按 E 放下巢穴" : "按 E 搬运巢穴";

        private void InitializeInteraction()
        {
            // 原预制体的 BoxCollider 保留实体碰撞，另外增加交互范围。
            var detector = gameObject.AddComponent<SphereCollider>();
            detector.isTrigger = true;
            detector.radius = _interactionRadius;
        }

        public override bool CanInteract(PlayerBasic player)
        {
            if (player == null || !isActiveAndEnabled) return false;

            if (player.CompareTag("Player"))
            {
                if (player.CarryController == null) return false;
                if (IsCarried)
                    return _carrier == player && player.CarryController.CurrentObject == this;
                return !player.IsCarry &&
                    (player.transform.position - transform.position).sqrMagnitude <= _interactionRadius * _interactionRadius;
            }

            if (player.CompareTag("Cockroach"))
            {
                // TODO: 夜晚产卵的可交互条件。蟑螂不能搬运巢穴。
                return false;
            }

            return false;
        }

        public override void Interact(PlayerBasic player)
        {
            if (player == null) return;

            if (player.CompareTag("Player"))
            {
                if (!CanInteract(player)) return;
                if (IsCarried)
                    player.CarryController.EndCarry(false);
                else
                    TryPickUp(player);
                return;
            }

            if (player.CompareTag("Cockroach"))
            {
                // TODO: 夜晚产卵交互。接通时先检查 CanInteract(player)。
                // 此分支不会进入人形拾起/放下逻辑。
                return;
            }
        }

        public bool TryPickUp(PlayerBasic player)
        {
            if (!CanInteract(player) || IsCarried || !player.CompareTag("Player")) return false;
            Transform anchor = player.CarryController.GetCarryAnchor();
            if (anchor == null)
            {
                Debug.LogWarning("[CockNest] 请配置 PlayerCarryController 的手持挂点，或提供人形右手骨骼。", player);
                return false;
            }

            _carrier = player;
            if (!player.CarryController.BeginCarry(PlayerCarryController.CarryType.CockroachNest, this, gameObject))
            {
                _carrier = null;
                return false;
            }

            _wasActive = isActive;
            isActive = false;

            _groundRotation = transform.rotation;
            _groundScale = transform.lossyScale;
            _carryColliders = GetComponentsInChildren<Collider>(true);
            _colliderEnabled = new bool[_carryColliders.Length];
            float bottom = transform.position.y;
            bool hasBounds = false;
            for (int i = 0; i < _carryColliders.Length; i++)
            {
                Collider collider = _carryColliders[i];
                _colliderEnabled[i] = collider.enabled;
                if (collider.enabled && !collider.isTrigger)
                {
                    bottom = hasBounds ? Mathf.Min(bottom, collider.bounds.min.y) : collider.bounds.min.y;
                    hasBounds = true;
                }
                collider.enabled = false;
            }
            _pivotAboveGround = hasBounds ? transform.position.y - bottom : 0f;

            _carryBody = GetComponent<Rigidbody>();
            if (_carryBody != null)
            {
                _bodyKinematic = _carryBody.isKinematic;
                if (!_bodyKinematic)
                {
                    _carryBody.linearVelocity = Vector3.zero;
                    _carryBody.angularVelocity = Vector3.zero;
                }
                _carryBody.isKinematic = true;
            }

            _carryCanvases = GetComponentsInChildren<Canvas>(true);
            _canvasEnabled = new bool[_carryCanvases.Length];
            for (int i = 0; i < _carryCanvases.Length; i++)
            {
                _canvasEnabled[i] = _carryCanvases[i].enabled;
                _carryCanvases[i].enabled = false;
            }
            if (_ringRoot != null)
            {
                _ringWasActive = _ringRoot.gameObject.activeSelf;
                _ringRoot.gameObject.SetActive(false);
            }

            UnregisterInteractionTargets();
            // 不关闭巢穴根物体，不触发 OnDisable 的数据清空。
            transform.SetParent(anchor, true);
            transform.localPosition = _carryLocalPosition;
            transform.localRotation = Quaternion.Euler(_carryLocalEulerAngles);
            // 碰撞体已停用，但携带中的巢穴仍必须作为当前交互目标保留，才能按 E 放下。
            _carrierInteraction = player.PlayerInteractionManager;
            if (_carrierInteraction != null)
            {
                _carrierInteraction.Register(this);
                _carrierInteraction.SelectInteractionTarget(this);
            }
            return true;
        }

        /// <summary>groundPosition 指定落地的 XZ 位置；碰撞体底部贴地，恢复同一实例。</summary>
        public void PlaceAt(Vector3 groundPosition)
        {
            if (!IsCarried) return;
            PlayerBasic player = _carrier;
            Vector3 position = groundPosition;
            float closest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(groundPosition + Vector3.up * 3f,
                Vector3.down, 10f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform)) continue;
                if (hit.distance >= closest) continue;
                closest = hit.distance;
                position.y = hit.point.y;
            }
            position.y += _pivotAboveGround + 0.01f;

            transform.SetParent(null, true);
            transform.rotation = _groundRotation;
            transform.localScale = _groundScale;
            transform.position = position;
            _carrierInteraction?.Unregister(this);
            _carrierInteraction = null;
            _carrier = null;
            isActive = _wasActive;
            for (int i = 0; i < _carryColliders.Length; i++)
                if (_carryColliders[i] != null) _carryColliders[i].enabled = _colliderEnabled[i];
            if (_carryBody != null) _carryBody.isKinematic = _bodyKinematic;
            for (int i = 0; i < _carryCanvases.Length; i++)
                if (_carryCanvases[i] != null) _carryCanvases[i].enabled = _canvasEnabled[i];
            if (_ringRoot != null) _ringRoot.gameObject.SetActive(_ringWasActive);
            ApplyPhasePresentation();
            RefreshDistanceField();
        }

        // 巢穴的交互范围是运行时添加的触发器，并且要同时识别人形与 AI 蟑螂，
        // 所以这里覆盖基类的注册逻辑，用自己的字典维护交互玩家。
        public override void OnTriggerEnter(Collider other) => TrackInteractionPlayer(other);
        private void OnTriggerStay(Collider other) => TrackInteractionPlayer(other);

        private void TrackInteractionPlayer(Collider other)
        {
            if (IsCarried || !other.enabled) return;
            // 用玩家根节点 Tag 判形态；子碰撞体可能没有 Tag，AI 蟑螂没有 PlayerBasic。
            var player = other.GetComponentInParent<PlayerBasic>();
            if (player == null || (!player.CompareTag("Player") && !player.CompareTag("Cockroach"))) return;
            var interaction = player.PlayerInteractionManager;
            if (interaction == null) return;
            if (!_interactionPlayers.TryGetValue(interaction, out var colliders))
            {
                colliders = new HashSet<Collider>();
                _interactionPlayers.Add(interaction, colliders);
                interaction.Register(this);
            }
            colliders.Add(other);
        }

        public override void OnTriggerExit(Collider other)
        {
            var interaction = other.GetComponentInParent<PlayerInteractionManager>();
            if (interaction == null || !_interactionPlayers.TryGetValue(interaction, out var colliders)) return;
            colliders.Remove(other);
            if (colliders.Count != 0) return;
            interaction.Unregister(this);
            _interactionPlayers.Remove(interaction);
        }

        private void UnregisterInteractionTargets()
        {
            var targets = new List<PlayerInteractionManager>(_interactionPlayers.Keys);
            _interactionPlayers.Clear();
            foreach (var interaction in targets)
                if (interaction != null) interaction.Unregister(this);
        }
    }
}
