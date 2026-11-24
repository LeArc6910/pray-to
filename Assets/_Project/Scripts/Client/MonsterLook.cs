using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Where a body is drawn for its size (2026-11-24, Docs/BiomeRework.md B0b): the same rule for an M slime and an XL
    // slime king, so a big body's picture stands on its feet, its health bar spans it and the screen sorts it by them.
    static class MonsterLook
    {
        // How far below its middle a body's feet are, in tiles: the bottom of its picture (the sprite's pivot is the
        // middle of its bottom edge), a little inside its round shape. The point to sort the world's standing things by.
        public static float FootOffset(float radius) => radius * 0.8f;

        // A body's feet in the world, from where it is (its middle).
        public static Vector3 Foot(Vector3 middle, float radius) => middle - new Vector3(0, FootOffset(radius), 0);

        // The width of its health bar, in tiles: an ordinary body's 0.8, or six tenths of a big one's width.
        public static float BarWidth(float diameter) => Mathf.Max(0.8f, diameter * 0.6f);

        // Its size class, for the screen and the playtests.
        public static MonsterSize SizeOf(MonsterDefinition monster) => monster?.Size ?? MonsterSize.M;
    }
}
