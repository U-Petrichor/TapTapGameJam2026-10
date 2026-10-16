using UnityEngine;

namespace TapTapGameJam.UI
{
    /// <summary>
    /// 界面容器基类：背包（<see cref="BagPanelUI"/>）、仓库（<see cref="RepoPanelUI"/>）、
    /// 待拾取位（<see cref="UnpickItemSlot"/>）都从这里派生。
    ///
    /// 【子类必须做的两件事】
    /// 1. 重写 Open/Close 时**先判 IsOpen，再调 base**：base 负责翻状态并让 UIManager 重算 ActionMap，
    ///    自己那套"建块 / 布局 / 显隐"放在 base 之后：
    ///    ```csharp
    ///    public override void OpenContainer()
    ///    {
    ///        if (IsOpen) return;      // 自己的开启动作不能重复跑
    ///        base.OpenContainer();    // 翻状态 + 通知 UIManager
    ///        ...建块 / 布局 / ApplyVisibility(true)...
    ///    }
    ///    ```
    /// 2. 打开时为自己持有的**每一件**物品建块并布局，关闭时把它们销毁。
    ///    容器**不要**记录"我持有哪些块" —— 块跟着物品走，物品转到别的容器后由那个容器销毁。
    ///
    /// 【为什么 IsOpen 在基类】开关去重不能指望发布方：按键、按钮、调试入口都可能重复发请求，
    /// 而"当前开/关"只有容器自己知道（以前界面层靠读 ActionMap 去猜，等于把状态寄存在别处）。
    /// </summary>
    public abstract class ItemContainer : MonoBehaviour
    {
        /// <summary>当前是不是开着。只由 <see cref="OpenContainer"/> / <see cref="CloseContainer"/> 维护</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// 打开。子类重写时写 `if (IsOpen) return; base.OpenContainer(); ...自己的活...`。
        /// 这里翻完状态立刻让 UIManager 重算 ActionMap（早于子类的建块 / 显隐）。
        /// 注意：只有被 UIManager 关注列表收进去的容器才会影响 ActionMap 和暂停。
        /// </summary>
        public virtual void OpenContainer()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            UIManager.Instance.RefreshActionMap();
        }

        /// <summary>关闭。子类重写时写 `if (!IsOpen) return; base.CloseContainer(); ...自己的活...`</summary>
        public virtual void CloseContainer()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            UIManager.Instance.RefreshActionMap();
        }

        /// <summary>开 ↔ 关。给"同一个按键切换"这类入口用（例如背包键）</summary>
        public void ToggleContainer()
        {
            if (IsOpen)
            {
                CloseContainer();
            }
            else
            {
                OpenContainer();
            }
        }
    }
}
