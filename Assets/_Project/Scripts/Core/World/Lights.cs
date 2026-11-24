namespace Game.Core
{
    // What gives light in the eyes of the monsters that fear it (2026-11-24, Docs/BiomeRework.md B1b, 4-1 흙 동굴: "등잔
    // 빛을 쥐가 싫어한다"). Core has no light of its own (the screen's LightMap draws it from tiles.csv light); this is
    // the one list of the tiles a fearlight monster keeps away from (World.FearsLight). A new light-giving tile (a lit
    // lantern prop) goes here and nowhere else.
    public static class Lights
    {
        // Placed by players (a torch, a spore lamp) or laid by the generator round the places and camps (a campfire, a
        // brazier, a lantern, a lamp post).
        static readonly TileType[] Givers =
        {
            TileType.Torch,
            TileType.Lamp,
            TileType.Campfire,
            TileType.Brazier,
            TileType.Lantern,
            TileType.LampPost,
        };

        static readonly bool[] Giving = Make();

        static bool[] Make()
        {
            var giving = new bool[256];
            foreach (var type in Givers) giving[(int)type] = true;
            return giving;
        }

        public static bool Gives(TileType type) => Giving[(int)type];

        // How far a fearlight monster keeps from one (monsters.csv traits fearlight, or fearlight:tiles for its own).
        public const float FearReach = 6;

        // The most a fearlight trait may ask (MonsterAttackSheet.Traits): what World.FearsLight looks round.
        public const int MostReach = 12;

        // Every tile type that gives light, for the tests and the docs.
        public static TileType[] All => (TileType[])Givers.Clone();
    }
}
