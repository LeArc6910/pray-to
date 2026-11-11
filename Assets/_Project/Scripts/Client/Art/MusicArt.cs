using System;
using System.Collections.Generic;

namespace Game.Client
{
    // A tune that loops while the player is somewhere: one for each biome, one for a boss fight, and one for each
    // style of the story's places (2026-11-11), played inside them.
    enum Tune
    {
        Cave,
        Mushroom,
        Ice,
        Crystal,
        Lava,
        Boss,
        Abyss,
        Lake,
        Sand,
        Village,
        Kingdom,
        Hut,
        Nest,
        Harbor,
        Ruins,
        Mine,
    }

    // Sound that loops under the music: the cave's wind and drips, the lava's rumble and bubbles, the hum of a
    // running machine, and in the story's places (2026-11-11) a hearth's crackle or water lapping at a pier.
    enum Ambience
    {
        Cave,
        Lava,
        Hum,
        Hearth,
        Waves,
    }

    // What the music plays (2026-11-09): a field's tune, calm or in a fight, or a boss's theme at a phase.
    readonly struct Song : IEquatable<Song>
    {
        Song(Tune tune, bool battle, string boss, int phase, bool night = false)
        {
            Night = night;
            Tune = tune;
            Battle = battle;
            Boss = boss;
            Phase = phase;
        }

        public Tune Tune { get; }
        public bool Battle { get; }
        // A place's tune at night (2026-11-11): quieter, slower, no drums.
        public bool Night { get; }
        // The boss's id in bosses.csv, or null for a field's tune.
        public string Boss { get; }
        public int Phase { get; }

        public static Song Field(Tune tune, bool battle = false, bool night = false) => new Song(tune, battle, null, 0, night && !battle);

        // Phases past the last theme play the last.
        public static Song Of(string boss, int phase) => new Song(Tune.Boss, false, boss, Math.Clamp(phase, 1, MusicArt.BossPhases));

        public string Key => Boss != null ? $"boss_{Boss}_{Phase}" : Battle ? $"tune_{Tune}_battle" : Night ? $"tune_{Tune}_night" : $"tune_{Tune}";

        // Two songs that are one loop in two arrangements, the same length, tempo and melody: a field's calm and
        // fight, or a boss's first two phases. One can take over from the other at the same place (Music).
        public bool InStepWith(Song other) =>
            Boss == null ? other.Boss == null && Tune == other.Tune && !Night && !other.Night
            : Boss == other.Boss && Math.Max(Phase, other.Phase) <= 2;

        public bool Equals(Song other) => Tune == other.Tune && Battle == other.Battle && Boss == other.Boss && Phase == other.Phase && Night == other.Night;
        public override bool Equals(object obj) => obj is Song other && Equals(other);
        public override int GetHashCode() => Key.GetHashCode();
        public override string ToString() => Key;
    }

    // Background music and ambience, made from code like the sound effects (SoundArt): bass, an arpeggio, a melody
    // and sometimes a held pad on chiptune waves and bells over a chord progression, with drums played from 16-step
    // patterns. The melody is written by a fixed scramble from the tune, so a tune always comes out the same.
    // Each field has a calm tune and a fight version of it: the same length, tempo, chords and melody with a driving
    // bass, quick arpeggios and full drums, so the music can turn urgent mid-bar (Music). Each boss has a theme in
    // three phases: the second adds those layers the same way; the third is a semitone higher and faster with the
    // melody doubled an octave up. Everything wraps round the end of its loop, so the loop has no seam. Mono, at
    // SoundArt.SampleRate. See Docs/ArtPipeline.md.
    static class MusicArt
    {
        public const int SampleRate = SoundArt.SampleRate;
        public const int BossPhases = 3;
        // Songs are brought to this loudness and kept under this peak (ambience is normalised to its peak), below the
        // sound effects, which play over them.
        const float Loudness = 0.11f;
        const float Peak = 0.55f;

        enum Wave
        {
            Square,
            Pulse,
            Triangle,
            Saw,
            Sine,
            // A struck bell: a sine with a fainter partial 2.76 times as high.
            Bell,
        }

        // How a tune goes: its speed, key, scale, chords and instruments. Drum patterns are 16 steps a bar, x a hit,
        // X a loud one, . a rest.
        sealed class Style
        {
            public float Bpm;
            public int Root;
            public int[] Scale;
            // Scale degrees of the chords, each lasting BarsPerChord bars, round and round for Bars bars.
            public int[] Chords;
            public int BarsPerChord = 2;
            public int Bars = 8;
            public Wave Bass = Wave.Triangle;
            // Bass notes a bar: 1 or 2 held, 4 bouncing, 8 driving with the octave on the off-beats.
            public int BassNotes = 2;
            public float BassVolume = 0.32f;
            public Wave Arp = Wave.Pulse;
            // Arpeggio notes a beat, and how loud.
            public int ArpRate = 2;
            public float ArpVolume = 0.12f;
            public int ArpOctave = 1;
            public Wave Lead = Wave.Square;
            public int LeadOctave = 1;
            public float LeadVolume = 0.22f;
            // The melody again an octave up, this loud (0 for none).
            public float LeadDouble;
            // The share of melody eighths left silent.
            public float Rest = 0.4f;
            // The chord held under everything, swelling in (0 for none).
            public Wave Pad = Wave.Triangle;
            public float PadVolume;
            public string Kick;
            public string Snare;
            public string Hat;
            // Low drums: a tom, or the deep "doum" of a hand drum.
            public string Tom;
            // The hand drum's sharp "tek".
            public string Tek;
            public float Echo = 0.3f;
            public int Seed;

            public Style Copy() => (Style)MemberwiseClone();
        }

        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };
        static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
        static readonly int[] Harmonic = { 0, 2, 3, 5, 7, 8, 11 };
        // Phrygian with a major third, the "hijaz" of desert tunes.
        static readonly int[] Hijaz = { 0, 1, 4, 5, 7, 8, 10 };
        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };

        static Style StyleOf(Tune tune) => tune switch
        {
            // Whimsical and bouncing: a dorian melody on a soft triangle over a hopping bass and a woodblock tick.
            Tune.Mushroom => new Style
            {
                Bpm = 84, Root = 50, Scale = Dorian, Chords = new[] { 0, 3, 0, 6 }, Lead = Wave.Triangle, LeadVolume = 0.3f,
                Arp = Wave.Pulse, BassNotes = 4, BassVolume = 0.26f, Rest = 0.35f, Hat = "x...x...x...x.x.", Seed = 2,
            },
            // A music box: lydian bells high over a held bass, a glassy pad, wide echoes.
            Tune.Ice => new Style
            {
                Bpm = 66, Root = 52, Scale = Lydian, Chords = new[] { 0, 1, 0, 4 }, Lead = Wave.Bell, LeadOctave = 2, LeadVolume = 0.25f,
                Arp = Wave.Sine, ArpRate = 4, ArpOctave = 2, ArpVolume = 0.08f, BassNotes = 1, Rest = 0.5f, Pad = Wave.Sine, PadVolume = 0.07f,
                Echo = 0.45f, Seed = 3,
            },
            // Glittering: harmonic minor bells running in sixteenths, a pulse melody, a faint shimmer of hats.
            Tune.Crystal => new Style
            {
                Bpm = 76, Root = 49, Scale = Harmonic, Chords = new[] { 0, 3, 5, 4 }, Lead = Wave.Pulse, LeadVolume = 0.2f,
                Arp = Wave.Bell, ArpRate = 4, ArpOctave = 2, ArpVolume = 0.12f, Rest = 0.45f, Hat = "..x...x...x...x.", Echo = 0.4f, Seed = 4,
            },
            // Heavy and hot: phrygian over a driving saw bass, toms rolling under the beat.
            Tune.Lava => new Style
            {
                Bpm = 92, Root = 40, Scale = Phrygian, Chords = new[] { 0, 1, 0, 6 }, Bass = Wave.Saw, BassNotes = 8, Lead = Wave.Square,
                LeadVolume = 0.2f, Arp = Wave.Pulse, ArpVolume = 0.08f, Rest = 0.45f, Kick = "x.....x...x.....", Snare = "....x.......x...",
                Tom = "..........x...x.", Hat = "x.x.x.x.x.x.x.x.", Echo = 0.2f, Seed = 5,
            },
            // Slow and cold: a sine melody high over a held bass and a dark drone, wide echoes.
            Tune.Abyss => new Style
            {
                Bpm = 60, Root = 47, Scale = Phrygian, Chords = new[] { 0, 1, 5, 6 }, Lead = Wave.Sine, LeadOctave = 2, LeadVolume = 0.24f,
                Arp = Wave.Triangle, ArpRate = 2, ArpOctave = 1, ArpVolume = 0.1f, BassNotes = 1, Rest = 0.55f, Pad = Wave.Saw, PadVolume = 0.05f,
                Echo = 0.5f, Seed = 7,
            },
            // Gently rocking: a dorian melody over a slow bass, arpeggios like ripples, long echoes.
            Tune.Lake => new Style
            {
                Bpm = 72, Root = 48, Scale = Dorian, Chords = new[] { 0, 4, 3, 0 }, Lead = Wave.Sine, LeadOctave = 1, LeadVolume = 0.26f,
                Arp = Wave.Triangle, ArpRate = 3, ArpOctave = 2, ArpVolume = 0.12f, BassNotes = 2, Rest = 0.5f, Pad = Wave.Sine, PadVolume = 0.05f,
                Echo = 0.45f, Seed = 8,
            },
            // Dry and swaying: a hijaz melody over a plucked bass and a hand drum's doum and tek, short echoes.
            Tune.Sand => new Style
            {
                Bpm = 96, Root = 45, Scale = Hijaz, Chords = new[] { 0, 1, 0, 6 }, Lead = Wave.Pulse, LeadOctave = 1, LeadVolume = 0.2f,
                Arp = Wave.Triangle, ArpRate = 2, ArpOctave = 1, ArpVolume = 0.1f, BassNotes = 4, Rest = 0.45f,
                Tom = "x.....x...x.....", Tek = "...x.x.x...x.x.x", Echo = 0.2f, Seed = 9,
            },
            // The story's places (2026-11-11, Docs/ArtPipeline.md 장소의 음악). The dawn village: a warm folk tune, a
            // triangle melody in major over a plucked bass and a light shaker, like a hearth after the war.
            Tune.Village => new Style
            {
                Bpm = 88, Root = 48, Scale = Major, Chords = new[] { 0, 3, 4, 0 }, Lead = Wave.Triangle, LeadVolume = 0.28f,
                Arp = Wave.Pulse, ArpRate = 2, ArpVolume = 0.09f, BassNotes = 2, BassVolume = 0.24f, Rest = 0.4f, Hat = "..x...x...x...x.",
                Echo = 0.25f, Seed = 31,
            },
            // The royal city: stately, a square fanfare in major over a marching snare and a full pad.
            Tune.Kingdom => new Style
            {
                Bpm = 96, Root = 50, Scale = Major, Chords = new[] { 0, 4, 5, 3 }, Lead = Wave.Square, LeadVolume = 0.22f,
                Arp = Wave.Pulse, ArpRate = 2, ArpVolume = 0.1f, BassNotes = 4, Rest = 0.35f, Pad = Wave.Triangle, PadVolume = 0.06f,
                Kick = "x.......x.......", Snare = "....x.......x.xx", Echo = 0.2f, Seed = 32,
            },
            // The hero's hut: quiet and alone, a sine melody over a held bass, a soft pad and long echoes.
            Tune.Hut => new Style
            {
                Bpm = 64, Root = 45, Scale = Dorian, Chords = new[] { 0, 3, 0, 4 }, Lead = Wave.Sine, LeadOctave = 1, LeadVolume = 0.26f,
                Arp = Wave.Triangle, ArpRate = 1, ArpVolume = 0.08f, BassNotes = 1, Rest = 0.55f, Pad = Wave.Triangle, PadVolume = 0.06f,
                Echo = 0.45f, Seed = 33,
            },
            // The demon nest: low and uneasy, harmonic minor over a dark saw drone and a slow hand drum.
            Tune.Nest => new Style
            {
                Bpm = 70, Root = 42, Scale = Harmonic, Chords = new[] { 0, 5, 1, 4 }, Lead = Wave.Pulse, LeadVolume = 0.2f,
                Arp = Wave.Triangle, ArpRate = 2, ArpVolume = 0.08f, BassNotes = 1, Rest = 0.5f, Pad = Wave.Saw, PadVolume = 0.05f,
                Tom = "x.......x..x....", Echo = 0.35f, Seed = 34,
            },
            // The misty harbour: lilting mixolydian, arpeggios in threes like water, a slow rocking bass.
            Tune.Harbor => new Style
            {
                Bpm = 80, Root = 47, Scale = Mixolydian, Chords = new[] { 0, 6, 3, 0 }, Lead = Wave.Triangle, LeadOctave = 1, LeadVolume = 0.25f,
                Arp = Wave.Triangle, ArpRate = 3, ArpOctave = 1, ArpVolume = 0.11f, BassNotes = 2, Rest = 0.45f, Pad = Wave.Sine,
                PadVolume = 0.05f, Echo = 0.4f, Seed = 35,
            },
            // The forgotten temple: lydian bells, very slow, over a held sine and the widest echoes.
            Tune.Ruins => new Style
            {
                Bpm = 58, Root = 50, Scale = Lydian, Chords = new[] { 0, 1, 4, 0 }, Lead = Wave.Bell, LeadOctave = 1, LeadVolume = 0.24f,
                Arp = Wave.Sine, ArpRate = 2, ArpOctave = 2, ArpVolume = 0.07f, BassNotes = 1, Rest = 0.6f, Pad = Wave.Sine, PadVolume = 0.07f,
                Echo = 0.55f, Seed = 36,
            },
            // The mining village: a work song, dorian on a pulse over a walking bass and a hammer's tek on the beat.
            Tune.Mine => new Style
            {
                Bpm = 100, Root = 43, Scale = Dorian, Chords = new[] { 0, 3, 6, 4 }, Lead = Wave.Pulse, LeadVolume = 0.22f,
                Arp = Wave.Triangle, ArpRate = 2, ArpVolume = 0.08f, BassNotes = 4, BassVolume = 0.26f, Rest = 0.4f,
                Kick = "x.......x.......", Tek = "x...x...x...x...", Echo = 0.15f, Seed = 37,
            },
            Tune.Boss => BossStyle(null),
            // The home caves: a slow minor melody with echoes over a held bass and a soft pad.
            _ => new Style
            {
                Bpm = 70, Root = 45, Scale = Minor, Chords = new[] { 0, 5, 2, 6 }, Lead = Wave.Triangle, LeadVolume = 0.3f,
                Arp = Wave.Pulse, ArpVolume = 0.1f, BassNotes = 1, Rest = 0.5f, Pad = Wave.Triangle, PadVolume = 0.05f, Echo = 0.4f, Seed = 1,
            },
        };

        // A field's tune in a fight: the same loop driven hard. The melody (seed, scale, rest, octave), chords, tempo
        // and length stay, so the two line up beat for beat.
        static Style Battle(Style calm)
        {
            var style = calm.Copy();
            style.Bass = Wave.Saw;
            style.BassNotes = 8;
            style.BassVolume = 0.3f;
            style.ArpRate = 4;
            style.ArpVolume = Math.Max(calm.ArpVolume, 0.11f);
            if (style.Lead == Wave.Sine || style.Lead == Wave.Triangle) style.Lead = Wave.Square;
            style.LeadVolume = Math.Min(0.24f, calm.LeadVolume);
            style.PadVolume = 0;
            style.Echo = Math.Min(calm.Echo, 0.18f);
            // Slow tunes get the drums in double time so the fight still runs.
            bool slow = calm.Bpm < 85;
            style.Kick = slow ? "x..x..x.x..x..x." : "x...x...x...x.x.";
            style.Snare = slow ? "....X..x....X..x" : "....X.......X...";
            style.Hat = slow ? "xxxxxxxxxxxxxxxx" : "x.xxx.xxx.xxx.xx";
            style.Tom = calm.Tek != null ? "x..x..x...x..x.." : "..............xx";
            if (calm.Tek != null) style.Tek = "x.xx.xx.x.xx.xxx";
            return style;
        }

        // The tunes of the story's places, which have a night arrangement too.
        public static readonly Tune[] PlaceTunes = { Tune.Village, Tune.Kingdom, Tune.Hut, Tune.Nest, Tune.Harbor, Tune.Ruins, Tune.Mine };

        // A place's tune at night (2026-11-11): the same melody slower and softer, the drums gone, the arpeggio
        // slowed to one note a beat, a held pad under it and longer echoes, as if heard from outside a lit window.
        static Style Night(Style day)
        {
            var style = day.Copy();
            style.Bpm = day.Bpm * 0.85f;
            style.Kick = null;
            style.Snare = null;
            style.Hat = null;
            style.Tom = null;
            style.Tek = null;
            style.BassNotes = 1;
            style.ArpRate = 1;
            style.ArpVolume = day.ArpVolume * 0.7f;
            style.LeadVolume = day.LeadVolume * 0.85f;
            if (style.Lead == Wave.Square || style.Lead == Wave.Saw) style.Lead = Wave.Triangle;
            if (day.PadVolume <= 0) style.Pad = Wave.Sine;
            style.PadVolume = Math.Max(day.PadVolume, 0.06f);
            style.Echo = Math.Max(day.Echo, 0.45f);
            style.Rest = Math.Min(0.7f, day.Rest + 0.1f);
            return style;
        }

        // A boss's theme, in the spirit of its place and of itself; null for a boss without one of its own.
        static Style BossStyle(string boss) => boss switch
        {
            // The stone golem of the home caves: minor, stomping toms and a heavy kick.
            "cave_warden" => new Style
            {
                Bpm = 120, Root = 45, Scale = Minor, Chords = new[] { 0, 5, 3, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw, BassNotes = 4,
                Lead = Wave.Square, LeadVolume = 0.22f, Arp = Wave.Pulse, ArpRate = 2, ArpVolume = 0.1f, Rest = 0.3f,
                Kick = "x.....x.x.......", Snare = "....x.......x...", Tom = "........x...x.x.", Echo = 0.15f, Seed = 21,
            },
            // The crystal spider: harmonic minor, bells skittering in sixteenths like legs, a ticking hat.
            "crystal_queen" => new Style
            {
                Bpm = 132, Root = 49, Scale = Harmonic, Chords = new[] { 0, 5, 1, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Triangle,
                BassNotes = 4, Lead = Wave.Pulse, LeadVolume = 0.2f, Arp = Wave.Bell, ArpRate = 4, ArpOctave = 2, ArpVolume = 0.12f, Rest = 0.3f,
                Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "xxx.xxx.xxx.xxx.", Echo = 0.2f, Seed = 22,
            },
            // The lava titan: low phrygian, a saw melody over a grinding bass and pounding drums.
            "lava_titan" => new Style
            {
                Bpm = 108, Root = 40, Scale = Phrygian, Chords = new[] { 0, 1, 0, 6 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw, BassNotes = 8,
                Lead = Wave.Saw, LeadVolume = 0.18f, Arp = Wave.Pulse, ArpRate = 2, ArpVolume = 0.08f, Rest = 0.3f,
                Kick = "x.x...x.x.x...x.", Snare = "....X.......X...", Tom = "......x.......xx", Echo = 0.12f, Seed = 23,
            },
            // The lord of the void: slow and vast, a cold sine melody over a dark choir of a pad.
            "void_lord" => new Style
            {
                Bpm = 100, Root = 47, Scale = Phrygian, Chords = new[] { 0, 1, 5, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw, BassNotes = 2,
                Lead = Wave.Sine, LeadOctave = 2, LeadVolume = 0.24f, Arp = Wave.Triangle, ArpRate = 2, ArpVolume = 0.1f, Rest = 0.35f,
                Pad = Wave.Saw, PadVolume = 0.06f, Kick = "x.......x.......", Snare = "........X.......", Tom = "............x.x.",
                Echo = 0.35f, Seed = 24,
            },
            // The tide serpent: dorian, rolling arpeggios in threes like waves.
            "tide_serpent" => new Style
            {
                Bpm = 126, Root = 48, Scale = Dorian, Chords = new[] { 0, 4, 3, 6 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Triangle,
                BassNotes = 4, Lead = Wave.Triangle, LeadVolume = 0.26f, Arp = Wave.Triangle, ArpRate = 3, ArpOctave = 2, ArpVolume = 0.12f,
                Rest = 0.3f, Kick = "x..x..x...x..x..", Snare = "....x.......x...", Echo = 0.25f, Seed = 25,
            },
            // The antlion in the sand: hijaz over hand drums.
            "antlion" => new Style
            {
                Bpm = 138, Root = 45, Scale = Hijaz, Chords = new[] { 0, 1, 6, 0 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw, BassNotes = 4,
                Lead = Wave.Pulse, LeadVolume = 0.2f, Arp = Wave.Triangle, ArpRate = 2, ArpVolume = 0.1f, Rest = 0.3f,
                Kick = "x.......x.......", Tom = "x.....x...x.....", Tek = "...x.x.x...x.x.x", Echo = 0.15f, Seed = 26,
            },
            // The mad general of the demon nest (2026-11-10): low phrygian war drums under a burning saw lead.
            "mad_general" => new Style
            {
                Bpm = 116, Root = 43, Scale = Phrygian, Chords = new[] { 0, 1, 6, 5 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw, BassNotes = 8,
                Lead = Wave.Saw, LeadVolume = 0.2f, Arp = Wave.Square, ArpRate = 2, ArpVolume = 0.08f, Rest = 0.3f,
                Kick = "x...x.x.x...x.x.", Snare = "....X.......X...", Tom = "..x.....x.x...x.", Echo = 0.15f, Seed = 27,
            },
            // The crown wraith under the lake (2026-11-11): a drowned waltz, bells over rolling arpeggios in threes and a
            // cold pad, the drums slow and far off.
            "crown_wraith" => new Style
            {
                Bpm = 112, Root = 47, Scale = Harmonic, Chords = new[] { 0, 5, 3, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Triangle,
                BassNotes = 2, Lead = Wave.Bell, LeadOctave = 1, LeadVolume = 0.24f, Arp = Wave.Triangle, ArpRate = 3, ArpOctave = 1,
                ArpVolume = 0.12f, Rest = 0.3f, Pad = Wave.Sine, PadVolume = 0.06f, Kick = "x...........x...", Tom = "......x.......x.",
                Echo = 0.4f, Seed = 28,
            },
            // Any other boss: fast harmonic minor with driving drums.
            _ => new Style
            {
                Bpm = 140, Root = 45, Scale = Harmonic, Chords = new[] { 0, 5, 3, 4 }, BarsPerChord = 1, Bars = 16, Bass = Wave.Saw,
                BassNotes = 8, Lead = Wave.Square, LeadVolume = 0.22f, Arp = Wave.Pulse, ArpRate = 4, ArpVolume = 0.1f, Rest = 0.25f,
                Kick = "x...x...x...x...", Snare = "....x.......x...", Hat = "x.xxx.xxx.xxx.xx", Echo = 0.15f, Seed = 6,
            },
        };

        // The boss's theme at a phase: the first as written; the second with the bass driving, sixteenth arpeggios
        // and hats added (the same loop); the third a semitone up and an eighth faster with the melody doubled.
        static Style BossPhase(string boss, int phase)
        {
            var style = BossStyle(boss);
            if (phase < 2) return style;
            style.BassNotes = 8;
            style.ArpRate = 4;
            style.ArpVolume = Math.Max(style.ArpVolume, 0.11f);
            style.Hat ??= "x.xxx.xxx.xxx.xx";
            if (style.Snare != null) style.Snare = style.Snare.Substring(0, 15) + "x";
            if (phase < 3) return style;
            style.Root += 1;
            style.Bpm *= 1.12f;
            style.LeadDouble = 0.1f;
            style.Hat = "xxxxxxxxxxxxxxxx";
            if (style.Kick != null) style.Kick = "x...x...x...x.x.";
            return style;
        }

        // Samples in one bar of the song, for changing songs on a bar line (Music, 2026-11-11).
        public static int BarSamples(Song song)
        {
            var style = song.Boss != null ? BossPhase(song.Boss, song.Phase)
                : song.Battle ? Battle(StyleOf(song.Tune))
                : song.Night ? Night(StyleOf(song.Tune))
                : StyleOf(song.Tune);
            return Math.Max(1, Samples(60 / style.Bpm * 4));
        }

        public static float[] Make(Tune tune) => Make(Song.Field(tune));

        public static float[] Make(Song song)
        {
            var steps = Making(song);
            while (steps.MoveNext())
            {
                if (steps.Current != null) return steps.Current;
            }
            throw new InvalidOperationException(song.Key);
        }

        // The song made a little at a time, so it can be made ahead over several frames without a stall (Music): a
        // step per part of a bar and per phrase, null until the last, which is the finished loop.
        public static IEnumerator<float[]> Making(Song song)
        {
            var style = song.Boss != null ? BossPhase(song.Boss, song.Phase)
                : song.Battle ? Battle(StyleOf(song.Tune))
                : song.Night ? Night(StyleOf(song.Tune))
                : StyleOf(song.Tune);
            float beat = 60 / style.Bpm;
            float bar = beat * 4;
            var buffer = new float[Samples(bar * style.Bars)];
            var lead = new float[buffer.Length];
            var random = new Scramble(style.Seed);

            for (int b = 0; b < style.Bars; b++)
            {
                int degree = style.Chords[b / style.BarsPerChord % style.Chords.Length];
                var chord = new[] { Pitch(style, degree, 0), Pitch(style, degree + 2, 0), Pitch(style, degree + 4, 0) };
                float start = b * bar;

                // Bass: the chord's root, held, bouncing or driving in eighths with the octave on the off-beats.
                float bassLength = bar / style.BassNotes;
                for (int i = 0; i < style.BassNotes; i++)
                {
                    int note = chord[0] - 12 + (style.BassNotes == 8 && i % 2 == 1 ? 12 : 0);
                    Note(buffer, style.Bass, start + i * bassLength, bassLength * (style.BassNotes == 4 ? 0.6f : 0.9f), note, style.BassVolume,
                        decay: style.BassNotes >= 4 ? 4 : 1.2f);
                }

                yield return null;

                // Arpeggio: up and down the chord.
                int steps = style.ArpRate * 4;
                float arpLength = beat / style.ArpRate;
                int[] order = { 0, 1, 2, 1 };
                for (int i = 0; i < steps; i++)
                {
                    int note = chord[order[i % order.Length]] + 12 * style.ArpOctave;
                    Note(buffer, style.Arp, start + i * arpLength, arpLength * 0.8f, note, style.ArpVolume, decay: 3);
                }

                yield return null;

                // The pad: the chord held through its bars, swelling in and out.
                if (style.PadVolume > 0 && b % style.BarsPerChord == 0)
                {
                    float length = bar * style.BarsPerChord;
                    foreach (int note in chord) Note(buffer, style.Pad, start, length, note, style.PadVolume, decay: 0.6f, attack: length * 0.3f, vibrato: 0.004f);
                }

                yield return null;
                Drums(buffer, style, start, beat);
                yield return null;
            }

            // The melody: two-bar phrases A B A C, where C is A ending on the root. Bars past eight repeat them.
            var phraseA = Phrase(style, random);
            var phraseB = Phrase(style, random);
            var phraseC = new List<(int Slot, int Note, int Length)>(phraseA);
            if (phraseC.Count > 0)
            {
                var last = phraseC[phraseC.Count - 1];
                phraseC[phraseC.Count - 1] = (last.Slot, Pitch(style, 0, style.LeadOctave), last.Length);
            }
            var phrases = new[] { phraseA, phraseB, phraseA, phraseC };
            float eighth = beat / 2;
            for (int p = 0; p < style.Bars / 2; p++)
            {
                // Each phrase is played over the chords of its own two bars, moved to fit them.
                foreach (var (slot, note, length) in phrases[p % phrases.Length])
                {
                    int degree = style.Chords[(p * 2 + slot / 8) / style.BarsPerChord % style.Chords.Length];
                    int fitted = Fit(style, note, degree, slot % 8 == 0);
                    float at = p * 2 * bar + slot * eighth;
                    float vibrato = style.Lead == Wave.Sine || style.Lead == Wave.Triangle ? 0.006f : 0;
                    Note(lead, style.Lead, at, length * eighth * 0.85f, fitted, style.LeadVolume, decay: 1.5f, vibrato: vibrato);
                    if (style.LeadDouble > 0) Note(lead, Wave.Pulse, at, length * eighth * 0.7f, fitted + 12, style.LeadDouble, decay: 2);
                }
                yield return null;
            }
            // A dotted-eighth echo on the melody, wrapping round like everything else.
            int delay = Samples(eighth * 1.5f);
            for (int i = 0; i < lead.Length; i++)
            {
                buffer[i] += lead[i];
                buffer[(i + delay) % buffer.Length] += lead[i] * style.Echo;
                buffer[(i + delay * 2) % buffer.Length] += lead[i] * style.Echo * style.Echo;
            }
            yield return Levelled(buffer);
        }

        // A song brought to an even loudness (Loudness, the root mean square) whatever its drums, so a fight or a
        // later phase is not quieter than the calm tune; peaks over Peak are rounded off softly instead of clipping.
        static float[] Levelled(float[] buffer)
        {
            double sum = 0;
            foreach (float sample in buffer) sum += sample * sample;
            float rms = (float)Math.Sqrt(sum / Math.Max(1, buffer.Length));
            if (rms <= 0) return buffer;
            float scale = Loudness / rms;
            for (int i = 0; i < buffer.Length; i++) buffer[i] = Peak * (float)Math.Tanh(buffer[i] * scale / Peak);
            return buffer;
        }

        public static float[] Make(Ambience ambience)
        {
            switch (ambience)
            {
                case Ambience.Lava:
                {
                    float seconds = 20;
                    var buffer = Wind(seconds, smoothing: 0.006f, volume: 1, swell: 0.4f, seed: 11);
                    var random = new Scramble(12);
                    for (float t = 0.3f; t < seconds; t += 0.25f + random.Next() * 1.1f)
                    {
                        // A bubble rising and popping.
                        float from = 140 + random.Next() * 120;
                        Sweep(buffer, Wave.Sine, t, 0.09f, from, from * 2.6f, 0.35f + random.Next() * 0.25f);
                    }
                    return Normalised(buffer, 0.45f);
                }
                case Ambience.Hearth:
                {
                    // A fire crackling: a low warm rush with pops and snaps.
                    float seconds = 16;
                    var buffer = Wind(seconds, smoothing: 0.04f, volume: 0.35f, swell: 0.3f, seed: 15);
                    var random = new Scramble(16);
                    for (float t = 0.1f; t < seconds; t += 0.05f + random.Next() * 0.45f)
                    {
                        Click(buffer, t, 0.012f + random.Next() * 0.02f, 0.4f + random.Next() * 0.6f, seed: 100 + (int)(t * 50), hiss: random.Next() < 0.5f);
                    }
                    return Normalised(LowPass(buffer, 0.5f), 0.4f);
                }
                case Ambience.Waves:
                {
                    // Water lapping at a pier: slow swells of soft noise, a wave's wash every few seconds, a creak now
                    // and then.
                    float seconds = 24;
                    var buffer = Wind(seconds, smoothing: 0.012f, volume: 0.5f, swell: 0.8f, seed: 17);
                    var random = new Scramble(18);
                    for (float t = 0.5f; t < seconds; t += 2.2f + random.Next() * 1.8f)
                    {
                        Click(buffer, t, 0.9f + random.Next() * 0.5f, 0.25f + random.Next() * 0.15f, seed: 200 + (int)(t * 10));
                        if (random.Next() < 0.3f) Sweep(buffer, Wave.Triangle, t + 0.6f, 0.25f, 180, 140, 0.12f);
                    }
                    return Normalised(LowPass(buffer, 0.3f), 0.4f);
                }
                case Ambience.Hum:
                {
                    // Two seconds holds a whole number of cycles of both tones, and ticks every half second.
                    float seconds = 2;
                    var buffer = new float[Samples(seconds)];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        double t = i / (double)SampleRate;
                        double saw = 2 * (t * 55 - Math.Floor(t * 55 + 0.5));
                        double square = Math.Sin(2 * Math.PI * 110 * t) >= 0 ? 1 : -1;
                        buffer[i] = (float)(saw * 0.5 + square * 0.12);
                    }
                    var smooth = LowPass(buffer, 0.08f);
                    for (int k = 0; k < 4; k++) Click(smooth, k * 0.5f, 0.04f, 0.6f, seed: 20 + k);
                    return Normalised(smooth, 0.4f);
                }
                default:
                {
                    float seconds = 24;
                    var buffer = Wind(seconds, smoothing: 0.02f, volume: 0.6f, swell: 0.7f, seed: 13);
                    var random = new Scramble(14);
                    for (float t = 0.8f; t < seconds; t += 1.2f + random.Next() * 2.8f)
                    {
                        // A drip far off in the cave, with its echoes.
                        float pitch = 1500 + random.Next() * 900;
                        float volume = 0.5f + random.Next() * 0.5f;
                        for (int echo = 0; echo < 3; echo++)
                        {
                            Sweep(buffer, Wave.Sine, t + echo * 0.21f, 0.06f, pitch, pitch * 0.55f, volume * (float)Math.Pow(0.35, echo));
                        }
                    }
                    return Normalised(buffer, 0.4f);
                }
            }
        }

        // ---- Notes ----

        // The note in the scale degree steps up from the root, octaves up.
        static int Pitch(Style style, int degree, int octaves)
        {
            int n = style.Scale.Length;
            int octave = (int)Math.Floor(degree / (double)n);
            int step = degree - octave * n;
            return style.Root + 12 * (octave + octaves) + style.Scale[step];
        }

        // Two bars of melody in eighths: (slot 0-15, note, length in eighths). Notes walk the scale in small steps
        // and land on the chord on strong beats.
        static List<(int Slot, int Note, int Length)> Phrase(Style style, Scramble random)
        {
            var notes = new List<(int, int, int)>();
            int degree = (int)(random.Next() * 5);
            int slot = 0;
            while (slot < 16)
            {
                int length = random.Next() < 0.3f ? 2 : random.Next() < 0.15f ? 3 : 1;
                length = Math.Min(length, 16 - slot);
                bool strong = slot % 4 == 0;
                if (!strong && random.Next() < style.Rest)
                {
                    slot += length;
                    continue;
                }
                float r = random.Next();
                degree += r < 0.35f ? 1 : r < 0.7f ? -1 : r < 0.85f ? 2 : -2;
                degree = Math.Clamp(degree, -2, 9);
                notes.Add((slot, Pitch(style, degree, style.LeadOctave), length));
                slot += length;
            }
            return notes;
        }

        // On a strong beat, the nearest note of the chord; otherwise the note as written.
        static int Fit(Style style, int note, int degree, bool strong)
        {
            if (!strong) return note;
            int best = note;
            int distance = int.MaxValue;
            foreach (int tone in new[] { degree, degree + 2, degree + 4 })
            {
                for (int octave = -1; octave <= 3; octave++)
                {
                    int candidate = Pitch(style, tone, octave);
                    int d = Math.Abs(candidate - note);
                    if (d >= distance) continue;
                    distance = d;
                    best = candidate;
                }
            }
            return best;
        }

        static double Frequency(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);

        // A note added into the loop, wrapping past its end: a quick attack, then a decay over its length.
        static void Note(float[] buffer, Wave wave, float start, float seconds, int note, float volume, float decay = 2, float vibrato = 0,
            float attack = 0.006f)
        {
            double frequency = Frequency(note);
            int first = Samples(start);
            int count = Samples(seconds);
            double phase = 0;
            // The decay as a factor a sample, so the loop multiplies instead of taking an exponential: the web build
            // makes a tune the first time it is needed, while the game runs.
            float fall = 1;
            float fallStep = (float)Math.Exp(-decay / (seconds * SampleRate));
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                phase += (vibrato > 0 ? frequency * (1 + vibrato * Math.Sin(2 * Math.PI * 5.5 * t)) : frequency) / SampleRate;
                float envelope = Math.Min(1, t / attack) * fall * Math.Min(1, (seconds - t) / Math.Max(0.01f, attack * 0.5f));
                fall *= fallStep;
                buffer[(first + i) % buffer.Length] += Value(wave, phase) * volume * Math.Max(0, envelope);
            }
        }

        // A tone swept in pitch with a sharp attack and a fast fall: drips and bubbles.
        static void Sweep(float[] buffer, Wave wave, float start, float seconds, float from, float to, float volume)
        {
            int first = Samples(start);
            int count = Samples(seconds);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                phase += from * Math.Pow(to / from, t / seconds) / SampleRate;
                float envelope = Math.Min(1, t / 0.003f) * (float)Math.Exp(-4 * t / seconds) * Math.Min(1, (seconds - t) / 0.005f);
                buffer[(first + i) % buffer.Length] += Value(wave, phase) * volume * Math.Max(0, envelope);
            }
        }

        static float Value(Wave wave, double phase)
        {
            double p = phase - Math.Floor(phase);
            return wave switch
            {
                Wave.Square => p < 0.5 ? 0.6f : -0.6f,
                Wave.Pulse => p < 0.25 ? 0.5f : -0.5f,
                Wave.Triangle => (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p),
                Wave.Saw => (float)(2 * p - 1) * 0.6f,
                Wave.Bell => (float)(Math.Sin(2 * Math.PI * p) * 0.75 + Math.Sin(2 * Math.PI * 2.76 * phase) * 0.25),
                _ => (float)Math.Sin(2 * Math.PI * p),
            };
        }

        // The style's drums for a bar, from their 16-step patterns: a kick (a sine falling fast), a snare, hats, a tom
        // or a hand drum's doum (lower sines falling) and its tek (a sharp tick).
        static void Drums(float[] buffer, Style style, float start, float beat)
        {
            float step = beat / 4;
            for (int i = 0; i < 16; i++)
            {
                float at = start + i * step;
                float Loud(string pattern, float volume) => pattern == null || i >= pattern.Length || pattern[i] == '.' ? 0 : pattern[i] == 'X' ? volume * 1.35f : volume;
                float kick = Loud(style.Kick, 0.55f);
                if (kick > 0) Sweep(buffer, Wave.Sine, at, 0.16f, 130, 42, kick);
                float snare = Loud(style.Snare, 0.3f);
                if (snare > 0) Click(buffer, at, 0.12f, snare, seed: 3 + i);
                float hat = Loud(style.Hat, i % 4 == 0 ? 0.1f : 0.07f);
                if (hat > 0) Click(buffer, at, 0.03f, hat, seed: 9 + i, hiss: true);
                float tom = Loud(style.Tom, 0.42f);
                if (tom > 0) Sweep(buffer, Wave.Sine, at, 0.22f, style.Tek != null ? 120 : 190, style.Tek != null ? 70 : 90, tom);
                float tek = Loud(style.Tek, 0.2f);
                if (tek > 0)
                {
                    Click(buffer, at, 0.05f, tek, seed: 40 + i, hiss: true);
                    Sweep(buffer, Wave.Triangle, at, 0.04f, 900, 600, tek * 0.6f);
                }
            }
        }

        // A burst of noise: a snare or a tick, or with hiss a hat.
        static void Click(float[] buffer, float start, float seconds, float volume, int seed, bool hiss = false)
        {
            int first = Samples(start);
            int count = Samples(seconds);
            var random = new Scramble(seed);
            float filtered = 0;
            for (int i = 0; i < count; i++)
            {
                float white = random.Next() * 2 - 1;
                filtered += 0.35f * (white - filtered);
                // A hat keeps only the hiss: the noise less its smoothed self.
                float value = hiss ? white - filtered : filtered;
                float t = i / (float)SampleRate;
                buffer[(first + i) % buffer.Length] += value * volume * (float)Math.Exp(-5 * t / seconds);
            }
        }

        // Low noise that swells and falls a few times over the loop, its end crossfaded into its start so it runs on
        // without a seam.
        static float[] Wind(float seconds, float smoothing, float volume, float swell, int seed)
        {
            int length = Samples(seconds);
            int fade = Samples(1);
            var raw = new float[length + fade];
            var random = new Scramble(seed);
            float filtered = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                filtered += smoothing * (random.Next() * 2 - 1 - filtered);
                double t = i / (double)SampleRate;
                // Three swells a loop, so the swell itself wraps.
                float gain = 1 - swell * 0.5f * (1 + (float)Math.Sin(2 * Math.PI * 3 * t / seconds));
                raw[i] = filtered * gain * volume * 6;
            }
            var buffer = new float[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = i < fade ? raw[i] * i / fade + raw[length + i] * (1 - i / (float)fade) : raw[i];
            }
            return buffer;
        }

        static float[] LowPass(float[] buffer, float smoothing)
        {
            var smooth = new float[buffer.Length];
            // Twice round, so the start already carries the end's filter state.
            float filtered = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    filtered += smoothing * (buffer[i] - filtered);
                    if (pass == 1) smooth[i] = filtered;
                }
            }
            return smooth;
        }

        static float[] Normalised(float[] buffer, float peak)
        {
            float loudest = 0;
            foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest <= 0) return buffer;
            float scale = peak / loudest;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
            return buffer;
        }

        static int Samples(float seconds) => (int)Math.Round(seconds * SampleRate);

        // A fixed stream of numbers from 0 to 1, the same for the same seed.
        sealed class Scramble
        {
            uint _state;

            public Scramble(int seed) => _state = 0x9E3779B9u * (uint)(seed + 1) + 0x7F4A7C15u;

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
