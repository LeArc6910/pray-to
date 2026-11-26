using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    // Playing together across PC, phones and the web (Docs/CrossPlay.md).
    public class CrossPlayTests
    {
        static WorldHost Host() =>
            new WorldHost(5, Monsters.Shipped, Gear.Data, new HostConfig { MapSize = WorldGenerator.GameSize, MapVersion = WorldGenerator.NewWorldVersion },
                new WorldConfig { EnemyTarget = 0 }, Bosses.Shipped, Classes.Shipped, Tiles.Shipped, story: Tales.Shipped);

        static WorldClient Client(GeneratedWorld made) =>
            new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped, Made = made };

        [Test]
        public void TheMapPacksSmallAndComesBackTheSame()
        {
            var world = WorldGenerator.Generate(5, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            var packed = MapPrint.Pack(world.Map, world.Biomes);
            Assert.Less(packed.Length, 1 << 20, "well under the two megabytes of tiles and biomes");
            var other = WorldGenerator.Generate(6, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            Assert.AreNotEqual(MapPrint.Of(world.Map, world.Biomes), MapPrint.Of(other.Map, other.Biomes));
            Assert.IsTrue(MapPrint.Unpack(packed, other.Map, other.Biomes));
            Assert.AreEqual(MapPrint.Of(world.Map, world.Biomes), MapPrint.Of(other.Map, other.Biomes));
            Assert.IsFalse(MapPrint.Unpack(packed.Take(packed.Length - 4).ToArray(), other.Map, other.Biomes), "a short one is refused");
            Assert.AreEqual(1, MapDataMessage.Split(new byte[10]).Count);
            Assert.AreEqual(3, MapDataMessage.Split(new byte[MapPrint.PartBytes * 2 + 1]).Count);
        }

        [Test]
        public void AJoinerWhoseMapCameOutOtherwiseTakesTheHosts()
        {
            var host = Host();
            void Deliver(WorldClient client)
            {
                foreach (var message in host.TakeOutgoing()) client.Receive(message.Data);
            }
            // One whose map came out as the host's asks for nothing.
            var same = Client(host.Generated.Copy());
            host.Receive(1, same.JoinMessage());
            host.Tick();
            Deliver(same);
            Assert.IsFalse(same.MapDiffers);
            Assert.IsNull(same.TakeMapRequest());

            // One a tile off (as a phone's float math might make it).
            var off = WorldGenerator.Generate(5, WorldGenerator.GameSize, WorldGenerator.GameSize, Tales.Shipped, Bosses.Shipped.Arenas, WorldGenerator.NewWorldVersion, Tiles.Shipped.Terrain);
            int x = off.SpawnX + 3, y = off.SpawnY + 3;
            var was = off.Map[x, y];
            off.Map[x, y] = was == TileType.Wall ? TileType.Floor : TileType.Wall;
            var joiner = Client(off);
            host.Receive(2, joiner.JoinMessage());
            host.Tick();
            Deliver(joiner);
            Assert.IsTrue(joiner.MapDiffers);
            var ask = joiner.TakeMapRequest();
            Assert.IsNotNull(ask);
            Assert.IsNull(joiner.TakeMapRequest(), "once");
            host.Receive(2, ask);
            Deliver(joiner);
            Assert.IsTrue(joiner.MapFromHost);
            Assert.AreEqual(was, joiner.Map[x, y]);
            Assert.AreEqual(MapPrint.Of(host.World.Map, host.Generated.Biomes), MapPrint.Of(joiner.Map, joiner.Biomes));
            Assert.AreEqual(1, host.MapsSent);
        }

        [Test]
        public void AnotherVersionIsToldWhoUpdates()
        {
            Assert.AreEqual(-1, Protocol.CompareVersions("0.99.0", "0.110.0"));
            Assert.AreEqual(1, Protocol.CompareVersions("0.112.1", "0.112.0"));
            Assert.AreEqual(0, Protocol.CompareVersions(Protocol.Version, Protocol.Version));
            StringAssert.Contains("내 게임을 업데이트하세요", Protocol.VersionRefusal("0.1.0"));
            StringAssert.Contains("호스트가 게임을 업데이트", Protocol.VersionRefusal("9.0.0"));
            var host = Host();
            var old = new WorldClient("key", "a", Monsters.Shipped, Bosses.Shipped, Gear.Data, Classes.Shipped, null, Tiles.Shipped) { Story = Tales.Shipped };
            host.Receive(1, new JoinMessage("key", "a", version: "0.1.0").Encode());
            foreach (var message in host.TakeOutgoing()) old.Receive(message.Data);
            Assert.AreEqual(ClientStatus.Refused, old.Status);
            StringAssert.Contains("업데이트", old.RefusedReason);
        }
    }
}
