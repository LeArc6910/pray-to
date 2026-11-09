using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Client
{
    // Plays the background music and the ambience (Art/MusicArt.cs) on looping 2D sources. A new song fades in over
    // the old one. A song in step with the one playing (a field's calm and fight, a boss's first two phases) starts
    // at the same place in the loop, so the music turns urgent on the beat: quickly into a fight, slowly out of it.
    // The music is as loud as the menu's music setting (Settings.MusicVolume); the ambience and the machine hum go
    // with the sound effects (Settings.Volume). Each loop is made the first time it is needed, or ahead of time
    // when Prepare says it soon will be, a part of a bar a frame (MusicArt.Making) so the making does not stall the
    // game.
    static class Music
    {
        const float FadeSeconds = 1.5f;
        const float IntoFightSeconds = 0.4f;
        const float OutOfFightSeconds = 3f;
        // How loud each kind is at full volume, under the sound effects.
        const float TuneLevel = 0.55f;
        const float AmbienceLevel = 0.35f;
        const float HumLevel = 0.3f;

        // At most this many songs are kept made (each half a minute of samples, a few megabytes): the ones used
        // longest ago go first.
        const int KeptSongs = 8;

        static readonly Dictionary<string, AudioClip> Songs = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> Used = new Dictionary<string, float>();
        static readonly Dictionary<Ambience, AudioClip> Ambiences = new Dictionary<Ambience, AudioClip>();
        static readonly List<Song> Wanted = new List<Song>();
        static GameObject _host;
        // Two tune sources, so one can fade out while the other fades in.
        static AudioSource[] _tunes;
        static AudioSource _ambience;
        static AudioSource _hum;
        static int _current;
        static Song? _song;
        static float _fade = FadeSeconds;
        // The song being made ahead, and how far.
        static Song? _makingSong;
        static IEnumerator<float[]> _making;
        static Ambience? _ambienceKind;

        // The song playing, by its key (MusicArt Song.Key); null for none.
        public static string Playing => _song?.Key;

        // Songs that may be wanted soon (the fight version of the field's tune, a boss's next phases): made ahead,
        // one at a time.
        public static void Prepare(Song song)
        {
            if (Songs.ContainsKey(song.Key)) Used[song.Key] = Time.unscaledTime;
            else if (!Wanted.Contains(song) && !Nullable.Equals(_makingSong, song)) Wanted.Add(song);
        }

        // A step of making the next wanted song.
        static void MakeAhead()
        {
            if (_making == null)
            {
                if (Wanted.Count == 0) return;
                _makingSong = Wanted[0];
                Wanted.RemoveAt(0);
                _making = MusicArt.Making(_makingSong.Value);
            }
            if (!_making.MoveNext())
            {
                _making = null;
                _makingSong = null;
            }
            else if (_making.Current != null)
            {
                string key = _makingSong.Value.Key;
                if (!Songs.ContainsKey(key)) Keep(key, Create(key, _making.Current));
                _making = null;
                _makingSong = null;
            }
        }

        public static void Update(Tune? tune, Ambience? ambience, float hum, float deltaTime) =>
            Update(tune is Tune t ? Song.Field(t) : (Song?)null, ambience, hum, deltaTime);

        // Once a frame: the song that should play (null for none), the ambience under it (null for none), and how
        // near a running machine is, from 0 (none) to 1 (beside it).
        public static void Update(Song? song, Ambience? ambience, float hum, float deltaTime)
        {
            Make();
            if (!Nullable.Equals(song, _song))
            {
                var before = _song;
                var playing = _tunes[_current];
                _song = song;
                _current = 1 - _current;
                var next = _tunes[_current];
                next.Stop();
                next.volume = 0;
                next.clip = song is Song s ? Clip(s) : null;
                bool inStep = before is Song a && song is Song b && a.InStepWith(b) && playing.isPlaying && playing.clip != null && next.clip != null
                    && playing.clip.samples == next.clip.samples;
                _fade = !inStep ? FadeSeconds : song.Value.Battle || song.Value.Phase > before.Value.Phase ? IntoFightSeconds : OutOfFightSeconds;
                if (inStep)
                {
                    next.Play();
                    next.timeSamples = playing.timeSamples;
                }
            }
            else
            {
                MakeAhead();
            }
            if (_song is Song now && Used.ContainsKey(now.Key)) Used[now.Key] = Time.unscaledTime;
            float music = Settings.MusicVolume / (float)Settings.MaxVolume * TuneLevel;
            for (int i = 0; i < _tunes.Length; i++)
            {
                var source = _tunes[i];
                float target = i == _current && source.clip != null ? music : 0;
                source.volume = Mathf.MoveTowards(source.volume, target, TuneLevel * deltaTime / _fade);
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
        static AudioClip Clip(Song song)
        {
            if (Songs.TryGetValue(song.Key, out var clip))
            {
                Used[song.Key] = Time.unscaledTime;
                return clip;
            }
            // Wanted now: finish it if it is being made ahead, else make it whole.
            float[] samples = null;
            if (_making != null && Nullable.Equals(_makingSong, song))
            {
                while (samples == null && _making.MoveNext()) samples = _making.Current;
                _making = null;
                _makingSong = null;
            }
            clip = Create(song.Key, samples ?? MusicArt.Make(song));
            Keep(song.Key, clip);
            Wanted.Remove(song);
            return clip;
        }

        // A song made, and the one used longest ago dropped if there are too many (never one a source holds).
        static void Keep(string key, AudioClip clip)
        {
            Songs[key] = clip;
            Used[key] = Time.unscaledTime;
            if (Songs.Count <= KeptSongs) return;
            string oldest = null;
            foreach (var pair in Songs)
            {
                if (pair.Value == _tunes[0].clip || pair.Value == _tunes[1].clip || pair.Key == key) continue;
                if (oldest == null || Used[pair.Key] < Used[oldest]) oldest = pair.Key;
            }
            if (oldest == null) return;
            Object.Destroy(Songs[oldest]);
            Songs.Remove(oldest);
            Used.Remove(oldest);
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
