using Game.Core;

namespace Game.Client
{
    // How a weapon kind's blows feel on the screen (2026-11-28, the person: "every weapon its own feel; a greatsword or
    // a hammer is heavy, so the shake and the hit stop a little bigger"; Docs/Feel.md 무기 종류마다). Multipliers of
    // the sword's (1 throughout), the feel pieces' own switches and the menu's screen shake still have the last word.
    // Others' blows use the weapon their snapshot shows, so everyone's look the same on every screen.
    readonly struct WeaponFeel
    {
        // The screen's jolt on one's own ordinary blow that lands (the sword's ordinary blows do not shake), and the
        // jolt of a finisher or crit as a share of the sword's.
        public readonly float Shake, Heavy;
        // The hit stop's length, the striker's lunge (below 0 thrown back: a crossbow's kick), the struck one's
        // knock back, the size of the blade's arc and how thin it is (a spear's thrust is a narrow streak).
        public readonly float Stop, Lunge, Knock, Arc, Thin;
        // Every blow's star as big as a crit's.
        public readonly bool BigStar;
        // The jolt as one's own shot is let go (a crossbow's kick) and where an orb's shot bursts.
        public readonly float Kick, Burst;
        // The swing's sound quieter (light weapons) or the heavy whoosh on every step (heavy ones).
        public readonly float Volume;
        public readonly bool HeavySound;

        WeaponFeel(float shake = 0, float heavy = 1, float stop = 1, float lunge = 1, float knock = 1, float arc = 1, float thin = 1,
            bool bigStar = false, float kick = 0, float burst = 0, float volume = 1, bool heavySound = false)
        {
            Shake = shake;
            Heavy = heavy;
            Stop = stop;
            Lunge = lunge;
            Knock = knock;
            Arc = arc;
            Thin = thin;
            BigStar = bigStar;
            Kick = kick;
            Burst = burst;
            Volume = volume;
            HeavySound = heavySound;
        }

        // Built through the constructor with an argument: `new WeaponFeel()` of a struct is its zeroed default, which
        // made the sword's (and the bow's, staff's, pickaxe's, bare hands' and monsters') arc, swing and hit sounds,
        // lunge, knock back and hit stop all nothing (2026-11-28, the owner: the void sword neither swished nor showed
        // its cut; the weapon audit, ArtPreview --only weaponaudit, now looks at every weapon's).
        public static readonly WeaponFeel Sword = new WeaponFeel(volume: 1);

        // Whether every multiplier that must not be nothing is something (the weapon audit).
        public bool Felt => Arc > 0 && Thin > 0 && Volume > 0 && Stop > 0 && Knock > 0 && Heavy > 0 && Lunge != 0;

        // The sound of a basic attack let go: a bow's shot, a magic bolt's zap, a heavy whoosh on a finisher or a heavy
        // weapon's every step, else the swing (WorldView's Attack event).
        public Sound SwingSound(BasicAttackKind kind, bool finisher) =>
            kind == BasicAttackKind.Arrow ? Sound.Shoot : kind == BasicAttackKind.Bolt ? Sound.Zap
            : finisher || HeavySound ? Sound.Heavy : Sound.Swing;

        // The sound of a blow landing on someone else (WorldView's Hit event): a heavy one's thump, a crit, or the hit.
        public Sound HitSound(bool heavy, bool critical) => heavy || HeavySound && !critical ? Sound.Thump : critical ? Sound.Crit : Sound.Hit;

        public static WeaponFeel Of(HeroStyle style) => style switch
        {
            HeroStyle.Dagger => new WeaponFeel(heavy: 0.5f, stop: 0.5f, lunge: 0.8f, knock: 0.6f, arc: 0.75f, volume: 0.7f),
            HeroStyle.Greatsword => new WeaponFeel(shake: 0.05f, heavy: 1.6f, stop: 1.6f, lunge: 1.3f, knock: 1.5f, arc: 1.35f, bigStar: true, heavySound: true),
            HeroStyle.Spear => new WeaponFeel(lunge: 1.5f, arc: 1.3f, thin: 0.35f),
            HeroStyle.Hammer => new WeaponFeel(shake: 0.07f, heavy: 2, stop: 1.8f, lunge: 1.2f, knock: 1.4f, arc: 1.1f, bigStar: true, heavySound: true),
            HeroStyle.Crossbow => new WeaponFeel(heavy: 1.2f, stop: 1.3f, lunge: -1.2f, knock: 1.3f, kick: 0.04f),
            HeroStyle.Boomerang => new WeaponFeel(stop: 0.8f, lunge: 0.8f, knock: 0.8f),
            HeroStyle.Wand => new WeaponFeel(heavy: 0.5f, stop: 0.4f, lunge: 0.4f, knock: 0.5f, volume: 0.6f),
            HeroStyle.Orb => new WeaponFeel(lunge: 0.7f, burst: 0.05f),
            _ => Sword,
        };
    }
}
