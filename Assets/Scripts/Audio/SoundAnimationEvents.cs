using UnityEngine;

namespace TapTapGameJam.Audio
{
    /// <summary>Place beside the Animator. Animation event: PlayFootstep(int soundNumber).
    /// 0 = resolve player form/NPC type; 6 = human; 26 = crawl; 34 = guard.</summary>
    [DisallowMultipleComponent]
    public sealed class SoundAnimationEvents : MonoBehaviour
    {
        public void PlayFootstep(int soundNumber)
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f) return;
            var player = GetComponentInParent<TapTapGameJam.Player.PlayerBasic>();
            var guard = GetComponentInParent<TapTapGameJam.Cockroach.Guard>();
            var npc = GetComponentInParent<TapTapGameJam.NPC.NPC>();
            SoundId id = soundNumber == 0
                ? player != null ? (player.IsHuman ? SoundId.HumanFootstep : SoundId.Crawl)
                    : guard != null ? SoundId.GuardFootstep : SoundId.HumanFootstep
                : (SoundId)soundNumber;
            if (id != SoundId.HumanFootstep && id != SoundId.Crawl && id != SoundId.GuardFootstep)
            { Debug.LogWarning("[Audio] PlayFootstep accepts 0, 6, 26 or 34.", this); return; }
            Object owner = player != null ? (Object)player : guard != null ? (Object)guard : npc != null ? (Object)npc : this;
            Sfx.At(id, transform.position, owner);
        }
    }
}
