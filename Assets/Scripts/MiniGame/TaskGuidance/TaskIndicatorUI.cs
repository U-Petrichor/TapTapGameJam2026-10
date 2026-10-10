using System.Collections.Generic;
using TapTapGameJam.MiniGame;
using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>管理任务与 Arrow 的一一对应关系</summary>
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(RectTransform))]
    public class TaskIndicatorUI : MonoBehaviour
    {
        [Tooltip("任务管理器")]
        [SerializeField] private MiniGameManager _manager; 
        [Tooltip("箭头预制体")]
        [SerializeField] private Arrow _arrowPrefab;
        [Tooltip("实际渲染游戏画面的 Camera")]
        [SerializeField] private Camera _worldCamera;
        [SerializeField, Min(0f)] private float _edgePadding = 20f; // 与屏幕边缘保持的距离

        private readonly Dictionary<string, Arrow> _arrows = new();
        private RectTransform _area; // 用于获取挂载在的GameObject的区域，这样才能确定箭头应该呈现在什么范围内

        private void Awake()
        {
            _area = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (_manager == null || _arrowPrefab == null)
            {
                Debug.LogError("[TaskIndicatorUI] 请配置任务管理器和 Arrow 预制体。", this);
                return;
            }
            _manager.EventTaskAdded += AddIndicator;
            _manager.EventTaskRemoved += RemoveIndicator;

            foreach (var instance in _manager.Tasks)
            {
                AddIndicator(instance);
            } 
        }

        private void OnDisable()
        {
            if (_manager != null)
            {
                _manager.EventTaskAdded -= AddIndicator;
                _manager.EventTaskRemoved -= RemoveIndicator;
            }
            foreach (var arrow in _arrows.Values)
                DestroyView(arrow);
            _arrows.Clear();
        }

        private void AddIndicator(MiniGameInstanceBasic instance)
        {
            if (instance.IsFinished || _arrows.ContainsKey(instance.Id))
            {
                // 若已经为此事件添加箭头，则返回
                return;
            }
            Arrow arrow = Instantiate(_arrowPrefab, transform, false);
            arrow.name = $"Arrow_{instance.Id}";
            var rect = (RectTransform)arrow.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            arrow.Bind(instance);
            _arrows.Add(instance.Id, arrow);
        }

        private void RemoveIndicator(MiniGameInstanceBasic instance)
        {
            
            if (!_arrows.TryGetValue(instance.Id, out Arrow view))
            {
                // 如果没找到就退出
                return;
            }
            _arrows.Remove(instance.Id);
            DestroyView(view);
        }

        private static void DestroyView(Arrow view)
        {
            if (view == null) return;
            view.Unbind();
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }

        private void LateUpdate()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            foreach (var view in _arrows.Values)
            {
                if (view != null)
                {
                    view.UpdatePosition(_area, _worldCamera, _edgePadding);
                }
            }
        }
    }
}
