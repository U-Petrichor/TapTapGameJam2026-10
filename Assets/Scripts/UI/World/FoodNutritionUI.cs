using NUnit.Framework;
using TapTapGameJam.Core;
using TapTapGameJam.Interactables;
using TapTapGameJam.Items;
using TapTapGameJam.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 挂在 World Space Canvas 上，每帧读 FoodItem 的剩余营养，
    /// 刷新中间的文本和圆圈的填充比例。
    ///
    /// 圆圈的 Image 需要在 Inspector 里设成：
    ///     Image Type = Filled, Fill Method = Radial 360, Fill Origin = Top, Clockwise
    /// 不设的话 fillAmount 不会生效（图还是整圆）。
    /// </summary>
    [DisallowMultipleComponent]
    public class FoodNutritionUI : MonoBehaviour
    {
        [Tooltip("圆圈填充图。留空则只刷新文本")]
        [SerializeField] private Image _fillImage;

        [Tooltip("数字文本")]
        [SerializeField] private TMP_Text _valueText;

        [Tooltip("要显示的食物。留空则从自己往上找")]
        [SerializeField] private FoodItem _food;

        [Header("富文本颜色")]
        [Tooltip("营养充足时的颜色")]
        [SerializeField] private string _highColor = "#8CE05A";

        [Tooltip("快吃完时的颜色")]
        [SerializeField] private string _lowColor = "#E05A5A";

        [Tooltip("低于这个比例就换成低营养颜色")]
        [SerializeField, UnityEngine.Range(0f, 1f)] private float _lowThreshold = 0.3f;

        private void Awake()
        {
            if (_food == null)
            {
                _food = GetComponentInParent<FoodItem>();
            }

            if (_food == null)
            {
                Debug.LogError($"[FoodNutritionUI:{name}]：没找到 FoodItem，也没手动指定");
            }

            if (_valueText == null)
            {
                Debug.LogError($"[FoodNutritionUI:{name}]：没有配置文本");
            }

        }

        private void Update()
        {
            if (_food == null)
            {
                return;
            }

            float remaining = _food.remainingNutrition;
            float total = _food.totalNutrition;

            // 填比例
            if (_fillImage != null)
            {
                float ratio = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
                _fillImage.fillAmount = ratio;
            }

            // 文本：数字按剩余比例换色
            if (_valueText != null)
            {
                string color = ProcessedRatio(remaining, total) <= _lowThreshold ? _lowColor : _highColor;
                _valueText.text = $"<color={color}>{Mathf.CeilToInt(remaining)}</color> / {total}";
            }
        }
        private static float ProcessedRatio(float remaining, float total)
        {
            return total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }
    }
}
