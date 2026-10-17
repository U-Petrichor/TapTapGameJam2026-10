using TapTapGameJam.Events;
using UnityEngine;

namespace TapTapGameJam.Audio
{
    // All calls stay on Unity's main thread, like EventPipeline itself.
    public static class Sfx
    {
        public static int FeedbackSequence { get; private set; }
        public static void Play(SoundId id, Object owner = null, int level = 1)
        {
            FeedbackSequence++;
            EventPipeline.Publish(new PlaySoundEvent(id, owner: owner, level: level));
        }

        public static void At(SoundId id, Vector3 position, Object owner = null, SoundBus bus = SoundBus.World)
        {
            FeedbackSequence++;
            EventPipeline.Publish(new PlaySoundEvent(id, bus, owner, true, position));
        }

        public static SoundEmitter Emitter(Component owner)
        {
            var emitter = owner.GetComponent<SoundEmitter>();
            return emitter != null ? emitter : owner.gameObject.AddComponent<SoundEmitter>();
        }
    }
}
