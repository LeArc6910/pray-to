using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // A boss's warnings on the floor and the blow that lands in their shape (E1 of Docs/EffectsRework.md). The
    // pictures are HazardArt's, drawn on the pixel grid at the size and angle the hazard is judged at; a warning
    // keeps one texture and draws it again only when its filling takes a step. The blow is that same shape burning,
    // then sparks with tails, bits of the floor it struck, dust under the other warnings, a scar on the floor and a
    // short light in the light map. Bosses with a blast of their own (the *Landed of each) do not come here.
    sealed partial class WorldView
    {
        // A picture of a hazard's shape that changes: its own texture, written from HazardArt's buffer.
        sealed class ShapeLayer
        {
            public SpriteRenderer Renderer;
            public HazardField Field;
            public Texture2D Texture;
            public Sprite Sprite;
            public byte[] Buffer;
            public int Shown = -1;
        }

        sealed class Blast
        {
            public ShapeLayer Layer;
            public Rgba[] Ramp;
            public float Age;
            public int Seed;
            public int Frame = -1;
            // Under 1 for an ordinary monster's (MonsterFx.BlowStrength).
            public float Strength = 1;
        }

        sealed class Scar
        {
            public ShapeLayer Layer;
            public float Age;
            public float Life;
        }

        readonly List<Blast> _blasts = new List<Blast>();
        readonly List<Scar> _scars = new List<Scar>();
        readonly List<(Vec2 At, float Radius, Rgba Color, float Born)> _blastLights = new List<(Vec2, float, Rgba, float)>();

        // What the rework allows (Docs/EffectsRework.md 3절): pieces in one effect, scars on screen, lights an effect.
        const int BlastMostPieces = 72;
        const int MostScars = 24;
        const int BlastMostLights = 3;
        const float BlastLightSeconds = 0.35f;
        // Under the warnings (DarkOrder + 10), so dust never hides one; over the dark, as every effect is.
        const int BlastDustOrder = DarkOrder + 8;
        // On the floor: over a pool, under every shadow and body.
        const int ScarOrder = 35;

        // How many pieces the last blow laid (the playtest checks it against the limit).
        public int LastBlastPieces { get; private set; }
        public int ScarCount => _scars.Count;
        public int WarnCount { get; private set; }

        // Warnings laid for a picture (the playtest's 'warn'): held at a moment of their filling until they are landed.
        readonly List<HazardView> _stagedMarks = new List<HazardView>();
        const int StagedIds = 0x7F000000;
        int _stagedSoFar;

        public void StageWarning(HazardShape shape, Vec2 at, Vec2 direction, float size, float width, bool harmful, float progress) =>
            _stagedMarks.Add(new HazardView(new HazardState(StagedIds + _stagedSoFar++, shape, at, direction, size, width, 0, 1, harmful), progress));

        // Lands the first so many of the staged warnings (their blow is drawn), or only takes them away.
        public void EndStagedWarnings(bool land, int count = int.MaxValue)
        {
            count = Mathf.Min(count, _stagedMarks.Count);
            if (land) for (int i = 0; i < count; i++) if (_stagedMarks[i].State.Harmful) BossBlow(_stagedMarks[i].State);
            for (int i = 0; i < count; i++) _stagedAs.Remove(_stagedMarks[i].State.Id);
            _stagedMarks.RemoveRange(0, count);
        }

        static HazardField FieldOf(HazardState hazard) => hazard.Shape switch
        {
            HazardShape.Ring => HazardArt.Field(HazardForm.Donut, hazard.Size, hazard.Width, hazard.Direction.Angle),
            HazardShape.Cone => HazardArt.Field(HazardForm.Cone, hazard.Size, hazard.Width, hazard.Direction.Angle),
            HazardShape.Line => HazardArt.Field(HazardForm.Band, hazard.Size, hazard.Width, hazard.Direction.Angle),
            _ => HazardArt.Field(HazardForm.Circle, hazard.Size, 0, 0),
        };

        static WarnTone ToneOf(HazardState hazard) =>
            hazard.Shape == HazardShape.Share ? WarnTone.Gather : hazard.Harmful ? WarnTone.Danger : WarnTone.Rush;

        ShapeLayer MakeLayer(Transform parent, string name, HazardField field, int order)
        {
            var texture = new Texture2D(field.Width, field.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pivot = new Vector2(field.OriginX / field.Width, 1 - field.OriginY / field.Height);
            var sprite = Sprite.Create(texture, new Rect(0, 0, field.Width, field.Height), pivot, Sprites.PixelsPerUnit / (float)field.Scale, 0, SpriteMeshType.FullRect);
            var layer = new ShapeLayer { Field = field, Texture = texture, Sprite = sprite, Buffer = field.Buffer(), Renderer = Renderer(parent, name, sprite, Color.white) };
            layer.Renderer.sortingOrder = order;
            // Clear until its first picture is written.
            Upload(layer);
            return layer;
        }

        static void Upload(ShapeLayer layer)
        {
            layer.Texture.LoadRawTextureData(layer.Buffer);
            layer.Texture.Apply(false);
        }

        static void Free(ShapeLayer layer)
        {
            if (layer == null) return;
            if (layer.Renderer != null) Object.Destroy(layer.Renderer.gameObject);
            Object.Destroy(layer.Sprite);
            Object.Destroy(layer.Texture);
        }

        // The textures of warnings, blows and scars are this view's own: they go with it.
        void FreeHazardPictures()
        {
            foreach (var mark in _marks.Values) Free(mark.Warn);
            foreach (var blast in _blasts) Free(blast.Layer);
            foreach (var scar in _scars) Free(scar.Layer);
            _blasts.Clear();
            _scars.Clear();
        }

        // A boss's warning: the shape itself, where it is judged.
        Mark MakeWarn(HazardState hazard)
        {
            var root = new GameObject("Telegraph").transform;
            root.SetParent(_root.transform, false);
            root.localPosition = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var layer = MakeLayer(root, "Warn", FieldOf(hazard), DarkOrder + 10);
            return new Mark { Root = root, Edge = layer.Renderer, Fill = layer.Renderer, State = hazard, Warn = layer };
        }

        void DrawWarn(Mark mark, HazardState hazard, float progress)
        {
            var layer = mark.Warn;
            // An attack only the boss's wind-up announces lays nothing to see: its mark is kept (for its blast when it
            // lands) but not shown (AttackWarning.Hidden, 2026-11-12).
            layer.Renderer.enabled = hazard.Warning != AttackWarning.Hidden;
            if (!layer.Renderer.enabled) return;
            WarnCount++;
            int step = Mathf.Clamp(Mathf.FloorToInt(progress * HazardArt.FillSteps), 0, HazardArt.FillSteps);
            // The last of its time it blinks whole (not with motion reduced: it stays at its brighter half).
            bool blink = progress > HazardArt.BlinkFrom && (Settings.ReduceMotion || (int)(Time.time * 25) % 2 == 0);
            // A shared circle's edge pulses, so it reads as a place to gather rather than to flee.
            bool lit = hazard.Shape == HazardShape.Share && Mathf.Sin(Time.time * 8) > 0;
            int shown = step | (blink ? 256 : 0) | (lit ? 512 : 0);
            if (shown == layer.Shown) return;
            layer.Shown = shown;
            HazardArt.Warn(layer.Field, ToneOf(hazard), step / (float)HazardArt.FillSteps, blink, lit, layer.Buffer, true);
            Upload(layer);
        }

        FloorStuff StuffAt(Vec2 at)
        {
            var map = _client.Map;
            int x = Mathf.FloorToInt(at.X), y = Mathf.FloorToInt(at.Y);
            if (map == null || _client.Biomes == null || !map.InBounds(x, y)) return FloorStuff.Stone;
            return _client.Biomes[x, y] switch
            {
                Biome.Ice => FloorStuff.Ice,
                Biome.Crystal => FloorStuff.Crystal,
                Biome.Lava => FloorStuff.Ember,
                Biome.Sand => FloorStuff.Sand,
                Biome.Mushroom => FloorStuff.Spore,
                Biome.Abyss => FloorStuff.Void,
                _ => FloorStuff.Stone,
            };
        }

        // A boss's blow: its own effect where it has one (each *Landed lays the shared landing under it), else the
        // shared landing alone.
        void BossBlow(HazardState hazard)
        {
            if (VeinEaterLanded(hazard) || KnightLanded(hazard) || SpiderLanded(hazard) || WardenLanded(hazard) || AntlionLanded(hazard) || SerpentLanded(hazard) || CrystalQueenLanded(hazard) || GeneralLanded(hazard)) return;
            if (ElementLanded(hazard)) return;
            BlastLanded(hazard);
        }

        // What a boss with no effects of its own is made of (the table the shots use, ShotArt.ElementOf), as an
        // element's colours; the crown wraith's bolts are lightning.
        static FxRamp? ElementOf(string boss, string attack) => ShotArt.ElementOf(boss) switch
        {
            ShotElement.Lava => FxRamp.Fire,
            ShotElement.Void => FxRamp.Violet,
            ShotElement.Water => FxRamp.Cold,
            ShotElement.Wraith => attack == "lightning_fall" || attack == "storm_brand" ? FxRamp.Bolt : FxRamp.Spirit,
            _ => (FxRamp?)null,
        };

        // The lava titan's, the void lord's, the tide serpent's and the crown wraith's blows: the shared landing in
        // their element's colours and, over it, what the element does (fire bursts and burns on, the void draws in,
        // water is thrown up, souls scatter). False for anyone else's.
        bool ElementLanded(HazardState hazard)
        {
            var (boss, attack) = BlowOf(hazard);
            if (boss == null && hazard.Id < StagedIds) boss = _bossInView?.Id;
            var element = boss == null ? null : ElementOf(boss, attack);
            if (!element.HasValue) return false;
            var field = FieldOf(hazard);
            // A place to gather burns gold as it warned; fire and lightning and the void bring their own light.
            bool own = element == FxRamp.Fire || element == FxRamp.Bolt;
            BlastLanded(hazard, hazard.Shape == HazardShape.Share ? (FxRamp?)null : element, sparks: !own && element != FxRamp.Cold, chunks: element != FxRamp.Cold && element != FxRamp.Spirit,
                light: !(own && field.Form == HazardForm.Circle));
            AddBossFx(BossFx.Element, new Vector3(hazard.Position.X, hazard.Position.Y, 0), new BossContext
            {
                Ramp = element.Value, Form = field.Form, Dir = hazard.Direction.Angle * Mathf.Rad2Deg, Size = hazard.Size * Ppu,
                Inner = field.Form == HazardForm.Donut ? hazard.Width * Ppu : 0,
                Width = field.Form == HazardForm.Cone ? hazard.Width : hazard.Width * Ppu,
            });
            return true;
        }

        // The blow lands in the warning's own shape: a white flash, the shape burning the way it reads, a shockwave,
        // sparks with tails, bits of the floor, dust, a scar and a light. A boss with an element of its own burns the
        // shape in that element's colours (E6) and may leave the sparks, the bits, the light or the wave to its own
        // effect laid over this.
        void BlastLanded(HazardState hazard, FxRamp? element = null, bool sparks = true, bool chunks = true, bool light = true, bool wave = true)
        {
            var field = FieldOf(hazard);
            var tone = ToneOf(hazard);
            var ramp = element.HasValue ? SkillFxArt.Ramp5(element.Value) : HazardArt.Tone(tone);
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            var stuff = StuffAt(hazard.Position);
            int pieces = 0;

            var layer = MakeLayer(_root.transform, "Blast", field, 19000);
            layer.Renderer.transform.localPosition = at;
            _blasts.Add(new Blast { Layer = layer, Ramp = ramp, Seed = hazard.Id });
            UpdateBlast(_blasts[_blasts.Count - 1], 0);
            pieces++;

            // The floor keeps it a while: 0.9 to 1.6 seconds, the oldest going first when there are too many.
            while (_scars.Count >= MostScars)
            {
                Free(_scars[0].Layer);
                _scars.RemoveAt(0);
            }
            var scar = MakeLayer(_root.transform, "Scar", field, ScarOrder);
            scar.Renderer.transform.localPosition = at;
            HazardArt.Scar(field, stuff, hazard.Id, scar.Buffer, true);
            Upload(scar);
            _scars.Add(new Scar { Layer = scar, Life = 0.9f + 0.7f * PixelNoise.Value(hazard.Id, 3, 51) });
            pieces++;

            var bright = Tint(ramp[0], 0.95f);
            if (wave && (field.Form == HazardForm.Circle || field.Form == HazardForm.Donut))
            {
                Shockwave(at, hazard.Size * 1.15f, bright, bold: hazard.Size >= 2.5f);
                pieces++;
                if (hazard.Shape == HazardShape.Share)
                {
                    Shockwave(at, hazard.Size * 0.7f, Tint(ramp[1], 0.9f));
                    pieces++;
                }
            }

            // A short light where it fell (the light map's, no blending of its own): along a line, three of them.
            int lights = !light ? 0 : field.Form == HazardForm.Band ? Mathf.Min(BlastMostLights, Mathf.Max(1, Mathf.RoundToInt(hazard.Size / 3))) : 1;
            for (int i = 0; i < lights; i++)
            {
                var where = field.Form == HazardForm.Band ? hazard.Position + hazard.Direction * (hazard.Size * (i + 0.5f) / lights)
                    : field.Form == HazardForm.Cone ? hazard.Position + hazard.Direction * (hazard.Size * 0.55f) : hazard.Position;
                float radius = field.Form == HazardForm.Band ? Mathf.Max(2.2f, hazard.Width * 1.6f) : Mathf.Max(2.2f, hazard.Size * 1.5f);
                _blastLights.Add((where, Mathf.Min(9, radius), ramp[2], Time.time));
            }

            if (Feel.Bits)
            {
                // More for a bigger blow, within the limit.
                float girth = field.Form == HazardForm.Band ? hazard.Size / 2 : field.Form == HazardForm.Cone ? hazard.Size * 0.6f : hazard.Size;
                int sparkCount = sparks ? Mathf.Clamp(Mathf.RoundToInt(5 + girth * 2.5f), 6, 14) : 0;
                int chunkCount = chunks ? Mathf.Clamp(Mathf.RoundToInt(3 + girth * 1.5f), 4, 9) : 0;
                int puffs = chunks && HazardArt.Dusty(stuff) ? Mathf.Clamp(Mathf.RoundToInt(2 + girth), 3, 6) : 0;
                var sparkSprite = Sprites.Centered(element.HasValue ? $"fx/hazard/spark/e{(int)element.Value}" : $"fx/hazard/spark/{(int)tone}", () => HazardArt.Spark(ramp[0], ramp[2]));
                for (int i = 0; i < sparkCount && pieces < BlastMostPieces; i++, pieces++)
                {
                    var (from, way) = BlastEdge(hazard, (i + Random.value * 0.6f) / sparkCount, i);
                    var renderer = Renderer(_root.transform, "Spark", sparkSprite, Color.white);
                    renderer.transform.localPosition = from;
                    renderer.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg);
                    renderer.sortingOrder = 19001;
                    // They slow as they go.
                    _bits.Add(new Bit { Renderer = renderer, Velocity = way * Random.Range(4.5f, 8f), Color = Color.white, Life = Random.Range(0.36f, 0.56f), Drag = 5, LateFade = true });
                }
                for (int i = 0; i < chunkCount && pieces < BlastMostPieces; i++, pieces++)
                {
                    var (from, way) = BlastEdge(hazard, (i + 0.5f) / chunkCount, i + 1);
                    int variant = (hazard.Id + i) % HazardArt.Chunks;
                    var renderer = Renderer(_root.transform, "Chunk", Sprites.Centered($"fx/hazard/chunk/{(int)stuff}/{variant}", () => HazardArt.Chunk(stuff, variant)), Color.white);
                    // From inside the shape, thrown up and out, falling back.
                    renderer.transform.localPosition = Vector3.Lerp(at, from, 0.6f);
                    renderer.sortingOrder = 19000;
                    _bits.Add(new Bit
                    {
                        Renderer = renderer, Velocity = way * Random.Range(1.2f, 2.6f) + Vector3.up * Random.Range(3f, 5f), Fall = new Vector3(0, -14, 0), Drag = 0.6f,
                        Color = Color.white, Life = Random.Range(0.5f, 0.75f), LateFade = true,
                    });
                }
                for (int i = 0; i < puffs && pieces < BlastMostPieces; i++, pieces++)
                {
                    var (from, way) = BlastEdge(hazard, (i + 0.5f) / puffs, i + 2);
                    int size = i % 2 == 0 ? 7 : 9;
                    var color = Tint(HazardArt.Stuff(stuff)[1], 0.7f);
                    var renderer = Renderer(_root.transform, "Dust", Sprites.Centered($"fx/hazard/puff/{size}", () => HazardArt.Puff(size)), color);
                    renderer.transform.localPosition = from;
                    renderer.sortingOrder = BlastDustOrder;
                    _bits.Add(new Bit { Renderer = renderer, Velocity = way * Random.Range(1.2f, 2.2f) + Vector3.up * 0.5f, Color = color, Life = Random.Range(0.5f, 0.7f), Drag = 3 });
                }
            }
            LastBlastPieces = pieces;
        }

        static Color Tint(Rgba c, float alpha) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, alpha);

        // A point on the shape's edge (t from 0 to 1 round or along it) and the way out of the shape from there.
        static (Vector3 From, Vector3 Way) BlastEdge(HazardState hazard, float t, int n)
        {
            var at = new Vector3(hazard.Position.X, hazard.Position.Y, 0);
            float facing = hazard.Direction.Angle;
            switch (hazard.Shape)
            {
                case HazardShape.Ring:
                {
                    // Out of the band both ways: inward from its inner edge, outward from its outer.
                    float angle = t * Mathf.PI * 2;
                    var out_ = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    return n % 2 == 0 ? (at + out_ * hazard.Size, out_) : (at + out_ * hazard.Width, -out_);
                }
                case HazardShape.Cone:
                {
                    float angle = facing + (t - 0.5f) * Mathf.Min(360, hazard.Width) * Mathf.Deg2Rad;
                    var out_ = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    return (at + out_ * (hazard.Size * 0.9f), out_);
                }
                case HazardShape.Line:
                {
                    var along = new Vector3(hazard.Direction.X, hazard.Direction.Y, 0);
                    var side = new Vector3(-along.y, along.x, 0) * (n % 2 == 0 ? 1 : -1);
                    return (at + along * (hazard.Size * t) + side * (hazard.Width * 0.5f), (side + along * 0.35f).normalized);
                }
                default:
                {
                    float angle = t * Mathf.PI * 2;
                    var out_ = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    return (at + out_ * (hazard.Size * 0.75f), out_);
                }
            }
        }

        void UpdateBlast(Blast blast, float deltaTime)
        {
            blast.Age += deltaTime;
            // Held at an age for a picture of one phase, as a skill's effect is (the playtest's 'fxhold').
            if (Feel.SkillFxHold >= 0) blast.Age = Mathf.Min(blast.Age, Feel.SkillFxHold);
            // A picture every thirtieth of a second.
            int frame = Mathf.FloorToInt(blast.Age * 30);
            if (frame == blast.Frame) return;
            blast.Frame = frame;
            if (!HazardArt.Blast(blast.Layer.Field, blast.Ramp, blast.Age, blast.Seed, blast.Layer.Buffer, true, blast.Strength)) blast.Age = float.MaxValue;
            else Upload(blast.Layer);
        }

        void UpdateBlasts(float deltaTime)
        {
            for (int i = _blasts.Count - 1; i >= 0; i--)
            {
                UpdateBlast(_blasts[i], deltaTime);
                if (_blasts[i].Age < 1) continue;
                Free(_blasts[i].Layer);
                _blasts.RemoveAt(i);
            }
            for (int i = _scars.Count - 1; i >= 0; i--)
            {
                var scar = _scars[i];
                scar.Age += Feel.SkillFxHold >= 0 ? 0 : deltaTime;
                if (scar.Age >= scar.Life)
                {
                    Free(scar.Layer);
                    _scars.RemoveAt(i);
                    continue;
                }
                scar.Layer.Renderer.color = new Color(1, 1, 1, Mathf.Clamp01((scar.Life - scar.Age) / 0.4f));
            }
        }

        // A blow lights the dark around it for a moment, in its warning's colour.
        void AddBlastLights()
        {
            for (int i = _blastLights.Count - 1; i >= 0; i--)
            {
                var light = _blastLights[i];
                float age = Time.time - light.Born;
                if (age >= BlastLightSeconds || age < 0)
                {
                    _blastLights.RemoveAt(i);
                    continue;
                }
                _lights.Add(new LightSource(light.At, light.Radius, 0.9f * (1 - age / BlastLightSeconds), light.Color, tints: true));
            }
        }
    }
}
