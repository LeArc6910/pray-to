using System;
using System.Collections.Generic;

namespace Game.Client
{
    // Every sound effect in the game.
    enum Sound
    {
        Swing,
        Hit,
        Crit,
        Hurt,
        Dig,
        Break,
        Pickup,
        Place,
        Craft,
        Click,
        Shoot,
        Zap,
        Boom,
        Nova,
        Buff,
        Heal,
        Pop,
        Down,
        Warning,
        Roar,
        Wake,
        Victory,
        Harvest,
        Water,
        Eat,
        // The player's own footsteps, quietly.
        Step,
        // A new character level.
        LevelUp,
        // Fishing: the line going out, a bite, a fish landed.
        Cast,
        Bite,
        Splash,
        // A combo's finisher: a heavier, lower whoosh, and the thump when it lands.
        Heavy,
        Thump,
        // A storm's thunder, rolling on.
        Thunder,
        // A dash: a rush of air rising.
        Dash,
        // A skill learned or raised: two bell notes.
        Learn,
        // A person's story step done: a short fanfare.
        Story,
        // Ice: a cold glassy shimmer, for frost skills.
        Ice,
        // A door swinging open on its hinge, and falling shut (2026-11-09).
        Door,
        DoorShut,
        // A hammer's tap on the workbench while something is being made (2026-11-09).
        Hammer,
        // A monster tramping through a crop at night: leaves crushed and a dull thud (2026-11-09).
        Trample,
        // The venom spider (2026-11-13): a long hiss as it rears; a thread shot out (a quick rising whip); its legs
        // ticking on stone as it skitters; and the wet crunch of fangs or venom landing.
        Hiss,
        Web,
        Skitter,
        Squelch,
    }

    // A monster's kind of voice (2026-11-09): what it sounds like when it notices someone, attacks and is struck.
    enum CreatureVoice
    {
        Slime,
        Mushroom,
        Bug,
        Bat,
        Wisp,
        Crab,
        Worm,
    }

    enum Voice
    {
        // Noticing a player and coming for them.
        Cry,
        // Winding up an attack.
        Attack,
        // Struck.
        Hurt,
    }

    // The sound effects, made from code the way the pixel art is: square, triangle, saw and sine tones swept in
    // pitch, and filtered noise, each shaped by a quick attack and a decay, in the manner of 8-bit consoles. No audio
    // files, so they reach the web demo directly and the preview tool (DevTools/ArtPreview) writes them out as WAV.
    // Mono, at SampleRate.
    static class SoundArt
    {
        public const int SampleRate = 22050;
        // Every sound is scaled so its loudest sample is this times its own Level, leaving room to mix.
        const float Peak = 0.7f;

        enum Wave
        {
            Square,
            Pulse,
            Triangle,
            Saw,
            Sine,
        }

        public static float[] Make(Sound sound)
        {
            var parts = new List<Part>();
            float level = 1;
            switch (sound)
            {
                case Sound.Swing:
                    parts.Add(Noise(0, 0.14f, 1, 0.22f, attack: 0.03f, decay: 3));
                    parts.Add(Tone(Wave.Sine, 0, 0.12f, 320, 160, 0.25f));
                    level = 0.55f;
                    break;
                case Sound.Hit:
                    parts.Add(Tone(Wave.Square, 0, 0.12f, 170, 55, 0.8f, decay: 5));
                    parts.Add(Noise(0, 0.05f, 0.6f, 0.5f, decay: 8));
                    level = 0.8f;
                    break;
                case Sound.Crit:
                    parts.Add(Tone(Wave.Square, 0, 0.12f, 170, 55, 0.8f, decay: 5));
                    parts.Add(Noise(0, 0.05f, 0.6f, 0.5f, decay: 8));
                    parts.Add(Tone(Wave.Pulse, 0.03f, 0.16f, 1320, 1320, 0.35f, decay: 4));
                    level = 0.9f;
                    break;
                case Sound.Hurt:
                    parts.Add(Tone(Wave.Saw, 0, 0.2f, 260, 110, 0.8f, decay: 3));
                    parts.Add(Noise(0, 0.1f, 0.5f, 0.3f, decay: 5));
                    break;
                case Sound.Dig:
                    parts.Add(Noise(0, 0.09f, 1, 0.35f, decay: 5));
                    parts.Add(Tone(Wave.Triangle, 0, 0.08f, 150, 70, 0.6f, decay: 6));
                    level = 0.6f;
                    break;
                case Sound.Break:
                    parts.Add(Noise(0, 0.12f, 1, 0.3f, decay: 4));
                    parts.Add(Noise(0.06f, 0.12f, 0.7f, 0.25f, decay: 4, seed: 2));
                    parts.Add(Noise(0.14f, 0.2f, 0.5f, 0.2f, decay: 4, seed: 3));
                    parts.Add(Tone(Wave.Triangle, 0, 0.3f, 110, 50, 0.5f, decay: 3));
                    level = 0.8f;
                    break;
                case Sound.Pickup:
                    parts.Add(Tone(Wave.Pulse, 0, 0.07f, 880, 880, 0.5f, decay: 1.5f));
                    parts.Add(Tone(Wave.Pulse, 0.07f, 0.1f, 1320, 1320, 0.5f, decay: 2));
                    level = 0.6f;
                    break;
                case Sound.Place:
                    parts.Add(Tone(Wave.Triangle, 0, 0.1f, 260, 150, 0.8f, decay: 6));
                    parts.Add(Noise(0, 0.03f, 0.4f, 0.6f, decay: 6));
                    level = 0.7f;
                    break;
                case Sound.Craft:
                    Arpeggio(parts, Wave.Pulse, 0.07f, 0.1f, 0.4f, 523, 659, 784, 1047);
                    level = 0.6f;
                    break;
                case Sound.Click:
                    parts.Add(Tone(Wave.Pulse, 0, 0.04f, 1200, 1100, 0.3f, decay: 6));
                    level = 0.4f;
                    break;
                case Sound.Hammer:
                    parts.Add(Tone(Wave.Pulse, 0, 0.05f, 1700, 1550, 0.45f, decay: 9));
                    parts.Add(Tone(Wave.Triangle, 0, 0.09f, 420, 300, 0.5f, decay: 6));
                    parts.Add(Noise(0, 0.025f, 0.3f, 0.8f, decay: 9));
                    level = 0.45f;
                    break;
                case Sound.Trample:
                    parts.Add(Noise(0, 0.2f, 0.55f, 0.35f, decay: 3, seed: 7));
                    parts.Add(Tone(Wave.Triangle, 0, 0.13f, 170, 85, 0.5f, decay: 5));
                    level = 0.55f;
                    break;
                case Sound.Hiss:
                    parts.Add(Noise(0, 0.5f, 0.8f, 0.95f, attack: 0.08f, decay: 2.2f, seed: 31));
                    parts.Add(Noise(0, 0.45f, 0.35f, 0.5f, attack: 0.05f, decay: 2.5f, seed: 32));
                    parts.Add(Tone(Wave.Saw, 0, 0.35f, 1900, 1500, 0.06f, attack: 0.05f, decay: 2));
                    level = 0.55f;
                    break;
                case Sound.Web:
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 420, 1700, 0.45f, decay: 3));
                    parts.Add(Noise(0, 0.12f, 0.45f, 0.75f, attack: 0.01f, decay: 5, seed: 33));
                    level = 0.55f;
                    break;
                case Sound.Skitter:
                    for (int i = 0; i < 6; i++) parts.Add(Noise(i * 0.034f, 0.022f, i % 2 == 0 ? 0.7f : 0.5f, 1, decay: 9, seed: 34 + i));
                    parts.Add(Tone(Wave.Pulse, 0, 0.18f, 2400, 2200, 0.05f, decay: 6));
                    level = 0.45f;
                    break;
                case Sound.Squelch:
                    parts.Add(Tone(Wave.Sine, 0, 0.2f, 260, 70, 0.75f, decay: 4));
                    parts.Add(Noise(0, 0.16f, 0.8f, 0.25f, decay: 4, seed: 41));
                    parts.Add(Noise(0.05f, 0.14f, 0.5f, 0.55f, decay: 5, seed: 42));
                    level = 0.75f;
                    break;
                case Sound.Shoot:
                    parts.Add(Tone(Wave.Triangle, 0, 0.14f, 720, 250, 0.7f, decay: 4));
                    parts.Add(Noise(0, 0.06f, 0.35f, 0.7f, decay: 6));
                    level = 0.6f;
                    break;
                case Sound.Zap:
                    parts.Add(Tone(Wave.Square, 0, 0.18f, 300, 1200, 0.45f, decay: 2, vibrato: 0.05f, vibratoRate: 40));
                    parts.Add(Tone(Wave.Saw, 0, 0.18f, 600, 2400, 0.15f, decay: 2));
                    level = 0.55f;
                    break;
                case Sound.Boom:
                    parts.Add(Noise(0, 0.45f, 1, 0.12f, attack: 0.002f, decay: 4));
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 90, 35, 0.9f, decay: 3));
                    break;
                case Sound.Nova:
                    parts.Add(Tone(Wave.Triangle, 0, 0.35f, 220, 880, 0.5f, decay: 1.5f));
                    parts.Add(Noise(0, 0.35f, 0.3f, 0.2f, attack: 0.1f, decay: 2));
                    level = 0.8f;
                    break;
                case Sound.Buff:
                    Arpeggio(parts, Wave.Triangle, 0.06f, 0.14f, 0.5f, 392, 494, 587, 784);
                    level = 0.7f;
                    break;
                case Sound.Heal:
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 520, 900, 0.5f, decay: 1, vibrato: 0.06f, vibratoRate: 18));
                    parts.Add(Tone(Wave.Sine, 0.05f, 0.35f, 780, 1350, 0.25f, decay: 1.5f));
                    level = 0.6f;
                    break;
                case Sound.Pop:
                    parts.Add(Tone(Wave.Square, 0, 0.16f, 520, 90, 0.6f, decay: 4));
                    parts.Add(Noise(0, 0.05f, 0.3f, 0.5f, decay: 6));
                    level = 0.7f;
                    break;
                case Sound.Down:
                    parts.Add(Tone(Wave.Square, 0, 0.2f, 440, 440, 0.4f, decay: 1.5f));
                    parts.Add(Tone(Wave.Square, 0.2f, 0.2f, 349, 349, 0.4f, decay: 1.5f));
                    parts.Add(Tone(Wave.Square, 0.4f, 0.35f, 262, 247, 0.4f, decay: 2));
                    level = 0.7f;
                    break;
                case Sound.Warning:
                    parts.Add(Tone(Wave.Square, 0, 0.07f, 988, 988, 0.4f, decay: 0.5f));
                    parts.Add(Tone(Wave.Square, 0.12f, 0.07f, 988, 988, 0.4f, decay: 0.5f));
                    level = 0.5f;
                    break;
                case Sound.Roar:
                    parts.Add(Tone(Wave.Saw, 0, 0.9f, 85, 55, 0.8f, attack: 0.08f, decay: 1.5f, vibrato: 0.08f, vibratoRate: 9));
                    parts.Add(Noise(0, 0.9f, 0.6f, 0.08f, attack: 0.1f, decay: 1.5f));
                    break;
                case Sound.Wake:
                    parts.Add(Tone(Wave.Square, 0, 0.4f, 110, 110, 0.5f, decay: 1));
                    parts.Add(Tone(Wave.Square, 0.4f, 0.5f, 104, 98, 0.5f, decay: 1));
                    parts.Add(Tone(Wave.Saw, 0, 0.9f, 55, 49, 0.4f, attack: 0.05f, decay: 1));
                    level = 0.9f;
                    break;
                case Sound.Victory:
                    Arpeggio(parts, Wave.Pulse, 0.1f, 0.12f, 0.4f, 523, 659, 784);
                    parts.Add(Tone(Wave.Pulse, 0.3f, 0.6f, 1047, 1047, 0.45f, decay: 1.5f, vibrato: 0.01f, vibratoRate: 6));
                    level = 0.8f;
                    break;
                case Sound.Harvest:
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 660, 660, 0.6f, decay: 4));
                    parts.Add(Tone(Wave.Triangle, 0.05f, 0.13f, 990, 990, 0.5f, decay: 4));
                    level = 0.6f;
                    break;
                case Sound.Water:
                    // A soft pour with a few bubbles rising through it.
                    parts.Add(Noise(0, 0.35f, 0.7f, 0.6f, attack: 0.04f, decay: 2.5f, seed: 7));
                    parts.Add(Tone(Wave.Sine, 0.04f, 0.06f, 700, 1100, 0.35f, decay: 6));
                    parts.Add(Tone(Wave.Sine, 0.12f, 0.06f, 850, 1300, 0.3f, decay: 6));
                    parts.Add(Tone(Wave.Sine, 0.2f, 0.06f, 760, 1200, 0.25f, decay: 6));
                    level = 0.5f;
                    break;
                case Sound.Eat:
                    // Two crunchy bites and a pleased little rise.
                    parts.Add(Noise(0, 0.05f, 0.8f, 0.35f, decay: 8, seed: 3));
                    parts.Add(Noise(0.12f, 0.05f, 0.8f, 0.35f, decay: 8, seed: 5));
                    parts.Add(Tone(Wave.Square, 0.24f, 0.14f, 440, 660, 0.3f, decay: 3));
                    level = 0.55f;
                    break;
                case Sound.LevelUp:
                    // A bright run up two octaves.
                    Arpeggio(parts, Wave.Square, 0.06f, 0.1f, 0.35f, 392, 523, 659, 784, 1047);
                    parts.Add(Tone(Wave.Triangle, 0.3f, 0.4f, 1568, 1568, 0.3f, decay: 2, vibrato: 0.01f, vibratoRate: 7));
                    level = 0.7f;
                    break;
                case Sound.Cast:
                    // A whip of the rod and a plop.
                    parts.Add(Noise(0, 0.12f, 0.7f, 0.5f, attack: 0.02f, decay: 3, seed: 11));
                    parts.Add(Tone(Wave.Sine, 0.18f, 0.08f, 900, 300, 0.5f, decay: 4));
                    level = 0.5f;
                    break;
                case Sound.Bite:
                    // Two quick bloops: something took it.
                    parts.Add(Tone(Wave.Sine, 0, 0.07f, 300, 700, 0.7f, decay: 3));
                    parts.Add(Tone(Wave.Sine, 0.1f, 0.07f, 300, 800, 0.7f, decay: 3));
                    level = 0.8f;
                    break;
                case Sound.Heavy:
                    parts.Add(Noise(0, 0.22f, 1, 0.3f, attack: 0.05f, decay: 2.5f, seed: 21));
                    parts.Add(Tone(Wave.Sine, 0, 0.2f, 220, 90, 0.4f, decay: 2.5f));
                    level = 0.7f;
                    break;
                case Sound.Thump:
                    parts.Add(Tone(Wave.Square, 0, 0.16f, 120, 40, 0.9f, decay: 4));
                    parts.Add(Tone(Wave.Sine, 0, 0.2f, 90, 45, 0.7f, decay: 3));
                    parts.Add(Noise(0, 0.08f, 0.5f, 0.6f, decay: 6, seed: 22));
                    level = 0.95f;
                    break;
                case Sound.Dash:
                    parts.Add(Noise(0, 0.28f, 0.7f, 0.35f, attack: 0.03f, decay: 2.5f, seed: 13));
                    parts.Add(Tone(Wave.Triangle, 0, 0.25f, 180, 620, 0.35f, decay: 2));
                    level = 0.6f;
                    break;
                case Sound.Learn:
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 1047, 1047, 0.45f, decay: 2.5f));
                    parts.Add(Tone(Wave.Sine, 0.12f, 0.6f, 1568, 1568, 0.35f, decay: 2.5f));
                    parts.Add(Tone(Wave.Triangle, 0.12f, 0.6f, 784, 784, 0.15f, decay: 2.5f));
                    level = 0.6f;
                    break;
                case Sound.Story:
                    Arpeggio(parts, Wave.Pulse, 0.09f, 0.11f, 0.35f, 392, 523, 659);
                    parts.Add(Tone(Wave.Pulse, 0.27f, 0.45f, 784, 784, 0.35f, decay: 1.5f, vibrato: 0.01f, vibratoRate: 6));
                    parts.Add(Tone(Wave.Triangle, 0.27f, 0.45f, 392, 392, 0.25f, decay: 1.5f));
                    level = 0.7f;
                    break;
                case Sound.Door:
                    // A short wooden creak, rising, and the latch.
                    parts.Add(Tone(Wave.Saw, 0, 0.22f, 180, 260, 0.25f, attack: 0.02f, decay: 2, vibrato: 0.08f, vibratoRate: 30));
                    parts.Add(Noise(0, 0.03f, 0.35f, 0.6f, decay: 6, seed: 41));
                    level = 0.45f;
                    break;
                case Sound.DoorShut:
                    // A soft wooden thud.
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 140, 70, 0.7f, decay: 6));
                    parts.Add(Noise(0, 0.06f, 0.5f, 0.3f, decay: 6, seed: 42));
                    level = 0.5f;
                    break;
                case Sound.Ice:
                    parts.Add(Tone(Wave.Sine, 0, 0.45f, 1800, 2400, 0.3f, decay: 2, vibrato: 0.03f, vibratoRate: 22));
                    parts.Add(Tone(Wave.Sine, 0.05f, 0.4f, 2700, 3200, 0.2f, decay: 2.5f));
                    parts.Add(Noise(0, 0.4f, 0.3f, 0.85f, attack: 0.05f, decay: 2.5f, seed: 17));
                    level = 0.5f;
                    break;
                case Sound.Thunder:
                    parts.Add(Noise(0, 1.4f, 1, 0.82f, attack: 0.02f, decay: 1.6f, seed: 31));
                    parts.Add(Noise(0.15f, 1.0f, 0.7f, 0.9f, attack: 0.1f, decay: 2.2f, seed: 32));
                    parts.Add(Tone(Wave.Sine, 0, 0.6f, 60, 35, 0.5f, decay: 2));
                    level = 0.8f;
                    break;
                case Sound.Splash:
                    parts.Add(Noise(0, 0.3f, 1, 0.35f, attack: 0.005f, decay: 4, seed: 12));
                    parts.Add(Tone(Wave.Sine, 0, 0.12f, 500, 1200, 0.4f, decay: 3));
                    level = 0.7f;
                    break;
                case Sound.Step:
                    // A soft scuff on stone.
                    parts.Add(Noise(0, 0.06f, 1, 0.18f, attack: 0.003f, decay: 6, seed: 7));
                    parts.Add(Tone(Wave.Triangle, 0, 0.05f, 120, 80, 0.4f, decay: 6));
                    level = 0.35f;
                    break;
            }
            return Mix(parts, level);
        }

        // Each skill's own sound as it is used (2026-11-09), by its id in skills.csv; null for a skill without one,
        // which sounds as its kind does (WorldView).
        public static float[] Skill(string id)
        {
            var parts = new List<Part>();
            float level;
            switch (id)
            {
                // ---- Warrior: steel and weight ----
                case "whirlwind":
                    // Two blades whooshing round and the ring of steel.
                    parts.Add(Noise(0, 0.18f, 1, 0.25f, attack: 0.05f, decay: 2.5f, seed: 41));
                    parts.Add(Noise(0.16f, 0.18f, 1, 0.3f, attack: 0.05f, decay: 2.5f, seed: 42));
                    parts.Add(Tone(Wave.Sine, 0, 0.32f, 320, 150, 0.3f, decay: 2));
                    parts.Add(Tone(Wave.Triangle, 0.05f, 0.3f, 1500, 1400, 0.12f, decay: 3));
                    level = 0.7f;
                    break;
                case "war_cry":
                    // A rough shout over a drum.
                    parts.Add(Tone(Wave.Saw, 0, 0.5f, 180, 230, 0.6f, attack: 0.04f, decay: 1.5f, vibrato: 0.05f, vibratoRate: 7));
                    parts.Add(Tone(Wave.Square, 0, 0.5f, 90, 115, 0.3f, attack: 0.04f, decay: 1.5f));
                    parts.Add(Tone(Wave.Sine, 0, 0.25f, 120, 45, 0.8f, decay: 4));
                    parts.Add(Noise(0, 0.4f, 0.4f, 0.15f, attack: 0.03f, decay: 2, seed: 43));
                    level = 0.85f;
                    break;
                case "charge":
                    // A rush of air and the stamp of landing.
                    parts.Add(Noise(0, 0.35f, 0.8f, 0.4f, attack: 0.02f, decay: 2, seed: 44));
                    parts.Add(Tone(Wave.Triangle, 0, 0.3f, 150, 500, 0.4f, decay: 2));
                    parts.Add(Tone(Wave.Sine, 0.3f, 0.2f, 110, 40, 0.8f, decay: 4));
                    parts.Add(Noise(0.3f, 0.08f, 0.6f, 0.5f, decay: 6, seed: 45));
                    level = 0.8f;
                    break;
                case "ground_slam":
                    // A deep thud, the ground rumbling and stones cracking.
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 80, 30, 1, decay: 3));
                    parts.Add(Noise(0, 0.6f, 1, 0.08f, decay: 3, seed: 50));
                    Crackle(parts, 0.05f, 0.3f, 4, 0.5f, 51);
                    level = 1;
                    break;
                case "iron_skin":
                    // A clang, and the iron ringing on.
                    parts.Add(Noise(0, 0.03f, 0.5f, 0.8f, decay: 6, seed: 46));
                    Bell(parts, 0, 0.9f, 520, 0.5f);
                    Bell(parts, 0.08f, 0.8f, 780, 0.35f);
                    level = 0.7f;
                    break;
                case "blade_storm":
                    // Five whooshes, quicker and quicker, and a long ring.
                    foreach (float at in new[] { 0f, 0.13f, 0.24f, 0.33f, 0.41f })
                        parts.Add(Noise(at, 0.16f, 1, 0.3f, attack: 0.04f, decay: 2.5f, seed: 47 + (int)(at * 100)));
                    parts.Add(Tone(Wave.Triangle, 0, 0.7f, 1800, 1600, 0.12f, decay: 2, vibrato: 0.02f, vibratoRate: 30));
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 260, 120, 0.25f, decay: 2));
                    level = 0.8f;
                    break;

                // ---- Fire mage: whooshes, hisses and crackles ----
                case "fireball":
                    parts.Add(Noise(0, 0.4f, 1, 0.3f, attack: 0.06f, decay: 2, seed: 52));
                    parts.Add(Tone(Wave.Sine, 0, 0.35f, 200, 520, 0.4f, decay: 2));
                    Crackle(parts, 0.08f, 0.3f, 3, 0.3f, 53);
                    level = 0.75f;
                    break;
                case "flame_brand":
                    // A match struck: a hiss, a warm rise and a glint.
                    parts.Add(Noise(0, 0.5f, 0.6f, 0.7f, attack: 0.1f, decay: 2.5f, seed: 54));
                    parts.Add(Tone(Wave.Triangle, 0.05f, 0.45f, 330, 660, 0.35f, decay: 2));
                    Bell(parts, 0.2f, 0.4f, 880, 0.2f);
                    level = 0.65f;
                    break;
                case "fire_ring":
                    // A roar of flame swelling out round the caster.
                    parts.Add(Noise(0, 0.7f, 1, 0.2f, attack: 0.15f, decay: 2, seed: 55));
                    parts.Add(Tone(Wave.Saw, 0, 0.6f, 110, 70, 0.3f, attack: 0.1f, decay: 2, vibrato: 0.05f, vibratoRate: 6));
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 220, 440, 0.25f, decay: 2));
                    level = 0.85f;
                    break;
                case "meteor":
                    // A whistle falling from the sky, then the crash.
                    parts.Add(Tone(Wave.Sine, 0, 0.6f, 1800, 400, 0.35f, attack: 0.1f, decay: 0.8f));
                    parts.Add(Noise(0.55f, 0.6f, 1, 0.1f, decay: 3, seed: 56));
                    parts.Add(Tone(Wave.Sine, 0.55f, 0.5f, 90, 30, 1, decay: 3));
                    Crackle(parts, 0.6f, 0.4f, 4, 0.35f, 57);
                    level = 1;
                    break;
                case "flame_burst":
                    // Three quick puffs of fire.
                    foreach (float at in new[] { 0f, 0.1f, 0.2f })
                    {
                        parts.Add(Noise(at, 0.15f, 1, 0.35f, attack: 0.02f, decay: 3, seed: 58 + (int)(at * 10)));
                        parts.Add(Tone(Wave.Sine, at, 0.12f, 300, 600, 0.25f, decay: 3));
                    }
                    level = 0.7f;
                    break;
                case "inferno":
                    // A long roar of fire, crackling, over a deep rumble.
                    parts.Add(Noise(0, 1.2f, 1, 0.15f, attack: 0.2f, decay: 1.5f, seed: 60));
                    parts.Add(Tone(Wave.Saw, 0, 1.1f, 70, 55, 0.4f, attack: 0.15f, decay: 1.5f, vibrato: 0.08f, vibratoRate: 5));
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 120, 40, 0.6f, decay: 3));
                    Crackle(parts, 0.1f, 1, 7, 0.35f, 61);
                    level = 1;
                    break;

                // ---- Archer: the bow and the air ----
                case "volley":
                    foreach (float at in new[] { 0f, 0.09f, 0.18f }) Twang(parts, at, 0.6f, 62 + (int)(at * 100));
                    level = 0.65f;
                    break;
                case "eagle_eye":
                    // A hawk's cry and a glint.
                    parts.Add(Tone(Wave.Sine, 0, 0.35f, 1800, 2600, 0.4f, attack: 0.03f, decay: 1.5f, vibrato: 0.03f, vibratoRate: 25));
                    parts.Add(Tone(Wave.Sine, 0.3f, 0.3f, 2600, 1500, 0.35f, decay: 2, vibrato: 0.03f, vibratoRate: 25));
                    Bell(parts, 0.05f, 0.5f, 1320, 0.2f);
                    level = 0.6f;
                    break;
                case "gale_step":
                    // A gust rising.
                    parts.Add(Noise(0, 0.45f, 0.8f, 0.55f, attack: 0.08f, decay: 2, seed: 63));
                    parts.Add(Tone(Wave.Sine, 0, 0.4f, 400, 1200, 0.25f, decay: 2, vibrato: 0.05f, vibratoRate: 9));
                    level = 0.6f;
                    break;
                case "power_shot":
                    // The bow creaking as it bends, then a deep twang and the arrow tearing away.
                    parts.Add(Tone(Wave.Saw, 0, 0.25f, 90, 140, 0.25f, attack: 0.05f, decay: 0.5f, vibrato: 0.2f, vibratoRate: 30));
                    parts.Add(Tone(Wave.Triangle, 0.25f, 0.25f, 500, 120, 0.9f, decay: 4));
                    parts.Add(Noise(0.25f, 0.25f, 0.6f, 0.5f, attack: 0.01f, decay: 3, seed: 64));
                    level = 0.85f;
                    break;
                case "explosive_arrow":
                    // A twang and a sizzling fuse.
                    Twang(parts, 0, 0.6f, 65);
                    parts.Add(Noise(0.05f, 0.45f, 0.4f, 0.9f, attack: 0.02f, decay: 1, seed: 66));
                    parts.Add(Tone(Wave.Square, 0.1f, 0.4f, 1200, 1300, 0.06f, decay: 1, vibrato: 0.1f, vibratoRate: 40));
                    level = 0.65f;
                    break;
                case "arrow_rain":
                    // A shot up, and arrows whistling down.
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 500, 900, 0.5f, decay: 3));
                    for (int k = 0; k < 5; k++) parts.Add(Tone(Wave.Sine, 0.15f + 0.08f * k, 0.3f, 2200 - 100 * k, 900, 0.18f, decay: 2));
                    level = 0.6f;
                    break;

                // ---- Summoner: bells, breath and voices ----
                case "spirits":
                    Arpeggio(parts, Wave.Sine, 0.07f, 0.4f, 0.3f, 784, 988, 1175, 1568);
                    parts.Add(Noise(0, 0.6f, 0.25f, 0.9f, attack: 0.2f, decay: 2, seed: 67));
                    level = 0.6f;
                    break;
                case "spirit_pact":
                    // Two bells a fifth apart and a slow pulse binding them.
                    Bell(parts, 0, 0.8f, 440, 0.4f);
                    Bell(parts, 0, 0.8f, 660, 0.3f);
                    parts.Add(Tone(Wave.Sine, 0, 0.8f, 220, 220, 0.25f, attack: 0.1f, decay: 1.5f, vibrato: 0.03f, vibratoRate: 4));
                    level = 0.6f;
                    break;
                case "life_spring":
                    // Water welling up and a harp running up.
                    foreach (float at in new[] { 0f, 0.07f, 0.15f, 0.22f }) parts.Add(Tone(Wave.Sine, at, 0.07f, 500 + at * 800, 1100 + at * 800, 0.3f, decay: 5));
                    Arpeggio(parts, Wave.Triangle, 0.08f, 0.3f, 0.3f, 523, 659, 784, 1047, 1319);
                    level = 0.6f;
                    break;
                case "spirit_horde":
                    // Many little bells scattered.
                    var notes = new[] { 1047f, 1319, 1568, 1175, 1760, 1397, 2093 };
                    for (int k = 0; k < notes.Length; k++) Bell(parts, k * 0.065f, 0.4f, notes[k], 0.25f);
                    parts.Add(Noise(0, 0.6f, 0.2f, 0.9f, attack: 0.2f, decay: 2, seed: 68));
                    level = 0.6f;
                    break;
                case "spirit_blast":
                    // A ghostly rise bursting.
                    parts.Add(Tone(Wave.Sine, 0, 0.3f, 300, 900, 0.4f, attack: 0.05f, decay: 1, vibrato: 0.08f, vibratoRate: 12));
                    parts.Add(Noise(0.22f, 0.4f, 0.8f, 0.3f, decay: 3, seed: 69));
                    parts.Add(Tone(Wave.Sine, 0.25f, 0.3f, 200, 60, 0.6f, decay: 3));
                    level = 0.85f;
                    break;
                case "guardian":
                    // A low choir and a bell: something great answering.
                    foreach (var (pitch, volume) in new[] { (220f, 0.3f), (277f, 0.25f), (330f, 0.22f) })
                        parts.Add(Tone(Wave.Sine, 0, 1, pitch, pitch, volume, attack: 0.15f, decay: 1, vibrato: 0.015f, vibratoRate: 5));
                    parts.Add(Tone(Wave.Triangle, 0, 0.6f, 110, 110, 0.3f, attack: 0.1f, decay: 1.5f));
                    Bell(parts, 0.1f, 0.8f, 880, 0.25f);
                    level = 0.75f;
                    break;

                // ---- Storm caller: lightning, ice and wind ----
                case "chain_lightning":
                    Crackle(parts, 0, 0.25f, 5, 0.9f, 70);
                    parts.Add(Tone(Wave.Square, 0, 0.3f, 2000, 300, 0.2f, decay: 2, vibrato: 0.3f, vibratoRate: 60));
                    level = 0.7f;
                    break;
                case "frost_ring":
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 1600, 2200, 0.3f, decay: 2, vibrato: 0.03f, vibratoRate: 20));
                    parts.Add(Noise(0, 0.5f, 0.4f, 0.85f, attack: 0.05f, decay: 2, seed: 71));
                    parts.Add(Noise(0.02f, 0.04f, 0.8f, 0.6f, decay: 8, seed: 72));
                    Bell(parts, 0.05f, 0.4f, 2637, 0.15f);
                    level = 0.6f;
                    break;
                case "ice_lance":
                    // A glassy whistle and a crack.
                    parts.Add(Tone(Wave.Sine, 0, 0.25f, 2400, 3000, 0.35f, decay: 1.5f));
                    parts.Add(Tone(Wave.Triangle, 0, 0.2f, 1200, 1500, 0.2f, decay: 2));
                    parts.Add(Noise(0.2f, 0.06f, 1, 0.6f, decay: 7, seed: 73));
                    Bell(parts, 0.2f, 0.3f, 3136, 0.15f);
                    level = 0.6f;
                    break;
                case "storm_mantle":
                    // Wind gathering, an electric hum and a spark.
                    parts.Add(Noise(0, 0.8f, 0.6f, 0.3f, attack: 0.2f, decay: 1.5f, seed: 74));
                    parts.Add(Tone(Wave.Saw, 0, 0.8f, 110, 110, 0.2f, attack: 0.1f, decay: 1.5f, vibrato: 0.02f, vibratoRate: 50));
                    parts.Add(Tone(Wave.Square, 0.1f, 0.15f, 800, 2400, 0.15f, decay: 3));
                    level = 0.6f;
                    break;
                case "thunderclap":
                    parts.Add(Noise(0, 0.08f, 1, 0.95f, attack: 0.001f, decay: 6, seed: 75));
                    parts.Add(Noise(0.04f, 0.9f, 0.9f, 0.06f, attack: 0.03f, decay: 2, seed: 76));
                    parts.Add(Tone(Wave.Sine, 0, 0.5f, 70, 35, 0.7f, decay: 3));
                    level = 1;
                    break;
                case "blizzard":
                    // A howling wind with ice glittering in it.
                    parts.Add(Noise(0, 1.4f, 0.8f, 0.25f, attack: 0.3f, decay: 1, seed: 77));
                    parts.Add(Tone(Wave.Sine, 0, 1.3f, 700, 900, 0.15f, attack: 0.2f, decay: 1, vibrato: 0.1f, vibratoRate: 3));
                    parts.Add(Tone(Wave.Sine, 0.1f, 1, 2400, 2800, 0.15f, decay: 2, vibrato: 0.03f, vibratoRate: 20));
                    level = 0.8f;
                    break;
                default:
                    return null;
            }
            return Mix(parts, level);
        }

        // The monster id's kind of voice: slimes blub, mushrooms puff, bugs click, bats screech, wisps chime, crabs
        // clack, worms (and the nest's demons) rumble.
        public static CreatureVoice VoiceOf(string monster) =>
            monster.Contains("slime") ? CreatureVoice.Slime
            : monster.Contains("spore") || monster.Contains("myconid") ? CreatureVoice.Mushroom
            : monster.Contains("bat") ? CreatureVoice.Bat
            : monster.Contains("wisp") ? CreatureVoice.Wisp
            : monster.Contains("crab") ? CreatureVoice.Crab
            : monster.Contains("worm") || monster.Contains("demon") ? CreatureVoice.Worm
            : monster.Contains("beetle") || monster.Contains("stalker") || monster.Contains("scorpion") ? CreatureVoice.Bug
            : CreatureVoice.Slime;

        public static float[] Creature(CreatureVoice creature, Voice voice)
        {
            var parts = new List<Part>();
            float level = 0.45f;
            switch (creature, voice)
            {
                case (CreatureVoice.Slime, Voice.Cry):
                    parts.Add(Tone(Wave.Sine, 0, 0.18f, 180, 420, 0.6f, decay: 2, vibrato: 0.1f, vibratoRate: 14));
                    break;
                case (CreatureVoice.Slime, Voice.Attack):
                    parts.Add(Tone(Wave.Sine, 0, 0.14f, 260, 120, 0.7f, decay: 3));
                    parts.Add(Noise(0.02f, 0.08f, 0.4f, 0.25f, decay: 5, seed: 81));
                    break;
                case (CreatureVoice.Slime, Voice.Hurt):
                    parts.Add(Tone(Wave.Sine, 0, 0.12f, 500, 220, 0.6f, decay: 4, vibrato: 0.15f, vibratoRate: 25));
                    break;
                case (CreatureVoice.Mushroom, Voice.Cry):
                    parts.Add(Tone(Wave.Triangle, 0, 0.2f, 220, 300, 0.5f, decay: 2));
                    parts.Add(Noise(0, 0.2f, 0.3f, 0.6f, attack: 0.05f, decay: 2, seed: 82));
                    break;
                case (CreatureVoice.Mushroom, Voice.Attack):
                    parts.Add(Noise(0, 0.22f, 0.8f, 0.5f, attack: 0.03f, decay: 3, seed: 83));
                    parts.Add(Tone(Wave.Triangle, 0, 0.1f, 300, 180, 0.4f, decay: 3));
                    break;
                case (CreatureVoice.Mushroom, Voice.Hurt):
                    parts.Add(Tone(Wave.Triangle, 0, 0.12f, 340, 200, 0.6f, decay: 3));
                    parts.Add(Noise(0, 0.08f, 0.4f, 0.6f, decay: 5, seed: 84));
                    break;
                case (CreatureVoice.Bug, Voice.Cry):
                    foreach (float at in new[] { 0f, 0.05f, 0.1f, 0.15f }) parts.Add(Noise(at, 0.025f, 0.8f, 0.9f, decay: 8, seed: 85 + (int)(at * 100)));
                    break;
                case (CreatureVoice.Bug, Voice.Attack):
                    parts.Add(Noise(0, 0.18f, 0.7f, 0.85f, attack: 0.02f, decay: 2, seed: 86));
                    parts.Add(Tone(Wave.Square, 0, 0.16f, 900, 1300, 0.12f, decay: 3, vibrato: 0.2f, vibratoRate: 60));
                    break;
                case (CreatureVoice.Bug, Voice.Hurt):
                    parts.Add(Noise(0, 0.04f, 1, 0.9f, decay: 8, seed: 87));
                    parts.Add(Tone(Wave.Square, 0.02f, 0.08f, 700, 400, 0.2f, decay: 4));
                    break;
                case (CreatureVoice.Bat, Voice.Cry):
                    parts.Add(Tone(Wave.Sine, 0, 0.12f, 2400, 3200, 0.5f, decay: 2));
                    parts.Add(Tone(Wave.Sine, 0.13f, 0.1f, 2600, 3400, 0.4f, decay: 2));
                    break;
                case (CreatureVoice.Bat, Voice.Attack):
                    parts.Add(Tone(Wave.Saw, 0, 0.16f, 1800, 900, 0.35f, decay: 2, vibrato: 0.05f, vibratoRate: 40));
                    parts.Add(Noise(0, 0.12f, 0.3f, 0.6f, decay: 3, seed: 88));
                    break;
                case (CreatureVoice.Bat, Voice.Hurt):
                    parts.Add(Tone(Wave.Sine, 0, 0.1f, 3200, 1800, 0.5f, decay: 3));
                    break;
                case (CreatureVoice.Wisp, Voice.Cry):
                    Bell(parts, 0, 0.5f, 880, 0.35f);
                    Bell(parts, 0.08f, 0.45f, 1320, 0.25f);
                    break;
                case (CreatureVoice.Wisp, Voice.Attack):
                    parts.Add(Tone(Wave.Sine, 0, 0.3f, 600, 1400, 0.4f, attack: 0.05f, decay: 1.5f, vibrato: 0.04f, vibratoRate: 9));
                    break;
                case (CreatureVoice.Wisp, Voice.Hurt):
                    Bell(parts, 0, 0.3f, 1760, 0.3f);
                    parts.Add(Noise(0, 0.1f, 0.2f, 0.9f, decay: 4, seed: 89));
                    break;
                case (CreatureVoice.Crab, Voice.Cry):
                    foreach (float at in new[] { 0f, 0.09f }) parts.Add(Noise(at, 0.03f, 1, 0.7f, decay: 8, seed: 90 + (int)(at * 100)));
                    parts.Add(Tone(Wave.Sine, 0.05f, 0.1f, 400, 700, 0.25f, decay: 3));
                    break;
                case (CreatureVoice.Crab, Voice.Attack):
                    parts.Add(Noise(0, 0.04f, 1, 0.75f, decay: 8, seed: 91));
                    parts.Add(Noise(0.06f, 0.04f, 1, 0.75f, decay: 8, seed: 92));
                    break;
                case (CreatureVoice.Crab, Voice.Hurt):
                    parts.Add(Noise(0, 0.05f, 0.9f, 0.7f, decay: 7, seed: 93));
                    parts.Add(Tone(Wave.Triangle, 0, 0.08f, 600, 300, 0.3f, decay: 4));
                    break;
                case (CreatureVoice.Worm, Voice.Cry):
                    parts.Add(Noise(0, 0.35f, 0.8f, 0.12f, attack: 0.08f, decay: 2, seed: 94));
                    parts.Add(Tone(Wave.Sine, 0, 0.3f, 90, 70, 0.5f, attack: 0.05f, decay: 2));
                    break;
                case (CreatureVoice.Worm, Voice.Attack):
                    parts.Add(Noise(0, 0.25f, 0.7f, 0.5f, attack: 0.02f, decay: 2.5f, seed: 95));
                    parts.Add(Tone(Wave.Sine, 0, 0.2f, 140, 80, 0.4f, decay: 3));
                    break;
                default:
                    // A worm struck.
                    parts.Add(Tone(Wave.Sine, 0, 0.14f, 160, 90, 0.6f, decay: 3));
                    parts.Add(Noise(0, 0.1f, 0.4f, 0.2f, decay: 4, seed: 96));
                    break;
            }
            return Mix(parts, level);
        }

        // A struck bell: the note and a fainter partial 2.76 times as high that dies sooner.
        static void Bell(List<Part> parts, float start, float seconds, float pitch, float volume)
        {
            parts.Add(Tone(Wave.Sine, start, seconds, pitch, pitch, volume, decay: 3));
            parts.Add(Tone(Wave.Sine, start, seconds * 0.6f, pitch * 2.76f, pitch * 2.76f, volume * 0.35f, decay: 4));
        }

        // A bowstring let go: a falling twang and a breath of air.
        static void Twang(List<Part> parts, float start, float volume, int seed)
        {
            parts.Add(Tone(Wave.Triangle, start, 0.12f, 720, 250, volume, decay: 4));
            parts.Add(Noise(start, 0.06f, volume * 0.5f, 0.7f, decay: 6, seed: seed));
        }

        // Short sharp bursts of noise spread over a time: crackling fire, cracking stone, lightning.
        static void Crackle(List<Part> parts, float start, float seconds, int count, float volume, int seed)
        {
            for (int k = 0; k < count; k++)
            {
                float at = start + seconds * k / count + (seed * 7 + k * 13) % 5 * 0.01f;
                parts.Add(Noise(at, 0.05f, volume * (1 - 0.1f * k), 0.75f, decay: 8, seed: seed * 10 + k));
            }
        }

        // A tone or noise burst placed in time; Render adds it into the buffer.
        sealed class Part
        {
            public float Start;
            public float Seconds;
            public Action<float[], int> Render;
        }

        static void Arpeggio(List<Part> parts, Wave wave, float step, float length, float volume, params float[] notes)
        {
            for (int i = 0; i < notes.Length; i++) parts.Add(Tone(wave, i * step, length, notes[i], notes[i], volume, decay: 3));
        }

        // A wave swept in pitch from one frequency to the other (evenly in octaves), with optional vibrato (a share
        // of the pitch, at a rate in hertz).
        static Part Tone(Wave wave, float start, float seconds, float from, float to, float volume,
            float attack = 0.004f, float decay = 3, float vibrato = 0, float vibratoRate = 0)
        {
            return new Part
            {
                Start = start,
                Seconds = seconds,
                Render = (buffer, offset) =>
                {
                    int count = Samples(seconds);
                    double phase = 0;
                    for (int i = 0; i < count && offset + i < buffer.Length; i++)
                    {
                        float t = i / (float)SampleRate;
                        float along = t / seconds;
                        double frequency = from * Math.Pow(to / from, along) * (1 + vibrato * Math.Sin(2 * Math.PI * vibratoRate * t));
                        phase += frequency / SampleRate;
                        double p = phase - Math.Floor(phase);
                        float value = wave switch
                        {
                            Wave.Square => p < 0.5 ? 1 : -1,
                            Wave.Pulse => p < 0.25 ? 1 : -1,
                            Wave.Triangle => (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p),
                            Wave.Saw => (float)(2 * p - 1),
                            _ => (float)Math.Sin(2 * Math.PI * p),
                        };
                        buffer[offset + i] += value * volume * Envelope(t, seconds, attack, decay);
                    }
                },
            };
        }

        // White noise through a one-pole low-pass filter: smoothing near 1 hisses, near 0 rumbles. Always the same
        // noise for the same seed.
        static Part Noise(float start, float seconds, float volume, float smoothing, float attack = 0.002f, float decay = 3, int seed = 1)
        {
            return new Part
            {
                Start = start,
                Seconds = seconds,
                Render = (buffer, offset) =>
                {
                    int count = Samples(seconds);
                    uint state = 0x9E3779B9u * (uint)seed + 1;
                    float filtered = 0;
                    for (int i = 0; i < count && offset + i < buffer.Length; i++)
                    {
                        state ^= state << 13;
                        state ^= state >> 17;
                        state ^= state << 5;
                        float white = (state & 0xFFFF) / 32767.5f - 1;
                        filtered += smoothing * (white - filtered);
                        float t = i / (float)SampleRate;
                        buffer[offset + i] += filtered * volume * Envelope(t, seconds, attack, decay);
                    }
                },
            };
        }

        // Rises over the attack, then dies away exponentially; the last few milliseconds fade to nothing so a sound
        // never ends with a click.
        static float Envelope(float t, float seconds, float attack, float decay)
        {
            float rise = attack > 0 ? Math.Min(1, t / attack) : 1;
            float fall = (float)Math.Exp(-decay * t / seconds);
            float tail = Math.Min(1, (seconds - t) / 0.005f);
            return rise * fall * Math.Max(0, tail);
        }

        static int Samples(float seconds) => (int)Math.Ceiling(seconds * SampleRate);

        static float[] Mix(List<Part> parts, float level)
        {
            float length = 0;
            foreach (var part in parts) length = Math.Max(length, part.Start + part.Seconds);
            var buffer = new float[Samples(length)];
            foreach (var part in parts) part.Render(buffer, Samples(part.Start));
            float loudest = 0;
            foreach (float sample in buffer) loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest > 0)
            {
                float scale = Peak * level / loudest;
                for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
            }
            return buffer;
        }
    }
}
