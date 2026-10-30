using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Plays the sound effects (Art/SoundArt.cs). The clips are made once from code. A small pool of 2D audio sources
    // plays them, so sounds overlap without one cutting another off. A sound in the world gets quieter with
    // distance from the player and is not heard past Hearing. The same sound cannot start twice within MinGap, so
    // a swing that hits five slimes does not play five hits at once. Off in the menu (Settings.Sound).
    static class Sounds
    {
        const int Voices = 16;
        const float Hearing = 16;
        const float MinGap = 0.05f;
        // A little random pitch, so repeated sounds do not drone.
        const float PitchJitter = 0.06f;

        static readonly int Count = Enum.GetValues(typeof(Sound)).Length;
        static AudioClip[] _clips;
        static readonly float[] LastPlayed = new float[Count];
        static GameObject _host;
        static AudioSource[] _sources;
        static int _next;

        public static void Play(Sound sound, float volume = 1)
        {
            if (!Settings.Sound || volume <= 0) return;
            float now = Time.unscaledTime;
            int index = (int)sound;
            if (now - LastPlayed[index] < MinGap && LastPlayed[index] > 0) return;
            LastPlayed[index] = now;
            var source = NextSource();
            source.clip = Clip(sound);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = 1 + UnityEngine.Random.Range(-PitchJitter, PitchJitter);
            source.Play();
        }

        // A sound at a place in the world, heard from the listener's place.
        public static void PlayAt(Sound sound, Vector3 at, Vector3 listener, float volume = 1)
        {
            float distance = Vector2.Distance(at, listener);
            if (distance >= Hearing) return;
            Play(sound, volume * (1 - distance / Hearing));
        }

        static AudioSource NextSource()
        {
            if (_host == null)
            {
                _host = new GameObject("Sounds");
                Object.DontDestroyOnLoad(_host);
                _sources = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    _sources[i] = _host.AddComponent<AudioSource>();
                    _sources[i].playOnAwake = false;
                    _sources[i].spatialBlend = 0;
                }
            }
            // The next source that is free, or else the one that started longest ago.
            for (int i = 0; i < Voices; i++)
            {
                var source = _sources[(_next + i) % Voices];
                if (source.isPlaying) continue;
                _next = (_next + i + 1) % Voices;
                return source;
            }
            var oldest = _sources[_next];
            _next = (_next + 1) % Voices;
            return oldest;
        }

        // A browser only accepts a clip whose samples are all given at once, not streamed (Docs/ArtPipeline.md).
        static AudioClip Clip(Sound sound)
        {
            _clips ??= new AudioClip[Count];
            int index = (int)sound;
            if (_clips[index] != null) return _clips[index];
            var samples = SoundArt.Make(sound);
            var clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SoundArt.SampleRate, false);
            clip.SetData(samples, 0);
            _clips[index] = clip;
            return clip;
        }
    }
}
