using System.Collections.Generic;
using TapTapGameJam.Cockroach;
using TapTapGameJam.UI;
using TMPro;
using UnityEngine;

namespace TapTapGameJam.Core.Disinfection
{
    /// <summary>
    /// A pre-authored room that can be selected for disinfection.
    /// The room may contain more than one child collider (for example House26).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisinfectionRoom : MonoBehaviour
    {
        [SerializeField] private string _displayName;
        [SerializeField] private Collider[] _areaColliders;
        [SerializeField] private Renderer[] _areaRenderers;
        [SerializeField] private Transform _indicatorTarget;
        [SerializeField] private float _countdownHeight = 1.5f;

        private TextMeshPro _countdownText;
        private bool _warningVisible;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public Transform IndicatorTarget => _indicatorTarget != null ? _indicatorTarget : transform;
        public bool IsWarningVisible => _warningVisible;

        private void Awake()
        {
            CacheComponents();
            BuildCountdownView();
            SetWarningVisible(false);
        }

        public void CacheComponents()
        {
            if (_areaColliders == null || _areaColliders.Length == 0)
            {
                _areaColliders = GetComponentsInChildren<Collider>(true);
            }

            if (_areaRenderers == null || _areaRenderers.Length == 0)
            {
                _areaRenderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        public bool ContainsNest(CockNest nest)
        {
            return nest != null && ContainsWorldPoint(nest.transform.position);
        }

        /// <summary>
        /// Rooms are floor regions. The XZ projection is used so a nest's vertical offset
        /// does not prevent it from being considered inside the room.
        /// </summary>
        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (_areaColliders == null)
            {
                return false;
            }

            for (int i = 0; i < _areaColliders.Length; i++)
            {
                Collider area = _areaColliders[i];
                if (area == null || !area.enabled)
                {
                    continue;
                }

                Vector3 closest = area.ClosestPoint(worldPoint);
                Vector2 delta = new Vector2(closest.x - worldPoint.x, closest.z - worldPoint.z);
                if (delta.sqrMagnitude <= 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        public int CountOccupiedNests(IReadOnlyList<CockNest> nests)
        {
            int count = 0;
            if (nests == null)
            {
                return count;
            }

            for (int i = 0; i < nests.Count; i++)
            {
                CockNest nest = nests[i];
                if (nest != null && nest.CockroachList != null &&
                    nest.CockroachList.Count > 0 && ContainsNest(nest))
                {
                    count++;
                }
            }

            return count;
        }

        public List<CockNest> GetOccupiedNests(IReadOnlyList<CockNest> nests)
        {
            var result = new List<CockNest>();
            if (nests == null)
            {
                return result;
            }

            for (int i = 0; i < nests.Count; i++)
            {
                CockNest nest = nests[i];
                if (nest != null && nest.CockroachList != null &&
                    nest.CockroachList.Count > 0 && ContainsNest(nest))
                {
                    result.Add(nest);
                }
            }

            return result;
        }

        public void SetWarningVisible(bool visible)
        {
            _warningVisible = visible;

            if (_areaRenderers != null)
            {
                for (int i = 0; i < _areaRenderers.Length; i++)
                {
                    if (_areaRenderers[i] != null)
                    {
                        _areaRenderers[i].enabled = visible;
                    }
                }
            }

            if (_countdownText != null)
            {
                _countdownText.gameObject.SetActive(visible);
            }
        }

        public void SetCountdown(float seconds)
        {
            if (_countdownText == null)
            {
                return;
            }

            _countdownText.text = $"<color=#FF3B30>消杀预警</color>\n<color=#FFFFFF>{Mathf.Max(0, Mathf.CeilToInt(seconds))}</color>";
        }

        public void HideCountdown()
        {
            if (_countdownText != null)
            {
                _countdownText.gameObject.SetActive(false);
            }
        }

        private void BuildCountdownView()
        {
            if (_countdownText != null)
            {
                return;
            }

            var textObject = new GameObject("DisinfectionCountdown");
            textObject.transform.SetParent(transform, false);
            textObject.transform.position = GetDisplayPosition();

            _countdownText = textObject.AddComponent<TextMeshPro>();
            _countdownText.alignment = TextAlignmentOptions.Center;
            _countdownText.fontSize = 1.2f;
            _countdownText.fontStyle = FontStyles.Bold;
            _countdownText.color = Color.white;
            _countdownText.textWrappingMode = TextWrappingModes.NoWrap;
            _countdownText.richText = true;
            _countdownText.outlineWidth = 0.15f;
            _countdownText.outlineColor = Color.black;
            textObject.AddComponent<WorldBillboardUI>();
            textObject.SetActive(false);
        }

        private Vector3 GetDisplayPosition()
        {
            if (_indicatorTarget != null)
            {
                return _indicatorTarget.position;
            }

            if (_areaColliders != null && _areaColliders.Length > 0)
            {
                Bounds bounds = _areaColliders[0].bounds;
                for (int i = 1; i < _areaColliders.Length; i++)
                {
                    if (_areaColliders[i] != null)
                    {
                        bounds.Encapsulate(_areaColliders[i].bounds);
                    }
                }

                return bounds.center + Vector3.up * _countdownHeight;
            }

            return transform.position + Vector3.up * _countdownHeight;
        }
    }
}
