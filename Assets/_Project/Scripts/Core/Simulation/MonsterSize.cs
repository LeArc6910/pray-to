using System;

namespace Game.Core
{
    // How big a monster's body is (monsters.csv size, 2026-11-24, Docs/BiomeRework.md 4-2 크기 등급 and B0b): the
    // swarms and fliers (XS, S), the ordinary body (M, every kind before the biome rework), the big beasts of the field
    // packs (L, two tiles across) and the mid-bosses and dungeon masters (XL, three across).
    public enum MonsterSize : byte
    {
        XS,
        S,
        M,
        L,
        XL,
    }

    // What each size class is, and the rules that go with it. The sheet's own numbers (health, speed, windup and the
    // attacks' telegraphs) are the monster's; the sheet reader checks that they fall in the class's band (MonsterSheet).
    // Only the knockback a blow gives is the class's alone (World.Knock), as the sheet has no column for it.
    public static class MonsterSizes
    {
        public readonly struct Band
        {
            public Band(float minRadius, float maxRadius, float health, float speed, float telegraph, float knockback, int fewest, int most, int minPixels,
                int maxPixels, float maxSpeed, float leastWindup)
            {
                MinRadius = minRadius;
                MaxRadius = maxRadius;
                Health = health;
                Speed = speed;
                Telegraph = telegraph;
                Knockback = knockback;
                Fewest = fewest;
                Most = most;
                MinPixels = minPixels;
                MaxPixels = maxPixels;
                MaxSpeed = maxSpeed;
                LeastWindup = leastWindup;
            }

            // The body's radius in tiles, both ends allowed.
            public float MinRadius { get; }
            public float MaxRadius { get; }
            // Against an M body of the same make: its health, its pace and how long its wind-ups show. A row with a base
            // (monsters.csv base) is held to these within MonsterSheet's slack.
            public float Health { get; }
            public float Speed { get; }
            public float Telegraph { get; }
            // How far a blow throws it, against an M body (0: not at all).
            public float Knockback { get; }
            // How many of it a pack holds (packs.csv members).
            public int Fewest { get; }
            public int Most { get; }
            // How wide its picture is drawn (the screen's art, CreatureArt).
            public int MinPixels { get; }
            public int MaxPixels { get; }
            // A big body is slow and warns long: the fastest it may walk and the shortest wind-up of its blows (its own
            // and every attack of monster_attacks.csv). No limit for XS to M.
            public float MaxSpeed { get; }
            public float LeastWindup { get; }
        }

        static readonly Band[] Bands =
        {
            new Band(0.20f, 0.25f, 0.4f, 1.4f, 0.7f, 2.0f, 4, 8, 8, 10, float.PositiveInfinity, 0),
            new Band(0.28f, 0.35f, 0.7f, 1.15f, 0.9f, 1.4f, 3, 5, 12, 14, float.PositiveInfinity, 0),
            new Band(0.38f, 0.45f, 1f, 1f, 1f, 1f, 2, 4, 14, 18, float.PositiveInfinity, 0),
            new Band(0.70f, 0.90f, 4f, 0.7f, 1.4f, 0.3f, 1, 2, 24, 32, 2.2f, 0.7f),
            new Band(1.20f, 1.40f, 12f, 0.5f, 1.8f, 0f, 1, 1, 40, 56, 1.6f, 0.9f),
        };

        public static Band Of(MonsterSize size) => Bands[(int)size];

        // The class a radius falls in, or null in a gap between them (a row with no size column then is an error).
        public static MonsterSize? FromRadius(float radius)
        {
            for (int i = 0; i < Bands.Length; i++)
                if (radius >= Bands[i].MinRadius - 1e-4f && radius <= Bands[i].MaxRadius + 1e-4f) return (MonsterSize)i;
            return null;
        }

        public static bool Fits(MonsterSize size, float radius) =>
            radius >= Of(size).MinRadius - 1e-4f && radius <= Of(size).MaxRadius + 1e-4f;

        // L and XL: they need room to walk (Clearance), come only in rooms and keep to them (World.Packs.cs).
        public static bool IsBig(MonsterSize size) => size >= MonsterSize.L;

        // A body this wide or narrower passes a one-tile gap, and the way-finding does not look at its size.
        public const float SlimRadius = 0.45f;

        // A big body passes where the way is its diameter and 0.4 more across (Docs/BiomeRework.md 4-2 큰 몸이 들어가는
        // 법): the clearance it needs at a tile is its radius and this.
        public const float FitMargin = 0.2f;

        public static float ClearanceFor(float radius) => radius <= SlimRadius ? 0 : radius + FitMargin;

        // How much further than an M body's its blows reach: the part of the body past the widest M (SlimRadius). The
        // attacks of monster_attacks.csv that start at its middle (a strike, a slam, a nova, a thrust) are measured from
        // its edge this way, so a big body's bite still reaches past its own belly; 0 for XS to M, whose numbers stand.
        public static float Beyond(float radius) => Math.Max(0, radius - SlimRadius);

        // An L body comes only in a room at least this wide (its radius in tiles) and never more than this far past its
        // edge (Docs/BiomeRework.md 4-2: 방 둘레 + 3칸).
        public const int LeastRoomRadius = 5;
        public const float RoomLeash = 3;
        // An XL body only where a square of open floor this wide is (7×7).
        public const int XlOpenSquare = 7;
        // How far from a big body the rest of its pack stand, past both their edges.
        public const float PackGap = 0.6f;

        public static string Name(MonsterSize size) => size switch
        {
            MonsterSize.XS => "xs",
            MonsterSize.S => "s",
            MonsterSize.M => "m",
            MonsterSize.L => "l",
            _ => "xl",
        };
    }
}
