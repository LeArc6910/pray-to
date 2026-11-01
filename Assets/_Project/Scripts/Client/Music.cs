using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Plays the background music and the ambience (Art/MusicArt.cs) on looping 2D sources. A new tune fades in over
    // the old one. The music is as loud as the menu's music setting (Settings.MusicVolume); the ambience and the
    // machine hum go with the sound effects (Settings.Volume). Each loop is made the first time it is needed.
    static class Music
    {
        const float FadeSeconds = 1.5f;
        // How loud each kind is at full volume, under the sound effects.
        const float TuneLevel = 0.55f;
        const float AmbienceLevel = 0.35f;
        const float HumLevel = 0.3f;

        static readonly Dictionary<Tune, AudioClip> Tunes = new Dictionary<Tune, AudioClip>();
        static readonly Dictionary<Ambience, AudioClip> Ambiences = new Dictionary<Ambience, AudioClip>();
        static GameObject _host;
        // Two tune sources, so one can fade out while the other fades in.
        static AudioSource[] _tunes;
        static AudioSource _ambience;
        static AudioSource _hum;
        static int _current;
        static Tune? _tune;
        static Ambience? _ambienceKind;

        // Once a frame: the tune that should play (null for none), the ambience under it (null for none), and how
        // near a running machine is, from 0 (none) to 1 (beside it).
        public static void Update(Tune? tune, Ambience? ambience, float hum, float deltaTime)
        {
            Make();
            if (tune != _tune)
            {
                _tune = tune;
                _current = 1 - _current;
                var next = _tunes[_current];
                next.Stop();
                next.volume = 0;
                next.clip = tune is Tune t ? Clip(t) : null;
            }
            float music = Settings.MusicVolume / (float)Settings.MaxVolume * TuneLevel;
            for (int i = 0; i < _tunes.Length; i++)
            {
                var source = _tunes[i];
                float target = i == _current && source.clip != null ? music : 0;
                source.volume = Mathf.MoveTowards(source.volume, target, TuneLevel * deltaTime / FadeSeconds);
                if (target > 0 && !source.isPlaying) source.Play();
                else if (target <= 0 && source.volume <= 0 && source.isPlaying) source.Stop();
            }

            float effects = Settings.Volume / (float)Settings.MaxVolume;
            if (ambience != _ambienceKind)
            {
                _ambienceKind = ambience;
                _ambience.Stop();
                _ambience.clip = ambience is Ambience a ? Clip(a) : null;
            }
            Loop(_ambience, effects * AmbienceLevel);
            if (hum > 0 && _hum.clip == null) _hum.clip = Clip(Ambience.Hum);
            Loop(_hum, Mathf.Clamp01(hum) * effects * HumLevel);
        }

        // Plays a loop at this volume, or stops it at none.
        static void Loop(AudioSource source, float volume)
        {
            source.volume = volume;
            if (volume > 0 && source.clip != null && !source.isPlaying) source.Play();
            else if ((volume <= 0 || source.clip == null) && source.isPlaying) source.Stop();
        }

        static void Make()
        {
            if (_host != null) return;
            _host = new GameObject("Music");
            Object.DontDestroyOnLoad(_host);
            _tunes = new[] { Source(), Source() };
            _ambience = Source();
            _hum = Source();
        }

        static AudioSource Source()
        {
            var source = _host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.volume = 0;
            return source;
        }

        // A browser only accepts a clip whose samples are all given at once, not streamed (Docs/ArtPipeline.md).
        static AudioClip Clip(Tune tune)
        {
            if (!Tunes.TryGetValue(tune, out var clip)) Tunes[tune] = clip = Create("tune_" + tune, MusicArt.Make(tune));
            return clip;
        }

        static AudioClip Clip(Ambience ambience)
        {
            if (!Ambiences.TryGetValue(ambience, out var clip)) Ambiences[ambience] = clip = Create("ambience_" + ambience, MusicArt.Make(ambience));
            return clip;
        }

        static AudioClip Create(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, MusicArt.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
