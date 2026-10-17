using System.Threading;
using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Audio
{
    public readonly struct SoundPlaybackId
    {
        private static long next;
        public long Value { get; }
        public bool IsValid => Value != 0;
        private SoundPlaybackId(long value) => Value = value;
        public static SoundPlaybackId New() => new SoundPlaybackId(Interlocked.Increment(ref next));
    }

    public readonly struct PlaySoundEvent : IEvent
    {
        public SoundId Sound { get; }
        public SoundPlaybackId Playback { get; }
        public Object Owner { get; }
        public Transform Follow { get; }
        public Vector3 Position { get; }
        public bool Spatial { get; }
        public bool Controlled { get; }
        public SoundBus Bus { get; }
        public int Level { get; }
        public PlaySoundEvent(SoundId sound, SoundBus bus = SoundBus.Interface,
            Object owner = null, bool spatial = false, Vector3 position = default,
            Transform follow = null, SoundPlaybackId playback = default, bool controlled = false,
            int level = 1)
        {
            Sound = sound; Bus = bus; Owner = owner; Spatial = spatial; Position = position;
            Follow = follow; Playback = playback; Controlled = controlled; Level = level;
        }
    }

    public readonly struct StopSoundEvent : IEvent
    {
        public SoundPlaybackId Playback { get; }
        public SoundStopReason Reason { get; }
        public StopSoundEvent(SoundPlaybackId playback, SoundStopReason reason = SoundStopReason.Cancelled)
        { Playback = playback; Reason = reason; }
    }
}
