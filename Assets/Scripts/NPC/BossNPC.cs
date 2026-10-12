using System;
using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>
    /// 老板 NPC 的专属行为：在多个区域之间巡查，并在区域内随机走动。
    /// 通用移动、状态机和动画由 NPC 基类负责。
    /// </summary>
    public sealed class BossNPC : NPC
    {
        [Serializable]
        public sealed class PatrolRegion
        {
            public string Name;
            [Tooltip("老板在这个区域内随机巡查的范围")]
            public BoxCollider Area;
            [Tooltip("切换到这个区域时优先走到的入口点")]
            public Transform EntryPoint;
            [Min(0.01f)] public float Weight = 1f;
        }

        [Header("老板巡查")]
        [Tooltip("老板会在当前区域内反复选择目标；走到目标一次算一次巡查")]
        [SerializeField] private PatrolRegion[] _regions = new PatrolRegion[0];
        [Range(0f, 1f)] [SerializeField] private float _exitProbabilityBase = 0.1f;
        [Range(0f, 1f)] [SerializeField] private float _exitProbabilityPerWander = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float _exitProbabilityMax = 0.8f;
        [Min(1)] [SerializeField] private int _maxWandersBeforeExit = 6;

        private PatrolRegion _currentRegion;
        private PatrolRegion _pendingRegion;
        private int _wanderCount;

        protected override void OnNpcAwake()
        {
            _exitProbabilityBase = Mathf.Clamp01(_exitProbabilityBase);
            _exitProbabilityPerWander = Mathf.Clamp01(_exitProbabilityPerWander);
            _exitProbabilityMax = Mathf.Clamp01(_exitProbabilityMax);
            _maxWandersBeforeExit = Mathf.Max(1, _maxWandersBeforeExit);

            _currentRegion = FindRegionAtPosition() ?? FirstValidRegion();
            _wanderCount = 0;
        }

        public override Vector3 GetNextWalkDestination()
        {
            if (TryGetNextBossDestination(out Vector3 destination)) return destination;
            return base.GetNextWalkDestination();
        }

        public override void NotifyWalkDestinationReached()
        {
            if (_pendingRegion != null)
            {
                _currentRegion = _pendingRegion;
                _pendingRegion = null;
                _wanderCount = 0;
            }
            else
            {
                _wanderCount++;
            }
        }

        private bool TryGetNextBossDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (_currentRegion == null) return false;

            bool mustLeave = _wanderCount >= _maxWandersBeforeExit;
            float exitProbability = Mathf.Min(
                _exitProbabilityMax,
                _exitProbabilityBase + _wanderCount * _exitProbabilityPerWander);

            PatrolRegion nextRegion = ChooseDifferentRegion();
            if (nextRegion != null && (mustLeave || UnityEngine.Random.value < exitProbability))
            {
                _pendingRegion = nextRegion;
                if (TryGetRegionEntry(nextRegion, out destination)) return true;
                _pendingRegion = null;
            }

            if (TrySampleRegion(_currentRegion, out destination)) return true;
            return TryGetRegionEntry(_currentRegion, out destination);
        }

        private PatrolRegion FindRegionAtPosition()
        {
            if (_regions == null) return null;

            for (int i = 0; i < _regions.Length; i++)
            {
                PatrolRegion region = _regions[i];
                if (region?.Area != null && region.Area.bounds.Contains(transform.position)) return region;
            }

            return null;
        }

        private PatrolRegion FirstValidRegion()
        {
            if (_regions == null) return null;

            for (int i = 0; i < _regions.Length; i++)
            {
                PatrolRegion region = _regions[i];
                if (region != null && (region.Area != null || region.EntryPoint != null)) return region;
            }

            return null;
        }

        private PatrolRegion ChooseDifferentRegion()
        {
            if (_regions == null) return null;

            float totalWeight = 0f;
            for (int i = 0; i < _regions.Length; i++)
            {
                PatrolRegion region = _regions[i];
                if (region == null || region == _currentRegion ||
                    (region.Area == null && region.EntryPoint == null)) continue;
                totalWeight += Mathf.Max(0.01f, region.Weight);
            }

            if (totalWeight <= 0f) return null;

            float pick = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < _regions.Length; i++)
            {
                PatrolRegion region = _regions[i];
                if (region == null || region == _currentRegion ||
                    (region.Area == null && region.EntryPoint == null)) continue;

                pick -= Mathf.Max(0.01f, region.Weight);
                if (pick <= 0f) return region;
            }

            return null;
        }

        private bool TryGetRegionEntry(PatrolRegion region, out Vector3 destination)
        {
            if (region != null && region.EntryPoint != null)
                return NpcNavigationUtility.TrySamplePoint(region.EntryPoint.position, Agent, out destination);

            return TrySampleRegion(region, out destination);
        }

        private bool TrySampleRegion(PatrolRegion region, out Vector3 destination)
        {
            return NpcNavigationUtility.TrySampleRegion(region?.Area, Agent, transform, out destination);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (_regions == null) return;

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.55f);
            for (int i = 0; i < _regions.Length; i++)
            {
                PatrolRegion region = _regions[i];
                if (region?.Area != null) Gizmos.DrawWireCube(region.Area.bounds.center, region.Area.bounds.size);
                if (region?.EntryPoint != null) Gizmos.DrawSphere(region.EntryPoint.position, 0.2f);
            }
        }
    }
}
