using UnityEngine;

namespace TapTapGameJam.Coffee
{
    // 给热水壶开关、研磨机操作部位添加此标记。
    // 点击/按住由 CoffeeDeskInteractor 读取，是否允许由流程控制器判断。
    [RequireComponent(typeof(Collider))]
    public class CoffeeClickable : MonoBehaviour { }
}
