using TapTapGameJam.Player;

namespace TapTapGameJam.Interactable
{
    public interface IInteractable
    {
        /// <summary>
        /// 用于显示E交互时的提示，例如“按E拾取”、“按E打开”
        /// </summary>
        string InteractionPrompt { get; }
        /// <summary>
        /// 当前能否交互，例如宝箱是被锁住，门是否可以从这一侧打开
        /// </summary>
        bool CanInteract(PlayerBasic player);
        /// <summary>
        /// 真正执行交互
        /// </summary>
        /// <param name="gameObject"></param>
        void Interact(PlayerBasic player);
    }
}

