using TapTapGameJam.Audio;
using UnityEngine;

namespace TapTapGameJam.UI
{
    public abstract class MiniGameContent : MonoBehaviour
    {
        protected SoundEmitter ActionSound => Sfx.Emitter(this);
        protected TapTapGameJam.Player.PlayerResources Resources { get; private set; }
        public void BindPlayer(TapTapGameJam.Player.PlayerBasic player) =>
            Resources = player != null ? player.PlayerResources : null;
        // 重置状态、初始化玩法，并保存完成通知。
        public abstract void Begin(System.Action onCompleted);

        // 立即停止输入、计时、拖拽等玩法行为。
        public abstract void End();

        // 结束本轮玩法并恢复初始画面；总管关闭窗口时调用。
        public abstract void ResetView();
    }

}
