using System;
using UnityEngine;

namespace TapTapGameJam.Audio
{
    [Serializable]
    public sealed class SoundDefinition
    {
        public SoundId id;
        public bool enabled = true;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 0.75f;
        public Vector2 pitch = Vector2.one;
        public bool loop;
        [Min(0f)] public float cooldown = 0.06f;
        public bool cooldownPerOwner;
        [Min(1)] public int maxInstances = 4;
        [Range(0, 100)] public int priority = 40;
        [Min(0f)] public float nearDistance = 2f;
        [Min(0.1f)] public float farDistance = 18f;
        [Tooltip("0 = full clip; positive = maximum audible duration per instance.")]
        [Min(0f)] public float maxDuration;
        [Min(0f)] public float fadeOut = 0.04f;
        [Tooltip("Additional volume per talent level above 1.")]
        [Min(0f)] public float volumePerLevel;
        [Min(0f)] public float pitchPerLevel;
    }

    [CreateAssetMenu(menuName = "TapTap/Audio/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.65f;
        [Range(0f, 1f)] public float effectsVolume = 0.8f;
        [Range(0f, 1f)] public float defeatDuckVolume = 0.25f;
        [Min(1)] public int voiceLimit = 32;
        public SoundDefinition[] sounds = Array.Empty<SoundDefinition>();

        public SoundDefinition Find(SoundId id)
        {
            foreach (var sound in sounds)
                if (sound != null && sound.id == id) return sound;
            return null;
        }
    }
}
