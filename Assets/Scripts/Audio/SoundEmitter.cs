using System.Collections.Generic;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Audio
{
    [DisallowMultipleComponent]
    public sealed class SoundEmitter : MonoBehaviour
    {
        private readonly Dictionary<SoundId, SoundPlaybackId> active = new();
        public void SetPlaying(SoundId sound, bool playing, SoundBus bus = SoundBus.MiniGame,
            bool spatial = false)
        {
            if (!playing) { Stop(sound); return; }
            if (!isActiveAndEnabled || SoundManager.Instance == null) return;
            if (active.TryGetValue(sound, out var id))
            {
                if (SoundManager.Instance.IsPlaying(id)) return;
            }
            else { id = SoundPlaybackId.New(); active.Add(sound, id); }
            EventPipeline.Publish(new PlaySoundEvent(sound, bus, this, spatial,
                transform.position, spatial ? transform : null, id, true));
        }
        public void Stop(SoundId sound, SoundStopReason reason = SoundStopReason.Cancelled)
        {
            if (!active.TryGetValue(sound, out var id)) return;
            active.Remove(sound);
            EventPipeline.Publish(new StopSoundEvent(id, reason));
        }
        public void StopAll()
        {
            foreach (var id in active.Values)
                EventPipeline.Publish(new StopSoundEvent(id, SoundStopReason.OwnerRemoved));
            active.Clear();
        }
        private void OnDisable() => StopAll();
        private void OnDestroy() => StopAll();
    }
}
