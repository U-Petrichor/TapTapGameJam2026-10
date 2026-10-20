using UnityEngine;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 通用高亮组件的接口约定。你提供的组件继承它即可，教程调度器只认这一层。
    /// </summary>
    public abstract class TutorialHighlightView : MonoBehaviour
    {
        public abstract void Show(RectTransform target, float durationSeconds);
        public abstract void Hide();
    }
}
