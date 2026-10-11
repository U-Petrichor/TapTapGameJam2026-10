using UnityEngine;

namespace TapTapGameJam.Coffee
{
    // 给热水壶开关、研磨机操作部位添加此标记。
    // 旧 3D 咖啡资产的标记组件；当前主场景使用 CoffeeGame 的 2D UI。
    [RequireComponent(typeof(Collider))]
    public class CoffeeClickable : MonoBehaviour { }
}
