using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // A boss's ground (2026-11-28, World.BossGround.cs): no field monster comes into being or stays within the arena
    // and BossGroundMargin round it; what the boss calls may.
    public class BossGroundTests
    {
        static readonly Vec2 Spawn = TileMap.Center(30, 5);
        static readonly Vec2 Home = TileMap.Center(30, 40);
        const float ArenaRadius = 8;
        const float Ground = ArenaRadius + World.BossGroundMargin;

        static World Arena(WorldConfig config = null)
        {
            var world = new World(Maps.Room(60), Spawn, Monsters.Shipped, 1, config ?? new WorldConfig { EnemyTarget = 0 });
            world.PlaceBoss(Bosses.Shipped.Find("crystal_queen"), Home, ArenaRadius);
            return world;
        }

        [Test]
        public void TheGroundIsTheArenaAndAMargin()
        {
            var world = Arena();
            Assert.IsTrue(world.InBossGround(Home + new Vec2(0, -(Ground - 0.5f))));
            Assert.IsFalse(world.InBossGround(Home + new Vec2(0, -(Ground + 0.5f))));
            Assert.IsTrue(world.NoFieldSpawn(Home));
        }

        [Test]
        public void AFieldMonsterIsSetBackOutsideTheGround()
        {
            var world = Arena();
            var slime = world.SpawnEnemy(Monsters.Shipped.Find("slime"), Home + new Vec2(0, -10));
            world.Step();
            Assert.GreaterOrEqual(Vec2.Distance(slime.Position, Home), Ground, "pushed out");
        }

        [Test]
        public void WhatTheBossCallsMayStay()
        {
            var world = Arena();
            var hatchling = world.SpawnEnemy(Monsters.Shipped.Find("crystal_hatchling"), Home + new Vec2(0, -4));
            hatchling.FromBoss = true;
            world.Step();
            Assert.Less(Vec2.Distance(hatchling.Position, Home), ArenaRadius, "not pushed");
        }

        [Test]
        public void TheSteadySpawnLeavesTheGroundEmpty()
        {
            // Steady spawn round a player standing just outside the ground, so half of where it may spawn is on it.
            var config = new WorldConfig { EnemyTarget = 0, SpawnDistance = 3 };
            var world = Arena(config);
            var player = world.AddPlayer("a", null);
            for (int i = 0; i < 100 && Vec2.Distance(player.Position, Home) > Ground + 2.01f; i++)
                world.MovePlayer(player.Id, player.Position + (Home - player.Position).ClampLength(2), player.Facing, 10);
            config.EnemyTarget = 40;
            for (int i = 0; i < World.Ticks(20); i++)
            {
                world.Step();
                Assert.IsFalse(world.Enemies.Any(e => !e.FromBoss && Vec2.Distance(e.Position, Home) < Ground), "no field monster on the ground");
            }
            Assert.Greater(world.Enemies.Count, 0, "the field still fills");
        }
    }
}
