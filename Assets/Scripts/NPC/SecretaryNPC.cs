using System;
using UnityEngine;

namespace TapTapGameJam.NPC
{
    /// <summary>
    /// 秘书 NPC 的专属行为：在办公室和外出点之间往返。
    /// 通用移动、状态机和动画由 NPC 基类负责。
    /// </summary>
    public sealed class SecretaryNPC : NPC
    {
        [Serializable]
        public sealed class RoutePoint
        {
            public string Name;
            public Transform Point;
            [Min(0.01f)] public float Weight = 1f;
        }

        [Header("秘书路线")]
        [SerializeField] private Transform _office;
        [SerializeField] private RoutePoint[] _stops = new RoutePoint[0];
        [Range(0f, 1f)] [SerializeField] private float _returnOfficeProbability = 0.4f;
        [Min(1)] [SerializeField] private int _maxExternalStops = 3;
        [Min(0.1f)] [SerializeField] private float _officeArrivalDistance = 1.25f;

        private bool _atOffice;
        private bool _pendingOffice;
        private int _externalStopCount;
        private RoutePoint _lastStop;
        private RoutePoint _pendingStop;

        protected override void OnNpcAwake()
        {
            _returnOfficeProbability = Mathf.Clamp01(_returnOfficeProbability);
            _maxExternalStops = Mathf.Max(1, _maxExternalStops);
            _officeArrivalDistance = Mathf.Max(0.1f, _officeArrivalDistance);

            _atOffice = IsNearOffice();
            _pendingOffice = !_atOffice && _office != null;
            _externalStopCount = 0;
        }

        public override Vector3 GetNextWalkDestination()
        {
            if (TryGetNextSecretaryDestination(out Vector3 destination)) return destination;
            return base.GetNextWalkDestination();
        }

        public override void NotifyWalkDestinationReached()
        {
            if (_pendingOffice)
            {
                _pendingOffice = false;
                _atOffice = true;
                _externalStopCount = 0;
                _pendingStop = null;
            }
            else if (_pendingStop != null)
            {
                _atOffice = false;
                _externalStopCount++;
                _lastStop = _pendingStop;
                _pendingStop = null;
            }
        }

        private bool TryGetNextSecretaryDestination(out Vector3 destination)
        {
            destination = transform.position;
            if (_office == null || _stops == null || _stops.Length == 0) return false;

            if (_pendingOffice || _externalStopCount >= _maxExternalStops)
            {
                _pendingOffice = true;
                return NpcNavigationUtility.TrySamplePoint(_office.position, Agent, out destination);
            }

            if (_atOffice)
            {
                _pendingStop = ChooseStop();
                return TryGetPoint(_pendingStop, out destination);
            }

            if (UnityEngine.Random.value < _returnOfficeProbability)
            {
                _pendingOffice = true;
                return NpcNavigationUtility.TrySamplePoint(_office.position, Agent, out destination);
            }

            _pendingStop = ChooseStop();
            return TryGetPoint(_pendingStop, out destination);
        }

        private RoutePoint ChooseStop()
        {
            float totalWeight = 0f;
            for (int i = 0; i < _stops.Length; i++)
            {
                RoutePoint stop = _stops[i];
                if (stop?.Point == null || stop == _lastStop) continue;
                totalWeight += Mathf.Max(0.01f, stop.Weight);
            }

            if (totalWeight <= 0f) return null;

            float pick = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < _stops.Length; i++)
            {
                RoutePoint stop = _stops[i];
                if (stop?.Point == null || stop == _lastStop) continue;

                pick -= Mathf.Max(0.01f, stop.Weight);
                if (pick <= 0f) return stop;
            }

            return null;
        }

        private bool TryGetPoint(RoutePoint point, out Vector3 destination)
        {
            if (point?.Point != null)
            {
                return NpcNavigationUtility.TrySamplePoint(point.Point.position, Agent, out destination);
            }

            destination = transform.position;
            return false;
        }

        private bool IsNearOffice()
        {
            return _office != null &&
                   (transform.position - _office.position).sqrMagnitude <=
                   _officeArrivalDistance * _officeArrivalDistance;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (_office != null)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                Gizmos.DrawSphere(_office.position, 0.25f);
            }

            if (_stops == null) return;

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
            for (int i = 0; i < _stops.Length; i++)
            {
                if (_stops[i]?.Point != null) Gizmos.DrawSphere(_stops[i].Point.position, 0.2f);
            }
        }
    }
}
